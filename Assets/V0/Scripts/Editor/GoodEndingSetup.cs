using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;
using V0.Cinematics;

namespace V0.Editor
{
    public static class GoodEndingSetup
    {
        [MenuItem("Tools/Setup Good Ending Trigger", false, 56)]
        public static void Setup()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Good Ending Trigger");

            // Find GoodEnding trigger object
            GameObject triggerObj = GameObject.Find("GoodEnding");
            if (triggerObj == null)
            {
                Debug.LogError("[GoodEndingSetup] Could not find 'GoodEnding' GameObject in scene!");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup GoodEndingTrigger");

            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            GoodEndingTrigger trigger = triggerObj.GetComponent<GoodEndingTrigger>();
            if (trigger == null) trigger = Undo.AddComponent<GoodEndingTrigger>(triggerObj);

            // Wire references
            SerializedObject so = new SerializedObject(trigger);
            so.FindProperty("_goodEndingSceneName").stringValue = "GoodEnding";
            so.FindProperty("_transitionDelay").floatValue = 1.5f;

            // Letterbox canvas
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            if (canvasObj != null)
            {
                so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasObj.GetComponent<CanvasGroup>();
                so.FindProperty("_subtitleText").objectReferenceValue = canvasObj.GetComponentInChildren<Text>();
            }

            // Player refs
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }

            so.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(undoGroup);

            // Make sure GoodEnding scene is in Build Settings
            string goodEndingPath = "Assets/V0/Scene/GoodEnding.unity";
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyAdded = false;
            foreach (var s in scenes)
            {
                if (s.path == goodEndingPath) { alreadyAdded = true; break; }
            }
            if (!alreadyAdded)
            {
                scenes.Add(new EditorBuildSettingsScene(goodEndingPath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"<color=cyan>[GoodEndingSetup]</color> Added '{goodEndingPath}' to Build Settings!");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[GoodEndingSetup]</color> Good Ending Trigger configured! Stranger's final threat dialogue ready.");
        }
    }
}
