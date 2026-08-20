#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WebLevelJsonImporter
{
    private const string LevelFolder = "Assets/_Game/Resources/Data/Levels";
    private const string DatabasePath = "Assets/_Game/Resources/Data/LevelDatabase.asset";

    [System.Serializable]
    private class WebLevelFile
    {
        public int levelNumber;
        public int moveCount;
        public int expectedCategoryCount;
        public int visibleRowCount;
        public List<Category> categories;
        public List<LevelWordEntry> orderedWords;
    }

    [MenuItem("Tools/Word Game/Import Web Level JSON")]
    private static void Import()
    {
        string sourcePath = EditorUtility.OpenFilePanel("Import Web Level JSON", "", "json");
        if (string.IsNullOrEmpty(sourcePath)) return;

        WebLevelFile source;
        try
        {
            source = JsonUtility.FromJson<WebLevelFile>(File.ReadAllText(sourcePath));
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

        string assetPath = $"{LevelFolder}/Level_{source.levelNumber:000}.asset";
        LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        bool created = level == null;
        if (created) level = ScriptableObject.CreateInstance<LevelData>();
        else Undo.RecordObject(level, "Import Web Level");

        level.levelNumber = source.levelNumber;
        level.moveCount = Mathf.Max(1, source.moveCount);
        level.expectedCategoryCount = Mathf.Max(0, source.expectedCategoryCount);
        level.visibleRowCount = Mathf.Max(0, source.visibleRowCount);
        level.categories = source.categories ?? new List<Category>();
        level.orderedWords = source.orderedWords ?? new List<LevelWordEntry>();

        if (!level.IsValid(out string error))
        {
            if (created) Object.DestroyImmediate(level);
            EditorUtility.DisplayDialog("Import Failed", error, "OK");
            return;
        }

        if (created) AssetDatabase.CreateAsset(level, assetPath);
        EditorUtility.SetDirty(level);

        LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database != null)
        {
            if (database.levels == null) database.levels = new List<LevelData>();
            if (!database.levels.Contains(level)) database.levels.Add(level);
            database.levels = database.levels.Where(item => item != null).OrderBy(item => item.levelNumber).ToList();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = level;
        EditorGUIUtility.PingObject(level);
        EditorUtility.DisplayDialog("Level Imported", $"Level {level.levelNumber} was imported successfully.", "OK");
    }
}
#endif
