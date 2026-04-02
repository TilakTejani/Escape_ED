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
        public bool  autoScale      = true;
        public float spacingMult    = 3.25f;
        public float dotRadiusMult  = 0.5f;
        public float arrowHeadBaseMult = 2.5f; // New: Sync Arrowhead length/width from here
        
        [Header("Visuals")]
        public float    spacing      = 0.28f;
        public float    dotRadius    = 0.04f;
        public Material whiteMaterial;
        public Material dotMaterial;

        private Dictionary<Vector3Int, GameObject> dots         = new Dictionary<Vector3Int, GameObject>();
        private List<GameObject>                   indexedDots  = new List<GameObject>();
        private List<Vector3Int>                   indexedCoords = new List<Vector3Int>(); // parallel to indexedDots
        private GameObject                         backgroundCube;
        private List<GameObject>                   visualFaces = new List<GameObject>();
        private GhostCubeController                ghostController;

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
                        GenerateGrid();
                        // Refresh background matches the spacing immediately.
                        RefreshBackground();
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
            if (autoScale) ApplyMasterScale();

            // Use a helper to clear existing dots based on context
            ClearDots();

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
                        
                        // Always keep the indexing for LevelData and Arrow placement 
                        // even without visual dots.
                        dots[gridPos] = null;
                        indexedDots.Add(null);
                        indexedCoords.Add(gridPos);
                    }
                }
            }
            Debug.Log($"Generated {indexedDots.Count} surface dots.");
        }

        private void ApplyMasterScale()
        {
            LevelManager lm = FindAnyObjectByType<LevelManager>();
            float arrowWidth = 0.08f; // Default fallback
            
            if (lm != null && lm.arrowPrefab != null)
            {
                Arrow a = lm.arrowPrefab.GetComponent<Arrow>();
                if (a != null) 
                {
                    arrowWidth = a.lineWidth;
                    // Protrude Master Scale to the Arrow prefab for 1:1 Head Ratio
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
            if (index >= 0 && index < indexedCoords.Count)
            {
                Vector3Int p = indexedCoords[index];
                return CalculateWorldPos(p.x, p.y, p.z);
            }
            return Vector3.zero;
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
                    backgroundCube      = new GameObject("VisualCube");
                    backgroundCube.transform.SetParent(transform);
                }
            }

            backgroundCube.transform.localPosition = Vector3.zero;

            // Clear ALL existing faces from the hierarchy (not just the ones in our local list)
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
            visualFaces.Clear();

            // Create 6 Faces (Planes)
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            Vector3 cubeDim = (Vector3)(size - Vector3Int.one) * spacing;

            foreach (var n in normals)
            {
                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Face_" + n.ToString();
                face.transform.SetParent(backgroundCube.transform);
                
                // Position at the cube edge
                face.transform.localPosition = Vector3.Scale(n, cubeDim * 0.5f);
                
                // Orient face to look outward
                face.transform.localRotation = Quaternion.LookRotation(n);
                
                // Scale to match face dimensions
                float sw = (Mathf.Abs(n.x) > 0.01f) ? cubeDim.z : cubeDim.x;
                float sh = (Mathf.Abs(n.y) > 0.01f) ? cubeDim.z : cubeDim.y;
                face.transform.localScale = new Vector3(sw, sh, 1f);

                if (whiteMaterial != null) face.GetComponent<Renderer>().sharedMaterial = whiteMaterial;
                visualFaces.Add(face);
            }

            // Initialize/Update the Ghost Controller
            if (ghostController == null) ghostController = GetComponent<GhostCubeController>();
            if (ghostController == null) ghostController = gameObject.AddComponent<GhostCubeController>();
            
            ghostController.Initialize(visualFaces, new List<Vector3>(normals));
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
