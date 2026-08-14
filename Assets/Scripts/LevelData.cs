using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_001", menuName = "Word Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int levelNumber = 1;
    [Min(1)] public int moveCount = 12;
    public List<Category> categories = new List<Category>();

    public bool IsValid(out string error)
    {
        if (categories == null || categories.Count == 0)
        {
            error = "Level has no categories.";
            return false;
        }

        if (categories.Any(category => category == null || category.words == null || category.words.Count != 4))
        {
            error = "Every category must contain exactly four words.";
            return false;
        }

        List<string> words = categories.SelectMany(category => category.words).Select(word => word.text).ToList();
        if (words.Count != words.Distinct(System.StringComparer.OrdinalIgnoreCase).Count())
        {
            error = "A word is repeated inside the level.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
