using System;

[Serializable]
public class PlayerSaveData
{
    public int version = 1;
    public int currentLevelIndex = 0;
    public int highestCompletedLevelIndex = -1;
    public int hintCount = 3;
    public bool soundEnabled = true;
    public bool musicEnabled = true;
}
