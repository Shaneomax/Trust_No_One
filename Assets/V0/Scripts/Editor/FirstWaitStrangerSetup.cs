using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using TrustNoOne.AI;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class FirstWaitStrangerSetup
    {
        [MenuItem("Tools/Setup Stranger Wait Cutscene (FirstWait)", false, 55)]
        [MenuItem("Tools/Trust No One/Setup Stranger Wait Cutscene (FirstWait)", false, 55)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Stranger Wait Cutscene (FirstWait)");

            // 1. Locate FirstWait GameObject
            GameObject triggerObj = GameObject.Find("FirstWait");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/FirstWait");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[FirstWaitStrangerSetup] Could not find 'FirstWait' GameObject in scene! Please ensure FirstWait exists under TriggerPoint.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup FirstWaitStrangerCutscene");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 2. Add or get FirstWaitStrangerCutscene component
            FirstWaitStrangerCutscene cutscene = triggerObj.GetComponent<FirstWaitStrangerCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<FirstWaitStrangerCutscene>(triggerObj);
            }

            // 3. Locate Enemy 2 (Stranger)
            DeceiverAI stranger = Object.FindFirstObjectByType<DeceiverAI>();

            // 4. Locate the closest Door to FirstWait
            DoorInteractable[] allDoors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            DoorInteractable roomDoor = null;
            float closestDist = float.MaxValue;
            foreach (DoorInteractable d in allDoors)
            {
                float dist = Vector3.Distance(triggerObj.transform.position, d.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    roomDoor = d;
                }
            }

            // 5. Locate or create Knife Destination Spot
            Transform knifeDestination = null;
            GameObject knifeObj = GameObject.Find("SM_Knife");
            if (knifeObj != null)
            {
                // Create or find a clean standing spot marker next to the table
                Transform standSpot = knifeObj.transform.Find("StrangerStandSpot");
                if (standSpot == null)
                {
                    GameObject spotObj = new GameObject("StrangerStandSpot");
                    spotObj.transform.SetParent(knifeObj.transform.parent != null ? knifeObj.transform.parent : knifeObj.transform, false);
                    spotObj.transform.position = knifeObj.transform.position + new Vector3(0.6f, 0f, -0.4f);
                    spotObj.transform.rotation = Quaternion.Euler(0f, -140f, 0f);
                    standSpot = spotObj.transform;
                    Undo.RegisterCreatedObjectUndo(spotObj, "Create StrangerStandSpot");
                }
                knifeDestination = standSpot;
            }

            // 6. Locate or create Cam_FirstWait virtual camera
            GameObject cutsceneRig = GameObject.Find("CutsceneCameras");
            if (cutsceneRig == null)
            {
                cutsceneRig = new GameObject("CutsceneCameras");
                Undo.RegisterCreatedObjectUndo(cutsceneRig, "Create CutsceneCameras Rig");
            }

            CinemachineCamera firstWaitCam = null;
            Transform camTr = cutsceneRig.transform.Find("Cam_FirstWait");
            GameObject camObj = camTr != null ? camTr.gameObject : null;

            if (camObj == null)
            {
                camObj = new GameObject("Cam_FirstWait");
                camObj.transform.SetParent(cutsceneRig.transform, false);
                firstWaitCam = camObj.AddComponent<CinemachineCamera>();

                // Position camera nicely framing stranger and door
                camObj.transform.position = triggerObj.transform.position + new Vector3(-0.8f, 1.6f, -1.8f);
                camObj.transform.rotation = Quaternion.Euler(6f, 25f, 0f);

                firstWaitCam.Priority = 0;
                Undo.RegisterCreatedObjectUndo(camObj, "Create Cam_FirstWait");
            }
            else
            {
                firstWaitCam = camObj.GetComponent<CinemachineCamera>();
            }

            // 7. Locate Letterbox Canvas & Text
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            CanvasGroup canvasGroup = null;
            Text subtitleText = null;
            if (canvasObj != null)
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                subtitleText = canvasObj.GetComponentInChildren<Text>();
            }

            // 8. Locate Player and PlayerFollowCamera
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            CinemachineVirtualCameraBase playerFollowCam = null;
            GameObject followCamObj = GameObject.Find("PlayerFollowCamera");
            if (followCamObj != null)
            {
                playerFollowCam = followCamObj.GetComponent<CinemachineVirtualCameraBase>();
            }

            // 9. Wire Serialized Object Properties
            SerializedObject so = new SerializedObject(cutscene);
            so.FindProperty("_stranger").objectReferenceValue = stranger;
            so.FindProperty("_roomDoor").objectReferenceValue = roomDoor;
            so.FindProperty("_knifeDestination").objectReferenceValue = knifeDestination;
            so.FindProperty("_cutsceneCamera").objectReferenceValue = firstWaitCam;
            so.FindProperty("_playerFollowCamera").objectReferenceValue = playerFollowCam;

            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInteraction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }

            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;

            // Wire Stranger DeceiverAI properties
            if (stranger != null)
            {
                SerializedObject strangerSO = new SerializedObject(stranger);
                SerializedProperty knifeProp = strangerSO.FindProperty("_knifeDestination");
                if (knifeProp != null) knifeProp.objectReferenceValue = knifeDestination;
                strangerSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(stranger.gameObject);
            }

            EditorUtility.SetDirty(triggerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            string doorName = roomDoor != null ? roomDoor.gameObject.name : "None";
            string knifeName = knifeDestination != null ? knifeDestination.gameObject.name : "None";

            EditorUtility.DisplayDialog(
                "Stranger Wait Cutscene Configured!",
                $"✓ Configured FirstWait cutscene successfully!\n\n" +
                $"• Stranger: {(stranger != null ? stranger.name : "Auto-detected")}\n" +
                $"• Room Door: {doorName}\n" +
                $"• Knife Destination: {knifeName} (You can drag & drop any custom Transform)\n" +
                $"• Camera: Cam_FirstWait\n" +
                $"• Dialogue: 'Wait here, I'll go get the truck key.'\n\n" +
                $"Stranger will walk to the door, open it, enter the room beside the knife, and close the door shut behind him!",
                "OK"
            );

            Debug.Log("<color=green><b>[FirstWaitStrangerSetup]</b> Successfully setup Stranger Wait Cutscene on FirstWait!</color>");
        }
    }
}
