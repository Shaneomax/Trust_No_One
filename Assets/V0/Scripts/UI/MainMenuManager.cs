using System.Collections;
using System.Collections.Generic;
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
    /// <summary>
    /// Main Menu Manager:
    /// Displays and connects the Main Menu with 'BackGround_Img' background, 'Play' and 'Controls' buttons,
    /// a full Controls popup modal, and smooth scene loading to GameScene.
    /// Automatically detects and fixes scene buttons and EventSystem at runtime.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("Exact name of the gameplay scene to load when clicking Play")]
        [SerializeField] private string _gameSceneName = "GameScene";

        [Header("Background & Assets")]
        [Tooltip("Main menu background sprite (Auto-finds Assets/V0/Images/BackGround_Img.png)")]
        [SerializeField] private Sprite _backgroundSprite;

        [Header("Audio (Optional)")]
        [Tooltip("Menu eerie ambient BGM (Auto-finds OutsideSound.mp3)")]
        [SerializeField] private AudioClip _menuAmbientAudio;
        [Range(0f, 1f)]
        [SerializeField] private float _ambientVolume = 0.45f;

        [Header("UI Timing")]
        [SerializeField] private float _fadeDuration = 0.8f;

        private Canvas _canvas;
        private CanvasGroup _menuCanvasGroup;
        private GameObject _controlsModalObj;
        private CanvasGroup _controlsCanvasGroup;
        private AudioSource _audioSource;
        private Font _font;
        private bool _isTransitioning = false;

        private void Awake()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            AutoLoadAssets();
            BuildOrConnectMainMenuUI();
            SetupMenuAudio();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Fade in main menu
            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.alpha = 0f;
                _menuCanvasGroup.DOFade(1f, 1.2f).SetEase(Ease.InOutSine);
            }
        }

        private void Update()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Close controls with Escape if open
            if (_controlsModalObj != null && _controlsModalObj.activeSelf)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseControlsModal();
                }
#else
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseControlsModal();
                }
#endif
            }
        }

        private void AutoLoadAssets()
        {
            if (_backgroundSprite == null)
            {
#if UNITY_EDITOR
                _backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/V0/Images/BackGround_Img.png");
#endif
                if (_backgroundSprite == null)
                {
                    Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    foreach (var s in allSprites)
                    {
                        if (s.name.ToLower().Contains("background_img") || s.name.ToLower().Contains("background"))
                        {
                            _backgroundSprite = s;
                            break;
                        }
                    }
                }
            }

            if (_menuAmbientAudio == null)
            {
#if UNITY_EDITOR
                _menuAmbientAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/OutsideSound.mp3");
#endif
            }
        }

        private void SetupMenuAudio()
        {
            if (_menuAmbientAudio != null && _audioSource == null)
            {
                _audioSource = gameObject.GetComponent<AudioSource>();
                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.clip = _menuAmbientAudio;
                _audioSource.loop = true;
                _audioSource.volume = _ambientVolume;
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
                if (!_audioSource.isPlaying) _audioSource.Play();
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
                StandaloneInputModule oldModule = es.GetComponent<StandaloneInputModule>();
                if (oldModule != null)
                {
                    DestroyImmediate(oldModule);
                }
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                {
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                }
#endif
            }
        }

        private void BuildOrConnectMainMenuUI()
        {
            EnsureEventSystem();

            // 1. Check if Canvas already exists in scene
            Canvas existingCanvas = Object.FindFirstObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
                if (_canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    _canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                _menuCanvasGroup = existingCanvas.GetComponent<CanvasGroup>() ?? existingCanvas.gameObject.AddComponent<CanvasGroup>();
                _menuCanvasGroup.blocksRaycasts = true;
                _menuCanvasGroup.interactable = true;

                // Hook up all existing buttons in the canvas
                Button[] buttons = existingCanvas.GetComponentsInChildren<Button>(true);
                foreach (Button b in buttons)
                {
                    string bName = b.gameObject.name.ToLower();

                    // Make sure button's image and text are raycastable & enabled
                    Image btnImg = b.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.enabled = true;
                        btnImg.raycastTarget = true;
                    }
                    Text btnTxt = b.GetComponentInChildren<Text>(true);
                    if (btnTxt != null)
                    {
                        btnTxt.raycastTarget = true;
                    }

                    if (bName.Contains("play"))
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(OnPlayButtonClicked);
                        Debug.Log("<color=green>[MainMenuManager]</color> Successfully hooked up Play button: " + b.gameObject.name);
                    }
                    else if (bName.Contains("control"))
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(OnControlsButtonClicked);
                        Debug.Log("<color=green>[MainMenuManager]</color> Successfully hooked up Controls button: " + b.gameObject.name);
                    }
                }

                // Check for ControlsModalPanel
                Transform modalT = existingCanvas.transform.Find("ControlsModalPanel");
                if (modalT != null)
                {
                    _controlsModalObj = modalT.gameObject;
                    _controlsCanvasGroup = _controlsModalObj.GetComponent<CanvasGroup>() ?? _controlsModalObj.AddComponent<CanvasGroup>();
                    Button closeBtn = _controlsModalObj.GetComponentInChildren<Button>(true);
                    if (closeBtn != null)
                    {
                        closeBtn.onClick.RemoveAllListeners();
                        closeBtn.onClick.AddListener(CloseControlsModal);
                    }
                    _controlsModalObj.SetActive(false);
                }
                else
                {
                    BuildControlsModal(existingCanvas.transform);
                }

                return;
            }

            // 2. Otherwise dynamically create Main Screen Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _menuCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            _menuCanvasGroup.blocksRaycasts = true;
            _menuCanvasGroup.interactable = true;

            // Background Image (BackGround_Img.png)
            GameObject bgObj = new GameObject("BackgroundImage");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            if (_backgroundSprite != null)
            {
                bgImg.sprite = _backgroundSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.preserveAspect = false;
            }
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Subtle Dark Atmosphere Vignette / Overlay
            GameObject overlayObj = new GameObject("AtmosphereOverlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            Image overlayImg = overlayObj.AddComponent<Image>();
            overlayImg.color = new Color(0.04f, 0.05f, 0.08f, 0.45f);
            overlayImg.raycastTarget = false;

            // Game Title Header
            GameObject titleObj = new GameObject("GameTitle");
            titleObj.transform.SetParent(canvasObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.76f);
            titleRect.anchorMax = new Vector2(0.5f, 0.76f);
            titleRect.sizeDelta = new Vector2(900, 120);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _font;
            titleText.fontSize = 68;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.96f, 0.94f, 0.90f, 1f);
            titleText.text = "Lost In Mind";
            titleText.raycastTarget = false;

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);

            // Sub-headline
            GameObject tagObj = new GameObject("GameTagline");
            tagObj.transform.SetParent(canvasObj.transform, false);
            RectTransform tagRect = tagObj.AddComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(0.5f, 0.69f);
            tagRect.anchorMax = new Vector2(0.5f, 0.69f);
            tagRect.sizeDelta = new Vector2(600, 40);
            Text tagText = tagObj.AddComponent<Text>();
            tagText.font = _font;
            tagText.fontSize = 20;
            tagText.fontStyle = FontStyle.Bold;
            tagText.alignment = TextAnchor.MiddleCenter;
            tagText.color = new Color(0.85f, 0.35f, 0.35f, 0.9f);
            tagText.text = "• SURVIVAL HORROR •";
            tagText.raycastTarget = false;

            // Buttons Container
            GameObject btnContainer = new GameObject("ButtonsContainer");
            btnContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform contRect = btnContainer.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.38f);
            contRect.anchorMax = new Vector2(0.5f, 0.38f);
            contRect.sizeDelta = new Vector2(320, 200);

            CreateMenuButton(btnContainer.transform, "PlayButton", "PLAY", new Vector2(0, 45), OnPlayButtonClicked);
            CreateMenuButton(btnContainer.transform, "ControlsButton", "CONTROLS", new Vector2(0, -45), OnControlsButtonClicked);

            BuildControlsModal(canvasObj.transform);
        }

        private void CreateMenuButton(Transform parent, string goName, string buttonLabel, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClickAction)
        {
            GameObject btnObj = new GameObject(goName);
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = anchoredPos;
            btnRect.sizeDelta = new Vector2(280, 64);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.13f, 0.18f, 0.88f);
            btnImg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.12f, 0.13f, 0.18f, 0.88f);
            colors.highlightedColor = new Color(0.35f, 0.42f, 0.58f, 1.0f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.12f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            btn.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text txt = textObj.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = buttonLabel;
            txt.raycastTarget = true;

            btn.onClick.AddListener(onClickAction);
        }

        private void BuildControlsModal(Transform parent)
        {
            _controlsModalObj = new GameObject("ControlsModalPanel");
            _controlsModalObj.transform.SetParent(parent, false);
            RectTransform modalRect = _controlsModalObj.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            _controlsCanvasGroup = _controlsModalObj.AddComponent<CanvasGroup>();
            _controlsCanvasGroup.alpha = 0f;

            Image dimmerImg = _controlsModalObj.AddComponent<Image>();
            dimmerImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimmerImg.raycastTarget = true;

            GameObject boxObj = new GameObject("ModalBox");
            boxObj.transform.SetParent(_controlsModalObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(720, 520);
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.15f, 0.95f);

            GameObject titleObj = new GameObject("ModalTitle");
            titleObj.transform.SetParent(boxObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.90f);
            titleRect.anchorMax = new Vector2(0.5f, 0.90f);
            titleRect.sizeDelta = new Vector2(600, 50);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _font;
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            titleText.text = "GAME CONTROLS";
            titleText.raycastTarget = false;

            GameObject contentObj = new GameObject("ControlsContent");
            contentObj.transform.SetParent(boxObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.52f);
            contentRect.anchorMax = new Vector2(0.5f, 0.52f);
            contentRect.sizeDelta = new Vector2(620, 300);
            Text contentText = contentObj.AddComponent<Text>();
            contentText.font = _font;
            contentText.fontSize = 20;
            contentText.alignment = TextAnchor.MiddleLeft;
            contentText.lineSpacing = 1.35f;
            contentText.color = new Color(0.92f, 0.92f, 0.94f, 1f);
            contentText.text = 
                "<b>W, A, S, D</b>  —  Move\n" +
                "<b>Mouse</b>  —  Look Around\n" +
                "<b>Left Shift</b>  —  Sprint  <i><color=#FF7070>(Warning: Ghost hears running!)</color></i>\n" +
                "<b>C / Left Ctrl</b>  —  Crouch  <i><color=#70FF90>(Silent Stealth)</color></i>\n" +
                "<b>E / Left Click</b>  —  Interact & Pick Up Keys\n" +
                "<b>F</b>  —  Toggle Flashlight\n" +
                "<b>Escape</b>  —  Back / Pause";
            contentText.raycastTarget = false;

            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(boxObj.transform, false);
            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0.12f);
            closeRect.anchorMax = new Vector2(0.5f, 0.12f);
            closeRect.sizeDelta = new Vector2(220, 50);

            Image closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.20f, 0.22f, 0.28f, 1f);
            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            ColorBlock closeColors = closeBtn.colors;
            closeColors.normalColor = new Color(0.20f, 0.22f, 0.28f, 1f);
            closeColors.highlightedColor = new Color(0.40f, 0.45f, 0.58f, 1f);
            closeColors.pressedColor = new Color(0.12f, 0.12f, 0.15f, 1f);
            closeBtn.colors = closeColors;

            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.font = _font;
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyle.Bold;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.text = "BACK";
            closeText.raycastTarget = true;

            closeBtn.onClick.AddListener(CloseControlsModal);

            _controlsModalObj.SetActive(false);
        }

        public void OnPlayButtonClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log($"<color=green>[MainMenuManager]</color> Starting game! Loading scene: <b>{_gameSceneName}</b>");

            if (_audioSource != null)
            {
                _audioSource.DOFade(0f, _fadeDuration);
            }

            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.DOFade(0f, _fadeDuration).OnComplete(() =>
                {
                    SceneManager.LoadScene(_gameSceneName);
                });
            }
            else
            {
                SceneManager.LoadScene(_gameSceneName);
            }
        }

        public void OnControlsButtonClicked()
        {
            if (_controlsModalObj != null)
            {
                _controlsModalObj.SetActive(true);
                if (_controlsCanvasGroup != null)
                {
                    _controlsCanvasGroup.DOKill();
                    _controlsCanvasGroup.alpha = 0f;
                    _controlsCanvasGroup.DOFade(1f, 0.35f);
                }
            }
        }

        public void CloseControlsModal()
        {
            if (_controlsModalObj != null && _controlsModalObj.activeSelf)
            {
                if (_controlsCanvasGroup != null)
                {
                    _controlsCanvasGroup.DOKill();
                    _controlsCanvasGroup.DOFade(0f, 0.25f).OnComplete(() =>
                    {
                        _controlsModalObj.SetActive(false);
                    });
                }
                else
                {
                    _controlsModalObj.SetActive(false);
                }
            }
        }
    }
}
