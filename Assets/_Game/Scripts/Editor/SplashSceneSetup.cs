#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SplashSceneSetup
{
    private const string SplashPath = "Assets/_Game/Scenes/Splash.unity";
    private const string GamePath = "Assets/_Game/Scenes/Game.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += CreateOrUpgradeSplashScene;
    }

    [MenuItem("Tools/Word Game/Setup Splash Scene Assets")]
    public static void SetupSplashSceneAssets()
    {
        CreateOrUpgradeSplashScene();
        Debug.Log("Splash scene assets are stored in Splash.unity and ready to edit.");
    }

    [MenuItem("Tools/Word Game/Create Splash Scene")]
    public static void CreateSplashScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SplashPath) != null &&
            !EditorUtility.DisplayDialog("Recreate Splash Scene",
                "This will replace the existing Splash scene. Continue?", "Recreate", "Cancel"))
            return;

        Scene splash = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        splash.name = "Splash";
        BuildSceneAssets(splash, null);
        EditorSceneManager.SaveScene(splash, SplashPath);
        EditorSceneManager.CloseScene(splash, true);
        UpdateBuildScenes();
        AssetDatabase.SaveAssets();
    }

    private static void CreateOrUpgradeSplashScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SplashPath) == null)
        {
            CreateSplashScene();
            return;
        }

        Scene splash = SceneManager.GetSceneByPath(SplashPath);
        bool openedForSetup = !splash.IsValid() || !splash.isLoaded;
        if (openedForSetup) splash = EditorSceneManager.OpenScene(SplashPath, OpenSceneMode.Additive);

        SplashController controller = splash.GetRootGameObjects()
            .Select(root => root.GetComponent<SplashController>())
            .FirstOrDefault(found => found != null);
        bool hasSceneAssets = splash.GetRootGameObjects().Any(root => root.name == "SplashCanvas");

        if (!hasSceneAssets)
        {
            BuildSceneAssets(splash, controller);
            EditorSceneManager.SaveScene(splash);
        }

        if (openedForSetup) EditorSceneManager.CloseScene(splash, true);
        UpdateBuildScenes();
        AssetDatabase.SaveAssets();
    }

    private static void BuildSceneAssets(Scene splash, SplashController existingController)
    {
        SplashController controller = existingController;
        if (controller == null)
        {
            GameObject controllerObject = new GameObject("SplashController");
            SceneManager.MoveGameObjectToScene(controllerObject, splash);
            controller = controllerObject.AddComponent<SplashController>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("SplashCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, splash);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = CreateUI("Pastel Background", canvas.transform);
        RawImage background = backgroundObject.AddComponent<RawImage>();
        Texture2D splashArt = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/_Game/Textures/Branding/WordSort-Splash-v1.png");
        background.texture = splashArt != null ? splashArt : Resources.Load<Texture2D>("Textures/PastelBackground");
        background.color = background.texture == null ? new Color(0.67f, 0.55f, 1f) : Color.white;
        Stretch(background.rectTransform);
        if (splashArt != null)
        {
            AspectRatioFitter fitter = backgroundObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)splashArt.width / splashArt.height;
        }

        Image tint = CreateImage("Soft Tint", canvas.transform, new Color(0.48f, 0.29f, 0.78f, 0.10f));
        Stretch(tint.rectTransform);
        if (splashArt != null) tint.gameObject.SetActive(false);

        GameObject contentObject = CreateUI("Animated Content", canvas.transform);
        CanvasGroup content = contentObject.AddComponent<CanvasGroup>();
        content.alpha = 1f;
        Stretch(contentObject.GetComponent<RectTransform>());

        Image card = CreateImage("Logo Card", content.transform, new Color(0.34f, 0.20f, 0.66f, 0.96f));
        RectTransform logoCard = card.rectTransform;
        logoCard.anchorMin = logoCard.anchorMax = new Vector2(0.5f, 0.56f);
        logoCard.sizeDelta = new Vector2(730f, 520f);
        logoCard.anchoredPosition = Vector2.zero;
        if (splashArt != null) card.gameObject.SetActive(false);

        Text title = CreateText("Title", card.transform, "WORD GAME", font, 108, FontStyle.Bold, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        title.rectTransform.anchorMin = new Vector2(0f, 0.28f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(20f, 0f);
        title.rectTransform.offsetMax = new Vector2(-20f, -20f);

        Text studio = CreateText("Studio", card.transform, "MOLA GAMES", font, 31, FontStyle.Bold,
            new Color(1f, 0.83f, 0.61f));
        studio.alignment = TextAnchor.MiddleCenter;
        studio.rectTransform.anchorMin = new Vector2(0f, 0.08f);
        studio.rectTransform.anchorMax = new Vector2(1f, 0.31f);
        studio.rectTransform.offsetMin = new Vector2(20f, 0f);
        studio.rectTransform.offsetMax = new Vector2(-20f, 0f);

        Image loadingTrack = CreateImage("Loading Bar", content.transform, new Color(1f, 1f, 1f, 0.36f));
        RectTransform trackRect = loadingTrack.rectTransform;
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.18f);
        trackRect.sizeDelta = new Vector2(520f, 24f);

        Image loadingFill = CreateImage("Fill", loadingTrack.transform, new Color(0.42f, 0.28f, 0.76f, 1f));
        Stretch(loadingFill.rectTransform);
        loadingFill.type = Image.Type.Filled;
        loadingFill.fillMethod = Image.FillMethod.Horizontal;
        loadingFill.fillOrigin = 0;
        loadingFill.fillAmount = 0f;

        Text loadingText = CreateText("Loading", content.transform, "LOADING... 0%", font, 25, FontStyle.Bold,
            new Color(0.29f, 0.18f, 0.48f, 0.78f));
        loadingText.alignment = TextAnchor.MiddleCenter;
        loadingText.rectTransform.anchorMin = loadingText.rectTransform.anchorMax = new Vector2(0.5f, 0.13f);
        loadingText.rectTransform.sizeDelta = new Vector2(420f, 70f);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("content").objectReferenceValue = content;
        serializedController.FindProperty("logoCard").objectReferenceValue = logoCard;
        serializedController.FindProperty("wordTiles").arraySize = 0;
        serializedController.FindProperty("loadingText").objectReferenceValue = loadingText;
        serializedController.FindProperty("loadingFill").objectReferenceValue = loadingFill;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(splash);
    }

    private static GameObject CreateUI(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject result = CreateUI(objectName, parent);
        Image image = result.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string objectName, Transform parent, string value, Font font, int size,
        FontStyle style, Color color)
    {
        GameObject result = CreateUI(objectName, parent);
        Text text = result.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = size;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void UpdateBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != SplashPath && scene.path != GamePath)
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(GamePath, true));
        scenes.Insert(0, new EditorBuildSettingsScene(SplashPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SplashPath);
    }
}
#endif
