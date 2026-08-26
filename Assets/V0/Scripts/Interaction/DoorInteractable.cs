using UnityEngine;

namespace V0.Interaction
{
    /// <summary>
    /// Interactable door component implementing IInteractable.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string _interactionPrompt = "Open Door";

        [Header("Door State")]
        [SerializeField] private bool _isOpen = false;

        public string InteractionPrompt => _isOpen ? "Close Door" : _interactionPrompt;

        public bool IsOpen => _isOpen;

        public void Interact()
        {
            _isOpen = !_isOpen;
            Debug.Log("PLayer opens the dooe");
        }
    }
}
