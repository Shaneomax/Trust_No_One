using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;
using TrustNoOne.AI;
using V0.Cinematics;

namespace V0.Editor
{
    public static class OkayEndingSetup
    {
        [MenuItem("Tools/Setup Okay Ending (Second Ending)", false, 56)]
        [MenuItem("Tools/Trust No One/Setup Okay Ending (Second Ending)", false, 56)]
        public static void SetupTrigger()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Okay Ending (Second Ending)");

            // 1. Locate OkayEnding GameObject
            GameObject triggerObj = GameObject.Find("OkayEnding");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/OkayEnding");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[OkayEndingSetup] Could not find 'OkayEnding' GameObject in scene! Please ensure OkayEnding exists under TriggerPoint.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup OkayEndingTrigger");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 2. Add or get OkayEndingTrigger component
            OkayEndingTrigger trigger = triggerObj.GetComponent<OkayEndingTrigger>();
            if (trigger == null)
            {
                trigger = Undo.AddComponent<OkayEndingTrigger>(triggerObj);
            }

            // 3. Locate Enemy 2 (Stranger)
            DeceiverAI stranger = Object.FindFirstObjectByType<DeceiverAI>();

            // 4. Locate Player
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();

            // 5. Locate Letterbox Canvas & Text
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            CanvasGroup canvasGroup = null;
            Text subtitleText = null;
            if (canvasObj != null)
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                subtitleText = canvasObj.GetComponentInChildren<Text>();
            }

            // 6. Wire Serialized Properties
            SerializedObject so = new SerializedObject(trigger);
            so.FindProperty("_stranger").objectReferenceValue = stranger;
            so.FindProperty("_endingSceneName").stringValue = "GoodEnding";

            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }

            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(triggerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[OkayEndingSetup]</b></color> Successfully configured Okay Ending on <b>'OkayEnding'</b>! Stranger pursuit, evil laugh, and transition to GoodEnding are ready.");
            EditorUtility.DisplayDialog("Okay Ending Setup", "Successfully configured Okay Ending (Second Ending) on 'OkayEnding'!\n\nWhen player enters this trigger:\n1. Stranger shouts 'STOP!'.\n2. Stranger rapidly approaches player.\n3. Player camera tracks Stranger.\n4. Stranger delivers Evil Laugh.\n5. Screen fades to black & loads GoodEnding scene.", "OK");
        }
    }
}
