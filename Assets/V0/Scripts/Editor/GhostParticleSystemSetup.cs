using System.IO;
using UnityEngine;
using UnityEditor;
using V0.Cinematics;

namespace V0.Editor
{
    public static class GhostParticleSystemSetup
    {
        [MenuItem("Tools/Setup Ghost Spawn Particles", false, 60)]
        [MenuItem("Tools/Trust No One/Setup Ghost Spawn Particles", false, 60)]
        public static void CreateAndWireGhostParticles()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Ghost Spawn Particles");

            // Ensure directories exist
            Directory.CreateDirectory("Assets/V0/Materials/Particles");
            Directory.CreateDirectory("Assets/V0/Prefabs/Effects");
            AssetDatabase.Refresh();

            // 1. Create or Load Fog Material
            Material fogMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/V0/Materials/Particles/M_GhostFog_Soft.mat");
            if (fogMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Sprites/Default");
                fogMat = new Material(shader);
                fogMat.name = "M_GhostFog_Soft";

                if (fogMat.HasProperty("_Surface")) fogMat.SetFloat("_Surface", 1);
                if (fogMat.HasProperty("_Blend")) fogMat.SetFloat("_Blend", 0);
                if (fogMat.HasProperty("_ZWrite")) fogMat.SetFloat("_ZWrite", 0);
                fogMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                Texture2D smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/V0/Textures/Particles/T_GhostSmoke_Soft.png");
                if (smokeTex != null)
                {
                    fogMat.mainTexture = smokeTex;
                    if (fogMat.HasProperty("_BaseMap")) fogMat.SetTexture("_BaseMap", smokeTex);
                }

                AssetDatabase.CreateAsset(fogMat, "Assets/V0/Materials/Particles/M_GhostFog_Soft.mat");
            }

            // 2. Create or Load Wisps Material
            Material wispMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/V0/Materials/Particles/M_GhostWisps_Additive.mat");
            if (wispMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Sprites/Default");
                wispMat = new Material(shader);
                wispMat.name = "M_GhostWisps_Additive";

                if (wispMat.HasProperty("_Surface")) wispMat.SetFloat("_Surface", 1);
                if (wispMat.HasProperty("_Blend")) wispMat.SetFloat("_Blend", 1);
                if (wispMat.HasProperty("_ZWrite")) wispMat.SetFloat("_ZWrite", 0);
                wispMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                Texture2D wispTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/V0/Textures/Particles/T_GhostWisps_Glow.png");
                if (wispTex != null)
                {
                    wispMat.mainTexture = wispTex;
                    if (wispMat.HasProperty("_BaseMap")) wispMat.SetTexture("_BaseMap", wispTex);
                }

                AssetDatabase.CreateAsset(wispMat, "Assets/V0/Materials/Particles/M_GhostWisps_Additive.mat");
            }

            AssetDatabase.SaveAssets();

            // 3. Locate Ghost Spawn Position
            Vector3 spawnPos = new Vector3(4.98f, 0.15f, 2.0f);
            GameObject ghostObj = GameObject.Find("Ghost");
            if (ghostObj != null)
            {
                spawnPos = ghostObj.transform.position;
                spawnPos.y += 0.05f;
            }

            // 4. Create or Update Root Particle System: GhostSpawnFog
            GameObject rootFogObj = GameObject.Find("GhostSpawnFog");
            if (rootFogObj == null)
            {
                rootFogObj = new GameObject("GhostSpawnFog");
                Undo.RegisterCreatedObjectUndo(rootFogObj, "Create GhostSpawnFog");
            }

            rootFogObj.transform.position = spawnPos;
            rootFogObj.transform.rotation = Quaternion.identity;

            for (int i = rootFogObj.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(rootFogObj.transform.GetChild(i).gameObject);
            }

            ParticleSystem mainPS = rootFogObj.GetComponent<ParticleSystem>();
            if (mainPS == null) mainPS = rootFogObj.AddComponent<ParticleSystem>();

            var main = mainPS.main;
            main.duration = 4.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
            main.startColor = new Color(0.65f, 0.85f, 1.0f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 25;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = mainPS.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            var shape = mainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var colLife = mainPS.colorOverLifetime;
            colLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.6f, 0.85f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.85f, 0.95f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.75f, 0.9f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.8f, 0.25f),
                    new GradientAlphaKey(0.8f, 0.75f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colLife.color = grad;

            ParticleSystemRenderer mainRend = rootFogObj.GetComponent<ParticleSystemRenderer>();
            if (mainRend != null)
            {
                mainRend.material = fogMat;
                mainRend.sortMode = ParticleSystemSortMode.Distance;
            }

            // 5. Child: Rising Soul Wisps
            GameObject wispsObj = new GameObject("RisingSoulWisps");
            wispsObj.transform.SetParent(rootFogObj.transform, false);
            wispsObj.transform.localPosition = Vector3.zero;

            ParticleSystem wispsPS = wispsObj.AddComponent<ParticleSystem>();
            var wMain = wispsPS.main;
            wMain.duration = 4.0f;
            wMain.loop = true;
            wMain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
            wMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            wMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            wMain.startColor = new Color(0.45f, 0.9f, 1.0f, 0.85f);
            wMain.playOnAwake = false;
            wMain.maxParticles = 25;
            wMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var wEmission = wispsPS.emission;
            wEmission.enabled = true;
            wEmission.rateOverTime = 12f;

            var wShape = wispsPS.shape;
            wShape.enabled = true;
            wShape.shapeType = ParticleSystemShapeType.Cone;
            wShape.radius = 0.8f;
            wShape.angle = 12f;
            wShape.length = 2.0f;
            wShape.rotation = new Vector3(-90f, 0f, 0f);

            var wColLife = wispsPS.colorOverLifetime;
            wColLife.enabled = true;
            wColLife.color = grad;

            ParticleSystemRenderer wispsRend = wispsObj.GetComponent<ParticleSystemRenderer>();
            if (wispsRend != null)
            {
                wispsRend.material = wispMat;
                wispsRend.sortMode = ParticleSystemSortMode.Distance;
            }

            // 6. Save as Prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(rootFogObj, "Assets/V0/Prefabs/Effects/GhostSpawnParticles.prefab", InteractionMode.AutomatedAction);

            // 7. Auto-wire to GhostSpawnCutscene in scene
            GhostSpawnCutscene cutscene = Object.FindFirstObjectByType<GhostSpawnCutscene>();
            if (cutscene != null)
            {
                Undo.RecordObject(cutscene, "Assign GhostSpawnFog");
                SerializedObject so = new SerializedObject(cutscene);
                SerializedProperty fogProp = so.FindProperty("_ghostSpawnFog");
                if (fogProp != null)
                {
                    fogProp.objectReferenceValue = mainPS;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(cutscene);
                    Debug.Log("<color=green><b>[GhostParticleSetup]</b></color> Successfully wired <b>GhostSpawnFog</b> to GhostSpawnCutscene slot!");
                }
            }

            EditorUtility.SetDirty(rootFogObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[GhostParticleSetup]</b></color> Ghost spawn particle effect restored and wired successfully!");
            EditorUtility.DisplayDialog("Ghost Particles Restored",
                "Successfully restored the previous atmospheric particle setup!\n\n" +
                "1. Ground Eerie Fog: Soft rolling cyan/spectral mist.\n" +
                "2. Rising Soul Wisps: Floating glowing ethereal motes.\n" +
                "3. Wired to GhostSpawnCutscene slot.",
                "OK");
        }
    }
}
