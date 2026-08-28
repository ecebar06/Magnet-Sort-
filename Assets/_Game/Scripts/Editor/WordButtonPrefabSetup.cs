#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the runtime word prefab once, rather than styling every word while
/// the board is being created. Run again from the menu if the prefab is reset.
/// </summary>
public static class WordButtonPrefabSetup
{
    private const string PrefabPath = "Assets/_Game/Prefabs/Gameplay/WordButtonPrefab.prefab";
    private const string MagnetSpritePath = "Assets/_Game/Resources/Art/Fridge/Magnets/magnet_word.png";
    private const string VariableFontPath = "Assets/_Game/Resources/Fonts/Fredoka-Variable SDF.asset";
    private const string FallbackFontPath = "Assets/_Game/Resources/Fonts/Fredoka-Medium SDF.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleFirstSetup()
    {
        EditorApplication.delayCall += SetupIfNeeded;
    }

    private static void SetupIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SetupIfNeeded;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        WordButton wordButton = prefab == null ? null : prefab.GetComponent<WordButton>();
        if (wordButton == null) return;

        SerializedObject serialized = new SerializedObject(wordButton);
        bool needsSetup = serialized.FindProperty("background").objectReferenceValue == null ||
                          serialized.FindProperty("label").objectReferenceValue == null ||
                          serialized.FindProperty("iconImage").objectReferenceValue == null ||
                          serialized.FindProperty("button").objectReferenceValue == null ||
                          serialized.FindProperty("canvasGroup").objectReferenceValue == null;
        if (needsSetup) SetupPrefab();
    }

    [MenuItem("Tools/Word Game/Setup Word Button Prefab")]
    public static void SetupPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Image background = root.GetComponent<Image>();
            Button button = root.GetComponent<Button>();
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();

            TextMeshProUGUI label = root.GetComponentInChildren<TextMeshProUGUI>(true);
            if (background == null || button == null || label == null)
            {
                Debug.LogError("WordButtonPrefab is missing its Image, Button or TMP label.");
                return;
            }

            Image icon = FindOrCreateIcon(root.transform);
            Sprite magnetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MagnetSpritePath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(VariableFontPath) ??
                                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(215f, 140f);

            LayoutElement layout = root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = 215f;
                layout.preferredHeight = 140f;
            }

            background.sprite = magnetSprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;
            button.targetGraphic = background;

            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 32f;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(8f, 4f, 8f, 4f);
            label.fontStyle = FontStyles.Normal;
            label.color = new Color(0.16f, 0.12f, 0.28f, 1f);
            label.outlineWidth = 0f;
            label.raycastTarget = false;
            if (font != null) label.font = font;

            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.20f, 0.12f);
            iconRect.anchorMax = new Vector2(0.80f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            icon.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(root.GetComponent<WordButton>());
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.FindProperty("iconImage").objectReferenceValue = icon;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("WordButtonPrefab visuals and references are ready.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Image FindOrCreateIcon(Transform root)
    {
        Transform existing = root.Find("Icon");
        if (existing != null && existing.TryGetComponent(out Image existingImage)) return existingImage;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(root, false);
        return iconObject.GetComponent<Image>();
    }
}
#endif
