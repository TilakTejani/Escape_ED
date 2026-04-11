using UnityEngine;

namespace EscapeED.Audio
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AudioSource _uiSource;
        private AudioClip _clickClip;
        private AudioClip _arrowClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Setup Audio Source for lowest latency
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0; // Pure 2D
            _uiSource.priority = 0;     // Highest Priority
            _uiSource.bypassEffects = true;
            _uiSource.bypassListenerEffects = true;
            _uiSource.ignoreListenerPause = true; // Still play menu clicks if game paused

            // Preload assets
            LoadAssets();
        }

        private void LoadAssets()
        {
            // We search for ui_click in Resources/Audio/
            _clickClip = Resources.Load<AudioClip>("Audio/ui_click");
            
            if (_clickClip == null)
            {
                Debug.LogWarning("[AudioManager] No 'ui_click' clip found in Resources/Audio/. Please add one for button sounds.");
            }

            _arrowClip = Resources.Load<AudioClip>("Audio/arrow");
            if (_arrowClip == null)
            {
                Debug.LogWarning("[AudioManager] No 'arrow' clip found in Resources/Audio/. Please add one for arrow gameplay sounds.");
            }
        }

        public void PlayClick()
        {
            if (_uiSource == null) return;

            if (_clickClip == null)
            {
                Debug.LogWarning("[AudioManager] Cannot play click: Clip is missing. Check Assets/Resources/Audio/ui_click");
                return;
            }

            // Check if settings allow sound
            bool soundEnabled = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
            if (!soundEnabled) return;

            // Final check: Is there a listener?
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                Debug.LogError("[AudioManager] CRITICAL: No AudioListener found in scene. Sound will be silent!");
            }

            _uiSource.PlayOneShot(_clickClip);
        }

        public void PlayArrowSound()
        {
            if (_uiSource == null) return;

            if (_arrowClip == null)
            {
                Debug.LogWarning("[AudioManager] Cannot play arrow sound: Clip is missing. Check Assets/Resources/Audio/arrow");
                // Fallback to click if arrow is missing? User specifically asked for arrow, so we warn.
                return;
            }

            bool soundEnabled = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
            if (!soundEnabled) return;

            _uiSource.PlayOneShot(_arrowClip);
        }

        // Method to reload assets if they were added later
        public void Reload() => LoadAssets();
    }
}
