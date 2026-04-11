using UnityEngine;
using UnityEngine.UI;

namespace EscapeED.UI
{
    /// <summary>
    /// View for the In-Game HUD, containing references to level info, hearts, and buttons.
    /// This is generated procedurally by UIAutoSetup.
    /// </summary>
    public class GameHUDView : BaseUIPanel
    {
        [Header("Informational Display")]
        public Text levelText;
        public Text difficultyText;
        public GameObject[] hearts;

        [Header("Interactive Elements")]
        public Button settingsButton;
        public Button hintButton;

        [Header("Gameplay Stats")]
        public Text arrowCountText;

        protected override void Awake()
        {
            base.Awake();
            targetState = GameState.Playing;
        }

        private void Start()
        {
            // Update initial state from LevelManager if it exists
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (LevelManager.Instance != null)
            {
                if (levelText != null) 
                    levelText.text = "Level " + LevelManager.Instance.CurrentLevel;
                if (arrowCountText != null)
                    arrowCountText.text = LevelManager.Instance.ActiveArrowCount.ToString();
            }
        }

        private void OnEnable()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnArrowCountChanged += OnArrowCountChanged;
            }
        }

        private void OnDisable()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnArrowCountChanged -= OnArrowCountChanged;
            }
        }

        private void OnArrowCountChanged()
        {
            if (arrowCountText != null && LevelManager.Instance != null)
            {
                arrowCountText.text = LevelManager.Instance.ActiveArrowCount.ToString();
            }
        }
    }
}
