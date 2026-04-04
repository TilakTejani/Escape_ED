using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI; 
using EscapeED.UI;

namespace EscapeED.EditorHelper
{
    public class UIAutoSetup : MonoBehaviour
    {
        [Header("Setup Configuration")]
        public Vector2 referenceResolution = new Vector2(1080, 1920);

#if UNITY_EDITOR
        [UnityEditor.MenuItem("EscapeED/Full UI Setup")]
        public static void ForceSetupFromMenu()
        {
            var setup = Object.FindAnyObjectByType<UIAutoSetup>();
            if (setup == null)
            {
                GameObject g = new GameObject("_UI_Setup_System", typeof(UIAutoSetup));
                setup = g.GetComponent<UIAutoSetup>();
            }
            setup.SetupUI();
            
            // Set Bundle Identifier for iOS
            UnityEditor.PlayerSettings.SetApplicationIdentifier(UnityEditor.BuildTargetGroup.iOS, "com.axiallabs.escape-3d");
            Debug.Log("<color=cyan>[UIAutoSetup] Bundle ID updated to: com.axiallabs.escape-3d</color>");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[UIAutoSetup] PREMIUM UI REBUILD COMPLETE!</color>");
        }
#endif

        [ContextMenu("Setup EscapeED UI")]
        public void SetupUI()
        {
            Debug.Log("[UIAutoSetup] 🚨 BEGINNING NUCLEAR PREMIUM UI REBUILD...");
            
            // 1. NUCLEAR WIPE: Destroy ALL existing Canvas and EventSystem objects
            var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allCanvases) 
            {
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            var allEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var evSystem in allEventSystems)
            {
                if (Application.isPlaying) Destroy(evSystem.gameObject);
                else DestroyImmediate(evSystem.gameObject);
            }

            Debug.Log("[UIAutoSetup] 🧹 Scene Cleaned. Starting Deep System Optimization...");

            // 2. Kill the White Screen: Force Camera Layers
            FixCameras();

            // Re-create GameStateManager if missing or broken
            GameStateManager existingGSM = Object.FindAnyObjectByType<GameStateManager>();
            if (existingGSM == null)
            {
                GameObject gsmObj = new GameObject("GameStateManager");
                gsmObj.AddComponent<GameStateManager>();
                Debug.Log("[UIAutoSetup] Created fresh GameStateManager.");
            }

            // Cleanup existing MainUI
            GameObject oldCanvas = GameObject.Find("MainUI");
            if (oldCanvas != null) 
            {
                if (Application.isPlaying) Destroy(oldCanvas);
                else DestroyImmediate(oldCanvas);
            }
            
            // 3. Create NEW EventSystem with correct InputSystemUIInputModule
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.layer = 5; // UI Layer
            Debug.Log("[UIAutoSetup] Created clean EventSystem with InputSystemUIInputModule");

            // 4. Create Main Canvas
            GameObject canvasObj = new GameObject("MainUI");
            canvasObj.layer = 5; // UI Layer
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            UIManager uiManager = canvasObj.AddComponent<UIManager>();
            
            // 4. Create Splash Panel
            SplashScreenView splashView = CreateSplashPanel(canvasObj.transform);
            splashView.targetState = GameState.Init;

            // 5. Create Home Panel
            GameObject homeObj = CreatePanel(canvasObj.transform, "HomePanel", new Color(0.05f, 0.05f, 0.05f, 1f));
            CanvasGroup homeCG = homeObj.AddComponent<CanvasGroup>();
            homeCG.alpha = 0f; 
            
            HomeScreenView homeView = homeObj.AddComponent<HomeScreenView>();
            homeView.targetState = GameState.MainMenu;

            CreateText(homeObj.transform, "MAIN MENU", 60, Color.white, new Vector2(0, 400));

            GameObject btnObj = CreateButton(homeObj.transform, "PlayButton", "PLAY GAME", new Vector2(0, 0));
            homeView.playButton = btnObj.GetComponent<Button>();
            homeView.playButton.onClick.AddListener(() => {
                Debug.Log("[UIAutoSetup] Play Button Clicked! Transitioning to PLAYING state.");
                GameStateManager.Instance.UpdateState(GameState.Playing);
            });

            // 6. Link panels to UIManager & FINAL HANDSHAKE
            BaseUIPanel[] allPanels = new BaseUIPanel[] { splashView, homeView };
            uiManager.Initialize(allPanels);

            Debug.Log("[UIAutoSetup] SUCCESS: Handshake complete. UI is now synchronized.");
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.layer = 5; // UI Layer
            obj.transform.SetParent(parent, false);
            
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private SplashScreenView CreateSplashPanel(Transform parent)
        {
            GameObject splashObj = new GameObject("SplashPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            splashObj.transform.SetParent(parent, false);
            var rect = splashObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            SplashScreenView splashView = splashObj.AddComponent<SplashScreenView>();

            // 1. Clean Light Grey Background (Premium Feel)
            var bgImage = splashObj.GetComponent<Image>();
            bgImage.color = new Color(0.94f, 0.94f, 0.94f, 1f); 

            // 2. Logo (Centered - Using RawImage for Device Compatibility)
            GameObject logoObj = new GameObject("LogoImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            logoObj.transform.SetParent(splashObj.transform, false);
            var logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.sizeDelta = new Vector2(650, 650); // Larger logo
            logoRect.anchoredPosition = Vector2.zero; // Center it
            
            var logoImg = logoObj.GetComponent<RawImage>();
            logoImg.color = Color.white; 
            
            // AUTO-LOGO ASSIGNMENT (Universal: Editor & Runtime)
            string logoResourcePath = "Logo/escape_ed_logo_accurate";
            
            // Load as Texture2D (Works on Device via Resources)
            var logoTex = Resources.Load<Texture2D>(logoResourcePath);
            
            // FALLBACK (Editor Only): In case it's not in Resources yet during development
#if UNITY_EDITOR
            if (logoTex == null)
            {
                string directPath = "Assets/Textures/Logo/escape_ed_logo_accurate.png";
                string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                string fullPath = System.IO.Path.Combine(projectRoot, directPath);
                
                if (System.IO.File.Exists(fullPath))
                    logoTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);
            }
#endif

            if (logoTex != null)
            {
                logoImg.texture = logoTex;
                
                // Maintain aspect ratio manually for RawImage
                float aspect = (float)logoTex.width / logoTex.height;
                if (aspect > 1) logoRect.sizeDelta = new Vector2(650, 650 / aspect);
                else logoRect.sizeDelta = new Vector2(650 * aspect, 650);

                logoImg.color = Color.white; 
                Debug.Log($"<color=green>[UIAutoSetup] Successfully assigned Logo Texture ({logoTex.name}) to RawImage.</color>");
            }
            else
            {
                Debug.LogWarning($"[UIAutoSetup] Logo '{logoResourcePath}' missing from Resources! Please ensure it exists at Assets/Resources/{logoResourcePath}.png");
            }

            return splashView;
        }

        private GameObject CreateText(Transform parent, string content, int fontSize, Color color, Vector2 anchoredPos)
        {
            GameObject obj = new GameObject("Text_" + content, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.layer = 5; // UI Layer
            obj.transform.SetParent(parent, false);
            
            Text t = obj.GetComponent<Text>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(800, 200);
            rect.anchoredPosition = anchoredPos;

            return obj;
        }

        private GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.layer = 5; // UI Layer
            btnObj.transform.SetParent(parent, false);
            
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500, 150);
            rect.anchoredPosition = anchoredPos;

            btnObj.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.85f);

            CreateText(btnObj.transform, label, 45, Color.white, Vector2.zero);

            return btnObj;
        }

        private static void FixCameras()
        {
            foreach (Camera cam in Camera.allCameras)
            {
                // Ensure UI layer (5) is visible in ALL cameras
                int uiLayer = LayerMask.NameToLayer("UI");
                cam.cullingMask |= (1 << uiLayer);
                
                Debug.Log($"[UIAutoSetup] Camera '{cam.name}' verified for UI rendering.");
            }
        }

        private static void ForceLayerRecursive(Transform trans, int layer)
        {
            trans.gameObject.layer = layer;
            foreach (Transform child in trans)
                ForceLayerRecursive(child, layer);
        }
    }
}
