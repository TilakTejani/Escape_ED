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
            obj.transform.position = worldPos + avgNormal * 0.002f;
            obj.transform.rotation = Quaternion.identity; // We build in world space coords relative to worldPos

            MeshFilter   mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();

            mf.mesh    = BuildFoldedCircleMesh(dotRadius, dotSegments, normals);
            mr.material = dotMaterial != null ? dotMaterial : whiteMaterial;

            return obj;
        }

        /// <summary>
        /// Generates a circle mesh that is "bent" to lay flat on all provided face normals.
        /// </summary>
        private static Mesh BuildFoldedCircleMesh(float radius, int segments, List<Vector3> faces)
        {
            var verts       = new List<Vector3>();
            var tris        = new List<int>();
            var uvs         = new List<Vector2>();
            var meshNormals = new List<Vector3>();

            // Centre vertex (origin is the cube vertex)
            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            meshNormals.Add(faces[0]); // Arbitrary center normal

            // We build the circle on a plane perpendicular to the average normal first
            Vector3 avgN = Vector3.zero;
            foreach (var f in faces) avgN += f;
            avgN.Normalize();

            // Basis for the circle plane
            Vector3 right = Vector3.Cross(avgN, Vector3.up);
            if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(avgN, Vector3.forward);
            right.Normalize();
            Vector3 up = Vector3.Cross(avgN, right).normalized;

            // Ring vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 flatV = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;

                // "Fold" the vertex onto the closest available face
                Vector3 foldedV = flatV;
                float   maxDot  = -2f;
                Vector3 bestN   = faces[0];

                foreach (var n in faces)
                {
                    float d = Vector3.Dot(flatV.normalized, n);
                    if (d > maxDot)
                    {
                        maxDot = d;
                        bestN  = n;
                    }
                }

                // Project flatV into the plane of bestN
                // P = V - (V.N)*N
                foldedV = flatV - Vector3.Dot(flatV, bestN) * bestN;
                
                verts.Add(foldedV);
                uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
                meshNormals.Add(bestN);
            }

            // Triangles
            for (int i = 1; i <= segments; i++)
            {
                int next = (i < segments) ? i + 1 : 1;
                tris.Add(0); tris.Add(i); tris.Add(next);
            }

            Mesh mesh = new Mesh { name = "FoldedDot" };
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv        = uvs.ToArray();
            mesh.normals   = meshNormals.ToArray();
            mesh.RecalculateBounds();
            return mesh;
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
