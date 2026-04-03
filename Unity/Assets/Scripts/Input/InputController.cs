using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeED.InputHandling
{
    /// <summary>
    /// Gesture Detection Layer.
    /// Listens to raw normalized touches and converts them into intent (Tap, Swipe, etc).
    /// Tracks concurrent touches in a dictionary for pure multi-touch support without 'primaryTouch' bugs.
    /// </summary>
    [RequireComponent(typeof(InputReader))]
    public class InputController : MonoBehaviour
    {
        [Header("Tap Settings")]
        public float maxTapDuration = 0.3f; // seconds
        public float maxTapDistance = 20f;  // screen pixels

        [Header("Dependencies")]
        [Tooltip("If null, will auto-fetch from this GameObject")]
        public InputReader inputReader;

        // Intent Events emitted to the InteractionSystem
        public event Action<Vector2, int> OnTap;

        // Session tracking per finger
        private struct TouchState
        {
            public Vector2 startPosition;
            public double startTime;
        }

        private readonly Dictionary<int, TouchState> activeTouches = new Dictionary<int, TouchState>();

        private void Awake()
        {
            if (inputReader == null)
            {
                inputReader = GetComponent<InputReader>();
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
                inputReader.OnTouch += HandleTouch;
        }

        private void OnDisable()
        {
            if (inputReader != null)
                inputReader.OnTouch -= HandleTouch;
        }

        private void HandleTouch(TouchData data)
        {
            switch (data.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    activeTouches[data.fingerId] = new TouchState
                    {
                        startPosition = data.position,
                        startTime = data.time
                    };
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                    // Here we could emit OnDrag if needed in the future
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    if (activeTouches.TryGetValue(data.fingerId, out TouchState state))
                    {
                        // Safely evaluate if the completed session constitutes a Tap
                        double duration = data.time - state.startTime;
                        float distance = Vector2.Distance(state.startPosition, data.position);

                        if (duration <= maxTapDuration && distance <= maxTapDistance)
                        {
                            OnTap?.Invoke(data.position, data.fingerId);
                        }
                        
                        activeTouches.Remove(data.fingerId);
                    }
                    break;
            }
        }
    }
}
