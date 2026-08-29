using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace V0.UI
{
    /// <summary>
    /// Singleton full-screen fade manager.
    /// Call FadeScreen.Instance.FadeToBlack() / FadeFromBlack() from any script.
    /// Auto-creates its own Canvas + Image if not already present in the scene.
    /// </summary>
    public class FadeScreen : MonoBehaviour
    {
        private static FadeScreen _instance;
        public static FadeScreen Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<FadeScreen>();
                }
                return _instance;
            }
        }

        [Header("Fade Settings")]
        [Tooltip("Color of the full-screen fade panel (default: black)")]
        [SerializeField] public Color fadeColor = Color.black;

        [Tooltip("Default fade duration in seconds")]
        [SerializeField] public float defaultDuration = 1.0f;

        [Header("Serialized References (Pre-baked in Scene)")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponentInChildren<CanvasGroup>();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        /// <summary>
        /// Fades screen to black (alpha 0 → 1).
        /// </summary>
        public void FadeToBlack(float duration = -1f, System.Action onComplete = null)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null) return;

            float dur = duration < 0 ? defaultDuration : duration;
            _canvasGroup.DOKill();
            _canvasGroup.DOFade(1f, dur).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Fades screen from black (alpha 1 → 0).
        /// </summary>
        public void FadeFromBlack(float duration = -1f, System.Action onComplete = null)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null) return;

            float dur = duration < 0 ? defaultDuration : duration;
            _canvasGroup.alpha = 1f;
            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, dur).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Instantly sets screen to black (alpha = 1, no animation).
        /// </summary>
        public void SetBlack()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.DOKill();
            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Instantly clears the fade panel (alpha = 0, no animation).
        /// </summary>
        public void SetClear()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Fades to black, invokes action, then fades back to clear.
        /// Great for scene transitions or cutscene cuts.
        /// </summary>
        public void FadeOutAndIn(float fadeOutDur, float holdDur, float fadeInDur, System.Action onBlack = null)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.DOKill();
            _canvasGroup.DOFade(1f, fadeOutDur).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                onBlack?.Invoke();
                DOVirtual.DelayedCall(holdDur, () =>
                {
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.DOFade(0f, fadeInDur).SetEase(Ease.OutQuad).SetUpdate(true);
                    }
                });
            });
        }

        public bool IsFullyBlack => _canvasGroup != null && _canvasGroup.alpha >= 0.99f;
        public bool IsFullyClear => _canvasGroup != null && _canvasGroup.alpha <= 0.01f;
        public float Alpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;
    }
}
