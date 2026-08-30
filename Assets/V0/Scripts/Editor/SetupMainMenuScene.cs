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
    public static class SetupMainMenuScene
    {
        [MenuItem("Tools/Setup Main Menu Scene", false, 80)]
        [MenuItem("Tools/Trust No One/Setup Main Menu Scene", false, 80)]
        public static void BakeMainMenu()
        {
            string scenePath = "Assets/V0/Scene/MainMenu.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/V0/Images/BackGround_Img.png");
            AudioClip ambientClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/OutsideSound.mp3");

            // 1. Set Camera background to solid black
            Camera cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                EditorUtility.SetDirty(cam);
            }

            // 2. EventSystem with InputSystemUIInputModule
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

            // 3. MainMenuManager Host
            GameObject host = GameObject.Find("MainMenuManager");
            if (host == null)
            {
                host = new GameObject("MainMenuManager");
                Undo.RegisterCreatedObjectUndo(host, "Create MainMenuManager");
            }

            MainMenuManager mgr = host.GetComponent<MainMenuManager>();
            if (mgr == null) mgr = host.AddComponent<MainMenuManager>();

            // Clean old canvas children under host and any old MainMenuCanvas in scene
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
            }

            GameObject oldCanvas = GameObject.Find("MainMenuCanvas");
            if (oldCanvas != null && oldCanvas != host)
            {
                Object.DestroyImmediate(oldCanvas);
            }

            // AudioSource
            AudioSource audioSource = host.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = host.AddComponent<AudioSource>();
            audioSource.clip = ambientClip;
            audioSource.loop = true;
            audioSource.volume = 0.45f;
            audioSource.playOnAwake = true;
            audioSource.spatialBlend = 0f;

            // 4. Main Canvas (Parented to MainMenuManager)
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.transform.SetParent(host.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup menuCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            menuCanvasGroup.blocksRaycasts = true;
            menuCanvasGroup.interactable = true;

            // Background Image (BackGround_Img.png)
            GameObject bgObj = new GameObject("BackgroundImage");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            if (bgSprite != null) bgImg.sprite = bgSprite;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Atmosphere Dark Vignette Overlay
            GameObject overlayObj = new GameObject("AtmosphereOverlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            Image overlayImg = overlayObj.AddComponent<Image>();
            overlayImg.color = new Color(0.04f, 0.05f, 0.08f, 0.45f);
            overlayImg.raycastTarget = false;

            // Game Title
            GameObject titleObj = new GameObject("GameTitle");
            titleObj.transform.SetParent(canvasObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.76f);
            titleRect.anchorMax = new Vector2(0.5f, 0.76f);
            titleRect.sizeDelta = new Vector2(900, 120);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 68;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.96f, 0.94f, 0.90f, 1f);
            titleText.text = "TRUST NO ONE";
            titleText.raycastTarget = false;

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);

            // Sub-tagline
            GameObject tagObj = new GameObject("GameTagline");
            tagObj.transform.SetParent(canvasObj.transform, false);
            RectTransform tagRect = tagObj.AddComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(0.5f, 0.69f);
            tagRect.anchorMax = new Vector2(0.5f, 0.69f);
            tagRect.sizeDelta = new Vector2(600, 40);
            Text tagText = tagObj.AddComponent<Text>();
            tagText.font = font;
            tagText.fontSize = 20;
            tagText.fontStyle = FontStyle.Bold;
            tagText.alignment = TextAnchor.MiddleCenter;
            tagText.color = new Color(0.85f, 0.35f, 0.35f, 0.9f);
            tagText.text = "• SURVIVAL HORROR •";
            tagText.raycastTarget = false;

            // Buttons Container
            GameObject btnContainer = new GameObject("ButtonsContainer");
            btnContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform contRect = btnContainer.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.38f);
            contRect.anchorMax = new Vector2(0.5f, 0.38f);
            contRect.sizeDelta = new Vector2(320, 200);

            // PLAY Button
            Button playBtn = CreateButton(btnContainer.transform, "PlayButton", "PLAY", new Vector2(0, 45), font);

            // CONTROLS Button
            Button controlsBtn = CreateButton(btnContainer.transform, "ControlsButton", "CONTROLS", new Vector2(0, -45), font);

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
            dimmerImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimmerImg.raycastTarget = true;

            GameObject boxObj = new GameObject("ModalBox");
            boxObj.transform.SetParent(modalObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(720, 520);
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.11f, 0.15f, 0.95f);

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
                "<b>Escape</b>  —  Back / Pause";
            contentText.raycastTarget = false;

            Button closeBtn = CreateButton(boxObj.transform, "CloseButton", "BACK", Vector2.zero, font);
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0.12f);
            closeRect.anchorMax = new Vector2(0.5f, 0.12f);
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = new Vector2(220, 50);

            modalObj.SetActive(false);

            // 5. Serialize fields directly onto MainMenuManager in scene
            SerializedObject so = new SerializedObject(mgr);
            so.FindProperty("_menuCanvasGroup").objectReferenceValue = menuCanvasGroup;
            so.FindProperty("_playButton").objectReferenceValue = playBtn;
            so.FindProperty("_controlsButton").objectReferenceValue = controlsBtn;
            so.FindProperty("_controlsModalObj").objectReferenceValue = modalObj;
            so.FindProperty("_controlsCanvasGroup").objectReferenceValue = controlsGroup;
            so.FindProperty("_closeControlsButton").objectReferenceValue = closeBtn;
            so.FindProperty("_audioSource").objectReferenceValue = audioSource;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(mgr);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green><b>[SetupMainMenuScene]</b></color> Successfully configured MainMenu scene with fully wired buttons!");
            EditorUtility.DisplayDialog("Main Menu Ready",
                "Successfully configured MainMenu scene!\n\n" +
                "1. PLAY button and CONTROLS button are wired directly.\n" +
                "2. EventSystem updated to InputSystemUIInputModule.\n" +
                "3. Controls modal popup is configured and hidden at start.",
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
            btnRect.sizeDelta = new Vector2(280, 64);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.13f, 0.18f, 0.88f);
            btnImg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.12f, 0.13f, 0.18f, 0.88f);
            colors.highlightedColor = new Color(0.35f, 0.42f, 0.58f, 1.0f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.12f, 1.0f);
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
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = buttonLabel;
            txt.raycastTarget = false;

            return btn;
        }
    }
}
