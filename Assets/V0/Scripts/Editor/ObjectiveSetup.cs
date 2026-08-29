using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using V0.UI;

namespace V0.Editor
{
    /// <summary>
    /// 1-Click Setup Tool for ObjectiveManager UI.
    /// Creates a crisp, atmospheric horror objective banner at the top-left of the screen.
    /// Accessible via: Tools > Setup Objective UI Manager (Top-Left)
    /// </summary>
    public static class ObjectiveSetup
    {
        [MenuItem("Tools/Setup Objective UI Manager (Top-Left)", false, 25)]
        [MenuItem("Tools/Trust No One/Setup Objective UI Manager (Top-Left)", false, 25)]
        public static void SetupObjectiveUI()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Objective UI Manager");

            // 1. Locate or create Canvas
            Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>();
            GameObject canvasObj = null;

            if (mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvasObj = mainCanvas.gameObject;
            }
            else
            {
                canvasObj = GameObject.Find("Canvas");
            }

            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
                Canvas c = canvasObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 50;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 2. Locate or create ObjectiveContainer under Canvas
            Transform existingContainer = canvasObj.transform.Find("ObjectiveContainer");
            GameObject containerObj = existingContainer != null ? existingContainer.gameObject : null;

            if (containerObj == null)
            {
                containerObj = new GameObject("ObjectiveContainer");
                Undo.RegisterCreatedObjectUndo(containerObj, "Create ObjectiveContainer");
                containerObj.transform.SetParent(canvasObj.transform, false);
            }
            else
            {
                Undo.RecordObject(containerObj, "Update ObjectiveContainer");
            }

            // RectTransform setup: Top-Left anchor
            RectTransform containerRect = containerObj.GetComponent<RectTransform>();
            if (containerRect == null) containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(0f, 1f);
            containerRect.pivot = new Vector2(0f, 1f);
            containerRect.anchoredPosition = new Vector2(40f, -40f);
            containerRect.sizeDelta = new Vector2(600f, 90f);

            // CanvasGroup
            CanvasGroup canvasGroup = containerObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = containerObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 3. Header Label ("OBJECTIVE")
            Transform existingHeader = containerObj.transform.Find("HeaderText");
            GameObject headerObj = existingHeader != null ? existingHeader.gameObject : null;
            if (headerObj == null)
            {
                headerObj = new GameObject("HeaderText");
                Undo.RegisterCreatedObjectUndo(headerObj, "Create HeaderText");
                headerObj.transform.SetParent(containerObj.transform, false);
            }

            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            if (headerRect == null) headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(0f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, 0f);
            headerRect.sizeDelta = new Vector2(500f, 24f);

            Text headerText = headerObj.GetComponent<Text>();
            if (headerText == null) headerText = headerObj.AddComponent<Text>();
            headerText.text = "OBJECTIVE";
            headerText.fontSize = 13;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.UpperLeft;
            headerText.color = new Color(1f, 0.85f, 0.45f, 0.9f); // Subtle amber/gold
            headerText.raycastTarget = false;

            Shadow headerShadow = headerObj.GetComponent<Shadow>();
            if (headerShadow == null) headerShadow = headerObj.AddComponent<Shadow>();
            headerShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            headerShadow.effectDistance = new Vector2(1f, -1f);

            // 4. Main Objective Text ("Seek Help from the House")
            Transform existingObjText = containerObj.transform.Find("ObjectiveText");
            GameObject objTextObj = existingObjText != null ? existingObjText.gameObject : null;
            if (objTextObj == null)
            {
                objTextObj = new GameObject("ObjectiveText");
                Undo.RegisterCreatedObjectUndo(objTextObj, "Create ObjectiveText");
                objTextObj.transform.SetParent(containerObj.transform, false);
            }

            RectTransform objRect = objTextObj.GetComponent<RectTransform>();
            if (objRect == null) objRect = objTextObj.AddComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0f, 1f);
            objRect.anchorMax = new Vector2(0f, 1f);
            objRect.pivot = new Vector2(0f, 1f);
            objRect.anchoredPosition = new Vector2(0f, -22f);
            objRect.sizeDelta = new Vector2(600f, 60f);

            Text objectiveText = objTextObj.GetComponent<Text>();
            if (objectiveText == null) objectiveText = objTextObj.AddComponent<Text>();
            objectiveText.text = "Seek Help from the House";
            objectiveText.fontSize = 20;
            objectiveText.fontStyle = FontStyle.Bold;
            objectiveText.alignment = TextAnchor.UpperLeft;
            objectiveText.color = Color.white;
            objectiveText.raycastTarget = false;

            Outline textOutline = objTextObj.GetComponent<Outline>();
            if (textOutline == null) textOutline = objTextObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 5. ObjectiveManager Component
            ObjectiveManager manager = containerObj.GetComponent<ObjectiveManager>();
            if (manager == null) manager = Undo.AddComponent<ObjectiveManager>(containerObj);

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_headerText").objectReferenceValue = headerText;
            so.FindProperty("_objectiveText").objectReferenceValue = objectiveText;
            so.FindProperty("_initialObjective").stringValue = "Seek Help from the House";
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(containerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[ObjectiveSetup]</b> Successfully configured ObjectiveManager UI (Top-Left) in Canvas!</color>");
            EditorUtility.DisplayDialog("Objective UI Setup",
                "Successfully configured Objective Manager UI!\n\n" +
                "• Position: Top-Left Screen Anchor\n" +
                "• Header: 'OBJECTIVE'\n" +
                "• Initial Text: 'Seek Help from the House'\n\n" +
                "All milestone objective hooks have been automatically wired:\n" +
                "1. Entry: 'Seek Help from the House'\n" +
                "2. First Trigger: 'Investigate the noise'\n" +
                "3. Second Trigger: 'Search for chainsaw to break the chain'\n" +
                "4. Pickup Crowbar: 'Retrieve Master key from the bedroom'\n" +
                "5. Pickup Master Key: 'Get the Chainsaw'\n" +
                "6. Pickup Chainsaw: 'Free the man'\n" +
                "7. First Wait: 'Check for the man'\n" +
                "8. Second Wait: 'Exit The House'", "OK");
        }
    }
}
