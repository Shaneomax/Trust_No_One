using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LineworkLite.FreeOutline;

namespace V0.EditorTools
{
    [InitializeOnLoad]
    public static class FreeOutlineSetup
    {
        static FreeOutlineSetup()
        {
            EditorApplication.delayCall += EnsureOutlineConfigured;
        }

        [MenuItem("Tools/Trust No One/Setup Free Outline for Interactables")]
        public static void EnsureOutlineConfigured()
        {
            // 1. Load Free Outline Settings asset
            string settingsPath = "Assets/Free Outline Settings.asset";
            FreeOutlineSettings settings = AssetDatabase.LoadAssetAtPath<FreeOutlineSettings>(settingsPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FreeOutlineSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }

            // 2. Check if it has an Outline profile
            SerializedObject so = new SerializedObject(settings);
            SerializedProperty outlinesProp = so.FindProperty("outlines");

            if (outlinesProp.arraySize == 0)
            {
                Outline outline = ScriptableObject.CreateInstance<Outline>();
                outline.name = "InteractableOutline";
                outline.color = new Color(1f, 0.85f, 0.2f, 1f); // Warm yellow/gold highlight
                outline.width = 18f;
                outline.layerMask = LayerMask.GetMask("Interactable");
#if UNITY_6000_0_OR_NEWER
                outline.RenderingLayer = (RenderingLayerMask)2; // Layer 2 / Light Layer 1
#else
                outline.RenderingLayer = 2;
#endif

                AssetDatabase.AddObjectToAsset(outline, settings);

                outlinesProp.arraySize = 1;
                outlinesProp.GetArrayElementAtIndex(0).objectReferenceValue = outline;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=green>[FreeOutlineSetup]</color> Created and configured InteractableOutline profile in Free Outline Settings!");
            }

            // 3. Make sure PC_Renderer and Mobile_Renderer have Free Outline assigned
            string[] renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
            foreach (string path in renderers)
            {
                var rendererObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (rendererObj != null)
                {
                    EditorUtility.SetDirty(rendererObj);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
