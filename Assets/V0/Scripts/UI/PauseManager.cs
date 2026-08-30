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
    /// <summary>
    /// Pause Menu Manager for GameScene:
    /// Pressing Escape pauses the game (Time.timeScale = 0), frees cursor, and displays the Pause Panel.
    /// Three main buttons:
    /// 1. RESUME: Returns to gameplay and locks cursor.
    /// 2. CONTROLS: Opens the Controls popup modal.
    /// 3. MAIN MENU: Restores timeScale and loads the MainMenu scene.
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        [Header("Scene Configuration")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("Audio")]
        [Tooltip("Pause all scene audio when game is paused")]
        [SerializeField] private bool _pauseAudioOnPause = true;

        [Header("UI References (Optional: Pre-baked or Auto-created)")]
        [SerializeField] private CanvasGroup _pauseCanvasGroup;
        [SerializeField] private GameObject _mainPausePanel;
        [SerializeField] private GameObject _controlsModalPanel;
        [SerializeField] private CanvasGroup _controlsCanvasGroup;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _controlsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _closeControlsButton;

        private bool _isPaused = false;
        private Font _font;

        public static bool IsPaused => Instance != null && Instance._isPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            string sceneName = SceneManager.GetActiveScene().name.ToLower();
            if (sceneName.Contains("gamescene") || sceneName.Contains("game"))
            {
                if (FindFirstObjectByType<PauseManager>() == null)
                {
                    GameObject go = new GameObject("[PauseManager]");
                    go.AddComponent<PauseManager>();
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            BuildOrConnectPauseUI();
        }

        private void Start()
        {
            // Ensure unpaused at start
            ResumeGame(false);
        }

        private void Update()
        {
            // Don't allow pause in menus or ending scenes
            string currentScene = SceneManager.GetActiveScene().name.ToLower();
            if (currentScene.Contains("menu") || currentScene.Contains("ending")) return;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
#endif
        }

        public void TogglePause()
        {
            // If controls modal is open, Escape should close the modal first
            if (_controlsModalPanel != null && _controlsModalPanel.activeSelf)
            {
                CloseControlsModal();
                return;
            }

            if (_isPaused)
            {
                ResumeGame(true);
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            _isPaused = true;
            Time.timeScale = 0f;

            if (_pauseAudioOnPause)
            {
                AudioListener.pause = true;
            }

            // Unlock and reveal mouse cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Show UI
            if (_pauseCanvasGroup != null)
            {
                _pauseCanvasGroup.gameObject.SetActive(true);
                _pauseCanvasGroup.DOKill();
                _pauseCanvasGroup.alpha = 0f;
                _pauseCanvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
            }

            if (_mainPausePanel != null) _mainPausePanel.SetActive(true);
            if (_controlsModalPanel != null) _controlsModalPanel.SetActive(false);

            Debug.Log("<color=yellow>[PauseManager]</color> Game Paused.");
        }

        public void ResumeGame(bool playFeedback = true)
        {
            _isPaused = false;
            Time.timeScale = 1f;

            if (_pauseAudioOnPause)
            {
                AudioListener.pause = false;
            }

            // Lock and hide mouse cursor for FPS gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_pauseCanvasGroup != null)
            {
                if (playFeedback)
                {
                    _pauseCanvasGroup.DOKill();
                    _pauseCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
                    {
                        _pauseCanvasGroup.gameObject.SetActive(false);
                    });
                }
                else
                {
                    _pauseCanvasGroup.alpha = 0f;
                    _pauseCanvasGroup.gameObject.SetActive(false);
                }
            }

            if (_controlsModalPanel != null) _controlsModalPanel.SetActive(false);

            if (playFeedback)
            {
                Debug.Log("<color=green>[PauseManager]</color> Game Resumed.");
            }
        }

        public void OpenControlsModal()
        {
            if (_controlsModalPanel != null)
            {
                _controlsModalPanel.SetActive(true);
                if (_controlsCanvasGroup != null)
                {
                    _controlsCanvasGroup.DOKill();
                    _controlsCanvasGroup.alpha = 0f;
                    _controlsCanvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
                }
            }
        }

        public void CloseControlsModal()
        {
            if (_controlsModalPanel != null && _controlsModalPanel.activeSelf)
            {
                if (_controlsCanvasGroup != null)
                {
                    _controlsCanvasGroup.DOKill();
                    _controlsCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
                    {
                        _controlsModalPanel.SetActive(false);
                    });
                }
                else
                {
                    _controlsModalPanel.SetActive(false);
                }
            }
        }

        public void LoadMainMenu()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("<color=cyan>[PauseManager]</color> Returning to MainMenu scene.");
            SceneManager.LoadScene(_mainMenuSceneName);
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

        private void BuildOrConnectPauseUI()
        {
            EnsureEventSystem();

            // Check if PauseCanvas is already pre-baked in the scene
            if (_pauseCanvasGroup == null)
            {
                CanvasGroup existingCg = GetComponentInChildren<CanvasGroup>(true);
                if (existingCg != null) _pauseCanvasGroup = existingCg;
            }

            if (_pauseCanvasGroup != null)
            {
                // UI is pre-baked at compile time! Wire listeners with 0 runtime allocations
                if (_resumeButton == null)
                {
                    Transform t = transform.Find("PauseCanvas/MainPausePanel/ButtonsContainer/ResumeButton");
                    if (t != null) _resumeButton = t.GetComponent<Button>();
                }
                if (_controlsButton == null)
                {
                    Transform t = transform.Find("PauseCanvas/MainPausePanel/ButtonsContainer/ControlsButton");
                    if (t != null) _controlsButton = t.GetComponent<Button>();
                }
                if (_mainMenuButton == null)
                {
                    Transform t = transform.Find("PauseCanvas/MainPausePanel/ButtonsContainer/MainMenuButton");
                    if (t != null) _mainMenuButton = t.GetComponent<Button>();
                }
                if (_controlsModalPanel == null)
                {
                    Transform t = transform.Find("PauseCanvas/ControlsModalPanel");
                    if (t != null) _controlsModalPanel = t.gameObject;
                }
                if (_controlsModalPanel != null && _closeControlsButton == null)
                {
                    _closeControlsButton = _controlsModalPanel.GetComponentInChildren<Button>(true);
                }

                if (_resumeButton != null)
                {
                    _resumeButton.onClick.RemoveAllListeners();
                    _resumeButton.onClick.AddListener(() => ResumeGame(true));
                }
                if (_controlsButton != null)
                {
                    _controlsButton.onClick.RemoveAllListeners();
                    _controlsButton.onClick.AddListener(OpenControlsModal);
                }
                if (_mainMenuButton != null)
                {
                    _mainMenuButton.onClick.RemoveAllListeners();
                    _mainMenuButton.onClick.AddListener(LoadMainMenu);
                }
                if (_closeControlsButton != null)
                {
                    _closeControlsButton.onClick.RemoveAllListeners();
                    _closeControlsButton.onClick.AddListener(CloseControlsModal);
                }

                _pauseCanvasGroup.gameObject.SetActive(false);
                if (_controlsModalPanel != null) _controlsModalPanel.SetActive(false);
                return;
            }

            // 2. Build Pause Canvas
            GameObject canvasObj = new GameObject("PauseCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _pauseCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // 3. Fullscreen Backdrop Dimmer
            GameObject bgDimObj = new GameObject("BackdropDimmer");
            bgDimObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgDimRect = bgDimObj.AddComponent<RectTransform>();
            bgDimRect.anchorMin = Vector2.zero;
            bgDimRect.anchorMax = Vector2.one;
            bgDimRect.sizeDelta = Vector2.zero;
            Image bgDimImg = bgDimObj.AddComponent<Image>();
            bgDimImg.color = new Color(0.04f, 0.04f, 0.06f, 0.82f);
            bgDimImg.raycastTarget = false;

            // 4. Main Pause Content Panel
            _mainPausePanel = new GameObject("MainPausePanel");
            _mainPausePanel.transform.SetParent(canvasObj.transform, false);
            RectTransform mainRect = _mainPausePanel.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.sizeDelta = Vector2.zero;

            // Title: "PAUSED"
            GameObject titleObj = new GameObject("PauseTitle");
            titleObj.transform.SetParent(_mainPausePanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.76f);
            titleRect.anchorMax = new Vector2(0.5f, 0.76f);
            titleRect.sizeDelta = new Vector2(600, 100);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _font;
            titleText.fontSize = 58;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            titleText.text = "PAUSED";
            titleText.raycastTarget = false;

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);

            // Button Container
            GameObject btnContainer = new GameObject("ButtonsContainer");
            btnContainer.transform.SetParent(_mainPausePanel.transform, false);
            RectTransform contRect = btnContainer.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.44f);
            contRect.anchorMax = new Vector2(0.5f, 0.44f);
            contRect.sizeDelta = new Vector2(320, 240);

            // Button 1: RESUME
            _resumeButton = CreatePauseButton(btnContainer.transform, "ResumeButton", "RESUME", new Vector2(0, 70), () => ResumeGame(true));

            // Button 2: CONTROLS
            _controlsButton = CreatePauseButton(btnContainer.transform, "ControlsButton", "CONTROLS", new Vector2(0, 0), OpenControlsModal);

            // Button 3: MAIN MENU
            _mainMenuButton = CreatePauseButton(btnContainer.transform, "MainMenuButton", "MAIN MENU", new Vector2(0, -70), LoadMainMenu);

            // 5. Controls Modal Panel
            BuildControlsModal(canvasObj.transform);

            canvasObj.SetActive(false);
        }

        private Button CreatePauseButton(Transform parent, string goName, string label, Vector2 pos, UnityEngine.Events.UnityAction onClickAction)
        {
            GameObject btnObj = new GameObject(goName);
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = pos;
            btnRect.sizeDelta = new Vector2(280, 58);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            btnImg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            colors.highlightedColor = new Color(0.38f, 0.44f, 0.60f, 1.0f);
            colors.pressedColor = new Color(0.08f, 0.09f, 0.12f, 1.0f);
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
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
            txt.raycastTarget = false;

            btn.onClick.AddListener(onClickAction);
            return btn;
        }

        private void BuildControlsModal(Transform parent)
        {
            _controlsModalPanel = new GameObject("ControlsModalPanel");
            _controlsModalPanel.transform.SetParent(parent, false);
            RectTransform modalRect = _controlsModalPanel.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            _controlsCanvasGroup = _controlsModalPanel.AddComponent<CanvasGroup>();
            _controlsCanvasGroup.alpha = 0f;

            Image dimmerImg = _controlsModalPanel.AddComponent<Image>();
            dimmerImg.color = new Color(0f, 0f, 0f, 0.8f);
            dimmerImg.raycastTarget = true;

            // Box
            GameObject boxObj = new GameObject("ModalBox");
            boxObj.transform.SetParent(_controlsModalPanel.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(720, 520);
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.15f, 0.98f);

            // Title
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

            // Content
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
                "<b>Escape</b>  —  Pause / Unpause Game\n" +
                "<b>Space</b>  —  Skip Cutscene";
            contentText.raycastTarget = false;

            // Back Button
            _closeControlsButton = CreatePauseButton(boxObj.transform, "CloseControlsButton", "BACK", Vector2.zero, CloseControlsModal);
            RectTransform closeRect = _closeControlsButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0.12f);
            closeRect.anchorMax = new Vector2(0.5f, 0.12f);
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = new Vector2(220, 50);

            _controlsModalPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
