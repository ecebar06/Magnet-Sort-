#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WebLevelJsonImporter
{
    private const string LevelFolder = "Assets/_Game/Resources/Data/Levels";

    [MenuItem("Tools/Word Game/Import Web Level JSON")]
    private static void Import()
    {
        string sourcePath = EditorUtility.OpenFilePanel("Import Web Level JSON", "", "json");
        if (string.IsNullOrEmpty(sourcePath)) return;

        LevelData source = null;
        try
        {
            source = LevelData.FromJson(File.ReadAllText(sourcePath));
        }
        catch (System.Exception exception)
        {
            EditorUtility.DisplayDialog("Import Failed", exception.Message, "OK");
            return;
        }

        if (source == null || source.levelNumber < 1 || source.categories == null)
        {
            EditorUtility.DisplayDialog("Import Failed", "This is not a valid Word Sort level JSON file.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(LevelFolder))
            Directory.CreateDirectory(Path.GetFullPath(LevelFolder));

        source.moveCount = Mathf.Max(1, source.moveCount);
        source.expectedCategoryCount = Mathf.Max(0, source.expectedCategoryCount);
        source.visibleRowCount = Mathf.Max(0, source.visibleRowCount);
        source.categories ??= new List<Category>();
        source.orderedWords ??= new List<LevelWordEntry>();

        if (!source.IsValid(out string error))
        {
            Object.DestroyImmediate(source);
            EditorUtility.DisplayDialog("Import Failed", error, "OK");
            return;
        }

        string assetPath = $"{LevelFolder}/Level_{source.levelNumber:000}.json";
        File.WriteAllText(Path.GetFullPath(assetPath), source.ToJson());
        AssetDatabase.Refresh();
        TextAsset imported = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        Selection.activeObject = imported;
        EditorGUIUtility.PingObject(imported);
        EditorUtility.DisplayDialog("Level Imported", $"Level {source.levelNumber} JSON was imported successfully.", "OK");
        Object.DestroyImmediate(source);
    }
}
#endif
