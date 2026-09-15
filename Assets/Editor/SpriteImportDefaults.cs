using UnityEditor;

/// <summary>
/// Imports every new PNG or JPG as a Sprite (2D and UI), so dropping UI art into
/// the project needs no trip to the Inspector.
///
/// Only touches a file the first time it is imported: once it has import
/// settings, anything changed by hand in the Inspector is left alone, and so is
/// every image that was already in the project.
/// </summary>
public class SpriteImportDefaults : AssetPostprocessor
{
    // Textures used on 3D materials rather than as sprites - the table and
    // paddle atlas lives here. Add a folder to keep its new images on Default.
    private static readonly string[] ExcludedFolders =
    {
        "Assets/Art/Textures/",
        "Assets/TextMesh Pro/",
        "Assets/Plugins/"
    };

    private void OnPreprocessTexture()
    {
        // Settings already exist: a reimport, or a file someone configured.
        if (!assetImporter.importSettingsMissing) return;
        if (!IsPngOrJpg(assetPath) || IsExcluded(assetPath)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;          // UI is drawn at screen size
    }

    private static bool IsPngOrJpg(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg");
    }

    private static bool IsExcluded(string path)
    {
        foreach (var folder in ExcludedFolders)
            if (path.StartsWith(folder)) return true;

        return false;
    }
}
