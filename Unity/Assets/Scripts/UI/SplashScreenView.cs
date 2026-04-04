using UnityEngine;
using System.Collections;

namespace EscapeED.UI
{
    public class SplashScreenView : BaseUIPanel
    {
        [Header("Splash Settings")]
        public float splashDuration = 2.5f;

        [Header("References")]
        public RectTransform loadingFill;

        protected override void Awake()
        {
            base.Awake();
            targetState = GameState.Init; // Built-in for Splash
        }

        public override void Show()
        {
            base.Show();
            StartCoroutine(TimedTransition());
        }

        private IEnumerator TimedTransition()
        {
            float elapsed = 0f;
            while (elapsed < splashDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.2f); // Short buffer

            // Resilient Transition: Find manager if Instance is lost
            GameStateManager manager = GameStateManager.Instance;
            if (manager == null)
            {
                manager = Object.FindAnyObjectByType<GameStateManager>();
            }

            if (manager != null)
            {
                manager.UpdateState(GameState.MainMenu);
            }
            else
            {
                Debug.LogError("[SplashScreen] CRITICAL: GameStateManager missing. Transition failed.");
                // Attempt direct UI switch if Manager is truly dead
                if (UIManager.Instance != null)
                {
                    GameStateManager.Instance.UpdateState(GameState.MainMenu); // Force try
                }
            }
        }
    }
}
