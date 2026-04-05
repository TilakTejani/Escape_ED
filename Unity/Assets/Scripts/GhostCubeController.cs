using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Manages the orientation-based transparency of the cube faces and arrows.
    /// Face transparency uses per-renderer property blocks.
    /// Arrow transparency is handled entirely in the shader via vertex normals —
    /// no per-arrow registration needed.
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

        public bool debugLog = true;

        [Header("References (Auto-populated)")]
        [SerializeField] private List<MeshRenderer> faceRenderers = new List<MeshRenderer>();
        [SerializeField] private List<Vector3>      faceNormals   = new List<Vector3>();

        private List<Color>          faceBaseColors = new List<Color>();
        private MaterialPropertyBlock propBlock;

        // Property IDs
        private static readonly int BaseColorId      = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId          = Shader.PropertyToID("_Color");
        private static readonly int MinArrowAlphaId  = Shader.PropertyToID("_MinArrowAlpha");

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
            debugLog = true;

#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable()
        {
            Debug.Log("[GhostCube] Component Enabled and Active.");
            FindCulprit();

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
            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mr in allRenderers)
            {
                if (faceRenderers.Contains(mr)) continue;
                if (mr.transform.IsChildOf(transform)) continue;
                float dist = Vector3.Distance(mr.transform.position, transform.position);
                if (dist < 2.0f)
                    Debug.LogWarning($"[GhostCube] FOUND CULPRIT: '{mr.gameObject.name}' at path '{GetGameObjectPath(mr.gameObject)}'.");
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
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                if (debugLog && Time.frameCount % 60 == 0)
                    Debug.LogWarning("[GhostCube] LateUpdate: No active camera found.");
                return;
            }

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            if (faceBaseColors.Count != faceRenderers.Count) CacheFaceColors();

            Vector3 camPos = cam.transform.position;

            // Update cube face transparency
            for (int i = 0; i < faceRenderers.Count; i++)
            {
                if (faceRenderers[i] == null) continue;

                Vector3 worldNormal = transform.TransformDirection(faceNormals[i]);
                Vector3 viewDir     = (faceRenderers[i].bounds.center - camPos).normalized;
                float   dot         = Mathf.Abs(Vector3.Dot(worldNormal, viewDir));
                float   alpha       = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(dot, fresnelPower));

                faceRenderers[i].GetPropertyBlock(propBlock);
                Color matColor = faceBaseColors[i];
                matColor.a = alpha;
                propBlock.SetColor(BaseColorId, matColor);
                propBlock.SetColor(ColorId, matColor);
                faceRenderers[i].SetPropertyBlock(propBlock);
            }

            // Arrow alpha is computed entirely in the shader using vertex normals.
            // We only need to push the minimum alpha threshold as a global uniform.
            Shader.SetGlobalFloat(MinArrowAlphaId, minArrowAlpha);
        }

        private Color GetRendererBaseColor(MeshRenderer r)
        {
            if (r.sharedMaterial.HasProperty(BaseColorId)) return r.sharedMaterial.GetColor(BaseColorId);
            if (r.sharedMaterial.HasProperty(ColorId))     return r.sharedMaterial.GetColor(ColorId);
            return Color.white;
        }

        private void CacheFaceColors()
        {
            faceBaseColors.Clear();
            foreach (var r in faceRenderers)
                faceBaseColors.Add(r != null ? GetRendererBaseColor(r) : Color.white);
        }
    }
}
