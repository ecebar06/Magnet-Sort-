#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExampleLevelCreator
{
    private const string LevelsFolder = "Assets/_Game/Resources/Data/Levels";
    private const string DatabasePath = "Assets/_Game/Resources/Data/LevelDatabase.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleCreation()
    {
        EditorApplication.delayCall += CreateExampleLevelsIfMissing;
    }

    private static void CreateExampleLevelsIfMissing()
    {
        LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database == null || database.levels == null || database.levels.Count == 0)
        {
            CreateExampleLevels();
        }
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

        LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<LevelDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.levels = levels;
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    private static LevelData CreateLevel(int number, int moves, params Category[] categories)
    {
        string path = $"{LevelsFolder}/Level_{number:000}.asset";
        LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (level == null)
        {
            level = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(level, path);
        }

        level.levelNumber = number;
        level.moveCount = moves;
        level.visibleRowCount = number == 1 ? 4 : categories.Length;
        level.expectedCategoryCount = categories.Length;
        level.categories = new List<Category>(categories);
        level.orderedWords = BuildOrderedWords(level);
        EditorUtility.SetDirty(level);
        return level;
    }

    private static List<LevelWordEntry> BuildOrderedWords(LevelData level)
    {
        List<Category> transforms = level.categories.Where(c => c.transformsOnComplete).ToList();
        HashSet<string> generated = transforms.Select(c => c.transformResult.text)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        List<Category> fixedCategories = level.categories
            .Where(c => !c.transformsOnComplete && c.words.All(w => !generated.Contains(w.text))).ToList();
        if (transforms.Count == 0)
            return Enumerable.Range(0, 4)
                .SelectMany(wordIndex => fixedCategories.Select(c => Entry(c.id, c.words[wordIndex])))
                .ToList();

        int visibleCount = level.visibleRowCount * 4;
        List<LevelWordEntry> visible = transforms[0].words.Select(w => Entry(transforms[0].id, w)).ToList();
        List<LevelWordEntry> queue = new List<LevelWordEntry>();
        for (int i = 1; i < transforms.Count; i++)
        {
            visible.Add(Entry(transforms[i].id, transforms[i].words[0]));
            queue.AddRange(transforms[i].words.Skip(1).Select(w => Entry(transforms[i].id, w)));
        }
        List<LevelWordEntry> fixedWords = fixedCategories.SelectMany(c => c.words.Select(w => Entry(c.id, w))).ToList();
        int takeFixed = Mathf.Clamp(visibleCount - visible.Count, 0, fixedWords.Count);
        visible.AddRange(fixedWords.Take(takeFixed));
        queue.AddRange(fixedWords.Skip(takeFixed));
        return visible.Concat(queue).ToList();
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
