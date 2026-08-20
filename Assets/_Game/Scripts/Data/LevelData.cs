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

        Dictionary<string, Category> transformingById = categories
            .Where(category => category.transformsOnComplete && !string.IsNullOrWhiteSpace(category.id))
            .GroupBy(category => category.id, System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.OrdinalIgnoreCase);
        foreach (Category start in transformingById.Values)
        {
            List<Category> path = new List<Category>();
            Category current = start;
            while (current != null)
            {
                int cycleStart = path.FindIndex(category => string.Equals(category.id, current.id,
                    System.StringComparison.OrdinalIgnoreCase));
                if (cycleStart >= 0)
                {
                    List<string> cycleNames = path.Skip(cycleStart).Select(category => category.name).ToList();
                    cycleNames.Add(current.name);
                    error = $"Transformation cycle: {string.Join(" → ", cycleNames)}. These categories wait for each other and cannot be completed.";
                    return false;
                }

                path.Add(current);
                transformingById.TryGetValue(current.transformResultCategoryId ?? string.Empty, out current);
            }
        }

        var repeatedWord = categories
            .SelectMany(category => category.words.Select(word => new { Word = word.text?.Trim(), Category = category.name }))
            .GroupBy(item => item.Word, System.StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (repeatedWord != null)
        {
            List<string> categoryNames = repeatedWord.Select(item => item.Category).Distinct().ToList();
            error = categoryNames.Count > 1
                ? $"The word '{repeatedWord.Key}' exists in both '{categoryNames[0]}' and '{categoryNames[1]}' categories."
                : $"The word '{repeatedWord.Key}' appears more than once in '{categoryNames[0]}'.";
            return false;
        }


        HashSet<string> generatedWords = categories.Where(c => c.transformsOnComplete && c.transformResult != null)
            .Select(c => c.transformResult.text).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        int playableWords = categories.SelectMany(c => c.words).Count(word => !generatedWords.Contains(word.text));
        int transformationCount = categories.Count(c => c.transformsOnComplete);
        int effectiveRowCount = visibleRowCount > 0 ? visibleRowCount : categories.Count;
        int requiredTransformationCount = categories.Count - effectiveRowCount;
        if (requiredTransformationCount < 0)
        {
            error = $"Visible row count ({effectiveRowCount}) cannot exceed category count ({categories.Count}).";
            return false;
        }
        if (transformationCount != requiredTransformationCount)
        {
            error = $"{categories.Count} categories with {effectiveRowCount} visible rows require exactly {requiredTransformationCount} transformations; {transformationCount} are currently set.";
            return false;
        }
        int queueWords = playableWords - effectiveRowCount * 4;
        if (queueWords != transformationCount * 3)
        {
            error = transformationCount == 0 && queueWords > 0
                ? $"{queueWords} words would remain queued, but this level has no transformations to bring them onto the board."
                : $"Row/category counts create {queueWords} queued words; {transformationCount * 3} are required for {transformationCount} transformations.";
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
