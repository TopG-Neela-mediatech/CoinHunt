using StorySystem.Story;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TMKOC.CoinHunt
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private Button playSchoolBackButton;
        [SerializeField] private GameObject storyPrefab;
        [SerializeField] private StoryController sc;

        [Header("Coin Spawning")]
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private CoinSpriteMapping[] coinSprites;
        [SerializeField] private RectTransform coinSpawnArea;
        [SerializeField] private Transform coinParent;
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.6f, 1.2f);
        [SerializeField] private int maxActiveCoins = 5;
        [SerializeField, Range(0f, 1f)] private float rupeeSpawnChance = 0.35f;
        // Coin prefab's on-screen size, used both to keep spawn positions fully inside coinSpawnArea
        // and as the basis for minCoinSpacing below (must exceed this or coins will visually overlap).
        [SerializeField] private Vector2 coinSize = new Vector2(200f, 200f);
        [SerializeField] private float minCoinSpacing = 240f;
        [SerializeField] private int maxSpawnPositionAttempts = 10;
        [SerializeField] private float expiredCoinRespawnDelay = 0.4f;

        [Header("Target Rotation")]
        // How long a coin type stays as the indicator's target before rotating to a different one.
        [SerializeField] private float targetActiveDuration = 10f;
        // Safety-net poll rate for catching a target type that's completely absent from screen
        // (separate from the scheduled rotation above — this reacts faster to a bad state).
        [SerializeField] private float targetPresenceCheckInterval = 1f;

        private bool isStoryPlayed;
        private readonly List<GameObject> activeCoins = new List<GameObject>();
        private readonly Queue<GameObject> coinPool = new Queue<GameObject>();
        private Coroutine spawnRoutine;
        private Coroutine targetWatchdogRoutine;
        private Coroutine targetRotationRoutine;

        // The currency the indicator currently shows — the only type that scores when tapped/grabbed.
        public CoinType CurrentTargetType { get; private set; }

        [Serializable]
        private struct CoinSpriteMapping
        {
            public CoinType type;
            public Sprite sprite;
        }

        private void StartLevel() => GameManager.Instance.InvokeLevelStart();


        private void Awake()
        {
            playSchoolBackButton.onClick.AddListener(() => SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu));
        }
        private void Start()
        {
            isStoryPlayed = false;
            GameManager.Instance.OnLevelStart += OnLevelStart;
            GameManager.Instance.OnLevelWin += OnLevelWin;
            if (sc != null)
            {
                sc.OnStoryFinished += OnStoryFinished;
            }
            StartStory();
        }
        private void StartStory()
        {
            if (!isStoryPlayed && sc != null)
            {
                isStoryPlayed = true;
                storyPrefab.SetActive(true);
            }
        }
        private void OnStoryFinished()
        {
            storyPrefab.SetActive(false);
            StartLevel();
        }
        private void OnLevelStart()
        {
            CurrentTargetType = GetRandomCoinType();
            UpdateTargetIndicator();
            ScheduleNextRotation();
            spawnRoutine = StartCoroutine(SpawnCoinsRoutine());
            targetWatchdogRoutine = StartCoroutine(TargetPresenceWatchdog());
        }
        private void OnLevelWin()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            if (targetWatchdogRoutine != null) StopCoroutine(targetWatchdogRoutine);
            targetWatchdogRoutine = null;
            if (targetRotationRoutine != null) StopCoroutine(targetRotationRoutine);
            targetRotationRoutine = null;

            foreach (GameObject coin in activeCoins.ToArray())
            {
                if (coin == null) continue;
                CoinController controller = coin.GetComponent<CoinController>();
                if (controller != null) controller.ForceRelease();
            }
            activeCoins.Clear();
        }
        // Called by JethalalController so he competes for the same coins on screen instead of scoring independently.
        // Returns false (and Jethalal scores nothing) if no eligible target-type coin is currently spawned.
        public bool TryCollectRandomTargetCoinForJethalal()
        {
            activeCoins.RemoveAll(coin => coin == null);

            List<CoinController> targetCoins = new List<CoinController>();
            foreach (GameObject coin in activeCoins)
            {
                CoinController controller = coin.GetComponent<CoinController>();
                // Skip coins still in their grace period so Jethalal can't snipe a coin the instant it spawns.
                if (controller != null && controller.CoinType == CurrentTargetType && controller.IsEligibleForJethalal) targetCoins.Add(controller);
            }

            if (targetCoins.Count == 0) return false;

            CoinController chosen = targetCoins[UnityEngine.Random.Range(0, targetCoins.Count)];
            return chosen.TryJethalalCollect();
        }
        // Target no longer rotates on every collection — it stays put for targetActiveDuration and only
        // changes early if it disappears from screen entirely (see HandleCoinExpired / TargetPresenceWatchdog).
        private IEnumerator RotateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            RetargetToPresentType();
        }
        private void ScheduleNextRotation()
        {
            if (targetRotationRoutine != null) StopCoroutine(targetRotationRoutine);
            targetRotationRoutine = StartCoroutine(RotateAfterDelay(targetActiveDuration));
        }
        // Safety net: if the current target's last coin expires, or the initial random pick at level start
        // doesn't match anything spawned yet, nothing else would ever re-check — so poll periodically and
        // retarget the instant the current target is no longer on screen at all.
        private IEnumerator TargetPresenceWatchdog()
        {
            while (true)
            {
                yield return new WaitForSeconds(targetPresenceCheckInterval);
                if (!IsTargetTypePresent()) RetargetToPresentType();
            }
        }
        private bool IsTargetTypePresent()
        {
            foreach (GameObject coin in activeCoins)
            {
                if (coin == null) continue;
                CoinController controller = coin.GetComponent<CoinController>();
                if (controller != null && controller.CoinType == CurrentTargetType) return true;
            }
            return false;
        }
        // Picks a new target type, preferring one actually present among activeCoins right now so the
        // indicator never points at a currency that isn't on screen. Falls back to any different random
        // type only when nothing else is currently spawned at all. Also restarts the rotation timer, so
        // an early forced change (coin disappeared) doesn't get immediately followed by the scheduled one.
        private void RetargetToPresentType()
        {
            List<CoinType> presentTypes = new List<CoinType>();
            foreach (GameObject coin in activeCoins)
            {
                if (coin == null) continue;
                CoinController controller = coin.GetComponent<CoinController>();
                if (controller != null && controller.CoinType != CurrentTargetType && !presentTypes.Contains(controller.CoinType))
                    presentTypes.Add(controller.CoinType);
            }

            CurrentTargetType = presentTypes.Count > 0
                ? presentTypes[UnityEngine.Random.Range(0, presentTypes.Count)]
                : GetRandomCoinType(CurrentTargetType);

            UpdateTargetIndicator();
            ScheduleNextRotation();
        }
        private void UpdateTargetIndicator()
        {
            Sprite sprite = GetSpriteFor(CurrentTargetType);
            if (sprite == null)
            {
                Debug.LogWarning($"LevelManager: no sprite mapped for CoinType.{CurrentTargetType} — target indicator won't update.");
                return;
            }
            if (GameManager.Instance.UIManager == null)
                Debug.LogWarning("LevelManager: GameManager's UIManager reference is not assigned — target indicator will not update.");
            GameManager.Instance.UIManager?.SetTargetIndicator(sprite);
        }
      
     
        private IEnumerator SpawnCoinsRoutine()
        {
            while (true)
            {
                activeCoins.RemoveAll(coin => coin == null);

                if (activeCoins.Count < maxActiveCoins) SpawnCoin();

                yield return new WaitForSeconds(UnityEngine.Random.Range(spawnIntervalRange.x, spawnIntervalRange.y));
            }
        }
        private void SpawnCoin()
        {
            if (!TryGetSpawnPosition(out Vector2 position)) return;
            SpawnCoinAt(position, GetRandomCoinType());
        }
        // A stale coin expired: replace it at the same spot with a different type so the board stays full.
        // Note: the expired coin removes itself from activeCoins via HandleCoinReleased once its despawn animation finishes.
        private void HandleCoinExpired(CoinController expiredCoin)
        {
            Vector2 position = expiredCoin.GetComponent<RectTransform>().anchoredPosition;
            CoinType replacementType = GetRandomCoinType(expiredCoin.CoinType);
            bool wasLastTargetCoin = expiredCoin.CoinType == CurrentTargetType;

            activeCoins.Remove(expiredCoin.gameObject);

            // Don't leave the indicator pointing at a currency that just timed out with none left on screen —
            // retarget immediately instead of waiting on the watchdog's next tick.
            if (wasLastTargetCoin && !IsTargetTypePresent()) RetargetToPresentType();

            // Wait a beat instead of spawning the replacement the instant the old coin starts its despawn
            // animation, so the new one doesn't pop in right on top of the one still shrinking away.
            StartCoroutine(SpawnCoinAfterDelay(position, replacementType, expiredCoinRespawnDelay));
        }
        private IEnumerator SpawnCoinAfterDelay(Vector2 position, CoinType type, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (spawnRoutine == null) yield break; // level already ended in the meantime
            SpawnCoinAt(position, type);
        }
        // Coin finished being collected/expired/force-released: hand it back to the pool instead of destroying it,
        // which avoids DOTween trying to touch a destroyed RectTransform after the level ends.
        private void HandleCoinReleased(CoinController releasedCoin)
        {
            activeCoins.Remove(releasedCoin.gameObject);
            releasedCoin.gameObject.SetActive(false);
            coinPool.Enqueue(releasedCoin.gameObject);
        }
        private void SpawnCoinAt(Vector2 position, CoinType type)
        {
            Sprite sprite = GetSpriteFor(type);
            if (sprite == null)
            {
                Debug.LogWarning($"LevelManager: no sprite mapped for CoinType.{type} in coinSprites — check the array in the Inspector.");
                return;
            }
            if (coinPrefab == null || coinSpawnArea == null) return;

            GameObject coinObject = GetCoinFromPool();
            coinObject.SetActive(true);

            RectTransform coinRect = coinObject.GetComponent<RectTransform>();
            if (coinRect != null) coinRect.anchoredPosition = position;

            CoinController controller = coinObject.GetComponent<CoinController>();
            controller.Setup(type, sprite);
            controller.PlaySpawnAnimation();

            activeCoins.Add(coinObject);
        }
        // Reuses a pooled coin GameObject if one is available; otherwise instantiates a fresh one
        // (subscribing its events exactly once, since the subscription persists across reuse).
        private GameObject GetCoinFromPool()
        {
            while (coinPool.Count > 0)
            {
                GameObject pooledCoin = coinPool.Dequeue();
                if (pooledCoin != null) return pooledCoin;
            }

            GameObject newCoin = Instantiate(coinPrefab, coinParent != null ? coinParent : coinSpawnArea);
            CoinController newController = newCoin.GetComponent<CoinController>();
            newController.OnExpired += HandleCoinExpired;
            newController.OnReleased += HandleCoinReleased;
            return newCoin;
        }
        private CoinType GetRandomCoinType()
        {
            if (UnityEngine.Random.value < rupeeSpawnChance) return CoinType.Rupee;

            CoinType[] nonRupeeTypes = { CoinType.Dollar, CoinType.Euro, CoinType.Pound, CoinType.Yen };
            return nonRupeeTypes[UnityEngine.Random.Range(0, nonRupeeTypes.Length)];
        }
        private CoinType GetRandomCoinType(CoinType excludeType)
        {
            CoinType type;
            do
            {
                type = GetRandomCoinType();
            } while (type == excludeType);
            return type;
        }
        private Sprite GetSpriteFor(CoinType type)
        {
            foreach (CoinSpriteMapping mapping in coinSprites)
            {
                if (mapping.type == type) return mapping.sprite;
            }
            return null;
        }
        // Picks a random point in the spawn area that isn't within minCoinSpacing of an existing coin.
        // Returns false if no clear spot was found within maxSpawnPositionAttempts (board is too full right now).
        private bool TryGetSpawnPosition(out Vector2 position)
        {
            for (int attempt = 0; attempt < maxSpawnPositionAttempts; attempt++)
            {
                Vector2 candidate = GetRandomPointInSpawnArea();
                if (IsPositionClear(candidate))
                {
                    position = candidate;
                    return true;
                }
            }
            position = Vector2.zero;
            return false;
        }
        private bool IsPositionClear(Vector2 candidate)
        {
            foreach (GameObject coin in activeCoins)
            {
                if (coin == null) continue;
                RectTransform coinRect = coin.GetComponent<RectTransform>();
                if (coinRect != null && Vector2.Distance(coinRect.anchoredPosition, candidate) < minCoinSpacing) return false;
            }
            return true;
        }
        private Vector2 GetRandomPointInSpawnArea()
        {
            // Inset by half the coin's size so a spawned coin's edges stay within coinSpawnArea
            // instead of a center placed right at the boundary clipping the coin off-screen.
            Vector2 halfArea = coinSpawnArea.rect.size * 0.5f;
            Vector2 halfCoin = coinSize * 0.5f;
            Vector2 halfRange = new Vector2(Mathf.Max(0f, halfArea.x - halfCoin.x), Mathf.Max(0f, halfArea.y - halfCoin.y));
            return new Vector2(UnityEngine.Random.Range(-halfRange.x, halfRange.x), UnityEngine.Random.Range(-halfRange.y, halfRange.y));
        }
        private IEnumerator LoadWinPanelWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
        }

        private void OnDestroy()
        {
            GameManager.Instance.OnLevelStart -= OnLevelStart;
            GameManager.Instance.OnLevelWin -= OnLevelWin;
        }
    }
}
