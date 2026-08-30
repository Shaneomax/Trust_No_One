using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace V0.UI
{
    /// <summary>
    /// Displays a simple, clean "Press E" prompt in pure white when the player is looking at an interactable object.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        public static InteractionPromptUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _textRect;
        [SerializeField] private Text _promptText;

        [Header("Animation Settings")]
        [SerializeField] private float _fadeDuration = 0.15f;

        private Tweener _fadeTween;
        private bool _isVisible = false;

        public static InteractionPromptUI GetOrCreate()
        {
            if (Instance != null) return Instance;

            InteractionPromptUI found = Object.FindFirstObjectByType<InteractionPromptUI>();
            if (found != null)
            {
                Instance = found;
                return Instance;
            }

            GameObject canvasObj = new GameObject("InteractionPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InteractionPromptUI));
            Instance = canvasObj.GetComponent<InteractionPromptUI>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureUIHierarchy();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void EnsureUIHierarchy()
        {
            // Destroy any old layout/badge container if it exists from previous version
            Transform oldContainer = transform.Find("PromptContainer");
            if (oldContainer != null)
            {
                Destroy(oldContainer.gameObject);
            }

            Transform textT = transform.Find("PressEText");
            GameObject textObj = textT != null ? textT.gameObject : new GameObject("PressEText", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(Outline));
            if (textT == null) textObj.transform.SetParent(transform, false);

            _textRect = textObj.GetComponent<RectTransform>();
            _textRect.anchorMin = new Vector2(0.5f, 0.5f);
            _textRect.anchorMax = new Vector2(0.5f, 0.5f);
            _textRect.pivot = new Vector2(0.5f, 0.5f);
            _textRect.anchoredPosition = new Vector2(0f, -40f); // Positioned cleanly right below crosshair
            _textRect.sizeDelta = new Vector2(300f, 40f);

            _canvasGroup = textObj.GetComponent<CanvasGroup>() ?? textObj.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _promptText = textObj.GetComponent<Text>() ?? textObj.AddComponent<Text>();
            _promptText.text = "Press E";
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.fontSize = 20;
            _promptText.fontStyle = FontStyle.Bold;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = Color.white;
            _promptText.raycastTarget = false;

            Outline outline = textObj.GetComponent<Outline>() ?? textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// Shows the "Press E" prompt in pure white.
        /// </summary>
        public void ShowPrompt(string promptText = null)
        {
            if (_promptText != null)
            {
                _promptText.text = "Press E";
                _promptText.color = Color.white;
            }

            if (!_isVisible)
            {
                _isVisible = true;

                if (_canvasGroup != null)
                {
                    _fadeTween?.Kill();
                    _fadeTween = _canvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutQuad);
                }
            }
        }

        /// <summary>
        /// Hides the "Press E" prompt smoothly.
        /// </summary>
        public void HidePrompt()
        {
            if (_isVisible)
            {
                _isVisible = false;

                if (_canvasGroup != null)
                {
                    _fadeTween?.Kill();
                    _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration * 0.8f).SetEase(Ease.InQuad);
                }
            }
        }
    }
}
