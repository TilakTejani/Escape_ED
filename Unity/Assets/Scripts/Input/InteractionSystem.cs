using UnityEngine;
using UnityEngine.EventSystems;

namespace EscapeED.InputHandling
{
    /// <summary>
    /// Bridge between 2D screen taps and 3D world objects.
    /// Guarded by UI pointer checks to prevent UI tap punch-through.
    /// </summary>
    [RequireComponent(typeof(InputController))]
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("The physics layer containing your interactable objects (e.g., Arrows)")]
        public LayerMask interactableLayer;
        public float raycastDistance = 100f;

        private InputController inputController;
        private Camera mainCamera;

        private void Awake()
        {
            inputController = GetComponent<InputController>();
            mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (inputController != null)
                inputController.OnTap += HandleTap;
        }

        private void OnDisable()
        {
            if (inputController != null)
                inputController.OnTap -= HandleTap;
        }

        private void HandleTap(Vector2 screenPosition, int fingerId)
        {
            // -------------------------------------------------------------
            // UI PROTECTION LAYER
            // -------------------------------------------------------------
            if (IsPointerOverUI(fingerId))
            {
                // UI absorbed the tap. Stop propagation immediately.
                return; 
            }

            // -------------------------------------------------------------
            // RAYCASTING LAYER
            // -------------------------------------------------------------
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactableLayer))
            {
                // Reject hits on back-facing arrow segments.
                // hit.normal is unreliable for BoxColliders — the ray hits the camera-facing underside
                // of the box even when the arrow's face points away from camera.
                // ArrowSegmentFace stores the true arrow face normal (local space) on each segment child.
                // For the tip MeshCollider (no ArrowSegmentFace), hit.normal IS the mesh triangle normal
                // which correctly reflects the tip face orientation.
                var segFace = hit.collider.GetComponent<ArrowSegmentFace>();
                Vector3 faceNormalWS = segFace != null
                    ? hit.collider.transform.parent.TransformDirection(segFace.localFaceNormal)
                    : hit.normal;

                if (Vector3.Dot(faceNormalWS, -ray.direction) <= 0f) return;

                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                    interactable.OnInteract();
            }
        }

        private bool IsPointerOverUI(int fingerId)
        {
            if (EventSystem.current == null) return false;

            // Normalize hardware identifiers:
            // InputReader sets mouse fingerId to -1
            if (fingerId == -1)
            {
                return EventSystem.current.IsPointerOverGameObject();
            }
            else
            {
                // Is evaluating a specific touchscreen finger ID
                return EventSystem.current.IsPointerOverGameObject(fingerId);
            }
        }
    }
}
