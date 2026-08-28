using UnityEditor;
using UnityEngine;
using V0.Interaction;

namespace V0.Editor
{
    public static class DoorPresetsHelper
    {
        [MenuItem("Tools/Presets/Apply 2nd Floor Bedroom Door (Crowbar Clue)", false, 70)]
        public static void ApplyBedroomDoorPreset()
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null)
            {
                EditorUtility.DisplayDialog("No Door Selected", "Please select a door GameObject in the Hierarchy (e.g. SM_Door_interior_01_LOD0) first!", "OK");
                return;
            }

            DoorInteractable door = selectedObj.GetComponent<DoorInteractable>();
            if (door == null) door = selectedObj.GetComponentInParent<DoorInteractable>();

            if (door == null)
            {
                EditorUtility.DisplayDialog("No DoorInteractable Found", $"The selected object '{selectedObj.name}' does not have a DoorInteractable component.", "OK");
                return;
            }

            door.ApplyPresetBedroomDoorCrowbarClue();
            EditorUtility.DisplayDialog("Preset Applied!", $"Successfully applied '2nd Floor Bedroom Door (Crowbar Clue)' preset to '{selectedObj.name}'!\n\nPrompts set to 'Stuck (Need Crowbar)' / 'Pry Open Door' and dialogue clues updated.", "Awesome");
        }

        [MenuItem("Tools/Presets/Apply Downstairs Door (Bedroom Key Clue)", false, 71)]
        public static void ApplyDownstairsDoorPreset()
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null)
            {
                EditorUtility.DisplayDialog("No Door Selected", "Please select a door GameObject in the Hierarchy (e.g. SM_Door_interior_01_LOD0) first!", "OK");
                return;
            }

            DoorInteractable door = selectedObj.GetComponent<DoorInteractable>();
            if (door == null) door = selectedObj.GetComponentInParent<DoorInteractable>();

            if (door == null)
            {
                EditorUtility.DisplayDialog("No DoorInteractable Found", $"The selected object '{selectedObj.name}' does not have a DoorInteractable component.", "OK");
                return;
            }

            door.ApplyPresetDownstairsDoorKeyClue();
            EditorUtility.DisplayDialog("Preset Applied!", $"Successfully applied 'Downstairs Door (Bedroom Key Clue)' preset to '{selectedObj.name}'!\n\nPrompts set to 'Locked (Need Key)' / 'Unlock Door' and dialogue clues updated.", "Awesome");
        }
    }
}
