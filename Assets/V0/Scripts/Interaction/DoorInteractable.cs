using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using V0.Cinematics;

namespace V0.Interaction
{
    /// <summary>
    /// Interactable door component using DOTween for smooth opening and closing animations.
    /// Implements IInteractable. Supports multi-key matching, locked door handle shake,
    /// and first-interaction locked clue dialogues from the trapped stranger.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [System.Serializable]
        public class LockedDialogueLine
        {
            [TextArea(1, 3)]
            public string text;
            public Color color = new Color(1f, 0.88f, 0.6f);
            public float duration = 4.5f;
        }

        [Header("Interaction Prompts")]
        [SerializeField] private string _openPrompt = "Open Door";
        [SerializeField] private string _closePrompt = "Close Door";
        [SerializeField] private string _lockedPrompt = "Locked (Need Key)";
        [SerializeField] private string _unlockPrompt = "Unlock Door";

        [Header("Door State")]
        [Tooltip("Is the door currently open?")]
        [SerializeField] private bool _isOpen = false;

        [Header("Lock Settings")]
        [Tooltip("Is the door locked until unlocked with a key?")]
        [SerializeField] private bool _isLocked = false;

        [Tooltip("Direct reference to the specific Key GameObject needed for this door. (Drag & drop the Key here!)")]
        [SerializeField] private KeyPickup _requiredKey;

        [Tooltip("Or match by Key ID string (e.g. 'DrawingRoomKey', 'BedroomKey', 'AtticKey')")]
        [SerializeField] private string _requiredKeyId = "DrawingRoomKey";

        [Header("Locked Clue Dialogue (Optional)")]
        [Tooltip("Trigger dialogue when player first tries to open this locked door without key")]
        [SerializeField] private bool _enableLockedDialogue = true;

        [Tooltip("Only show locked dialogue AFTER the stranger has been met (SecondTrigger cutscene has played)")]
        [SerializeField] private bool _requireStrangerMetFirst = true;

        [Tooltip("Only trigger this dialogue after the main front entrance door has been slammed and locked")]
        [SerializeField] private bool _requireMainDoorLockedFirst = true;

        [Tooltip("Reference to the main entrance door. Auto-finds SM_Door_Front_01 if left empty.")]
        [SerializeField] private DoorInteractable _mainFrontDoorReference;

        [Tooltip("List of dialogue lines triggered on first locked interaction")]
        [SerializeField] private List<LockedDialogueLine> _lockedDialogueLines = new List<LockedDialogueLine>()
        {
            new LockedDialogueLine()
            {
                text = "[Stranger Behind Door]: \"Hey! That door is locked! You'll need the key from our bedroom on the 2nd floor!\"",
                color = new Color(1f, 0.88f, 0.6f),
                duration = 4.8f
            },
            new LockedDialogueLine()
            {
                text = "[Stranger Behind Door]: \"Be careful... my crazy wife is guarding that key! And that bedroom door gets stuck from time to time—you might have to force it open!\"",
                color = new Color(1f, 0.88f, 0.6f),
                duration = 5.5f
            },
            new LockedDialogueLine()
            {
                text = "[Player]: \"2nd floor bedroom... got it. I need to watch out for her.\"",
                color = new Color(0.95f, 0.95f, 0.9f),
                duration = 3.5f
            }
        };

        [Header("On Lock - Activate GameObject")]
        [Tooltip("Drag any GameObject here. It will be SetActive(true) when this door is slammed and locked (e.g. a SecondTrigger, UI hint, etc.)")]
        [SerializeField] private GameObject _objectToActivateOnLock;

        [Header("On Unlock - GameObjects to Deactivate / Activate")]
        [Tooltip("Drag any GameObject here (e.g. chains, padlock, barricade). It will be SetActive(false) as soon as this door is unlocked.")]
        [SerializeField] private GameObject _objectToDeactivateOnUnlock;

        [Tooltip("List of multiple GameObjects to SetActive(false) on unlock (e.g. multiple chain meshes).")]
        [SerializeField] private List<GameObject> _objectsToDeactivateOnUnlock = new List<GameObject>();

        [Tooltip("Optional GameObject to SetActive(true) when this door is unlocked.")]
        [SerializeField] private GameObject _objectToActivateOnUnlock;

        [Tooltip("List of multiple GameObjects to SetActive(true) on unlock.")]
        [SerializeField] private List<GameObject> _objectsToActivateOnUnlock = new List<GameObject>();

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip _unlockSound;
        [SerializeField] private AudioClip _lockedJiggleSound;

        [Header("Animation Settings")]
        [Tooltip("Transform to rotate. If left unassigned, uses this GameObject's transform.")]
        [SerializeField] private Transform _doorTransform;

        [Tooltip("Local Euler angles when the door is closed.")]
        [SerializeField] private Vector3 _closedRotation = Vector3.zero;

        [Tooltip("Local Euler angles when the door is open.")]
        [SerializeField] private Vector3 _openRotation = new Vector3(0f, -90f, 0f);

        [Tooltip("Duration of the open/close animation in seconds.")]
        [SerializeField] private float _animationDuration = 0.8f;

        [Tooltip("Easing curve for opening.")]
        [SerializeField] private Ease _openEase = Ease.OutQuad;

        [Tooltip("Easing curve for closing.")]
        [SerializeField] private Ease _closeEase = Ease.InQuad;

        private bool _hasTriggeredLockedDialogue = false;
        private Coroutine _dialogueCoroutine;
        private static Text _cachedSubtitleText;
        private static CanvasGroup _cachedLetterboxGroup;

        /// <summary>
        /// Checks if the player holds the exact key needed for this specific door.
        /// </summary>
        public bool PlayerHasKeyForThisDoor()
        {
            // 1. If direct KeyPickup reference is assigned in inspector, check that!
            if (_requiredKey != null)
            {
                return KeyPickup.HasKey(_requiredKey);
            }

            // 2. Otherwise check matching Key ID string
            if (!string.IsNullOrEmpty(_requiredKeyId))
            {
                return KeyPickup.HasKey(_requiredKeyId);
            }

            // 3. Fallback: if locked with no specific key assigned, any key works
            return KeyPickup.HasAnyKey;
        }

        public string InteractionPrompt
        {
            get
            {
                if (_isLocked)
                {
                    return PlayerHasKeyForThisDoor() ? _unlockPrompt : _lockedPrompt;
                }
                return _isOpen ? _closePrompt : _openPrompt;
            }
        }

        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;

        private void Awake()
        {
            if (_doorTransform == null)
            {
                _doorTransform = transform;
            }
        }

        public void Interact()
        {
            if (_doorTransform == null)
            {
                _doorTransform = transform;
            }

            if (_isLocked)
            {
                if (PlayerHasKeyForThisDoor())
                {
                    // Player has the specific key for this door: unlock!
                    _isLocked = false;
                    if (_unlockSound != null)
                    {
                        AudioSource.PlayClipAtPoint(_unlockSound, transform.position, 1.0f);
                    }

                    // Deactivate assigned GameObjects (e.g. chains, padlock, barricades)
                    if (_objectToDeactivateOnUnlock != null)
                    {
                        _objectToDeactivateOnUnlock.SetActive(false);
                    }
                    if (_objectsToDeactivateOnUnlock != null)
                    {
                        foreach (GameObject go in _objectsToDeactivateOnUnlock)
                        {
                            if (go != null) go.SetActive(false);
                        }
                    }

                    // Activate assigned GameObjects (if any)
                    if (_objectToActivateOnUnlock != null)
                    {
                        _objectToActivateOnUnlock.SetActive(true);
                    }
                    if (_objectsToActivateOnUnlock != null)
                    {
                        foreach (GameObject go in _objectsToActivateOnUnlock)
                        {
                            if (go != null) go.SetActive(true);
                        }
                    }

                    Debug.Log($"<color=green>[DoorInteractable]</color> Unlocked '{gameObject.name}' with required key!");
                }
                else
                {
                    // Door is locked: jiggle handle animation
                    if (_lockedJiggleSound != null)
                    {
                        AudioSource.PlayClipAtPoint(_lockedJiggleSound, transform.position, 1.0f);
                    }
                    _doorTransform.DOKill();
                    _doorTransform.DOShakeRotation(0.25f, new Vector3(0, 4f, 0), 10, 90, false);
                    string keyName = _requiredKey != null ? _requiredKey.name : _requiredKeyId;
                    Debug.Log($"<color=yellow>[DoorInteractable]</color> '{gameObject.name}' is locked. Requires key: '{keyName}'");

                    // Trigger locked clue dialogue on first attempt
                    // Gate: only after main door is locked AND stranger has been met (SecondTrigger fired)
                    bool strangerConditionMet = !_requireStrangerMetFirst || StrangerDialogueCutscene.HasMet;
                    if (_enableLockedDialogue && !_hasTriggeredLockedDialogue && IsMainDoorLocked() && strangerConditionMet && _lockedDialogueLines != null && _lockedDialogueLines.Count > 0)
                    {
                        _hasTriggeredLockedDialogue = true;
                        if (_dialogueCoroutine != null) StopCoroutine(_dialogueCoroutine);
                        _dialogueCoroutine = StartCoroutine(PlayLockedDialogueRoutine());
                    }

                    return;
                }
            }

            _isOpen = !_isOpen;

            // Stop any running tween on this transform to smoothly handle rapid interactions
            _doorTransform.DOKill();

            Vector3 targetRotation = _isOpen ? _openRotation : _closedRotation;
            Ease targetEase = _isOpen ? _openEase : _closeEase;

            _doorTransform.DOLocalRotate(targetRotation, _animationDuration)
                .SetEase(targetEase);

            if (_isOpen)
            {
                Debug.Log("Player opens the door");
            }
            else
            {
                Debug.Log("Player closes the door");
            }
        }

        /// <summary>
        /// Explicitly opens the door if currently closed.
        /// </summary>
        public void OpenDoor()
        {
            if (!_isOpen)
            {
                Interact();
            }
        }

        /// <summary>
        /// Explicitly closes the door if currently open.
        /// </summary>
        public void CloseDoor()
        {
            if (_isOpen)
            {
                Interact();
            }
        }

        private IEnumerator PlayLockedDialogueRoutine()
        {
            FindSubtitleReferences();

            if (_cachedLetterboxGroup != null)
            {
                _cachedLetterboxGroup.DOKill();
                _cachedLetterboxGroup.DOFade(1f, 0.5f);
            }

            for (int i = 0; i < _lockedDialogueLines.Count; i++)
            {
                LockedDialogueLine line = _lockedDialogueLines[i];
                if (line == null || string.IsNullOrEmpty(line.text)) continue;

                if (_cachedSubtitleText != null)
                {
                    _cachedSubtitleText.DOKill();
                    _cachedSubtitleText.text = line.text;
                    _cachedSubtitleText.color = new Color(line.color.r, line.color.g, line.color.b, 0f);
                    _cachedSubtitleText.DOFade(1f, 0.4f);
                }

                yield return new WaitForSeconds(line.duration);

                if (_cachedSubtitleText != null)
                {
                    _cachedSubtitleText.DOFade(0f, 0.4f);
                }
                yield return new WaitForSeconds(0.25f);
            }

            if (_cachedLetterboxGroup != null)
            {
                _cachedLetterboxGroup.DOKill();
                _cachedLetterboxGroup.DOFade(0f, 0.5f);
            }
            _dialogueCoroutine = null;
        }

        private void FindSubtitleReferences()
        {
            if (_cachedSubtitleText == null)
            {
                GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
                if (canvasObj != null)
                {
                    _cachedSubtitleText = canvasObj.GetComponentInChildren<Text>();
                    _cachedLetterboxGroup = canvasObj.GetComponent<CanvasGroup>();
                }
            }
        }

        private bool IsMainDoorLocked()
        {
            if (!_requireMainDoorLockedFirst) return true;

            if (_mainFrontDoorReference == null)
            {
                GameObject frontDoorObj = GameObject.Find("SM_Door_Front_01");
                if (frontDoorObj != null)
                {
                    _mainFrontDoorReference = frontDoorObj.GetComponent<DoorInteractable>();
                    if (_mainFrontDoorReference == null) _mainFrontDoorReference = frontDoorObj.GetComponentInParent<DoorInteractable>();
                }
            }

            if (_mainFrontDoorReference != null)
            {
                return _mainFrontDoorReference.IsLocked;
            }

            return true;
        }

        /// <summary>
        /// Forces the door to slam shut and lock itself immediately (e.g. triggered by cutscenes / horror events).
        /// </summary>
        public void ForceSlamAndLock(AudioClip slamSound = null, float duration = 0.25f)
        {
            if (_doorTransform == null) _doorTransform = transform;

            _isOpen = false;
            _isLocked = true;

            _doorTransform.DOKill();
            _doorTransform.DOLocalRotate(_closedRotation, duration).SetEase(Ease.InQuad).OnComplete(() =>
            {
                // Impact shake
                _doorTransform.DOShakePosition(0.25f, new Vector3(0.03f, 0.02f, 0.03f), 15);
            });

            if (slamSound != null)
            {
                AudioSource.PlayClipAtPoint(slamSound, transform.position, 1.0f);
            }

            // Activate any assigned GameObject when door locks (e.g. SecondTrigger, hint UI)
            if (_objectToActivateOnLock != null)
            {
                _objectToActivateOnLock.SetActive(true);
                Debug.Log($"<color=cyan>[DoorInteractable]</color> Door locked → Activated '{_objectToActivateOnLock.name}'!");
            }

            Debug.Log($"<color=red>[DoorInteractable]</color> '{gameObject.name}' SLAMMED SHUT and LOCKED!");
        }

        private void OnDestroy()
        {
            if (_doorTransform != null)
            {
                _doorTransform.DOKill();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Preset: 2nd Floor Bedroom Door (Crowbar Clue)")]
        public void ApplyPresetBedroomDoorCrowbarClue()
        {
            UnityEditor.Undo.RecordObject(this, "Apply Preset Bedroom Door");
            _lockedPrompt = "Stuck (Need Crowbar)";
            _unlockPrompt = "Pry Open Door";
            _enableLockedDialogue = true;
            _requireMainDoorLockedFirst = true;

            _lockedDialogueLines = new List<LockedDialogueLine>()
            {
                new LockedDialogueLine()
                {
                    text = "[Stranger Behind Door]: \"Ah, damn... that bedroom door is jammed shut! You'll need a crowbar to force it open!\"",
                    color = new Color(1f, 0.88f, 0.6f),
                    duration = 5.0f
                },
                new LockedDialogueLine()
                {
                    text = "[Stranger Behind Door]: \"I know there's a crowbar somewhere downstairs, but I can't remember which room... You'll have to search for it!\"",
                    color = new Color(1f, 0.88f, 0.6f),
                    duration = 5.5f
                },
                new LockedDialogueLine()
                {
                    text = "[Player]: \"A crowbar downstairs... got it. I need to search the rooms on the ground floor.\"",
                    color = new Color(0.95f, 0.95f, 0.9f),
                    duration = 4.0f
                }
            };
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"<color=green>[DoorInteractable]</color> Applied 2nd Floor Bedroom Door (Crowbar Clue) preset to '{gameObject.name}'!");
        }

        [ContextMenu("Apply Preset: Downstairs Door (Bedroom Key Clue)")]
        public void ApplyPresetDownstairsDoorKeyClue()
        {
            UnityEditor.Undo.RecordObject(this, "Apply Preset Downstairs Door");
            _lockedPrompt = "Locked (Need Key)";
            _unlockPrompt = "Unlock Door";
            _enableLockedDialogue = true;
            _requireMainDoorLockedFirst = true;

            _lockedDialogueLines = new List<LockedDialogueLine>()
            {
                new LockedDialogueLine()
                {
                    text = "[Stranger Behind Door]: \"Hey! That door is locked! You'll need the key from our bedroom on the 2nd floor!\"",
                    color = new Color(1f, 0.88f, 0.6f),
                    duration = 4.8f
                },
                new LockedDialogueLine()
                {
                    text = "[Stranger Behind Door]: \"Be careful... my crazy wife is guarding that key! And that bedroom door gets stuck from time to time—you might have to force it open!\"",
                    color = new Color(1f, 0.88f, 0.6f),
                    duration = 5.5f
                },
                new LockedDialogueLine()
                {
                    text = "[Player]: \"2nd floor bedroom... got it. I need to watch out for her.\"",
                    color = new Color(0.95f, 0.95f, 0.9f),
                    duration = 3.5f
                }
            };
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"<color=green>[DoorInteractable]</color> Applied Downstairs Door (Bedroom Key Clue) preset to '{gameObject.name}'!");
        }

        [ContextMenu("Set Current Rotation As Open")]
        private void SetCurrentRotationAsOpen()
        {
            _openRotation = transform.localEulerAngles;
        }

        [ContextMenu("Set Current Rotation As Closed")]
        private void SetCurrentRotationAsClosed()
        {
            _closedRotation = transform.localEulerAngles;
        }
#endif
    }
}
