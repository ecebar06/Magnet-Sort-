#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class SettingsGoldDisplayInstaller
{
    private const string CoinPath = "Assets/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Demo/Demo_ItemIcon/ItemIcon_Coins_s.png";

    static SettingsGoldDisplayInstaller() => EditorApplication.delayCall += Install;

    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        MagnetPopupUI popup = Object.FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        if (popup == null || popup.SettingsScreen == null) return;
        Transform panel = popup.SettingsScreen.transform.Find("Panel");
        if (panel == null || panel.Find("GoldDisplay") != null) return;

        GameObject display = new GameObject("GoldDisplay", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(display, "Add settings gold display");
        RectTransform displayRect = display.GetComponent<RectTransform>();
        displayRect.SetParent(panel, false);
        displayRect.anchorMin = new Vector2(0.055f, 0.83f);
        displayRect.anchorMax = new Vector2(0.31f, 0.96f);
        displayRect.offsetMin = Vector2.zero;
        displayRect.offsetMax = Vector2.zero;

        GameObject coinObject = new GameObject("GoldIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform coinRect = coinObject.GetComponent<RectTransform>();
        coinRect.SetParent(displayRect, false);
        coinRect.anchorMin = new Vector2(0f, 0.1f);
        coinRect.anchorMax = new Vector2(0.32f, 0.9f);
        coinRect.offsetMin = coinRect.offsetMax = Vector2.zero;
        Image coin = coinObject.GetComponent<Image>();
        coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
        coin.preserveAspect = true;
        coin.raycastTarget = false;

        GameObject textObject = new GameObject("GoldText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(displayRect, false);
        textRect.anchorMin = new Vector2(0.29f, 0f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = SaveSystem.Current.gold.ToString();
        text.fontSize = 36f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 36f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.raycastTarget = false;

        TextMeshProUGUI source = panel.Find("HeaderGroup/Title")?.GetComponent<TextMeshProUGUI>();
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.fontStyle = source.fontStyle;
        }

        EditorUtility.SetDirty(display);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = display;
        Debug.Log("Settings GoldDisplay eklendi. Ctrl+S ile sahneyi kaydet.", display);
    }
}
#endif
