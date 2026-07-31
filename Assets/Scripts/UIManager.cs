using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    public class UIManager : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private TextMeshProUGUI playerScoreText;
        [SerializeField] private TextMeshProUGUI jethalalScoreText;
        [SerializeField] private RectTransform playerCollectionPoint;
        [SerializeField] private RectTransform jethalalCollectionPoint;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private float levelDuration = 60f;

        public RectTransform PlayerCollectionPoint => playerCollectionPoint;
        public RectTransform JethalalCollectionPoint => jethalalCollectionPoint;

        private int playerScore;
        private int jethalalScore;
        private float remainingTime;
        private bool timerRunning;

        private void Start()
        {
            GameManager.Instance.OnLevelStart += OnLevelStart;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnLevelStart -= OnLevelStart;
        }

        private void Update()
        {
            if (!timerRunning) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                timerRunning = false;
                UpdateTimerText();
                EndLevel();
            }
            else
            {
                UpdateTimerText();
            }
        }

        private void OnLevelStart()
        {
            playerScore = 0;
            jethalalScore = 0;
            UpdateScoreTexts();

            remainingTime = levelDuration;
            timerRunning = true;
            UpdateTimerText();
        }

        public void AddPlayerScore(int amount)
        {
            playerScore += amount;
            UpdateScoreTexts();
            PunchScore(playerScoreText);
        }

        public void AddJethalalScore(int amount)
        {
            jethalalScore += amount;
            UpdateScoreTexts();
            PunchScore(jethalalScoreText);
        }

        private void EndLevel()
        {
            if (playerScore > jethalalScore) Debug.Log("Player won");
            else if (playerScore < jethalalScore) Debug.Log("Player lost");
            else Debug.Log("Draw");

            GameManager.Instance.InvokeLevelWin();
        }

        private void PunchScore(TextMeshProUGUI scoreText)
        {
            if (scoreText == null) return;
            scoreText.transform.DOKill();
            scoreText.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, vibrato: 1, elasticity: 0.5f);
        }

        private void UpdateScoreTexts()
        {
            if (playerScoreText != null) playerScoreText.text = playerScore.ToString();
            if (jethalalScoreText != null) jethalalScoreText.text = jethalalScore.ToString();
        }

        private void UpdateTimerText()
        {
            if (timerText != null) timerText.text = Mathf.CeilToInt(remainingTime).ToString();
        }
    }
}
