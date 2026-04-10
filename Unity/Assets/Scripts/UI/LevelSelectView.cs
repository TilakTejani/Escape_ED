using UnityEngine;
using UnityEngine.UI;

namespace EscapeED.UI
{
    /// <summary>
    /// Level selection screen — dynamically builds one button per level found in Resources/Levels/.
    /// Each button shows the level name and loads that level when tapped.
    ///
    /// Scene setup:
    ///   1. Create a UI Panel GameObject, add CanvasGroup + this script.
    ///   2. Set targetState = LevelSelect in Inspector.
    ///   3. Add a child ScrollView with a GridLayoutGroup inside its Content.
    ///   4. Assign the Content RectTransform to "gridContainer".
    ///   5. Assign a Button prefab (with a Text child) to "levelButtonPrefab".
    ///   6. Assign a Back Button to "backButton".
    /// </summary>
    public class LevelSelectView : BaseUIPanel
    {
        [Header("Level Grid")]
        [Tooltip("Content RectTransform of a ScrollView with GridLayoutGroup.")]
        public RectTransform gridContainer;

        [Tooltip("Button prefab — must have a Text (or TMP_Text) child for the label.")]
        public GameObject levelButtonPrefab;

        [Header("Navigation")]
        public Button backButton;

        private LevelManager levelManager;

        protected override void Awake()
        {
            base.Awake();
            targetState = GameState.LevelSelect;
        }

        public override void Show()
        {
            base.Show();
            BuildGrid();
        }

        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void BuildGrid()
        {
            if (gridContainer == null || levelButtonPrefab == null)
            {
                Debug.LogError("[LevelSelectView] gridContainer or levelButtonPrefab not assigned.");
                return;
            }

            // Clear old buttons
            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);

            // Find LevelManager
            if (levelManager == null)
                levelManager = Object.FindAnyObjectByType<LevelManager>();

            if (levelManager == null)
            {
                Debug.LogError("[LevelSelectView] LevelManager not found in scene.");
                return;
            }

            int count = levelManager.LevelCount;
            if (count == 0)
            {
                Debug.LogWarning("[LevelSelectView] No levels found.");
                return;
            }

            // Load all level assets to get names
            TextAsset[] levels = Resources.LoadAll<TextAsset>("Levels");

            for (int i = 0; i < levels.Length; i++)
            {
                int capturedIndex = i;
                TextAsset asset = levels[i];

                GameObject btn = Instantiate(levelButtonPrefab, gridContainer);

                // Set label — supports both legacy Text and TMP
                SetButtonLabel(btn, asset.name);

                // Wire click
                Button button = btn.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => OnLevelSelected(capturedIndex));
            }
        }

        private void SetButtonLabel(GameObject btn, string label)
        {
            // Try legacy Text first
            Text legacyText = btn.GetComponentInChildren<Text>();
            if (legacyText != null) { legacyText.text = label; return; }

            // Try TMPro
            var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null) { tmp.text = label; return; }
        }

        private void OnLevelSelected(int index)
        {
            if (levelManager == null)
                levelManager = Object.FindAnyObjectByType<LevelManager>();

            if (levelManager != null)
                levelManager.LoadLevelByIndex(index);

            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.UpdateState(GameState.Playing);
        }

        private void OnBackClicked()
        {
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.UpdateState(GameState.MainMenu);
        }
    }
}
