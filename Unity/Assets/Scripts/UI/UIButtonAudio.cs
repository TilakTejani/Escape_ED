using UnityEngine;
using UnityEngine.UI;
using EscapeED.Audio;

namespace EscapeED.UI
{
    [RequireComponent(typeof(Button))]
    public class UIButtonAudio : MonoBehaviour
    {
        private void Awake()
        {
            Button btn = GetComponent<Button>();
            btn.onClick.AddListener(PlaySound);
        }

        private void PlaySound()
        {
            AudioManager.Instance.PlayClick();
        }
    }
}
