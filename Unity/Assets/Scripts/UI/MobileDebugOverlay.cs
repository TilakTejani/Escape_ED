using UnityEngine;

namespace EscapeED.UI
{
    /// <summary>
    /// A temporary diagnostic tool that shows critical errors directly on the real device screen.
    /// This helps troubleshoot the "White Screen" without needing a laptop/Xcode console.
    /// </summary>
    public class MobileDebugOverlay : MonoBehaviour
    {
        private string lastErrorMessage = "";
        
        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                if (logString.Contains("[UIManager]") || logString.Contains("[GameState]"))
                {
                    lastErrorMessage = logString;
                }
            }
        }

        private void OnGUI()
        {
            // Always show status in a small box at the top
            GUIStyle style = new GUIStyle();
            style.fontSize = 30;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.UpperLeft;

            string status = "[DIAGNOSTIC]\n";
            if (UIManager.Instance != null && UIManager.Instance.panels != null)
                status += $"Panels: {UIManager.Instance.panels.Length}\n";
            else
                status += "Panels: MISSING\n";

            if (!string.IsNullOrEmpty(lastErrorMessage))
            {
                style.normal.textColor = Color.red;
                status += "ERROR: " + lastErrorMessage;
            }

            GUI.Box(new Rect(10, 10, 600, 200), status, style);
        }
    }
}
