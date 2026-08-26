namespace V0.Interaction
{
    /// <summary>
    /// Interface for any in-game object that the player can interact with.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Prompt or description to display in UI when looking at this object (e.g. "Open Door").
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Called when the player interacts with this object (e.g. pressing E).
        /// </summary>
        void Interact();
    }
}
