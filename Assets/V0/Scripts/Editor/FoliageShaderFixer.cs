using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace V0.Editor
{
    /// <summary>
    /// Converts all HDRP / unclipped Foliage & Plant materials to Universal Render Pipeline/Lit
    /// with Alpha Clipping, Double-Sided rendering, and proper cutout thresholds so leaves and grass
    /// render crisp transparency without solid black/grey square quads.
    /// </summary>
    [InitializeOnLoad]
    public static class FoliageShaderFixer
    {
        static FoliageShaderFixer()
        {
            EditorApplication.delayCall += AutoFixOnLoad;
        }

        private static void AutoFixOnLoad()
        {
            // Only auto-run once per editor session
            if (SessionState.GetBool("FoliageShaderFixer_Run", false)) return;
            SessionState.SetBool("FoliageShaderFixer_Run", true);

            FixAllFoliageMaterials(silent: true);
        }

        [MenuItem("Tools/Fix Foliage & Plant Alpha Transparency (URP)")]
        public static void FixAllFoliageMaterialsMenu()
        {
            FixAllFoliageMaterials(silent: false);
        }

        public static void FixAllFoliageMaterials(bool silent)
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("[FoliageShaderFixer] Could not find 'Universal Render Pipeline/Lit' shader!");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            int fixedCount = 0;

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string lowerPath = path.ToLower();
                string lowerName = mat.name.ToLower();
                string shaderName = mat.shader != null ? mat.shader.name : "";

                bool isFoliageMat = lowerPath.Contains("foliage")
                                 || lowerPath.Contains("grass")
                                 || lowerPath.Contains("wildgrass")
                                 || lowerPath.Contains("beech")
                                 || lowerPath.Contains("wheat")
                                 || lowerPath.Contains("tree")
                                 || lowerPath.Contains("plant")
                                 || lowerPath.Contains("leaf")
                                 || lowerPath.Contains("weed")
                                 || lowerName.Contains("foliage")
                                 || lowerName.Contains("grass")
                                 || lowerName.Contains("beech")
                                 || lowerName.Contains("wheat")
                                 || lowerName.Contains("plant")
                                 || lowerName.Contains("straw")
                                 || lowerName.Contains("hay")
                                 || shaderName.Contains("Foliage")
                                 || shaderName.Contains("S_Foliage");

                if (!isFoliageMat) continue;

                Undo.RecordObject(mat, "Fix Foliage Alpha Cutout");

                // Get existing textures before changing shader
                Texture mainTex = mat.GetTexture("_BaseMap")
                               ?? mat.GetTexture("_MainTex")
                               ?? mat.GetTexture("_BaseColorMap");

                Texture normalMap = mat.GetTexture("_BumpMap")
                                 ?? mat.GetTexture("_NormalMap");

                Texture maskMap = mat.GetTexture("_MaskMap")
                               ?? mat.GetTexture("_Mask");

                // Assign URP Lit Shader
                mat.shader = urpLitShader;

                // Reassign textures
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (maskMap != null) mat.SetTexture("_MetallicGlossMap", maskMap);

                // Configure URP Lit Alpha Cutout
                mat.SetFloat("_Surface", 0.0f); // 0 = Opaque (with Alpha Clip)
                mat.SetFloat("_Blend", 0.0f);
                mat.SetFloat("_AlphaClip", 1.0f); // 1 = Enable Alpha Clipping
                mat.SetFloat("_Cutoff", 0.35f); // 0.35 = Clean cutout threshold
                mat.SetFloat("_Cull", 0.0f); // 0 = Off (Double-Sided so leaves render from both sides)
                mat.SetFloat("_ReceiveShadows", 1.0f);
                mat.SetFloat("_Smoothness", 0.15f); // Lower smoothness for natural matte foliage

                mat.EnableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_DOUBLESIDED_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; // 2450

                EditorUtility.SetDirty(mat);
                fixedCount++;
                Debug.Log($"<color=green>[FoliageShaderFixer]</color> Fixed alpha cutout on: {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.DisplayDialog("Foliage Alpha Fix Complete", $"Successfully converted and fixed {fixedCount} foliage materials with URP Alpha Clipping & Double-Sided rendering!", "OK");
            }
            Debug.Log($"<color=cyan><b>[FoliageShaderFixer]</b> Successfully fixed {fixedCount} foliage & plant materials!</color>");
        }
    }
}
