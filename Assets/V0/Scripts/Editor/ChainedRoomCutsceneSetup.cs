using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class ChainedRoomCutsceneSetup
    {
        [MenuItem("Tools/Setup Chained Room Cutscene (FirstTrigger)", false, 52)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Chained Room Cutscene");

            // 1. Locate or Create Cutscene Camera Rig Parent
            GameObject cutsceneRig = GameObject.Find("CutsceneCameras");
            if (cutsceneRig == null)
            {
                cutsceneRig = new GameObject("CutsceneCameras");
                Undo.RegisterCreatedObjectUndo(cutsceneRig, "Create CutsceneCameras Rig");
            }

            // 2. Create or Get Chained Room Camera
            GameObject camObj = GameObject.Find("Cam_ChainedRoom");
            CinemachineCamera chainedCam = null;

            if (camObj == null)
            {
                camObj = new GameObject("Cam_ChainedRoom");
                Undo.RegisterCreatedObjectUndo(camObj, "Create Cam_ChainedRoom");
                camObj.transform.SetParent(cutsceneRig.transform, false);
                chainedCam = camObj.AddComponent<CinemachineCamera>();
            }
            else
            {
                Undo.RecordObject(camObj.transform, "Update Cam_ChainedRoom Transform");
                chainedCam = camObj.GetComponent<CinemachineCamera>();
                if (chainedCam == null) chainedCam = Undo.AddComponent<CinemachineCamera>(camObj);
            }

            // Position facing the chained door in the hallway (editable in scene view)
            camObj.transform.position = new Vector3(3.6f, 4.2f, 1.2f);
            camObj.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            chainedCam.Priority.Value = 10;
            chainedCam.Lens.FieldOfView = 45f;

            // 3. Locate FirstTrigger GameObject
            GameObject triggerObj = GameObject.Find("FirstTrigger");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/FirstTrigger");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[ChainedRoomSetup] Could not find 'FirstTrigger' GameObject in scene! Please ensure FirstTrigger exists.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup ChainedRoomCutscene on FirstTrigger");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 4. Add or get ChainedRoomCutscene component
            ChainedRoomCutscene cutscene = triggerObj.GetComponent<ChainedRoomCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<ChainedRoomCutscene>(triggerObj);
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
            so.FindProperty("_chainedRoomCamera").objectReferenceValue = chainedCam;
            so.FindProperty("_playerFollowCamera").objectReferenceValue = playerFollowCam;
            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInteraction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }
            so.FindProperty("_cameraBlendDuration").floatValue = 2.5f;
            so.FindProperty("_shotDuration").floatValue = 6.5f;
            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[ChainedRoomSetup]</color> Successfully configured Chained Room Cutscene on 'FirstTrigger'!");
        }
    }
}
