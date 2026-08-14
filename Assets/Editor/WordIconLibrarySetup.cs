#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WordIconLibrarySetup
{
    private const string IconAssetPath = "Assets/Resources/TestAppleIcon.asset";
    private const string LibraryAssetPath = "Assets/Resources/MainWordIconLibrary.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += EnsureTestLibrary;
    }

    [MenuItem("Tools/Word Game/Create Test Icon Library")]
    public static void EnsureTestLibrary()
    {
        Sprite appleSprite = LoadOrCreateAppleSprite();
        WordIconLibrary library = AssetDatabase.LoadAssetAtPath<WordIconLibrary>(LibraryAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<WordIconLibrary>();
            AssetDatabase.CreateAsset(library, LibraryAssetPath);
        }

        library.SetIcon("fruits__apple", appleSprite);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
    }

    private static Sprite LoadOrCreateAppleSprite()
    {
        Sprite existing = AssetDatabase.LoadAllAssetsAtPath(IconAssetPath).OfType<Sprite>().FirstOrDefault();
        if (existing != null) return existing;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "TestAppleTexture";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 leftCenter = new Vector2(50f, 60f);
        Vector2 rightCenter = new Vector2(78f, 60f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                bool apple = Vector2.Distance(point, leftCenter) < 37f || Vector2.Distance(point, rightCenter) < 37f;
                bool lowerCut = y < 22 && (x < 45 || x > 83);
                bool stem = x >= 60 && x <= 68 && y >= 92 && y <= 116;
                bool leaf = Mathf.Pow((x - 80f) / 20f, 2f) + Mathf.Pow((y - 105f) / 10f, 2f) <= 1f;

                Color color = Color.clear;
                if (apple && !lowerCut) color = new Color(1f, 0.30f, 0.34f, 1f);
                if (stem) color = new Color(0.36f, 0.20f, 0.12f, 1f);
                if (leaf) color = new Color(0.30f, 0.76f, 0.42f, 1f);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, IconAssetPath);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "TestAppleIcon";
        AssetDatabase.AddObjectToAsset(sprite, texture);
        AssetDatabase.ImportAsset(IconAssetPath);
        AssetDatabase.SaveAssets();
        return sprite;
    }
}
#endif
