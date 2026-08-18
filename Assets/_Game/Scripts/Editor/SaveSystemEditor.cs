#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SaveSystem))]
public class SaveSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Device Storage", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Save Inspector Values"))
                SaveSystem.Save();

            if (GUILayout.Button("Reload From Device"))
            {
                SaveSystem.Load();
                Repaint();
            }
        }

        EditorGUILayout.HelpBox(
            "Enter Play Mode, select SaveSystem, edit Player Save Data, then press Save Inspector Values.",
            MessageType.Info);
    }

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
