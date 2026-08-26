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

        [Header("Door State")]
        [Tooltip("Is the door currently open?")]
        [SerializeField] private bool _isOpen = false;

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

        public string InteractionPrompt => _isOpen ? _closePrompt : _openPrompt;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_doorTransform == null)
            {
                _doorTransform = transform;
            }
        }

        public void Interact()
        {
            _isOpen = !_isOpen;

            if (_doorTransform == null)
            {
                _doorTransform = transform;
            }

            // Stop any running tween on this transform to smoothly handle rapid interactions
            _doorTransform.DOKill();

            Vector3 targetRotation = _isOpen ? _openRotation : _closedRotation;
            Ease targetEase = _isOpen ? _openEase : _closeEase;

            _doorTransform.DOLocalRotate(targetRotation, _animationDuration)
                .SetEase(targetEase);

            if (_isOpen)
            {
                Debug.Log("PLayer opens the dooe");
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
