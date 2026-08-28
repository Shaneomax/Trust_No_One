using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class StrangerDialogueSetup
    {
        [MenuItem("Tools/Setup Stranger Dialogue Cutscene (SecondTrigger)", false, 53)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Stranger Dialogue Cutscene");

            // 1. Locate SecondTrigger GameObject
            GameObject triggerObj = GameObject.Find("SecondTrigger");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/SecondTrigger");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[StrangerDialogueSetup] Could not find 'SecondTrigger' GameObject in scene! Please ensure SecondTrigger exists.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup StrangerDialogueCutscene on SecondTrigger");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 2. Add or get StrangerDialogueCutscene component
            StrangerDialogueCutscene cutscene = triggerObj.GetComponent<StrangerDialogueCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<StrangerDialogueCutscene>(triggerObj);
            }

            // 3. Locate Chained Room Virtual Camera & DoorShut_Cam
            CinemachineCamera chainedCam = null;
            GameObject camObj = GameObject.Find("Cam_ChainedRoom");
            if (camObj != null)
            {
                chainedCam = camObj.GetComponent<CinemachineCamera>();
            }

            CinemachineCamera doorShutCam = null;
            GameObject doorCamObj = GameObject.Find("DoorShut_Cam");
            if (doorCamObj != null)
            {
                doorShutCam = doorCamObj.GetComponent<CinemachineCamera>();
            }

            // 4. Locate Main Front Door
            DoorInteractable frontDoor = null;
            GameObject frontDoorObj = GameObject.Find("SM_Door_Front_01");
            if (frontDoorObj != null)
            {
                frontDoor = frontDoorObj.GetComponent<DoorInteractable>();
                if (frontDoor == null) frontDoor = frontDoorObj.GetComponentInParent<DoorInteractable>();
            }

            // Locate Letterbox Canvas & Text
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            CanvasGroup canvasGroup = null;
            Text subtitleText = null;
            if (canvasObj != null)
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                subtitleText = canvasObj.GetComponentInChildren<Text>();
            }

            // Locate Player and PlayerFollowCamera
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            CinemachineVirtualCameraBase playerFollowCam = null;
            GameObject followCamObj = GameObject.Find("PlayerFollowCamera");
            if (followCamObj != null)
            {
                playerFollowCam = followCamObj.GetComponent<CinemachineVirtualCameraBase>();
            }

            // Wire up SerializedProperties
            SerializedObject so = new SerializedObject(cutscene);
            so.FindProperty("_playerFollowCamera").objectReferenceValue = playerFollowCam;
            so.FindProperty("_mainFrontDoor").objectReferenceValue = frontDoor;
            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInteraction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }
            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;
            so.FindProperty("_cameraBlendDuration").floatValue = 2.0f;

            // Configure Shots List
            SerializedProperty shotsList = so.FindProperty("_shots");
            shotsList.ClearArray();

            // Shot 1: Greeting
            AddDialogueShot(shotsList, "1. Stranger Greeting", chainedCam, 5.0f,
                "[Stranger Behind Door]: \"Hey! You! Over here! Please, you have to help me!\"",
                new Color(1f, 0.88f, 0.6f), false, false);

            // Shot 2: Wife locked him in
            AddDialogueShot(shotsList, "2. Wife Locked Him In", chainedCam, 5.5f,
                "[Stranger Behind Door]: \"My wife locked me in here with these chains... She lost her mind, she's a crazy bitch!\"",
                new Color(1f, 0.88f, 0.6f), false, false);

            // Shot 3: Bolt cutter in dining area
            AddDialogueShot(shotsList, "3. Chain Cutter In Dining Area", chainedCam, 6.0f,
                "[Stranger Behind Door]: \"There's a chain cutter in the dining area. Find it, break these chains, and I'll get us out of here!\"",
                new Color(1f, 0.88f, 0.6f), false, false);

            // Shot 4: Door Slam & Lock Event (switches to DoorShut_Cam if available!)
            CinemachineCamera slamCam = doorShutCam != null ? doorShutCam : chainedCam;
            AddDialogueShot(shotsList, "4. Front Door Slam & Lock Event", slamCam, 5.0f,
                "[Player]: \"What the hell was that noise?! ...The front door just slammed shut!\"",
                new Color(0.95f, 0.95f, 0.9f), true, true);

            // Shot 5: Stranger frantic urgency
            AddDialogueShot(shotsList, "5. Stranger's Urgent Warning", chainedCam, 5.5f,
                "[Stranger Behind Door]: \"She knows you're in the house! Hurry, find that chain cutter before she finds you!\"",
                new Color(1f, 0.45f, 0.45f), false, false);

            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[StrangerDialogueSetup]</color> Successfully configured Stranger Dialogue Cutscene on 'SecondTrigger' with 5 customizable shots!");
        }

        private static void AddDialogueShot(SerializedProperty list, string name, CinemachineCamera cam, float duration, string text, Color color, bool doorSlam, bool shake)
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty shotProp = list.GetArrayElementAtIndex(index);
            shotProp.FindPropertyRelative("shotName").stringValue = name;
            shotProp.FindPropertyRelative("virtualCamera").objectReferenceValue = cam;
            shotProp.FindPropertyRelative("duration").floatValue = duration;
            shotProp.FindPropertyRelative("subtitleText").stringValue = text;
            shotProp.FindPropertyRelative("textColor").colorValue = color;
            shotProp.FindPropertyRelative("triggerDoorSlam").boolValue = doorSlam;
            shotProp.FindPropertyRelative("shakeCamera").boolValue = shake;
        }
    }
}
