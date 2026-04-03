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
        public bool forceWhiteBackground = true; // New: Toggle for plain white look
        private CubeNavigator navigator;

        private List<GameObject>      activeArrows   = new List<GameObject>();
        private GhostCubeController   ghostController;

        [Header("Physics")]
        public LayerMask arrowLayer; // New: Assigned to "Arrow" layer

        private void Reset()
        {
            if (grid == null)      grid      = GetComponent<CubeGrid>();
            if (navigator == null) navigator = GetComponent<CubeNavigator>();
        }

        void Awake()
        {
            navigator       = GetComponent<CubeNavigator>();
            if (grid != null) ghostController = grid.GetComponent<GhostCubeController>();
        }

        void OnEnable()
        {
            GameStateManager.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState newState)
        {
            // Transition to actual gameplay logic only when in Playing state
            if (newState == GameState.Playing)
            {
                GenerateTestLevel();
            }
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

            // Note: GameStateManager will auto-trigger GameState.Init on its own Start()
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
                Debug.LogWarning("[LevelManager] No JSON assigned. Falling back to procedural.");
                GenerateProceduralLevel();
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
                worldPath.Add(grid.GetWorldPosByIndex(index));
                allNormals.Add(grid.GetAllFaceNormals(index));
                dotTypes.Add(grid.GetDotType(index));
            }

            if (worldPath.Count < 2) return;

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
                if (arrow != null) DestroyImmediate(arrow);
            activeArrows.Clear();
            if (ghostController != null) ghostController.ClearArrows();
        }

        private void AutoFrameCamera()
        {
            if (grid == null) return;

            float maxDim = Mathf.Max(grid.size.x, grid.size.y, grid.size.z) * grid.spacing;
            Camera cam = Camera.main;
            float aspect = cam.aspect;
            float vFovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float hFovRad = 2.0f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);
            float effectiveFovRad = Mathf.Min(vFovRad, hFovRad);
            float distance = (maxDim * 1.1f) / Mathf.Tan(effectiveFovRad * 0.5f);
            distance = Mathf.Max(distance, 2.5f);
            
            CubeRotator rotator = GetComponent<CubeRotator>();
            if (rotator == null) rotator = grid.GetComponent<CubeRotator>();
            
            if (rotator != null) {
                rotator.SetZoomLimits(distance);
            } else {
                Vector3 camPos = cam.transform.position;
                Vector3 dir = (camPos - transform.position).normalized;
                cam.transform.position = transform.position + dir * distance;
            }
        }

        void Update()
        {
            // Block input unless we are in the Playing state
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

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

        private bool IsArrowBlocked(Arrow arrow)
        {
            arrow.GetEjectionData(out Vector3 tipPos, out Vector3 tipDir, out Vector3 faceNormal);
            
            // SphereCast with a slightly smaller radius than the actual arrow so we don't graze neighbors
            float radius = arrow.lineWidth * 0.45f;
            Vector3 origin = tipPos + faceNormal * arrow.surfaceOffset;

            // Cast forward far enough to clear a reasonably sized grid
            float checkDistance = grid != null ? Mathf.Max(grid.size.x, grid.size.y, grid.size.z) * grid.spacing : 20f;

            // Only check collisions on the Arrow layer
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, tipDir, checkDistance, arrowLayer);

            foreach (var hit in hits)
            {
                Arrow hitArrow = hit.collider.GetComponentInParent<Arrow>();
                if (hitArrow != null && hitArrow != arrow && !hitArrow.IsEjecting)
                {
                    Debug.Log($"[LevelManager] Ejection blocked! {arrow.name} hit {hitArrow.name}");
                    return true;
                }
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
            foreach (var arrowObj in activeArrows)
            {
                if (arrowObj == null) continue;
                Arrow arrow = arrowObj.GetComponent<Arrow>();
                if (arrow != null) arrow.Eject();
            }
            activeArrows.Clear();
        }

        [ContextMenu("Generate Procedural Level")]
        public void GenerateProceduralLevel()
        {
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
                worldPoints.Add(grid.CalculateWorldPos(p.x, p.y, p.z));
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
