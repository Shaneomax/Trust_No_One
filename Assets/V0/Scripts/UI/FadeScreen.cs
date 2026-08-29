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
                    GameObject go = new GameObject("FadeScreen");
                    _instance = go.AddComponent<FadeScreen>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Fade Settings")]
        [Tooltip("Color of the full-screen fade panel (default: black)")]
        [SerializeField] public Color fadeColor = Color.black;

        [Tooltip("Default fade duration in seconds")]
        [SerializeField] public float defaultDuration = 1.0f;

        private CanvasGroup _canvasGroup;
        private bool _initialized = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Create Canvas
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Always on top

            // Canvas Scaler
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create full-screen Image
            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(transform, false);

            Image img = imgObj.AddComponent<Image>();
            img.color = fadeColor;
            img.raycastTarget = false;

            RectTransform rt = imgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Canvas Group for alpha control
            _canvasGroup = imgObj.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>
        /// Fades screen to black (alpha 0 → 1).
        /// </summary>
        public void FadeToBlack(float duration = -1f, System.Action onComplete = null)
        {
            if (_canvasGroup == null) Initialize();
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
            if (_canvasGroup == null) Initialize();
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
            if (_canvasGroup == null) Initialize();
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Instantly clears the fade panel (alpha = 0, no animation).
        /// </summary>
        public void SetClear()
        {
            if (_canvasGroup == null) Initialize();
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Fades to black, invokes action, then fades back to clear.
        /// Great for scene transitions or cutscene cuts.
        /// </summary>
        public void FadeOutAndIn(float fadeOutDur, float holdDur, float fadeInDur, System.Action onBlack = null)
        {
            if (_canvasGroup == null) Initialize();
            _canvasGroup.DOKill();
            _canvasGroup.DOFade(1f, fadeOutDur).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
            {
                onBlack?.Invoke();
                DOVirtual.DelayedCall(holdDur, () =>
                {
                    _canvasGroup.DOFade(0f, fadeInDur).SetEase(Ease.OutQuad).SetUpdate(true);
                });
            });
        }

        public bool IsFullyBlack => _canvasGroup != null && _canvasGroup.alpha >= 0.99f;
        public bool IsFullyClear => _canvasGroup != null && _canvasGroup.alpha <= 0.01f;
        public float Alpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;
    }
}
