using UnityEngine;
using DG.Tweening;

namespace V0.Interaction
{
    /// <summary>
    /// Interactable door component using DOTween for smooth opening and closing animations.
    /// Implements IInteractable.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
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

        private void OnDestroy()
        {
            if (_doorTransform != null)
            {
                _doorTransform.DOKill();
            }
        }

#if UNITY_EDITOR
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
