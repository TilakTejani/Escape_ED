using UnityEngine;
using UnityEngine.UI;
using EscapeED.Audio;

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

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => {
                    AudioManager.Instance.PlayClick();
                    Hide();
                });
            }

            if (soundToggle != null)
            {
                soundToggle.onValueChanged.AddListener(OnSoundChanged);
            }

            if (vibeToggle != null)
            {
                vibeToggle.onValueChanged.AddListener(OnVibeChanged);
            }
        }

        // Keep for assignment, logic is in Start
        public void InitializeSync() { }

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
