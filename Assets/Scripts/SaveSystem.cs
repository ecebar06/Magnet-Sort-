using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string FileName = "player_save.json";
    private const string BackupFileName = "player_save.backup.json";
    private const string TempFileName = "player_save.tmp";

    private static PlayerSaveData current;

    public static PlayerSaveData Current => current ?? Load();
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
    private static string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);

    public static PlayerSaveData Load()
    {
        current = TryRead(SavePath) ?? TryRead(BackupPath) ?? CreateDefault();
        Sanitize(current);
        return current;
    }

    public static bool Save()
    {
        return Save(Current);
    }

    public static bool Save(PlayerSaveData data)
    {
        if (data == null) return false;
        Sanitize(data);

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(SavePath))
                File.Copy(SavePath, BackupPath, true);

            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(TempPath, SavePath);
            current = data;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save player data: {exception.Message}");
            return false;
        }
    }

    public static void CompleteLevel(int completedLevelIndex, int nextLevelIndex)
    {
        PlayerSaveData data = Current;
        data.highestCompletedLevelIndex = Mathf.Max(data.highestCompletedLevelIndex, completedLevelIndex);
        data.currentLevelIndex = Mathf.Max(0, nextLevelIndex);
        Save(data);
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
        current = CreateDefault();
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

    private static PlayerSaveData CreateDefault()
    {
        return new PlayerSaveData();
    }

    private static void Sanitize(PlayerSaveData data)
    {
        data.version = Mathf.Max(1, data.version);
        data.currentLevelIndex = Mathf.Max(0, data.currentLevelIndex);
        data.highestCompletedLevelIndex = Mathf.Max(-1, data.highestCompletedLevelIndex);
        data.hintCount = Mathf.Max(0, data.hintCount);
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
