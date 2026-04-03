using UnityEngine;
using System.Collections;

namespace EscapeED.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BaseUIPanel : MonoBehaviour
    {
        [Header("Settings")]
        public GameState targetState;
        public float fadeDuration = 0.3f;

        protected CanvasGroup canvasGroup;
        private Coroutine fadeCoroutine;

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public virtual void Show()
        {
            EnsureInitialized();
            StopFade();
            gameObject.SetActive(true);
            Debug.Log($"[UI] Start showing panel: {gameObject.name}");
            fadeCoroutine = StartCoroutine(Fade(1f, true));
        }

        public virtual void Hide()
        {
            StopFade();
            fadeCoroutine = StartCoroutine(Fade(0f, false));
        }

        private void StopFade()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        }

        private IEnumerator Fade(float targetAlpha, bool show)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            if (!show) gameObject.SetActive(false);
        }
    }
}
