using System;
using AssetKits.ParticleImage;
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
        [SerializeField] private ParticleImage playerPiggyConfetti;
        [SerializeField] private ParticleImage jethalalPiggyConfetti;


        [Header("Target Indicator")]
        [SerializeField] private Image targetIndicatorImage;
        // Y anchored-position the indicator appears at (toward screen center) when the target
        // changes, before settling back down into its normal resting spot.
        [SerializeField] private float indicatorCenterY = -350f;
        [SerializeField] private float indicatorAppearScale = 2f;
        [SerializeField] private float indicatorAppearDuration = 0.5f;
        [SerializeField] private float indicatorHoldDuration = 0.5f;
        [SerializeField] private float indicatorSettleDuration = 0.5f;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private float levelDuration = 60f;

        public RectTransform PlayerPiggyBank => playerPiggyBank;
        public RectTransform JethalalPiggyBank => jethalalPiggyBank;

        private int playerScore;
        private int jethalalScore;
        private float remainingTime;
        private bool timerRunning;

        // Cached once up front so every animation below always has a known-good baseline to return
        // to or restart from — rather than trusting "current" values, which could already be drifted
        // if a previous tween on the same RectTransform got interrupted mid-flight.
        private RectTransform targetIndicatorRect;
        private Vector2 targetIndicatorRestAnchoredPos;
        private Vector3 targetIndicatorRestScale = Vector3.one;
        private Vector3 playerPiggyBankRestScale = Vector3.one;
        private Vector3 jethalalPiggyBankRestScale = Vector3.one;

        private void Awake()
        {
            if (targetIndicatorImage != null)
            {
                targetIndicatorRect = targetIndicatorImage.rectTransform;
                targetIndicatorRestAnchoredPos = targetIndicatorRect.anchoredPosition;
                targetIndicatorRestScale = targetIndicatorRect.localScale;
            }
            if (playerPiggyBank != null) playerPiggyBankRestScale = playerPiggyBank.localScale;
            if (jethalalPiggyBank != null) jethalalPiggyBankRestScale = jethalalPiggyBank.localScale;
        }

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

            // Guard against any residual drift left over from a tween interrupted at the end of the
            // previous playthrough (e.g. a punch/move killed mid-flight) — always start a level clean.
            // Note: the target indicator is deliberately NOT reset here — LevelManager calls
            // SetTargetIndicator() right after this fires (same GameManager.OnLevelStart event, order
            // not guaranteed), which already does its own DOKill()+full reset before playing the
            // entrance animation. Resetting it here too raced with that: if this ran second, it killed
            // the just-started animation and snapped straight to the resting state, so the entrance
            // never appeared to play.
            if (playerPiggyBank != null)
            {
                playerPiggyBank.DOKill();
                playerPiggyBank.localScale = playerPiggyBankRestScale;
            }
            if (jethalalPiggyBank != null)
            {
                jethalalPiggyBank.DOKill();
                jethalalPiggyBank.localScale = jethalalPiggyBankRestScale;
            }
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

        // Called by LevelManager whenever the coin type to look for changes: snaps to indicatorCenterY
        // at scale 0 (X untouched), scales up to indicatorAppearScale over indicatorAppearDuration,
        // holds there for indicatorHoldDuration, then moves down to its resting Y and back to normal
        // scale over indicatorSettleDuration. onComplete fires once fully settled — LevelManager uses
        // it to know when it's safe to resume spawning coins for the new target. Always starts from
        // the snapped state fresh (not "wherever it currently is"), so an interrupted previous run of
        // this (or ShakeTargetIndicator, which shares the same RectTransform) can never leave it stuck.
        public void SetTargetIndicator(Sprite sprite, Action onComplete = null)
        {
            if (targetIndicatorImage == null || targetIndicatorRect == null)
            {
                onComplete?.Invoke();
                return;
            }

            targetIndicatorRect.DOKill();

            targetIndicatorImage.sprite = sprite;
            targetIndicatorRect.anchoredPosition = new Vector2(targetIndicatorRestAnchoredPos.x, indicatorCenterY);
            targetIndicatorRect.localScale = Vector3.zero;

            Sequence sequence = DOTween.Sequence();
            sequence.Append(targetIndicatorRect.DOScale(targetIndicatorRestScale * indicatorAppearScale, indicatorAppearDuration).SetEase(Ease.OutBack));
            sequence.AppendInterval(indicatorHoldDuration);
            sequence.Append(targetIndicatorRect.DOAnchorPosY(targetIndicatorRestAnchoredPos.y, indicatorSettleDuration).SetEase(Ease.OutQuad));
            sequence.Join(targetIndicatorRect.DOScale(targetIndicatorRestScale, indicatorSettleDuration).SetEase(Ease.OutQuad));
            sequence.OnComplete(() => onComplete?.Invoke());
        }

        // Called by CoinController when the player taps a wrong coin, as a reminder of what to look for.
        public void ShakeTargetIndicator()
        {
            if (targetIndicatorRect == null) return;
            targetIndicatorRect.DOKill();
            targetIndicatorRect.anchoredPosition = targetIndicatorRestAnchoredPos;
            targetIndicatorRect.localScale = targetIndicatorRestScale;
            targetIndicatorRect.DOShakePosition(0.3f, 12f, vibrato: 10, randomness: 90, fadeOut: true);
        }

        // Called by CoinController once a collected coin's fly-to animation finishes arriving at the piggy bank.
        public void BouncePlayerPiggyBank()
        {
            BouncePiggyBank(playerPiggyBank, playerPiggyBankRestScale);
            playerPiggyConfetti.Play();
        }
        public void BounceJethalalPiggyBank()
        {
            BouncePiggyBank(jethalalPiggyBank, jethalalPiggyBankRestScale);
            jethalalPiggyConfetti.Play();
        }

        // DOPunchScale animates relative to whatever scale the object is at when it starts. If a
        // previous punch got interrupted by DOKill() mid-flight, it froze at an enlarged in-between
        // scale, and the next punch built on top of THAT — compounding into permanent growth over a
        // session (the piggy bank visibly growing after every collect). Resetting to the cached rest
        // scale before every punch closes that off.
        private void BouncePiggyBank(RectTransform piggyBank, Vector3 restScale)
        {
            if (piggyBank == null) return;
            piggyBank.DOKill();
            piggyBank.localScale = restScale;
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
