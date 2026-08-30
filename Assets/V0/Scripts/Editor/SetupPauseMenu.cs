using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using V0.UI;

namespace V0.Editor
{
    public static class SetupPauseMenu
    {
        [MenuItem("Tools/Setup Pause Menu (GameScene)", false, 50)]
        [MenuItem("Tools/Trust No One/Setup Pause Menu (GameScene)", false, 50)]
        public static void BakePauseMenu()
        {
            string scenePath = "Assets/V0/Scene/GameScene.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");

            // 1. Ensure EventSystem exists and uses InputSystemUIInputModule
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                StandaloneInputModule oldMod = es.GetComponent<StandaloneInputModule>();
                if (oldMod != null) Object.DestroyImmediate(oldMod);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                {
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                }
#endif
            }

            // 2. Locate or Create [PauseManager]
            GameObject host = GameObject.Find("[PauseManager]");
            if (host == null)
            {
                host = new GameObject("[PauseManager]");
                Undo.RegisterCreatedObjectUndo(host, "Create [PauseManager]");
            }

            PauseManager pauseMgr = host.GetComponent<PauseManager>();
            if (pauseMgr == null) pauseMgr = host.AddComponent<PauseManager>();

            // Clean old child pause canvas under host
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
            }

            // 3. Pre-bake Pause Canvas
            GameObject canvasObj = new GameObject("PauseCanvas");
            canvasObj.transform.SetParent(host.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup pauseCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.blocksRaycasts = true;
            pauseCanvasGroup.interactable = true;

            // Fullscreen Backdrop Dimmer
            GameObject bgDimObj = new GameObject("BackdropDimmer");
            bgDimObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgDimRect = bgDimObj.AddComponent<RectTransform>();
            bgDimRect.anchorMin = Vector2.zero;
            bgDimRect.anchorMax = Vector2.one;
            bgDimRect.sizeDelta = Vector2.zero;
            Image bgDimImg = bgDimObj.AddComponent<Image>();
            bgDimImg.color = new Color(0.04f, 0.04f, 0.06f, 0.82f);
            bgDimImg.raycastTarget = false;

            // Main Pause Panel
            GameObject mainPanelObj = new GameObject("MainPausePanel");
            mainPanelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform mainRect = mainPanelObj.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.sizeDelta = Vector2.zero;

            // Title: "PAUSED"
            GameObject titleObj = new GameObject("PauseTitle");
            titleObj.transform.SetParent(mainPanelObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.76f);
            titleRect.anchorMax = new Vector2(0.5f, 0.76f);
            titleRect.sizeDelta = new Vector2(600, 100);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 58;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            titleText.text = "PAUSED";
            titleText.raycastTarget = false;

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);

            // Buttons Container
            GameObject btnContainer = new GameObject("ButtonsContainer");
            btnContainer.transform.SetParent(mainPanelObj.transform, false);
            RectTransform contRect = btnContainer.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.44f);
            contRect.anchorMax = new Vector2(0.5f, 0.44f);
            contRect.sizeDelta = new Vector2(320, 240);

            // Button 1: RESUME
            Button resumeBtn = CreateButton(btnContainer.transform, "ResumeButton", "RESUME", new Vector2(0, 70), font);

            // Button 2: CONTROLS
            Button controlsBtn = CreateButton(btnContainer.transform, "ControlsButton", "CONTROLS", new Vector2(0, 0), font);

            // Button 3: MAIN MENU
            Button mainMenuBtn = CreateButton(btnContainer.transform, "MainMenuButton", "MAIN MENU", new Vector2(0, -70), font);

            // Controls Modal Panel
            GameObject modalObj = new GameObject("ControlsModalPanel");
            modalObj.transform.SetParent(canvasObj.transform, false);
            RectTransform modalRect = modalObj.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;
            CanvasGroup controlsGroup = modalObj.AddComponent<CanvasGroup>();
            controlsGroup.alpha = 0f;

            Image dimmerImg = modalObj.AddComponent<Image>();
            dimmerImg.color = new Color(0f, 0f, 0f, 0.8f);
            dimmerImg.raycastTarget = true;

            GameObject boxObj = new GameObject("ModalBox");
            boxObj.transform.SetParent(modalObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(720, 520);
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.15f, 0.98f);

            GameObject modalTitleObj = new GameObject("ModalTitle");
            modalTitleObj.transform.SetParent(boxObj.transform, false);
            RectTransform modalTitleRect = modalTitleObj.AddComponent<RectTransform>();
            modalTitleRect.anchorMin = new Vector2(0.5f, 0.90f);
            modalTitleRect.anchorMax = new Vector2(0.5f, 0.90f);
            modalTitleRect.sizeDelta = new Vector2(600, 50);
            Text modalTitleText = modalTitleObj.AddComponent<Text>();
            modalTitleText.font = font;
            modalTitleText.fontSize = 32;
            modalTitleText.fontStyle = FontStyle.Bold;
            modalTitleText.alignment = TextAnchor.MiddleCenter;
            modalTitleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            modalTitleText.text = "GAME CONTROLS";
            modalTitleText.raycastTarget = false;

            GameObject contentObj = new GameObject("ControlsContent");
            contentObj.transform.SetParent(boxObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.52f);
            contentRect.anchorMax = new Vector2(0.5f, 0.52f);
            contentRect.sizeDelta = new Vector2(620, 300);
            Text contentText = contentObj.AddComponent<Text>();
            contentText.font = font;
            contentText.fontSize = 20;
            contentText.alignment = TextAnchor.MiddleLeft;
            contentText.lineSpacing = 1.35f;
            contentText.color = new Color(0.92f, 0.92f, 0.94f, 1f);
            contentText.text = 
                "<b>W, A, S, D</b>  —  Move\n" +
                "<b>Mouse</b>  —  Look Around\n" +
                "<b>Left Shift</b>  —  Sprint  <i><color=#FF7070>(Warning: Ghost hears running!)</color></i>\n" +
                "<b>C / Left Ctrl</b>  —  Crouch  <i><color=#70FF90>(Silent Stealth)</color></i>\n" +
                "<b>E / Left Click</b>  —  Interact & Pick Up Keys\n" +
                "<b>F</b>  —  Toggle Flashlight\n" +
                "<b>Escape</b>  —  Pause / Unpause Game\n" +
                "<b>Space</b>  —  Skip Cutscene";
            contentText.raycastTarget = false;

            Button closeBtn = CreateButton(boxObj.transform, "CloseControlsButton", "BACK", Vector2.zero, font);
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0.12f);
            closeRect.anchorMax = new Vector2(0.5f, 0.12f);
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = new Vector2(220, 50);

            modalObj.SetActive(false);
            canvasObj.SetActive(false);

            // 4. Wire serialized properties directly onto PauseManager in scene
            SerializedObject so = new SerializedObject(pauseMgr);
            so.FindProperty("_pauseCanvasGroup").objectReferenceValue = pauseCanvasGroup;
            so.FindProperty("_mainPausePanel").objectReferenceValue = mainPanelObj;
            so.FindProperty("_controlsModalPanel").objectReferenceValue = modalObj;
            so.FindProperty("_controlsCanvasGroup").objectReferenceValue = controlsGroup;
            so.FindProperty("_resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("_controlsButton").objectReferenceValue = controlsBtn;
            so.FindProperty("_mainMenuButton").objectReferenceValue = mainMenuBtn;
            so.FindProperty("_closeControlsButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(pauseMgr);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green><b>[SetupPauseMenu]</b></color> Successfully pre-baked Pause Menu into GameScene at compile time!");
            EditorUtility.DisplayDialog("Pause Menu Ready",
                "Successfully pre-baked Pause Menu into GameScene!\n\n" +
                "• 0 Runtime Allocations / GC Spikes.\n" +
                "• Three buttons: RESUME, CONTROLS, MAIN MENU.\n" +
                "• Pressing Escape toggles pause and opens/closes menu.\n" +
                "• Controls modal popup configured.",
                "OK");
        }

        private static Button CreateButton(Transform parent, string goName, string buttonLabel, Vector2 anchoredPos, Font font)
        {
            GameObject btnObj = new GameObject(goName);
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = anchoredPos;
            btnRect.sizeDelta = new Vector2(280, 58);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            btnImg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            colors.highlightedColor = new Color(0.38f, 0.44f, 0.60f, 1.0f);
            colors.pressedColor = new Color(0.08f, 0.09f, 0.12f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            btn.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text txt = textObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = buttonLabel;
            txt.raycastTarget = true;

            return btn;
        }
    }
}
