using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    // Plays a one-time "tap this coin" lesson the first time a level starts: freezes the timer,
    // Jethalal, and coin spawning, makes every coin but one non-interactable, and loops a hand
    // tapping animation on the one correct coin until the player taps it — then resumes normally.
    public class TutorialController : MonoBehaviour
    {
        [Header("References")]
        // Disabled by default in the scene — this script enables/disables it.
        [SerializeField] private RectTransform handImage;

        [Header("Hand Animation")]
        // How far the fingertip presses down (from its resting position, capped at the coin's
        // vertical center) into the coin for a visible tap motion each cycle.
        [SerializeField] private float tapPressDepth = 30f;
        [SerializeField] private float tapMoveDuration = 0.4f;
        [SerializeField] private float tapHoldDelay = 0.15f;
        // How long the hand takes to pop in (scale 0 -> 1) once revealed alongside the coin.
        [SerializeField] private float handRevealDuration = 0.35f;

        [Header("Setup")]
        // How long to keep checking for a target-type coin to appear before giving up on the tutorial.
        [SerializeField] private float findCoinPollInterval = 0.15f;
        [SerializeField] private float findCoinTimeout = 5f;

        private bool hasPlayedTutorial;
        private CoinController tutorialCoin;
        private Sequence handSequence;

        private void Start()
        {
            GameManager.Instance.OnLevelStart += OnLevelStart;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnLevelStart -= OnLevelStart;
        }

        private void OnLevelStart()
        {
            if (hasPlayedTutorial) return;
            hasPlayedTutorial = true;
            StartCoroutine(RunTutorial());
        }

        private IEnumerator RunTutorial()
        {
            // Let the board spawn normally first — don't touch spawning/timer/Jethalal until we
            // actually have a coin to freeze on, otherwise pausing spawning up front means the
            // very first coin (the one we're waiting for) never gets a chance to appear.
            CoinController correctCoin = null;
            float elapsed = 0f;
            while (correctCoin == null && elapsed < findCoinTimeout)
            {
                correctCoin = FindTargetTypeCoin();
                if (correctCoin != null) break;

                yield return new WaitForSeconds(findCoinPollInterval);
                elapsed += findCoinPollInterval;
            }

            if (correctCoin == null)
            {
                Debug.LogWarning("TutorialController: no target-type coin appeared in time — skipping tutorial.");
                yield break;
            }

            // Freeze everything the instant the coin is found — timer, Jethalal, new spawns, target
            // rotation, and every active coin's own despawn timer. This must happen right away, not
            // after the indicator-wait below, otherwise another coin spawns in during that ~1.5s wait
            // (well past the normal spawn interval) before spawning actually gets paused.
            GameManager.Instance.UIManager?.PauseTimer();
            GameManager.Instance.JethalalController?.PauseCollecting();
            GameManager.Instance.LevelManager?.PauseSpawning();
            GameManager.Instance.LevelManager?.PauseTargetRotation();
            FreezeBoardExcept(correctCoin);

            tutorialCoin = correctCoin;
            tutorialCoin.OnReleased += OnTutorialCoinCollected;

            // Hide the coin and hand — the target indicator's own entrance animation may still be
            // playing at this point in level start, and revealing the tutorial on top of it looked
            // cluttered. Both reveal together, matching pop, once it's fully settled.
            // DOKill first: the coin's own spawn-in tween may still be running, and without killing it
            // that tween would just overwrite this scale-0 on its very next update.
            correctCoin.transform.DOKill();
            correctCoin.transform.localScale = Vector3.zero;
            if (handImage != null)
            {
                handImage.gameObject.SetActive(true);
                handImage.DOKill();
                handImage.localScale = Vector3.zero;
            }

            LevelManager levelManagerRef = GameManager.Instance.LevelManager;
            while (levelManagerRef != null && !levelManagerRef.IsIndicatorEntranceComplete)
            {
                yield return null;
            }

            RevealAndPlayHandAnimation(correctCoin);
        }

        private CoinController FindTargetTypeCoin()
        {
            LevelManager levelManager = GameManager.Instance.LevelManager;
            if (levelManager == null) return null;

            foreach (CoinController coin in levelManager.GetActiveCoinControllers())
            {
                if (coin.CoinType == levelManager.CurrentTargetType) return coin;
            }
            return null;
        }

        // Disables interaction on every coin except the one being demonstrated, and pauses every
        // active coin's own despawn timer so none of them (including the demo coin) time out
        // while the player is still figuring out the tutorial.
        private void FreezeBoardExcept(CoinController exceptCoin)
        {
            LevelManager levelManager = GameManager.Instance.LevelManager;
            if (levelManager == null) return;

            foreach (CoinController coin in levelManager.GetActiveCoinControllers())
            {
                coin.SetInteractable(coin == exceptCoin);
                coin.PauseLifetime();
            }
        }

        // Reveals the coin (via its normal spawn pop) and the hand (scale 0 -> 1) together, then
        // starts the tap loop once the hand has finished popping in. Assumes handImage shares the
        // coin's parent so anchored positions line up directly.
        private void RevealAndPlayHandAnimation(CoinController coin)
        {
            coin.PlaySpawnAnimation();

            if (handImage == null) return;

            RectTransform coinRect = coin.GetComponent<RectTransform>();
            if (coinRect == null) return;

            // Distance from the hand's pivot up to the top of its own rect — since the finger points
            // up, this is how far the visible fingertip sits above wherever we place the pivot.
            float fingertipToPivot = handImage.rect.height * (1f - handImage.pivot.y);

            // Resting position: fingertip lands exactly at the coin's vertical center. This is the
            // highest the fingertip is ever allowed to reach — it must never go above the coin's
            // halfway point, so every other position in this animation sits at or below it.
            Vector2 restAnchor = coinRect.anchoredPosition - new Vector2(0f, fingertipToPivot);
            Vector2 pressAnchor = restAnchor - new Vector2(0f, tapPressDepth);

            handImage.SetAsLastSibling(); // render above the coin instead of behind it
            handImage.DOKill();
            handImage.anchoredPosition = restAnchor;

            handSequence = DOTween.Sequence();
            handSequence.Append(handImage.DOScale(1f, handRevealDuration).SetEase(Ease.OutBack));
            handSequence.OnComplete(() => StartTapLoop(restAnchor, pressAnchor));
        }

        // Loops the hand pressing down and releasing back onto the coin until the player taps it.
        private void StartTapLoop(Vector2 restAnchor, Vector2 pressAnchor)
        {
            handSequence = DOTween.Sequence();
            handSequence.Append(handImage.DOAnchorPos(pressAnchor, tapMoveDuration).SetEase(Ease.OutQuad));
            handSequence.AppendInterval(tapHoldDelay);
            handSequence.Append(handImage.DOAnchorPos(restAnchor, tapMoveDuration).SetEase(Ease.InQuad));
            handSequence.AppendInterval(tapHoldDelay);
            handSequence.SetLoops(-1);
        }

        private void OnTutorialCoinCollected(CoinController coin)
        {
            coin.OnReleased -= OnTutorialCoinCollected;
            tutorialCoin = null;
            EndTutorial();
        }

        private void EndTutorial()
        {
            if (handSequence != null)
            {
                handSequence.Kill();
                handSequence = null;
            }
            if (handImage != null) handImage.gameObject.SetActive(false);

            LevelManager levelManager = GameManager.Instance.LevelManager;
            if (levelManager != null)
            {
                foreach (CoinController coin in levelManager.GetActiveCoinControllers())
                {
                    coin.SetInteractable(true);
                    coin.ResumeLifetime();
                }
                levelManager.ResumeSpawning();
                levelManager.ResumeTargetRotation();
            }

            GameManager.Instance.JethalalController?.ResumeCollecting();
            GameManager.Instance.UIManager?.ResumeTimer();
        }
    }
}
