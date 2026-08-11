using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TMKOC.CoinHunt
{
    public class UIManager : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private TextMeshProUGUI playerScoreText;
        [SerializeField] private TextMeshProUGUI jethalalScoreText;
        [FormerlySerializedAs("playerCollectionPoint")]
        [SerializeField] private RectTransform playerPiggyBank;
        [FormerlySerializedAs("jethalalCollectionPoint")]
        [SerializeField] private RectTransform jethalalPiggyBank;

        [Header("Target Indicator")]
        [SerializeField] private Image targetIndicatorImage;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private float levelDuration = 60f;

        public RectTransform PlayerPiggyBank => playerPiggyBank;
        public RectTransform JethalalPiggyBank => jethalalPiggyBank;

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

        // Used by TutorialController to freeze the countdown while the one-time tutorial plays.
        public void PauseTimer() => timerRunning = false;
        public void ResumeTimer() => timerRunning = true;

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

        // Called by LevelManager whenever the coin type to look for changes.
        public void SetTargetIndicator(Sprite sprite)
        {
            if (targetIndicatorImage == null) return;
            targetIndicatorImage.sprite = sprite;
            targetIndicatorImage.transform.DOKill();
            //targetIndicatorImage.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, vibrato: 1, elasticity: 0.6f);
            targetIndicatorImage.transform.DOLocalMoveY(300f, 0f).OnComplete(() =>
            {
                targetIndicatorImage.transform.DOLocalMoveY(0f, 0.5f).SetEase(Ease.OutBack, 0.7f);
            });
        }

        // Called by CoinController when the player taps a wrong coin, as a reminder of what to look for.
        public void ShakeTargetIndicator()
        {
            if (targetIndicatorImage == null) return;
            targetIndicatorImage.transform.DOKill();
            targetIndicatorImage.transform.DOShakePosition(0.3f, 12f, vibrato: 10, randomness: 90, fadeOut: true);
        }

        // Called by CoinController once a collected coin's fly-to animation finishes arriving at the piggy bank.
        public void BouncePlayerPiggyBank() => BouncePiggyBank(playerPiggyBank);
        public void BounceJethalalPiggyBank() => BouncePiggyBank(jethalalPiggyBank);

        private void BouncePiggyBank(RectTransform piggyBank)
        {
            if (piggyBank == null) return;
            piggyBank.DOKill();
            piggyBank.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 1, elasticity: 0.6f);
        }

        private void EndLevel()
        {
            // Ties go to the player — there's no draw outcome, only win or lose.
            if (playerScore >= jethalalScore)
            {
                GameManager.Instance.EndPanelScript.ShowWin();
                GameManager.Instance.SoundManager.PlayPlayerWin();
            }
            else
            {
                GameManager.Instance.EndPanelScript.ShowLose();
                GameManager.Instance.SoundManager.PlayJethaWin();
            }
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
            if (timerText == null) return;

            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
