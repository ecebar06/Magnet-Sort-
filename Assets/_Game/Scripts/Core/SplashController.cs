using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashController : MonoBehaviour
{
    [Header("Loading")]
    [SerializeField, Min(0.5f)] private float displayDuration = 4f;
    [SerializeField] private string nextScene = "Game";

    [Header("Scene References")]
    [SerializeField] private CanvasGroup content;
    [SerializeField] private RectTransform logoCard;
    [SerializeField] private RectTransform[] wordTiles = new RectTransform[0];
    [SerializeField] private Text loadingText;
    [SerializeField] private Image loadingFill;
    [SerializeField] private RectTransform loadingKnob;

    private IEnumerator Start()
    {
        if (content == null || logoCard == null || loadingText == null || loadingFill == null)
        {
            Debug.LogError("Splash scene references are missing. Run Tools > Word Game > Setup Splash Scene Assets.", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"Splash could not load scene '{nextScene}'. Add it to Build Settings.", this);
            yield break;
        }

        content.alpha = 0f;
        loadingFill.fillAmount = 0f;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextScene);
        loadOperation.allowSceneActivation = false;

        const float fadeIn = 0.55f;
        const float fadeOut = 0.35f;
        float holdUntil = Mathf.Max(fadeIn, displayDuration - fadeOut);
        float elapsed = 0f;

        while (elapsed < holdUntil || loadOperation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            content.alpha = elapsed < fadeIn ? Smooth(elapsed / fadeIn) : 1f;
            UpdateLoading(loadOperation.progress);
            AnimateContent(elapsed);
            yield return null;
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOut)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            content.alpha = 1f - Smooth(fadeElapsed / fadeOut);
            UpdateLoading(loadOperation.progress);
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
            if (wordTiles[i] == null) continue;
            float wave = Mathf.Sin(elapsed * 3.2f + i * 0.8f) * 5f;
            wordTiles[i].anchoredPosition = new Vector2(wordTiles[i].anchoredPosition.x, wave);
        }
    }

    private void UpdateLoading(float rawProgress)
    {
        float normalizedProgress = Mathf.Clamp01(rawProgress / 0.9f);
        loadingFill.fillAmount = normalizedProgress;
        if (loadingKnob != null)
        {
            RectTransform fillRect = loadingFill.rectTransform;
            Vector3 edge = new Vector3(Mathf.Lerp(fillRect.rect.xMin, fillRect.rect.xMax,
                normalizedProgress), fillRect.rect.center.y, 0f);
            loadingKnob.position = fillRect.TransformPoint(edge);
        }
        loadingText.text = $"LOADING... {Mathf.RoundToInt(normalizedProgress * 100f)}%";
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
