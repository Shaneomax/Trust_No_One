using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace V0.Interaction
{
    /// <summary>
    /// Player interaction component. Raycasts forward from player camera
    /// and invokes IInteractable.Interact() when the interact input is triggered.
    /// </summary>
    [RequireComponent(typeof(StarterAssetsInputs))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Max reach distance for interaction")]
        [SerializeField] private float _interactDistance = 3.0f;

        [Tooltip("Layer mask for interactable objects")]
        [SerializeField] private LayerMask _interactableLayer;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private StarterAssetsInputs _input;

        private IInteractable _currentInteractable;

        public IInteractable CurrentInteractable => _currentInteractable;

        private void Update()
        {
            UpdateCurrentInteractable();
            HandleInteraction();
        }

        private void UpdateCurrentInteractable()
        {
            if (_playerCamera == null) return;

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableLayer, QueryTriggerInteraction.Ignore))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    interactable = hit.collider.GetComponent<IInteractable>();
                }
                _currentInteractable = interactable;
            }
            else
            {
                _currentInteractable = null;
            }
        }

        private void HandleInteraction()
        {
            if (_input == null) return;

            if (_input.interact)
            {
                _input.interact = false;

                if (_currentInteractable != null)
                {
                    _currentInteractable.Interact();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = _playerCamera != null ? _playerCamera : Camera.main;
            if (cam != null)
            {
                Gizmos.color = _currentInteractable != null ? Color.green : Color.red;
                Gizmos.DrawRay(cam.transform.position, cam.transform.forward * _interactDistance);
            }
        }
    }
}
