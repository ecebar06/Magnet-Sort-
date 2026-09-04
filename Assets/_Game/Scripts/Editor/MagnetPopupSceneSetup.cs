#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MagnetPopupSceneSetup
{
    private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string PopupArtFolder = "Assets/_Game/Resources/Art/UI/Popups";

    [MenuItem("Tools/Word Game/Build Magnet Popups")]
    public static void BuildFromMenu()
    {
        BuildAndSave(false);
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildAndSave(true);
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildAndSave(bool commandLine)
    {
        ImportPopupSprites();
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Transform gameHud = FindInScene(scene, "GameHUD");
        if (gameHud == null)
            throw new MissingReferenceException("GameHUD was not found in Game.unity.");

        CreateAndSave(scene, gameHud, commandLine);
    }

    private static void CreateAndSave(Scene scene, Transform gameHud, bool commandLine)
    {

        Transform existing = gameHud.Find("MagnetPopupUI");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        MagnetPopupUI popup = MagnetPopupUI.CreateAuthored((RectTransform)gameHud);
        popup.transform.SetAsLastSibling();
        EditorUtility.SetDirty(popup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Magnet popups were authored and saved in Game.unity.");

        if (!commandLine)
        {
            Selection.activeGameObject = popup.gameObject;
            EditorGUIUtility.PingObject(popup.gameObject);
        }
    }

    private static void ImportPopupSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PopupArtFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency || importer.maxTextureSize < 2048;
            if (!changed) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
                if (candidate.name == objectName) return candidate;
        }
        return null;
    }
}
#endif
