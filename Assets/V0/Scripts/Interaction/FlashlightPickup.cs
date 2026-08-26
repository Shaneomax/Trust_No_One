using UnityEngine;
using DG.Tweening;

namespace V0.Interaction
{
    /// <summary>
    /// Put this on any world flashlight that the player can pick up by pressing E.
    /// Uses DOTween to smoothly move towards the player position on pickup.
    /// </summary>
    public class FlashlightPickup : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string _interactionPrompt = "Take Flashlight";

        [Header("Pickup Animation")]
        [Tooltip("Duration to move towards the player")]
        [SerializeField] private float _pickupDuration = 0.3f;

        [Tooltip("Easing curve for moving towards the player")]
        [SerializeField] private Ease _moveEase = Ease.InQuad;

        [Tooltip("Height offset added to player position (e.g. chest height)")]
        [SerializeField] private float _playerHeightOffset = 1.0f;

        private bool _isBeingPickedUp = false;

        public string InteractionPrompt => _interactionPrompt;

        public void Interact()
        {
            if (_isBeingPickedUp) return;
            _isBeingPickedUp = true;

            // Disable collider immediately so it can't be interacted with again
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            // Find player
            FlashlightController controller = Object.FindFirstObjectByType<FlashlightController>();
            Vector3 targetPosition = controller != null 
                ? controller.transform.position + Vector3.up * _playerHeightOffset 
                : transform.position;

            // Move cleanly towards the player position
            transform.DOMove(targetPosition, _pickupDuration)
                .SetEase(_moveEase)
                .OnComplete(() =>
                {
                    if (controller != null)
                    {
                        controller.PickupFlashlight();
                    }
                    Destroy(gameObject);
                });
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
