using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using DG.Tweening;

namespace V0.UI
{
    public enum EndingType
    {
        Good,        // GoodEnding trigger
        Okay,        // OkayEnding trigger
        LastTrigger  // LastTrigger cutscene (Backstab betrayal)
    }

    /// <summary>
    /// Unified Ending Screen Manager:
    /// Attach this script to an 'EndingManager' GameObject in the 'GoodEnding' scene.
    /// Handles all 3 endings in this single scene:
    /// 1. Good Ending: "You successfully escaped the terror."
    /// 2. Okay Ending: "You might have save yourself now But oneday He will hunt you down."
    /// 3. Last Trigger Ending: "Have a Good Night sleep. Because you are not waking up"
    /// </summary>
    public class EndingManager : MonoBehaviour
    {
        public static EndingType CurrentEnding = EndingType.Good;

        [Header("Ending Narratives (Customizable)")]
        [TextArea(2, 3)]
        [SerializeField] private string _goodEndingText = "You successfully escaped the terror.";
        [TextArea(2, 3)]
        [SerializeField] private string _okayEndingText = "You might have save yourself now But oneday He will hunt you down.";
        [TextArea(2, 3)]
        [SerializeField] private string _lastTriggerText = "Have a Good Night sleep. Because you are not waking up";

        [Header("Thank You Screen Settings")]
        [SerializeField] private string _thankYouTitle = "Thank you for playing!";
        [SerializeField] private string _subtitleText = "Trust No One";
        [SerializeField] private string _continueButtonText = "CONTINUE";

        [Header("Timing")]
        [SerializeField] private float _textFadeInDuration = 1.6f;
        [SerializeField] private float _textHoldDuration = 4.5f;
        [SerializeField] private float _textFadeOutDuration = 1.2f;
        [SerializeField] private float _thankYouFadeInDuration = 1.5f;

        private Canvas _canvas;
        private CanvasGroup _narrativeGroup;
        private Text _narrativeText;
        private CanvasGroup _thankYouGroup;
        private Button _continueButton;
        private Font _font;
        private bool _isThankYouActive = false;
        private bool _hasClickedContinue = false;

        private void Awake()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            BuildEndingUI();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(PlayEndingSequence());
        }

        private void Update()
        {
            if (_isThankYouActive && !_hasClickedContinue)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    OnContinueClicked();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnContinueClicked();
                }
#endif
            }
        }

        private void BuildEndingUI()
        {
            // 1. Ensure EventSystem exists and uses the New Input System module
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                StandaloneInputModule oldModule = es.GetComponent<StandaloneInputModule>();
                if (oldModule != null)
                {
                    DestroyImmediate(oldModule);
                    if (es.GetComponent<InputSystemUIInputModule>() == null)
                    {
                        es.gameObject.AddComponent<InputSystemUIInputModule>();
                    }
                }
#endif
            }

            // 2. Main Canvas
            GameObject canvasObj = new GameObject("EndingCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 3. Fullscreen Black Background
            GameObject bgObj = new GameObject("BlackBackground");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false; // NEVER block raycasts!

            // 4. Narrative Text Panel
            GameObject narrativeObj = new GameObject("NarrativePanel");
            narrativeObj.transform.SetParent(canvasObj.transform, false);
            RectTransform narrativeRect = narrativeObj.AddComponent<RectTransform>();
            narrativeRect.anchorMin = new Vector2(0.1f, 0.25f);
            narrativeRect.anchorMax = new Vector2(0.9f, 0.75f);
            narrativeRect.sizeDelta = Vector2.zero;
            _narrativeGroup = narrativeObj.AddComponent<CanvasGroup>();
            _narrativeGroup.alpha = 0f;
            _narrativeGroup.blocksRaycasts = false;

            GameObject textObj = new GameObject("NarrativeText");
            textObj.transform.SetParent(narrativeObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            _narrativeText = textObj.AddComponent<Text>();
            _narrativeText.font = _font;
            _narrativeText.fontSize = 38;
            _narrativeText.alignment = TextAnchor.MiddleCenter;
            _narrativeText.color = Color.white;
            _narrativeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _narrativeText.verticalOverflow = VerticalWrapMode.Overflow;
            _narrativeText.lineSpacing = 1.25f;
            _narrativeText.raycastTarget = false;

            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            textShadow.effectDistance = new Vector2(2f, -2f);

            // 5. Final "Thank You" Panel
            GameObject thankYouObj = new GameObject("ThankYouPanel");
            thankYouObj.transform.SetParent(canvasObj.transform, false);
            RectTransform thankYouRect = thankYouObj.AddComponent<RectTransform>();
            thankYouRect.anchorMin = Vector2.zero;
            thankYouRect.anchorMax = Vector2.one;
            thankYouRect.sizeDelta = Vector2.zero;
            _thankYouGroup = thankYouObj.AddComponent<CanvasGroup>();
            _thankYouGroup.alpha = 0f;
            _thankYouGroup.blocksRaycasts = true;
            _thankYouGroup.interactable = true;
            thankYouObj.SetActive(false);

            // Title: "Thank you for playing!"
            GameObject titleObj = new GameObject("ThankYouTitle");
            titleObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.62f);
            titleRect.anchorMax = new Vector2(0.5f, 0.62f);
            titleRect.sizeDelta = new Vector2(900, 100);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _font;
            titleText.fontSize = 46;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.75f, 1f); // Warm luminous ivory
            titleText.text = _thankYouTitle;
            titleText.raycastTarget = false;

            // Subtitle: "Trust No One"
            GameObject subObj = new GameObject("GameSubtitle");
            subObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.52f);
            subRect.anchorMax = new Vector2(0.5f, 0.52f);
            subRect.sizeDelta = new Vector2(600, 60);
            Text subText = subObj.AddComponent<Text>();
            subText.font = _font;
            subText.fontSize = 22;
            subText.fontStyle = FontStyle.Italic;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.7f, 0.75f, 0.8f, 0.85f);
            subText.text = _subtitleText;
            subText.raycastTarget = false;

            // Bottom-Middle Continue Button
            GameObject btnObj = new GameObject("ContinueButton");
            btnObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0, 130);
            btnRect.sizeDelta = new Vector2(280, 68);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            btnImg.raycastTarget = true;

            _continueButton = btnObj.AddComponent<Button>();
            _continueButton.targetGraphic = btnImg;
            ColorBlock colors = _continueButton.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            colors.highlightedColor = new Color(0.40f, 0.44f, 0.56f, 1.0f);
            colors.pressedColor = new Color(0.10f, 0.10f, 0.12f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            _continueButton.colors = colors;

            // Continue Button Text
            GameObject btnTextObj = new GameObject("BtnText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = _font;
            btnText.fontSize = 24;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = _continueButtonText;
            btnText.raycastTarget = false;

            _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private IEnumerator PlayEndingSequence()
        {
            switch (CurrentEnding)
            {
                case EndingType.Good:
                    _narrativeText.text = _goodEndingText;
                    break;
                case EndingType.Okay:
                    _narrativeText.text = _okayEndingText;
                    break;
                case EndingType.LastTrigger:
                    _narrativeText.text = _lastTriggerText;
                    break;
            }

            yield return new WaitForSeconds(0.5f);

            // 1. Fade in narrative text on black screen
            _narrativeGroup.DOKill();
            _narrativeGroup.DOFade(1f, _textFadeInDuration).SetEase(Ease.InOutSine);

            // 2. Hold text on screen for player to read
            yield return new WaitForSeconds(_textHoldDuration);

            // 3. Fade out narrative text
            _narrativeGroup.DOFade(0f, _textFadeOutDuration).SetEase(Ease.InOutSine);
            yield return new WaitForSeconds(_textFadeOutDuration + 0.3f);

            // 4. Fade in "Thank you for playing!" & Continue button
            _thankYouGroup.gameObject.SetActive(true);
            _thankYouGroup.DOKill();
            _thankYouGroup.DOFade(1f, _thankYouFadeInDuration).SetEase(Ease.InOutSine);

            _isThankYouActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnContinueClicked()
        {
            if (_hasClickedContinue) return;
            _hasClickedContinue = true;

            if (_continueButton != null) _continueButton.interactable = false;

            // Fade out and return to MainMenu scene
            _thankYouGroup.DOKill();
            _thankYouGroup.DOFade(0f, 0.8f).OnComplete(() =>
            {
                Debug.Log("<color=green>[EndingManager]</color> Returning to MainMenu scene.");
                SceneManager.LoadScene("MainMenu");
            });
        }
    }
}
