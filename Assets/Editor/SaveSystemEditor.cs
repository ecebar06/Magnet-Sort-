#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SaveSystemEditor
{
    [MenuItem("Tools/Word Game/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(SaveSystem.SavePath);
    }

    [MenuItem("Tools/Word Game/Delete Save Data")]
    private static void DeleteSaveData()
    {
        if (!EditorUtility.DisplayDialog("Delete Save Data",
                "Delete the player's local progress and reset to Level 1?", "Delete", "Cancel"))
            return;

        SaveSystem.DeleteSaveData();
        Debug.Log("Player save data deleted. The next game starts from Level 1.");
    }
}
#endif
