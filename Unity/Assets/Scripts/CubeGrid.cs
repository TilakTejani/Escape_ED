using UnityEngine;
using System.Collections.Generic;

namespace EscapeED
{
    public enum DotType { Face, Edge, Corner }

    [ExecuteAlways]
    [RequireComponent(typeof(GhostCubeController))]
    public class CubeGrid : MonoBehaviour
    {
        public Vector3Int size      = new Vector3Int(5, 5, 5);
        
        [Header("Master Scale")]
        [Tooltip("Automatically calculate spacing and dotRadius relative to Arrow width.")]
        public bool debugLog = true; 
        public bool  autoScale      = true;
        public float spacingMult    = 1.25f;
        public float dotRadiusMult  = 0.5f;
        public float arrowHeadBaseMult = 2.5f; 
        
        [Header("Visuals")]
        public float    spacing      = 0.28f;
        public float    dotRadius    = 0.04f;
        public Material whiteMaterial;
        public Material dotMaterial;

        [Header("Edges")]
        [Tooltip("0 = auto (spacing * 0.12)")]
        public float edgeThickness = 0f;
        [Range(0f, 1f)] public float edgeAlpha = 0.25f;

        private Dictionary<Vector3Int, GameObject> dots         = new Dictionary<Vector3Int, GameObject>();
        private List<GameObject>                   indexedDots  = new List<GameObject>();
        private List<Vector3Int>                   indexedCoords = new List<Vector3Int>();
        private List<Vector3>                      indexedWorldPositions = new List<Vector3>();
        private List<Vector3>                      indexedNormals = new List<Vector3>();
        private GameObject                         backgroundCube;
        private List<GameObject>                   visualFaces  = new List<GameObject>();
        private List<GameObject>                   edgeObjects  = new List<GameObject>();
        private GhostCubeController                ghostController;
        private LevelManager                       _cachedLevelManager;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        void OnEnable()
        {
            if (Application.isPlaying) GenerateGrid();
            else RefreshBackground();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () => {
                    if (this != null) {
                        GenerateGrid();
                        RefreshBackground();
                    }
                };
            }
#endif
        }

        private void Reset()
        {
            size = new Vector3Int(5, 5, 5);
            spacing = 0.28f;
            dotRadius = 0.04f;
            GenerateGrid();
        }

        [ContextMenu("Force Regenerate Grid")]
        public void ManualRegenerate()
        {
            GenerateGrid();
            RefreshBackground();
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
            if (autoScale) ApplyMasterScale();
            ClearDots();

            dots.Clear();
            indexedDots.Clear();
            indexedCoords.Clear();
            indexedWorldPositions.Clear();
            indexedNormals.Clear();

            float centerX = size.x / 2f;
            float centerY = size.y / 2f;
            float centerZ = size.z / 2f;

            // REPLICATE WEB ORDER: Front, Back, Right, Left, Top, Bottom
            
            // 0: Front (+Z), 1: Back (-Z)
            for (int face = 0; face < 2; face++) {
                bool isFront = face == 0;
                float zPos = isFront ? centerZ : -centerZ;
                Vector3 normal = isFront ? Vector3.forward : Vector3.back;
                for (int y = 0; y < size.y; y++) {
                    for (int x = 0; x < size.x; x++) {
                        AddNode(new Vector3Int(x, y, isFront ? size.z - 1 : 0), 
                               new Vector3(x + 0.5f - centerX, y + 0.5f - centerY, zPos), normal);
                    }
                }
            }

            // 2: Right (+X), 3: Left (-X)
            for (int face = 2; face < 4; face++) {
                bool isRight = face == 2;
                float xPos = isRight ? centerX : -centerX;
                Vector3 normal = isRight ? Vector3.right : Vector3.left;
                for (int y = 0; y < size.y; y++) {
                    for (int z = 0; z < size.z; z++) {
                        AddNode(new Vector3Int(isRight ? size.x - 1 : 0, y, z), 
                               new Vector3(xPos, y + 0.5f - centerY, z + 0.5f - centerZ), normal);
                    }
                }
            }

            // 4: Top (+Y), 5: Bottom (-Y)
            for (int face = 4; face < 6; face++) {
                bool isTop = face == 4;
                float yPos = isTop ? centerY : -centerY;
                Vector3 normal = isTop ? Vector3.up : Vector3.down;
                for (int z = 0; z < size.z; z++) {
                    for (int x = 0; x < size.x; x++) {
                        AddNode(new Vector3Int(x, isTop ? size.y - 1 : 0, z), 
                               new Vector3(x + 0.5f - centerX, yPos, z + 0.5f - centerZ), normal);
                    }
                }
            }

            Debug.Log($"Generated {indexedDots.Count} face-centered nodes across 6 surfaces.");
        }

        private void AddNode(Vector3Int gridPos, Vector3 worldPos, Vector3 normal)
        {
            indexedDots.Add(null);
            indexedCoords.Add(gridPos);
            indexedWorldPositions.Add(worldPos * spacing);
            indexedNormals.Add(normal);
            // We don't use 'dots' dictionary for indexing anymore since coords are not unique
        }

        private void ApplyMasterScale()
        {
            if (_cachedLevelManager == null) _cachedLevelManager = FindAnyObjectByType<LevelManager>();
            LevelManager lm = _cachedLevelManager;
            float arrowWidth = 0.08f;
            
            if (lm != null && lm.arrowPrefab != null)
            {
                Arrow a = lm.arrowPrefab.GetComponent<Arrow>();
                if (a != null) 
                {
                    arrowWidth = a.lineWidth;
                    a.tipLengthMult = arrowHeadBaseMult;
                    a.tipWidthMult  = arrowHeadBaseMult;
                }
            }
            
            spacing   = arrowWidth * spacingMult;
            dotRadius = arrowWidth * dotRadiusMult;
            
            Debug.Log($"[CubeGrid] MasterScale Applied: Spacing={spacing:F2}, DotRadius={dotRadius:F3}, ArrowHead={arrowHeadBaseMult:F1}x");
        }

        public Vector3 GetWorldPosByIndex(int index)
        {
            if (index >= 0 && index < indexedWorldPositions.Count)
            {
                return transform.TransformPoint(indexedWorldPositions[index]);
            }
            return Vector3.zero;
        }

        public GameObject GetDotByIndex(int index)
        {
            if (index >= 0 && index < indexedDots.Count) return indexedDots[index];
            return null;
        }

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
                    backgroundCube = new GameObject("VisualCube");
                    backgroundCube.transform.SetParent(transform);
                }
            }

            // CRITICAL CULPRIT REMOVAL:
            var parentRenderer = backgroundCube.GetComponent<MeshRenderer>();
            var parentFilter = backgroundCube.GetComponent<MeshFilter>();
            if (parentRenderer != null)
            {
                if (Application.isPlaying) Destroy(parentRenderer);
                else                       DestroyImmediate(parentRenderer);
            }
            if (parentFilter != null)
            {
                if (Application.isPlaying) Destroy(parentFilter);
                else                       DestroyImmediate(parentFilter);
            }

            backgroundCube.transform.localPosition = Vector3.zero;

            if (backgroundCube != null)
            {
                var children = new List<GameObject>();
                foreach (Transform child in backgroundCube.transform) children.Add(child.gameObject);
                foreach (var child in children)
                {
                    if (Application.isPlaying) Destroy(child);
                    else                       DestroyImmediate(child);
                }
            }
            
            foreach (Transform child in transform)
            {
                if (child.gameObject == backgroundCube) continue;
                if (child.name.Contains("Cube") || child.name.Contains("Quad") || child.name.Contains("Dot"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else                       DestroyImmediate(child.gameObject);
                }
            }

            visualFaces.Clear();
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            Vector3 cubeDim = (Vector3)size * spacing;

            foreach (var n in normals)
            {
                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Face_" + n.ToString();
                face.transform.SetParent(backgroundCube.transform);
                face.transform.localPosition = Vector3.Scale(n, cubeDim * 0.5f);
                
                // Orient face OUTWARD
                face.transform.localRotation = Quaternion.LookRotation(-n);
                
                float sw = (Mathf.Abs(n.x) > 0.01f) ? cubeDim.z : cubeDim.x;
                float sh = (Mathf.Abs(n.y) > 0.01f) ? cubeDim.z : cubeDim.y;
                face.transform.localScale = new Vector3(sw, sh, 1f);

                var mc = face.GetComponent<MeshCollider>();
                if (mc != null) 
                {
                    if (Application.isPlaying) Destroy(mc);
                    else                       DestroyImmediate(mc);
                }

                if (whiteMaterial != null) 
                {
                    face.GetComponent<Renderer>().sharedMaterial = whiteMaterial;
                    if (debugLog && visualFaces.Count == 0) Debug.Log($"[CubeGrid] Assigned core material: {whiteMaterial.name}");
                }
                visualFaces.Add(face);
            }

            if (ghostController == null) ghostController = GetComponent<GhostCubeController>();
            if (ghostController == null) ghostController = gameObject.AddComponent<GhostCubeController>();

            ghostController.Initialize(visualFaces, new List<Vector3>(normals));
            Debug.Log($"[CubeGrid] Background Refreshed: {visualFaces.Count} faces initialized.");

            CreateEdgeLines(cubeDim);
        }

        private void CreateEdgeLines(Vector3 cubeDim)
        {
            edgeObjects.Clear(); // GameObjects already destroyed by CreateBackgroundCube cleanup above

            float hx = cubeDim.x * 0.5f;
            float hy = cubeDim.y * 0.5f;
            float hz = cubeDim.z * 0.5f;
            float t  = edgeThickness > 0f ? edgeThickness : spacing * 0.03f;

            // 12 edges as (localCenter, scale). Axis-aligned — no rotation needed.
            // Extra `t` on the long axis so adjacent edges meet flush at corners.
            (Vector3 center, Vector3 scale)[] edges =
            {
                // 4 along X
                (new Vector3(0, -hy, -hz), new Vector3(cubeDim.x + t, t, t)),
                (new Vector3(0, +hy, -hz), new Vector3(cubeDim.x + t, t, t)),
                (new Vector3(0, -hy, +hz), new Vector3(cubeDim.x + t, t, t)),
                (new Vector3(0, +hy, +hz), new Vector3(cubeDim.x + t, t, t)),
                // 4 along Y
                (new Vector3(-hx, 0, -hz), new Vector3(t, cubeDim.y + t, t)),
                (new Vector3(+hx, 0, -hz), new Vector3(t, cubeDim.y + t, t)),
                (new Vector3(-hx, 0, +hz), new Vector3(t, cubeDim.y + t, t)),
                (new Vector3(+hx, 0, +hz), new Vector3(t, cubeDim.y + t, t)),
                // 4 along Z
                (new Vector3(-hx, -hy, 0), new Vector3(t, t, cubeDim.z + t)),
                (new Vector3(+hx, -hy, 0), new Vector3(t, t, cubeDim.z + t)),
                (new Vector3(-hx, +hy, 0), new Vector3(t, t, cubeDim.z + t)),
                (new Vector3(+hx, +hy, 0), new Vector3(t, t, cubeDim.z + t)),
            };

            var propBlock = new MaterialPropertyBlock();
            var edgeColor = new Color(1f, 1f, 1f, edgeAlpha);

            for (int i = 0; i < edges.Length; i++)
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = $"Edge_{i}";
                obj.transform.SetParent(backgroundCube.transform);
                obj.transform.localPosition = edges[i].center;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale    = edges[i].scale;

                // Edges are decorative — no collider needed
                var col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else                       DestroyImmediate(col);
                }

                var rend = obj.GetComponent<MeshRenderer>();
                if (whiteMaterial != null) rend.sharedMaterial = whiteMaterial;

                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, edgeColor);
                propBlock.SetColor(ColorId, edgeColor);
                rend.SetPropertyBlock(propBlock);

                edgeObjects.Add(obj);
            }
        }

        public GameObject GetDotAt(Vector3Int gridPos)
        {
            if (dots.ContainsKey(gridPos)) return dots[gridPos];
            return null;
        }

        public Vector3 GetSurfaceNormal(int index)
        {
            if (index >= 0 && index < indexedNormals.Count) return indexedNormals[index];
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
            float centerX = size.x / 2f;
            float centerY = size.y / 2f;
            float centerZ = size.z / 2f;

            // Start with true centroid
            float px = (x + 0.5f) - centerX;
            float py = (y + 0.5f) - centerY;
            float pz = (z + 0.5f) - centerZ;

            // Project only the axis defining the surface to the boundary
            if (x == 0) px = -centerX;
            else if (x == size.x - 1) px = centerX;

            if (y == 0) py = -centerY;
            else if (y == size.y - 1) py = centerY;

            if (z == 0) pz = -centerZ;
            else if (z == size.z - 1) pz = centerZ;

            return new Vector3(px, py, pz) * spacing;
        }

        public int GetIndexByCoords(Vector3Int p)
        {
            for (int i = 0; i < indexedCoords.Count; i++)
            {
                if (indexedCoords[i] == p) return i;
            }
            return -1;
        }

        private void ClearDots()
        {
            foreach (var dot in dots.Values)
            {
                if (dot == null) continue;
                if (Application.isPlaying) Destroy(dot);
                else                       DestroyImmediate(dot);
            }
        }
    }
}
