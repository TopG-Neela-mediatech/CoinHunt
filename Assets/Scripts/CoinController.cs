using System;
using System.Collections;
using AssetKits.ParticleImage;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.CoinHunt
{
    [RequireComponent(typeof(Button))]
    public class CoinController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button coinButton;
        [SerializeField] private Image coinImage;     
       // [SerializeField] private AudioSource audioSource;

       /* [Header("Audio")]
        [SerializeField] private AudioClip correctCoinClip;
        [SerializeField] private AudioClip incorrectCoinClip;*/

        [Header("Animation")]
        [SerializeField] private float spawnDuration = 0.35f;
        [SerializeField] private float collectDuration = 0.4f;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeStrength = 12f;

        [Header("Lifetime")]
        [SerializeField] private float lifetimeDuration = 6f;
        [SerializeField] private float expireDuration = 0.25f;
        [SerializeField] private float jethalalGraceDuration = 1f;

        private CoinType coinType;
        private bool isConsumed;
        private Coroutine lifetimeRoutine;
        private float lifetimeRemaining;
        private bool lifetimePaused;
        private float jethalalEligibleAt;
        private Tween glowStopTween;

        public CoinType CoinType => coinType;

        // The player can always tap immediately; Jethalal has to wait out this grace period first,
        // so a rupee coin doesn't get auto-collected by the AI the instant it spawns.
        public bool IsEligibleForJethalal => Time.time >= jethalalEligibleAt;

        // Raised when this coin times out uncollected, so LevelManager can remove it and spawn a replacement.
        public event Action<CoinController> OnExpired;
        // Raised once this coin's lifecycle (collect/expire animation) is fully finished, so LevelManager can pool it instead of destroying it.
        public event Action<CoinController> OnReleased;

        private void Awake()
        {
            if (coinButton == null) coinButton = GetComponent<Button>();
            if (coinImage == null) coinImage = GetComponent<Image>();
            coinButton.onClick.AddListener(OnCoinClicked);
        }
        // Called by LevelManager on every rupee coin currently on screen when the player taps a wrong coin.
       
        // The 2s auto-stop above isn't tied to this coin's lifecycle, so anything that ends this coin's
        // active life (collected, expired, force-released, or reused from the pool) must call this directly —
        // otherwise a still-playing glow can carry over onto a pooled coin reused as a different type.
     
        public void Setup(CoinType type, Sprite sprite)
        {
            coinType = type;
            isConsumed = false;
            coinButton.interactable = true;
            if (coinImage != null) coinImage.sprite = sprite;
          

            jethalalEligibleAt = Time.time + jethalalGraceDuration;

            lifetimeRemaining = lifetimeDuration;
            lifetimePaused = false;
            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = StartCoroutine(LifetimeRoutine());
        }

        // Used by TutorialController to freeze despawning while a coin is being demonstrated/held for the player.
        public void PauseLifetime() => lifetimePaused = true;
        public void ResumeLifetime() => lifetimePaused = false;

        private IEnumerator LifetimeRoutine()
        {
            while (lifetimeRemaining > 0f)
            {
                if (!lifetimePaused) lifetimeRemaining -= Time.deltaTime;
                yield return null;
            }
            Expire();
        }

        private void Expire()
        {
            if (isConsumed) return;
            isConsumed = true;
            coinButton.interactable = false;
          

            OnExpired?.Invoke(this);

            transform.DOKill();
            transform.DOScale(0f, expireDuration).SetEase(Ease.InBack).OnComplete(Release);
        }

        // Immediately hands this coin back to the pool, skipping any in-progress animation.
        // Used when the level ends so leftover coins don't leave dangling tweens behind.
        public void ForceRelease()
        {
            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);
            transform.DOKill();
            isConsumed = true;
           
            Release();
        }

        private void Release()
        {
            OnReleased?.Invoke(this);
        }

        public void PlaySpawnAnimation()
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, spawnDuration).SetEase(Ease.OutBack, 0.7f);
        }

        // Used by TutorialController to freeze every coin except the one it's demonstrating on.
        public void SetInteractable(bool interactable)
        {
            if (!isConsumed) coinButton.interactable = interactable;
        }

        private bool IsTargetType()
        {
            return GameManager.Instance.LevelManager != null && GameManager.Instance.LevelManager.CurrentTargetType == coinType;
        }

        private void OnCoinClicked()
        {
            if (isConsumed) return;

            if (IsTargetType()) Collect();
            else PlayIncorrectFeedback();
        }

        private void Collect()
        {
            isConsumed = true;
            coinButton.interactable = false;
            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);

           // PlaySfx(correctCoinClip);
            if (GameManager.Instance.UIManager == null)
                Debug.LogWarning("CoinController: GameManager's UIManager reference is not assigned — score will not update.");
            GameManager.Instance.UIManager?.AddPlayerScore(1);
           GameManager.Instance.SoundManager.PlaySFX(sfxEnum.Correct);
            GameManager.Instance.JethalalController?.ReactToPlayerScore();

            Vector3 targetPosition = GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.PlayerPiggyBank != null
                ? GameManager.Instance.UIManager.PlayerPiggyBank.position
                : transform.position;

            PlayCollectAnimation(targetPosition, () =>
            {
                GameManager.Instance.UIManager?.BouncePlayerPiggyBank();
            });
        }

        // Called by JethalalController (via LevelManager) when he grabs this coin before the player does.
        // Returns false if the coin is no longer available or isn't the current target type.
        public bool TryJethalalCollect()
        {
            if (isConsumed || !IsTargetType() || !IsEligibleForJethalal) return false;

            isConsumed = true;
            coinButton.interactable = false;
            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);

            if (GameManager.Instance.UIManager == null)
                Debug.LogWarning("CoinController: GameManager's UIManager reference is not assigned — Jethalal's score will not update.");
            GameManager.Instance.UIManager?.AddJethalalScore(1);

            Vector3 targetPosition = GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.JethalalPiggyBank != null
                ? GameManager.Instance.UIManager.JethalalPiggyBank.position
                : transform.position;

            PlayCollectAnimation(targetPosition, () =>
            {
                GameManager.Instance.UIManager?.BounceJethalalPiggyBank();
            });
            return true;
        }

        // onArrived fires once the coin visually reaches the piggy bank — target rotation is deferred until
        // then (rather than the instant the coin is claimed) so a quick second tap of the same type mid-flight
        // isn't rejected as "wrong" by a target that already changed.
        private void PlayCollectAnimation(Vector3 targetPosition, Action onArrived)
        {
            Sequence collectSequence = DOTween.Sequence();
            collectSequence.Append(transform.DOScale(1.15f, 0.1f));
            collectSequence.Append(transform.DOMove(targetPosition, collectDuration).SetEase(Ease.InBack));
            collectSequence.Join(transform.DOScale(0f, collectDuration).SetEase(Ease.InBack));
            collectSequence.OnComplete(() =>
            {
                onArrived?.Invoke();
                Release();
            });
        }

        private void PlayIncorrectFeedback()
        {
            //  PlaySfx(incorrectCoinClip);
            GameManager.Instance.SoundManager.PlaySFX(sfxEnum.Incorrect);
            transform.DOKill();
            transform.DOShakePosition(shakeDuration, shakeStrength, vibrato: 10, randomness: 90, fadeOut: true);
            GameManager.Instance.UIManager?.ShakeTargetIndicator();
        }

        private void PlaySfx(AudioClip clip)
        {
           // if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
