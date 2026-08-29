using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace V0.UI
{
    /// <summary>
    /// Displays a subtle, cinematic stealth/horror detection indicator on the player's screen.
    /// - Searching (Subtle Amber/Yellow): Soft, atmospheric corner pulse when ghost is investigating.
    /// - Detected (Muted Crimson/Red): Intense horror heartbeat vignette edge when ghost is chasing.
    /// - None: Completely transparent — zero obstruction to gameplay vision.
    /// </summary>
    public class DetectionIndicatorUI : MonoBehaviour
    {
        public enum DetectionState
        {
            None,
            Searching,  // Soft amber indicator (investigating sound / searching)
            Detected    // Crimson pulse (spotted / chase)
        }

        private static DetectionIndicatorUI _instance;
        private static bool _isApplicationQuitting = false;

        public static DetectionIndicatorUI Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    GameObject go = new GameObject("DetectionIndicatorCanvas");
                    _instance = go.AddComponent<DetectionIndicatorUI>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Horror Muted Colors")]
        [SerializeField] private Color _searchingColor = new Color(0.95f, 0.75f, 0.25f, 0.9f); // Subtle Warm Amber
        [SerializeField] private Color _detectedColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);    // Blood Crimson

        [Header("Vignette Intensities (Keeps Center Clear)")]
        [Range(0.02f, 0.35f)]
        [SerializeField] private float _searchVignetteMaxAlpha = 0.12f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _detectedVignetteMaxAlpha = 0.22f;

        [Header("UI Element References (Auto-created if empty)")]
        [SerializeField] private CanvasGroup _mainCanvasGroup;
        [SerializeField] private Image _vignetteImage;
        [SerializeField] private Text _statusText;
        [SerializeField] private RectTransform _badgeContainer;

        private DetectionState _currentState = DetectionState.None;
        private Tweener _vignettePulseTween;
        private Tweener _badgePulseTween;
        private bool _initialized = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 1. Setup Canvas
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // Above 3D scene, below cutscene blackout

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _mainCanvasGroup = GetComponent<CanvasGroup>();
            if (_mainCanvasGroup == null) _mainCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            _mainCanvasGroup.alpha = 0f;
            _mainCanvasGroup.blocksRaycasts = false;
            _mainCanvasGroup.interactable = false;

            // 2. Setup Screen Edge Vignette (Soft corner falloff only — center is 100% clear)
            if (_vignetteImage == null)
            {
                GameObject vigObj = new GameObject("EdgeVignette");
                vigObj.transform.SetParent(transform, false);
                _vignetteImage = vigObj.AddComponent<Image>();
                _vignetteImage.raycastTarget = false;

                RectTransform vigRT = vigObj.GetComponent<RectTransform>();
                vigRT.anchorMin = Vector2.zero;
                vigRT.anchorMax = Vector2.one;
                vigRT.offsetMin = Vector2.zero;
                vigRT.offsetMax = Vector2.zero;

                _vignetteImage.sprite = CreateSoftHorrorVignetteSprite();
                _vignetteImage.color = new Color(0f, 0f, 0f, 0f);
            }

            // 3. Setup Minimalist Horror Indicator Badge (Top Center)
            if (_badgeContainer == null)
            {
                GameObject badgeObj = new GameObject("BadgeContainer");
                badgeObj.transform.SetParent(transform, false);
                _badgeContainer = badgeObj.AddComponent<RectTransform>();
                _badgeContainer.anchorMin = new Vector2(0.5f, 1f);
                _badgeContainer.anchorMax = new Vector2(0.5f, 1f);
                _badgeContainer.pivot = new Vector2(0.5f, 1f);
                _badgeContainer.anchoredPosition = new Vector2(0f, -28f);
                _badgeContainer.sizeDelta = new Vector2(240f, 38f);

                // Subtle dark translucent pill background (not a heavy pitch black box)
                Image bg = badgeObj.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.04f, 0.06f, 0.55f);
                bg.raycastTarget = false;

                // Sleek typography for horror aesthetic
                GameObject textObj = new GameObject("StatusText");
                textObj.transform.SetParent(badgeObj.transform, false);
                _statusText = textObj.AddComponent<Text>();
                _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _statusText.fontSize = 15;
                _statusText.fontStyle = FontStyle.Bold;
                _statusText.alignment = TextAnchor.MiddleCenter;
                _statusText.raycastTarget = false;
                _statusText.text = "· H U N T I N G ·";

                RectTransform textRT = textObj.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(8, 2);
                textRT.offsetMax = new Vector2(-8, -2);
            }
        }

        /// <summary>
        /// Global helper to update detection state from any script safely.
        /// </summary>
        public static void SetGlobalState(DetectionState state)
        {
            if (_isApplicationQuitting) return;

            // If instance is null and state is None, avoid spawning unnecessary GameObjects
            if (_instance == null)
            {
                if (state == DetectionState.None) return;
            }

            DetectionIndicatorUI inst = Instance;
            if (inst != null)
            {
                inst.SetState(state);
            }
        }

        /// <summary>
        /// Sets the current detection state with smooth DOTween animations.
        /// </summary>
        public void SetState(DetectionState newState)
        {
            if (_isApplicationQuitting) return;
            if (_mainCanvasGroup == null) Initialize();

            if (_currentState == newState) return;
            _currentState = newState;

            _vignettePulseTween?.Kill();
            _badgePulseTween?.Kill();

            switch (newState)
            {
                case DetectionState.None:
                    _mainCanvasGroup.DOKill();
                    _mainCanvasGroup.DOFade(0f, 0.45f).SetEase(Ease.InQuad);
                    break;

                case DetectionState.Searching:
                    _mainCanvasGroup.DOKill();
                    _mainCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);

                    if (_statusText != null)
                    {
                        _statusText.text = "· S E A R C H I N G ·";
                        _statusText.color = _searchingColor;
                    }

                    if (_vignetteImage != null)
                    {
                        _vignetteImage.color = new Color(_searchingColor.r, _searchingColor.g, _searchingColor.b, 0.04f);
                        _vignettePulseTween = _vignetteImage.DOFade(_searchVignetteMaxAlpha, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }

                    if (_badgeContainer != null)
                    {
                        _badgeContainer.localScale = Vector3.one;
                        _badgePulseTween = _badgeContainer.DOScale(1.03f, 1.1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }
                    break;

                case DetectionState.Detected:
                    _mainCanvasGroup.DOKill();
                    _mainCanvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);

                    if (_statusText != null)
                    {
                        _statusText.text = "! H U N T I N G !";
                        _statusText.color = _detectedColor;
                    }

                    if (_vignetteImage != null)
                    {
                        _vignetteImage.color = new Color(_detectedColor.r, _detectedColor.g, _detectedColor.b, 0.08f);
                        _vignettePulseTween = _vignetteImage.DOFade(_detectedVignetteMaxAlpha, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }

                    if (_badgeContainer != null)
                    {
                        _badgeContainer.localScale = Vector3.one;
                        _badgeContainer.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.35f, 6, 0.8f);
                        _badgePulseTween = _badgeContainer.DOScale(1.05f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(0.35f);
                    }
                    break;
            }
        }

        /// <summary>
        /// Generates a soft horror vignette texture where the center 80% is crystal clear
        /// and only the extreme outer edges/corners have subtle darkening.
        /// </summary>
        private Sprite CreateSoftHorrorVignetteSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = center.x * 1.414f; // diagonal corner distance

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    // Center 75% is completely clear (alpha = 0); outer 25% fades in softly
                    float norm = Mathf.Clamp01((dist - (maxDist * 0.65f)) / (maxDist * 0.35f));
                    float alpha = norm * norm * norm; // Cubic curve for gentle, feathered corner falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            _vignettePulseTween?.Kill();
            _badgePulseTween?.Kill();
            _mainCanvasGroup?.DOKill();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
