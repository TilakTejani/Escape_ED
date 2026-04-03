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

        /// <summary>
        /// MANUAL INITIALIZATION: 
        /// Used by the bootstrapper to fix the UI on the fly.
        /// </summary>
        public void Initialize(BaseUIPanel[] manualPanels)
        {
            this.panels = manualPanels;
            panelMap.Clear();

            foreach (var panel in panels)
            {
                if (panel == null) continue;
                panelMap[panel.targetState] = panel;
                panel.gameObject.SetActive(false);
                Debug.Log($"[UIManager] Manually Registered Panel: {panel.name} for State: {panel.targetState}");
            }

            // Kick-start the first state immediately
            if (GameStateManager.Instance != null)
            {
                HandleStateChanged(GameStateManager.Instance.CurrentState);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // If we already have panels (inspector or manual), don't search
            if (panels != null && panels.Length > 0)
            {
                Initialize(panels);
                return;
            }

            // AUTO-RECONNECT: If inspector refs are lost, find them in children automatically
            panels = GetComponentsInChildren<BaseUIPanel>(true);

            if (panels != null && panels.Length > 0)
            {
                Initialize(panels);
            }
            else
            {
                Debug.LogWarning("[UIManager] No panels found yet. Waiting for manual Initialize call.");
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
            // MOBILE STABILITY: On iOS, wait a few frames for the world to stabilize
            yield return new WaitForSeconds(0.2f);

            GameStateManager manager = null;
            int retries = 5;

            while (manager == null && retries > 0)
            {
                manager = GameStateManager.Instance;
                if (manager == null) manager = Object.FindAnyObjectByType<GameStateManager>();

                if (manager == null)
                {
                    retries--;
                    Debug.LogWarning($"[UIManager] GameStateManager not found, retrying... ({retries} left)");
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (manager != null)
            {
                Debug.Log($"[UIManager] Resilient start-up initialization. Current State: {manager.CurrentState}");
                HandleStateChanged(manager.CurrentState);
            }
            else
            {
                Debug.LogError("[UIManager] CRITICAL ERROR: GameStateManager NOT FOUND after multiple retries! Please check your scene hierarchy.");
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
