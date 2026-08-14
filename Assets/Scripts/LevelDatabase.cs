using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Word Game/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelData> levels = new List<LevelData>();

    public LevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0) return null;
        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}
