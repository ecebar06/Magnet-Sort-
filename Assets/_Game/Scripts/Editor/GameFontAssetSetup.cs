#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Generates persistent TextMeshPro SDF assets from the project's Fredoka
/// source fonts and assigns them to the Game scene and gameplay prefab.
/// Nothing in this setup is generated in a player build.
/// </summary>
public static class GameFontAssetSetup
{
    private const string VariableSource = "Assets/_Game/Resources/Fonts/Fredoka-VariableFont_wdth,wght.ttf";
    private const string MediumSource = "Assets/_Game/Resources/Fonts/Fredoka-Medium.ttf";
    private const string SemiBoldSource = "Assets/_Game/Resources/Fonts/Fredoka-SemiBold.ttf";
    private const string BoldSource = "Assets/_Game/Resources/Fonts/Fredoka-Bold.ttf";
    private const string DisplaySource = "Assets/_Game/Resources/Fonts/FredokaOne-Regular.ttf";
    private const string VariableAsset = "Assets/_Game/Resources/Fonts/Fredoka-Variable SDF.asset";
    private const string MediumAsset = "Assets/_Game/Resources/Fonts/Fredoka-Medium SDF.asset";
    private const string SemiBoldAsset = "Assets/_Game/Resources/Fonts/Fredoka-SemiBold SDF.asset";
    private const string BoldAsset = "Assets/_Game/Resources/Fonts/Fredoka-Bold SDF.asset";
    private const string DisplayAsset = "Assets/_Game/Resources/Fonts/FredokaOne-Regular SDF.asset";
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string WordPrefabPath = "Assets/_Game/Prefabs/Gameplay/WordButtonPrefab.prefab";

    // Printable ASCII plus symbols and Turkish glyphs that can appear in UI.
    private const string CharacterSet =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
        "abcdefghijklmnopqrstuvwxyz{|}~•×₺€£ÇĞİÖŞÜçğıöşü";

    [InitializeOnLoadMethod]
    private static void ScheduleMissingFontSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(VariableAsset) != null &&
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumAsset) != null &&
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldAsset) != null &&
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldAsset) != null &&
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayAsset) != null)
            return;

        EditorApplication.delayCall += GenerateMissingFontsWhenReady;
    }

    private static void GenerateMissingFontsWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += GenerateMissingFontsWhenReady;
            return;
        }

        try
        {
            // Opening the Editor may create missing assets, but must not reassign
            // scene/prefab fonts or rewrite an authored font's weight table.
            CreatePersistentSdf(VariableSource, VariableAsset, "Fredoka-Variable SDF");
            CreatePersistentSdf(MediumSource, MediumAsset, "Fredoka-Medium SDF");
            CreatePersistentSdf(SemiBoldSource, SemiBoldAsset, "Fredoka-SemiBold SDF");
            CreatePersistentSdf(BoldSource, BoldAsset, "Fredoka-Bold SDF");
            CreatePersistentSdf(DisplaySource, DisplayAsset, "FredokaOne-Regular SDF");
            AssetDatabase.SaveAssets();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Word Game/Generate And Assign TMP Font Assets")]
    public static void GenerateAndAssignFonts()
    {
        TMP_FontAsset variable = CreatePersistentSdf(VariableSource, VariableAsset, "Fredoka-Variable SDF");
        TMP_FontAsset medium = CreatePersistentSdf(MediumSource, MediumAsset, "Fredoka-Medium SDF");
        TMP_FontAsset semiBold = CreatePersistentSdf(SemiBoldSource, SemiBoldAsset, "Fredoka-SemiBold SDF");
        TMP_FontAsset bold = CreatePersistentSdf(BoldSource, BoldAsset, "Fredoka-Bold SDF");
        TMP_FontAsset display = CreatePersistentSdf(DisplaySource, DisplayAsset, "FredokaOne-Regular SDF");

        ConfigureFontWeights(variable, medium, semiBold, bold);
        AssignGameScene(variable, display);
        AssignWordPrefab(medium != null ? medium : variable);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Persistent Fredoka TMP SDF assets generated and assigned successfully.");
    }

    private static TMP_FontAsset CreatePersistentSdf(string sourcePath, string assetPath, string assetName)
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
            throw new InvalidOperationException($"Font source was not found: {sourcePath}");

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        FontEngine.InitializeFontEngine();
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            false);

        if (fontAsset == null)
            throw new InvalidOperationException($"TMP failed to create an SDF asset from {sourcePath}");

        fontAsset.name = assetName;
        if (!fontAsset.TryAddCharacters(CharacterSet, out string missingCharacters, true))
            Debug.LogWarning($"{assetName} is missing glyphs: {missingCharacters}");

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        foreach (Texture2D atlas in fontAsset.atlasTextures.Where(item => item != null))
        {
            atlas.name = assetName + " Atlas";
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = assetName + " Material";
            if (!AssetDatabase.Contains(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        return fontAsset;
    }

    private static void ConfigureFontWeights(
        TMP_FontAsset variable,
        TMP_FontAsset medium,
        TMP_FontAsset semiBold,
        TMP_FontAsset bold)
    {
        if (variable == null) return;

        // TMP uses indexes 1-9 for weights 100-900. The variable face is the
        // default/regular face; explicit SDF faces provide real heavier weights.
        TMP_FontWeightPair[] weights = variable.fontWeightTable;
        if (weights == null || weights.Length < 10) weights = new TMP_FontWeightPair[10];

        weights[5] = new TMP_FontWeightPair { regularTypeface = medium };
        weights[6] = new TMP_FontWeightPair { regularTypeface = semiBold };
        weights[7] = new TMP_FontWeightPair { regularTypeface = bold };

        SerializedObject serialized = new SerializedObject(variable);
        SerializedProperty table = serialized.FindProperty("m_FontWeightTable");
        table.arraySize = weights.Length;
        for (int index = 0; index < weights.Length; index++)
        {
            SerializedProperty pair = table.GetArrayElementAtIndex(index);
            pair.FindPropertyRelative("regularTypeface").objectReferenceValue = weights[index].regularTypeface;
            pair.FindPropertyRelative("italicTypeface").objectReferenceValue = weights[index].italicTypeface;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(variable);
    }

    private static void AssignGameScene(TMP_FontAsset variable, TMP_FontAsset display)
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup) scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        foreach (TextMeshProUGUI text in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<TextMeshProUGUI>(true)))
        {
            text.font = IsDisplayText(text.transform) ? display : variable;
            EditorUtility.SetDirty(text);
        }

        foreach (TableController controller in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<TableController>(true)))
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("gameFont").objectReferenceValue = variable;
            serialized.FindProperty("displayFont").objectReferenceValue = display;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool IsDisplayText(Transform text)
    {
        if (text == null) return false;
        if (text.name == "MovesTitle" || text.name == "MoveCount" || text.name == "LevelLabel")
            return true;
        return text.name == "Value" && text.parent != null && text.parent.name == "Knob";
    }

    private static void AssignWordPrefab(TMP_FontAsset wordFont)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WordPrefabPath);
        try
        {
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.font = wordFont;
                EditorUtility.SetDirty(text);
            }
            PrefabUtility.SaveAsPrefabAsset(root, WordPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
