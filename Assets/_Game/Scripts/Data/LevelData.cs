using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_001", menuName = "Word Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int levelNumber = 1;
    [Min(1)] public int moveCount = 12;
    [Min(0)] public int expectedCategoryCount;
    [Tooltip("Rows visible at once. Zero keeps the legacy one-row-per-category behaviour.")]
    [Min(0)] public int visibleRowCount;
    public List<Category> categories = new List<Category>();
    [Tooltip("Exact initial board and queue order. First rowCount * 4 entries are visible.")]
    public List<LevelWordEntry> orderedWords = new List<LevelWordEntry>();

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

        if (expectedCategoryCount > 0 && categories.Count != expectedCategoryCount)
        {
            error = $"Expected {expectedCategoryCount} categories, but {categories.Count} were added.";
            return false;
        }

        if (categories.Any(category => category.transformsOnComplete &&
            (category.transformResult == null || string.IsNullOrWhiteSpace(category.transformResult.text) ||
             string.IsNullOrWhiteSpace(category.transformResultCategoryId))))
        {
            error = "Transforming categories need a result word and target category id.";
            return false;
        }

        foreach (Category category in categories.Where(c => c.transformsOnComplete))
        {
            Category target = categories.FirstOrDefault(c => c.id == category.transformResultCategoryId);
            if (target == null || target.words == null ||
                !target.words.Any(word => string.Equals(word.text, category.transformResult.text,
                    System.StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Transformation result '{category.transformResult.text}' is missing from target category '{category.transformResultCategoryId}'.";
                return false;
            }
        }

        List<string> words = categories.SelectMany(category => category.words).Select(word => word.text).ToList();
        if (words.Count != words.Distinct(System.StringComparer.OrdinalIgnoreCase).Count())
        {
            error = "A word is repeated inside the level.";
            return false;
        }


        HashSet<string> generatedWords = categories.Where(c => c.transformsOnComplete && c.transformResult != null)
            .Select(c => c.transformResult.text).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        int playableWords = categories.SelectMany(c => c.words).Count(word => !generatedWords.Contains(word.text));
        int transformationCount = categories.Count(c => c.transformsOnComplete);
        int effectiveRowCount = visibleRowCount > 0 ? visibleRowCount : categories.Count;
        int queueWords = playableWords - effectiveRowCount * 4;
        if (transformationCount > 0 && queueWords != transformationCount * 3)
        {
            error = $"Row/category counts create {queueWords} queued words; {transformationCount * 3} are required for transformations.";
            return false;
        }


        if (orderedWords != null && orderedWords.Count > 0)
        {
            int expectedWordCount = categories.SelectMany(c => c.words)
                .Count(word => !generatedWords.Contains(word.text));
            if (orderedWords.Count != expectedWordCount)
            {
                error = $"Generated order has {orderedWords.Count} words, expected {expectedWordCount}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
