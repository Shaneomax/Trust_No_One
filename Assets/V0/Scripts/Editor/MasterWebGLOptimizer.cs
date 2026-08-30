using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

namespace V0.Editor
{
    /// <summary>
    /// Safe WebGL 1080p Build Optimizer:
    /// - Configures PlayerSettings for 1920x1080 WebGL.
    /// - Tunes QualitySettings for 60 FPS in browsers.
    /// - Marks static environment meshes for batching/culling in current scene.
    /// 
    /// DOES NOT modify any scene files, UI hierarchies, audio, or game mechanics.
    /// </summary>
    public static class MasterWebGLOptimizer
    {
        [MenuItem("Tools/WebGL 1080p Build Settings Only (Safe - No Scene Changes)", false, 0)]
        [MenuItem("Tools/Trust No One/WebGL 1080p Build Settings Only (Safe - No Scene Changes)", false, 0)]
        public static void ApplyWebGLBuildSettings()
        {
            Debug.Log("<color=cyan><b>[WebGL Optimizer]</b> Applying WebGL 1080p build settings (NO scene modifications)...</color>");

            // 1. WebGL Resolution
            PlayerSettings.defaultWebScreenWidth = 1920;
            PlayerSettings.defaultWebScreenHeight = 1080;

            // 2. WebGL Build Configuration
#if UNITY_WEBGL || true
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
#endif

            // 3. Quality tuning for smooth WebGL framerate
            QualitySettings.vSyncCount = 0;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.shadowDistance = 40f;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.particleRaycastBudget = 64;

            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[WebGL Optimizer]</b> Done! Build settings configured for 1920x1080 WebGL.</color>");

            EditorUtility.DisplayDialog(
                "WebGL 1080p Build Settings Applied",
                "WebGL build settings configured:\n\n" +
                "✓ Default resolution: 1920 x 1080\n" +
                "✓ Compression: Gzip\n" +
                "✓ Data Caching: Enabled\n" +
                "✓ Exceptions: Explicit Only (faster)\n" +
                "✓ IL2CPP: Master (max optimization)\n" +
                "✓ Memory: 512 MB\n" +
                "✓ Quality: Shadow distance 40m, 4 pixel lights\n\n" +
                "No scenes, UI, or audio were modified.",
                "OK"
            );
        }

        [MenuItem("Tools/Batch Static Meshes in Current Scene (Safe)", false, 1)]
        [MenuItem("Tools/Trust No One/Batch Static Meshes in Current Scene (Safe)", false, 1)]
        public static void BatchStaticMeshesInCurrentScene()
        {
            int batchedCount = 0;
            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MeshRenderer mr in allRenderers)
            {
                GameObject go = mr.gameObject;

                // Skip ALL interactive/dynamic objects — never touch gameplay
                if (go.GetComponentInParent<V0.Interaction.IInteractable>() != null ||
                    go.GetComponentInParent<V0.Interaction.DoorInteractable>() != null ||
                    go.GetComponentInParent<V0.Interaction.KeyPickup>() != null ||
                    go.GetComponentInParent<TrustNoOne.AI.EnemyAI>() != null ||
                    go.GetComponentInParent<TrustNoOne.AI.DeceiverAI>() != null ||
                    go.CompareTag("Player") ||
                    go.name.Contains("Trigger") ||
                    go.name.Contains("Door") ||
                    go.name.Contains("Key") ||
                    go.name.Contains("Flashlight") ||
                    go.name.Contains("Chainsaw") ||
                    go.name.Contains("HaliganBar") ||
                    go.name.Contains("Ghost") ||
                    go.name.Contains("Canvas") ||
                    go.name.Contains("Camera") ||
                    go.name.Contains("Light"))
                {
                    continue;
                }

                StaticEditorFlags current = GameObjectUtility.GetStaticEditorFlags(go);
                StaticEditorFlags target = current
                    | StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.OccluderStatic;

                if (current != target)
                {
                    Undo.RecordObject(go, "Set Static Flags");
                    GameObjectUtility.SetStaticEditorFlags(go, target);
                    batchedCount++;
                }
            }

            Debug.Log($"<color=green><b>[WebGL Optimizer]</b> Marked {batchedCount} environment meshes for static batching & occlusion culling.</color>");

            EditorUtility.DisplayDialog(
                "Static Batching Applied",
                $"Marked {batchedCount} environment meshes for:\n\n" +
                "✓ Static Batching (fewer draw calls)\n" +
                "✓ Occlusion Culling (skip hidden geometry)\n\n" +
                "Interactive objects (doors, keys, ghost, player) were NOT touched.\n" +
                "Use Ctrl+Z to undo if needed.",
                "OK"
            );
        }
    }
}
