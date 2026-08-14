using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashController : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float displayDuration = 4f;
    [SerializeField] private string nextScene = "Game";

    private CanvasGroup content;
    private RectTransform logoCard;
    private RectTransform[] wordTiles;
    private Text loadingText;

    private void Awake()
    {
        BuildSplashUI();
    }

    private IEnumerator Start()
    {
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"Splash could not load scene '{nextScene}'. Add it to Build Settings.");
            yield break;
        }

        // Start loading immediately, but keep the Splash visible until both the
        // minimum display time and the background load are complete.
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextScene);
        loadOperation.allowSceneActivation = false;

        float fadeIn = 0.55f;
        float fadeOut = 0.35f;
        float holdUntil = Mathf.Max(fadeIn, displayDuration - fadeOut);
        float elapsed = 0f;

        while (elapsed < holdUntil || loadOperation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            content.alpha = elapsed < fadeIn ? Smooth(elapsed / fadeIn) : 1f;
            UpdateLoadingText(loadOperation.progress);
            AnimateContent(elapsed);
            yield return null;
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOut)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            content.alpha = 1f - Smooth(fadeElapsed / fadeOut);
            UpdateLoadingText(loadOperation.progress);
            AnimateContent(elapsed);
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone) yield return null;
    }

    private void AnimateContent(float elapsed)
    {
        float entrance = Smooth(Mathf.Clamp01(elapsed / 0.7f));
        logoCard.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, entrance);
        for (int i = 0; i < wordTiles.Length; i++)
        {
            float wave = Mathf.Sin(elapsed * 3.2f + i * 0.8f) * 5f;
            wordTiles[i].anchoredPosition = new Vector2(wordTiles[i].anchoredPosition.x, wave);
        }
    }

    private void UpdateLoadingText(float rawProgress)
    {
        if (loadingText == null) return;
        int percent = Mathf.RoundToInt(Mathf.Clamp01(rawProgress / 0.9f) * 100f);
        loadingText.text = $"LOADING... {percent}%";
    }

    private void BuildSplashUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("SplashCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = CreateUI("Pastel Background", canvas.transform);
        RawImage background = backgroundObject.AddComponent<RawImage>();
        background.texture = Resources.Load<Texture2D>("PastelBackground");
        background.color = background.texture == null ? new Color(0.67f, 0.55f, 1f) : Color.white;
        Stretch(background.rectTransform);

        Image tint = CreateImage("Soft Tint", canvas.transform, new Color(0.48f, 0.29f, 0.78f, 0.10f));
        Stretch(tint.rectTransform);

        GameObject contentObject = CreateUI("Animated Content", canvas.transform);
        content = contentObject.AddComponent<CanvasGroup>();
        content.alpha = 0f;
        Stretch(contentObject.GetComponent<RectTransform>());

        Image card = CreateImage("Logo Card", content.transform, new Color(0.34f, 0.20f, 0.66f, 0.96f));
        logoCard = card.rectTransform;
        logoCard.anchorMin = logoCard.anchorMax = new Vector2(0.5f, 0.56f);
        logoCard.sizeDelta = new Vector2(730, 520);
        logoCard.anchoredPosition = Vector2.zero;

        Text title = CreateText("Title", card.transform, "WORD GAME", font, 108, FontStyle.Bold, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        title.rectTransform.anchorMin = new Vector2(0, 0.28f);
        title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.offsetMin = new Vector2(20, 0);
        title.rectTransform.offsetMax = new Vector2(-20, -20);

        Text studio = CreateText("Studio", card.transform, "MOLA GAMES", font, 31, FontStyle.Bold, new Color(1f, 0.83f, 0.61f));
        studio.alignment = TextAnchor.MiddleCenter;
        studio.rectTransform.anchorMin = new Vector2(0, 0.08f);
        studio.rectTransform.anchorMax = new Vector2(1, 0.31f);
        studio.rectTransform.offsetMin = new Vector2(20, 0);
        studio.rectTransform.offsetMax = new Vector2(-20, 0);

        wordTiles = new RectTransform[0];

        loadingText = CreateText("Loading", content.transform, "LOADING... 0%", font, 25, FontStyle.Bold, new Color(0.29f, 0.18f, 0.48f, 0.78f));
        loadingText.alignment = TextAnchor.MiddleCenter;
        loadingText.rectTransform.anchorMin = loadingText.rectTransform.anchorMax = new Vector2(0.5f, 0.13f);
        loadingText.rectTransform.sizeDelta = new Vector2(400, 70);
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject result = CreateUI(name, parent);
        Image image = result.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, Font font, int size, FontStyle style, Color color)
    {
        GameObject result = CreateUI(name, parent);
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
}
