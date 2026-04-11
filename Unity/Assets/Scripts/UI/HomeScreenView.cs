using UnityEngine;
using UnityEngine.UI;
using EscapeED.Audio;

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

        private void Start()
        {
            // PROPER RUNTIME WIRING: Standard AddListener is for runtime logic only
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (shopButton != null) shopButton.onClick.AddListener(OnShopClicked);
            if (homeButton != null) homeButton.onClick.AddListener(() => {
                AudioManager.Instance.PlayClick();
                Debug.Log("[HomeScreen] Home Clicked!");
            });
            if (collectionButton != null) collectionButton.onClick.AddListener(() => {
                AudioManager.Instance.PlayClick();
                Debug.Log("[HomeScreen] Collection Clicked!");
            });
        }

        // Keep for assignment, but logic happens in Start
        public void InitializeSync() { }

        private void OnPlayClicked()
        {
            AudioManager.Instance.PlayClick();
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm == null) gsm = Object.FindAnyObjectByType<GameStateManager>();

            if (gsm != null)
                gsm.UpdateState(GameState.Playing);
            else
                Debug.LogError("[HomeScreen] FAILED to transition: GameStateManager missing!");
        }

        private void OnSettingsClicked()
        {
            AudioManager.Instance.PlayClick();
            Debug.Log("[HomeScreen] Settings Clicked!");
            if (settingsView != null)
            {
                settingsView.Show();
            }
        }

        private void OnShopClicked()
        {
            AudioManager.Instance.PlayClick();
            Debug.Log("[HomeScreen] Shop Clicked!");
        }
    }
}
