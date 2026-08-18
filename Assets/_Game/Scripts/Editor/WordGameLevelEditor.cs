#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WordGameLevelEditor : EditorWindow
{
    private int pendingWordDragIndex = -1;
    private Vector2 pendingWordDragStart;
    private const string LibraryPath = "Assets/_Game/Resources/Data/WordLibrary.json";
    private const string DatabasePath = "Assets/_Game/Resources/Data/LevelDatabase.asset";
    private const string LevelsFolder = "Assets/_Game/Resources/Data/Levels";

    private WordLibrary library;
    private LevelDatabase database;
    private WordIconLibrary iconLibrary;
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
    private int newTransformSourceIndex;
    private int newTransformTargetIndex;
    private string newTransformResult = "";

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
        iconLibrary = AssetDatabase.LoadAssetAtPath<WordIconLibrary>("Assets/_Game/Resources/Data/MainWordIconLibrary.asset");
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
                if (category.words != null)
                {
                    EditorGUI.BeginChangeCheck();
                    foreach (WordItem word in category.words)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(word.text, GUILayout.MinWidth(80));
                            word.hasSprite = EditorGUILayout.ToggleLeft("Has Sprite", word.hasSprite, GUILayout.Width(85));
                            GUI.enabled = word.hasSprite;
                            word.spriteKey = EditorGUILayout.TextField(word.spriteKey ?? "", GUILayout.MinWidth(100));
                            Sprite currentSprite = null;
                            if (word.hasSprite && iconLibrary != null) iconLibrary.TryGetIcon(word.spriteKey, out currentSprite);
                            Sprite nextSprite = (Sprite)EditorGUILayout.ObjectField(currentSprite, typeof(Sprite), false, GUILayout.Width(70));
                            if (word.hasSprite && iconLibrary != null && nextSprite != currentSprite && !string.IsNullOrWhiteSpace(word.spriteKey))
                            {
                                iconLibrary.SetIcon(word.spriteKey, nextSprite);
                                EditorUtility.SetDirty(iconLibrary);
                            }
                            GUI.enabled = true;
                        }
                    }
                    if (EditorGUI.EndChangeCheck()) SaveLibrary();
                }
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
        int categoryCount = EditorGUILayout.IntField("Category Count", level.expectedCategoryCount);
        int visibleRows = EditorGUILayout.IntField("Visible Row Count", level.visibleRowCount);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(level, "Edit Level Settings");
            level.levelNumber = Mathf.Max(1, number);
            level.moveCount = Mathf.Max(1, moves);
            level.expectedCategoryCount = Mathf.Max(0, categoryCount);
            level.visibleRowCount = Mathf.Max(0, visibleRows);
            EditorUtility.SetDirty(level);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"LEVEL CATEGORIES ({level.categories.Count} rows)", EditorStyles.boldLabel);
        DrawValidation();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Solveable Order", GUILayout.Height(28))) GenerateSolveableOrder();
            GUI.enabled = level.orderedWords != null && level.orderedWords.Count > 0;
            if (GUILayout.Button("Clear Generated Order", GUILayout.Height(28)))
            {
                Undo.RecordObject(level, "Clear Generated Level Order");
                level.orderedWords?.Clear();
                EditorUtility.SetDirty(level);
            }
            GUI.enabled = true;
        }

        DrawOrderedWordLayout();
        DrawTransformationList();

        levelScroll = EditorGUILayout.BeginScrollView(levelScroll, "box");
        for (int i = 0; i < level.categories.Count; i++) DrawLevelCategory(i);
        EditorGUILayout.EndScrollView();
    }

    private void GenerateSolveableOrder()
    {
        if (level.categories == null || level.categories.Count == 0 || level.visibleRowCount <= 0)
        {
            status = "Add categories and set a row count before generating.";
            return;
        }

        List<Category> transforms = level.categories.Where(c => c.transformsOnComplete).ToList();
        HashSet<string> generatedWords = transforms.Where(c => c.transformResult != null)
            .Select(c => c.transformResult.text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<Category> fixedCategories = level.categories
            .Where(c => !c.transformsOnComplete && c.words.All(w => !generatedWords.Contains(w.text)))
            .ToList();
        int visibleWordCount = level.visibleRowCount * 4;
        int playableWordCount = transforms.Sum(c => c.words.Count) + fixedCategories.Sum(c => c.words.Count);
        int queueWordCount = playableWordCount - visibleWordCount;

        if (transforms.Count > 0 && queueWordCount != transforms.Count * 3)
        {
            status = $"Cannot generate without gaps: queue has {queueWordCount} words, but {transforms.Count * 3} are required.";
            return;
        }
        if (playableWordCount < visibleWordCount)
        {
            status = $"Not enough words for {level.visibleRowCount} rows.";
            return;
        }

        if (transforms.Count == 0)
        {
            Undo.RecordObject(level, "Generate Solveable Level Order");
            level.orderedWords = Enumerable.Range(0, 4)
                .SelectMany(wordIndex => fixedCategories.Select(c => Entry(c.id, c.words[wordIndex])))
                .ToList();
            EditorUtility.SetDirty(level);
            status = $"Generated deterministic order for {level.orderedWords.Count} words.";
            return;
        }

        List<LevelWordEntry> visible = new List<LevelWordEntry>();
        List<LevelWordEntry> queue = new List<LevelWordEntry>();
        if (transforms.Count > 0)
        {
            visible.AddRange(transforms[0].words.Select(w => Entry(transforms[0].id, w)));
            for (int i = 1; i < transforms.Count; i++)
            {
                visible.Add(Entry(transforms[i].id, transforms[i].words[0]));
                queue.AddRange(transforms[i].words.Skip(1).Select(w => Entry(transforms[i].id, w)));
            }
        }

        List<LevelWordEntry> fixedWords = fixedCategories
            .SelectMany(c => c.words.Select(w => Entry(c.id, w))).ToList();
        int fixedVisibleCount = visibleWordCount - visible.Count;
        if (fixedVisibleCount < 0 || fixedVisibleCount > fixedWords.Count)
        {
            status = "Too many transforming categories for the selected row count.";
            return;
        }
        visible.AddRange(fixedWords.Take(fixedVisibleCount));
        queue.AddRange(fixedWords.Skip(fixedVisibleCount));

        Undo.RecordObject(level, "Generate Solveable Level Order");
        level.orderedWords = visible.Concat(queue).ToList();
        EditorUtility.SetDirty(level);
        status = $"Generated {visible.Count} visible words and {queue.Count} queued words.";
    }

    private static LevelWordEntry Entry(string categoryId, WordItem source)
    {
        return new LevelWordEntry { categoryId = categoryId, word = CloneWord(source) };
    }

    private static WordItem CloneWord(WordItem source)
    {
        return source == null ? new WordItem() : new WordItem
        {
            text = source.text,
            hasSprite = source.hasSprite,
            spriteKey = source.spriteKey,
            syllablePartA = source.syllablePartA,
            syllablePartB = source.syllablePartB
        };
    }

    private void DrawOrderedWordLayout()
    {
        if (level.orderedWords == null || level.orderedWords.Count == 0) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("GENERATED WORD ORDER (drag to swap)", EditorStyles.boldLabel);
        int visibleCount = Mathf.Min(level.orderedWords.Count, level.visibleRowCount * 4);
        for (int start = 0; start < level.orderedWords.Count; start += 4)
        {
            if (start == visibleCount) EditorGUILayout.LabelField("QUEUE", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + 4, level.orderedWords.Count); i++) DrawDraggableWord(i);
            }
        }
    }

    private void DrawDraggableWord(int index)
    {
        LevelWordEntry entry = level.orderedWords[index];
        string label = entry.word == null ? "Missing" : entry.word.hasSprite ? $"{entry.word.text} [Icon]" : entry.word.text;
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.miniButton,
            GUILayout.MinWidth(72), GUILayout.Height(24), GUILayout.ExpandWidth(true));
        GUI.Box(rect, label, EditorStyles.miniButton);
        Event current = Event.current;

        if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
        {
            pendingWordDragIndex = index;
            pendingWordDragStart = current.mousePosition;
            current.Use();
        }

        if (current.type == EventType.MouseDrag && pendingWordDragIndex == index &&
            Vector2.Distance(pendingWordDragStart, current.mousePosition) > 3f)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("WordGameOrderIndex", index);
            DragAndDrop.StartDrag(label);
            pendingWordDragIndex = -1;
            current.Use();
        }
        if ((current.type == EventType.DragUpdated || current.type == EventType.DragPerform) && rect.Contains(current.mousePosition))
        {
            object value = DragAndDrop.GetGenericData("WordGameOrderIndex");
            if (value is int sourceIndex && sourceIndex != index)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.RecordObject(level, "Swap Level Words");
                    LevelWordEntry temporary = level.orderedWords[sourceIndex];
                    level.orderedWords[sourceIndex] = level.orderedWords[index];
                    level.orderedWords[index] = temporary;
                    EditorUtility.SetDirty(level);
                    Repaint();
                }
                current.Use();
            }
        }

        if (current.rawType == EventType.MouseUp) pendingWordDragIndex = -1;
    }

    private void DrawTransformationList()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("TRANSFORMATIONS", EditorStyles.boldLabel);
        foreach (Category category in level.categories.Where(c => c.transformsOnComplete).ToList())
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{category.name}  →  {category.transformResult?.text}  →  {category.transformResultCategoryId}");
                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                {
                    Undo.RecordObject(level, "Remove Transformation");
                    category.transformsOnComplete = false;
                    category.transformResult = null;
                    category.transformResultCategoryId = "";
                    level.orderedWords?.Clear();
                    EditorUtility.SetDirty(level);
                }
            }
        }

        if (level.categories.Count == 0) return;
        string[] categoryNames = level.categories.Select(c => c.name).ToArray();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            newTransformSourceIndex = EditorGUILayout.Popup("Source Category", Mathf.Clamp(newTransformSourceIndex, 0, categoryNames.Length - 1), categoryNames);
            newTransformResult = EditorGUILayout.TextField("Result Word", newTransformResult);
            newTransformTargetIndex = EditorGUILayout.Popup("Target Category", Mathf.Clamp(newTransformTargetIndex, 0, categoryNames.Length - 1), categoryNames);
            if (GUILayout.Button("Add / Update Transformation"))
            {
                Category source = level.categories[newTransformSourceIndex];
                Category target = level.categories[newTransformTargetIndex];
                Undo.RecordObject(level, "Add Transformation");
                source.transformsOnComplete = true;
                source.transformResult = new WordItem { text = newTransformResult.Trim() };
                source.transformResultCategoryId = target.id;
                level.orderedWords?.Clear();
                EditorUtility.SetDirty(level);
            }
        }
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

            EditorGUI.BeginChangeCheck();
            bool transforms = EditorGUILayout.Toggle("Transforms On Complete", category.transformsOnComplete);
            string resultText = category.transformResult == null ? "" : category.transformResult.text;
            string targetCategory = category.transformResultCategoryId ?? "";
            if (transforms)
            {
                resultText = EditorGUILayout.TextField("Result Word", resultText);
                targetCategory = EditorGUILayout.TextField("Target Category ID", targetCategory);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Category Transformation");
                category.transformsOnComplete = transforms;
                category.transformResultCategoryId = targetCategory.Trim();
                if (transforms)
                {
                    if (category.transformResult == null) category.transformResult = new WordItem();
                    category.transformResult.text = resultText.Trim();
                }
                EditorUtility.SetDirty(level);
            }
        }
    }

    private void DrawValidation()
    {
        if (level.IsValid(out string error) && level.orderedWords != null && level.orderedWords.Count > 0)
            EditorGUILayout.HelpBox("Level is valid, ordered and ready to play.", MessageType.Info);
        else if (string.IsNullOrEmpty(error))
            EditorGUILayout.HelpBox("Categories are valid. Click Generate Solveable Order before saving.", MessageType.Warning);
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
        level.orderedWords?.Clear();
        EditorUtility.SetDirty(level);
        status = $"Added {source.name} to Level {level.levelNumber}";
    }

    private void RemoveCategory(int index)
    {
        Undo.RecordObject(level, "Remove Level Category");
        level.categories.RemoveAt(index);
        level.orderedWords?.Clear();
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
        AssetDatabase.SaveAssets();
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
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
            AssetDatabase.CreateFolder("Assets/_Game/Resources/Data", "Levels");
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
            transformsOnComplete = source.transformsOnComplete,
            transformResultCategoryId = source.transformResultCategoryId,
            transformResult = source.transformResult == null ? null : new WordItem
            {
                text = source.transformResult.text,
                hasSprite = source.transformResult.hasSprite,
                spriteKey = source.transformResult.spriteKey,
                syllablePartA = source.transformResult.syllablePartA,
                syllablePartB = source.transformResult.syllablePartB
            },
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
