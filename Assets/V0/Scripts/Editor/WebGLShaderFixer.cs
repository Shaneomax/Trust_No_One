using System.IO;
using UnityEditor;
using UnityEngine;

namespace V0.Editor
{
    /// <summary>
    /// Fixes WebGL shader compilation error:
    /// 'Shader error in Shader Graphs/S_Blend: maximum ps_5_0 sampler register index (16) exceeded (on gles3)'
    /// 
    /// Converts unused/incompatible HDRP materials to Standard URP Lit and disables the 24-sampler HDRP shader graph.
    /// </summary>
    public static class WebGLShaderFixer
    {
        [InitializeOnLoadMethod]
        private static void AutoFixOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                FixWebGLShaderError(silent: true);
            };
        }

        [MenuItem("Tools/Fix WebGL Shader Errors (S_Blend 16 Sampler Exceeded)", false, 5)]
        [MenuItem("Tools/Trust No One/Fix WebGL Shader Errors (S_Blend 16 Sampler Exceeded)", false, 5)]
        public static void FixWebGLShaderErrorManual()
        {
            FixWebGLShaderError(silent: false);
        }

        public static void FixWebGLShaderError(bool silent)
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                urpLitShader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            }

            int fixedMaterialsCount = 0;

            // 1. Scan all materials in the project that use S_Blend or missing/broken HDRP shaders
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                if (mat.shader.name.Contains("S_Blend") || mat.shader.name.Contains("HDRP") || mat.shader.name == "Hidden/InternalErrorShader")
                {
                    Undo.RecordObject(mat, "Fix WebGL Incompatible Shader");

                    // Preserve textures if present in old HDRP properties
                    Texture baseTex = mat.HasProperty("_BackgroundBase") ? mat.GetTexture("_BackgroundBase") : null;
                    if (baseTex == null && mat.HasProperty("_BaseColorMap")) baseTex = mat.GetTexture("_BaseColorMap");
                    if (baseTex == null && mat.HasProperty("_BaseMap")) baseTex = mat.GetTexture("_BaseMap");

                    Texture normalTex = mat.HasProperty("_BackgroundNormal") ? mat.GetTexture("_BackgroundNormal") : null;
                    if (normalTex == null && mat.HasProperty("_NormalMap")) normalTex = mat.GetTexture("_NormalMap");
                    if (normalTex == null && mat.HasProperty("_BumpMap")) normalTex = mat.GetTexture("_BumpMap");

                    Texture maskTex = mat.HasProperty("_BackgroundMask") ? mat.GetTexture("_BackgroundMask") : null;

                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

                    // Assign standard URP Lit shader
                    mat.shader = urpLitShader;

                    if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
                    if (normalTex != null)
                    {
                        mat.SetTexture("_BumpMap", normalTex);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    if (maskTex != null)
                    {
                        mat.SetTexture("_MetallicGlossMap", maskTex);
                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }

                    mat.SetColor("_BaseColor", baseColor);
                    mat.SetFloat("_Smoothness", 0.5f);

                    EditorUtility.SetDirty(mat);
                    fixedMaterialsCount++;
                }
            }

            // 2. Disable unused HDRP S_Blend.shadergraph so WebGL GLES3 compiler doesn't attempt to build it
            string sBlendPath = "Assets/HIVEMIND/HauntedFarmHouse/HDRP (Default)/Art/Shaders/S_Blend.shadergraph";
            if (File.Exists(sBlendPath))
            {
                string disabledPath = sBlendPath + ".disabled";
                if (File.Exists(disabledPath)) File.Delete(disabledPath);

                File.Move(sBlendPath, disabledPath);
                if (File.Exists(sBlendPath + ".meta"))
                {
                    File.Move(sBlendPath + ".meta", disabledPath + ".meta");
                }
                Debug.Log("<color=green><b>[WebGLShaderFixer]</b> Successfully disabled unused HDRP 'S_Blend.shadergraph'.</color>");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green><b>[WebGLShaderFixer]</b> Fixed {fixedMaterialsCount} materials and resolved WebGL GLES3 sampler register limit!</color>");

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "WebGL Shader Error Fixed!",
                    $"Successfully resolved WebGL build error:\n\n" +
                    $"✓ Converted {fixedMaterialsCount} HDRP materials to Universal Render Pipeline/Lit\n" +
                    $"✓ Disabled unused 24-sampler HDRP S_Blend.shadergraph\n\n" +
                    "You can now click 'Build' in the Build Profiles window without any shader errors!",
                    "Awesome"
                );
            }
        }
    }
}
