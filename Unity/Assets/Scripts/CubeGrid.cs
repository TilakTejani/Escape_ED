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
        public float spacingMult    = 3.25f;
        public float dotRadiusMult  = 0.5f;
        public float arrowHeadBaseMult = 2.5f; 
        
        [Header("Visuals")]
        public float    spacing      = 0.28f;
        public float    dotRadius    = 0.04f;
        public Material whiteMaterial;
        public Material dotMaterial;

        private Dictionary<Vector3Int, GameObject> dots         = new Dictionary<Vector3Int, GameObject>();
        private List<GameObject>                   indexedDots  = new List<GameObject>();
        private List<Vector3Int>                   indexedCoords = new List<Vector3Int>(); 
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

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        if (!IsSurface(x, y, z)) continue;
                        Vector3Int gridPos  = new Vector3Int(x, y, z);
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
            if (index >= 0 && index < indexedCoords.Count)
            {
                Vector3Int p = indexedCoords[index];
                return transform.TransformPoint(CalculateWorldPos(p.x, p.y, p.z));
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
            Vector3 cubeDim = (Vector3)(size - Vector3Int.one) * spacing;

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
