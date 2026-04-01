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
        private CubeNavigator navigator;

        private List<GameObject> activeArrows  = new List<GameObject>();
        private List<GameObject> hiddenDots    = new List<GameObject>();

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
        }

        private void SpawnArrowFromData(ArrowData data)
        {
            if (data.path == null || data.path.Length < 2) return;

            var worldPath  = new List<Vector3>();
            var allNormals = new List<List<Vector3>>();
            var dotTypes   = new List<DotType>();

            foreach (int index in data.path)
            {
                GameObject dot = grid.GetDotByIndex(index);
                if (dot != null)
                {
                    worldPath.Add(dot.transform.position);
                    allNormals.Add(grid.GetAllFaceNormals(index));
                    dotTypes.Add(grid.GetDotType(index));
                    dot.SetActive(false);
                    hiddenDots.Add(dot);
                }
                else
                {
                    Debug.LogWarning($"[LevelManager] Missing Dot #{index} for Arrow {data.id}!");
                }
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
            activeArrows.Add(arrowObj);
        }

        private void ClearActiveLevel()
        {
            foreach (var arrow in activeArrows)
                if (arrow != null) DestroyImmediate(arrow);
            activeArrows.Clear();

            foreach (var dot in hiddenDots)
                if (dot != null) dot.SetActive(true);
            hiddenDots.Clear();
        }

        void Awake()
        {
            navigator = GetComponent<CubeNavigator>();
        }

        void Start()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.UpdateState(GameState.Playing);

            if (levelJsonFile != null)
            {
                if (grid == null) grid = GetComponent<CubeGrid>();
                grid.GenerateGrid();
                LoadLevelFromJSON(levelJsonFile);
                Debug.Log("[LevelManager] Auto-loaded level on Start.");
            }
        }

        public void OnJumpPressed(InputAction.CallbackContext context)
        {
            if (context.performed && GameStateManager.Instance.CurrentState == GameState.Playing)
                GenerateProceduralLevel();
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

                GameObject dot = grid.GetDotAt(p);
                if (dot != null) { dot.SetActive(false); hiddenDots.Add(dot); }
            }

            arrow.SetPath(worldPoints, allNormals, dotTypes);
            activeArrows.Add(obj);
        }
    }
}
