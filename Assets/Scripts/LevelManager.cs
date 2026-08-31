using StorySystem.Story.CoinHunt;
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
        // Coin prefab's on-screen size — keeps spawn positions inside coinSpawnArea and is the
        // basis for minCoinSpacing below (must exceed this or coins will visually overlap).
        [SerializeField] private Vector2 coinSize = new Vector2(200f, 200f);
        [SerializeField] private float minCoinSpacing = 240f;
        [SerializeField] private float expiredCoinRespawnDelay = 0.4f;
        // How many random positions to try before giving up on spawning this tick (board's just full).
        private const int MaxSpawnPositionAttempts = 10;

        [Header("Target Rotation")]
        // How long a coin type stays as the indicator's target before rotating to a different one.
        [SerializeField] private float targetActiveDuration = 10f;
        // How long every active coin takes to scale out when the target changes and the board clears.
        [SerializeField] private float boardClearDuration = 0.5f;

        private bool isStoryPlayed;
        private bool spawningPaused;
        private readonly List<GameObject> activeCoins = new List<GameObject>();
        private readonly Queue<GameObject> coinPool = new Queue<GameObject>();
        private Coroutine spawnRoutine;
        private Coroutine targetRotationRoutine;
        private Coroutine targetChangeTransitionRoutine;
        private Coroutine levelStartRoutine;

        // The currency the indicator currently shows — the only type that scores when tapped/grabbed.
        public CoinType CurrentTargetType { get; private set; }

        // True once the indicator's entrance animation (played at level start) has fully settled.
        // TutorialController waits on this so its own hand/coin reveal doesn't visually clash with
        // the indicator still flying in.
        public bool IsIndicatorEntranceComplete { get; private set; }

        // Used by TutorialController to freeze the board (no new spawns) while the one-time tutorial plays.
        public void PauseSpawning() => spawningPaused = true;
        public void ResumeSpawning() => spawningPaused = false;

        // Used by TutorialController so the target can't rotate out from under the coin it's demonstrating —
        // without this, a slow tutorial tap would leave the highlighted coin permanently failing
        // IsTargetType() once the 10s timer fired, making it impossible to ever collect.
        public void PauseTargetRotation()
        {
            if (targetRotationRoutine != null) StopCoroutine(targetRotationRoutine);
            targetRotationRoutine = null;
        }
        public void ResumeTargetRotation() => ScheduleNextRotation();

        // Lets other scripts (TutorialController) inspect what's currently on screen without exposing the list itself.
        public IEnumerable<CoinController> GetActiveCoinControllers()
        {
            foreach (GameObject coin in activeCoins)
            {
                if (coin == null) continue;
                CoinController controller = coin.GetComponent<CoinController>();
                if (controller != null) yield return controller;
            }
        }

        [Serializable]
        private struct CoinSpriteMapping
        {
            public CoinType type;
            public Sprite sprite;
        }

        public void StartLevel() => GameManager.Instance.InvokeLevelStart();


        private void Awake()
        {
            playSchoolBackButton.onClick.AddListener(() => SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu));
        }
        private void Start()
        {
            isStoryPlayed = false;
            GameManager.Instance.OnLevelStart += OnLevelStart;
            GameManager.Instance.OnLevelWin += OnLevelWin;
            StartCoroutine(StartLevelNextFrame());
           /* if (sc != null)
            {
                sc.OnStoryFinished += OnStoryFinished;
            }
            StartStory();*/
        }
        private IEnumerator StartLevelNextFrame()
        {
            // Unity doesn't guarantee Start() order across scripts — wait a frame so UIManager and
            // JethalalController have already subscribed to OnLevelStart before it fires, otherwise
            // things like the timer silently never start.
            yield return null;
            StartLevel();
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
            CurrentTargetType = CoinType.Rupee;
            IsIndicatorEntranceComplete = false;

            // PlayIntro only actually plays (and returns a nonzero wait) the very first time ever —
            // every restart after that returns 0 and this proceeds immediately.
            float introDelay = GameManager.Instance.SoundManager.PlayIntro();
            if (levelStartRoutine != null) StopCoroutine(levelStartRoutine);
            levelStartRoutine = StartCoroutine(BeginLevelAfterIntro(introDelay));
        }
        // Waits out the one-time intro voice line before doing anything else — previously the
        // currency cue and the indicator's visual entrance (and thus coin spawning) started
        // immediately in parallel with the intro clip, so coins were already tappable while it was
        // still playing.
        private IEnumerator BeginLevelAfterIntro(float introDelay)
        {
            if (introDelay > 0f) yield return new WaitForSeconds(introDelay);

            GameManager.Instance.SoundManager.PlayCurrencyIntro(CurrentTargetType);

            // Coin spawning (and the rotation timer) don't start until the indicator has fully
            // settled — every level start (first time or a restart) should show the indicator
            // animation on an empty board first, then coins pop in, not both happening at once.
            UpdateTargetIndicator(() =>
            {
                IsIndicatorEntranceComplete = true;
                // Guard against OnLevelStart somehow firing more than once for a single level —
                // without this, a second call would leave two SpawnCoinsRoutine coroutines running
                // in parallel.
                if (spawnRoutine != null) StopCoroutine(spawnRoutine);
                spawnRoutine = StartCoroutine(SpawnCoinsRoutine());
                ScheduleNextRotation();
            });
        }
        private void OnLevelWin()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;
            if (targetRotationRoutine != null) StopCoroutine(targetRotationRoutine);
            targetRotationRoutine = null;
            if (targetChangeTransitionRoutine != null) StopCoroutine(targetChangeTransitionRoutine);
            targetChangeTransitionRoutine = null;
            if (levelStartRoutine != null) StopCoroutine(levelStartRoutine);
            levelStartRoutine = null;

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
        // Target rotates purely on this fixed timer now — no more collection- or absence-triggered
        // early changes, which were causing it to rotate far more often than targetActiveDuration.
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
        // Picks a new target type, preferring one actually present among activeCoins right now so the
        // indicator points at something visible when possible. Falls back to any different random
        // type when nothing else is currently spawned. The actual board-clear/showcase/resume sequence
        // runs in PlayTargetChangeTransition — this only decides WHAT the next target is.
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

            // Tracked so OnLevelWin can cancel this if the level ends mid-transition — otherwise its
            // remaining steps (including a PlayCurrencyIntro sound call) kept running after the
            // win/lose panel appeared, stepping on the win/lose audio.
            if (targetChangeTransitionRoutine != null) StopCoroutine(targetChangeTransitionRoutine);
            targetChangeTransitionRoutine = StartCoroutine(PlayTargetChangeTransition());
        }
        // Clears every coin currently on screen (scaled out, not instantly destroyed), waits for that
        // to finish, then plays the indicator's "new target" showcase, and only resumes spawning once
        // that's fully settled. The next rotation isn't scheduled until this whole sequence completes,
        // so targetActiveDuration always measures from when the new target actually finished appearing.
        private IEnumerator PlayTargetChangeTransition()
        {
            spawningPaused = true;

            foreach (CoinController coin in GetActiveCoinControllers())
            {
                coin.PlayClearAnimation(boardClearDuration);
            }

            yield return new WaitForSeconds(boardClearDuration);

            bool indicatorSettled = false;
            UpdateTargetIndicator(() => indicatorSettled = true);
            GameManager.Instance.SoundManager.PlayCurrencyIntro(CurrentTargetType);

            yield return new WaitUntil(() => indicatorSettled);

            spawningPaused = false;
            ScheduleNextRotation();
        }
        private void UpdateTargetIndicator(Action onComplete = null)
        {
            Sprite sprite = GetSpriteFor(CurrentTargetType);
            if (sprite == null)
            {
                Debug.LogWarning($"LevelManager: no sprite mapped for CoinType.{CurrentTargetType} — target indicator won't update.");
                onComplete?.Invoke();
                return;
            }
            if (GameManager.Instance.UIManager == null)
            {
                Debug.LogWarning("LevelManager: GameManager's UIManager reference is not assigned — target indicator will not update.");
                onComplete?.Invoke();
                return;
            }
            GameManager.Instance.UIManager.SetTargetIndicator(sprite, onComplete);
        }
      
     
        private IEnumerator SpawnCoinsRoutine()
        {
            while (true)
            {
                activeCoins.RemoveAll(coin => coin == null);

                if (!spawningPaused && activeCoins.Count < maxActiveCoins) SpawnCoin();

                yield return new WaitForSeconds(UnityEngine.Random.Range(spawnIntervalRange.x, spawnIntervalRange.y));
            }
        }
        private void SpawnCoin()
        {
            if (!TryGetSpawnPosition(out Vector2 position)) return;
            SpawnCoinAt(position, GetSpawnTypeGuaranteeingTarget());
        }
        // A stale coin expired: replace it at the same spot so the board stays full.
        // Note: the coin stays in activeCoins (still occupying its spot for overlap purposes) until
        // HandleCoinReleased fires once its despawn animation actually finishes — see that method.
        private void HandleCoinExpired(CoinController expiredCoin)
        {
            Vector2 position = expiredCoin.GetComponent<RectTransform>().anchoredPosition;
            CoinType expiredType = expiredCoin.CoinType;

            // Wait a beat instead of spawning the replacement the instant the old coin starts its despawn
            // animation, so the new one doesn't pop in right on top of the one still shrinking away.
            StartCoroutine(SpawnCoinAfterDelay(position, expiredType, expiredCoinRespawnDelay));
        }
        private IEnumerator SpawnCoinAfterDelay(Vector2 position, CoinType expiredType, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (spawnRoutine == null || spawningPaused) yield break; // level ended, or board is frozen (e.g. tutorial)
            // The periodic SpawnCoinsRoutine may have already refilled the gap this coin left behind
            // by the time this delay elapses — without this check the two paths compound over a
            // session and the board slowly overshoots maxActiveCoins.
            if (activeCoins.Count >= maxActiveCoins) yield break;
            // Another coin may have since spawned near the vacated spot — fall back to a freshly
            // validated position instead of forcing an overlap at the stale stored one.
            if (!IsPositionClear(position) && !TryGetSpawnPosition(out position)) yield break;
            SpawnCoinAt(position, GetSpawnTypeGuaranteeingTarget(expiredType));
        }
        // Picks the type for a new/replacement coin. If the current target type has completely
        // disappeared from the board, force it — so the player is never stuck waiting on a random
        // chance for the coin they actually need to reappear. Otherwise pick randomly (excluding the
        // type that just left this spot, if any, so the same currency doesn't just reappear in place).
        private CoinType GetSpawnTypeGuaranteeingTarget(CoinType? excludeType = null)
        {
            if (!IsTargetTypePresent()) return CurrentTargetType;
            return excludeType.HasValue ? GetRandomCoinType(excludeType.Value) : GetRandomCoinType();
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
        private static readonly CoinType[] AllCoinTypes =
        {
            CoinType.Rupee, CoinType.Dollar, CoinType.Euro, CoinType.Pound, CoinType.Yen
        };
        private CoinType GetRandomCoinType()
        {
            return AllCoinTypes[UnityEngine.Random.Range(0, AllCoinTypes.Length)];
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
        // Returns false if no clear spot was found within MaxSpawnPositionAttempts (board is too full right now).
        private bool TryGetSpawnPosition(out Vector2 position)
        {
            for (int attempt = 0; attempt < MaxSpawnPositionAttempts; attempt++)
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
