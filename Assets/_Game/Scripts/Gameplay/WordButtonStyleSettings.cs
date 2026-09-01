using UnityEngine;

[CreateAssetMenu(fileName = "WordButtonStyleSettings", menuName = "Word Game/Word Button Style Settings")]
public class WordButtonStyleSettings : ScriptableObject
{
    [Header("Completed Row")]
    public Color matchedWordAndRowColor = new Color(0.05f, 0.82f, 0.43f, 1f);
    [Tooltip("14 reserved colors: entries 0-6 are the primary row colors; entries 7-13 are the second group colors for those same rows.")]
    public Color[] rowMatchColors =
    {
        new Color(0.05f, 0.82f, 0.43f, 1f),
        new Color(0.20f, 0.65f, 1f, 1f),
        new Color(1f, 0.67f, 0.20f, 1f),
        new Color(0.72f, 0.45f, 0.95f, 1f),
        new Color(1f, 0.42f, 0.55f, 1f),
        new Color(0.20f, 0.80f, 0.75f, 1f),
        new Color(0.95f, 0.82f, 0.24f, 1f),
        new Color(0.95f, 0.65f, 0.78f, 1f),
        new Color(0.65f, 0.82f, 0.30f, 1f),
        new Color(0.52f, 0.60f, 0.92f, 1f),
        new Color(0.87f, 0.48f, 0.30f, 1f),
        new Color(0.52f, 0.85f, 0.93f, 1f),
        new Color(0.85f, 0.38f, 0.78f, 1f),
        new Color(0.70f, 0.66f, 0.56f, 1f)
    };

    [Header("Interaction")]
    public Color backlightColor = new Color(1f, 0.92f, 0.32f, 1f);
    [Min(1f)] public float selectedScale = 1.04f;
    [Min(1f)] public float dragScale = 1.15f;
    [Min(1f)] public float dropTargetScale = 1.08f;
    [Min(0f)] public float dragLift = 10f;
    [Min(0.01f)] public float animationDuration = 0.14f;

    public static WordButtonStyleSettings LoadDefault()
    {
        return Resources.Load<WordButtonStyleSettings>("Data/WordButtonStyleSettings");
    }

    public Color GetRowMatchColor(int rowIndex, bool secondary = false)
    {
        int colorIndex = Mathf.Max(0, rowIndex) + (secondary ? 7 : 0);
        if (rowMatchColors != null && colorIndex < rowMatchColors.Length)
            return rowMatchColors[colorIndex];
        // Do not wrap onto another row's reserved color if an asset is incomplete.
        return Color.HSVToRGB((colorIndex * 0.618034f) % 1f, 0.55f, 0.9f);
    }
}
