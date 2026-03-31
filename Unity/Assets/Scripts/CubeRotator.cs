using UnityEngine;

namespace EscapeED
{
    public class CubeRotator : MonoBehaviour
    {
        [Header("Interaction Settings")]
        public float rotationSpeed = 150f;
        public float smoothDegree = 10f;
        public float friction = 0.95f;

        private Vector3 currentRotation;
        private Vector3 targetRotation;
        private Vector2 dragVelocity;
        private bool isDragging = false;
        private Vector2 lastMousePosition;

        void Update()
        {
            HandleInput();
            ApplyRotation();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
                dragVelocity = Vector2.zero;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector2 currentMousePosition = Input.mousePosition;
                Vector2 delta = currentMousePosition - lastMousePosition;
                
                // Horizontal mouse movement rotates around Y axis, vertical rotates around X
                dragVelocity = new Vector2(-delta.y, -delta.x) * (rotationSpeed / 100f);
                targetRotation += (Vector3)dragVelocity;
                
                lastMousePosition = currentMousePosition;
            }
            else
            {
                // Apply inertia/friction
                dragVelocity *= friction;
                targetRotation += (Vector3)dragVelocity;
            }
        }

        private void ApplyRotation()
        {
            currentRotation = Vector3.Lerp(currentRotation, targetRotation, Time.deltaTime * smoothDegree);
            transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
        }
        
        [ContextMenu("Reset Rotation")]
        public void ResetRotation()
        {
            targetRotation = Vector3.zero;
            currentRotation = Vector3.zero;
        }
    }
}
