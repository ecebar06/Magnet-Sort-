using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LevelDatabase
{
    public List<LevelData> levels = new List<LevelData>();

    public static LevelDatabase LoadFromResources()
    {
        LevelDatabase database = new LevelDatabase();
        database.levels = Resources.LoadAll<TextAsset>("Data/Levels")
            .Where(asset => asset != null && asset.name.StartsWith("Level_"))
            .Select(asset => LevelData.FromJson(asset.text))
            .Where(level => level != null && level.levelNumber > 0)
            .OrderBy(level => level.levelNumber)
            .ToList();

        return database;
    }

    public LevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0) return null;
        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}
