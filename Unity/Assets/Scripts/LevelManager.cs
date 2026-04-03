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
        private CubeNavigator navigator;

        private List<GameObject>      activeArrows   = new List<GameObject>();
        private GhostCubeController   ghostController;

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

            // Single tap — raycast to find and eject the tapped arrow
            bool tapped = false;
            Vector2 tapPos = Vector2.zero;

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                tapped = true;
                tapPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
                tapPos = Mouse.current.position.ReadValue();
            }

            if (tapped)
            {
                Ray ray = Camera.main.ScreenPointToRay(tapPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Arrow arrow = hit.collider.GetComponent<Arrow>();
                    if (arrow != null)
                    {
                        arrow.Eject();
                        activeArrows.Remove(arrow.gameObject);
                    }
                }
            }
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
            activeArrows.Add(obj);

            if (ghostController != null)
                ghostController.RegisterArrow(obj.GetComponent<MeshRenderer>());
        }
    }
}
