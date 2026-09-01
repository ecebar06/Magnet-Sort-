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
    private const string StyleSettingsPath = "Assets/_Game/Resources/Data/WordButtonStyleSettings.asset";

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
                          serialized.FindProperty("canvasGroup").objectReferenceValue == null ||
                          serialized.FindProperty("backlight").objectReferenceValue == null ||
                          serialized.FindProperty("matchedOverlay").objectReferenceValue == null ||
                          serialized.FindProperty("styleSettings").objectReferenceValue == null;
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
            Image backlight = FindOrCreateBacklight(root.transform);
            Image matchedOverlay = FindOrCreateMatchedOverlay(root.transform);
            backlight.gameObject.layer = root.layer;
            matchedOverlay.gameObject.layer = root.layer;
            WordButtonStyleSettings styleSettings = LoadOrCreateStyleSettings();
            Sprite magnetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MagnetSpritePath);

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
            ColorBlock buttonColors = button.colors;
            buttonColors.disabledColor = Color.white;
            button.colors = buttonColors;

            label.raycastTarget = false;
            // All label visuals are authored by the designer in the prefab.

            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.20f, 0.12f);
            iconRect.anchorMax = new Vector2(0.80f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            icon.gameObject.SetActive(false);

            backlight.sprite = null;
            backlight.color = new Color(1f, 1f, 1f, 0f);
            backlight.raycastTarget = false;
            RectTransform backlightRect = backlight.rectTransform;
            backlightRect.anchorMin = Vector2.zero;
            backlightRect.anchorMax = Vector2.one;
            backlightRect.offsetMin = new Vector2(-8f, -8f);
            backlightRect.offsetMax = new Vector2(8f, 8f);
            backlight.transform.SetAsFirstSibling();
            backlight.gameObject.SetActive(false);

            matchedOverlay.sprite = background.sprite;
            matchedOverlay.type = background.type;
            matchedOverlay.color = styleSettings == null
                ? new Color(0.05f, 0.82f, 0.43f, 1f)
                : styleSettings.matchedWordAndRowColor;
            matchedOverlay.raycastTarget = false;
            RectTransform matchedRect = matchedOverlay.rectTransform;
            matchedRect.anchorMin = Vector2.zero;
            matchedRect.anchorMax = Vector2.one;
            matchedRect.offsetMin = Vector2.zero;
            matchedRect.offsetMax = Vector2.zero;
            matchedOverlay.transform.SetSiblingIndex(1);
            matchedOverlay.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(root.GetComponent<WordButton>());
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.FindProperty("iconImage").objectReferenceValue = icon;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serialized.FindProperty("backlight").objectReferenceValue = backlight;
            serialized.FindProperty("matchedOverlay").objectReferenceValue = matchedOverlay;
            serialized.FindProperty("styleSettings").objectReferenceValue = styleSettings;
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

    private static Image FindOrCreateBacklight(Transform root)
    {
        Transform existing = root.Find("Backlight");
        if (existing != null && existing.TryGetComponent(out Image existingImage)) return existingImage;

        GameObject lightObject = new GameObject("Backlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lightObject.transform.SetParent(root, false);
        return lightObject.GetComponent<Image>();
    }

    private static Image FindOrCreateMatchedOverlay(Transform root)
    {
        Transform existing = root.Find("MatchedOverlay");
        if (existing != null && existing.TryGetComponent(out Image existingImage)) return existingImage;

        GameObject overlayObject = new GameObject(
            "MatchedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.transform.SetParent(root, false);
        return overlayObject.GetComponent<Image>();
    }

    private static WordButtonStyleSettings LoadOrCreateStyleSettings()
    {
        WordButtonStyleSettings settings = AssetDatabase.LoadAssetAtPath<WordButtonStyleSettings>(StyleSettingsPath);
        if (settings != null) return settings;

        settings = ScriptableObject.CreateInstance<WordButtonStyleSettings>();
        AssetDatabase.CreateAsset(settings, StyleSettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }
}
#endif
