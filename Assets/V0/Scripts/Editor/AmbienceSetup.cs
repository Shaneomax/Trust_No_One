using UnityEditor;
using UnityEngine;
using V0.Audio;

namespace V0.Editor
{
    /// <summary>
    /// 1-Click Setup Tool for Ambient Audio (Outside & Inside House).
    /// Accessible via: Tools > Audio > Setup House Ambience Manager
    /// </summary>
    public static class AmbienceSetup
    {
        [MenuItem("Tools/Audio/Setup House Ambience Manager (Outside & Inside)", false, 40)]
        [MenuItem("Tools/Trust No One/Setup House Ambience Manager", false, 40)]
        public static void SetupAmbience()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup House Ambience Manager");

            // 1. Locate or create [AmbienceManager] GameObject
            GameObject managerObj = GameObject.Find("[AmbienceManager]");
            if (managerObj == null)
            {
                managerObj = new GameObject("[AmbienceManager]");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create [AmbienceManager]");
            }
            else
            {
                Undo.RecordObject(managerObj, "Update [AmbienceManager]");
            }

            HouseAmbienceManager manager = managerObj.GetComponent<HouseAmbienceManager>();
            if (manager == null) manager = Undo.AddComponent<HouseAmbienceManager>(managerObj);

            // 2. Locate Audio Assets
            AudioClip outsideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/OutsideSound.mp3");
            AudioClip insideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/Inside_House.mp3");

            if (outsideClip == null)
            {
                string[] guids = AssetDatabase.FindAssets("OutsideSound t:AudioClip");
                if (guids.Length > 0) outsideClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (insideClip == null)
            {
                string[] guids = AssetDatabase.FindAssets("Inside_House t:AudioClip");
                if (guids.Length > 0) insideClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            SerializedObject so = new SerializedObject(manager);
            if (outsideClip != null) so.FindProperty("_outsideAmbienceClip").objectReferenceValue = outsideClip;
            if (insideClip != null) so.FindProperty("_insideAmbienceClip").objectReferenceValue = insideClip;

            so.FindProperty("_outsideMaxVolume").floatValue = 0.60f;
            so.FindProperty("_insideMaxVolume").floatValue = 0.45f;
            so.FindProperty("_outsideBleedWhenInside").floatValue = 0.08f;
            so.FindProperty("_fadeDuration").floatValue = 2.0f;
            so.FindProperty("_startOutside").boolValue = true;
            so.ApplyModifiedProperties();

            // 3. Locate or create House Interior Ambience Trigger Zone
            GameObject triggerObj = GameObject.Find("HouseAmbienceTrigger");
            if (triggerObj == null)
            {
                triggerObj = new GameObject("HouseAmbienceTrigger");
                Undo.RegisterCreatedObjectUndo(triggerObj, "Create HouseAmbienceTrigger");

                // Position over house interior
                triggerObj.transform.position = new Vector3(1.0f, 2.5f, 4.5f);
            }
            else
            {
                Undo.RecordObject(triggerObj, "Update HouseAmbienceTrigger");
            }

            BoxCollider box = triggerObj.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(triggerObj);
            box.isTrigger = true;
            box.size = new Vector3(20.0f, 8.0f, 20.0f);
            box.center = Vector3.zero;

            AmbienceZoneTrigger zone = triggerObj.GetComponent<AmbienceZoneTrigger>();
            if (zone == null) zone = Undo.AddComponent<AmbienceZoneTrigger>(triggerObj);

            EditorUtility.SetDirty(managerObj);
            EditorUtility.SetDirty(triggerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[AmbienceSetup]</b> Successfully configured HouseAmbienceManager and Interior Trigger Zone!</color>");
            EditorUtility.DisplayDialog(
                "Ambience Setup Complete!",
                "Successfully configured Global Ambience System!\n\n" +
                "✓ '[AmbienceManager]' created in scene\n" +
                "✓ 'OutsideSound.mp3' assigned to Outside Ambience slot\n" +
                "✓ 'HouseAmbienceTrigger' zone created covering the house interior\n" +
                "✓ 2.0s smooth crossfade between Outside Wind and Inside Ambience\n\n" +
                "You can select '[AmbienceManager]' in the Hierarchy to adjust volume or drop in an Inside Ambience clip anytime!",
                "OK"
            );
        }
    }
}
