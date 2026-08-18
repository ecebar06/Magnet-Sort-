#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WordIconLibrary))]
public class WordIconLibraryEditor : Editor
{
    private const string WordLibraryPath = "Assets/_Game/Resources/Data/WordLibrary.json";
    private const string IconLibraryPath = "Assets/_Game/Resources/Data/MainWordIconLibrary.asset";

    private enum IconFilter
    {
        All,
        MissingSprite,
        AssignedSprite
    }

    private sealed class IconWord
    {
        public Category category;
        public WordItem word;
    }

    private readonly List<IconWord> iconWords = new List<IconWord>();
    private string search = string.Empty;
    private IconFilter filter;
    private Vector2 scroll;
    private WordLibrary wordLibrary;
    private string loadError;

    [MenuItem("Tools/Word Game/Word Icon Library")]
    private static void SelectMainLibrary()
    {
        WordIconLibrary library = AssetDatabase.LoadAssetAtPath<WordIconLibrary>(IconLibraryPath);
        if (library == null)
        {
            EditorUtility.DisplayDialog("Word Icon Library", "MainWordIconLibrary.asset could not be found.", "OK");
            return;
        }

        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
    }

    private void OnEnable()
    {
        ReloadWords();
    }

    public override void OnInspectorGUI()
    {
        WordIconLibrary library = (WordIconLibrary)target;

        EditorGUILayout.LabelField("WORD ICON LIBRARY", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(160));
            filter = (IconFilter)EditorGUILayout.EnumPopup(filter, EditorStyles.toolbarPopup, GUILayout.Width(125));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) ReloadWords();
        }

        if (!string.IsNullOrEmpty(loadError))
        {
            EditorGUILayout.HelpBox(loadError, MessageType.Error);
            return;
        }

        int assigned = iconWords.Count(item => HasAssignedSprite(library, item.word));
        int missing = iconWords.Count - assigned;
        EditorGUILayout.HelpBox(
            $"Icon words: {iconWords.Count}    Sprites assigned: {assigned}    Missing sprites: {missing}",
            missing > 0 ? MessageType.Warning : MessageType.Info);

        IEnumerable<IconWord> results = iconWords.Where(item => MatchesSearch(item, search));
        if (filter == IconFilter.MissingSprite)
            results = results.Where(item => !HasAssignedSprite(library, item.word));
        else if (filter == IconFilter.AssignedSprite)
            results = results.Where(item => HasAssignedSprite(library, item.word));

        List<IconWord> visible = results.ToList();
        EditorGUILayout.LabelField($"Showing {visible.Count} result(s)", EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(220));
        foreach (IconWord item in visible) DrawIconWord(library, item);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Changes", GUILayout.Height(28))) SaveAll();
            if (GUILayout.Button("Select WordLibrary.json", GUILayout.Height(28)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(WordLibraryPath);
            }
        }
    }

    private void DrawIconWord(WordIconLibrary library, IconWord item)
    {
        WordItem word = item.word;
        EnsureSpriteKey(item.category, word);
        library.TryGetIcon(word.spriteKey, out Sprite currentSprite);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"{item.category.name} / {word.text}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect previewRect = GUILayoutUtility.GetRect(52, 52, GUILayout.Width(52), GUILayout.Height(52));
                if (currentSprite != null)
                    GUI.DrawTexture(previewRect, currentSprite.texture, ScaleMode.ScaleToFit, true);
                else
                    EditorGUI.HelpBox(previewRect, "?", MessageType.None);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string nextKey = EditorGUILayout.TextField("Key", word.spriteKey);
                    Sprite nextSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", currentSprite, typeof(Sprite), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(library, "Edit Word Icon");
                        string oldKey = word.spriteKey;
                        word.spriteKey = string.IsNullOrWhiteSpace(nextKey)
                            ? BuildSpriteKey(item.category, word)
                            : nextKey.Trim();

                        if (!string.Equals(oldKey, word.spriteKey, StringComparison.Ordinal))
                            library.RemoveIcon(oldKey);

                        library.SetIcon(word.spriteKey, nextSprite);
                        EditorUtility.SetDirty(library);
                        SaveWordLibrary();
                    }
                }
            }
        }
    }

    private void ReloadWords()
    {
        iconWords.Clear();
        loadError = null;
        TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(WordLibraryPath);
        if (source == null)
        {
            loadError = $"Word library was not found at {WordLibraryPath}";
            return;
        }

        wordLibrary = JsonUtility.FromJson<WordLibrary>(source.text);
        if (wordLibrary == null || wordLibrary.categories == null)
        {
            loadError = "WordLibrary.json could not be read.";
            return;
        }

        foreach (Category category in wordLibrary.categories.Where(category => category != null))
        {
            if (category.words != null)
            {
                foreach (WordItem word in category.words.Where(word => word != null && word.hasSprite))
                {
                    EnsureSpriteKey(category, word);
                    iconWords.Add(new IconWord { category = category, word = word });
                }
            }

            if (category.transformsOnComplete && category.transformResult != null && category.transformResult.hasSprite)
            {
                EnsureSpriteKey(category, category.transformResult);
                iconWords.Add(new IconWord { category = category, word = category.transformResult });
            }
        }

        WordIconLibrary iconLibrary = (WordIconLibrary)target;
        Undo.RecordObject(iconLibrary, "Synchronize Word Icon Library");
        iconLibrary.SynchronizeKeys(iconWords.Select(item => item.word.spriteKey));
        EditorUtility.SetDirty(iconLibrary);
        SaveWordLibrary();
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private static bool HasAssignedSprite(WordIconLibrary library, WordItem word)
    {
        return word != null && !string.IsNullOrWhiteSpace(word.spriteKey) &&
               library.TryGetIcon(word.spriteKey, out Sprite sprite) && sprite != null;
    }

    private static bool MatchesSearch(IconWord item, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return Contains(item.category.name, query) || Contains(item.category.id, query) ||
               Contains(item.word.text, query) || Contains(item.word.spriteKey, query);
    }

    private static bool Contains(string source, string query)
    {
        return !string.IsNullOrEmpty(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void EnsureSpriteKey(Category category, WordItem word)
    {
        if (string.IsNullOrWhiteSpace(word.spriteKey)) word.spriteKey = BuildSpriteKey(category, word);
    }

    private static string BuildSpriteKey(Category category, WordItem word)
    {
        return $"{Slug(category.id)}__{Slug(word.text)}";
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "word";
        char[] chars = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
        return new string(chars).Trim('_');
    }

    private void SaveAll()
    {
        SaveWordLibrary();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void SaveWordLibrary()
    {
        if (wordLibrary == null) return;
        File.WriteAllText(Path.GetFullPath(WordLibraryPath), JsonUtility.ToJson(wordLibrary, true));
    }
}
#endif
