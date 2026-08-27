using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using StarterAssets;
using V0.Cinematics;
using V0.Interaction;

namespace V0.Editor
{
    public static class WakeUpSequenceSetup
    {
        [MenuItem("Tools/Setup Wake-Up Intro Sequence", false, 50)]
        public static void SetupSequence()
        {
            // 1. Locate Player
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player == null)
            {
                Debug.LogError("[WakeUpSetup] Could not find FirstPersonController in the scene! Please ensure PlayerCapsule is in the scene.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Wake-Up Intro Sequence");

            // 2. Ensure MainCamera has CinemachineBrain with fast settle blend (since struggle is fully animated)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null)
                {
                    brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
                }
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.2f);
                EditorUtility.SetDirty(brain);
            }

            // 3. Locate PlayerFollowCamera
            CinemachineVirtualCameraBase playerFollowCam = null;
            GameObject followCamObj = GameObject.Find("PlayerFollowCamera");
            if (followCamObj != null)
            {
                playerFollowCam = followCamObj.GetComponent<CinemachineVirtualCameraBase>();
            }

            // 4. Create or Locate WakeUpCamera
            GameObject wakeUpCamObj = GameObject.Find("WakeUpCamera");
            CinemachineCamera wakeUpCam = null;
            if (wakeUpCamObj == null)
            {
                wakeUpCamObj = new GameObject("WakeUpCamera");
                Undo.RegisterCreatedObjectUndo(wakeUpCamObj, "Create WakeUpCamera");

                wakeUpCam = wakeUpCamObj.AddComponent<CinemachineCamera>();
                wakeUpCam.Priority.Value = 30;
                wakeUpCam.Lens.FieldOfView = 50f;
            }
            else
            {
                Undo.RecordObject(wakeUpCamObj.transform, "Update WakeUpCamera Transform");
                wakeUpCam = wakeUpCamObj.GetComponent<CinemachineCamera>();
                if (wakeUpCam == null)
                {
                    wakeUpCam = Undo.AddComponent<CinemachineCamera>(wakeUpCamObj);
                }
            }

            // Position on ground near player: 0.35m high, looking down the path towards the house (NOT outside the map)
            Vector3 groundPos = player.transform.position + Vector3.up * 0.35f;
            wakeUpCamObj.transform.position = groundPos;
            wakeUpCamObj.transform.rotation = Quaternion.Euler(-4f, player.transform.eulerAngles.y, 15f);

            // 5. Create or Locate WakeUpCanvas (Black Eyelids Overlay)
            GameObject canvasObj = GameObject.Find("WakeUpCanvas");
            CanvasGroup canvasGroup = null;
            if (canvasObj == null)
            {
                canvasObj = new GameObject("WakeUpCanvas");
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create WakeUpCanvas");

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999; // Top of all UI

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
                canvasGroup = canvasObj.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;

                // Black fullscreen image
                GameObject imageObj = new GameObject("BlackOverlay");
                imageObj.transform.SetParent(canvasObj.transform, false);

                Image img = imageObj.AddComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;

                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }
            else
            {
                canvasGroup = canvasObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = Undo.AddComponent<CanvasGroup>(canvasObj);
                }
            }

            // 6. Create or Locate WakeUpSequenceController
            GameObject controllerObj = GameObject.Find("WakeUpSequenceController");
            WakeUpSequenceController controller = null;
            if (controllerObj == null)
            {
                controllerObj = new GameObject("WakeUpSequenceController");
                Undo.RegisterCreatedObjectUndo(controllerObj, "Create WakeUpSequenceController");
                controller = controllerObj.AddComponent<WakeUpSequenceController>();
            }
            else
            {
                controller = controllerObj.GetComponent<WakeUpSequenceController>();
                if (controller == null)
                {
                    controller = Undo.AddComponent<WakeUpSequenceController>(controllerObj);
                }
            }

            // Wire up SerializedProperties
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("_wakeUpCamera").objectReferenceValue = wakeUpCam;
            so.FindProperty("_playerFollowCamera").objectReferenceValue = playerFollowCam;
            so.FindProperty("_playerController").objectReferenceValue = player;
            so.FindProperty("_playerInteraction").objectReferenceValue = player.GetComponent<PlayerInteraction>();
            so.FindProperty("_playerInputs").objectReferenceValue = player.GetComponent<StarterAssetsInputs>();
            so.FindProperty("_blackoutCanvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[WakeUpSetup]</color> Wake-Up Sequence successfully configured! Press Play to watch the horror intro.");
        }
    }
}
