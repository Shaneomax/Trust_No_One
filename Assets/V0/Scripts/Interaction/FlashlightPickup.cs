using UnityEngine;

namespace V0.Interaction
{
    /// <summary>
    /// Put this on any world flashlight that the player can pick up by pressing E.
    /// Implements IInteractable.
    /// </summary>
    public class FlashlightPickup : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string _interactionPrompt = "Take Flashlight";

        public string InteractionPrompt => _interactionPrompt;

        public void Interact()
        {
            // Find the FlashlightController on the player
            FlashlightController controller = Object.FindFirstObjectByType<FlashlightController>();
            if (controller != null)
            {
                controller.PickupFlashlight();
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("FlashlightController not found on Player!");
            }
        }
    }
}
