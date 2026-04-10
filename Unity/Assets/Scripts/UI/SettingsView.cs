using UnityEngine;
using UnityEngine.UI;

namespace EscapeED.UI
{
    public class SettingsView : BaseUIPanel
    {
        [Header("Controls")]
        public Button closeButton;
        public Toggle soundToggle;
        public Toggle vibeToggle;
        public Button privacyButton;
        public Button purchasesButton;

        protected override void Awake()
        {
            base.Awake();
        }

        public void InitializeSync()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (soundToggle != null)
            {
                soundToggle.onValueChanged.RemoveAllListeners();
                soundToggle.onValueChanged.AddListener(OnSoundChanged);
            }

            if (vibeToggle != null)
            {
                vibeToggle.onValueChanged.RemoveAllListeners();
                vibeToggle.onValueChanged.AddListener(OnVibeChanged);
            }
        }

        private void OnSoundChanged(bool isOn)
        {
            Debug.Log($"[Settings] Sound toggled: {isOn}");
            // Add persistent settings logic here later
        }

        private void OnVibeChanged(bool isOn)
        {
            Debug.Log($"[Settings] Vibe toggled: {isOn}");
            // Add persistent settings logic here later
        }
    }
}
