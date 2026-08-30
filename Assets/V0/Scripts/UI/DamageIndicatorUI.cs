using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace V0.UI
{
    /// <summary>
    /// Displays immersive horror damage feedback on the player's screen:
    /// - Red Impact Flash: Bloody vignette impulse whenever the ghost hits the player.
    /// - Low Health Pulsating Vignette: Soft continuous bloody heartbeat pulse at screen edges when health < 30%.
    /// - Clean Screen: Zero HUD clutter / no health bar on screen for pure horror immersion.
    /// </summary>
    public class DamageIndicatorUI : MonoBehaviour
    {
        public static DamageIndicatorUI Instance { get; private set; }

        [Header("Damage Flash Settings")]
        [SerializeField] private CanvasGroup _damageFlashCanvasGroup;
        [SerializeField] private Image _damageFlashImage;
        [SerializeField] private Color _hitFlashColor = new Color(0.85f, 0.05f, 0.05f, 0.75f);

        [Header("Low Health (< 30%) Pulsating Vignette")]
        [SerializeField] private CanvasGroup _lowHealthVignetteCanvasGroup;
        [SerializeField] private Image _lowHealthVignetteImage;

        private Tweener _flashTween;
        private Tweener _lowHealthPulseTween;

        public static DamageIndicatorUI GetOrCreate()
        {
            if (Instance != null) return Instance;

            DamageIndicatorUI found = Object.FindFirstObjectByType<DamageIndicatorUI>();
            if (found != null)
            {
                Instance = found;
                return Instance;
            }

            GameObject canvasObj = new GameObject("DamageIndicatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DamageIndicatorUI));
            Instance = canvasObj.GetComponent<DamageIndicatorUI>();
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
            canvas.sortingOrder = 85;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureUIHierarchy();

            SafeSetAlpha(ref _damageFlashCanvasGroup, "DamageFlash", 0f);
            SafeSetAlpha(ref _lowHealthVignetteCanvasGroup, "LowHealthVignette", 0f);
        }

        private void SafeSetAlpha(ref CanvasGroup group, string childName, float alpha)
        {
            if (group == null)
            {
                Transform t = transform.Find(childName);
                if (t != null)
                {
                    group = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private void EnsureUIHierarchy()
        {
            Sprite vignetteSprite = Resources.Load<Sprite>("SoftVignette");
            if (vignetteSprite == null)
            {
                #if UNITY_EDITOR
                vignetteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/V0/Textures/UI/SoftVignette.png");
                #endif
            }

            // 1. Damage Flash
            Transform flashT = transform.Find("DamageFlash");
            GameObject flashObj = flashT != null ? flashT.gameObject : new GameObject("DamageFlash", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            if (flashT == null) flashObj.transform.SetParent(transform, false);

            _damageFlashCanvasGroup = flashObj.GetComponent<CanvasGroup>() ?? flashObj.AddComponent<CanvasGroup>();
            _damageFlashCanvasGroup.alpha = 0f;
            _damageFlashCanvasGroup.blocksRaycasts = false;
            _damageFlashCanvasGroup.interactable = false;

            _damageFlashImage = flashObj.GetComponent<Image>() ?? flashObj.AddComponent<Image>();
            if (vignetteSprite != null) _damageFlashImage.sprite = vignetteSprite;
            _damageFlashImage.color = _hitFlashColor;
            _damageFlashImage.raycastTarget = false;
            SetFullScreenRect(flashObj.GetComponent<RectTransform>());

            // 2. Low Health Vignette
            Transform lowHealthT = transform.Find("LowHealthVignette");
            GameObject lowHealthObj = lowHealthT != null ? lowHealthT.gameObject : new GameObject("LowHealthVignette", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            if (lowHealthT == null) lowHealthObj.transform.SetParent(transform, false);

            _lowHealthVignetteCanvasGroup = lowHealthObj.GetComponent<CanvasGroup>() ?? lowHealthObj.AddComponent<CanvasGroup>();
            _lowHealthVignetteCanvasGroup.alpha = 0f;
            _lowHealthVignetteCanvasGroup.blocksRaycasts = false;
            _lowHealthVignetteCanvasGroup.interactable = false;

            _lowHealthVignetteImage = lowHealthObj.GetComponent<Image>() ?? lowHealthObj.AddComponent<Image>();
            if (vignetteSprite != null) _lowHealthVignetteImage.sprite = vignetteSprite;
            _lowHealthVignetteImage.color = new Color(0.65f, 0.02f, 0.02f, 0.6f);
            _lowHealthVignetteImage.raycastTarget = false;
            SetFullScreenRect(lowHealthObj.GetComponent<RectTransform>());

            // Destroy any legacy HealthBarHUD if present
            Transform oldHud = transform.Find("HealthBarHUD");
            if (oldHud != null)
            {
                Destroy(oldHud.gameObject);
            }
        }

        private static void SetFullScreenRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Triggered whenever the player takes damage from the ghost.
        /// </summary>
        public void OnPlayerHit(float damageAmount, float currentHealthPercent, bool isLowHealth)
        {
            if (_damageFlashCanvasGroup == null)
            {
                Transform t = transform.Find("DamageFlash");
                if (t != null) _damageFlashCanvasGroup = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
            }

            // 1. Violent Red Flash on screen
            if (_damageFlashCanvasGroup != null)
            {
                _flashTween?.Kill();
                _damageFlashCanvasGroup.alpha = 0.75f;
                _flashTween = _damageFlashCanvasGroup.DOFade(0f, 0.65f).SetEase(Ease.OutQuad);
            }

            // 2. Update Low Health Vignette state
            SetLowHealthState(isLowHealth);
        }

        /// <summary>
        /// Updates the low health vignette state.
        /// </summary>
        public void UpdateHealthDisplay(float currentHealth, float maxHealth)
        {
            SetLowHealthState(currentHealth <= 30f);
        }

        private void SetLowHealthState(bool isLowHealth)
        {
            if (_lowHealthVignetteCanvasGroup == null)
            {
                Transform t = transform.Find("LowHealthVignette");
                if (t != null) _lowHealthVignetteCanvasGroup = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
            }

            if (_lowHealthVignetteCanvasGroup == null) return;

            if (isLowHealth)
            {
                if (_lowHealthPulseTween == null || !_lowHealthPulseTween.IsActive())
                {
                    _lowHealthVignetteCanvasGroup.alpha = 0.2f;
                    _lowHealthPulseTween = _lowHealthVignetteCanvasGroup
                        .DOFade(0.45f, 0.6f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }
            else
            {
                if (_lowHealthPulseTween != null)
                {
                    _lowHealthPulseTween.Kill();
                    _lowHealthPulseTween = null;
                }
                _lowHealthVignetteCanvasGroup.DOFade(0f, 0.5f);
            }
        }
    }
}
