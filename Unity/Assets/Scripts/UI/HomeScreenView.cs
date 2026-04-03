using UnityEngine;
using UnityEngine.UI;

namespace EscapeED.UI
{
    public class HomeScreenView : BaseUIPanel
    {
        [Header("References")]
        public Button playButton;

        protected override void Awake()
        {
            base.Awake();
            targetState = GameState.MainMenu; 
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            GameStateManager.Instance.UpdateState(GameState.Playing);
        }
    }
}
