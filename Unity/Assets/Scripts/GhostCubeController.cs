using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Manages the orientation-based transparency of the cube faces.
    /// Fades out faces that are pointing directly at the camera.
    /// </summary>
    [ExecuteAlways]
    public class GhostCubeController : MonoBehaviour
    {
        [Header("Face Settings")]
        [Range(0f, 1f)] public float minAlpha = 0.05f;
        [Range(0f, 1f)] public float maxAlpha = 0.2f;
        [Range(1f, 5f)] public float fresnelPower = 2.0f;

        [Header("Arrow Settings")]
        [Range(0f, 1f)] public float minArrowAlpha = 0.08f;
        [Range(1f, 5f)] public float arrowFadePower = 1.5f;

        public bool debugLog = true;

        [Header("References (Auto-populated)")]
        [SerializeField] private List<MeshRenderer> faceRenderers = new List<MeshRenderer>();
        [SerializeField] private List<Vector3> faceNormals = new List<Vector3>();

        private List<MeshRenderer> arrowRenderers = new List<MeshRenderer>();
        private List<Color>        faceBaseColors = new List<Color>();

        private MaterialPropertyBlock propBlock;

        // Cube face normals in local space — order must match FaceIndexFromNormal in Arrow.cs
        // 0=up  1=down  2=left  3=right  4=forward  5=back
        private static readonly Vector3[] CubeFaceNormals =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };

        // Property IDs
        private static readonly int BaseColorId    = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId        = Shader.PropertyToID("_Color");
        private static readonly int FaceAlphas0Id  = Shader.PropertyToID("_FaceAlphas0");
        private static readonly int FaceAlphas1Id  = Shader.PropertyToID("_FaceAlphas1");

        public void RegisterArrow(MeshRenderer renderer)
        {
            if (renderer != null)
                arrowRenderers.Add(renderer);
        }

        public void ClearArrows()
        {
            arrowRenderers.Clear();
        }

        private Color GetRendererBaseColor(MeshRenderer r)
        {
            if (r.sharedMaterial.HasProperty(BaseColorId))
                return r.sharedMaterial.GetColor(BaseColorId);
            if (r.sharedMaterial.HasProperty(ColorId))
                return r.sharedMaterial.GetColor(ColorId);
            return Color.white;
        }

        private void CacheFaceColors()
        {
            faceBaseColors.Clear();
            foreach (var r in faceRenderers)
                faceBaseColors.Add(r != null ? GetRendererBaseColor(r) : Color.white);
        }

        public void Initialize(List<GameObject> faces, List<Vector3> normals)
        {
            faceRenderers.Clear();
            faceNormals.Clear();
            faceBaseColors.Clear();
            propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < faces.Count; i++)
            {
                var renderer = faces[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    faceRenderers.Add(renderer);
                    faceNormals.Add(normals[i]);
                    faceBaseColors.Add(GetRendererBaseColor(renderer));
                }
            }
            
            Debug.Log($"[GhostCube] Initialized with {faceRenderers.Count} faces.");
            debugLog = true; // Force it on to ensure we see the trace
            
            // Mark as dirty to ensure Unity saves this in the Editor
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable()
        {
            Debug.Log("[GhostCube] Component Enabled and Active.");
            FindCulprit();
            
            // PROACTIVE HIERARCHY CLEANUP:
            // If this script is attached to a standard Cube object, 
            // the core cube will block our transparent face effects.
            var mr = GetComponent<MeshRenderer>();
            var mf = GetComponent<MeshFilter>();
            if (mr != null || mf != null)
            {
                Debug.LogWarning("[GhostCubeController] Found Mesh Renderer/Filter on parent! Disabling it to allow Ghost transparency.");
                if (mr != null) mr.enabled = false;
            }
        }

        [ContextMenu("Find Solid Culprit")]
        public void FindCulprit()
        {
            Debug.Log("[GhostCube] Searching for solid renderers near the grid...");

            // Use the modern API that doesn't use the obsolete FindObjectsSortMode
            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mr in allRenderers)
            {
                // Skip our own faces
                if (faceRenderers.Contains(mr)) continue;
                
                // If it's close to our center and has a mesh, it's a culprit
                float dist = Vector3.Distance(mr.transform.position, transform.position);
                if (dist < 2.0f) 
                {
                    Debug.LogWarning($"[GhostCube] FOUND CULPRIT: '{mr.gameObject.name}' at path '{GetGameObjectPath(mr.gameObject)}'. Rendering is solid? {!mr.sharedMaterial.name.Contains("Ghost")}");
                    // mr.enabled = false; // We can't safely disable everything, but we can log it.
                }
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        [ContextMenu("Force Transparency Update")]
        public void ForceUpdate()
        {
            debugLog = true;
            Debug.Log("[GhostCube] Manual Update Triggered.");
            LateUpdate();
        }

        void LateUpdate()
        {
            if (faceRenderers.Count == 0)
            {
                if (debugLog) Debug.LogWarning("[GhostCube] LateUpdate: No face renderers found!");
                return;
            }

            Camera cam = null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (UnityEditor.SceneView.lastActiveSceneView != null)
                {
                    cam = UnityEditor.SceneView.lastActiveSceneView.camera;
                }
            }
#endif
            if (cam == null) cam = Camera.main;
            
            if (cam == null) 
            {
                 if (debugLog && Time.frameCount % 60 == 0) Debug.LogWarning("[GhostCube] LateUpdate: No active camera found (SceneView or Main).");
                 return;
            }

            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            // Rebuild color cache if stale (e.g. renderers added via Inspector in editor)
            if (faceBaseColors.Count != faceRenderers.Count) CacheFaceColors();

            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < faceRenderers.Count; i++)
            {
                if (faceRenderers[i] == null) continue;

                // Accurate world normal based on cube rotation
                Vector3 worldNormal = transform.TransformDirection(faceNormals[i]);
                
                // View direction from camera center to face center
                Vector3 viewDir = (faceRenderers[i].bounds.center - camPos).normalized;
                
                // Focus factor (1.0 = looking straight at it, 0.0 = grazing)
                float dot = Mathf.Abs(Vector3.Dot(worldNormal, viewDir));

                // Alpha fades to minAlpha when focus is high (dot -> 1)
                float alpha = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(dot, fresnelPower));

                faceRenderers[i].GetPropertyBlock(propBlock);

                Color matColor = faceBaseColors[i];

                matColor.a = alpha;
                
                // Set both for robustness
                propBlock.SetColor(BaseColorId, matColor);
                propBlock.SetColor(ColorId, matColor);
                
                faceRenderers[i].SetPropertyBlock(propBlock);

                if (debugLog && i == 0)
                {
                    // Debug.Log($"[GhostCube] Face 0 Alpha: {alpha:F3} (Dot: {dot:F3}) Camera: {cam.name}");
                }
            }

            // Compute per-face alpha for all 6 cube faces (shared across all arrows this frame).
            // Any face with a positive dot toward camera = fully opaque.
            // Back-facing (dot <= 0) = minArrowAlpha, with a small smooth zone to avoid a hard pop.
            Vector3 cubeToCam = (camPos - transform.position).normalized;
            float[] faceAlphas = new float[6];
            for (int f = 0; f < 6; f++)
            {
                Vector3 worldNormal  = transform.TransformDirection(CubeFaceNormals[f]);
                float   dot          = Vector3.Dot(worldNormal, cubeToCam);
                // Smooth 0→1 over a small transition band around dot=0, then clamp to full opacity.
                float   t            = Mathf.Clamp01(dot / 0.2f + 1f); // 0 at dot=-0.2, 1 at dot=0
                faceAlphas[f] = Mathf.Lerp(minArrowAlpha, 1f, t);
            }

            // Pack into two Vector4s: (up,down,left,right) and (forward,back,_,_)
            Vector4 alphas0 = new Vector4(faceAlphas[0], faceAlphas[1], faceAlphas[2], faceAlphas[3]);
            Vector4 alphas1 = new Vector4(faceAlphas[4], faceAlphas[5], 0f, 0f);

            for (int i = 0; i < arrowRenderers.Count; i++)
            {
                if (arrowRenderers[i] == null) continue;
                arrowRenderers[i].GetPropertyBlock(propBlock);
                propBlock.SetVector(FaceAlphas0Id, alphas0);
                propBlock.SetVector(FaceAlphas1Id, alphas1);
                arrowRenderers[i].SetPropertyBlock(propBlock);
            }
        }
    }
}
