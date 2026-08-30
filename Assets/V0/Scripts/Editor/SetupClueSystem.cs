using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using V0.Gameplay;
using V0.Interaction;

namespace V0.Editor
{
    public static class SetupClueSystem
    {
        [MenuItem("Tools/Setup Key Clue System", false, 59)]
        [MenuItem("Tools/Trust No One/Setup Key Clue System", false, 59)]
        public static void ConfigureClueSystem()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Key Clue System");

            GameObject clueObj = GameObject.Find("Clue");
            if (clueObj == null)
            {
                clueObj = new GameObject("Clue");
                Undo.RegisterCreatedObjectUndo(clueObj, "Create Clue GameObject");
            }

            KeyClueSystem clueSystem = clueObj.GetComponent<KeyClueSystem>();
            if (clueSystem == null)
            {
                clueSystem = Undo.AddComponent<KeyClueSystem>(clueObj);
            }

            EditorUtility.SetDirty(clueObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[SetupClueSystem]</b></color> Successfully configured Key Clue & Hint System on <b>'Clue'</b> GameObject!");
            EditorUtility.DisplayDialog("Key Clue System Setup",
                "Successfully configured Key Clue System on 'Clue'!\n\n" +
                "Narrative Sequence:\n" +
                "1. Search for Chainsaw Room outside\n" +
                "2. Chainsaw room locked -> Search 2nd-floor Bedroom\n" +
                "3. Bedroom locked -> Search Drawing Room Key downstairs\n" +
                "4. Got Drawing Room Key -> Get Bedroom Key\n" +
                "5. Got Bedroom Key -> Get Chainsaw\n" +
                "6. Got Chainsaw -> Cut chains on door\n\n" +
                "Hints pop up every 60s (or your test duration) if player gets stuck.",
                "OK");
        }
    }
}
