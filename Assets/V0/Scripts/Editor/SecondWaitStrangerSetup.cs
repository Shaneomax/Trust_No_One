using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;
using TrustNoOne.AI;
using V0.Cinematics;

namespace V0.Editor
{
    public static class SecondWaitStrangerSetup
    {
        [MenuItem("Tools/Setup Stranger Knife Pickup Cutscene (SecondWait)", false, 57)]
        [MenuItem("Tools/Trust No One/Setup Stranger Knife Pickup Cutscene (SecondWait)", false, 57)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup SecondWait Knife Pickup Cutscene");

            // 1. Locate SecondWait GameObject
            GameObject triggerObj = GameObject.Find("SecondWait");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/SecondWait");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[SecondWaitStrangerSetup] Could not find 'SecondWait' GameObject in scene! Please ensure SecondWait exists under TriggerPoint.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup SecondWaitStrangerCutscene");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 2. Add or get SecondWaitStrangerCutscene component
            SecondWaitStrangerCutscene cutscene = triggerObj.GetComponent<SecondWaitStrangerCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<SecondWaitStrangerCutscene>(triggerObj);
            }

            // 3. Locate Enemy 2 (Stranger)
            DeceiverAI stranger = Object.FindFirstObjectByType<DeceiverAI>();

            // 4. Locate Table Knife
            GameObject tableKnife = GameObject.Find("SM_Knife");

            // 5. Locate Hand Knife attached to Stranger
            GameObject handKnife = null;
            if (stranger != null)
            {
                Transform[] allChildren = stranger.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in allChildren)
                {
                    if (child.name.ToLower().Contains("knife") && child.gameObject != tableKnife)
                    {
                        handKnife = child.gameObject;
                        break;
                    }
                }
            }

            // 6. Locate Player
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();

            // 7. Locate Letterbox Canvas & Text
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            CanvasGroup canvasGroup = null;
            Text subtitleText = null;
            if (canvasObj != null)
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                subtitleText = canvasObj.GetComponentInChildren<Text>();
            }

            // 8. Wire Serialized Properties
            SerializedObject so = new SerializedObject(cutscene);
            so.FindProperty("_stranger").objectReferenceValue = stranger;
            so.FindProperty("_tableKnife").objectReferenceValue = tableKnife;
            so.FindProperty("_handKnife").objectReferenceValue = handKnife;

            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }

            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;

            so.ApplyModifiedProperties();

            // Wire hand knife on DeceiverAI as well
            if (stranger != null && handKnife != null)
            {
                SerializedObject strangerSO = new SerializedObject(stranger);
                SerializedProperty hkProp = strangerSO.FindProperty("_handKnife");
                if (hkProp != null) hkProp.objectReferenceValue = handKnife;
                strangerSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(stranger.gameObject);
            }

            EditorUtility.SetDirty(triggerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            string tableKnifeName = tableKnife != null ? tableKnife.name : "None (Auto)";
            string handKnifeName = handKnife != null ? handKnife.name : "None (Auto-detected)";

            Debug.Log($"<color=green><b>[SecondWaitStrangerSetup]</b></color> SecondWait Cutscene configured!\n• Table Knife: {tableKnifeName}\n• Hand Knife: {handKnifeName}");
            EditorUtility.DisplayDialog("SecondWait Cutscene Setup",
                $"Successfully configured Knife Pickup Cutscene on 'SecondWait'!\n\n" +
                $"• Stranger: {(stranger != null ? stranger.name : "Found")}\n" +
                $"• Table Knife: {tableKnifeName}\n" +
                $"• Hand Knife: {handKnifeName}\n\n" +
                $"Sequence:\n" +
                $"1. Stranger plays PickUP animation.\n" +
                $"2. Table knife is destroyed & Hand knife is set active.\n" +
                $"3. Player asks: \"What's that for...?\"\n" +
                $"4. Stranger replies: \"I got the truck key. Let's get out of this house before my wife returns.\"\n" +
                $"5. Cutscene ends & Stranger resumes following player.", "OK");
        }
    }
}
