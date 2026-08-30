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
    /// Unified Ending Screen Controller for the GoodEnding scene.
    /// Works 100% out of the box (both with pre-baked scenes and dynamic auto-creation).
    /// Handles narrative text sequence -> "Thank you for playing!" & "Created by: Anik Pal" -> Continue to MainMenu.
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
        [SerializeField] private string _creatorCreditsText = "Created by: Anik Pal";
        [SerializeField] private string _continueButtonText = "CONTINUE";

        [Header("UI References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _narrativeGroup;
        [SerializeField] private Text _narrativeText;
        [SerializeField] private CanvasGroup _thankYouGroup;
        [SerializeField] private Text _thankYouTitleText;
        [SerializeField] private Text _creatorCreditsTextUI;
        [SerializeField] private Button _continueButton;

        [Header("Timing")]
        [SerializeField] private float _textFadeInDuration = 1.6f;
        [SerializeField] private float _textHoldDuration = 4.5f;
        [SerializeField] private float _textFadeOutDuration = 1.2f;
        [SerializeField] private float _thankYouFadeInDuration = 1.5f;

        private bool _isThankYouActive = false;
        private bool _hasClickedContinue = false;
        private Font _font;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            // Clean up any leftover canvases from previous scenes
            GameObject oldClue = GameObject.Find("KeyClueCanvas");
            if (oldClue != null) Destroy(oldClue);

            GameObject oldPause = GameObject.Find("PauseCanvas");
            if (oldPause != null) Destroy(oldPause);

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            BuildOrConnectEndingUI();
        }

        private void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

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

        private void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
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
                StandaloneInputModule oldMod = es.GetComponent<StandaloneInputModule>();
                if (oldMod != null) DestroyImmediate(oldMod);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                {
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                }
#endif
            }
        }

        private void BuildOrConnectEndingUI()
        {
            EnsureEventSystem();

            // 1. Check if pre-baked in scene
            if (_narrativeGroup == null)
            {
                Transform nT = transform.Find("EndingCanvas/NarrativePanel") ?? transform.Find("NarrativePanel");
                if (nT != null)
                {
                    _narrativeGroup = nT.GetComponent<CanvasGroup>();
                    _narrativeText = nT.GetComponentInChildren<Text>(true);
                }
            }

            if (_thankYouGroup == null)
            {
                Transform tT = transform.Find("EndingCanvas/ThankYouPanel") ?? transform.Find("ThankYouPanel");
                if (tT != null)
                {
                    _thankYouGroup = tT.GetComponent<CanvasGroup>();
                    _continueButton = tT.GetComponentInChildren<Button>(true);
                    Text[] texts = tT.GetComponentsInChildren<Text>(true);
                    if (texts.Length > 0 && _thankYouTitleText == null) _thankYouTitleText = texts[0];
                    if (texts.Length > 1 && _creatorCreditsTextUI == null) _creatorCreditsTextUI = texts[1];
                }
            }

            // 2. If pre-baked UI found, we're ready!
            if (_narrativeGroup != null && _thankYouGroup != null)
            {
                _narrativeGroup.alpha = 0f;
                _thankYouGroup.alpha = 0f;
                _thankYouGroup.gameObject.SetActive(false);
                return;
            }

            // 3. Otherwise dynamically construct full UI
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

            // Fullscreen Black Background
            GameObject bgObj = new GameObject("BlackBackground");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false;

            // Narrative Panel
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

            // Thank You Panel
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
            _thankYouTitleText = titleObj.AddComponent<Text>();
            _thankYouTitleText.font = _font;
            _thankYouTitleText.fontSize = 48;
            _thankYouTitleText.fontStyle = FontStyle.Bold;
            _thankYouTitleText.alignment = TextAnchor.MiddleCenter;
            _thankYouTitleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            _thankYouTitleText.text = _thankYouTitle;
            _thankYouTitleText.raycastTarget = false;

            // Creator Credits: "Created by: Anik Pal"
            GameObject creditObj = new GameObject("CreatorCredits");
            creditObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform creditRect = creditObj.AddComponent<RectTransform>();
            creditRect.anchorMin = new Vector2(0.5f, 0.50f);
            creditRect.anchorMax = new Vector2(0.5f, 0.50f);
            creditRect.sizeDelta = new Vector2(600, 50);
            _creatorCreditsTextUI = creditObj.AddComponent<Text>();
            _creatorCreditsTextUI.font = _font;
            _creatorCreditsTextUI.fontSize = 24;
            _creatorCreditsTextUI.fontStyle = FontStyle.Bold;
            _creatorCreditsTextUI.alignment = TextAnchor.MiddleCenter;
            _creatorCreditsTextUI.color = new Color(0.9f, 0.92f, 0.96f, 0.95f);
            _creatorCreditsTextUI.text = _creatorCreditsText;
            _creatorCreditsTextUI.raycastTarget = false;

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
        }

        private IEnumerator PlayEndingSequence()
        {
            if (_narrativeText != null)
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
            }

            if (_thankYouTitleText != null) _thankYouTitleText.text = _thankYouTitle;
            if (_creatorCreditsTextUI != null) _creatorCreditsTextUI.text = _creatorCreditsText;

            yield return new WaitForSeconds(0.5f);

            // 1. Fade in narrative text on black screen
            if (_narrativeGroup != null)
            {
                _narrativeGroup.DOKill();
                _narrativeGroup.alpha = 0f;
                _narrativeGroup.DOFade(1f, _textFadeInDuration).SetEase(Ease.InOutSine);
            }

            // 2. Hold text on screen for player to read
            yield return new WaitForSeconds(_textHoldDuration);

            // 3. Fade out narrative text
            if (_narrativeGroup != null)
            {
                _narrativeGroup.DOFade(0f, _textFadeOutDuration).SetEase(Ease.InOutSine);
            }
            yield return new WaitForSeconds(_textFadeOutDuration + 0.3f);

            // 4. Fade in "Thank you for playing!" & Continue button
            if (_thankYouGroup != null)
            {
                _thankYouGroup.gameObject.SetActive(true);
                _thankYouGroup.DOKill();
                _thankYouGroup.alpha = 0f;
                _thankYouGroup.DOFade(1f, _thankYouFadeInDuration).SetEase(Ease.InOutSine);
            }

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
            if (_thankYouGroup != null)
            {
                _thankYouGroup.DOKill();
                _thankYouGroup.DOFade(0f, 0.8f).OnComplete(() =>
                {
                    Debug.Log("<color=green>[EndingManager]</color> Returning to MainMenu scene.");
                    SceneManager.LoadScene("MainMenu");
                });
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
