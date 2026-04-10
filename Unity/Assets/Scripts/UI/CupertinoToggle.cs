using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace EscapeED.UI
{
    [RequireComponent(typeof(Toggle))]
    public class CupertinoToggle : MonoBehaviour
    {
        [Header("Components")]
        public RectTransform knob;
        public Image backgroundImage;
        
        [Header("Settings")]
        public float animationDuration = 0.2f;
        public Color onColor = new Color(0.204f, 0.780f, 0.349f); // #34C759
        public Color offColor = new Color(0.898f, 0.898f, 0.918f); // #E5E5EA
        public float knobPadding = 4f;

        private Toggle toggle;
        private Coroutine animateCoroutine;
        private float offPosX;
        private float onPosX;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            
            // Calculate positions based on background size
            RectTransform bgRect = backgroundImage.rectTransform;
            float halfBg = bgRect.rect.width / 2f;
            float halfKnob = knob.rect.width / 2f;
            
            offPosX = -halfBg + halfKnob + knobPadding;
            onPosX = halfBg - halfKnob - knobPadding;

            // Initial State
            UpdateState(toggle.isOn, false);
            
            toggle.onValueChanged.AddListener((val) => UpdateState(val, true));
        }

        public void UpdateState(bool isOn, bool animate)
        {
            if (animateCoroutine != null) StopCoroutine(animateCoroutine);

            if (animate && gameObject.activeInHierarchy)
            {
                animateCoroutine = StartCoroutine(AnimateToggle(isOn));
            }
            else
            {
                SetStateImmediate(isOn);
            }
        }

        private void SetStateImmediate(bool isOn)
        {
            knob.anchoredPosition = new Vector2(isOn ? onPosX : offPosX, 0);
            backgroundImage.color = isOn ? onColor : offColor;
        }

        private IEnumerator AnimateToggle(bool isOn)
        {
            float elapsed = 0f;
            Vector2 startPos = knob.anchoredPosition;
            Vector2 endPos = new Vector2(isOn ? onPosX : offPosX, 0);
            Color startCol = backgroundImage.color;
            Color endCol = isOn ? onColor : offColor;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                
                // Use a smooth ease-out curve
                float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                knob.anchoredPosition = Vector2.Lerp(startPos, endPos, easedT);
                backgroundImage.color = Color.Lerp(startCol, endCol, easedT);
                
                yield return null;
            }

            knob.anchoredPosition = endPos;
            backgroundImage.color = endCol;
        }
    }
}
