using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EscapeED
{
    /// <summary>
    /// Professional 3D Rotator and Zoomer using the New Input System.
    /// Handles Rotation (Delta-based) and Zoom (Scroll/Pinch).
    /// </summary>
    public class CubeRotator : MonoBehaviour
    {
        [Header("Input Actions")]
        public InputActionReference rotationAction;
        public InputActionReference pressAction;
        public InputActionReference zoomAction;

        [Header("Rotation Settings")]
        public float rotationSpeed    = 15f;
        public float rotationSmooth   = 10f;
        public float rotationFriction = 0.95f;

        [Header("Zoom Settings")]
        public float zoomSpeed        = 0.5f;
        public float zoomSmooth       = 8f;
        public float minDistance      = 2f;
        public float maxDistance      = 15f;

        private Vector2 rotationVelocity;
        private bool    isDragging = false;
        
        private float   targetDistance;
        private float   currentDistance;
        private Transform mainCamTransform;

        // For Pinch-to-Zoom logic
        private float lastPinchDistance;
        private bool  wasPinchingLastFrame;

        private void OnEnable()
        {
            if (pressAction != null)
            {
                pressAction.action.started  += OnPressStarted;
                pressAction.action.canceled += OnPressEnded;
                pressAction.action.Enable();
            }
            if (rotationAction != null) rotationAction.action.Enable();
            if (zoomAction != null)     zoomAction.action.Enable();

            mainCamTransform = Camera.main.transform;
            currentDistance  = Vector3.Distance(mainCamTransform.position, transform.position);
            targetDistance   = currentDistance;
        }

        private void OnDisable()
        {
            if (pressAction != null)
            {
                pressAction.action.started  -= OnPressStarted;
                pressAction.action.canceled -= OnPressEnded;
            }
        }

        private void OnPressStarted(InputAction.CallbackContext context) => isDragging = true;
        private void OnPressEnded(InputAction.CallbackContext context)   => isDragging = false;

        void Update()
        {
            HandleRotationInput();
            HandleZoomInput();
            
            ApplyTransformations();
        }

        private void HandleRotationInput()
        {
            if (isDragging && rotationAction != null)
            {
                Vector2 delta = rotationAction.action.ReadValue<Vector2>();
                rotationVelocity = Vector2.Lerp(rotationVelocity, delta * (rotationSpeed * 0.05f), Time.deltaTime * rotationSmooth);
            }
            else
            {
                rotationVelocity *= rotationFriction;
            }
        }

        private void HandleZoomInput()
        {
            // 1. Desktop Scroll Zoom
            if (zoomAction != null)
            {
                float scroll = zoomAction.action.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetDistance -= scroll * zoomSpeed * 0.01f;
                }
            }

            // 2. Mobile Pinch Zoom
            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                TouchControl f0 = null;
                TouchControl f1 = null;
                int activeCount = 0;

                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.isPressed)
                    {
                        if (activeCount == 0) f0 = touches[i];
                        else if (activeCount == 1) f1 = touches[i];
                        activeCount++;
                    }
                }

                if (activeCount == 2)
                {
                    float currentPinchDistance = Vector2.Distance(f0.position.ReadValue(), f1.position.ReadValue());

                    if (wasPinchingLastFrame)
                    {
                        float deltaPinch = currentPinchDistance - lastPinchDistance;
                        // Increased sensitivity for a snappier feel
                        float sensitivity = 0.015f * targetDistance; 
                        targetDistance -= deltaPinch * sensitivity;
                    }

                    lastPinchDistance = currentPinchDistance;
                    wasPinchingLastFrame = true;
                }
                else
                {
                    wasPinchingLastFrame = false;
                }
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        private void ApplyTransformations()
        {
            // Apply Rotation
            if (rotationVelocity.sqrMagnitude > 0.00001f)
            {
                transform.Rotate(Vector3.up,    -rotationVelocity.x, Space.World);
                transform.Rotate(Vector3.right,  rotationVelocity.y, Space.World);
            }

            // Apply Zoom (Camera Distance)
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmooth);
            
            // Move camera along its local Z relative to this object
            Vector3 direction = (mainCamTransform.position - transform.position).normalized;
            mainCamTransform.position = transform.position + direction * currentDistance;
        }

        /// <summary>
        /// Sets the ideal focal distance and calculates relative min/max zoom limits.
        /// Ensure the cube is always perfectly framed regardless of size.
        /// </summary>
        public void SetZoomLimits(float idealDistance)
        {
            targetDistance  = idealDistance;
            currentDistance = idealDistance;
            
            // Dynamic limits: 40% of ideal for close-up, 150% for maximum overview
            minDistance = idealDistance * 0.4f;
            maxDistance = idealDistance * 1.5f;

            Debug.Log($"[CubeRotator] Dynamic Zoom Limits Set: {minDistance:F2} to {maxDistance:F2} (Ideal={idealDistance:F2})");
        }

        [ContextMenu("Reset View")]
        public void ResetView()
        {
            transform.rotation = Quaternion.identity;
            rotationVelocity   = Vector2.zero;
            targetDistance     = 10f; // Default
        }
    }
}
