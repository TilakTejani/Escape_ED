using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeED.InputHandling
{
    /// <summary>
    /// Hardware Abstraction Layer.
    /// Polling Unity's Input System to capture and forward all touches (and mouse) independently.
    /// NEVER handles game logic or knows about the scene.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        public event Action<TouchData> OnTouch;

        private bool    wasMousePressedLastFrame = false;
        private Vector2 lastMousePosition;

        void Update()
        {
            ProcessTouchscreen();
            ProcessMouse();
        }

        private void ProcessTouchscreen()
        {
            if (Touchscreen.current == null) return;

            foreach (var touch in Touchscreen.current.touches)
            {
                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;

                TouchData data = new TouchData
                {
                    fingerId = touch.touchId.ReadValue(),
                    position = touch.position.ReadValue(),
                    phase = phase,
                    time = Time.unscaledTimeAsDouble
                };

                OnTouch?.Invoke(data);
            }
        }

        private void ProcessMouse()
        {
            if (Mouse.current == null) return;

            bool isPressed = Mouse.current.leftButton.isPressed;
            Vector2 pos = Mouse.current.position.ReadValue();

            if (isPressed && !wasMousePressedLastFrame)
            {
                OnTouch?.Invoke(new TouchData { fingerId = -1, position = pos, phase = UnityEngine.InputSystem.TouchPhase.Began, time = Time.unscaledTimeAsDouble });
            }
            else if (isPressed && wasMousePressedLastFrame)
            {
                // Only emit Moved when position actually changed to avoid per-frame spam
                if (pos != lastMousePosition)
                    OnTouch?.Invoke(new TouchData { fingerId = -1, position = pos, phase = UnityEngine.InputSystem.TouchPhase.Moved, time = Time.unscaledTimeAsDouble });
            }
            else if (!isPressed && wasMousePressedLastFrame)
            {
                OnTouch?.Invoke(new TouchData { fingerId = -1, position = pos, phase = UnityEngine.InputSystem.TouchPhase.Ended, time = Time.unscaledTimeAsDouble });
            }

            wasMousePressedLastFrame = isPressed;
            lastMousePosition = pos;
        }
    }
}
