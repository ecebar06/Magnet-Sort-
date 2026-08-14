#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WordGameLevelEditor : EditorWindow
{
    private const string LibraryPath = "Assets/Resources/WordLibrary.json";
    private const string DatabasePath = "Assets/Resources/LevelDatabase.asset";
    private const string LevelsFolder = "Assets/Resources/Levels";

    private WordLibrary library;
    private LevelDatabase database;
    private LevelData level;
    private Vector2 libraryScroll;
    private Vector2 levelScroll;
    private string search = "";
    private string status = "";
    private int selectedLevelIndex;
    private readonly HashSet<string> expandedCategories = new HashSet<string>();

    private bool showNewCategory;
    private string newCategoryId = "";
    private string newCategoryName = "";
    private readonly string[] newWords = new string[4];

    [MenuItem("Tools/Word Game/Level Editor")]
    public static void Open()
    {
        WordGameLevelEditor window = GetWindow<WordGameLevelEditor>("Level Editor");
        window.minSize = new Vector2(900, 560);
        window.Show();
    }

    private void OnEnable()
    {
        ReloadAll();
    }

    private void ReloadAll()
    {
        LoadLibrary();
        database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (database != null && database.levels != null && database.levels.Count > 0)
        {
            selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, database.levels.Count - 1);
            level = database.levels[selectedLevelIndex];
        }
        else
        {
            level = null;
        }
        Repaint();
    }

    private void LoadLibrary()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(LibraryPath);
        library = json == null ? null : JsonUtility.FromJson<WordLibrary>(json.text);
        if (library == null) library = new WordLibrary { id = "bubble_words_library", categories = new List<Category>() };
        if (library.categories == null) library.categories = new List<Category>();
        status = $"Library: {library.categories.Count} categories";
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.43f)))
                DrawLibraryPanel();

            GUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope())
                DrawLevelPanel();
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUI.HelpBox(new Rect(8, position.height - 25, position.width - 16, 20), status, MessageType.Info);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("WORD GAME LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70))) ReloadAll();
            GUI.enabled = level != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70))) SaveCurrentLevel();
            GUI.enabled = true;
        }
    }

    private void DrawLibraryPanel()
    {
        EditorGUILayout.LabelField("WORD LIBRARY", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
            if (GUILayout.Button("New Category", GUILayout.Width(105))) showNewCategory = !showNewCategory;
        }

        if (showNewCategory) DrawNewCategoryBox();

        int wordCount = library.categories.Sum(c => c.words == null ? 0 : c.words.Count);
        EditorGUILayout.LabelField($"{library.categories.Count} categories • {wordCount} words", EditorStyles.miniLabel);

        List<Category> filtered = FilteredCategories().ToList();
        if (filtered.Count > 200)
            EditorGUILayout.HelpBox($"Showing the first 200 of {filtered.Count} results. Use search to narrow the list.", MessageType.None);
        libraryScroll = EditorGUILayout.BeginScrollView(libraryScroll, "box");
        foreach (Category category in filtered.Take(200)) DrawLibraryCategory(category);
        EditorGUILayout.EndScrollView();
    }

    private IEnumerable<Category> FilteredCategories()
    {
        string query = (search ?? "").Trim();
        if (query.Length == 0) return library.categories;
        return library.categories.Where(c =>
            Contains(c.name, query) || Contains(c.id, query) ||
            (c.words != null && c.words.Any(w => Contains(w.text, query))));
    }

    private static bool Contains(string value, string query)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrawLibraryCategory(Category category)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool expanded = expandedCategories.Contains(category.id);
                if (GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    if (expanded) expandedCategories.Remove(category.id); else expandedCategories.Add(category.id);
                }
                EditorGUILayout.LabelField(category.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                bool alreadyAdded = LevelContains(category.id);
                GUI.enabled = level != null && !alreadyAdded;
                if (GUILayout.Button(alreadyAdded ? "Added" : "Add", GUILayout.Width(58))) AddCategoryToLevel(category);
                GUI.enabled = true;
            }

            if (expandedCategories.Contains(category.id))
            {
                string words = category.words == null ? "No words" : string.Join("  •  ", category.words.Select(w => w.text));
                EditorGUILayout.LabelField(words, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(category.id, EditorStyles.miniLabel);
            }
        }
    }

    private void DrawNewCategoryBox()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("New Library Category", EditorStyles.boldLabel);
            newCategoryName = EditorGUILayout.TextField("Name", newCategoryName);
            newCategoryId = EditorGUILayout.TextField("ID", newCategoryId);
            for (int i = 0; i < 4; i++) newWords[i] = EditorGUILayout.TextField($"Word {i + 1}", newWords[i]);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(70))) showNewCategory = false;
                if (GUILayout.Button("Add to Library", GUILayout.Width(110))) AddNewLibraryCategory();
            }
        }
    }

    private void DrawLevelPanel()
    {
        DrawLevelSelector();
        if (level == null)
        {
            EditorGUILayout.HelpBox("Create or select a level.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int number = EditorGUILayout.IntField("Level Number", level.levelNumber);
        int moves = EditorGUILayout.IntField("Move Count", level.moveCount);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(level, "Edit Level Settings");
            level.levelNumber = Mathf.Max(1, number);
            level.moveCount = Mathf.Max(1, moves);
            EditorUtility.SetDirty(level);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"LEVEL CATEGORIES ({level.categories.Count} rows)", EditorStyles.boldLabel);
        DrawValidation();

        levelScroll = EditorGUILayout.BeginScrollView(levelScroll, "box");
        for (int i = 0; i < level.categories.Count; i++) DrawLevelCategory(i);
        EditorGUILayout.EndScrollView();
    }

    private void DrawLevelSelector()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("LEVEL", EditorStyles.boldLabel, GUILayout.Width(48));
            string[] names = database == null || database.levels == null
                ? Array.Empty<string>()
                : database.levels.Select((l, i) => l == null ? $"Missing ({i})" : $"{i + 1}. Level {l.levelNumber} ({l.categories.Count} rows)").ToArray();

            if (names.Length > 0)
            {
                int next = EditorGUILayout.Popup(selectedLevelIndex, names);
                if (next != selectedLevelIndex)
                {
                    selectedLevelIndex = next;
                    level = database.levels[next];
                }
            }
            else EditorGUILayout.LabelField("No levels");

            if (GUILayout.Button("New", GUILayout.Width(55))) CreateNewLevel();
            GUI.enabled = level != null;
            if (GUILayout.Button("Delete", GUILayout.Width(55)) && EditorUtility.DisplayDialog("Delete Level", $"Delete Level {level.levelNumber}?", "Delete", "Cancel")) DeleteCurrentLevel();
            GUI.enabled = true;
        }
    }

    private void DrawLevelCategory(int index)
    {
        Category category = level.categories[index];
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Row {index + 1}", GUILayout.Width(48));
                EditorGUILayout.LabelField(category.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = index > 0;
                if (GUILayout.Button("▲", GUILayout.Width(28))) MoveCategory(index, index - 1);
                GUI.enabled = index < level.categories.Count - 1;
                if (GUILayout.Button("▼", GUILayout.Width(28))) MoveCategory(index, index + 1);
                GUI.enabled = true;
                if (GUILayout.Button("Remove", GUILayout.Width(62))) { RemoveCategory(index); return; }
            }

            if (category.words != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (WordItem word in category.words)
                    {
                        string label = word.hasSprite ? $"{word.text}  🖼" : word.text;
                        GUILayout.Label(label, EditorStyles.miniButton, GUILayout.MinWidth(65));
                    }
                }
            }
        }
    }

    private void DrawValidation()
    {
        if (level.IsValid(out string error))
            EditorGUILayout.HelpBox("Level is valid and ready to play.", MessageType.Info);
        else
            EditorGUILayout.HelpBox(error, MessageType.Warning);
    }

    private bool LevelContains(string categoryId)
    {
        return level != null && level.categories != null && level.categories.Any(c => c.id == categoryId);
    }

    private void AddCategoryToLevel(Category source)
    {
        Undo.RecordObject(level, "Add Level Category");
        level.categories.Add(CloneCategory(source));
        EditorUtility.SetDirty(level);
        status = $"Added {source.name} to Level {level.levelNumber}";
    }

    private void RemoveCategory(int index)
    {
        Undo.RecordObject(level, "Remove Level Category");
        level.categories.RemoveAt(index);
        EditorUtility.SetDirty(level);
    }

    private void MoveCategory(int from, int to)
    {
        Undo.RecordObject(level, "Reorder Level Categories");
        Category item = level.categories[from];
        level.categories.RemoveAt(from);
        level.categories.Insert(to, item);
        EditorUtility.SetDirty(level);
    }

    private void AddNewLibraryCategory()
    {
        string id = string.IsNullOrWhiteSpace(newCategoryId) ? Slug(newCategoryName) : Slug(newCategoryId);
        if (string.IsNullOrWhiteSpace(newCategoryName) || string.IsNullOrWhiteSpace(id) || newWords.Any(string.IsNullOrWhiteSpace))
        {
            status = "Category name, ID and exactly four words are required.";
            return;
        }
        if (library.categories.Any(c => string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase)))
        {
            status = $"Category ID '{id}' already exists.";
            return;
        }

        Category category = new Category
        {
            id = id,
            name = newCategoryName.Trim(),
            words = newWords.Select(w => new WordItem { text = w.Trim() }).ToList()
        };
        library.categories.Add(category);
        SaveLibrary();
        expandedCategories.Add(id);
        showNewCategory = false;
        newCategoryId = newCategoryName = "";
        Array.Clear(newWords, 0, newWords.Length);
        status = $"Added {category.name} to WordLibrary.json";
    }

    private void SaveLibrary()
    {
        File.WriteAllText(Path.GetFullPath(LibraryPath), JsonUtility.ToJson(library, true));
        AssetDatabase.ImportAsset(LibraryPath, ImportAssetOptions.ForceUpdate);
    }

    private void CreateNewLevel()
    {
        EnsureDatabase();
        int number = database.levels.Where(l => l != null).Select(l => l.levelNumber).DefaultIfEmpty(0).Max() + 1;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/Level_{number:000}.asset");
        LevelData created = CreateInstance<LevelData>();
        created.levelNumber = number;
        created.moveCount = 12;
        created.categories = new List<Category>();
        AssetDatabase.CreateAsset(created, path);
        Undo.RecordObject(database, "Add Level");
        database.levels.Add(created);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        selectedLevelIndex = database.levels.Count - 1;
        level = created;
        status = $"Created Level {number}";
    }

    private void DeleteCurrentLevel()
    {
        string path = AssetDatabase.GetAssetPath(level);
        Undo.RecordObject(database, "Delete Level");
        database.levels.Remove(level);
        EditorUtility.SetDirty(database);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, Mathf.Max(0, database.levels.Count - 1));
        level = database.levels.Count == 0 ? null : database.levels[selectedLevelIndex];
    }

    private void SaveCurrentLevel()
    {
        EditorUtility.SetDirty(level);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        status = level.IsValid(out string error) ? $"Level {level.levelNumber} saved." : $"Saved with warning: {error}";
    }

    private void EnsureDatabase()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(LevelsFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Levels");
        if (database != null) return;
        database = CreateInstance<LevelDatabase>();
        database.levels = new List<LevelData>();
        AssetDatabase.CreateAsset(database, DatabasePath);
    }

    private static Category CloneCategory(Category source)
    {
        return new Category
        {
            id = source.id,
            name = source.name,
            words = source.words == null ? new List<WordItem>() : source.words.Select(w => new WordItem
            {
                text = w.text,
                hasSprite = w.hasSprite,
                spriteKey = w.spriteKey,
                syllablePartA = w.syllablePartA,
                syllablePartB = w.syllablePartB
            }).ToList()
        };
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        char[] chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return string.Join("_", new string(chars).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
#endif
