using UnityEngine;
using UnityEngine.SceneManagement;

namespace V0.UI
{
    /// <summary>
    /// Manages the top-left horror game objective display.
    /// Pure OnGUI rendering eliminates all Canvas layer conflicts, font missing errors, and text overlapping bugs.
    /// Automatically hides in MainMenu, GoodEnding, OkayEnding, and any ending/credits scenes.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Current Objective")]
        [SerializeField] private string _currentObjective = "Seek Help from the House";
        [SerializeField] private bool _showObjective = true;

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

            _showObjective = ShouldShowObjectiveInCurrentScene();
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
            _highlightTimer = 2.0f;

            Debug.Log($"<color=yellow><b>[ObjectiveManager]</b></color> <color=white><b>{newObjective}</b></color>");
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

        private void InitGUIStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _headerStyle.normal.textColor = _headerColor;

            _shadowHeaderStyle = new GUIStyle(_headerStyle);
            _shadowHeaderStyle.normal.textColor = _shadowColor;

            _objectiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _objectiveStyle.normal.textColor = _textColor;

            _shadowObjectiveStyle = new GUIStyle(_objectiveStyle);
            _shadowObjectiveStyle.normal.textColor = _shadowColor;
        }

        private void OnGUI()
        {
            if (!_showObjective || string.IsNullOrEmpty(_currentObjective) || !ShouldShowObjectiveInCurrentScene()) return;

            InitGUIStyles();

            GUI.depth = -100; // Draw on top of all UI layers

            // Coordinates (Top-Left)
            float startX = 45f;
            float startY = 40f;
            float width = 600f;

            // Highlight pulse color for newly updated objectives
            Color activeTextColor = _highlightTimer > 0f
                ? Color.Lerp(_textColor, new Color(1f, 0.92f, 0.55f), _highlightTimer / 2.0f)
                : _textColor;
            _objectiveStyle.normal.textColor = activeTextColor;

            // 1. Draw Header ("OBJECTIVE")
            GUI.Label(new Rect(startX + 1.5f, startY + 1.5f, width, 22f), "OBJECTIVE", _shadowHeaderStyle);
            GUI.Label(new Rect(startX, startY, width, 22f), "OBJECTIVE", _headerStyle);

            // 2. Draw Active Objective
            float objY = startY + 20f;
            GUI.Label(new Rect(startX + 1.5f, objY + 1.5f, width, 60f), _currentObjective, _shadowObjectiveStyle);
            GUI.Label(new Rect(startX, objY, width, 60f), _currentObjective, _objectiveStyle);
        }
    }
}
