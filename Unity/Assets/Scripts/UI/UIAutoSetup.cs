using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using EscapeED.UI;
using EscapeED.InputHandling;

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

            // 3. SET UP GAME CORE (The Fix!)
            SetupGameCore();

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

            // 5. Create Home Panel (FULL REDESIGN)
            GameObject homeObj = CreatePanel(canvasObj.transform, "HomePanel", new Color(0.94f, 0.95f, 1f, 1f)); // #F0F2FF
            CanvasGroup homeCG = homeObj.AddComponent<CanvasGroup>();
            homeCG.alpha = 0f; 
            
            HomeScreenView homeView = homeObj.AddComponent<HomeScreenView>();
            homeView.targetState = GameState.MainMenu;

            // Build the Redesigned Menu Layout
            CreateHomeLayout(homeObj.transform, homeView);
            
            // 6. FINAL SYNC: Attach listeners now that buttons exist (Critical for device)
            homeView.InitializeSync();

            // Link panels to UIManager & FINAL HANDSHAKE
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

            // 2. Logo (Centered - Upper Middle)
            GameObject logoObj = new GameObject("LogoImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            logoObj.transform.SetParent(splashObj.transform, false);
            var logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.sizeDelta = new Vector2(650, 650); 
            logoRect.anchoredPosition = new Vector2(0, 200); // Moved up to make room for bar
            
            var logoImg = logoObj.GetComponent<RawImage>();
            logoImg.color = Color.white; 
            
            // AUTO-LOGO ASSIGNMENT (Universal: Editor & Runtime)
            string logoResourcePath = "Logo/escape_ed_logo_accurate";
            var logoTex = Resources.Load<Texture2D>(logoResourcePath);
            
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
                float aspect = (float)logoTex.width / logoTex.height;
                if (aspect > 1) logoRect.sizeDelta = new Vector2(650, 650 / aspect);
                else logoRect.sizeDelta = new Vector2(650 * aspect, 650);
            }

            // 3. Loading UI (Bottom)
            // Text: "LOADING"
            GameObject loadingTxtObj = CreateText(splashObj.transform, "LOADING", 35, new Color(0.36f, 0.61f, 1f), new Vector2(0, -600));
            loadingTxtObj.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Bar Track (Perfect Circular Borders)
            Sprite roundedSprite = GetRoundedRectSprite();
            
            GameObject barTrack = new GameObject("ProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barTrack.transform.SetParent(splashObj.transform, false);
            var trackRect = barTrack.GetComponent<RectTransform>();
            trackRect.sizeDelta = new Vector2(600, 45); // Thick bar
            trackRect.anchoredPosition = new Vector2(0, -680);
            
            var trackImg = barTrack.GetComponent<Image>();
            trackImg.sprite = roundedSprite;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey track

            // Bar Fill (Perfect Circular borders)
            GameObject barFillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barFillObj.transform.SetParent(barTrack.transform, false);
            var fillRect = barFillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0.05f); // Tiny offset to keep it inside track
            fillRect.anchorMax = new Vector2(0, 0.95f); 
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.sizeDelta = Vector2.zero; 

            var fillImg = barFillObj.GetComponent<Image>();
            fillImg.sprite = roundedSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0f, 1f, 0f, 1f); // Bright Green

            // Link to SplashView for animation
            splashView.loadingFill = fillRect;

            return splashView;
        }

        private void SetupGameCore()
        {
            Debug.Log("[UIAutoSetup] 🧩 Building Game Core Logic...");
            
            // 1. Scene Cleanup: Remove legacy or duplicate objects that cause interference
            string[] legacyObjects = { "GameManager", "LevelManager", "CubeManager" };
            foreach (var name in legacyObjects)
            {
                var legacy = GameObject.Find(name);
                if (legacy != null) Object.DestroyImmediate(legacy);
            }

            GameObject core = GameObject.Find("GameCore");
            if (core == null) core = new GameObject("GameCore");

            // 2. Attach core components IN ORDER (Dependencies first)
            CubeGrid grid = core.GetComponent<CubeGrid>() ?? core.AddComponent<CubeGrid>();
            CubeNavigator nav = core.GetComponent<CubeNavigator>() ?? core.AddComponent<CubeNavigator>();
            CubeRotator rot = core.GetComponent<CubeRotator>() ?? core.AddComponent<CubeRotator>();
            GhostCubeController ghost = core.GetComponent<GhostCubeController>() ?? core.AddComponent<GhostCubeController>();
            
            // 2b. Runtime Input Mapping (CRITICAL for Dynamic Rebuilds)
            var inputAsset = Resources.Load<InputActionAsset>("Input/InputSystem_Actions");
            if (inputAsset != null)
            {
                var playerMap = inputAsset.FindActionMap("Player");
                if (playerMap != null)
                {
                    var lookAction   = playerMap.FindAction("Look");
                    var pressAction  = playerMap.FindAction("Attack");
                    var zoomAction    = playerMap.FindAction("Zoom");
                    
                    rot.InitializeRuntimeActions(lookAction, pressAction, zoomAction);
                    Debug.Log("[UIAutoSetup] ✅ CubeRotator Input Actions Linked Successfully.");
                }
                else Debug.LogWarning("[UIAutoSetup] FAILED to find 'Player' action map!");
            }
            else Debug.LogWarning("[UIAutoSetup] FAILED to load InputSystem_Actions from Resources/Input/");
            
            // 2c. Input + Interaction System (order matters — Reader → Controller → Interaction)
            InputReader inputReader = core.GetComponent<InputReader>() ?? core.AddComponent<InputReader>();
            InputController inputController = core.GetComponent<InputController>() ?? core.AddComponent<InputController>();
            InteractionSystem interactionSystem = core.GetComponent<InteractionSystem>() ?? core.AddComponent<InteractionSystem>();
            interactionSystem.interactableLayer = LayerMask.GetMask(ArrowConstants.LAYER_ARROW);
            Debug.Log("[UIAutoSetup] ✅ InteractionSystem wired with Arrow layer.");

            // LevelManager depends on Grid and Navigator
            LevelManager lm = core.GetComponent<LevelManager>() ?? core.AddComponent<LevelManager>();

            // Configure Grid
            grid.size = new Vector3Int(3, 3, 3);
            grid.spacing = 1.0f;

            // 3. Link Components MANUALLY (Critical for device timing)
            lm.grid = grid;
            lm.navigator = nav;
            lm.forceWhiteBackground = true;

            // 4. AUTOMATED ASSET LINKING
            lm.levelJsonFile = Resources.Load<TextAsset>("Levels/5x5x5");
            lm.arrowPrefab = Resources.Load<GameObject>("Prefabs/Arrow");

            // Repair missing material on prefab — assigned directly to MeshRenderer, not via LevelManager.
            if (lm.arrowPrefab != null)
            {
                MeshRenderer mr = lm.arrowPrefab.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial == null)
                {
                    Material pulseMat = Resources.Load<Material>("Materials/ArrowPulseMat");
                    if (pulseMat != null)
                    {
                        mr.sharedMaterial = pulseMat;
                        Debug.Log("<color=green>[UIAutoSetup] Assigned ArrowPulseMat to Arrow prefab MeshRenderer.</color>");
                    }
                    else
                        Debug.LogWarning("[UIAutoSetup] ArrowPulseMat not found in Resources/Materials/");
                }
            }

            if (lm.levelJsonFile != null)
                Debug.Log($"<color=green>[UIAutoSetup] SUCCESS: Level JSON Loaded: {lm.levelJsonFile.name}</color>");
            else
                Debug.LogWarning("[UIAutoSetup] FAILED: Default Level JSON (5x5x5) not found in Resources/Levels/");

            if (lm.arrowPrefab == null) Debug.LogWarning("[UIAutoSetup] FAILED: Arrow Prefab not found in Resources/Prefabs/");

            Debug.Log("[UIAutoSetup] ✅ Game Core is synchronized and correctly linked.");
        }

        private void CreateHomeLayout(Transform parent, HomeScreenView view)
        {
            // 1. TOP LEFT BUTTONS
            GameObject settingsObj = CreateIconButton(parent, "SettingsBtn", "UI/settings_icon", new Vector2(-420, 750));
            view.settingsButton = settingsObj.GetComponent<Button>();

            GameObject adsObj = CreateIconButton(parent, "AdsBtn", "UI/ads_icon", new Vector2(-420, 500));
            view.adsButton = adsObj.GetComponent<Button>();

            // 2. CENTER CONTENT - MODERN GLOSSY BUTTON UPGRADE
            CreateText(parent, "Arrows\nCube Escape", 85, new Color(0.2f, 0.2f, 0.2f, 1f), new Vector2(0, 150)).GetComponent<Text>().fontStyle = FontStyle.Bold;
            
            GameObject levelObj = CreateText(parent, "Level 12", 45, new Color(0f, 0.3f, 1f, 1f), new Vector2(0, -20));
            view.levelText = levelObj.GetComponent<Text>();
            view.levelText.fontStyle = FontStyle.Bold;

            // RECONSTRUCT PLAY BUTTON HIERARCHY FOR SOFT SHADOWS
            GameObject playBtnObj = new GameObject("PlayButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Button));
            playBtnObj.layer = 5;
            playBtnObj.transform.SetParent(parent, false);
            var playRect = playBtnObj.GetComponent<RectTransform>();
            playRect.sizeDelta = new Vector2(480, 160);
            playRect.anchoredPosition = new Vector2(0, -400);
            view.playButton = playBtnObj.GetComponent<Button>();

            // Layer 1: Soft Dynamic Shadow
            GameObject shadowObj = new GameObject("ShadowLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowObj.transform.SetParent(playBtnObj.transform, false);
            var shadowRect = shadowObj.GetComponent<RectTransform>();
            shadowRect.anchorMin = Vector2.zero; shadowRect.anchorMax = Vector2.one;
            shadowRect.sizeDelta = new Vector2(60, 60); // Padding for blur
            shadowRect.anchoredPosition = new Vector2(0, -12); // Modern offset
            var shadowImg = shadowObj.GetComponent<Image>();
            shadowImg.sprite = GetSoftShadowSprite(40, 25);
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0, 0, 0, 0.4f);

            // Layer 2: Glossy Face
            GameObject faceObj = new GameObject("FaceLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            faceObj.transform.SetParent(playBtnObj.transform, false);
            var faceRect = faceObj.GetComponent<RectTransform>();
            faceRect.anchorMin = Vector2.zero; faceRect.anchorMax = Vector2.one;
            faceRect.sizeDelta = Vector2.zero;
            var faceImg = faceObj.GetComponent<Image>();
            // HEX: Top #4DB8E8, Bottom #5DDDFF
            faceImg.sprite = GetGlossyButtonSprite(new Color(0.30f, 0.72f, 0.91f), new Color(0.36f, 0.87f, 1.00f));
            faceImg.type = Image.Type.Sliced;
            
            view.playButton.targetGraphic = faceImg;

            // Layer 3: Text
            GameObject playTxtObj = CreateText(playBtnObj.transform, "Play", 70, Color.white, Vector2.zero);
            Text playTxt = playTxtObj.GetComponent<Text>();
            playTxt.fontStyle = FontStyle.Bold;
            var txtShadow = playTxt.gameObject.AddComponent<Shadow>();
            txtShadow.effectColor = new Color(0, 0, 0, 0.2f);
            txtShadow.effectDistance = new Vector2(2, -2);

            // 3. BOTTOM NAVIGATION BAR - PREMIUM 3D NAVIGATION REBUILD
            GameObject navBar = CreatePanel(parent, "BottomNav", Color.white);
            RectTransform navRect = navBar.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0, 0);
            navRect.anchorMax = new Vector2(1, 0.22f); 
            navRect.offsetMin = Vector2.zero;
            navRect.offsetMax = Vector2.zero;

            // COLOR PALETTE
            Color homeTop = new Color(0.83f, 0.77f, 0.98f); // #D4C5F9
            Color homeBottom = new Color(0.91f, 0.88f, 1f); // #E8E0FF
            Color homeAccent = new Color(0.29f, 0.25f, 0.48f); // #4A3F7A (Dark Purple)
            
            Color lockTop = new Color(0.24f, 0.31f, 0.56f); // #3D4F8F
            Color lockBottom = new Color(0.29f, 0.37f, 0.63f); // #4A5FA0

            // --- LOCK 1: SHOP (LEFT) ---
            view.shopButton = Create3DNavIconButton(navBar.transform, "ShopNav", "UI/shop_icon", lockTop, lockBottom, new Vector2(-320, 55), 130);

            // --- HOME BUTTON (CENTER) ---
            view.homeButton = Create3DNavButton(navBar.transform, "HomeNav", "Home", "UI/home_icon", homeTop, homeBottom, homeAccent, new Vector2(0, 55));

            // --- LOCK 2: COLLECTION (RIGHT) ---
            view.collectionButton = Create3DNavIconButton(navBar.transform, "CollectionNav", "UI/shop_icon", lockTop, lockBottom, new Vector2(320, 55), 130);
        }

        private Button Create3DNavButton(Transform parent, string name, string label, string iconPath, Color top, Color bottom, Color accent, Vector2 pos)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Button));
            root.layer = 5; root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 160); // Container
            rect.anchoredPosition = pos;

            // Layer 1: Shadow (Under the pill only)
            GameObject shadow = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadow.transform.SetParent(root.transform, false);
            var sRect = shadow.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(180, 130); sRect.anchoredPosition = new Vector2(0, -8);
            var sImg = shadow.GetComponent<Image>();
            sImg.sprite = GetSoftShadowSprite(40, 20); sImg.type = Image.Type.Sliced;
            sImg.color = new Color(0, 0, 0, 0.35f);

            // Layer 2: Lavender Pill Face (FLAT - NOT 3D GLOSSY)
            GameObject face = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            face.transform.SetParent(root.transform, false);
            var fRect = face.GetComponent<RectTransform>();
            fRect.sizeDelta = new Vector2(140, 95); fRect.anchoredPosition = Vector2.zero;
            var fImg = face.GetComponent<Image>();
            fImg.sprite = GetRoundedRectSprite(); // Use flat sprite
            fImg.type = Image.Type.Sliced;
            fImg.color = top; // Use the primary lavender color
            
            root.GetComponent<Button>().targetGraphic = fImg;

            // Layer 3: Icon (Centered in Pill)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(face.transform, false);
            var iRect = iconObj.GetComponent<RectTransform>();
            iRect.sizeDelta = new Vector2(60, 60); iRect.anchoredPosition = Vector2.zero;
            var iImg = iconObj.GetComponent<Image>();
            Texture2D tex = Resources.Load<Texture2D>(iconPath);
            if (tex) iImg.sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
            iImg.color = accent; // Dark purple house

            // Layer 4: Text (Below Face)
            GameObject txtObj = CreateText(root.transform, label, 32, accent, new Vector2(0, -75));
            txtObj.GetComponent<Text>().fontStyle = FontStyle.Bold;

            return root.GetComponent<Button>();
        }

        private Button Create3DNavIconButton(Transform parent, string name, string iconPath, Color top, Color bottom, Vector2 pos, float size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Button));
            root.layer = 5; root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = pos;

            // Icon Face (STANDALONE with Gradient)
            GameObject iconObj = new GameObject("IconFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(root.transform, false);
            var iRect = iconObj.GetComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one; iRect.sizeDelta = Vector2.zero;
            var iImg = iconObj.GetComponent<Image>();
            
            // Build the styled icon sprite
            Sprite styledSprite = GetStyledIconSprite(iconPath, top, bottom);
            iImg.sprite = styledSprite;
            
            // SHAPE-MATCHED SHADOW (No background boxes/circles)
            // We use the same icon shape but offset and tinted dark to create a true drop shadow
            GameObject shadowObj = new GameObject("ShadowLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowObj.transform.SetParent(iconObj.transform, false);
            shadowObj.transform.SetAsFirstSibling();
            var sRect = shadowObj.GetComponent<RectTransform>();
            sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one;
            sRect.sizeDelta = Vector2.zero;
            sRect.anchoredPosition = new Vector2(0, -8); // Offset
            
            var sImg = shadowObj.GetComponent<Image>();
            sImg.sprite = styledSprite; // Match shape exactly
            sImg.color = new Color(0, 0, 0, 0.4f); // Transparent dark shadow
            
            root.GetComponent<Button>().targetGraphic = iImg;

            return root.GetComponent<Button>();
        }

        private Sprite GetStyledIconSprite(string path, Color top, Color bottom)
        {
            Texture2D src = Resources.Load<Texture2D>(path);
            if (!src) return null;

            // Note: If GetPixels fails (non-readable), we fallback to top color tint
            try {
                Texture2D dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                Color[] pix = src.GetPixels();
                for (int i = 0; i < pix.Length; i++) {
                    int y = i / src.width;
                    float t = (float)y / src.height;
                    pix[i] *= Color.Lerp(bottom, top, t);
                }
                dst.SetPixels(pix); dst.Apply();
                return Sprite.Create(dst, new Rect(0,0,dst.width,dst.height), new Vector2(0.5f,0.5f));
            } catch {
                return Sprite.Create(src, new Rect(0,0,src.width,src.height), new Vector2(0.5f,0.5f));
            }
        }

        private GameObject CreateIconButton(Transform parent, string name, string resourcePath, Vector2 pos)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.layer = 5; // UI Layer
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 120);
            rect.anchoredPosition = pos;

            var img = obj.GetComponent<Image>();
            
            // Try Loading via Resources (Works on Device)
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            
            // FALLBACK TO ASSETDATABASE (Editor Only)
#if UNITY_EDITOR
            if (tex == null)
            {
                string directPath = "Assets/Resources/" + resourcePath + ".png";
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);
            }
            
            if (tex == null) // Second fallback: old location
            {
                string directPath = "Assets/Textures/" + resourcePath + ".png";
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);
            }
#endif

            if (tex != null)
            {
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Grey placeholder
                Debug.LogWarning($"[UIAutoSetup] Failed to load icon at: {resourcePath}");
            }

            return obj;
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

        private void ForceLayerRecursive(Transform trans, int layer)
        {
            trans.gameObject.layer = layer;
            foreach (Transform child in trans)
                ForceLayerRecursive(child, layer);
        }

        private Sprite Get3DBoxSprite(Color baseColor)
        {
            int size = 128;
            int depthHeight = 20; // Side thickness
            float radius = 15.0f; 
            
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            // Color palette derived from base
            Color faceBottom = baseColor;
            Color faceTop    = Color.Lerp(baseColor, Color.white, 0.3f);
            Color sideColor  = Color.Lerp(baseColor, Color.black, 0.25f);
            Color highlight  = Color.white;
            highlight.a = 0.6f;
            Color shadow     = Color.black;
            shadow.a = 0.3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Outer rounded rect check
                    float dx = Mathf.Max(radius - x, 0, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0, y - (size - 1 - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    Color finalCol;

                    if (y < depthHeight)
                    {
                        // THE SIDE (DEPTH)
                        finalCol = sideColor;
                    }
                    else
                    {
                        // THE FACE
                        float faceT = (float)(y - depthHeight) / (size - depthHeight);
                        finalCol = Color.Lerp(faceBottom, faceTop, faceT);

                        // BEVELS / HIGHLIGHTS
                        // Top Shine
                        if (y > size - 8) finalCol = Color.Lerp(finalCol, highlight, 0.5f);
                        // Bottom Face Shadow (Inner bevel)
                        else if (y < depthHeight + 6) finalCol = Color.Lerp(finalCol, shadow, 0.4f);
                        // Side Highlights (Optional)
                        if (x < 6 || x > size - 6) finalCol = Color.Lerp(finalCol, shadow, 0.1f);
                    }

                    // Antialiasing
                    if (dist > radius - 1.0f)
                    {
                        finalCol.a *= (radius - dist);
                    }

                    pixels[y * size + x] = finalCol;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Sliced borders: Left=35, Bottom=45 (covers depth), Right=35, Top=35
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(35, 45, 35, 35));
        }

        private Sprite GetGlossyButtonSprite(Color topCol, Color bottomCol)
        {
            int size = 128;
            float radius = 40.0f; 
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0, y - (size - 1 - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius) { pixels[y * size + x] = Color.clear; continue; }

                    float t = (float)y / size;
                    Color c = Color.Lerp(bottomCol, topCol, t);

                    // High-end gloss highlight at the top
                    if (y > size - 12) c = Color.Lerp(c, Color.white, 0.25f);
                    // Subtle inner rim shadow
                    else if (y < 12) c = Color.Lerp(c, Color.black, 0.1f);
                    
                    pixels[y * size + x] = c;
                    if (dist > radius - 1f) pixels[y * size + x].a *= (radius - dist);
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(50, 50, 50, 50));
        }

        private Sprite GetSoftShadowSprite(float radius, float blur)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            float center = size / 2.0f;
            float innerSize = size - (blur * 2.5f);
            float innerRad = radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - center) - (innerSize/2f - innerRad), 0);
                    float dy = Mathf.Max(Mathf.Abs(y - center) - (innerSize/2f - innerRad), 0);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 0;
                    if (dist < innerRad) alpha = 1f;
                    else if (dist < innerRad + blur) alpha = Mathf.SmoothStep(1f, 0f, (dist - innerRad) / blur);

                    pixels[y * size + x] = new Color(0, 0, 0.15f, alpha); // Black/Dark blue shadow base
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(50, 50, 50, 50));
        }

        private Sprite GetRoundedRectSprite()
        {
            int size = 128;
            float radius = 25.0f; // Subtle roundness (Adjust to user preference)
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to the edge of a rounded rect logic
                    float dx = Mathf.Max(radius - x, 0, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0, y - (size - 1 - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < radius - 1f) pixels[y * size + x] = Color.white;
                    else if (dist < radius) pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - dist));
                    else pixels[y * size + x] = Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            // Sliced with 30px borders ensures corners don't stretch
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(30, 30, 30, 30));
        }
    }
}
