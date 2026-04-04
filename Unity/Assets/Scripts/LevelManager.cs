using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeED
{
    public class LevelManager : MonoBehaviour
    {
        [Header("Level Data")]
        [Tooltip("The JSON file defining the arrows and paths.")]
        public TextAsset levelJsonFile;

        [Header("Prefabs")]
        public GameObject arrowPrefab;
        public Material   arrowMaterial; // Assign ArrowPulseMat here

        [Header("References")]
        public CubeGrid grid;
        public bool forceWhiteBackground = true; 
        public LayerMask arrowLayer; // Use Layer 6 (Arrow) in Inspector
        public CubeNavigator navigator;

        private List<GameObject>      activeArrows   = new List<GameObject>();
        public GhostCubeController   ghostController;

        private void OnEnable()
        {
            GameStateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.Playing)
            {
                Debug.Log("[LevelManager] Playing state detected. Auto-Generating Level...");
                GenerateTestLevel();
            }
        }

        private void Reset()
        {
            if (grid == null)      grid      = GetComponent<CubeGrid>();
            if (navigator == null) navigator = GetComponent<CubeNavigator>();
        }

        [ContextMenu("Generate Test Level")]
        public void GenerateTestLevel()
        {
            if (grid == null)      grid      = GetComponent<CubeGrid>();
            if (navigator == null) navigator = GetComponent<CubeNavigator>();

            if (grid == null)
            {
                Debug.LogError("[LevelManager] Grid Reference MISSING!");
                return;
            }

            if (levelJsonFile != null)
            {
                grid.GenerateGrid();
                Debug.Log($"[LevelManager] Loading level from JSON: {levelJsonFile.name}");
                LoadLevelFromJSON(levelJsonFile);
            }
            else
            {
                // [Mobile Fix] Check Resources if no Inspector file is assigned
                TextAsset resourcesJson = Resources.Load<TextAsset>("Levels/5x5x5");
                if (resourcesJson != null)
                {
                    Debug.Log("<color=green>[LevelManager] Loading 5x5x5.json from Resources Success!</color>");
                    grid.GenerateGrid();
                    LoadLevelFromJSON(resourcesJson);
                }
                else
                {
                    Debug.LogWarning("[LevelManager] No JSON found in Inspector or Resources. Falling back to procedural.");
                    GenerateProceduralLevel();
                }
            }
        }

        public void LoadLevelFromJSON(TextAsset jsonAsset)
        {
            ClearActiveLevel();

            LevelData data = LevelLoader.LoadFromJSON(jsonAsset.text);
            if (data == null)
            {
                Debug.LogError("[LevelManager] JSON parsing failed!");
                return;
            }

            // Sync Grid Size
            if (grid != null && data.gridSize != null)
            {
                grid.size = new Vector3Int(data.gridSize.x, data.gridSize.y, data.gridSize.z);
                grid.GenerateGrid();
                Debug.Log($"[LevelManager] Grid Resized to {grid.size}");
            }

            if (data.arrows == null)
            {
                Debug.LogWarning("[LevelManager] No 'arrows' found in JSON!");
                return;
            }

            Debug.Log($"[LevelManager] Spawning {data.arrows.Length} arrows.");
            foreach (var arrowData in data.arrows)
                SpawnArrowFromData(arrowData);

            AutoFrameCamera();
        }

        private void SpawnArrowFromData(ArrowData data)
        {
            if (data.path == null || data.path.Length < 2) return;

            var worldPath  = new List<Vector3>();
            var allNormals = new List<List<Vector3>>();
            var dotTypes   = new List<DotType>();

            foreach (int index in data.path)
            {
                // We use mathematical world positions from the grid directly
                worldPath.Add(grid.GetWorldPosByIndex(index));
                allNormals.Add(grid.GetAllFaceNormals(index));
                dotTypes.Add(grid.GetDotType(index));
                
                // No more dots to hide!
            }

            if (worldPath.Count < 2) return;

            // Ensure arrowhead is always at path end
            if (data.headEnd == "start")
            {
                worldPath.Reverse();
                allNormals.Reverse();
                dotTypes.Reverse();
            }

            GameObject arrowObj = Instantiate(arrowPrefab, transform);
            arrowObj.transform.localPosition = Vector3.zero;
            arrowObj.transform.localRotation = Quaternion.identity;

            Arrow arrow = arrowObj.GetComponent<Arrow>();
            if (arrow == null)
            {
                Debug.LogError($"[LevelManager] arrowPrefab is missing the Arrow script!");
                Destroy(arrowObj);
                return;
            }

            if (arrowMaterial != null) arrow.arrowMaterial = arrowMaterial;
            arrow.SetPath(worldPath, allNormals, dotTypes);
            arrow.OnInteractionTriggered += HandleArrowInteraction;
            activeArrows.Add(arrowObj);

            if (ghostController != null)
                ghostController.RegisterArrow(arrowObj.GetComponent<MeshRenderer>());
        }

        private void ClearActiveLevel()
        {
            foreach (var arrow in activeArrows)
                if (arrow != null) Destroy(arrow);
            activeArrows.Clear();
            if (ghostController != null) ghostController.ClearArrows();
        }

        private void AutoFrameCamera()
        {
            if (grid == null) return;

            // Calculate the maximum dimension of the cube
            float maxDim = Mathf.Max(grid.size.x, grid.size.y, grid.size.z) * grid.spacing;
            
            // Standard Camera Fit Math: 
            // We must account for both Vertical and Horizontal FOV to ensure 
            // the sides don't get cut off on Portrait screens (iPhone).
            Camera cam = Camera.main;
            float aspect = cam.aspect;
            float vFovRad = cam.fieldOfView * Mathf.Deg2Rad;
            
            // Calculate horizontal FOV in radians
            float hFovRad = 2.0f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);
            
            // We use the smaller of the two FOVs to "Fit" the cube safely
            float effectiveFovRad = Mathf.Min(vFovRad, hFovRad);
            
            // distance = (size / 2) / tan(fov / 2)
            float distance = (maxDim * 1.1f) / Mathf.Tan(effectiveFovRad * 0.5f);
            
            // Relaxing the clamp for smaller cubes (2x2)
            distance = Mathf.Max(distance, 2.5f);
            
            // Update the CubeRotator so it doesn't snap back on first touch
            CubeRotator rotator = GetComponent<CubeRotator>();
            if (rotator == null) rotator = grid.GetComponent<CubeRotator>();
            
            if (rotator != null)
            {
                rotator.SetZoomLimits(distance);
            }
            else
            {
                // Fallback: move camera directly
                Vector3 camPos = cam.transform.position;
                Vector3 dir = (camPos - transform.position).normalized;
                cam.transform.position = transform.position + dir * distance;
            }

            Debug.Log($"[LevelManager] Auto-Framing Cube at distance: {distance:F2}");
        }

        void Awake()
        {
            navigator       = GetComponent<CubeNavigator>();
            if (grid != null) ghostController = grid.GetComponent<GhostCubeController>();
        }

        void Start()
        {
            // Apply Environment Styling (Plain White Background)
            if (forceWhiteBackground)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.white;
                }
            }

            // Note: GameState change is now handled by UIManager/HomeScreenView in Mobile Flow

            if (levelJsonFile != null)
            {
                if (grid == null) grid = GetComponent<CubeGrid>();
                grid.GenerateGrid();
                LoadLevelFromJSON(levelJsonFile);
                Debug.Log("[LevelManager] Auto-loaded level on Start.");
            }
        }

        void Update()
        {
            // 🖥️ PC/Editor Shortcut — eject all
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                TestAllEjections();
                return;
            }
        }

        private void HandleArrowInteraction(Arrow arrow)
        {
            if (arrow == null || arrow.IsEjecting) return;

            if (IsArrowBlocked(arrow))
            {
                arrow.PlayBlockedAnimation();
            }
            else
            {
                arrow.Eject();
                activeArrows.Remove(arrow.gameObject);
            }
        }

        // Zero-GC Mobile Optimization for Overlap Queries
        private Collider[] overlapResults = new Collider[30];

        private bool IsArrowBlocked(Arrow arrow)
        {
            arrow.GetEjectionData(out Vector3 tipPos, out Vector3 tipDir, out Vector3 faceNormal);

            // 0.95f safety margin to allow corner-kiss without grazing side obstacles
            float radius        = (arrow.lineWidth * 0.5f) * 0.95f;
            float checkDistance = grid != null ? Mathf.Max(grid.size.x, grid.size.y, grid.size.z) * grid.spacing : 20f;

            // p1 at tip surface, p2 extends full grid width along eject direction
            Vector3 p1 = tipPos + faceNormal * arrow.surfaceOffset;
            Vector3 p2 = p1 + tipDir * checkDistance;

            int hitCount = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, overlapResults, LayerMask.GetMask(ArrowConstants.LAYER_ARROW));

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = overlapResults[i];
                if (hitCollider == null) continue;

                // Ignore own body segments
                if (hitCollider.transform.IsChildOf(arrow.transform)) continue;

                Arrow hitArrow = hitCollider.GetComponentInParent<Arrow>();
                if (hitArrow != null && hitArrow != arrow && !hitArrow.IsEjecting)
                    return true;
            }
            return false;
        }

        public void OnJumpPressed(InputAction.CallbackContext context)
        {
            if (context.performed && GameStateManager.Instance.CurrentState == GameState.Playing)
                GenerateProceduralLevel();
        }

        [ContextMenu("Test All Ejections")]
        public void TestAllEjections()
        {
            Debug.Log($"[LevelManager] Triggering ejection for {activeArrows.Count} arrows.");
            foreach (var arrowObj in activeArrows)
            {
                if (arrowObj == null) continue;
                Arrow arrow = arrowObj.GetComponent<Arrow>();
                if (arrow != null) arrow.Eject();
            }
            
            // Note: Arrow.cs handles its own destruction after animation
            activeArrows.Clear();
        }

        [ContextMenu("Generate Procedural Level")]
        public void GenerateProceduralLevel()
        {
            if (grid == null)      grid      = GetComponent<CubeGrid>();
            if (navigator == null) navigator = GetComponent<CubeNavigator>();
            
            if (grid == null || navigator == null)
            {
                Debug.LogError($"[LevelManager] FAILED: Grid({grid!=null}) or Navigator({navigator!=null}) missing!");
                return;
            }

            ClearActiveLevel();

            Vector3Int startPoint = Vector3Int.zero;
            var path = new List<Vector3Int> { startPoint };
            Vector3Int current = startPoint;

            for (int i = 0; i < 10; i++)
            {
                MoveDir randomDir = (MoveDir)Random.Range(0, 4);
                Vector3Int next = navigator.GetNextPoint(current, randomDir);
                if (next != current) { path.Add(next); current = next; }
            }

            if (path.Count > 1) SpawnArrow(path);
        }

        void SpawnArrow(List<Vector3Int> gridPath)
        {
            GameObject obj   = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
            Arrow      arrow = obj.GetComponent<Arrow>();
            if (arrowMaterial != null) arrow.arrowMaterial = arrowMaterial;

            var worldPoints = new List<Vector3>();
            var allNormals  = new List<List<Vector3>>();
            var dotTypes    = new List<DotType>();

            foreach (var p in gridPath)
            {
                worldPoints.Add(grid.transform.TransformPoint(grid.CalculateWorldPos(p.x, p.y, p.z)));
                allNormals.Add(grid.GetAllFaceNormals(p));
                dotTypes.Add(grid.GetDotType(p));
            }

            arrow.SetPath(worldPoints, allNormals, dotTypes);
            arrow.OnInteractionTriggered += HandleArrowInteraction;
            activeArrows.Add(obj);

            if (ghostController != null)
                ghostController.RegisterArrow(obj.GetComponent<MeshRenderer>());
        }
    }
}
