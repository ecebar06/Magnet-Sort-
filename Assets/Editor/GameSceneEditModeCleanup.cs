#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameSceneEditModeCleanup
{
    private static Sprite pastelPreviewSprite;

    static GameSceneEditModeCleanup()
    {
        EditorApplication.delayCall += CleanLoadedGameScene;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += CleanLoadedGameScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += CleanLoadedGameScene;
    }

    private static void CleanLoadedGameScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        TableController[] controllers = Object.FindObjectsByType<TableController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (TableController controller in controllers)
        {
            if (controller == null || controller.wordContainer == null) continue;

            Transform board = controller.wordContainer;
            for (int i = board.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(board.GetChild(i).gameObject);

            Canvas canvas = board.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                DestroyChild(canvas.transform, "GameHUD");
                DestroyChild(canvas.transform, "ResultOverlay");

                Transform backgroundTransform = canvas.transform.Find("background");
                Image background = backgroundTransform == null ? null : backgroundTransform.GetComponent<Image>();
                if (background != null)
                {
                    Sprite pastel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/PastelBackground.png");
                    if (pastel == null)
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/PastelBackground.png");
                        if (texture != null && pastelPreviewSprite == null)
                        {
                            pastelPreviewSprite = Sprite.Create(texture,
                                new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                            pastelPreviewSprite.hideFlags = HideFlags.HideAndDontSave;
                        }
                        pastel = pastelPreviewSprite;
                    }
                    if (pastel != null) background.sprite = pastel;
                    background.color = Color.white;
                    background.enabled = true;
                    background.gameObject.SetActive(true);
                    background.transform.SetAsFirstSibling();
                }
            }

            // In edit mode the board is intentionally hidden. TableController.Start
            // enables it again before creating the gameplay UI.
            board.gameObject.SetActive(false);
        }

        SceneView.RepaintAll();
    }

    private static void DestroyChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }
}
#endif
