using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Small cross-platform haptic gateway. Android uses the current Unity view,
/// so it needs no third-party package or vibration permission.
/// </summary>
public static class HapticFeedback
{
    public enum Strength
    {
        Light,
        Medium,
        Success
    }

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt("HapticsEnabled", 1) == 1;
        set
        {
            PlayerPrefs.SetInt("HapticsEnabled", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Play(Strength strength)
    {
        if (!Enabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Android HapticFeedbackConstants: KEYBOARD_TAP=3,
            // VIRTUAL_KEY=1 and LONG_PRESS=0 are supported on old devices too.
            int constant = strength == Strength.Light ? 3 :
                strength == Strength.Medium ? 1 : 0;
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
            using AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView");
            decorView.Call<bool>("performHapticFeedback", constant);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Android haptic could not play: {exception.Message}");
        }
#elif UNITY_EDITOR
        Debug.Log($"Haptic preview: {strength}");
#else
        if (strength != Strength.Light) Handheld.Vibrate();
#endif
    }
}

/// <summary>Adds light haptics to ordinary UI buttons without editing scenes.</summary>
public sealed class HapticButtonInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject installer = new GameObject(nameof(HapticButtonInstaller));
        DontDestroyOnLoad(installer);
        installer.AddComponent<HapticButtonInstaller>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToCurrentScene();
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToCurrentScene();

    private static void AddToCurrentScene()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            // WordButton already triggers at pointer-down, which feels more immediate.
            if (button.GetComponent<WordButton>() != null ||
                button.GetComponent<HapticButtonRelay>() != null) continue;
            button.gameObject.AddComponent<HapticButtonRelay>();
        }
    }
}

public sealed class HapticButtonRelay : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Play);
    }

    private static void Play() => HapticFeedback.Play(HapticFeedback.Strength.Light);
}
