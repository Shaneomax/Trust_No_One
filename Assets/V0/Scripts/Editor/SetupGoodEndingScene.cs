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
    public static class SetupGoodEndingScene
    {
        [MenuItem("Tools/Bake Ending Screen to GoodEnding Scene", false, 70)]
        [MenuItem("Tools/Trust No One/Bake Ending Screen to GoodEnding Scene", false, 70)]
        public static void BakeEndingScreen()
        {
            string scenePath = "Assets/V0/Scene/GoodEnding.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");

            // 1. Camera Solid Black
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

            // 3. EndingManager Host Object
            GameObject host = GameObject.Find("EndingManager");
            if (host == null)
            {
                host = new GameObject("EndingManager");
                Undo.RegisterCreatedObjectUndo(host, "Create EndingManager");
            }

            EndingManager mgr = host.GetComponent<EndingManager>();
            if (mgr == null) mgr = host.AddComponent<EndingManager>();

            // Remove any old canvas children for clean bake
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
            }

            // 4. Main Canvas
            GameObject canvasObj = new GameObject("EndingCanvas");
            canvasObj.transform.SetParent(host.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Fullscreen Black Image
            GameObject bgObj = new GameObject("BlackBackground");
            bgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false;

            // Narrative Panel
            GameObject narrativeObj = new GameObject("NarrativePanel");
            narrativeObj.transform.SetParent(canvasObj.transform, false);
            RectTransform narrativeRect = narrativeObj.AddComponent<RectTransform>();
            narrativeRect.anchorMin = new Vector2(0.1f, 0.25f);
            narrativeRect.anchorMax = new Vector2(0.9f, 0.75f);
            narrativeRect.sizeDelta = Vector2.zero;
            CanvasGroup narrativeGroup = narrativeObj.AddComponent<CanvasGroup>();
            narrativeGroup.alpha = 0f;
            narrativeGroup.blocksRaycasts = false;

            GameObject textObj = new GameObject("NarrativeText");
            textObj.transform.SetParent(narrativeObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text narrativeText = textObj.AddComponent<Text>();
            narrativeText.font = font;
            narrativeText.fontSize = 38;
            narrativeText.alignment = TextAnchor.MiddleCenter;
            narrativeText.color = Color.white;
            narrativeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            narrativeText.verticalOverflow = VerticalWrapMode.Overflow;
            narrativeText.lineSpacing = 1.25f;
            narrativeText.raycastTarget = false;

            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            textShadow.effectDistance = new Vector2(2f, -2f);

            // Thank You Panel
            GameObject thankYouObj = new GameObject("ThankYouPanel");
            thankYouObj.transform.SetParent(canvasObj.transform, false);
            RectTransform thankYouRect = thankYouObj.AddComponent<RectTransform>();
            thankYouRect.anchorMin = Vector2.zero;
            thankYouRect.anchorMax = Vector2.one;
            thankYouRect.sizeDelta = Vector2.zero;
            CanvasGroup thankYouGroup = thankYouObj.AddComponent<CanvasGroup>();
            thankYouGroup.alpha = 0f;
            thankYouGroup.blocksRaycasts = true;
            thankYouGroup.interactable = true;
            thankYouObj.SetActive(false);

            // Title: "Thank you for playing!"
            GameObject titleObj = new GameObject("ThankYouTitle");
            titleObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.62f);
            titleRect.anchorMax = new Vector2(0.5f, 0.62f);
            titleRect.sizeDelta = new Vector2(900, 100);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.75f, 1f);
            titleText.text = "Thank you for playing!";
            titleText.raycastTarget = false;

            // Creator Credits: "Created by: Anik Pal"
            GameObject creditObj = new GameObject("CreatorCredits");
            creditObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform creditRect = creditObj.AddComponent<RectTransform>();
            creditRect.anchorMin = new Vector2(0.5f, 0.50f);
            creditRect.anchorMax = new Vector2(0.5f, 0.50f);
            creditRect.sizeDelta = new Vector2(600, 50);
            Text creditText = creditObj.AddComponent<Text>();
            creditText.font = font;
            creditText.fontSize = 24;
            creditText.fontStyle = FontStyle.Bold;
            creditText.alignment = TextAnchor.MiddleCenter;
            creditText.color = new Color(0.9f, 0.92f, 0.96f, 0.95f);
            creditText.text = "Created by: Anik Pal";
            creditText.raycastTarget = false;

            // Bottom-Middle Continue Button
            GameObject btnObj = new GameObject("ContinueButton");
            btnObj.transform.SetParent(thankYouObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0, 130);
            btnRect.sizeDelta = new Vector2(280, 68);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            btnImg.raycastTarget = true;

            Button continueButton = btnObj.AddComponent<Button>();
            continueButton.targetGraphic = btnImg;
            ColorBlock colors = continueButton.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            colors.highlightedColor = new Color(0.40f, 0.44f, 0.56f, 1.0f);
            colors.pressedColor = new Color(0.10f, 0.10f, 0.12f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            continueButton.colors = colors;

            GameObject btnTextObj = new GameObject("BtnText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 24;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = "CONTINUE";
            btnText.raycastTarget = false;

            // 5. Serialize fields directly onto EndingManager component in scene
            SerializedObject so = new SerializedObject(mgr);
            so.FindProperty("_narrativeGroup").objectReferenceValue = narrativeGroup;
            so.FindProperty("_narrativeText").objectReferenceValue = narrativeText;
            so.FindProperty("_thankYouGroup").objectReferenceValue = thankYouGroup;
            so.FindProperty("_thankYouTitleText").objectReferenceValue = titleText;
            so.FindProperty("_creatorCreditsTextUI").objectReferenceValue = creditText;
            so.FindProperty("_continueButton").objectReferenceValue = continueButton;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(mgr);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green><b>[SetupGoodEndingScene]</b></color> Pre-baked all Ending UI elements at compile time with 0 runtime allocations!");
            EditorUtility.DisplayDialog("Ending Screen Baked",
                "Successfully pre-baked Ending Screen at compile-time!\n\n" +
                "• 0 Runtime Allocations / GC Spikes.\n" +
                "• Canvas, Panels, Texts, and Buttons baked directly in GoodEnding.unity.\n" +
                "• All serialized fields connected.",
                "OK");
        }
    }
}
