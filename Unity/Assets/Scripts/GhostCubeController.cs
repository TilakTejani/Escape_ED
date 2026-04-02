using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    /// <summary>
    /// Manages the orientation-based transparency of the cube faces.
    /// Fades out faces that are pointing directly at the camera.
    /// </summary>
    public class GhostCubeController : MonoBehaviour
    {
        [Header("Settings")]
        [Range(0f, 1f)] public float minAlpha = 0.05f;
        [Range(0f, 1f)] public float maxAlpha = 0.4f;
        [Range(1f, 5f)] public float fresnelPower = 2.0f;

        private List<MeshRenderer> faceRenderers = new List<MeshRenderer>();
        private List<Vector3> faceNormals = new List<Vector3>();
        private MaterialPropertyBlock propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Initialize(List<GameObject> faces, List<Vector3> normals)
        {
            faceRenderers.Clear();
            faceNormals.Clear();
            propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < faces.Count; i++)
            {
                var renderer = faces[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    faceRenderers.Add(renderer);
                    faceNormals.Add(normals[i]);
                }
            }
        }

        void LateUpdate()
        {
            if (faceRenderers.Count == 0) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 camForward = cam.transform.forward;

            for (int i = 0; i < faceRenderers.Count; i++)
            {
                // Calculate dot product between face normal and view direction
                // A dot of 1 means it's facing away/towards perfectly.
                // We use transform.TransformDirection to handle cube rotation.
                Vector3 worldNormal = transform.TransformDirection(faceNormals[i]);
                float dot = Mathf.Abs(Vector3.Dot(worldNormal, camForward));

                // Fresnel effect: higher alpha at grazing angles (edges), lower in center
                // But specifically, we want the FACE pointing at us to be MOST transparent.
                float alpha = Mathf.Lerp(maxAlpha, minAlpha, Mathf.Pow(dot, fresnelPower));

                faceRenderers[i].GetPropertyBlock(propBlock);
                Color col = faceRenderers[i].sharedMaterial.GetColor(BaseColorId);
                col.a = alpha;
                propBlock.SetColor(BaseColorId, col);
                faceRenderers[i].SetPropertyBlock(propBlock);
            }
        }
    }
}
