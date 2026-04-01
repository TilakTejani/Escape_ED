using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    public enum DotType { Face, Edge, Corner }

    [ExecuteAlways]
    public class CubeGrid : MonoBehaviour
    {
        public Vector3Int size    = new Vector3Int(5, 5, 5);
        public float      spacing = 0.28f;

        [Header("Visuals")]
        public Material whiteMaterial;
        public Material dotMaterial;    // Material for the surface-embedded dot circles
        public float    dotRadius    = 0.04f;
        public int      dotSegments  = 16;

        private Dictionary<Vector3Int, GameObject> dots         = new Dictionary<Vector3Int, GameObject>();
        private List<GameObject>                   indexedDots  = new List<GameObject>();
        private List<Vector3Int>                   indexedCoords = new List<Vector3Int>(); // parallel to indexedDots
        private GameObject                         backgroundCube;

        void OnEnable()
        {
            if (Application.isPlaying) GenerateGrid();
            else RefreshBackground();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // This forces the editor to refresh when you change values in the Inspector
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () => {
                    if (this != null) {
                        RefreshBackground();
                        // We don't auto-generate dots on every slider move to avoid lag, 
                        // but we ensure the background cube matches the spacing immediately.
                    }
                };
            }
#endif
        }

        private void Reset()
        {
            // Called when you click "Reset" in the Inspector context menu
            size = new Vector3Int(5, 5, 5);
            spacing = 0.28f;
            dotRadius = 0.04f;
            GenerateGrid();
        }

        [ContextMenu("Force Regenerate Grid")]
        public void ManualRegenerate()
        {
            GenerateGrid();
        }

        void OnDestroy()
        {
            if (!Application.isPlaying && backgroundCube != null)
                DestroyImmediate(backgroundCube);
        }

        private void RefreshBackground()
        {
            if (backgroundCube == null)
            {
                Transform existing = transform.Find("VisualCube");
                if (existing != null) backgroundCube = existing.gameObject;
            }
            CreateBackgroundCube();
        }

        public void GenerateGrid()
        {
            foreach (var dot in dots.Values) if (dot != null) Destroy(dot);
            dots.Clear();
            indexedDots.Clear();
            indexedCoords.Clear();

            // Deterministic order: Z -> Y -> X (must match Level Maker's cube.ts)
            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        if (!IsSurface(x, y, z)) continue;

                        Vector3Int gridPos  = new Vector3Int(x, y, z);
                        Vector3    worldPos = CalculateWorldPos(x, y, z);
                        List<Vector3> faceNormals = GetAllFaceNormals(gridPos);

                        GameObject dotObj = CreateDotObject(x, y, z, worldPos, faceNormals);
                        dots[gridPos]  = dotObj;
                        indexedDots.Add(dotObj);
                        indexedCoords.Add(gridPos);
                    }
                }
            }
            Debug.Log($"Generated {indexedDots.Count} surface dots.");
        }

        /// <summary>
        /// Creates a circle mesh that "folds" over edges and corners.
        /// </summary>
        private GameObject CreateDotObject(int x, int y, int z, Vector3 worldPos, List<Vector3> normals)
        {
            GameObject obj = new GameObject($"Dot_{x}_{y}_{z}");
            obj.transform.SetParent(transform);

            obj.transform.position = worldPos;

            MeshFilter   mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> meshNormals = new List<Vector3>();

            // Use configured dotSegments for smoothness
            AddFoldCircle(Vector3.zero, normals, dotRadius, dotSegments, 0.002f, verts, tris, uvs, meshNormals);

            mf.mesh    = new Mesh { name = "FoldedDot", vertices = verts.ToArray(), triangles = tris.ToArray(), uv = uvs.ToArray(), normals = meshNormals.ToArray() };
            mf.mesh.RecalculateBounds();
            mr.material = dotMaterial != null ? dotMaterial : whiteMaterial;

            return obj;
        }

        /// <summary>
        /// Per-face arc generator with exact seam boundaries.
        /// Builds a per-face 2D basis so arcs lie correctly in each face's plane —
        /// no projection step, so no degenerate collapse on non-primary faces.
        /// Seam endpoints are pinned to exact Cross(faceN, otherN) directions
        /// so adjacent face arcs share identical 3D boundary vertices.
        /// </summary>
        static void AddFoldCircle(
            Vector3 center, List<Vector3> faces, float radius, int segments, float lift,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            if (faces == null || faces.Count == 0) return;

            foreach (var faceN in faces)
            {
                // Per-face 2D basis lying IN this face's plane
                Vector3 basisU = Vector3.Cross(faceN, Vector3.up);
                if (basisU.sqrMagnitude < 0.001f) basisU = Vector3.Cross(faceN, Vector3.forward);
                basisU.Normalize();
                Vector3 basisV = Vector3.Cross(basisU, faceN).normalized;

                // Uniform samples + exact seam boundary angles so arc endpoints
                // match in 3D space regardless of segment count
                var angleList = new List<float>(segments + 4);
                for (int s = 0; s <= segments; s++)
                    angleList.Add(s * Mathf.PI * 2f / segments);

                foreach (var otherN in faces)
                {
                    if (otherN == faceN) continue;
                    Vector3 seam = Vector3.Cross(faceN, otherN);
                    if (seam.sqrMagnitude < 0.001f) continue;
                    seam.Normalize();
                    InsertAngle(angleList, Atan2Basis( seam, basisU, basisV));
                    InsertAngle(angleList, Atan2Basis(-seam, basisU, basisV));
                }
                angleList.Sort();

                int centerIdx = verts.Count;
                verts.Add(center + faceN * lift);
                uvs.Add(new Vector2(0.5f, 0.5f));
                normals.Add(faceN);

                int arcStart = verts.Count;
                int arcCount = 0;

                foreach (float ang in angleList)
                {
                    // dir already lies in faceN's plane — no projection needed
                    Vector3 dir = basisU * Mathf.Cos(ang) + basisV * Mathf.Sin(ang);

                    bool belongs = true;
                    foreach (var otherN in faces)
                    {
                        if (otherN == faceN) continue;
                        if (Vector3.Dot(dir, otherN) > 0.02f) { belongs = false; break; }
                    }
                    if (!belongs) continue;

                    verts.Add(center + dir * radius + faceN * lift);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    normals.Add(faceN);
                    arcCount++;
                }

                for (int i = 0; i < arcCount - 1; i++)
                {
                    tris.Add(centerIdx); tris.Add(arcStart + i);     tris.Add(arcStart + i + 1);
                    tris.Add(centerIdx); tris.Add(arcStart + i + 1); tris.Add(arcStart + i);
                }
            }
        }

        static float Atan2Basis(Vector3 dir, Vector3 u, Vector3 v)
        {
            float a = Mathf.Atan2(Vector3.Dot(dir, v), Vector3.Dot(dir, u));
            return a < 0f ? a + Mathf.PI * 2f : a;
        }

        static void InsertAngle(List<float> list, float a, float eps = 0.001f)
        {
            foreach (var x in list) if (Mathf.Abs(x - a) < eps) return;
            list.Add(a);
        }

        public GameObject GetDotByIndex(int index)
        {
            if (index >= 0 && index < indexedDots.Count) return indexedDots[index];
            return null;
        }

        /// <summary>
        /// Returns ALL face normals for a dot — 1 for face interior, 2 for edge, 3 for corner.
        /// Used by Arrow to detect edge-spanning segments that need the fold treatment.
        /// </summary>
        public List<Vector3> GetAllFaceNormals(int dotIndex)
        {
            if (dotIndex >= 0 && dotIndex < indexedCoords.Count)
                return GetAllFaceNormals(indexedCoords[dotIndex]);
            return new List<Vector3> { Vector3.up };
        }

        public List<Vector3> GetAllFaceNormals(Vector3Int p)
        {
            var result = new List<Vector3>(3);
            if (p.x == 0)          result.Add(Vector3.left);
            if (p.x == size.x - 1) result.Add(Vector3.right);
            if (p.y == 0)          result.Add(Vector3.down);
            if (p.y == size.y - 1) result.Add(Vector3.up);
            if (p.z == 0)          result.Add(Vector3.back);
            if (p.z == size.z - 1) result.Add(Vector3.forward);
            return result;
        }

        public DotType GetDotType(int index)
        {
            if (index >= 0 && index < indexedCoords.Count)
                return GetDotType(indexedCoords[index]);
            return DotType.Face;
        }

        public DotType GetDotType(Vector3Int p)
        {
            int faceCount = 0;
            if (p.x == 0 || p.x == size.x - 1) faceCount++;
            if (p.y == 0 || p.y == size.y - 1) faceCount++;
            if (p.z == 0 || p.z == size.z - 1) faceCount++;
            if (faceCount >= 3) return DotType.Corner;
            if (faceCount == 2) return DotType.Edge;
            return DotType.Face;
        }

        private void CreateBackgroundCube()
        {
            if (backgroundCube == null)
            {
                Transform existing = transform.Find("VisualCube");
                if (existing != null) backgroundCube = existing.gameObject;
                else
                {
                    backgroundCube      = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    backgroundCube.name = "VisualCube";
                    backgroundCube.transform.SetParent(transform);
                }
            }

            backgroundCube.transform.localPosition = Vector3.zero;
            backgroundCube.transform.localScale     = (Vector3)(size - Vector3Int.one) * spacing;

            if (whiteMaterial != null)
            {
                Renderer r = backgroundCube.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = whiteMaterial;
            }
        }

        public GameObject GetDotAt(Vector3Int gridPos)
        {
            if (dots.ContainsKey(gridPos)) return dots[gridPos];
            return null;
        }

        public Vector3 GetSurfaceNormal(Vector3Int p)
        {
            if (p.x == 0)          return Vector3.left;
            if (p.x == size.x - 1) return Vector3.right;
            if (p.y == 0)          return Vector3.down;
            if (p.y == size.y - 1) return Vector3.up;
            if (p.z == 0)          return Vector3.back;
            if (p.z == size.z - 1) return Vector3.forward;
            return Vector3.up;
        }

        public bool IsSurface(int x, int y, int z)
        {
            return x == 0 || x == size.x - 1 ||
                   y == 0 || y == size.y - 1 ||
                   z == 0 || z == size.z - 1;
        }

        public Vector3 CalculateWorldPos(int x, int y, int z)
        {
            return new Vector3(
                x - (size.x - 1) / 2f,
                y - (size.y - 1) / 2f,
                z - (size.z - 1) / 2f
            ) * spacing;
        }
    }
}
