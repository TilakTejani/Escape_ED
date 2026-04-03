using UnityEngine;
using System.Collections;

namespace EscapeED.UI
{
    public class SplashScreenView : BaseUIPanel
    {
        [Header("Splash Settings")]
        public float splashDuration = 2.5f;

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
            yield return new WaitForSeconds(splashDuration);
            GameStateManager.Instance.UpdateState(GameState.MainMenu);
        }
    }
}
