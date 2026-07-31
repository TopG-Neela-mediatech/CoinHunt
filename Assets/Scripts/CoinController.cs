using System;
using System.Collections;
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

        private CoinType coinType;
        private bool isConsumed;
        private Coroutine lifetimeRoutine;

        public CoinType CoinType => coinType;

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

        public void Setup(CoinType type, Sprite sprite)
        {
            coinType = type;
            isConsumed = false;
            coinButton.interactable = true;
            if (coinImage != null) coinImage.sprite = sprite;

            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(lifetimeDuration);
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

        private void OnCoinClicked()
        {
            if (isConsumed) return;

            if (coinType == CoinType.Rupee) Collect();
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
            GameManager.Instance.JethalalController?.ReactToPlayerScore();

            Vector3 targetPosition = GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.PlayerCollectionPoint != null
                ? GameManager.Instance.UIManager.PlayerCollectionPoint.position
                : transform.position;

            PlayCollectAnimation(targetPosition);
        }

        // Called by JethalalController (via LevelManager) when he grabs this coin before the player does.
        // Returns false if the coin is no longer available or isn't a rupee, so the caller can try another.
        public bool TryJethalalCollect()
        {
            if (isConsumed || coinType != CoinType.Rupee) return false;

            isConsumed = true;
            coinButton.interactable = false;
            if (lifetimeRoutine != null) StopCoroutine(lifetimeRoutine);

            if (GameManager.Instance.UIManager == null)
                Debug.LogWarning("CoinController: GameManager's UIManager reference is not assigned — Jethalal's score will not update.");
            GameManager.Instance.UIManager?.AddJethalalScore(1);

            Vector3 targetPosition = GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.JethalalCollectionPoint != null
                ? GameManager.Instance.UIManager.JethalalCollectionPoint.position
                : transform.position;

            PlayCollectAnimation(targetPosition);
            return true;
        }

        private void PlayCollectAnimation(Vector3 targetPosition)
        {
            Sequence collectSequence = DOTween.Sequence();
            collectSequence.Append(transform.DOScale(1.15f, 0.1f));
            collectSequence.Append(transform.DOMove(targetPosition, collectDuration).SetEase(Ease.InBack));
            collectSequence.Join(transform.DOScale(0f, collectDuration).SetEase(Ease.InBack));
            collectSequence.OnComplete(Release);
        }

        private void PlayIncorrectFeedback()
        {
          //  PlaySfx(incorrectCoinClip);

            transform.DOKill();
            transform.DOShakePosition(shakeDuration, shakeStrength, vibrato: 10, randomness: 90, fadeOut: true);
        }

        private void PlaySfx(AudioClip clip)
        {
           // if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
