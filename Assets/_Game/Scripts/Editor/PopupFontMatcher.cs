#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PopupFontMatcher
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";

    [MenuItem("Tools/Word Game/Match Popup Fonts To Level")]
    public static void ApplyFromMenu()
    {
        ApplyIfGameSceneIsOpen();
    }

    private static void ApplyIfGameSceneIsOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != GameScenePath) return;

        TMP_Text levelLabel = FindVisibleLevelLabel(scene);
        MagnetPopupUI popup = Object.FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        if (levelLabel == null || popup == null) return;

        TMP_Text[] popupTexts = popup.GetComponentsInChildren<TMP_Text>(true);
        SerializedObject sourceSerialized = new SerializedObject(levelLabel);
        Object sourceFontAsset = sourceSerialized.FindProperty("m_fontAsset").objectReferenceValue;
        Object sourceSharedMaterial = sourceSerialized.FindProperty("m_sharedMaterial").objectReferenceValue;
        Object sourceFontMaterial = sourceSerialized.FindProperty("m_fontMaterial").objectReferenceValue;
        Shadow sourceShadow = levelLabel.GetComponent<Shadow>();
        foreach (TMP_Text target in popupTexts)
        {
            Color originalColor = target.color;
            float originalSize = target.fontSize;
            bool originalAutoSize = target.enableAutoSizing;
            float originalMinSize = target.fontSizeMin;
            float originalMaxSize = target.fontSizeMax;

            Undo.RecordObject(target, "Match Popup Font To Level");
            SerializedObject targetSerialized = new SerializedObject(target);
            targetSerialized.FindProperty("m_fontAsset").objectReferenceValue = sourceFontAsset;
            targetSerialized.FindProperty("m_sharedMaterial").objectReferenceValue = sourceSharedMaterial;
            targetSerialized.FindProperty("m_fontMaterial").objectReferenceValue = sourceFontMaterial;
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();

            target.font = levelLabel.font;
            target.fontStyle = levelLabel.fontStyle;
            target.fontWeight = levelLabel.fontWeight;
            target.characterSpacing = levelLabel.characterSpacing;
            target.wordSpacing = levelLabel.wordSpacing;
            target.lineSpacing = levelLabel.lineSpacing;
            target.faceColor = levelLabel.faceColor;
            target.outlineColor = levelLabel.outlineColor;
            target.outlineWidth = Mathf.Max(0.189f, levelLabel.outlineWidth);

            if (sourceShadow != null)
            {
                Shadow targetShadow = target.GetComponent<Shadow>();
                if (targetShadow == null) targetShadow = Undo.AddComponent<Shadow>(target.gameObject);
                targetShadow.effectColor = sourceShadow.effectColor;
                targetShadow.effectDistance = sourceShadow.effectDistance;
                targetShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
                EditorUtility.SetDirty(targetShadow);
            }

            target.color = originalColor;
            target.fontSize = originalSize;
            target.enableAutoSizing = originalAutoSize;
            target.fontSizeMin = originalMinSize;
            target.fontSizeMax = originalMaxSize;
            target.SetMaterialDirty();
            target.SetVerticesDirty();
            EditorUtility.SetDirty(target);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Matched {popupTexts.Length} popup texts to the LevelLabel font.");
    }

    private static TMP_Text FindVisibleLevelLabel(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                if (label.name != "LevelLabel") continue;
                Transform parent = label.transform.parent;
                while (parent != null)
                {
                    if (parent.name == "FridgeTopHud") return label;
                    parent = parent.parent;
                }
            }
        }

        return FindByName<TMP_Text>(scene, "LevelLabel");
    }

    private static T FindByName<T>(Scene scene, string objectName) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            foreach (T component in components)
                if (component.name == objectName) return component;
        }
        return null;
    }
}
#endif
