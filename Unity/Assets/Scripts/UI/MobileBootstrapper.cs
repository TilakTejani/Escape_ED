using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using EscapeED.EditorHelper;

namespace EscapeED.UI
{
    /// <summary>
    /// FIERCE MOBILE INITIALIZATION:
    /// This script runs AUTOMATICALLY before any scene loads on iOS.
    /// It ensures the core UI system is alive and the "White Screen" is prevented.
    /// </summary>
    public static class MobileBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ForceMobileStart()
        {
            Debug.Log("[MobileBootstrapper] ATTEMPTING AGGRESSIVE SYSTEM START...");

            // 1. VISUAL CONFIRMATION: Change background from White to Dark Grey
            // If the user sees GREY, our code is alive.
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
                Debug.Log("[MobileBootstrapper] Initial Camera Background set to DARK GREY.");
            }

            // 2. ENSURE GAME STATE MANAGER
            if (GameStateManager.Instance == null)
            {
                GameObject gsmObj = new GameObject("BOOT_GameStateManager", typeof(GameStateManager));
                UnityEngine.Object.DontDestroyOnLoad(gsmObj);
                Debug.Log("[MobileBootstrapper] Auto-Spawned GameStateManager.");
            }

            // 3. ENSURE UI SYSTEM
            UIManager existingUI = UnityEngine.Object.FindAnyObjectByType<UIManager>();
            if (existingUI == null)
            {
                Debug.LogWarning("[MobileBootstrapper] UIManager MISSING at start! This causes the White Screen.");
                // We don't spawn full UI here (too complex for BeforeSceneLoad), 
                // but we flag that we need an immediate setup check.
            }
            
            // 4. ENSURE INPUT SYSTEM COMPATIBILITY
            // Aggressively find and destroy ANY EventSystem that is not our modern one
            EventSystem[] allES = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var esInstance in allES)
            {
                if (esInstance.GetComponent<InputSystemUIInputModule>() == null)
                {
                    Debug.LogWarning($"[MobileBootstrapper] Destroying Legacy EventSystem: {esInstance.name}");
                    UnityEngine.Object.Destroy(esInstance.gameObject);
                }
            }

            EventSystem currentES = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (currentES == null)
            {
                GameObject esObj = new GameObject("BOOT_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                UnityEngine.Object.DontDestroyOnLoad(esObj);
                Debug.Log("[MobileBootstrapper] Auto-Spawned Modern EventSystem.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void ForceAutoBuildUI()
        {
            Debug.Log("[MobileBootstrapper] AFTER_SCENE_LOAD: Checking UI health...");
            
            if (UnityEngine.Object.FindAnyObjectByType<UIManager>() == null)
            {
                Debug.Log("<color=orange>[MobileBootstrapper] UI NOT FOUND! Forcing Premium Runtime Rebuild...</color>");
                
                // Use the UIAutoSetup tool to build the missing UI on-the-fly
                GameObject setupObj = new GameObject("RUNTIME_UISetup", typeof(UIAutoSetup));
                var setup = setupObj.GetComponent<UIAutoSetup>();
                
                // Build it
                setup.SetupUI();
                
                Debug.Log("<color=green>[MobileBootstrapper] UI BUILD COMPLETE.</color>");
                UnityEngine.Object.Destroy(setupObj);
            }
            else
            {
                Debug.Log("[MobileBootstrapper] UIManager confirmed present. UI is stable.");
            }
        }
    }
}
