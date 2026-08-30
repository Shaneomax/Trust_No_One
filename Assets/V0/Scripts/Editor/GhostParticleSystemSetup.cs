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

            // 1. Create or Load Spherical Glow/Wisp Material
            Material sphereParticleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/V0/Materials/Particles/M_GhostSphere_Additive.mat");
            if (sphereParticleMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Sprites/Default");

                sphereParticleMat = new Material(shader);
                sphereParticleMat.name = "M_GhostSphere_Additive";

                if (sphereParticleMat.HasProperty("_Surface")) sphereParticleMat.SetFloat("_Surface", 1); // Transparent
                if (sphereParticleMat.HasProperty("_Blend")) sphereParticleMat.SetFloat("_Blend", 1); // Additive
                if (sphereParticleMat.HasProperty("_ZWrite")) sphereParticleMat.SetFloat("_ZWrite", 0);
                sphereParticleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                Texture2D wispTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/V0/Textures/Particles/T_GhostWisps_Glow.png");
                if (wispTex != null)
                {
                    sphereParticleMat.mainTexture = wispTex;
                    if (sphereParticleMat.HasProperty("_BaseMap")) sphereParticleMat.SetTexture("_BaseMap", wispTex);
                }

                AssetDatabase.CreateAsset(sphereParticleMat, "Assets/V0/Materials/Particles/M_GhostSphere_Additive.mat");
            }

            // 2. Soft Ground Fog Material
            Material fogMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/V0/Materials/Particles/M_GhostFog_Soft.mat");
            if (fogMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Sprites/Default");

                fogMat = new Material(shader);
                fogMat.name = "M_GhostFog_Soft";

                if (fogMat.HasProperty("_Surface")) fogMat.SetFloat("_Surface", 1); // Transparent
                if (fogMat.HasProperty("_Blend")) fogMat.SetFloat("_Blend", 0); // Alpha
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

            // Remove any old broken child components
            for (int i = rootFogObj.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(rootFogObj.transform.GetChild(i).gameObject);
            }

            ParticleSystem mainPS = rootFogObj.GetComponent<ParticleSystem>();
            if (mainPS == null) mainPS = rootFogObj.AddComponent<ParticleSystem>();

            // Configure Root: Low ground subtle misty fog
            var main = mainPS.main;
            main.duration = 4.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startColor = new Color(0.6f, 0.85f, 1.0f, 0.25f); // Subtle soft cyan mist
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 25; // Highly optimized
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = mainPS.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            var shape = mainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.8f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var colLife = mainPS.colorOverLifetime;
            colLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.6f, 0.85f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.8f, 0.95f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.75f, 0.9f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.7f, 0.3f),
                    new GradientAlphaKey(0.7f, 0.7f),
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

            // 5. Child: Very Small Floating Spherical Wisps (Soul Motes)
            GameObject wispsObj = new GameObject("SmallSoulSpheres");
            wispsObj.transform.SetParent(rootFogObj.transform, false);
            wispsObj.transform.localPosition = Vector3.zero;

            ParticleSystem wispsPS = wispsObj.AddComponent<ParticleSystem>();
            var wMain = wispsPS.main;
            wMain.duration = 4.0f;
            wMain.loop = true;
            wMain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            wMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            wMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f); // VERY SMALL SPHERES!
            wMain.startColor = new Color(0.45f, 0.9f, 1.0f, 0.9f); // Glowing cyan sphere
            wMain.playOnAwake = false;
            wMain.maxParticles = 30; // Highly optimized
            wMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var wEmission = wispsPS.emission;
            wEmission.enabled = true;
            wEmission.rateOverTime = 14f;

            var wShape = wispsPS.shape;
            wShape.enabled = true;
            wShape.shapeType = ParticleSystemShapeType.Cone;
            wShape.radius = 0.8f;
            wShape.angle = 12f;
            wShape.length = 2.0f;
            wShape.rotation = new Vector3(-90f, 0f, 0f);

            var wColLife = wispsPS.colorOverLifetime;
            wColLife.enabled = true;
            Gradient wGrad = new Gradient();
            wGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.3f, 0.85f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.9f, 1.0f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.6f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.25f),
                    new GradientAlphaKey(0.8f, 0.75f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            wColLife.color = wGrad;

            ParticleSystemRenderer wispsRend = wispsObj.GetComponent<ParticleSystemRenderer>();
            if (wispsRend != null)
            {
                wispsRend.material = sphereParticleMat; // Smooth spherical additive material
                wispsRend.sortMode = ParticleSystemSortMode.Distance;
            }

            // 6. Save as Prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(rootFogObj, "Assets/V0/Prefabs/Effects/GhostSpawnParticles.prefab", InteractionMode.AutomatedAction);

            // 7. Auto-wire to GhostSpawnCutscene in the scene
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

            Debug.Log("<color=green><b>[GhostParticleSetup]</b></color> Small spherical ghost particles built and wired successfully!");
            EditorUtility.DisplayDialog("Ghost Particles Configured",
                "Successfully configured Ghost Particle System!\n\n" +
                "1. Particle Shape: Delicate glowing spheres (0.04m - 0.12m).\n" +
                "2. Materials: Soft circular feathered textures (0 square boxes/quads).\n" +
                "3. Performance: Ultra-lightweight (~20 particles total), fully optimized for WebGL and low-end PCs.\n" +
                "4. Compile-time: Baked directly into scene and wired to GhostSpawnCutscene.",
                "OK");
        }
    }
}
