#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the fridge artwork crisp and loadable through Resources.Load at runtime.
/// </summary>
public sealed class FridgeArtImporter : AssetPostprocessor
{
    private const string ArtRoot = "Assets/_Game/Resources/Art/Fridge/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRoot, System.StringComparison.OrdinalIgnoreCase)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
    }
}
#endif
