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
    private const string LevelsFolder = "Assets/_Game/Resources/Data/Levels";

    private WordLibrary library;
    private LevelDatabase database;
    private readonly Dictionary<LevelData, string> levelJsonPaths = new Dictionary<LevelData, string>();
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
    private int newTransformWordIndex;

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
        LoadJsonLevels();
        string migrationKey = $"WordSort.WordIconMigration.{Application.dataPath}";
        if (!EditorPrefs.GetBool(migrationKey, false))
        {
            bool migrated = MigrateLevelIconsToWordLibrary();
            EditorPrefs.SetBool(migrationKey, true);
            if (migrated) SaveLibrary();
        }
        SynchronizeAllLevelIconsFromLibrary();
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

    private void LoadJsonLevels()
    {
        database = new LevelDatabase();
        database.levels = new List<LevelData>();
        levelJsonPaths.Clear();
        if (!AssetDatabase.IsValidFolder(LevelsFolder)) return;

        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            LevelData loaded = textAsset == null ? null : LevelData.FromJson(textAsset.text);
            if (loaded == null || loaded.levelNumber < 1) continue;
            database.levels.Add(loaded);
            levelJsonPaths[loaded] = path;
        }

        database.levels = database.levels.OrderBy(item => item.levelNumber).ToList();
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
                    if (EditorGUI.EndChangeCheck())
                    {
                        foreach (WordItem word in category.words)
                        {
                            if (word.hasSprite && string.IsNullOrWhiteSpace(word.spriteKey))
                                word.spriteKey = BuildSpriteKey(category.id, word.text);
                            if (!word.hasSprite)
                            {
                                if (iconLibrary != null) iconLibrary.RemoveIcon(word.spriteKey);
                                word.spriteKey = "";
                            }
                        }
                        SynchronizeAllLevelIconsFromLibrary();
                        SaveLibrary();
                    }
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

        int recommendationCategoryCount = level.expectedCategoryCount > 0
            ? level.expectedCategoryCount
            : level.categories.Count;
        int recommendedMoves = RecommendedMoveCount(recommendationCategoryCount, level.visibleRowCount);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"Recommended Moves: {recommendedMoves} (estimate)");
            if (GUILayout.Button($"Use {recommendedMoves}", GUILayout.Width(80)))
            {
                Undo.RecordObject(level, "Use Recommended Move Count");
                level.moveCount = recommendedMoves;
                EditorUtility.SetDirty(level);
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"LEVEL CATEGORIES ({level.categories.Count} rows)", EditorStyles.boldLabel);
        DrawValidation();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Build Level", GUILayout.Height(28))) AutoBuildLevel();
            if (GUILayout.Button("Auto Set Transformations", GUILayout.Height(28))) AutoSetTransformations();
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

    private void AutoSetTransformations()
    {
        if (level.categories == null || level.categories.Count == 0)
        {
            status = "Add categories before creating transformations.";
            return;
        }

        int effectiveRows = level.visibleRowCount > 0 ? level.visibleRowCount : level.categories.Count;
        int required = level.categories.Count - effectiveRows;
        if (required < 0)
        {
            status = "Visible row count cannot exceed category count.";
            return;
        }
        if (level.categories.Any(category => category.words == null || category.words.Count != 4))
        {
            status = "Every category must contain exactly four words first.";
            return;
        }
        if (level.categories.Any(category => category.transformsOnComplete) &&
            !EditorUtility.DisplayDialog("Replace Transformations",
                "Replace the current transformation setup with an automatic cycle-free chain?",
                "Replace", "Cancel"))
            return;

        Undo.RecordObject(level, "Auto Set Level Transformations");
        int applied = ApplyAutomaticTransformations();
        EditorUtility.SetDirty(level);
        status = applied > 0
            ? $"Added {applied} cycle-free transformations. Review them, then generate the order."
            : "This level does not need transformations.";
    }

    private int ApplyAutomaticTransformations()
    {
        int effectiveRows = level.visibleRowCount > 0 ? level.visibleRowCount : level.categories.Count;
        int required = level.categories.Count - effectiveRows;
        foreach (Category category in level.categories)
        {
            category.transformsOnComplete = false;
            category.transformResult = new WordItem();
            category.transformResultCategoryId = string.Empty;
        }
        for (int index = 0; index < required; index++)
        {
            Category source = level.categories[index];
            Category target = level.categories[index + 1];
            source.transformsOnComplete = true;
            source.transformResultCategoryId = target.id;
            source.transformResult = CloneWord(target.words[0]);
        }

        level.orderedWords?.Clear();
        return required;
    }

    private void AutoBuildLevel()
    {
        if (library?.categories == null || library.categories.Count == 0)
        {
            status = "WordLibrary.json is empty.";
            return;
        }

        int desiredCount = level.expectedCategoryCount;
        int visibleRows = level.visibleRowCount;
        if (desiredCount < 1)
        {
            status = "Set Category Count before building the level.";
            return;
        }
        if (visibleRows < 1 || visibleRows > desiredCount)
        {
            status = "Visible Row Count must be between 1 and Category Count.";
            return;
        }
        if (level.categories.Count > 0 && !EditorUtility.DisplayDialog("Replace Level Content",
                "Replace the current categories, transformations and generated order?",
                "Replace", "Cancel"))
            return;

        List<Category> candidates = library.categories
            .Where(category => category?.words != null && category.words.Count == 4 &&
                category.words.All(word => !string.IsNullOrWhiteSpace(word.text)) &&
                category.words.Select(word => word.text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4)
            .ToList();
        List<Category> selected = new List<Category>();
        HashSet<string> usedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int startIndex = candidates.Count > 0 ? ((level.levelNumber - 1) * desiredCount) % candidates.Count : 0;
        for (int step = 0; step < candidates.Count && selected.Count < desiredCount; step++)
        {
            Category candidate = candidates[(startIndex + step) % candidates.Count];
            List<string> words = candidate.words.Select(word => word.text.Trim()).ToList();
            if (words.Any(usedWords.Contains)) continue;
            selected.Add(candidate);
            foreach (string word in words) usedWords.Add(word);
        }
        if (selected.Count != desiredCount)
        {
            status = $"Could only find {selected.Count}/{desiredCount} categories without repeated words.";
            return;
        }

        Undo.RecordObject(level, "Auto Build Level");
        level.categories = selected.Select(CloneCategory).ToList();
        ApplyAutomaticTransformations();
        EditorUtility.SetDirty(level);
        GenerateSolveableOrder();
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
        List<Category> fixedCategories = level.categories.Where(c => !c.transformsOnComplete).ToList();
        int visibleWordCount = level.visibleRowCount * 4;
        int playableWordCount = level.categories.Sum(category =>
            category.words.Count(word => !generatedWords.Contains(word.text)));
        int queueWordCount = playableWordCount - visibleWordCount;

        if (queueWordCount != transforms.Count * 3)
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

        List<(Category category, List<WordItem> words)> transformGroups = transforms
            .Select(category => (category, category.words
                .Where(word => !generatedWords.Contains(word.text)).ToList()))
            .ToList();
        int rootTransformIndex = transformGroups.FindIndex(group => group.words.Count == 4);
        if (rootTransformIndex < 0 || transformGroups.Any(group => group.words.Count < 3 || group.words.Count > 4))
        {
            status = "Cannot generate: transformations need one root category with four playable words and three or four playable words thereafter.";
            return;
        }

        (Category category, List<WordItem> words) rootTransform = transformGroups[rootTransformIndex];
        transformGroups.RemoveAt(rootTransformIndex);
        transformGroups.Insert(0, rootTransform);

        List<LevelWordEntry> visible = transformGroups[0].words
            .Select(w => Entry(transformGroups[0].category.id, w)).ToList();
        List<LevelWordEntry> queue = new List<LevelWordEntry>();
        for (int i = 1; i < transformGroups.Count; i++)
        {
            (Category category, List<WordItem> words) group = transformGroups[i];
            if (group.words.Count == 4)
                visible.Add(Entry(group.category.id, group.words[0]));
            queue.AddRange(group.words.Skip(group.words.Count == 4 ? 1 : 0)
                .Select(word => Entry(group.category.id, word)));
        }

        List<(Category category, List<WordItem> words)> fixedGroups = fixedCategories
            .Select(category => (category, category.words.Where(word => !generatedWords.Contains(word.text)).ToList()))
            .Where(group => group.Item2.Count > 0)
            .ToList();

        // Every transformation reveals three queued words. Later source categories use
        // the first groups; the final group must also be a completeable set of three.
        int finalGroupIndex = fixedGroups.FindIndex(group => group.words.Count == 3);
        if (finalGroupIndex < 0)
            finalGroupIndex = fixedGroups.FindLastIndex(group => group.words.Count == 4);
        if (finalGroupIndex < 0)
        {
            status = "Cannot generate: no category can receive the final three queued words.";
            return;
        }

        (Category category, List<WordItem> words) finalGroup = fixedGroups[finalGroupIndex];
        fixedGroups.RemoveAt(finalGroupIndex);
        if (finalGroup.words.Count == 4)
            visible.Add(Entry(finalGroup.category.id, finalGroup.words[0]));
        queue.AddRange(finalGroup.words.Skip(Mathf.Max(0, finalGroup.words.Count - 3))
            .Select(word => Entry(finalGroup.category.id, word)));

        visible.AddRange(fixedGroups.SelectMany(group =>
            group.words.Select(word => Entry(group.category.id, word))));

        if (visible.Count != visibleWordCount || queue.Count != queueWordCount)
        {
            status = $"Cannot generate a complete order: created {visible.Count}/{visibleWordCount} visible and {queue.Count}/{queueWordCount} queued words.";
            return;
        }

        visible = InterleaveVisibleWords(visible);
        Undo.RecordObject(level, "Generate Solveable Level Order");
        level.orderedWords = visible.Concat(queue).ToList();
        EditorUtility.SetDirty(level);
        status = $"Generated {visible.Count} visible words and {queue.Count} queued words.";
    }

    private static LevelWordEntry Entry(string categoryId, WordItem source)
    {
        return new LevelWordEntry { categoryId = categoryId, word = CloneWord(source) };
    }

    private static List<LevelWordEntry> InterleaveVisibleWords(IEnumerable<LevelWordEntry> source)
    {
        List<Queue<LevelWordEntry>> categoryQueues = source
            .GroupBy(entry => entry.categoryId)
            .Select(group => new Queue<LevelWordEntry>(group))
            .ToList();
        List<LevelWordEntry> result = new List<LevelWordEntry>();
        while (categoryQueues.Any(queue => queue.Count > 0))
        {
            foreach (Queue<LevelWordEntry> queue in categoryQueues)
                if (queue.Count > 0) result.Add(queue.Dequeue());
        }
        return result;
    }

    private static WordItem CloneWord(WordItem source)
    {
        return source == null ? new WordItem() : new WordItem
        {
            text = source.text,
            hasSprite = source.hasSprite,
            useIcon = source.useIcon,
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
        string label = entry.word == null ? "Missing" : entry.word.useIcon ? $"{entry.word.text} [Icon]" : entry.word.text;
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
            newTransformTargetIndex = EditorGUILayout.Popup("Target Category", Mathf.Clamp(newTransformTargetIndex, 0, categoryNames.Length - 1), categoryNames);
            Category selectedTarget = level.categories[newTransformTargetIndex];
            List<WordItem> targetWords = selectedTarget.words ?? new List<WordItem>();
            string[] targetWordNames = targetWords.Select(word => word == null || string.IsNullOrWhiteSpace(word.text) ? "Missing Word" : word.text).ToArray();
            GUI.enabled = targetWordNames.Length > 0;
            newTransformWordIndex = targetWordNames.Length == 0
                ? 0
                : EditorGUILayout.Popup("Result Word", Mathf.Clamp(newTransformWordIndex, 0, targetWordNames.Length - 1), targetWordNames);
            GUI.enabled = true;
            if (GUILayout.Button("Add / Update Transformation"))
            {
                Category source = level.categories[newTransformSourceIndex];
                Category target = level.categories[newTransformTargetIndex];
                if (target.words == null || target.words.Count == 0)
                {
                    status = "Target category has no words to select.";
                    return;
                }
                Undo.RecordObject(level, "Add Transformation");
                source.transformsOnComplete = true;
                source.transformResult = CloneWord(target.words[Mathf.Clamp(newTransformWordIndex, 0, target.words.Count - 1)]);
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
                        string label = word.useIcon ? $"◆ {word.text}" : word.text;
                        bool nextUseIcon = GUILayout.Toggle(word.useIcon, label, EditorStyles.miniButton,
                            GUILayout.MinWidth(65));
                        if (nextUseIcon != word.useIcon)
                            SetLevelWordIconUsage(category.id, word.text, nextUseIcon);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            bool transforms = EditorGUILayout.Toggle("Transforms On Complete", category.transformsOnComplete);
            string resultText = category.transformResult == null ? "" : category.transformResult.text;
            string targetCategory = category.transformResultCategoryId ?? "";
            WordItem selectedResultWord = category.transformResult;
            if (transforms)
            {
                string[] targetNames = level.categories.Select(item => item.name).ToArray();
                int targetIndex = Mathf.Max(0, level.categories.FindIndex(item => item.id == targetCategory));
                targetIndex = EditorGUILayout.Popup("Target Category", targetIndex, targetNames);
                Category target = level.categories[targetIndex];
                targetCategory = target.id;

                List<WordItem> words = target.words ?? new List<WordItem>();
                string[] wordNames = words.Select(word => word == null || string.IsNullOrWhiteSpace(word.text) ? "Missing Word" : word.text).ToArray();
                int resultIndex = Mathf.Max(0, words.FindIndex(word => word != null &&
                    string.Equals(word.text, resultText, StringComparison.OrdinalIgnoreCase)));
                GUI.enabled = wordNames.Length > 0;
                if (wordNames.Length > 0)
                {
                    resultIndex = EditorGUILayout.Popup("Result Word", resultIndex, wordNames);
                    selectedResultWord = words[resultIndex];
                    resultText = selectedResultWord.text;
                }
                else EditorGUILayout.LabelField("Result Word", "Target has no words");
                GUI.enabled = true;
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Category Transformation");
                category.transformsOnComplete = transforms;
                category.transformResultCategoryId = targetCategory.Trim();
                if (transforms)
                {
                    category.transformResult = CloneWord(selectedResultWord ?? new WordItem { text = resultText.Trim() });
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

    private bool MigrateLevelIconsToWordLibrary()
    {
        if (library?.categories == null || database?.levels == null) return false;
        bool changed = false;
        foreach (LevelData storedLevel in database.levels.Where(item => item != null && item.categories != null))
        {
            foreach (Category levelCategory in storedLevel.categories.Where(item => item != null))
            {
                changed |= MigrateWords(levelCategory.id, levelCategory.words);
                if (levelCategory.transformsOnComplete && levelCategory.transformResult != null)
                    changed |= MigrateWords(levelCategory.transformResultCategoryId,
                        new[] { levelCategory.transformResult });
            }

            foreach (LevelWordEntry entry in storedLevel.orderedWords ?? new List<LevelWordEntry>())
                if (entry?.word != null) changed |= MigrateWords(entry.categoryId, new[] { entry.word });
        }
        return changed;
    }

    private bool MigrateWords(string categoryId, IEnumerable<WordItem> levelWords)
    {
        Category masterCategory = library.categories.FirstOrDefault(item => item != null && item.id == categoryId);
        if (masterCategory?.words == null || levelWords == null) return false;
        bool changed = false;
        foreach (WordItem levelWord in levelWords.Where(item => item != null && item.hasSprite))
        {
            WordItem master = masterCategory.words.FirstOrDefault(item => item != null &&
                string.Equals(item.text, levelWord.text, StringComparison.OrdinalIgnoreCase));
            if (master == null) continue;
            if (!master.hasSprite)
            {
                master.hasSprite = true;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(master.spriteKey))
            {
                master.spriteKey = string.IsNullOrWhiteSpace(levelWord.spriteKey)
                    ? BuildSpriteKey(masterCategory.id, master.text)
                    : levelWord.spriteKey;
                changed = true;
            }
        }
        return changed;
    }

    private void SynchronizeAllLevelIconsFromLibrary()
    {
        if (library?.categories == null || database?.levels == null) return;
        bool anyChanged = false;
        foreach (LevelData storedLevel in database.levels.Where(item => item != null))
        {
            bool changed = false;
            foreach (Category levelCategory in storedLevel.categories ?? new List<Category>())
            {
                changed |= SynchronizeWords(levelCategory.id, levelCategory.words);
                if (levelCategory.transformsOnComplete && levelCategory.transformResult != null)
                    changed |= SynchronizeWords(levelCategory.transformResultCategoryId,
                        new[] { levelCategory.transformResult });
            }
            foreach (LevelWordEntry entry in storedLevel.orderedWords ?? new List<LevelWordEntry>())
                if (entry?.word != null) changed |= SynchronizeWords(entry.categoryId, new[] { entry.word });
            if (changed)
            {
                WriteLevelJson(storedLevel, false);
                anyChanged = true;
            }
        }
        if (anyChanged) AssetDatabase.Refresh();
    }

    private bool SynchronizeWords(string categoryId, IEnumerable<WordItem> levelWords)
    {
        Category masterCategory = library.categories.FirstOrDefault(item => item != null && item.id == categoryId);
        if (masterCategory?.words == null || levelWords == null) return false;
        bool changed = false;
        foreach (WordItem levelWord in levelWords.Where(item => item != null))
        {
            WordItem master = masterCategory.words.FirstOrDefault(item => item != null &&
                string.Equals(item.text, levelWord.text, StringComparison.OrdinalIgnoreCase));
            if (master == null) continue;
            if (levelWord.hasSprite != master.hasSprite || levelWord.spriteKey != master.spriteKey)
            {
                levelWord.hasSprite = master.hasSprite;
                levelWord.spriteKey = master.spriteKey;
                changed = true;
            }
        }
        return changed;
    }

    private void SetLevelWordIconUsage(string categoryId, string wordText, bool useIcon)
    {
        Undo.RecordObject(level, "Toggle Level Word Icon");
        Category masterCategory = library?.categories?.FirstOrDefault(item => item != null && item.id == categoryId);
        WordItem master = masterCategory?.words?.FirstOrDefault(item => item != null &&
            string.Equals(item.text, wordText, StringComparison.OrdinalIgnoreCase));

        if (useIcon && master != null)
        {
            master.hasSprite = true;
            if (string.IsNullOrWhiteSpace(master.spriteKey))
                master.spriteKey = BuildSpriteKey(categoryId, master.text);
            SaveLibrary();
        }

        void Apply(WordItem item)
        {
            if (item == null || !string.Equals(item.text, wordText, StringComparison.OrdinalIgnoreCase)) return;
            item.useIcon = useIcon;
            if (master != null)
            {
                item.hasSprite = master.hasSprite;
                item.spriteKey = master.spriteKey;
            }
        }

        foreach (Category category in level.categories ?? new List<Category>())
        {
            if (category.id == categoryId)
                foreach (WordItem item in category.words ?? new List<WordItem>()) Apply(item);
            if (category.transformResultCategoryId == categoryId) Apply(category.transformResult);
        }
        foreach (LevelWordEntry entry in level.orderedWords ?? new List<LevelWordEntry>())
            if (entry?.categoryId == categoryId) Apply(entry.word);

        EditorUtility.SetDirty(level);
        status = useIcon
            ? $"{wordText} will use its icon in this level (text fallback remains enabled)."
            : $"{wordText} will use text in this level.";
    }

    private static string BuildSpriteKey(string categoryId, string word)
    {
        return $"{Slug(categoryId)}__{Slug(word)}";
    }

    private void CreateNewLevel()
    {
        EnsureDatabase();
        int number = database.levels.Where(l => l != null).Select(l => l.levelNumber).DefaultIfEmpty(0).Max() + 1;
        LevelData created = CreateInstance<LevelData>();
        created.levelNumber = number;
        created.moveCount = 12;
        created.categories = new List<Category>();
        database.levels.Add(created);
        selectedLevelIndex = database.levels.Count - 1;
        level = created;
        WriteLevelJson(created);
        status = $"Created Level {number}";
    }

    private void DeleteCurrentLevel()
    {
        levelJsonPaths.TryGetValue(level, out string path);
        database.levels.Remove(level);
        levelJsonPaths.Remove(level);
        if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        DestroyImmediate(level);
        selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, Mathf.Max(0, database.levels.Count - 1));
        level = database.levels.Count == 0 ? null : database.levels[selectedLevelIndex];
    }

    private void SaveCurrentLevel()
    {
        WriteLevelJson(level);
        status = level.IsValid(out string error) ? $"Level {level.levelNumber} saved." : $"Saved with warning: {error}";
    }

    private void WriteLevelJson(LevelData storedLevel, bool refresh = true)
    {
        if (storedLevel == null) return;
        EnsureDatabase();
        string desiredPath = $"{LevelsFolder}/Level_{storedLevel.levelNumber:000}.json";
        if (levelJsonPaths.TryGetValue(storedLevel, out string previousPath) &&
            !string.Equals(previousPath, desiredPath, StringComparison.OrdinalIgnoreCase) &&
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(previousPath) != null)
        {
            AssetDatabase.DeleteAsset(previousPath);
        }

        File.WriteAllText(Path.GetFullPath(desiredPath), storedLevel.ToJson());
        levelJsonPaths[storedLevel] = desiredPath;
        if (refresh)
        {
            AssetDatabase.ImportAsset(desiredPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }
    }

    private void EnsureDatabase()
    {
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
            AssetDatabase.CreateFolder("Assets/_Game/Resources/Data", "Levels");
        if (database != null) return;
        database = new LevelDatabase();
        database.levels = new List<LevelData>();
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
                useIcon = source.transformResult.useIcon,
                spriteKey = source.transformResult.spriteKey,
                syllablePartA = source.transformResult.syllablePartA,
                syllablePartB = source.transformResult.syllablePartB
            },
            words = source.words == null ? new List<WordItem>() : source.words.Select(w => new WordItem
            {
                text = w.text,
                hasSprite = w.hasSprite,
                useIcon = w.useIcon,
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

    private static int RecommendedMoveCount(int categoryCount, int visibleRows)
    {
        categoryCount = Mathf.Max(1, categoryCount);
        visibleRows = Mathf.Max(1, visibleRows);
        int transformations = Mathf.Max(0, categoryCount - visibleRows);
        return categoryCount * 3 + transformations;
    }
}
#endif
