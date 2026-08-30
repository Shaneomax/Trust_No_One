using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;
using TrustNoOne.AI;
using V0.Cinematics;

namespace V0.Editor
{
    public static class LastTriggerSetup
    {
        [MenuItem("Tools/Setup Last Trigger Cutscene (Betrayal Ending)", false, 58)]
        [MenuItem("Tools/Trust No One/Setup Last Trigger Cutscene (Betrayal Ending)", false, 58)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup LastTrigger Betrayal Cutscene");

            // 1. Locate LastTrigger GameObject
            GameObject triggerObj = GameObject.Find("LastTrigger");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/LastTrigger");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[LastTriggerSetup] Could not find 'LastTrigger' GameObject in scene! Please ensure LastTrigger exists under TriggerPoint.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup LastTriggerCutscene");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 2. Add or get LastTriggerCutscene component
            LastTriggerCutscene cutscene = triggerObj.GetComponent<LastTriggerCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<LastTriggerCutscene>(triggerObj);
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
            SerializedObject so = new SerializedObject(cutscene);
            so.FindProperty("_stranger").objectReferenceValue = stranger;
            so.FindProperty("_endingSceneName").stringValue = "GoodEnding";

            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }

            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;

            // Wire audio
            AudioClip dropDeadClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/DropDeadSound.mp3");
            if (dropDeadClip != null) so.FindProperty("_dropDeadSound").objectReferenceValue = dropDeadClip;

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(triggerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[LastTriggerSetup]</b></color> Successfully configured Backstab Betrayal Cutscene on <b>'LastTrigger'</b>!");
            EditorUtility.DisplayDialog("LastTrigger Cutscene Setup",
                "Successfully configured Backstab Betrayal Cutscene on 'LastTrigger'!\n\n" +
                "Sequence:\n" +
                "1. Violent stab from behind with camera shake.\n" +
                "2. Player collapses and tilts sideways onto the floor.\n" +
                "3. Stranger steps over the fallen player.\n" +
                "4. Stranger says: \"You are not leaving.\"\n" +
                "5. Screen slowly fades out to black & loads GoodEnding scene.", "OK");
        }
    }
}
