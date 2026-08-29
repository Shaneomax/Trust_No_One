using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace V0.UI
{
    /// <summary>
    /// Manages the top-left horror game objective display.
    /// Uses high-performance OnGUI and UI Canvas rendering to guarantee 100% visibility on all screen resolutions and cameras.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Current Objective")]
        [SerializeField] private string _currentObjective = "Seek Help from the House";
        [SerializeField] private bool _showObjective = true;

        [Header("UI Component References (Optional Canvas Mode)")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _headerText;
        [SerializeField] private Text _objectiveText;

        [Header("Styling")]
        [SerializeField] private Color _headerColor = new Color(1f, 0.82f, 0.35f, 1f); // Horror amber gold
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.95f);

        private float _displayAlpha = 1f;
        private float _highlightTimer = 0f;
        private GUIStyle _headerStyle;
        private GUIStyle _objectiveStyle;
        private GUIStyle _shadowHeaderStyle;
        private GUIStyle _shadowObjectiveStyle;

        public static string CurrentObjective => Instance != null ? Instance._currentObjective : "Seek Help from the House";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitAtStartup()
        {
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

        private void Awake()
        {
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

            AutoWireCanvasReferences();
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_currentObjective))
            {
                _currentObjective = "Seek Help from the House";
            }
            UpdateCanvasUI();
        }

        private void Update()
        {
            if (_highlightTimer > 0f)
            {
                _highlightTimer -= Time.deltaTime;
            }
        }

        public void AutoWireCanvasReferences()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_objectiveText == null)
            {
                Text[] texts = GetComponentsInChildren<Text>(true);
                if (texts.Length > 1)
                {
                    _headerText = texts[0];
                    _objectiveText = texts[1];
                }
                else if (texts.Length == 1)
                {
                    _objectiveText = texts[0];
                }
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
            _showObjective = true;
            _displayAlpha = 1f;
            _highlightTimer = 1.8f;

            Debug.Log($"<color=yellow><b>[ObjectiveManager]</b></color> <color=white><b>{newObjective}</b></color>");

            UpdateCanvasUI();
        }

        private void UpdateCanvasUI()
        {
            if (_headerText != null)
            {
                _headerText.text = "OBJECTIVE";
                _headerText.color = _headerColor;
            }

            if (_objectiveText != null)
            {
                _objectiveText.DOKill();
                _objectiveText.text = _currentObjective;
                _objectiveText.color = new Color(1f, 0.9f, 0.5f);
                _objectiveText.DOColor(_textColor, 1.2f).SetDelay(0.3f);
                _objectiveText.transform.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.4f, 2, 0.5f);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 1f;
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
                if (Instance._canvasGroup != null)
                {
                    Instance._canvasGroup.DOKill();
                    Instance._canvasGroup.DOFade(0f, 0.5f);
                }
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
            if (!_showObjective || string.IsNullOrEmpty(_currentObjective)) return;

            InitGUIStyles();

            // Screen space coordinates (Top-Left)
            float startX = 42f;
            float startY = 38f;
            float width = 600f;

            // Highlight pulse color for new objectives
            Color activeTextColor = _highlightTimer > 0f ? Color.Lerp(_textColor, new Color(1f, 0.92f, 0.55f), _highlightTimer / 1.8f) : _textColor;
            _objectiveStyle.normal.textColor = activeTextColor;

            // 1. Draw Header Shadow & Text ("OBJECTIVE")
            GUI.Label(new Rect(startX + 1.5f, startY + 1.5f, width, 22f), "OBJECTIVE", _shadowHeaderStyle);
            GUI.Label(new Rect(startX, startY, width, 22f), "OBJECTIVE", _headerStyle);

            // 2. Draw Objective Shadow & Text (e.g. "Seek Help from the House")
            float objY = startY + 20f;
            GUI.Label(new Rect(startX + 1.5f, objY + 1.5f, width, 60f), _currentObjective, _shadowObjectiveStyle);
            GUI.Label(new Rect(startX, objY, width, 60f), _currentObjective, _objectiveStyle);
        }
    }
}
