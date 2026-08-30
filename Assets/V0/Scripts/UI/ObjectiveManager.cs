using UnityEngine;
using UnityEngine.SceneManagement;

namespace V0.UI
{
    /// <summary>
    /// Manages the top-left horror game objective display.
    /// Pure OnGUI rendering eliminates all Canvas layer conflicts, font missing errors, and text overlapping bugs.
    /// Automatically hides in MainMenu, GoodEnding, OkayEnding, and any ending/credits scenes,
    /// as well as during intro cutscenes until completed.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Current Objective")]
        [SerializeField] private string _currentObjective = "Seek Help from the House";
        [SerializeField] private bool _showObjective = false;

        [Header("Styling")]
        [SerializeField] private Color _headerColor = new Color(1f, 0.82f, 0.35f, 1f); // Horror amber gold
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.95f);

        private float _highlightTimer = 0f;
        private GUIStyle _headerStyle;
        private GUIStyle _objectiveStyle;
        private GUIStyle _shadowHeaderStyle;
        private GUIStyle _shadowObjectiveStyle;

        public static string CurrentObjective => Instance != null ? Instance._currentObjective : "Seek Help from the House";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitAtStartup()
        {
            CleanupDuplicateSceneUI();

            if (Instance == null)
            {
                ObjectiveManager found = Object.FindFirstObjectByType<ObjectiveManager>();
                if (found != null)
                {
                    Instance = found;
                }
                else
                {
                    GameObject go = new GameObject("[ObjectiveManager]");
                    Instance = go.AddComponent<ObjectiveManager>();
                    DontDestroyOnLoad(go);
                }
            }

            if (string.IsNullOrEmpty(Instance._currentObjective))
            {
                Instance._currentObjective = "Seek Help from the House";
            }
        }

        private static void CleanupDuplicateSceneUI()
        {
            // Destroy any old leftover Canvas UI containers to prevent any text ghosting/overlapping
            GameObject oldCanvas = GameObject.Find("ObjectiveCanvas");
            if (oldCanvas != null) Destroy(oldCanvas);

            GameObject oldContainer = GameObject.Find("ObjectiveContainer");
            if (oldContainer != null) Destroy(oldContainer);
        }

        private void Awake()
        {
            CleanupDuplicateSceneUI();

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupDuplicateSceneUI();

            if (!ShouldShowObjectiveInCurrentScene())
            {
                _showObjective = false;
                return;
            }

            // If a WakeUpSequenceController is present, hide objective until cutscene finishes
            var wakeUpSeq = Object.FindFirstObjectByType<V0.Cinematics.WakeUpSequenceController>();
            if (wakeUpSeq != null && wakeUpSeq.gameObject.activeInHierarchy)
            {
                _showObjective = false;
            }
            else
            {
                _showObjective = true;
            }
        }

        private bool ShouldShowObjectiveInCurrentScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return false;

            // Completely hide in MainMenu, GoodEnding, OkayEnding, BadEnding, or any menu/ending scenes
            if (sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("ending", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("credit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_currentObjective))
            {
                _currentObjective = "Seek Help from the House";
            }

            // Check if intro cutscene is running in this scene
            var wakeUpSeq = Object.FindFirstObjectByType<V0.Cinematics.WakeUpSequenceController>();
            if (wakeUpSeq != null && wakeUpSeq.gameObject.activeInHierarchy)
            {
                _showObjective = false;
            }
            else
            {
                _showObjective = ShouldShowObjectiveInCurrentScene();
            }
        }

        private void Update()
        {
            if (_highlightTimer > 0f)
            {
                _highlightTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Global static method to update the active objective from any script.
        /// </summary>
        public static void SetObjective(string newObjective)
        {
            if (string.IsNullOrEmpty(newObjective)) return;

            if (Instance == null)
            {
                AutoInitAtStartup();
            }

            if (Instance != null)
            {
                Instance.SetObjectiveInternal(newObjective);
            }
        }

        private void SetObjectiveInternal(string newObjective)
        {
            _currentObjective = newObjective;
            _showObjective = ShouldShowObjectiveInCurrentScene();
            _highlightTimer = 2.5f;

            Debug.Log($"<color=yellow><b>[ObjectiveManager]</b></color> <color=white><b>{newObjective}</b></color>");
        }

        /// <summary>
        /// Shows or hides the objective display directly.
        /// </summary>
        public static void SetVisible(bool visible)
        {
            if (Instance != null)
            {
                Instance._showObjective = visible && Instance.ShouldShowObjectiveInCurrentScene();
                if (visible)
                {
                    Instance._highlightTimer = 2.5f;
                }
            }
        }

        /// <summary>
        /// Clears or hides the objective banner.
        /// </summary>
        public static void ClearObjective()
        {
            if (Instance != null)
            {
                Instance._showObjective = false;
            }
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                _headerStyle.normal.textColor = _headerColor;

                _objectiveStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                _objectiveStyle.normal.textColor = _textColor;

                _shadowHeaderStyle = new GUIStyle(_headerStyle);
                _shadowHeaderStyle.normal.textColor = _shadowColor;

                _shadowObjectiveStyle = new GUIStyle(_objectiveStyle);
                _shadowObjectiveStyle.normal.textColor = _shadowColor;
            }
        }

        private void OnGUI()
        {
            if (!_showObjective || string.IsNullOrEmpty(_currentObjective)) return;

            InitStyles();

            // Responsive positioning for any screen resolution
            float screenScale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 1.4f);
            float startX = 35f * screenScale;
            float startY = 32f * screenScale;
            float panelWidth = 550f * screenScale;
            float headerHeight = 22f * screenScale;
            float objectiveHeight = 35f * screenScale;

            _headerStyle.fontSize = Mathf.RoundToInt(13 * screenScale);
            _objectiveStyle.fontSize = Mathf.RoundToInt(18 * screenScale);
            _shadowHeaderStyle.fontSize = _headerStyle.fontSize;
            _shadowObjectiveStyle.fontSize = _objectiveStyle.fontSize;

            // Pulsing highlight effect when updated
            Color textCol = _textColor;
            if (_highlightTimer > 0f)
            {
                float pulse = Mathf.PingPong(_highlightTimer * 4f, 1f);
                textCol = Color.Lerp(_textColor, new Color(1f, 0.9f, 0.4f, 1f), pulse);
            }
            _objectiveStyle.normal.textColor = textCol;

            // Draw "OBJECTIVE" Tag Shadow & Text
            Rect headerShadowRect = new Rect(startX + 1.5f, startY + 1.5f, panelWidth, headerHeight);
            Rect headerRect = new Rect(startX, startY, panelWidth, headerHeight);
            GUI.Label(headerShadowRect, "OBJECTIVE", _shadowHeaderStyle);
            GUI.Label(headerRect, "OBJECTIVE", _headerStyle);

            // Draw Current Objective Shadow & Text
            float objY = startY + headerHeight - (2f * screenScale);
            Rect objShadowRect = new Rect(startX + 1.5f, objY + 1.5f, panelWidth, objectiveHeight);
            Rect objRect = new Rect(startX, objY, panelWidth, objectiveHeight);
            GUI.Label(objShadowRect, _currentObjective, _shadowObjectiveStyle);
            GUI.Label(objRect, _currentObjective, _objectiveStyle);
        }
    }
}
