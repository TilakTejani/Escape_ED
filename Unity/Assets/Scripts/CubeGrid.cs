using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
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
            
            // Average normal for base lifting (z-fighting avoidance)
            Vector3 avgNormal = Vector3.zero;
            foreach (var n in normals) avgNormal += n;
            avgNormal.Normalize();

            // Lift slightly (0.01 spacing for mobile safety)
            obj.transform.position = worldPos;
            obj.transform.rotation = Quaternion.identity; // We build in world space coords relative to worldPos

            MeshFilter   mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> meshNormals = new List<Vector3>();

            // 24 segments for extra smoothness on mobile
            AddFoldCircle(Vector3.zero, normals, dotRadius, 24, 0.002f, verts, tris, uvs, meshNormals);

            mf.mesh    = new Mesh { name = "FoldedDot", vertices = verts.ToArray(), triangles = tris.ToArray(), uv = uvs.ToArray(), normals = meshNormals.ToArray() };
            mf.mesh.RecalculateBounds();
            mr.material = dotMaterial != null ? dotMaterial : whiteMaterial;

            return obj;
        }

        /// <summary>
        /// Professional Face-by-Face generator for grid dots with Unified Global Basis.
        /// This ensures perfect, seamless alignment across all 3D corners.
        /// </summary>
        static void AddFoldCircle(
            Vector3 center, List<Vector3> faces, float radius, int segments, float lift,
            List<Vector3> verts, List<int> tris,
            List<Vector2> uvs, List<Vector3> normals)
        {
            if (faces == null || faces.Count == 0) return;

            // 1. Unified Basis for ALL faces to ensure seams match perfectly.
            Vector3 refN = faces[0];
            Vector3 baseRight = Vector3.Cross(refN, Vector3.up);
            if (baseRight.sqrMagnitude < 0.001f) baseRight = Vector3.Cross(refN, Vector3.forward);
            baseRight.Normalize();
            Vector3 baseUp = Vector3.Cross(refN, baseRight).normalized;

            // 2. Build Slices per face
            foreach (var faceN in faces)
            {
                int faceCenterIdx = verts.Count;
                verts.Add(center + faceN * lift);
                uvs.Add(new Vector2(0.5f, 0.5f));
                normals.Add(faceN);

                int startIdx = verts.Count;
                int faceVertCount = 0;

                // 3. Loop through segments using the UNIFIED basis
                for (int s = 0; s <= segments; s++)
                {
                    float   angle  = s * Mathf.PI * 2f / segments;
                    Vector3 worldV = baseRight * Mathf.Cos(angle) + baseUp * Mathf.Sin(angle);
                    
                    // 4. "Octant Culling" logic with slight overlap for seamless spread
                    float dotSelf = Vector3.Dot(worldV.normalized, faceN);
                    
                    bool belongs = true;
                    foreach(var otherN in faces) {
                        if (otherN == faceN) continue;
                        // Use a -0.05 margin to avoid "flat" cutoffs at the edge
                        if (Vector3.Dot(worldV.normalized, otherN) > dotSelf + 0.05f) {
                            belongs = false;
                            break;
                        }
                    }

                    if (belongs)
                    {
                        // 5. Flatten perfectly onto the face plane
                        Vector3 projV = (worldV - Vector3.Dot(worldV, faceN) * faceN);
                        if (projV.sqrMagnitude > 0.0001f)
                        {
                            Vector3 finalV = projV.normalized * radius;
                            verts.Add(center + finalV + faceN * lift);
                            uvs.Add(new Vector2(0.5f, 0.5f));
                            normals.Add(faceN);
                            faceVertCount++;
                        }
                    }
                }

                for (int i = 0; i < faceVertCount - 1; i++)
                {
                    int v1 = startIdx + i;
                    int v2 = startIdx + i + 1;
                    tris.Add(faceCenterIdx); tris.Add(v1); tris.Add(v2);
                    tris.Add(faceCenterIdx); tris.Add(v2); tris.Add(v1);
                }
            }
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
