using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class GhostSpawnSetup
    {
        [MenuItem("Tools/Setup Ghost Spawn Cutscene (GhostTrigger)", false, 54)]
        public static void SetupCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Ghost Spawn Cutscene");

            // 1. Locate or Create Cutscene Camera Rig Parent
            GameObject cutsceneRig = GameObject.Find("CutsceneCameras");
            if (cutsceneRig == null)
            {
                cutsceneRig = new GameObject("CutsceneCameras");
                Undo.RegisterCreatedObjectUndo(cutsceneRig, "Create CutsceneCameras Rig");
            }

            // 2. Create or Locate Cam_GhostGrandEntrance Virtual Camera
            GameObject camObj = GameObject.Find("Cam_GhostGrandEntrance");
            CinemachineCamera ghostCam = null;

            if (camObj == null)
            {
                camObj = new GameObject("Cam_GhostGrandEntrance");
                Undo.RegisterCreatedObjectUndo(camObj, "Create Cam_GhostGrandEntrance");
                camObj.transform.SetParent(cutsceneRig.transform, false);
                ghostCam = camObj.AddComponent<CinemachineCamera>();
            }
            else
            {
                Undo.RecordObject(camObj.transform, "Update Cam_GhostGrandEntrance Transform");
                ghostCam = camObj.GetComponent<CinemachineCamera>();
                if (ghostCam == null) ghostCam = Undo.AddComponent<CinemachineCamera>(camObj);
            }

            // Position looking at the downstairs grand entrance / foyer
            camObj.transform.position = new Vector3(4.98f, 1.4f, -2.5f);
            camObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            ghostCam.Priority.Value = 0;
            ghostCam.Lens.FieldOfView = 50f;

            // 3. Locate or Create GhostSpawnFog Particle System
            GameObject fogObj = GameObject.Find("GhostSpawnFog");
            ParticleSystem fogPS = null;
            if (fogObj == null)
            {
                fogObj = new GameObject("GhostSpawnFog");
                Undo.RegisterCreatedObjectUndo(fogObj, "Create GhostSpawnFog");
                fogObj.transform.position = new Vector3(4.98f, 0.2f, 2.0f);
                fogPS = fogObj.AddComponent<ParticleSystem>();

                var main = fogPS.main;
                main.startLifetime = 3.5f;
                main.startSpeed = 0.25f;
                main.startSize = 3.5f;
                main.startColor = new Color(0.85f, 0.92f, 0.95f, 0.35f);
                main.maxParticles = 60;
                main.playOnAwake = false;
                main.loop = true;

                var emission = fogPS.emission;
                emission.rateOverTime = 18f;

                var shape = fogPS.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 2.5f;
            }
            else
            {
                fogPS = fogObj.GetComponent<ParticleSystem>();
            }

            // 4. Locate GhostTrigger GameObject
            GameObject triggerObj = GameObject.Find("GhostTrigger");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint/GhostTrigger");
            }

            if (triggerObj == null)
            {
                Debug.LogError("[GhostSpawnSetup] Could not find 'GhostTrigger' GameObject in scene! Please ensure GhostTrigger exists.");
                return;
            }

            Undo.RecordObject(triggerObj, "Setup GhostSpawnCutscene on GhostTrigger");

            // Ensure BoxCollider is a Trigger
            BoxCollider col = triggerObj.GetComponent<BoxCollider>();
            if (col == null) col = triggerObj.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 5. Add or get GhostSpawnCutscene component
            GhostSpawnCutscene cutscene = triggerObj.GetComponent<GhostSpawnCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<GhostSpawnCutscene>(triggerObj);
            }

            // Locate Ghost
            GameObject ghostObj = GameObject.Find("Ghost");

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
            so.FindProperty("_ghostGameObject").objectReferenceValue = ghostObj;
            so.FindProperty("_ghostSpawnFog").objectReferenceValue = fogPS;
            if (player != null)
            {
                so.FindProperty("_playerController").objectReferenceValue = player;
                so.FindProperty("_playerInteraction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
                so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            }
            so.FindProperty("_letterboxCanvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_subtitleText").objectReferenceValue = subtitleText;
            so.FindProperty("_cameraBlendDuration").floatValue = 2.5f;

            // Configure Shots List
            SerializedProperty shotsList = so.FindProperty("_shots");
            shotsList.ClearArray();

            // Shot 1: Ghost awakens in fog & Stranger panic
            AddDialogueShot(shotsList, "1. Ghost Materializes in Fog", ghostCam, 5.0f,
                "[Stranger Behind Door]: \"Listen to me! She's awake! Do NOT let her catch you!\"",
                new Color(1f, 0.45f, 0.45f), true, true);

            // Shot 2: Player disbelief
            AddDialogueShot(shotsList, "2. Player Disbelief", ghostCam, 4.5f,
                "[Player]: \"What... what is that thing?! Is she even alive?!\"",
                new Color(0.95f, 0.95f, 0.9f), false, false);

            // Shot 3: Stealth tutorial / warning
            AddDialogueShot(shotsList, "3. Stealth Warning & Rules", ghostCam, 6.0f,
                "[Stranger Behind Door]: \"Her senses are sharp! Try not to sprint to avoid making noise—crouch and stay in the shadows!\"",
                new Color(1f, 0.88f, 0.6f), false, false);

            so.ApplyModifiedProperties();

            // Link DrawingRoomKey to activate GhostTrigger on pickup
            KeyPickup[] allKeys = Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None);
            foreach (KeyPickup key in allKeys)
            {
                if (key != null && (key.KeyId == "DrawingRoomKey" || key.name.Contains("DrawingRoomKey") || key.name.Contains("Key")))
                {
                    SerializedObject keySO = new SerializedObject(key);
                    keySO.FindProperty("_triggerToActivateOnPickup").objectReferenceValue = triggerObj;
                    keySO.FindProperty("_spawnGhostOnPickup").boolValue = false;
                    keySO.ApplyModifiedProperties();
                    Debug.Log($"<color=cyan>[GhostSpawnSetup]</color> Linked key '{key.name}' (DrawingRoomKey) to activate 'GhostTrigger' on pickup!");
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[GhostSpawnSetup]</color> Successfully configured Ghost Spawn Cutscene on 'GhostTrigger' with Grand Entrance Fog and Idle Ghost animation!");
        }

        private static void AddDialogueShot(SerializedProperty list, string name, CinemachineCamera cam, float duration, string text, Color color, bool ghostActivate, bool shake)
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty shotProp = list.GetArrayElementAtIndex(index);
            shotProp.FindPropertyRelative("shotName").stringValue = name;
            shotProp.FindPropertyRelative("virtualCamera").objectReferenceValue = cam;
            shotProp.FindPropertyRelative("duration").floatValue = duration;
            shotProp.FindPropertyRelative("subtitleText").stringValue = text;
            shotProp.FindPropertyRelative("textColor").colorValue = color;
            shotProp.FindPropertyRelative("activateGhost").boolValue = ghostActivate;
            shotProp.FindPropertyRelative("shakeCamera").boolValue = shake;
        }
    }
}
