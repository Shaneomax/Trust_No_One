using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using V0.UI;

namespace V0.Editor
{
    public static class SetupGoodEndingScene
    {
        [MenuItem("Tools/Bake Ending Screen to GoodEnding Scene", false, 70)]
        [MenuItem("Tools/Trust No One/Bake Ending Screen to GoodEnding Scene", false, 70)]
        public static void BakeEndingScreen()
        {
            string scenePath = "Assets/V0/Scene/GoodEnding.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Set Camera background to solid black
            Camera cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                EditorUtility.SetDirty(cam);
            }

            // 2. Ensure EndingManager exists in the scene
            GameObject host = GameObject.Find("EndingManager");
            if (host == null)
            {
                host = new GameObject("EndingManager");
                host.AddComponent<EndingManager>();
                Undo.RegisterCreatedObjectUndo(host, "Create EndingManager");
            }
            else
            {
                if (host.GetComponent<EndingManager>() == null)
                {
                    host.AddComponent<EndingManager>();
                }
            }

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=green><b>[SetupGoodEndingScene]</b></color> Successfully baked EndingManager into GoodEnding scene!");
            EditorUtility.DisplayDialog("Ending Scene Ready",
                "Successfully baked EndingManager into GoodEnding scene!\n\n" +
                "1. Camera background set to solid black.\n" +
                "2. EndingManager component attached.\n" +
                "3. Handles all 3 endings, text fades, and continue button to MainMenu.",
                "OK");
        }
    }
}
