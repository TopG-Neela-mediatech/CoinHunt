using System.Collections;
using AssetKits.ParticleImage;
using DG.Tweening;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    public class JethalalController : MonoBehaviour
    {
        [Header("AI Speed")]
        [SerializeField] private Vector2 collectIntervalRange = new Vector2(1.5f, 3f);

        [Header("Reactions")]
        [SerializeField] private RectTransform jethalalVisual;
        [SerializeField] private float collectPunchScale = 0.12f;
        [SerializeField] private float collectPunchDuration = 0.3f;
        [SerializeField] private float surprisedPunchAngle = 8f;
        [SerializeField] private float surprisedPunchDuration = 0.25f;
        private Coroutine collectRoutine;
        private bool isCollecting;
        private bool isPaused;

        // Used by TutorialController to stop Jethalal from sniping the guaranteed coin mid-tutorial,
        // without tearing down and restarting the whole collect coroutine.
        public void PauseCollecting() => isPaused = true;
        public void ResumeCollecting() => isPaused = false;

        private void Start()
        {
            GameManager.Instance.OnLevelStart += StartCollecting;
            GameManager.Instance.OnLevelWin += StopCollecting;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnLevelStart -= StartCollecting;
            GameManager.Instance.OnLevelWin -= StopCollecting;
        }

        private void StartCollecting()
        {
            // Guard against OnLevelStart somehow firing more than once for a single level — without
            // this, a second call would leave two CollectRoutine coroutines running in parallel.
            if (collectRoutine != null) StopCoroutine(collectRoutine);

            isCollecting = true;
            collectRoutine = StartCoroutine(CollectRoutine());
        }

        private void StopCollecting()
        {
            isCollecting = false;
            if (collectRoutine != null) StopCoroutine(collectRoutine);
            collectRoutine = null;
        }

        private IEnumerator CollectRoutine()
        {
            while (isCollecting)
            {
                yield return new WaitForSeconds(Random.Range(collectIntervalRange.x, collectIntervalRange.y));
                if (!isCollecting) yield break;
                if (isPaused) continue;

                if (GameManager.Instance.LevelManager == null)
                {
                    Debug.LogWarning("JethalalController: GameManager's LevelManager reference is not assigned.");
                    continue;
                }

                bool collected = GameManager.Instance.LevelManager.TryCollectRandomTargetCoinForJethalal();
                if (collected) PlayCollectReaction();
            }
        }

        private void PlayCollectReaction()
        {
            if (jethalalVisual == null) return;
            jethalalVisual.DOKill();
            jethalalVisual.DOPunchScale(Vector3.one * collectPunchScale, collectPunchDuration, vibrato: 1, elasticity: 0.5f);
        }
        public void ReactToPlayerScore()
        {
            if (jethalalVisual == null) return;
            jethalalVisual.DOKill();
            jethalalVisual.DOPunchRotation(new Vector3(0, 0, surprisedPunchAngle), surprisedPunchDuration, vibrato: 6, elasticity: 0.6f);
        }
    }
}
