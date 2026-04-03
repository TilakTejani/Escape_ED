using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeED.InputHandling
{
    /// <summary>
    /// Normalized container for touches, preventing dependencies on Unity's raw hardware structs outside the Input layer.
    /// </summary>
    public struct TouchData
    {
        public int fingerId;
        public Vector2 position;
        public UnityEngine.InputSystem.TouchPhase phase;
        public double time;
    }
}
