using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EscapeED;

namespace EscapeED.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Panels")]
        public BaseUIPanel[] panels;
        
        private Dictionary<GameState, BaseUIPanel> panelMap = new Dictionary<GameState, BaseUIPanel>();
        private BaseUIPanel currentPanel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // AUTO-RECONNECT: If inspector refs are lost, find them in children automatically
            if (panels == null || panels.Length == 0)
            {
                Debug.LogWarning("[UIManager] Inspector references lost. Attempting Auto-Reconnect with children...");
                panels = GetComponentsInChildren<BaseUIPanel>(true);
            }

            if (panels == null || panels.Length == 0)
            {
                Debug.LogError("[UIManager] CRITICAL ERROR: No BaseUIPanel children found! Please run 'Setup EscapeED UI' context menu action.");
                return;
            }

            foreach (var panel in panels)
            {
                if (panel == null) continue;
                panelMap[panel.targetState] = panel;
                panel.gameObject.SetActive(false);
                Debug.Log($"[UIManager] Registered Panel: {panel.name} for State: {panel.targetState}");
            }
        }

        private void OnEnable()
        {
            GameStateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            StartCoroutine(InitUIWithDelay());
        }

        private IEnumerator InitUIWithDelay()
        {
            // Give Unity 0.1s to finish and stabilize all script Awake/Start calls
            yield return new WaitForSeconds(0.1f);

            // AGGRESSIVE SEARCH: If Instance is still null, search the entire scene!
            GameStateManager manager = GameStateManager.Instance;
            if (manager == null)
            {
                manager = Object.FindAnyObjectByType<GameStateManager>();
                if (manager != null)
                {
                    Debug.Log("[UIManager] Auto-Search found GameStateManager!");
                }
            }

            if (manager != null)
            {
                Debug.Log($"[UIManager] Start-up initialization. State: {manager.CurrentState}");
                HandleStateChanged(manager.CurrentState);
            }
            else
            {
                Debug.LogError("[UIManager] CRITICAL ERROR: GameStateManager NOT FOUND in scene! Please ensure the Manager object exists.");
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            Debug.Log($"[UIManager] Transitioning to State: {newState}");

            if (panelMap.TryGetValue(newState, out BaseUIPanel targetPanel))
            {
                if (currentPanel != null && currentPanel != targetPanel)
                {
                    currentPanel.Hide();
                }

                currentPanel = targetPanel;
                targetPanel.gameObject.SetActive(true);
                targetPanel.Show();
                Debug.Log($"[UIManager] Showing Panel: {targetPanel.name}");
            }
            else
            {
                Debug.Log($"[UIManager] No specific panel mapped for state: {newState}. Hiding current UI.");
                if (currentPanel != null)
                {
                    currentPanel.Hide();
                    currentPanel = null;
                }
            }
        }
    }
}
