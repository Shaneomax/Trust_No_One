using UnityEditor;
using UnityEngine;

namespace V0.Editor
{
    /// <summary>
    /// Quick Editor utility to adjust interior room lights in the house.
    /// Accessible via: Tools > Lighting > ...
    /// </summary>
    public static class HouseLightingAdjuster
    {
        [MenuItem("Tools/Lighting/Set Room Lights (Brighter & Clear - Recommended)", false, 30)]
        [MenuItem("Tools/Trust No One/Set Room Lights (Brighter & Clear)", false, 30)]
        public static void SetRoomLightsABitBrighter()
        {
            ApplyRoomLightSettings(intensityMultiplier: 2.3f, rangeMultiplier: 2.8f, minIntensity: 2.6f, minRange: 7.5f);
        }

        [MenuItem("Tools/Lighting/Set Room Lights (Moderate / Soft)", false, 31)]
        public static void SetRoomLightsSlightlyBrighter()
        {
            ApplyRoomLightSettings(intensityMultiplier: 1.6f, rangeMultiplier: 2.4f, minIntensity: 1.85f, minRange: 6.5f);
        }

        [MenuItem("Tools/Lighting/Set Room Lights (Original Dim)", false, 32)]
        public static void SetRoomLightsOriginal()
        {
            ApplyRoomLightSettings(intensityMultiplier: 1.0f, rangeMultiplier: 1.0f, minIntensity: 1.1f, minRange: 2.7f);
        }

        public static void ApplyRoomLightSettings(float intensityMultiplier, float rangeMultiplier, float minIntensity, float minRange)
        {
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int modifiedCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Adjust House Room Lights");

            foreach (Light l in allLights)
            {
                // Only modify interior room point lights (skip sun/directional and flashlight)
                if (l.type == LightType.Point && l.gameObject.name.ToLower().Contains("light") && !l.gameObject.name.ToLower().Contains("flash"))
                {
                    Undo.RecordObject(l, "Adjust Room Light");
                    l.intensity = minIntensity;
                    l.range = minRange;
                    EditorUtility.SetDirty(l);
                    modifiedCount++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"<color=yellow><b>[Lighting]</b></color> Successfully updated <b>{modifiedCount}</b> room lights! (Intensity: {minIntensity}, Range: {minRange})");
        }
    }
}
