#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LegacyLevelJsonMigration
{
    private const string LevelsFolder = "Assets/_Game/Resources/Data/Levels";
    private const string LegacyDatabasePath = "Assets/_Game/Resources/Data/LevelDatabase.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleMigration()
    {
        EditorApplication.delayCall += MigrateIfNeeded;
    }

    [MenuItem("Tools/Word Game/Migrate Legacy Levels To JSON")]
    public static void MigrateIfNeeded()
    {
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
            Directory.CreateDirectory(Path.GetFullPath(LevelsFolder));

        string[] legacyPaths = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (legacyPaths.Length == 0) return;

        int migrated = 0;
        foreach (string legacyPath in legacyPaths)
        {
            LevelData legacy = AssetDatabase.LoadAssetAtPath<LevelData>(legacyPath);
            if (legacy == null) continue;
            string jsonPath = $"{LevelsFolder}/Level_{legacy.levelNumber:000}.json";
            if (!File.Exists(Path.GetFullPath(jsonPath)))
                File.WriteAllText(Path.GetFullPath(jsonPath), legacy.ToJson());
            AssetDatabase.DeleteAsset(legacyPath);
            migrated++;
        }

        if (AssetDatabase.LoadMainAssetAtPath(LegacyDatabasePath) != null)
            AssetDatabase.DeleteAsset(LegacyDatabasePath);

        AssetDatabase.Refresh();
        Debug.Log($"Migrated {migrated} legacy level asset(s) to JSON. Runtime and editors now use Resources/Data/Levels/*.json.");
    }
}
#endif
