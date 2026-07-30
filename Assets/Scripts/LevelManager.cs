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
        [SerializeField] private int maxActiveCoins = 8;
        [SerializeField, Range(0f, 1f)] private float rupeeSpawnChance = 0.35f;
        [SerializeField] private float minCoinSpacing = 160f;
        [SerializeField] private int maxSpawnPositionAttempts = 10;

        private readonly List<GameObject> activeCoins = new List<GameObject>();
        private Coroutine spawnRoutine;

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
            GameManager.Instance.OnLevelStart += OnLevelStart;
            GameManager.Instance.OnLevelWin += OnLevelWin;
            if (sc != null)
            {
                sc.OnStoryFinished += OnStoryFinished;
            }
            StartCoroutine(StartLevelNextFrame());
        }
        private IEnumerator StartLevelNextFrame()
        {
            // Unity doesn't guarantee Start() order across scripts, so firing here
            // ensures UIManager/JethalalController have already subscribed in their own Start().
            yield return null;
            StartLevel();
        }
        private void OnStoryFinished()
        {
            storyPrefab.SetActive(false);         
        }
        private void OnLevelStart()
        {
            spawnRoutine = StartCoroutine(SpawnCoinsRoutine());
        }
        private void OnLevelWin()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;

            foreach (GameObject coin in activeCoins)
            {
                if (coin != null) Destroy(coin);
            }
            activeCoins.Clear();
        }
        // Called by JethalalController so he competes for the same coins on screen instead of scoring independently.
        // Returns false (and Jethalal scores nothing) if no rupee coin is currently spawned.
        public bool TryCollectRandomRupeeCoinForJethalal()
        {
            activeCoins.RemoveAll(coin => coin == null);

            List<CoinController> rupeeCoins = new List<CoinController>();
            foreach (GameObject coin in activeCoins)
            {
                CoinController controller = coin.GetComponent<CoinController>();
                if (controller != null && controller.CoinType == CoinType.Rupee) rupeeCoins.Add(controller);
            }

            if (rupeeCoins.Count == 0) return false;

            CoinController chosen = rupeeCoins[UnityEngine.Random.Range(0, rupeeCoins.Count)];
            return chosen.TryJethalalCollect();
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
        private void HandleCoinExpired(CoinController expiredCoin)
        {
            expiredCoin.OnExpired -= HandleCoinExpired;
            Vector2 position = expiredCoin.GetComponent<RectTransform>().anchoredPosition;
            CoinType replacementType = GetRandomCoinType(expiredCoin.CoinType);

            activeCoins.Remove(expiredCoin.gameObject);
            SpawnCoinAt(position, replacementType);
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

            GameObject coinObject = Instantiate(coinPrefab, coinParent != null ? coinParent : coinSpawnArea);
            RectTransform coinRect = coinObject.GetComponent<RectTransform>();
            if (coinRect != null) coinRect.anchoredPosition = position;

            CoinController controller = coinObject.GetComponent<CoinController>();
            controller.Setup(type, sprite);
            controller.PlaySpawnAnimation();
            controller.OnExpired += HandleCoinExpired;

            activeCoins.Add(coinObject);
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
            Vector2 halfSize = coinSpawnArea.rect.size * 0.5f;
            return new Vector2(UnityEngine.Random.Range(-halfSize.x, halfSize.x), UnityEngine.Random.Range(-halfSize.y, halfSize.y));
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
