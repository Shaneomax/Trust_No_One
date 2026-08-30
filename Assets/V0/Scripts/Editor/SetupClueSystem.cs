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

            clueSystem.AutoWireReferences();

            EditorUtility.SetDirty(clueObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[SetupClueSystem]</b></color> Successfully configured Key Clue & Hint System on <b>'Clue'</b> GameObject!");
            EditorUtility.DisplayDialog("Key Clue System Setup",
                "Successfully configured Key Clue System on 'Clue'!\n\n" +
                "Features:\n" +
                "1. Tracks: BedRoomKey -> DrawingRoomKey -> ChainSaw.\n" +
                "2. Timer: Triggers every 60 seconds after SecondTrigger if the current key is not found.\n" +
                "3. Dialogue: Player mumbles/thinks clues pointing to the room.\n" +
                "4. Beacon: Soft visual glow marks the key location during the hint.\n" +
                "5. Advances & resets timer upon key pickup.",
                "OK");
        }
    }
}
