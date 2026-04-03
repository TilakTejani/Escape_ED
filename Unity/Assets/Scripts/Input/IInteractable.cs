namespace EscapeED.InputHandling
{
    /// <summary>
    /// Interface for any world object that can receive a tap from the InteractionSystem.
    /// Completely decouples the InteractionSystem from knowing about specific game classes like 'Arrow'.
    /// </summary>
    public interface IInteractable
    {
        void OnInteract();
    }
}
