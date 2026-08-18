using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class SaveSystem : MonoBehaviour
{
    private const string FileName = "player_save.json";
    private const string BackupFileName = "player_save.backup.json";
    private const string TempFileName = "player_save.tmp";
    private const int StartingGold = 500;
    private const int LevelCompletionGold = 40;
    private static SaveSystem instance;

    [Header("Player Save Data")]
    [Tooltip("Current player values. These can be inspected and changed while the game is running.")]
    [SerializeField] private PlayerSaveData currentData = new PlayerSaveData();

    [Header("Behaviour")]
    [Tooltip("Load player_save.json when this component wakes up.")]
    [SerializeField] private bool loadFromDiskOnAwake = true;

    public static SaveSystem Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<SaveSystem>();
            if (instance != null) return instance;

            GameObject saveObject = new GameObject(nameof(SaveSystem));
            instance = saveObject.AddComponent<SaveSystem>();
            return instance;
        }
    }

    public static PlayerSaveData Current => Instance.GetCurrentData();
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
    private static string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (loadFromDiskOnAwake) LoadInternal();
        else Sanitize(currentData);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    public static PlayerSaveData Load() => Instance.LoadInternal();
    public static bool Save() => Instance.SaveInternal(Instance.currentData);
    public static bool Save(PlayerSaveData data) => Instance.SaveInternal(data);

    public static void CompleteLevel(int completedLevelIndex, int nextLevelIndex)
    {
        PlayerSaveData data = Current;
        data.highestCompletedLevelIndex = Mathf.Max(data.highestCompletedLevelIndex, completedLevelIndex);
        data.currentLevelIndex = Mathf.Max(0, nextLevelIndex);
        data.gold += LevelCompletionGold;
        Save(data);
    }

    public static void AddGold(int amount)
    {
        if (amount <= 0) return;
        Current.gold += amount;
        Save();
    }

    public static bool TrySpendGold(int amount)
    {
        if (amount < 0 || Current.gold < amount) return false;
        Current.gold -= amount;
        Save();
        return true;
    }

    public static void SetHintCount(int count)
    {
        Current.hintCount = Mathf.Max(0, count);
        Save();
    }

    public static void SetAudioSettings(bool soundEnabled, bool musicEnabled)
    {
        Current.soundEnabled = soundEnabled;
        Current.musicEnabled = musicEnabled;
        Save();
    }

    public static void DeleteSaveData()
    {
        DeleteIfExists(SavePath);
        DeleteIfExists(BackupPath);
        DeleteIfExists(TempPath);
        Instance.currentData = CreateDefault();
    }

    private PlayerSaveData GetCurrentData()
    {
        if (currentData == null) currentData = CreateDefault();
        Sanitize(currentData);
        return currentData;
    }

    private PlayerSaveData LoadInternal()
    {
        currentData = TryRead(SavePath) ?? TryRead(BackupPath) ?? CreateDefault();
        Sanitize(currentData);
        return currentData;
    }

    private bool SaveInternal(PlayerSaveData data)
    {
        if (data == null) return false;
        Sanitize(data);

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempPath, SavePath);
            currentData = data;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save player data: {exception.Message}", this);
            return false;
        }
    }

    private static PlayerSaveData TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<PlayerSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read save file '{path}': {exception.Message}");
            return null;
        }
    }

    private static PlayerSaveData CreateDefault() => new PlayerSaveData();

    private static void Sanitize(PlayerSaveData data)
    {
        if (data.version < 2)
        {
            data.gold = StartingGold;
            data.version = 2;
        }

        data.currentLevelIndex = Mathf.Max(0, data.currentLevelIndex);
        data.highestCompletedLevelIndex = Mathf.Max(-1, data.highestCompletedLevelIndex);
        data.hintCount = Mathf.Max(0, data.hintCount);
        data.gold = Mathf.Max(0, data.gold);
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not delete save file '{path}': {exception.Message}");
        }
    }
}
