using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MagnetPopupUI : MonoBehaviour
{
    public static MagnetPopupUI Instance { get; private set; }
    private const string ArtPath = "Art/UI/Popups/";

    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject timeUpScreen;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button spendGoldButton;
    [SerializeField] private TextMeshProUGUI explanationText;
    [SerializeField] private TextMeshProUGUI goldPriceText;
    private TextMeshProUGUI settingsGoldText;

    public GameObject SettingsScreen => settingsScreen;
    public GameObject TimeUpScreen => timeUpScreen;
    public Button RestartButton => restartButton;
    public Button WatchAdButton => watchAdButton;
    public Button SpendGoldButton => spendGoldButton;
    public TextMeshProUGUI ExplanationText => explanationText;
    public TextMeshProUGUI GoldPriceText => goldPriceText;

    private Image soundBackground;
    private Image musicBackground;
    private Image hapticBackground;

    private void Awake()
    {
        Instance = this;
        ResolveSceneReferences();
        EnsurePopupCanvas();
        if (settingsScreen != null) settingsScreen.SetActive(false);
        if (timeUpScreen != null) timeUpScreen.SetActive(false);
        WireSettingsButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsurePopupCanvas()
    {
        Canvas popupCanvas = GetComponent<Canvas>();
        if (popupCanvas == null) popupCanvas = gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 500;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void Start()
    {
        ResolveSceneReferences();
        WireSettingsButtons();
        WireSettingsLaunchers();
    }

    private void WireSettingsLaunchers()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.name != "SettingsButton" || button.gameObject.scene != gameObject.scene) continue;
            button.onClick.RemoveListener(ShowSettings);
            button.onClick.AddListener(ShowSettings);
        }
    }

    private void WireSettingsButtons()
    {
        if (settingsScreen == null) return;
        Transform panel = settingsScreen.transform.Find("Panel");
        Button close = panel?.Find("CloseButton")?.GetComponent<Button>();
        Button sound = panel?.Find("Options/SoundGroup/Background")?.GetComponent<Button>();
        Button music = panel?.Find("Options/MusicGroup/Background")?.GetComponent<Button>();
        Button haptic = panel?.Find("Options/HapticGroup/Background")?.GetComponent<Button>();
        Wire(close, HideSettings);
        Wire(sound, ToggleSound);
        Wire(music, ToggleMusic);
        Wire(haptic, ToggleHaptic);
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ResolveSceneReferences()
    {
        if (settingsScreen == null)
            settingsScreen = transform.Find("SettingsScreen")?.gameObject;
        if (timeUpScreen == null)
            timeUpScreen = transform.Find("TimeUpScreen")?.gameObject;
        if (settingsScreen != null)
        {
            Transform panel = settingsScreen.transform.Find("Panel");
            settingsGoldText = panel?.Find("GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
            soundBackground = panel?.Find("Options/SoundGroup/Background")?.GetComponent<Image>();
            musicBackground = panel?.Find("Options/MusicGroup/Background")?.GetComponent<Image>();
            hapticBackground = panel?.Find("Options/HapticGroup/Background")?.GetComponent<Image>();
            if (restartButton == null)
                restartButton = panel?.Find("RestartGroup/Background")?.GetComponent<Button>();
        }
        if (timeUpScreen != null)
        {
            Transform panel = timeUpScreen.transform.Find("Panel");
            if (watchAdButton == null)
                watchAdButton = panel?.Find("WatchAdGroup/Background")?.GetComponent<Button>();
            if (spendGoldButton == null)
                spendGoldButton = panel?.Find("GoldGroup/Background")?.GetComponent<Button>();
            if (explanationText == null)
                explanationText = panel?.Find("Explanation")?.GetComponent<TextMeshProUGUI>();
            if (goldPriceText == null)
                goldPriceText = panel?.Find("GoldGroup/Price")?.GetComponent<TextMeshProUGUI>();
        }
    }

    public static MagnetPopupUI CreateAuthored(RectTransform parent)
    {
        GameObject root = Rect("MagnetPopupUI", parent);
        Stretch(root.GetComponent<RectTransform>());
        MagnetPopupUI popup = root.AddComponent<MagnetPopupUI>();
        popup.Build();
        return popup;
    }

    private void Build()
    {
        settingsScreen = CreateScreen("SettingsScreen");
        BuildSettings(settingsScreen.transform);
        settingsScreen.SetActive(false);
        timeUpScreen = CreateScreen("TimeUpScreen");
        BuildTimeUp(timeUpScreen.transform);
        timeUpScreen.SetActive(false);
    }

    private GameObject CreateScreen(string screenName)
    {
        GameObject screen = Rect(screenName, transform);
        Stretch(screen.GetComponent<RectTransform>());
        Image dimmer = screen.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.66f);
        dimmer.raycastTarget = true;
        return screen;
    }

    private void BuildSettings(Transform screen)
    {
        RectTransform panel = Rect("Panel", screen).GetComponent<RectTransform>();
        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.88f, 0.62f));
        RectTransform body = ImageObject("Body", panel, "card_vertical_purple_tall 6", false);
        Stretch(body);
        body.SetAsFirstSibling();
        RectTransform header = Rect("HeaderGroup", panel).GetComponent<RectTransform>();
        SetRect(header, new Vector2(0.5f, 0.965f), new Vector2(0.72f, 0.25f));
        RectTransform headerBackground = ImageObject("Background", header, "card_header", false);
        Stretch(headerBackground);
        AddLabel("Title", header, "SETTINGS", 72f, new Vector2(0.16f, 0.18f), new Vector2(0.94f, 0.84f));
        RectTransform headerIcon = ImageObject("GearIcon", header, "PictoIcon_Phone", true);
        headerIcon.GetComponent<Image>().sprite = TableController.GetGearIconSprite();
        SetAnchors(headerIcon, new Vector2(0.05f, 0.23f), new Vector2(0.25f, 0.77f));

        Button close = CreateSpriteButton("CloseButton", panel, "exit", "exit");
        SetRect(close.GetComponent<RectTransform>(), new Vector2(0.96f, 0.94f), new Vector2(0.18f, 0.18f));
        close.onClick.AddListener(HideSettings);

        RectTransform options = Rect("Options", panel).GetComponent<RectTransform>();
        SetAnchors(options, new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.73f));
        Button sound = CreateOption(options, "Sound", "PictoIcon_Sound", 0.17f, out soundBackground);
        Button music = CreateOption(options, "Music", "PictoIcon_Music_2", 0.50f, out musicBackground);
        Button haptic = CreateOption(options, "Haptic", "PictoIcon_Phone", 0.83f, out hapticBackground);
        sound.onClick.AddListener(ToggleSound);
        music.onClick.AddListener(ToggleMusic);
        haptic.onClick.AddListener(ToggleHaptic);

        RectTransform restartGroup = Rect("RestartGroup", panel).GetComponent<RectTransform>();
        SetRect(restartGroup, new Vector2(0.5f, 0.20f), new Vector2(0.55f, 0.18f));
        restartButton = CreateSpriteButton("Background", restartGroup, "Button_Green", "Button_Green_Pushed");
        Stretch(restartButton.GetComponent<RectTransform>());
        AddLabel("Label", restartGroup, "RESTART", 58f, Vector2.zero, Vector2.one);
    }

    private Button CreateOption(RectTransform parent, string label, string iconName, float x, out Image background)
    {
        RectTransform group = Rect(label + "Group", parent).GetComponent<RectTransform>();
        SetRect(group, new Vector2(x, 0.58f), new Vector2(0.28f, 0.72f));
        Button button = CreateSpriteButton("Background", group, "button_purple_framed", "button_purple_framed_pushed");
        Stretch(button.GetComponent<RectTransform>());
        background = button.GetComponent<Image>();
        RectTransform icon = ImageObject("Icon", group, iconName, true);
        SetAnchors(icon, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.80f));
        AddLabel("Label", group, label.ToUpperInvariant(), 34f,
            new Vector2(-0.18f, -0.28f), new Vector2(1.18f, 0.06f));
        return button;
    }

    private void BuildTimeUp(Transform screen)
    {
        RectTransform panel = Rect("Panel", screen).GetComponent<RectTransform>();
        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.88f, 0.64f));
        RectTransform body = ImageObject("Body", panel, "card_vertical_purple_tall 6", false);
        Stretch(body);
        body.SetAsFirstSibling();
        RectTransform header = Rect("HeaderGroup", panel).GetComponent<RectTransform>();
        SetRect(header, new Vector2(0.5f, 0.965f), new Vector2(0.72f, 0.25f));
        RectTransform headerBackground = ImageObject("Background", header, "card_header", false);
        Stretch(headerBackground);
        AddLabel("Title", header, "OUT OF MOVES!", 64f, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.84f));
        explanationText = AddLabel("Explanation", panel, "Add extra moves to continue.", 38f,
            new Vector2(0.10f, 0.67f), new Vector2(0.90f, 0.78f));
        RectTransform hourglassGroup = Rect("HourglassGroup", panel).GetComponent<RectTransform>();
        SetRect(hourglassGroup, new Vector2(0.5f, 0.54f), new Vector2(0.28f, 0.25f));
        RectTransform hourglassRing = ImageObject("Ring", hourglassGroup, "button_circle_framed", true);
        Stretch(hourglassRing);
        RectTransform hourglass = ImageObject("Icon", hourglassGroup, "PictoIcon_Hourglass", true);
        SetAnchors(hourglass, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));

        RectTransform watchGroup = Rect("WatchAdGroup", panel).GetComponent<RectTransform>();
        SetRect(watchGroup, new Vector2(0.30f, 0.25f), new Vector2(0.40f, 0.20f));
        watchAdButton = CreateSpriteButton("Background", watchGroup, "Button_V3_Red", "Button_orange_Pushed");
        Stretch(watchAdButton.GetComponent<RectTransform>());
        RectTransform movie = ImageObject("MovieIcon", watchGroup, "PictoIcon_Movie", true);
        SetAnchors(movie, new Vector2(-0.07f, 0.54f), new Vector2(0.25f, 1.18f));
        AddLabel("Label", watchGroup, "WATCH AD", 34f, new Vector2(0.16f, 0.38f), new Vector2(0.96f, 0.84f));
        AddLabel("Detail", watchGroup, "+5 MOVES", 24f, new Vector2(0.16f, 0.10f), new Vector2(0.96f, 0.42f));

        RectTransform goldGroup = Rect("GoldGroup", panel).GetComponent<RectTransform>();
        SetRect(goldGroup, new Vector2(0.72f, 0.25f), new Vector2(0.40f, 0.20f));
        spendGoldButton = CreateSpriteButton("Background", goldGroup, "Button_Green", "Button_Green_Pushed");
        Stretch(spendGoldButton.GetComponent<RectTransform>());
        goldPriceText = AddLabel("Price", goldGroup, "900", 46f,
            new Vector2(0.18f, 0.37f), new Vector2(0.94f, 0.88f));
        AddLabel("Detail", goldGroup, "+5 MOVES", 24f,
            new Vector2(0.18f, 0.09f), new Vector2(0.94f, 0.40f));
    }

    public void ShowSettings()
    {
        ResolveSceneReferences();
        EnsurePopupCanvas();
        if (settingsScreen == null)
        {
            Debug.LogError("SettingsScreen bulunamadi. MagnetPopupUI hiyerarsisini kontrol et.", this);
            return;
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        settingsScreen.SetActive(true);
        settingsScreen.transform.SetAsLastSibling();
        if (settingsGoldText != null)
            settingsGoldText.text = SaveSystem.Current.gold.ToString();
        RefreshSettingsVisuals();
        Canvas.ForceUpdateCanvases();
        Debug.Log("Settings popup acildi.", this);
    }

    public void HideSettings() => settingsScreen.SetActive(false);

    private void ToggleSound()
    {
        PlayerSaveData data = SaveSystem.Current;
        SaveSystem.SetAudioSettings(!data.soundEnabled, data.musicEnabled);
        AudioListener.volume = SaveSystem.Current.soundEnabled ? 1f : 0f;
        RefreshSettingsVisuals();
    }

    private void ToggleMusic()
    {
        PlayerSaveData data = SaveSystem.Current;
        SaveSystem.SetAudioSettings(data.soundEnabled, !data.musicEnabled);
        RefreshSettingsVisuals();
    }

    private void ToggleHaptic()
    {
        HapticFeedback.Enabled = !HapticFeedback.Enabled;
        if (HapticFeedback.Enabled) HapticFeedback.Play(HapticFeedback.Strength.Light);
        RefreshSettingsVisuals();
    }

    private void RefreshSettingsVisuals()
    {
        PlayerSaveData data = SaveSystem.Current;
        SetToggleVisual(soundBackground, data.soundEnabled);
        SetToggleVisual(musicBackground, data.musicEnabled);
        SetToggleVisual(hapticBackground, HapticFeedback.Enabled);
    }

    private static void SetToggleVisual(Image image, bool enabled)
    {
        if (image == null) return;
        image.sprite = Load(enabled ? "button_purple_framed" : "button_purple_framed_dark");
        image.color = enabled ? Color.white : new Color(0.68f, 0.68f, 0.72f, 1f);
    }

    private static Button CreateSpriteButton(string name, Transform parent, string normal, string pressed)
    {
        RectTransform rect = ImageObject(name, parent, normal, false);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SpriteState state = button.spriteState;
        state.pressedSprite = Load(pressed);
        state.selectedSprite = Load(normal);
        button.spriteState = state;
        button.transition = Selectable.Transition.SpriteSwap;
        return button;
    }

    private static RectTransform ImageObject(string name, Transform parent, string spriteName, bool preserveAspect)
    {
        GameObject go = Rect(name, parent);
        Image image = go.AddComponent<Image>();
        image.sprite = Load(spriteName);
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI AddLabel(string name, Transform parent, string value, float size, Vector2 min, Vector2 max)
    {
        GameObject go = Rect(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, size * 0.55f);
        text.fontSizeMax = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.22f;
        text.raycastTarget = false;
        SetAnchors(text.rectTransform, min, max);
        return text;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Sprite Load(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(ArtPath + name);
        if (sprite != null) return sprite;
        Texture2D texture = Resources.Load<Texture2D>(ArtPath + name);
        return texture == null
            ? null
            : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 center, Vector2 normalizedSize)
    {
        rect.anchorMin = center - normalizedSize * 0.5f;
        rect.anchorMax = center + normalizedSize * 0.5f;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
