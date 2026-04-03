using UnityEngine;
using System.Collections.Generic;

namespace EscapeED.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("References")]
        public BaseUIPanel[] panels;

        private Dictionary<GameState, BaseUIPanel> panelMap = new Dictionary<GameState, BaseUIPanel>();
        private BaseUIPanel currentPanel;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // Initialize panel map
            foreach (var panel in panels)
            {
                panelMap[panel.targetState] = panel;
                panel.gameObject.SetActive(false); // Start hidden
            }
        }

        void OnEnable()
        {
            GameStateManager.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        void Start()
        {
            // --- CATCH UP CHECK ---
            // If the GameStateManager already started (Init), we must show the UI now.
            if (GameStateManager.Instance != null)
            {
                HandleStateChanged(GameStateManager.Instance.CurrentState);
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            // If we are already showing this panel, do nothing
            if (currentPanel != null && currentPanel.targetState == newState) return;

            if (currentPanel != null) currentPanel.Hide();

            if (panelMap.TryGetValue(newState, out var nextPanel))
            {
                currentPanel = nextPanel;
                currentPanel.Show();
            }
            else
            {
                currentPanel = null;
            }
        }
    }
}
