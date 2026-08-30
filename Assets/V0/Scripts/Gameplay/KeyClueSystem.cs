using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Gameplay
{
    /// <summary>
    /// Key Clue & Hint System:
    /// - Tracks progression keys in strict order: BedRoomKey -> DrawingRoomKey -> ChainSaw.
    /// - Activates after SecondTrigger (Stranger dialogue).
    /// - Every N seconds (e.g. 10s for testing, 60s default), if the current key has not been picked up:
    ///   * Displays a dedicated, crystal-clear Thought / Clue dialogue banner at the bottom of the screen.
    ///   * Spawns a pulsing glowing beacon at the target key's world position.
    /// - Automatically advances and resets the countdown timer whenever the required key is collected.
    /// </summary>
    [AddComponentMenu("Trust No One/Key Clue System")]
    public class KeyClueSystem : MonoBehaviour
    {
        public static KeyClueSystem Instance { get; private set; }

        [System.Serializable]
        public class KeyStage
        {
            [Tooltip("ID of the key (BedRoomKey, DrawingRoomKey, ChainSaw)")]
            public string keyId;

            [Tooltip("Target KeyPickup in the scene")]
            public KeyPickup targetKey;

            [Tooltip("Clue dialogue mumbled by the player if not collected in time")]
            [TextArea(2, 4)]
            public string hintDialogue;

            [Tooltip("Subtitle text color")]
            public Color textColor = new Color(1f, 0.92f, 0.65f);

            [Tooltip("Optional audio whisper/mumble clip")]
            public AudioClip mumbleAudio;
        }

        [Header("Hint Timing Settings")]
        [Tooltip("Seconds before showing a hint for the current uncollected key (Default: 60s)")]
        [SerializeField] private float _hintIntervalSeconds = 60.0f;

        [Tooltip("How long the clue subtitle stays on screen (seconds)")]
        [SerializeField] private float _hintDisplayDuration = 6.0f;

        [Header("Testing & Activation")]
        [Tooltip("If true, starts the clue timer immediately on Play (useful for quick testing without waiting for SecondTrigger)")]
        [SerializeField] private bool _startImmediatelyForTesting = false;

        [Header("Key Progression Stages (Strict Order)")]
        [SerializeField] private List<KeyStage> _stages = new List<KeyStage>()
        {
            new KeyStage()
            {
                keyId = "BedRoomKey",
                hintDialogue = "[Player]: \"The stranger said the Bedroom Key is upstairs on the 2nd floor... I should search the bedrooms.\"",
                textColor = new Color(1f, 0.92f, 0.65f)
            },
            new KeyStage()
            {
                keyId = "DrawingRoomKey",
                hintDialogue = "[Player]: \"Now I need the Drawing Room Key... It should be downstairs in the drawing room or study desk.\"",
                textColor = new Color(1f, 0.92f, 0.65f)
            },
            new KeyStage()
            {
                keyId = "ChainSaw",
                hintDialogue = "[Player]: \"I need the Chainsaw to cut the chains on the door... It must be outside in the barn or shed.\"",
                textColor = new Color(1f, 0.92f, 0.65f)
            }
        };

        [Header("Visual Indicator Settings")]
        [Tooltip("Spawn a subtle glowing pulse/beacon at the key location when the hint triggers")]
        [SerializeField] private bool _enableVisualBeacon = true;
        [SerializeField] private float _beaconDuration = 8.0f;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _audioSource;

        // Dedicated Self-Contained UI
        private Canvas _clueCanvas;
        private CanvasGroup _clueCanvasGroup;
        private RectTransform _cluePanelRect;
        private Text _clueText;
        private Tweener _fadeTween;
        private Tweener _scaleTween;

        private int _currentStageIndex = 0;
        private float _timer = 0f;
        private bool _isSystemActive = false;
        private bool _isDisplayingHint = false;
        private Coroutine _displayCoroutine;
        private GameObject _activeBeaconObj;

        public int CurrentStageIndex => _currentStageIndex;
        public float RemainingTime => Mathf.Max(0f, _hintIntervalSeconds - _timer);
        public bool IsActive => _isSystemActive;

        public static KeyClueSystem GetOrCreate()
        {
            if (Instance != null) return Instance;

            KeyClueSystem found = UnityEngine.Object.FindFirstObjectByType<KeyClueSystem>();
            if (found != null)
            {
                Instance = found;
                return Instance;
            }

            GameObject clueObj = GameObject.Find("Clue");
            if (clueObj == null)
            {
                clueObj = new GameObject("Clue");
            }

            Instance = clueObj.GetComponent<KeyClueSystem>() ?? clueObj.AddComponent<KeyClueSystem>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0f;
                }
            }

            EnsureClueUIHierarchy();
            AutoWireReferences();
        }

        private void Start()
        {
            EnsureClueUIHierarchy();
            AutoWireReferences();
            CheckCurrentStage();

            if (_startImmediatelyForTesting)
            {
                StartClueSystem();
            }
        }

        private void OnEnable()
        {
            KeyPickup.OnKeyCollected += HandleKeyCollected;

            StrangerDialogueCutscene strangerCutscene = UnityEngine.Object.FindFirstObjectByType<StrangerDialogueCutscene>();
            if (strangerCutscene != null)
            {
                strangerCutscene.OnCutsceneStarted += HandleStrangerMet;
                strangerCutscene.OnCutsceneCompleted += HandleStrangerMet;
            }
        }

        private void OnDisable()
        {
            KeyPickup.OnKeyCollected -= HandleKeyCollected;

            StrangerDialogueCutscene strangerCutscene = UnityEngine.Object.FindFirstObjectByType<StrangerDialogueCutscene>();
            if (strangerCutscene != null)
            {
                strangerCutscene.OnCutsceneStarted -= HandleStrangerMet;
                strangerCutscene.OnCutsceneCompleted -= HandleStrangerMet;
            }
        }

        private void Update()
        {
            // Activate once SecondTrigger has occurred or if testing
            if (!_isSystemActive)
            {
                if (_startImmediatelyForTesting || StrangerDialogueCutscene.HasMet)
                {
                    _isSystemActive = true;
                    _timer = 0f;
                    Debug.Log("<color=cyan><b>[KeyClueSystem]</b> Activated clue timer!</color>");
                }
                return;
            }

            // If all stages complete, stop
            if (_currentStageIndex >= _stages.Count) return;

            _timer += Time.deltaTime;

            if (_timer >= _hintIntervalSeconds)
            {
                _timer = 0f; // Reset timer for next cycle if still not picked up
                TriggerCurrentHint();
            }
        }

        public void StartClueSystem()
        {
            _isSystemActive = true;
            _timer = 0f;
            CheckCurrentStage();
            Debug.Log("<color=cyan><b>[KeyClueSystem]</b> Key Clue System Started!</color>");
        }

        private void HandleStrangerMet()
        {
            if (!_isSystemActive)
            {
                StartClueSystem();
            }
        }

        private void HandleKeyCollected(string keyId)
        {
            Debug.Log($"<color=yellow><b>[KeyClueSystem]</b> Key collected: {keyId}. Checking stage progress...</color>");
            
            // Advance stage and reset timer
            CheckCurrentStage();
            _timer = 0f; // Fresh timer for next key!

            // Hide any active hint
            if (_isDisplayingHint && _displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                HideHintUI();
            }
        }

        /// <summary>
        /// Evaluates collected keys to find the exact uncollected stage in order.
        /// </summary>
        public void CheckCurrentStage()
        {
            for (int i = 0; i < _stages.Count; i++)
            {
                if (!KeyPickup.HasKey(_stages[i].keyId))
                {
                    _currentStageIndex = i;
                    return;
                }
            }

            // All keys collected!
            _currentStageIndex = _stages.Count;
            _isSystemActive = false;
            Debug.Log("<color=green><b>[KeyClueSystem]</b> All progression keys collected! Clue system completed.</color>");
        }

        /// <summary>
        /// Displays the narrative thought clue and visual beacon for the current key.
        /// </summary>
        public void TriggerCurrentHint()
        {
            if (_currentStageIndex >= _stages.Count) return;

            KeyStage currentStage = _stages[_currentStageIndex];
            Debug.Log($"<color=yellow><b>[KeyClueSystem]</b> Interval reached without {currentStage.keyId}! Showing Clue: \"{currentStage.hintDialogue}\"</color>");

            if (_displayCoroutine != null) StopCoroutine(_displayCoroutine);
            _displayCoroutine = StartCoroutine(DisplayHintRoutine(currentStage));
        }

        private IEnumerator DisplayHintRoutine(KeyStage stage)
        {
            _isDisplayingHint = true;
            EnsureClueUIHierarchy();

            // 1. Play mumble audio if available
            if (stage.mumbleAudio != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(stage.mumbleAudio, 0.9f);
            }

            // 2. Display Subtitle Mumble on dedicated UI
            if (_clueText != null && _clueCanvasGroup != null)
            {
                _clueText.text = stage.hintDialogue;
                _clueText.color = stage.textColor;

                _fadeTween?.Kill();
                _fadeTween = _clueCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);

                if (_cluePanelRect != null)
                {
                    _scaleTween?.Kill();
                    _scaleTween = _cluePanelRect.DOScale(1f, 0.4f).From(0.92f).SetEase(Ease.OutBack);
                }
            }

            // 3. Optional Visual Beacon at key location
            if (_enableVisualBeacon)
            {
                Transform targetTrans = GetKeyTransform(stage);
                if (targetTrans != null)
                {
                    SpawnBeaconAt(targetTrans.position);
                }
            }

            yield return new WaitForSeconds(_hintDisplayDuration);

            HideHintUI();
        }

        private void HideHintUI()
        {
            _isDisplayingHint = false;

            if (_clueCanvasGroup != null)
            {
                _fadeTween?.Kill();
                _fadeTween = _clueCanvasGroup.DOFade(0f, 0.5f).SetEase(Ease.InQuad);
            }

            if (_activeBeaconObj != null)
            {
                Destroy(_activeBeaconObj);
                _activeBeaconObj = null;
            }
        }

        private Transform GetKeyTransform(KeyStage stage)
        {
            if (stage.targetKey != null) return stage.targetKey.transform;

            KeyPickup[] allKeys = UnityEngine.Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None);
            foreach (var k in allKeys)
            {
                if (k.KeyId != null && k.KeyId.Equals(stage.keyId, System.StringComparison.OrdinalIgnoreCase))
                {
                    stage.targetKey = k;
                    return k.transform;
                }
            }

            return null;
        }

        private void SpawnBeaconAt(Vector3 worldPos)
        {
            if (_activeBeaconObj != null) Destroy(_activeBeaconObj);

            _activeBeaconObj = new GameObject("KeyClueBeacon");
            _activeBeaconObj.transform.position = worldPos + Vector3.up * 0.35f;

            // Add soft glowing point light
            Light light = _activeBeaconObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.4f);
            light.range = 4.0f;
            light.intensity = 0f;

            light.DOIntensity(2.0f, 0.8f).SetLoops(6, LoopType.Yoyo);
            Destroy(_activeBeaconObj, _beaconDuration);
        }

        private void EnsureClueUIHierarchy()
        {
            if (_clueCanvas != null && _clueText != null) return;

            GameObject canvasObj = GameObject.Find("KeyClueCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("KeyClueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                DontDestroyOnLoad(canvasObj);
            }

            _clueCanvas = canvasObj.GetComponent<Canvas>();
            _clueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _clueCanvas.sortingOrder = 80;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Transform panelT = canvasObj.transform.Find("CluePanel");
            GameObject panelObj = panelT != null ? panelT.gameObject : new GameObject("CluePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            if (panelT == null) panelObj.transform.SetParent(canvasObj.transform, false);

            _cluePanelRect = panelObj.GetComponent<RectTransform>();
            _cluePanelRect.anchorMin = new Vector2(0.5f, 0f);
            _cluePanelRect.anchorMax = new Vector2(0.5f, 0f);
            _cluePanelRect.pivot = new Vector2(0.5f, 0f);
            _cluePanelRect.anchoredPosition = new Vector2(0f, 90f); // Centered near bottom
            _cluePanelRect.sizeDelta = new Vector2(900f, 75f);

            _clueCanvasGroup = panelObj.GetComponent<CanvasGroup>();
            _clueCanvasGroup.alpha = 0f;
            _clueCanvasGroup.blocksRaycasts = false;
            _clueCanvasGroup.interactable = false;

            Image panelBg = panelObj.GetComponent<Image>();
            panelBg.color = new Color(0.04f, 0.04f, 0.04f, 0.85f); // Sleek dark bar
            panelBg.raycastTarget = false;

            Transform textT = panelObj.transform.Find("ClueText");
            GameObject textObj = textT != null ? textT.gameObject : new GameObject("ClueText", typeof(RectTransform), typeof(Text), typeof(Outline));
            if (textT == null) textObj.transform.SetParent(panelObj.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(25f, 8f);
            textRect.offsetMax = new Vector2(-25f, -8f);

            _clueText = textObj.GetComponent<Text>();
            _clueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _clueText.fontSize = 20;
            _clueText.fontStyle = FontStyle.Bold;
            _clueText.alignment = TextAnchor.MiddleCenter;
            _clueText.color = new Color(1f, 0.92f, 0.65f);
            _clueText.raycastTarget = false;

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        public void AutoWireReferences()
        {
            // Auto-wire target keys in scene
            KeyPickup[] allKeys = UnityEngine.Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None);
            foreach (var stage in _stages)
            {
                if (stage.targetKey == null)
                {
                    foreach (var k in allKeys)
                    {
                        if (k != null && k.KeyId != null && k.KeyId.Equals(stage.keyId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            stage.targetKey = k;
                            break;
                        }
                    }
                }
            }
        }
    }
}
