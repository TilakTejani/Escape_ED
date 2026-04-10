using UnityEngine;
using UnityEngine.UI;

namespace EscapeED.UI
{
    public class HomeScreenView : BaseUIPanel
    {
        [Header("Main Content")]
        public Button playButton;
        public Text levelText;

        [Header("Utility Buttons")]
        public Button settingsButton;
        public Button adsButton;

        [Header("Bottom Navigation")]
        public Button shopButton;
        public Button homeButton;
        public Button collectionButton;

        [Header("References")]
        public SettingsView settingsView;

        protected override void Awake()
        {
            base.Awake();
            targetState = GameState.MainMenu; 
        }

        // Dedicated late-init method to ensure buttons exist first (called by UIAutoSetup)
        public void InitializeSync()
        {
            if (playButton != null) 
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }
            if (shopButton != null)
            {
                shopButton.onClick.RemoveAllListeners();
                shopButton.onClick.AddListener(OnShopClicked);
            }
        }

        private void OnPlayClicked()
        {
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm == null) gsm = Object.FindAnyObjectByType<GameStateManager>();

            if (gsm != null)
            {
                gsm.UpdateState(GameState.Playing);
            }
            else
            {
                Debug.LogError("[HomeScreen] FAILED to transition: GameStateManager missing!");
            }
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[HomeScreen] Settings Clicked!");
            if (settingsView != null)
            {
                settingsView.Show();
            }
        }

        private void OnShopClicked()
        {
            Debug.Log("[HomeScreen] Shop Clicked!");
        }
    }
}
