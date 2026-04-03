using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI; // Required for the new Input System module
using EscapeED.UI;

namespace EscapeED.EditorHelper
{
    public class UIAutoSetup : MonoBehaviour
    {
        [Header("Setup Configuration")]
        public Vector2 referenceResolution = new Vector2(1080, 1920);

        [ContextMenu("Setup EscapeED UI")]
        public void SetupUI()
        {
            // 1. Cleanup existing objects to avoid duplicates and legacy crashes
            GameObject oldCanvas = GameObject.Find("MainUI");
            if (oldCanvas != null) DestroyImmediate(oldCanvas);
            
            GameObject oldES = GameObject.Find("EventSystem");
            if (oldES != null) DestroyImmediate(oldES);

            // 2. Create EventSystem with correct New Input System Module
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[UIAutoSetup] Created EventSystem with InputSystemUIInputModule");

            // 3. Create Main Canvas
            GameObject canvasObj = new GameObject("MainUI");
            canvasObj.layer = LayerMask.NameToLayer("UI");
            
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
            GameObject splashObj = CreatePanel(canvasObj.transform, "SplashPanel", Color.black);
            CanvasGroup splashCG = splashObj.AddComponent<CanvasGroup>();
            splashCG.alpha = 1f;
            
            SplashScreenView splashView = splashObj.AddComponent<SplashScreenView>();
            splashView.targetState = GameState.Init;

            CreateText(splashObj.transform, "ESCAPE-ED", 80, Color.white, new Vector2(0, 100));

            // 5. Create Home Panel
            GameObject homeObj = CreatePanel(canvasObj.transform, "HomePanel", new Color(0.05f, 0.05f, 0.05f, 1f));
            CanvasGroup homeCG = homeObj.AddComponent<CanvasGroup>();
            homeCG.alpha = 0f; 
            
            HomeScreenView homeView = homeObj.AddComponent<HomeScreenView>();
            homeView.targetState = GameState.MainMenu;

            CreateText(homeObj.transform, "MAIN MENU", 60, Color.white, new Vector2(0, 400));

            GameObject btnObj = CreateButton(homeObj.transform, "PlayButton", "PLAY GAME", new Vector2(0, 0));
            homeView.playButton = btnObj.GetComponent<Button>();

            // 6. Finalize References
            uiManager.panels = new BaseUIPanel[] { splashView, homeView };

            Debug.Log("[UIAutoSetup] SUCCESS: UI Structure rebuilt with New Input System compatibility.");
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private GameObject CreateText(Transform parent, string content, int fontSize, Color color, Vector2 anchoredPos)
        {
            GameObject obj = new GameObject("Text_" + content, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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
            btnObj.transform.SetParent(parent, false);
            
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500, 150);
            rect.anchoredPosition = anchoredPos;

            btnObj.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.85f);

            CreateText(btnObj.transform, label, 45, Color.white, Vector2.zero);

            return btnObj;
        }
    }
}
