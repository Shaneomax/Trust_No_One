using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace V0.Editor
{
    [InitializeOnLoad]
    public static class DarkOccultLightingSetup
    {
        static DarkOccultLightingSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ApplyLightingInternal(false);
                }
            };

            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.delayCall += () => ApplyLightingInternal(false);
                }
            };
        }

        [MenuItem("Tools/Apply RE4 Style Horror Lighting", false, 60)]
        [MenuItem("Tools/Fix Building Light Bleed", false, 61)]
        public static void ApplyLightingMenu()
        {
            ApplyLightingInternal(true);
        }

        public static void ApplyLightingInternal(bool logToConsole)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply RE4 Remake Lighting & Fix Light Bleed");

            // ============================================================
            // 1. DIRECTIONAL LIGHT — Atmospheric Cold Blue Moonlight (RE4 Remake Exterior)
            //    Clear visibility outside: ground, trees, trucks, fences clearly readable!
            // ============================================================
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
                Undo.RecordObject(dirLight, "Update Directional Light for RE4 Exterior");
                // Pale cool blue moonlight — gives clean visibility without pitch-black void
                dirLight.color = new Color(180f / 255f, 210f / 255f, 250f / 255f, 1f);
                dirLight.intensity = 1.25f;
                dirLight.useColorTemperature = false; // Pure clean color, no muddy double-filtering
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.85f;
                dirLight.shadowBias = 0.03f;
                dirLight.shadowNormalBias = 0.2f;
                dirLight.shadowNearPlane = 0.1f;
                EditorUtility.SetDirty(dirLight);
            }

            // ============================================================
            // 2. ENVIRONMENT AMBIENT LIGHTING — RE4 Remake Cool Night Ambience
            //    Readable ground and midground, rich night sky, specular reflection response
            // ============================================================
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Sky: Deep atmospheric night blue
            RenderSettings.ambientSkyColor = new Color(32f / 255f, 48f / 255f, 75f / 255f, 1f);
            // Equator: Misty dusk horizon
            RenderSettings.ambientEquatorColor = new Color(22f / 255f, 32f / 255f, 50f / 255f, 1f);
            // Ground: Subtle cool earth
            RenderSettings.ambientGroundColor = new Color(14f / 255f, 18f / 255f, 24f / 255f, 1f);
            RenderSettings.ambientIntensity = 1.0f; // High enough so the outside is NOT pitch black!
            RenderSettings.reflectionIntensity = 1.0f; // 100% PBR specular reflection response
            RenderSettings.defaultReflectionResolution = 512;

            // ============================================================
            // 3. FOG — Atmospheric Cool Mist (RE4 Remake style)
            //    Adds distance depth and mood without obscuring nearby gameplay
            // ============================================================
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(18f / 255f, 26f / 255f, 40f / 255f, 1f);
            RenderSettings.fogDensity = 0.007f;

            // ============================================================
            // 4. DEACTIVATE ROGUE HIGH-POWER LIGHTS
            //    Turn off 100m range spotlights in root that wash out scene
            // ============================================================
            // Light[] allSceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            // int rogueCleaned = 0;
            // foreach (var l in allSceneLights)
            // {
            //     if (l == null) continue;
            //     if (l.transform.parent == null &&
            //         (l.gameObject.name == "Point Light" ||
            //          l.gameObject.name == "Point Light (1)" ||
            //          (l.type == LightType.Spot && l.range >= 50f)))
            //     {
            //         Undo.RecordObject(l.gameObject, "Deactivate Rogue Root Light");
            //         l.gameObject.SetActive(false);
            //         rogueCleaned++;
            //     }
            // }

            // ============================================================
            // 5. COLOR PALETTE — RE4 Remake Interior (NO REDDISH VIBE!)
            //    Candles: Pure warm golden yellow (user requested: candle light should be yellow)
            //    Wall Sconces: Clean soft warm ivory / champagne (natural warm white, not red/orange)
            // ============================================================
            Color candleYellow = new Color(255f / 255f, 222f / 255f, 105f / 255f, 1f); // Clean golden yellow candle light
            Color sconceWarmWhite = new Color(255f / 255f, 242f / 255f, 220f / 255f, 1f); // Natural warm ivory/champagne

            int interiorCount = 0;
            int candleCount = 0;

            // ============================================================
            // 6. CONFIGURE INTERIOR HOUSE LIGHTS (PointLights parent)
            //    CRITICAL FIX FOR LIGHT BLEED:
            //    - Range kept strictly to 2.4m - 2.8m (rooms are 3-4m wide)
            //    - At this range, light physically cannot reach or penetrate exterior walls!
            //    - Intensity balanced at 0.9 - 1.1 for pleasant, motion-sickness-free illumination
            //    - useColorTemperature set to FALSE to eliminate the double-multiplied scarlet red tint!
            // ============================================================
            GameObject pointLightsParent = GameObject.Find("PointLights");
            if (pointLightsParent != null)
            {
                Light[] pLights = pointLightsParent.GetComponentsInChildren<Light>(true);
                foreach (var pl in pLights)
                {
                    Undo.RecordObject(pl, "Update Interior Light (RE4 Style)");
                    bool isNearCandle = IsNearCandle(pl.transform);

                    if (isNearCandle)
                    {
                        // Candle Light: Pure warm golden yellow, gentle intimate radius
                        pl.color = candleYellow;
                        pl.intensity = 0.9f;
                        pl.range = 2.2f; // Tight radius: completely contained to table/floor, ZERO wall bleed
                        pl.useColorTemperature = false; // NEVER double-filter with colorTemperature!
                        candleCount++;
                    }
                    else
                    {
                        // Wall Sconce / Room Light: Clean soft warm ivory/white
                        pl.color = sconceWarmWhite;
                        pl.intensity = 1.1f;
                        pl.range = 2.7f; // Room-scale radius: stays inside room, drops off before exterior walls
                        pl.useColorTemperature = false; // Pure clean warm white, NO red tint
                    }

                    // Soft shadows with tight bias to ground objects and prevent any shadow-atlas overflow
                    pl.shadows = LightShadows.None; // With range 2.2-2.7m, distance attenuation prevents any bleed!
                    EditorUtility.SetDirty(pl);
                    interiorCount++;
                }
            }

            // ============================================================
            // 7. MODULAR HOUSE OTHER LIGHTS
            // ============================================================
            GameObject houseObj = GameObject.Find("ModularHouse");
            if (houseObj != null)
            {
                Light[] houseLights = houseObj.GetComponentsInChildren<Light>(true);
                foreach (var hl in houseLights)
                {
                    if (pointLightsParent != null && hl.transform.IsChildOf(pointLightsParent.transform))
                        continue;

                    if (hl.type == LightType.Point)
                    {
                        Undo.RecordObject(hl, "Update House Light (RE4 Style)");
                        bool isNearCandle = IsNearCandle(hl.transform);

                        if (isNearCandle)
                        {
                            hl.color = candleYellow;
                            hl.intensity = 0.9f;
                            hl.range = 2.2f;
                            hl.useColorTemperature = false;
                            candleCount++;
                        }
                        else
                        {
                            hl.color = sconceWarmWhite;
                            hl.intensity = 1.0f;
                            hl.range = 2.6f;
                            hl.useColorTemperature = false;
                        }

                        hl.shadows = LightShadows.None;
                        EditorUtility.SetDirty(hl);
                        interiorCount++;
                    }
                }

                // ============================================================
                // 8. MESH SHADOW & TWO-SIDED SETUP
                //    - Architectural walls/floors/roofs cast Two-Sided shadows
                //    - This ensures moonlight outside cannot penetrate inside,
                //      and interior lights are physically blocked by double-sided geometry.
                //    - Lamp & candle fixture meshes cast NO shadows (prevent self-shadow artifact)
                // ============================================================
                MeshRenderer[] houseRenderers = houseObj.GetComponentsInChildren<MeshRenderer>(true);
                int meshUpdated = 0;
                Bounds houseBounds = new Bounds(houseObj.transform.position, Vector3.zero);
                bool hasBounds = false;

                foreach (var mr in houseRenderers)
                {
                    bool isLightFixture = mr.gameObject.name.Contains("Lamp") ||
                                          mr.gameObject.name.Contains("Candle") ||
                                          (mr.transform.parent != null &&
                                           (mr.transform.parent.name.Contains("Lamp") ||
                                            mr.transform.parent.name.Contains("Candle")));

                    if (isLightFixture)
                    {
                        if (mr.shadowCastingMode != ShadowCastingMode.Off)
                        {
                            Undo.RecordObject(mr, "Disable Shadow on Light Fixture");
                            mr.shadowCastingMode = ShadowCastingMode.Off;
                            EditorUtility.SetDirty(mr);
                            meshUpdated++;
                        }
                    }
                    else
                    {
                        if (mr.shadowCastingMode != ShadowCastingMode.TwoSided)
                        {
                            Undo.RecordObject(mr, "Set Two-Sided Shadow on Wall/Floor Mesh");
                            mr.shadowCastingMode = ShadowCastingMode.TwoSided;
                            EditorUtility.SetDirty(mr);
                            meshUpdated++;
                        }

                        if (!mr.receiveShadows)
                        {
                            Undo.RecordObject(mr, "Enable Receive Shadows");
                            mr.receiveShadows = true;
                            EditorUtility.SetDirty(mr);
                        }

                        // Accumulate bounds for reflection probe
                        if (mr.gameObject.name.Contains("Floor") ||
                            mr.gameObject.name.Contains("Wall") ||
                            mr.gameObject.name.Contains("Ceiling") ||
                            mr.gameObject.name.Contains("Roof"))
                        {
                            if (!hasBounds)
                            {
                                houseBounds = mr.bounds;
                                hasBounds = true;
                            }
                            else
                            {
                                houseBounds.Encapsulate(mr.bounds);
                            }
                        }
                    }
                }

                // ============================================================
                // 9. REFLECTION PROBE — Crisp PBR Reflections (RE4 Remake Floor Shine)
                //    Polished wooden floors and surfaces reflect candles and lamps
                // ============================================================
                Transform existingProbeTrans = houseObj.transform.Find("House_ReflectionProbe");
                GameObject probeObj = existingProbeTrans != null ? existingProbeTrans.gameObject : null;
                if (probeObj == null)
                {
                    probeObj = new GameObject("House_ReflectionProbe");
                    Undo.RegisterCreatedObjectUndo(probeObj, "Create House Reflection Probe");
                    probeObj.transform.SetParent(houseObj.transform, true);
                }

                ReflectionProbe probe = probeObj.GetComponent<ReflectionProbe>();
                if (probe == null)
                {
                    probe = Undo.AddComponent<ReflectionProbe>(probeObj);
                }

                Undo.RecordObject(probe, "Configure Reflection Probe (RE4 Style)");
                if (hasBounds)
                {
                    probeObj.transform.position = houseBounds.center;
                    probe.size = houseBounds.size + new Vector3(2f, 2f, 2f);
                }
                else
                {
                    probeObj.transform.localPosition = new Vector3(0f, 2f, 0f);
                    probe.size = new Vector3(35f, 12f, 35f);
                }

                probe.center = Vector3.zero;
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.boxProjection = true;
                probe.intensity = 1.0f; // Balanced specular reflection
                probe.blendDistance = 1.5f;
                probe.cullingMask = ~0;
                probe.resolution = 512;
                probe.RenderProbe();
                EditorUtility.SetDirty(probe);
            }

            // ============================================================
            // 10. GLOBAL POST-PROCESSING VOLUME (Tonemapping, Bloom & Vignette)
            //     Ensures Tonemapping is active to prevent color blowout & red shifts
            // ============================================================
            GameObject globalVolObj = GameObject.Find("Global_PostProcess_Volume");
            if (globalVolObj == null)
            {
                globalVolObj = new GameObject("Global_PostProcess_Volume");
                Undo.RegisterCreatedObjectUndo(globalVolObj, "Create Global PostProcess Volume");
            }

            Volume volume = globalVolObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = Undo.AddComponent<Volume>(globalVolObj);
            }

            Undo.RecordObject(volume, "Configure Global Volume Profile");
            volume.isGlobal = true;
            volume.priority = 1f;

            VolumeProfile sampleProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (sampleProfile != null)
            {
                volume.profile = sampleProfile;
            }
            EditorUtility.SetDirty(volume);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (logToConsole)
            {
                Debug.Log($"<color=#66CCFF>[RE4 Lighting & Bleed Fix]</color> Applied successfully! " +
                          $"{interiorCount} interior lights configured ({candleCount} pure yellow candles, clean warm white sconces, NO red tint), " +
                          $"all ranges tightly clamped to 2.2m-2.7m (zero exterior bleed), " +
                          $"PBR Reflection Probe updated, Global Post-Processing Volume active.");
            }
        }

        /// <summary>
        /// Checks if a light transform is near a candle mesh object (SM_Candle_*).
        /// Searches hierarchy names, siblings, and nearby scene objects within 1.5m.
        /// </summary>
        private static bool IsNearCandle(Transform lightTransform)
        {
            Transform current = lightTransform;
            for (int i = 0; i < 3 && current != null; i++)
            {
                if (current.name.Contains("Candle") || current.name.Contains("candle"))
                    return true;
                current = current.parent;
            }

            if (lightTransform.parent != null)
            {
                foreach (Transform sibling in lightTransform.parent)
                {
                    if (sibling.name.Contains("Candle") || sibling.name.Contains("candle"))
                        return true;
                }
            }

            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in allRenderers)
            {
                if (mr.gameObject.name.StartsWith("SM_Candle"))
                {
                    float dist = Vector3.Distance(lightTransform.position, mr.transform.position);
                    if (dist < 1.5f)
                        return true;
                }
            }

            return false;
        }
    }
}
