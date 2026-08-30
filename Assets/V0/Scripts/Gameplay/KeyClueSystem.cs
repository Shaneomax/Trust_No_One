using System;
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
    /// Narrative Key & Clue Progression System:
    /// 1. Player searches for Chainsaw Room / Toolshed (Hint 1).
    /// 2. When Chainsaw Room is locked -> Hint 2 points to 2nd Floor Bedroom.
    /// 3. When 2nd Floor Bedroom is locked -> Hint 3 points to Drawing Room Key downstairs.
    /// 4. After getting Drawing Room Key -> Hint 4 shows (tells to get Bedroom Key).
    /// 5. After getting Bedroom Key -> Hint 5 shows (tells to get Chainsaw).
    /// 6. After getting Chainsaw -> Hint 6 shows (tells to cut door chains).
    /// </summary>
    [AddComponentMenu("Trust No One/Key Clue System")]
    public class KeyClueSystem : MonoBehaviour
    {
        public static KeyClueSystem Instance { get; private set; }

        public enum ClueState
        {
            SearchChainsawRoom = 0,
            ChainsawLocked_SearchBedroom = 1,
            BedroomLocked_SearchDrawingRoom = 2,
            GotDrawingRoomKey_GetBedroomKey = 3,
            GotBedroomKey_GetChainsaw = 4,
            GotChainsaw_CutChains = 5,
            Completed = 6
        }

        [Header("Hint Timing Settings")]
        [Tooltip("Seconds before showing a hint if player hasn't progressed (Default: 60s)")]
        [SerializeField] private float _hintIntervalSeconds = 60.0f;

        [Tooltip("How long the clue banner stays on screen (seconds)")]
        [SerializeField] private float _hintDisplayDuration = 6.0f;

        [Header("Testing & Activation")]
        [Tooltip("If true, starts the clue timer immediately on Play (without needing SecondTrigger)")]
        [SerializeField] private bool _startImmediatelyForTesting = false;

        [Header("Current Narrative State")]
        [SerializeField] private ClueState _currentState = ClueState.SearchChainsawRoom;

        [Header("Clue Dialogues (Customizable in Inspector)")]
        [TextArea(2, 4)]
        [SerializeField] private string _hint1_SearchChainsaw = "[Player]: \"The stranger said I need a chainsaw to free him... I should check the toolshed or barn outside.\"";

        [TextArea(2, 4)]
        [SerializeField] private string _hint2_ChainsawLocked = "[Player]: \"The chainsaw room is locked! The stranger said the key might be in the 2nd floor bedroom upstairs.\"";

        [TextArea(2, 4)]
        [SerializeField] private string _hint3_BedroomLocked = "[Player]: \"The 2nd floor bedroom is locked too! I need to search the Drawing Room downstairs for the key.\"";

        [TextArea(2, 4)]
        [SerializeField] private string _hint4_GotDrawingRoomKey = "[Player]: \"I have the Drawing Room Key! Now I can find the Bedroom Key inside the drawing room.\"";

        [TextArea(2, 4)]
        [SerializeField] private string _hint5_GotBedroomKey = "[Player]: \"Let's get the chainsaw from downstairs.\"";

        [TextArea(2, 4)]
        [SerializeField] private string _hint6_GotChainsaw = "[Player]: \"I have the Chainsaw! Time to cut the chains on the door and free the stranger.\"";

        [Header("Visual Indicator Settings")]
        [Tooltip("Spawn a soft glowing beacon at the clue destination when the hint triggers")]
        [SerializeField] private bool _enableVisualBeacon = true;
        [SerializeField] private float _beaconDuration = 8.0f;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _clueChimeAudio;

        // Dedicated UI References
        private Canvas _clueCanvas;
        private CanvasGroup _clueCanvasGroup;
        private RectTransform _cluePanelRect;
        private Text _clueText;
        private Tweener _fadeTween;
        private Tweener _scaleTween;

        private float _timer = 0f;
        private bool _isSystemActive = false;
        private bool _isDisplayingHint = false;
        private Coroutine _displayCoroutine;
        private GameObject _activeBeaconObj;

        public ClueState CurrentState => _currentState;
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
        }

        private void Start()
        {
            EnsureClueUIHierarchy();
            EvaluateStateFromInventory();

            if (_startImmediatelyForTesting)
            {
                StartClueSystem();
            }
        }

        private void OnEnable()
        {
            KeyPickup.OnKeyCollected += HandleKeyCollected;
            DoorInteractable.OnLockedDoorInteracted += HandleLockedDoorInteracted;
            DoorInteractable.OnDoorUnlocked += HandleDoorUnlocked;

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
            DoorInteractable.OnLockedDoorInteracted -= HandleLockedDoorInteracted;
            DoorInteractable.OnDoorUnlocked -= HandleDoorUnlocked;

            StrangerDialogueCutscene strangerCutscene = UnityEngine.Object.FindFirstObjectByType<StrangerDialogueCutscene>();
            if (strangerCutscene != null)
            {
                strangerCutscene.OnCutsceneStarted -= HandleStrangerMet;
                strangerCutscene.OnCutsceneCompleted -= HandleStrangerMet;
            }
        }

        private void Update()
        {
            // If already finished or chainsaw door unlocked, do nothing!
            if (_currentState == ClueState.Completed) return;

            // Activate once SecondTrigger has occurred or if testing
            if (!_isSystemActive)
            {
                if (_startImmediatelyForTesting || StrangerDialogueCutscene.HasMet)
                {
                    _isSystemActive = true;
                    _timer = 0f;
                    Debug.Log("<color=cyan><b>[KeyClueSystem]</b> Activated clue system timer!</color>");
                }
                return;
            }

            _timer += Time.deltaTime;

            if (_timer >= _hintIntervalSeconds)
            {
                _timer = 0f; // Reset timer for next cycle
                TriggerCurrentHint();
            }
        }

        public void StartClueSystem()
        {
            if (_currentState == ClueState.Completed) return;

            _isSystemActive = true;
            _timer = 0f;
            EvaluateStateFromInventory();
            Debug.Log($"<color=cyan><b>[KeyClueSystem]</b> Started in state: {_currentState}</color>");
        }

        private void HandleStrangerMet()
        {
            if (!_isSystemActive && _currentState != ClueState.Completed)
            {
                StartClueSystem();
            }
        }

        private void HandleLockedDoorInteracted(DoorInteractable door, string requiredKeyId)
        {
            if (_currentState == ClueState.Completed) return;

            Debug.Log($"<color=yellow><b>[KeyClueSystem]</b> Player tried locked door with required key '{requiredKeyId}'. Current state: {_currentState}</color>");

            // 1. If player tries Chainsaw Room door while searching for chainsaw -> advance state to search Bedroom (with full delay before hint!)
            if (_currentState == ClueState.SearchChainsawRoom)
            {
                SetState(ClueState.ChainsawLocked_SearchBedroom, showImmediateHint: false);
                return;
            }

            // 2. If player tries 2nd Floor Bedroom door -> advance state to search Drawing Room Key (with full delay before hint!)
            if (_currentState == ClueState.ChainsawLocked_SearchBedroom || _currentState == ClueState.SearchChainsawRoom)
            {
                if (!string.IsNullOrEmpty(requiredKeyId) && requiredKeyId.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetState(ClueState.BedroomLocked_SearchDrawingRoom, showImmediateHint: false);
                    return;
                }
            }
        }

        private void HandleKeyCollected(string keyId)
        {
            if (_currentState == ClueState.Completed) return;

            Debug.Log($"<color=yellow><b>[KeyClueSystem]</b> Key collected: '{keyId}'. Current state: {_currentState}</color>");

            // 1. Drawing Room Key / Kitchen Key -> Advance to Hint 4
            if (!string.IsNullOrEmpty(keyId) && (keyId.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 || keyId.IndexOf("kitchen", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                SetState(ClueState.GotDrawingRoomKey_GetBedroomKey, showImmediateHint: false);
                return;
            }

            // 2. Bedroom Key / Crowbar -> Advance to Hint 5
            if (!string.IsNullOrEmpty(keyId) && (keyId.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0 || keyId.IndexOf("crowbar", StringComparison.OrdinalIgnoreCase) >= 0 || keyId.IndexOf("haligan", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                SetState(ClueState.GotBedroomKey_GetChainsaw, showImmediateHint: false);
                return;
            }

            // 3. Chainsaw -> Advance to Hint 6
            if (!string.IsNullOrEmpty(keyId) && keyId.IndexOf("chainsaw", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetState(ClueState.GotChainsaw_CutChains, showImmediateHint: false);
                return;
            }

            EvaluateStateFromInventory();
        }

        private void HandleDoorUnlocked(DoorInteractable door, string requiredKeyId)
        {
            // When the Chainsaw door is unlocked, complete the clue system permanently!
            if (!string.IsNullOrEmpty(requiredKeyId) && requiredKeyId.Equals("ChainSaw", StringComparison.OrdinalIgnoreCase))
            {
                CompleteSystem();
            }
        }

        public void CompleteSystem()
        {
            _currentState = ClueState.Completed;
            _isSystemActive = false;
            _timer = 0f;

            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            HideHintUI();
            Debug.Log("<color=green><b>[KeyClueSystem]</b> Chainsaw door unlocked! Clue system completely finished - no more prompts will show.</color>");
        }

        public void SetState(ClueState newState, bool showImmediateHint = false)
        {
            _currentState = newState;
            _timer = 0f; // Reset countdown timer for fresh duration
            _isSystemActive = true; // Ensure timer is actively ticking
            Debug.Log($"<color=green><b>[KeyClueSystem]</b> Set state to: {_currentState} (Hint will show in {_hintIntervalSeconds}s)</color>");

            if (showImmediateHint)
            {
                TriggerCurrentHint();
            }
        }

        public void EvaluateStateFromInventory()
        {
            if (KeyPickup.HasKey("ChainSaw"))
            {
                _currentState = ClueState.GotChainsaw_CutChains;
            }
            else if (KeyPickup.HasKey("BedRoomKey"))
            {
                _currentState = ClueState.GotBedroomKey_GetChainsaw;
            }
            else if (KeyPickup.HasKey("DrawingRoomKey"))
            {
                _currentState = ClueState.GotDrawingRoomKey_GetBedroomKey;
            }
        }

        /// <summary>
        /// Displays the narrative thought clue and visual beacon for the current progression step.
        /// </summary>
        public void TriggerCurrentHint()
        {
            string dialogue = "";
            Vector3 beaconPosition = Vector3.zero;
            bool hasBeacon = false;

            switch (_currentState)
            {
                case ClueState.SearchChainsawRoom:
                    dialogue = _hint1_SearchChainsaw;
                    beaconPosition = FindObjectPosition("Chainsaw", "SM_Door_interior_01");
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.ChainsawLocked_SearchBedroom:
                    dialogue = _hint2_ChainsawLocked;
                    beaconPosition = FindObjectPosition("BedRoomKey", "Bedroom");
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.BedroomLocked_SearchDrawingRoom:
                    dialogue = _hint3_BedroomLocked;
                    beaconPosition = FindKeyPosition("DrawingRoomKey");
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.GotDrawingRoomKey_GetBedroomKey:
                    dialogue = _hint4_GotDrawingRoomKey;
                    beaconPosition = FindKeyPosition("BedRoomKey");
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.GotBedroomKey_GetChainsaw:
                    dialogue = _hint5_GotBedroomKey;
                    beaconPosition = FindKeyPosition("ChainSaw");
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.GotChainsaw_CutChains:
                    dialogue = _hint6_GotChainsaw;
                    beaconPosition = FindChainedDoorPosition();
                    hasBeacon = beaconPosition != Vector3.zero;
                    break;

                case ClueState.Completed:
                    return;
            }

            Debug.Log($"<color=yellow><b>[KeyClueSystem]</b> Showing Clue ({_currentState}): \"{dialogue}\"</color>");

            if (_displayCoroutine != null) StopCoroutine(_displayCoroutine);
            _displayCoroutine = StartCoroutine(DisplayHintRoutine(dialogue, hasBeacon ? beaconPosition : (Vector3?)null));
        }

        private IEnumerator DisplayHintRoutine(string dialogueText, Vector3? beaconPos)
        {
            _isDisplayingHint = true;
            EnsureClueUIHierarchy();

            // 1. Play audio chime if available
            if (_clueChimeAudio != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_clueChimeAudio, 0.8f);
            }

            // 2. Display Subtitle Mumble on dedicated UI
            if (_clueText != null && _clueCanvasGroup != null)
            {
                _clueText.text = dialogueText;

                _fadeTween?.Kill();
                _fadeTween = _clueCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);

                if (_cluePanelRect != null)
                {
                    _scaleTween?.Kill();
                    _scaleTween = _cluePanelRect.DOScale(1f, 0.4f).From(0.92f).SetEase(Ease.OutBack);
                }
            }

            // 3. Optional Visual Beacon at target location
            if (_enableVisualBeacon && beaconPos.HasValue)
            {
                SpawnBeaconAt(beaconPos.Value);
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

        private Vector3 FindKeyPosition(string keyId)
        {
            KeyPickup[] allKeys = UnityEngine.Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None);
            foreach (var k in allKeys)
            {
                if (k != null && k.KeyId != null && k.KeyId.Equals(keyId, StringComparison.OrdinalIgnoreCase))
                {
                    return k.transform.position;
                }
            }
            return Vector3.zero;
        }

        private Vector3 FindChainedDoorPosition()
        {
            DoorInteractable[] doors = UnityEngine.Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (d != null && d.RequiredKeyId != null && d.RequiredKeyId.Equals("ChainSaw", StringComparison.OrdinalIgnoreCase))
                {
                    return d.transform.position;
                }
            }
            return Vector3.zero;
        }

        private Vector3 FindObjectPosition(params string[] searchNames)
        {
            foreach (string name in searchNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null) return obj.transform.position;
            }
            return Vector3.zero;
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
            if (_clueCanvas != null && _clueText != null && _clueCanvasGroup != null) return;

            GameObject canvasObj = GameObject.Find("KeyClueCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("KeyClueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            _cluePanelRect.sizeDelta = new Vector2(920f, 75f);

            _clueCanvasGroup = panelObj.GetComponent<CanvasGroup>() ?? panelObj.AddComponent<CanvasGroup>();
            _clueCanvasGroup.alpha = 0f;
            _clueCanvasGroup.blocksRaycasts = false;
            _clueCanvasGroup.interactable = false;

            Image panelBg = panelObj.GetComponent<Image>() ?? panelObj.AddComponent<Image>();
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
            _clueText.fontSize = 19;
            _clueText.fontStyle = FontStyle.Bold;
            _clueText.alignment = TextAnchor.MiddleCenter;
            _clueText.color = new Color(1f, 0.92f, 0.65f);
            _clueText.raycastTarget = false;

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }
}
