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
                    _instance = Object.FindFirstObjectByType<DetectionIndicatorUI>();
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

        [Header("Serialized References (Pre-baked in Scene)")]
        [SerializeField] private CanvasGroup _mainCanvasGroup;
        [SerializeField] private Image _vignetteImage;
        [SerializeField] private Text _statusText;
        [SerializeField] private RectTransform _badgeContainer;

        private DetectionState _currentState = DetectionState.None;
        private Tweener _vignettePulseTween;
        private Tweener _badgePulseTween;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_mainCanvasGroup == null)
            {
                _mainCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (_mainCanvasGroup != null)
            {
                _mainCanvasGroup.alpha = 0f;
                _mainCanvasGroup.blocksRaycasts = false;
                _mainCanvasGroup.interactable = false;
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
            if (_mainCanvasGroup == null) return;

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
