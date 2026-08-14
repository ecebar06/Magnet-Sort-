#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SplashSceneSetup
{
    private const string SplashPath = "Assets/Splash.unity";
    private const string GamePath = "Assets/Game.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Word Game/Create Splash Scene")]
    public static void CreateSplashScene()
    {
        Scene splash = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        splash.name = "Splash";
        GameObject controller = new GameObject("SplashController");
        SceneManager.MoveGameObjectToScene(controller, splash);
        controller.AddComponent<SplashController>();
        EditorSceneManager.SaveScene(splash, SplashPath);
        EditorSceneManager.CloseScene(splash, true);
        UpdateBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("Splash scene created. It is now the first scene in Build Settings.");
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SplashPath) == null)
            CreateSplashScene();
        else
            UpdateBuildScenes();
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
