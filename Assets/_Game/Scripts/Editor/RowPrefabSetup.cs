#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the four permanent word slots stored inside RowPrefab.</summary>
public static class RowPrefabSetup
{
    private const string RowPrefabPath = "Assets/_Game/Prefabs/Gameplay/RowPrefab.prefab";
    private const string WordPrefabPath = "Assets/_Game/Prefabs/Gameplay/WordButtonPrefab.prefab";
    private const string HolderSpritePath = "Assets/_Game/Resources/Art/Fridge/Rows/magnet_holder.png";
    private const string CategoryCardSpritePath = "Assets/_Game/Resources/Art/Fridge/Rows/category_card.png";
    private const string OrangeSpritePath = "Assets/_Game/Resources/Art/Fridge/Magnets/magnet_orange.png";
    private const string FontPath = "Assets/_Game/Resources/Fonts/Fredoka-Medium SDF.asset";
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
    private const int SceneRowCount = 6;

    [InitializeOnLoadMethod]
    private static void BakeMissingSceneRowsOnEditorOpen()
    {
        EditorApplication.delayCall += () =>
        {
            // A delayed editor callback can survive until the exact frame in
            // which Play Mode starts. Never perform scene-authoring work then.
            if (Application.isPlaying || EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != GameScenePath) return;

            TableController table = Object.FindFirstObjectByType<TableController>();
            if (table == null) return;
            SerializedObject serializedTable = new SerializedObject(table);
            SerializedProperty pool = serializedTable.FindProperty("rowPool");
            if (pool != null && pool.arraySize == SceneRowCount)
            {
                bool complete = true;
                for (int index = 0; index < pool.arraySize; index++)
                    complete &= pool.GetArrayElementAtIndex(index).objectReferenceValue != null;
                if (complete) return;
            }

            BakeRowsIntoGameScene();
        };
    }

    [MenuItem("Tools/Word Game/Setup Row Prefab")]
    public static void SetupPrefab()
    {
        GameObject row = PrefabUtility.LoadPrefabContents(RowPrefabPath);
        try
        {
            GameObject wordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WordPrefabPath);
            ConfigureCategoryCardSlicing();
            Sprite holderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HolderSpritePath);
            Sprite categoryCardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CategoryCardSpritePath);
            Sprite orangeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OrangeSpritePath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (wordPrefab == null || holderSprite == null || categoryCardSprite == null || orangeSprite == null)
            {
                Debug.LogError("Row setup could not find WordButtonPrefab or magnet_holder.png.");
                return;
            }

            Image holder = row.GetComponent<Image>();
            holder.sprite = holderSprite;
            holder.type = Image.Type.Simple;
            holder.color = Color.white;
            holder.raycastTarget = false;

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(900f, 160f);

            // Rebuild only the four gameplay slots; category art will be added
            // in the next RowController task.
            for (int i = row.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = row.transform.GetChild(i);
                if (child.GetComponent<WordButton>() != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            WordButton[] slots = new WordButton[4];
            float[] sourceCenters = { 137f, 383f, 629f, 875f };
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(wordPrefab, row.transform);
                slot.name = $"Word Slot {i + 1}";
                RectTransform rect = slot.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(215f, 140f);
                rect.anchoredPosition = new Vector2((sourceCenters[i] / 1024f - 0.5f) * 900f, 0f);
                slots[i] = slot.GetComponent<WordButton>();
            }

            RowController controller = row.GetComponent<RowController>();
            if (controller == null) controller = row.AddComponent<RowController>();
            Image categoryCard = FindOrCreateImage(row.transform, "CategoryCard");
            categoryCard.sprite = categoryCardSprite;
            categoryCard.type = Image.Type.Sliced;
            // Preserve the original pill-shaped end caps of category_card.png
            // when the middle section expands for longer category names.
            categoryCard.pixelsPerUnitMultiplier = 19f;
            categoryCard.color = Color.white;
            categoryCard.raycastTarget = false;
            RectTransform cardRect = categoryCard.rectTransform;
            // The row keeps magnet_holder.png even after completion. The
            // category label floats just above its top-left edge.
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0f, 0f);
            cardRect.pivot = new Vector2(0f, 0.5f);
            // Anchored to the parent's bottom-left. Keep the sticker fully
            // above the word magnets, matching the reference composition.
            cardRect.anchoredPosition = new Vector2(72f, 200f);
            cardRect.sizeDelta = new Vector2(150f, 50f);
            CanvasGroup cardGroup = categoryCard.GetComponent<CanvasGroup>();
            if (cardGroup == null) cardGroup = categoryCard.gameObject.AddComponent<CanvasGroup>();
            Outline cardOutline = categoryCard.GetComponent<Outline>();
            if (cardOutline != null) Object.DestroyImmediate(cardOutline);

            Image orange = FindOrCreateImage(categoryCard.transform, "Orange");
            orange.sprite = orangeSprite;
            orange.preserveAspect = true;
            orange.color = Color.white;
            orange.raycastTarget = false;
            orange.gameObject.SetActive(true);
            RectTransform orangeRect = orange.rectTransform;
            orangeRect.anchorMin = orangeRect.anchorMax = new Vector2(0f, 0.5f);
            orangeRect.pivot = new Vector2(0.5f, 0.5f);
            // Reference composition: the orange overlaps the card, is roughly
            // 1.5x its height and sits slightly above its vertical centre.
            orangeRect.anchoredPosition = new Vector2(-20f, 8f);
            orangeRect.sizeDelta = new Vector2(74f, 74f);

            TextMeshProUGUI categoryLabel = FindOrCreateLabel(categoryCard.transform);
            if (font != null) categoryLabel.font = font;
            categoryLabel.fontSize = 26f;
            categoryLabel.enableAutoSizing = false;
            categoryLabel.fontStyle = FontStyles.Normal;
            categoryLabel.alignment = TextAlignmentOptions.Center;
            categoryLabel.color = new Color(0.14f, 0.10f, 0.28f, 1f);
            categoryLabel.raycastTarget = false;
            RectTransform labelRect = categoryLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            // Reserve the left side for the overlapping orange magnet so the
            // category text can never slide underneath it.
            labelRect.offsetMin = new Vector2(60f, 5f);
            labelRect.offsetMax = new Vector2(-28f, -5f);
            categoryCard.gameObject.SetActive(false);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("holderImage").objectReferenceValue = holder;
            SerializedProperty slotProperty = serialized.FindProperty("wordSlots");
            slotProperty.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                slotProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            serialized.FindProperty("normalHolderColor").colorValue = Color.white;
            serialized.FindProperty("completedHolderColor").colorValue = new Color(0.72f, 0.76f, 0.74f, 1f);
            serialized.FindProperty("categoryCard").objectReferenceValue = categoryCard;
            serialized.FindProperty("categoryOrange").objectReferenceValue = orange;
            serialized.FindProperty("categoryLabel").objectReferenceValue = categoryLabel;
            serialized.FindProperty("categoryCanvasGroup").objectReferenceValue = cardGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(row, RowPrefabPath);
            Debug.Log("RowPrefab now contains four permanent WordButtonPrefab slots.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(row);
        }
    }

    [MenuItem("Tools/Word Game/Bake Rows Into Game Scene")]
    public static void BakeRowsIntoGameScene()
    {
        if (Application.isPlaying || EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Rows can only be baked while the game is stopped.");
            return;
        }

        // Do not reopen Game.unity when it is already the active scene. Apart
        // from being unnecessary, OpenScene is forbidden during Play Mode.
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        TableController table = Object.FindFirstObjectByType<TableController>();
        GameObject rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (table == null || table.wordContainer == null || rowPrefab == null)
        {
            Debug.LogError("Could not find TableController, WordContainer or RowPrefab while baking Game.unity.");
            return;
        }

        Transform container = table.wordContainer;
        for (int index = container.childCount - 1; index >= 0; index--)
        {
            Transform child = container.GetChild(index);
            if (child.TryGetComponent(out RowController _))
                Object.DestroyImmediate(child.gameObject);
        }

        RowController[] rows = new RowController[SceneRowCount];
        for (int index = 0; index < rows.Length; index++)
        {
            GameObject rowObject = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab, container);
            rowObject.name = $"Scene Row {index + 1}";
            rows[index] = rowObject.GetComponent<RowController>();
            // Keep the first level visible and editable while the game is not
            // running. Runtime activates the exact amount required by a level.
            rowObject.SetActive(index < 2);
        }

        SerializedObject serializedTable = new SerializedObject(table);
        SerializedProperty pool = serializedTable.FindProperty("rowPool");
        pool.arraySize = rows.Length;
        for (int index = 0; index < rows.Length; index++)
            pool.GetArrayElementAtIndex(index).objectReferenceValue = rows[index];
        serializedTable.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(table);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Game.unity now contains {SceneRowCount} reusable scene rows. Runtime row creation is disabled.");
    }

    private static void ConfigureCategoryCardSlicing()
    {
        TextureImporter importer = AssetImporter.GetAtPath(CategoryCardSpritePath) as TextureImporter;
        if (importer == null) return;
        // Keep the complete rounded corners inside the 9-slice border. The
        // Image's Pixels Per Unit Multiplier scales those corners down at UI
        // size, leaving a straight, stretchable centre section.
        Vector4 border = new Vector4(105f, 132f, 105f, 132f);
        if (importer.spriteBorder == border) return;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static Image FindOrCreateImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Image image)) return image;
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        return target.GetComponent<Image>();
    }

    private static TextMeshProUGUI FindOrCreateLabel(Transform parent)
    {
        Transform existing = parent.Find("CategoryName");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI label)) return label;
        GameObject target = new GameObject("CategoryName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        return target.GetComponent<TextMeshProUGUI>();
    }
}
#endif
