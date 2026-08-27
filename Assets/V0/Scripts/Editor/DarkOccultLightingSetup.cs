using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace V0.Editor
{
    public static class DarkOccultLightingSetup
    {
        [MenuItem("Tools/Apply Clean Horror Lighting", false, 60)]
        public static void ApplyLightingMenu()
        {
            ApplyLightingInternal(true);
        }

        public static void ApplyLightingInternal(bool logToConsole)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Clean Horror Lighting");

            // 1. Configure DirectionalLight (Atmospheric Pale Moonlight, Soft & Moody Outside)
            Light dirLight = null;
            GameObject dirLightObj = GameObject.Find("DirectionalLight");
            if (dirLightObj != null)
            {
                dirLight = dirLightObj.GetComponent<Light>();
            }

            if (dirLight == null)
            {
                Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in allLights)
                {
                    if (l.type == LightType.Directional)
                    {
                        dirLight = l;
                        break;
                    }
                }
            }

            if (dirLight != null)
            {
                Undo.RecordObject(dirLight, "Update Directional Light for Clean Horror Atmosphere");
                dirLight.color = new Color(190f / 255f, 210f / 255f, 240f / 255f, 1f); // Pale silvery moonlight
                dirLight.intensity = 0.75f; // Soft, moody exterior moonlight (not overexposed)
                dirLight.useColorTemperature = true;
                dirLight.colorTemperature = 7000f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.75f;
                EditorUtility.SetDirty(dirLight);
            }

            // 2. Configure Environment Ambient Lighting (Deep midnight-blue Trilight, preserving clean dark shadows)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(18f / 255f, 25f / 255f, 40f / 255f, 1f);     // Deep midnight blue
            RenderSettings.ambientEquatorColor = new Color(12f / 255f, 16f / 255f, 24f / 255f, 1f); // Muted dusk horizon
            RenderSettings.ambientGroundColor = new Color(6f / 255f, 8f / 255f, 12f / 255f, 1f);    // Deep dark ground
            RenderSettings.ambientIntensity = 0.5f; // Subtle ambient fill, keeping crevices & unlit corners dark & clean
            RenderSettings.reflectionIntensity = 0.25f;

            // 3. Fog (Clean, atmospheric midnight mist)
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(8f / 255f, 12f / 255f, 18f / 255f, 1f);
            RenderSettings.fogDensity = 0.005f;

            // 4. Configure Interior House Lights (WARM ATMOSPHERIC CANDLE & INCANDESCENT GLOW)
            Color warmCandleColor = new Color(255f / 255f, 185f / 255f, 115f / 255f, 1f); // Warm amber candle glow
            Color warmSconceColor = new Color(255f / 255f, 210f / 255f, 155f / 255f, 1f); // Soft incandescent sconce
            int interiorCount = 0;

            // A) Main Room & Candle PointLights
            GameObject pointLightsParent = GameObject.Find("PointLights");
            if (pointLightsParent != null)
            {
                Light[] pLights = pointLightsParent.GetComponentsInChildren<Light>(true);
                foreach (var pl in pLights)
                {
                    Undo.RecordObject(pl, "Update Interior Candle/Room Light");
                    pl.color = warmCandleColor;
                    pl.intensity = 1.1f;  // Balanced, natural candle illumination
                    pl.range = 5.0f;      // Intimate, localized reach (not bleeding across entire house)
                    pl.useColorTemperature = true;
                    pl.colorTemperature = 2400f; // Warm candlelight
                    pl.shadows = LightShadows.None;
                    EditorUtility.SetDirty(pl);
                    interiorCount++;
                }
            }

            // B) Wall Sconces in ModularHouse
            GameObject houseObj = GameObject.Find("ModularHouse");
            if (houseObj != null)
            {
                Light[] houseLights = houseObj.GetComponentsInChildren<Light>(true);
                foreach (var hl in houseLights)
                {
                    if (hl.type == LightType.Point)
                    {
                        Undo.RecordObject(hl, "Update Wall Sconce Light");
                        hl.color = warmSconceColor;
                        hl.intensity = 1.0f;
                        hl.range = 5.5f;
                        hl.useColorTemperature = true;
                        hl.colorTemperature = 2800f; // Soft incandescent
                        hl.shadows = LightShadows.None;
                        EditorUtility.SetDirty(hl);
                        interiorCount++;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (logToConsole)
            {
                Debug.Log($"<color=green>[Lighting]</color> Clean Horror Atmosphere applied! Warm candle interiors ({interiorCount} lights) & Deep Midnight exterior moonlight.");
            }
        }
    }
}
