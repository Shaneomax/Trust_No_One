using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class HouseFlyoverSetup
    {
        [MenuItem("Tools/Setup House & Map Flyover Cutscene", false, 51)]
        public static void SetupFlyoverCutscene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup House & Map Flyover Cutscene");

            // 1. Locate Main Camera & CinemachineBrain
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null)
                {
                    brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
                }
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.6f);
                EditorUtility.SetDirty(brain);
            }

            // 2. Locate or Create Letterbox Canvas
            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            CanvasGroup canvasGroup = null;
            Text subtitleText = null;

            if (canvasObj == null)
            {
                canvasObj = new GameObject("CinematicLetterboxCanvas");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create CinematicLetterboxCanvas");

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 900;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
                canvasGroup = canvasObj.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0f; // Hidden by default

                // Top Black Bar
                GameObject topBarObj = new GameObject("TopBlackBar");
                topBarObj.transform.SetParent(canvasObj.transform, false);
                Image topImg = topBarObj.AddComponent<Image>();
                topImg.color = Color.black;
                topImg.raycastTarget = false;
                RectTransform topRT = topImg.rectTransform;
                topRT.anchorMin = new Vector2(0f, 0.88f);
                topRT.anchorMax = new Vector2(1f, 1f);
                topRT.sizeDelta = Vector2.zero;

                // Bottom Black Bar
                GameObject botBarObj = new GameObject("BottomBlackBar");
                botBarObj.transform.SetParent(canvasObj.transform, false);
                Image botImg = botBarObj.AddComponent<Image>();
                botImg.color = Color.black;
                botImg.raycastTarget = false;
                RectTransform botRT = botImg.rectTransform;
                botRT.anchorMin = new Vector2(0f, 0f);
                botRT.anchorMax = new Vector2(1f, 0.12f);
                botRT.sizeDelta = Vector2.zero;

                // Subtitle Text inside Bottom Bar
                GameObject subObj = new GameObject("SubtitleText");
                subObj.transform.SetParent(botBarObj.transform, false);
                subtitleText = subObj.AddComponent<Text>();
                subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                subtitleText.fontSize = 24;
                subtitleText.alignment = TextAnchor.MiddleCenter;
                subtitleText.color = new Color(0.9f, 0.9f, 0.85f, 0.95f);
                RectTransform subRT = subtitleText.rectTransform;
                subRT.anchorMin = Vector2.zero;
                subRT.anchorMax = Vector2.one;
                subRT.sizeDelta = Vector2.zero;
            }
            else
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                subtitleText = canvasObj.GetComponentInChildren<Text>();
            }

            // 3. Create Cutscene Camera Rig Parent
            GameObject cutsceneRig = GameObject.Find("CutsceneCameras");
            if (cutsceneRig == null)
            {
                cutsceneRig = new GameObject("CutsceneCameras");
                Undo.RegisterCreatedObjectUndo(cutsceneRig, "Create CutsceneCameras Rig");
            }

            // Shot 1: Farmhouse Approach (Low angle looking down path past trucks)
            CinemachineCamera shot1Cam = CreateOrGetVirtualCam("Cam_Shot1_HouseApproach", cutsceneRig.transform,
                new Vector3(11.8f, 1.2f, -22f), Quaternion.Euler(-4f, 0f, 0f), 45f);

            // Shot 2: Derelict Barn Overview (High angle showcasing the shed & farm landscape)
            CinemachineCamera shot2Cam = CreateOrGetVirtualCam("Cam_Shot2_BarnOverview", cutsceneRig.transform,
                new Vector3(-8f, 6.5f, -12f), Quaternion.Euler(15f, 45f, 0f), 55f);

            // Shot 3: Upper Window / Second Story Zoom (Looking up at the dark mystery inside)
            CinemachineCamera shot3Cam = CreateOrGetVirtualCam("Cam_Shot3_UpperWindow", cutsceneRig.transform,
                new Vector3(4.5f, 3.5f, -8f), Quaternion.Euler(-18f, 0f, 0f), 38f);

            // 4. Locate Entry Trigger GameObject
            GameObject triggerObj = GameObject.Find("Entry Trigger");
            if (triggerObj == null)
            {
                triggerObj = GameObject.Find("TriggerPoint");
            }

            if (triggerObj == null)
            {
                triggerObj = new GameObject("Entry Trigger");
                Undo.RegisterCreatedObjectUndo(triggerObj, "Create Entry Trigger");
                triggerObj.transform.position = new Vector3(12.35f, -0.12f, -28.78f);
                triggerObj.transform.localScale = new Vector3(6.9f, 0.6f, 0.94f);
                BoxCollider box = triggerObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }

            // 5. Add / Configure HouseFlyoverCutscene on Entry Trigger
            HouseFlyoverCutscene cutscene = triggerObj.GetComponent<HouseFlyoverCutscene>();
            if (cutscene == null)
            {
                cutscene = Undo.AddComponent<HouseFlyoverCutscene>(triggerObj);
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

            // Add Shot 1: Smooth establishing push towards the farmhouse
            AddShotToSerializedProperty(shotsList, "House Approach (Driveway)", shot1Cam, 5.5f, "The old Blackwood Estate... Abandoned for decades.");
            // Add Shot 2: High angle panning over the barn and misty fields
            AddShotToSerializedProperty(shotsList, "Barn & Shed Overview", shot2Cam, 5.0f, "A decaying truck sits in the barn... Perhaps a way out?");
            // Add Shot 3: Moody zoom toward the dark upper window
            AddShotToSerializedProperty(shotsList, "Upper Window Mystery", shot3Cam, 4.5f, "Something feels wrong. Someone is watching from the window.");

            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[HouseFlyoverSetup]</color> Successfully configured House & Map Flyover Cutscene on 'Entry Trigger'!");
        }

        private static CinemachineCamera CreateOrGetVirtualCam(string name, Transform parent, Vector3 pos, Quaternion rot, float fov)
        {
            GameObject camObj = GameObject.Find(name);
            CinemachineCamera vcam = null;

            if (camObj == null)
            {
                camObj = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(camObj, "Create " + name);
                camObj.transform.SetParent(parent, false);

                vcam = camObj.AddComponent<CinemachineCamera>();
            }
            else
            {
                Undo.RecordObject(camObj.transform, "Update " + name);
                vcam = camObj.GetComponent<CinemachineCamera>();
                if (vcam == null) vcam = Undo.AddComponent<CinemachineCamera>(camObj);
            }

            camObj.transform.position = pos;
            camObj.transform.rotation = rot;
            vcam.Priority.Value = 10;
            vcam.Lens.FieldOfView = fov;

            return vcam;
        }

        private static void AddShotToSerializedProperty(SerializedProperty list, string name, CinemachineCamera cam, float duration, string subtitle)
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty shotProp = list.GetArrayElementAtIndex(index);
            shotProp.FindPropertyRelative("shotName").stringValue = name;
            shotProp.FindPropertyRelative("virtualCamera").objectReferenceValue = cam;
            shotProp.FindPropertyRelative("duration").floatValue = duration;
            shotProp.FindPropertyRelative("subtitleText").stringValue = subtitle;
        }
    }
}
