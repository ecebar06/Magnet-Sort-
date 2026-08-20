#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExampleLevelCreator
{
    private const string LevelsFolder = "Assets/_Game/Resources/Data/Levels";

    [InitializeOnLoadMethod]
    private static void ScheduleCreation()
    {
        EditorApplication.delayCall += CreateExampleLevelsIfMissing;
    }

    private static void CreateExampleLevelsIfMissing()
    {
        if (AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Any(path => path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))) return;

        if (AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder }).Length > 0) return;
        CreateExampleLevels();
    }

    [MenuItem("Tools/Word Game/Create Example Levels")]
    public static void CreateExampleLevels()
    {
        EnsureFolder("Assets/_Game/Resources/Data", "Levels");

        List<LevelData> levels = new List<LevelData>
        {
            CreateLevel(1, 35,
                TransformingCategory("fruits", "Fruits", "Harvest", "concepts", IconWord("Apple", "fruits__apple"), Word("Banana"), Word("Orange"), Word("Grape")),
                TransformingCategory("colors", "Colors", "Rainbow", "concepts", Word("Red"), Word("Blue"), Word("Green"), Word("Yellow")),
                TransformingCategory("pets", "Pets", "Companion", "concepts", Word("Cat"), Word("Dog"), Word("Bird"), Word("Fish")),
                TransformingCategory("shapes", "Shapes", "Geometry", "concepts", Word("Circle"), Word("Square"), Word("Triangle"), Word("Rectangle")),
                Category("music_l1", "Music", Word("Guitar"), Word("Piano"), Word("Violin"), Word("Drums")),
                Category("weather_l1", "Weather", Word("Rain"), Word("Snow"), Word("Wind"), Word("Storm")),
                Category("kitchen_l1", "Kitchen", Word("Whisk"), Word("Spatula"), Word("Ladle"), Word("Tongs")),
                Category("concepts", "Concepts", Word("Harvest"), Word("Rainbow"), Word("Companion"), Word("Geometry"))),

            CreateLevel(2, 13,
                Category("music", "Music", Word("Guitar"), Word("Piano"), Word("Violin"), Word("Drums")),
                Category("weather", "Weather", Word("Rain"), Word("Snow"), Word("Wind"), Word("Storm")),
                Category("kitchen", "Kitchen", Word("Whisk"), Word("Spatula"), Word("Ladle"), Word("Tongs")),
                Category("travel", "Travel", Word("Passport"), Word("Ticket"), Word("Suitcase"), Word("Hotel"))),

            CreateLevel(3, 18,
                Category("space", "Space", Word("Galaxy"), Word("Nebula"), Word("Comet"), Word("Asteroid")),
                Category("flowers", "Flowers", Word("Rose"), Word("Tulip"), Word("Daisy"), Word("Orchid")),
                Category("clothing", "Clothing", Word("Shirt"), Word("Pants"), Word("Jacket"), Word("Socks")),
                Category("vehicles", "Vehicles", Word("Car"), Word("Bus"), Word("Train"), Word("Bicycle")),
                Category("school", "School", Word("Pencil"), Word("Ruler"), Word("Eraser"), Word("Notebook"))),

            CreateLevel(4, 17,
                Category("water", "Water Bodies", Word("River"), Word("Lake"), Word("Ocean"), Word("Pond")),
                Category("computer", "Computer", Word("Mouse"), Word("Keyboard"), Word("Monitor"), Word("Printer")),
                Category("sports", "Sports", Word("Tennis"), Word("Golf"), Word("Boxing"), Word("Rugby")),
                Category("materials", "Materials", Word("Wood"), Word("Glass"), Word("Steel"), Word("Cotton")),
                Category("emotions", "Emotions", Word("Joy"), Word("Anger"), Word("Fear"), Word("Surprise"))),

            CreateLevel(5, 22,
                Category("mythology", "Mythology", Word("Zeus"), Word("Odin"), Word("Thor"), Word("Anubis")),
                Category("cities", "Famous Cities", Word("Paris"), Word("London"), Word("Rome"), Word("Tokyo")),
                Category("medical", "Medical", Word("Bandage"), Word("Syringe"), Word("Stethoscope"), Word("Thermometer")),
                Category("film", "Film Genres", Word("Comedy"), Word("Horror"), Word("Drama"), Word("Western")),
                Category("desserts", "Desserts", Word("Brownie"), Word("Pudding"), Word("Donut"), Word("Gelato")),
                Category("navigation", "Navigation", Word("Compass"), Word("Map"), Word("Route"), Word("Landmark")))
        };

        foreach (LevelData level in levels)
            File.WriteAllText(Path.GetFullPath($"{LevelsFolder}/Level_{level.levelNumber:000}.json"), level.ToJson());
        AssetDatabase.Refresh();
    }

    private static LevelData CreateLevel(int number, int moves, params Category[] categories)
    {
        LevelData level = ScriptableObject.CreateInstance<LevelData>();

        level.levelNumber = number;
        level.moveCount = moves;
        level.visibleRowCount = number == 1 ? 4 : categories.Length;
        level.expectedCategoryCount = categories.Length;
        level.categories = new List<Category>(categories);
        level.orderedWords = BuildOrderedWords(level);
        return level;
    }

    private static List<LevelWordEntry> BuildOrderedWords(LevelData level)
    {
        List<Category> transforms = level.categories.Where(c => c.transformsOnComplete).ToList();
        HashSet<string> generated = transforms.Select(c => c.transformResult.text)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        List<Category> fixedCategories = level.categories.Where(c => !c.transformsOnComplete).ToList();
        if (transforms.Count == 0)
            return Enumerable.Range(0, 4)
                .SelectMany(wordIndex => fixedCategories.Select(c => Entry(c.id, c.words[wordIndex])))
                .ToList();

        List<LevelWordEntry> visible = transforms[0].words.Select(w => Entry(transforms[0].id, w)).ToList();
        List<LevelWordEntry> queue = new List<LevelWordEntry>();
        for (int i = 1; i < transforms.Count; i++)
        {
            visible.Add(Entry(transforms[i].id, transforms[i].words[0]));
            queue.AddRange(transforms[i].words.Skip(1).Select(w => Entry(transforms[i].id, w)));
        }
        List<(Category category, List<WordItem> words)> groups = fixedCategories
            .Select(category => (category, category.words.Where(word => !generated.Contains(word.text)).ToList()))
            .Where(group => group.Item2.Count > 0).ToList();
        int finalIndex = groups.FindIndex(group => group.words.Count == 3);
        if (finalIndex < 0) finalIndex = groups.FindLastIndex(group => group.words.Count == 4);
        if (finalIndex < 0) return new List<LevelWordEntry>();
        (Category category, List<WordItem> words) finalGroup = groups[finalIndex];
        groups.RemoveAt(finalIndex);
        if (finalGroup.words.Count == 4) visible.Add(Entry(finalGroup.category.id, finalGroup.words[0]));
        queue.AddRange(finalGroup.words.Skip(Mathf.Max(0, finalGroup.words.Count - 3))
            .Select(word => Entry(finalGroup.category.id, word)));
        visible.AddRange(groups.SelectMany(group => group.words.Select(word => Entry(group.category.id, word))));
        visible = InterleaveVisibleWords(visible);
        return visible.Concat(queue).ToList();
    }

    private static List<LevelWordEntry> InterleaveVisibleWords(IEnumerable<LevelWordEntry> source)
    {
        List<Queue<LevelWordEntry>> groups = source.GroupBy(entry => entry.categoryId)
            .Select(group => new Queue<LevelWordEntry>(group)).ToList();
        List<LevelWordEntry> result = new List<LevelWordEntry>();
        while (groups.Any(group => group.Count > 0))
            foreach (Queue<LevelWordEntry> group in groups)
                if (group.Count > 0) result.Add(group.Dequeue());
        return result;
    }

    private static LevelWordEntry Entry(string categoryId, WordItem word)
    {
        return new LevelWordEntry { categoryId = categoryId, word = new WordItem
        {
            text = word.text,
            hasSprite = word.hasSprite,
            spriteKey = word.spriteKey,
            syllablePartA = word.syllablePartA,
            syllablePartB = word.syllablePartB
        }};
    }

    private static Category Category(string id, string name, params WordItem[] words)
    {
        return new Category { id = id, name = name, words = new List<WordItem>(words) };
    }

    private static Category TransformingCategory(string id, string name, string result,
        string targetCategoryId, params WordItem[] words)
    {
        Category category = Category(id, name, words);
        category.transformsOnComplete = true;
        category.transformResult = Word(result);
        category.transformResultCategoryId = targetCategoryId;
        return category;
    }

    private static WordItem Word(string text)
    {
        return new WordItem { text = text };
    }

    private static WordItem IconWord(string text, string key)
    {
        return new WordItem { text = text, hasSprite = true, spriteKey = key };
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
