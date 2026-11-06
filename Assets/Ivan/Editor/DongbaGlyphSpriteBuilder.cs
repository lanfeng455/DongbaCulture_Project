using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Scans every image in Art/Dongba/Raw and turns it into a tintable glyph sprite in
// Art/Dongba (white background cut to alpha, cropped to ink bounds, custom pivot).
//
// There is no fixed asset list: drop a new .png/.jpg into Raw and it is picked up on
// the next build, so new Dongba art needs zero code changes. Full-bleed background
// plates that are not ink art are listed in s_skippedNames so they are left alone.
public static class DongbaGlyphSpriteBuilder
{
    private const string k_rawFolderPath = "Assets/Ivan/Art/Dongba/Raw";
    private const string k_dongbaArtFolderPath = "Assets/Ivan/Art/Dongba";

    // Extensions the glyph extractor understands (see DongbaAssetBuilder.FindRawImagePath)
    private static readonly string[] s_extensions = { ".png", ".jpg" };

    // Background plates share the folder but must not be white-cut and cropped
    private static readonly HashSet<string> s_skippedNames = new HashSet<string> { "ground_watercolor" };

    // Fallback for any newly added glyph: nothing to configure, and the in-world size
    // is still controlled by each prefab's transform scale
    private static readonly GlyphImport s_defaultImport = new GlyphImport(1.5f, new Vector2(0.5f, 0f));

    // Glyphs that were already tuned by hand keep their exact size and anchor, so
    // re-running the build never silently resizes art that is already placed in scenes
    private static readonly Dictionary<string, GlyphImport> s_tunedImports = new Dictionary<string, GlyphImport>
    {
        { "dongba_ren", new GlyphImport(1.7f, new Vector2(0.5f, 0f)) },
        { "dongba_mu", new GlyphImport(2.6f, new Vector2(0.5f, 0f)) },
        { "dongba_ri", new GlyphImport(2f, new Vector2(0.5f, 0f)) },
        { "dongba_fu", new GlyphImport(0.6f, new Vector2(0f, 0.5f)) },
        { "dongba_gun", new GlyphImport(0.8f, new Vector2(0f, 0f)) },
    };

    [MenuItem("Tools/Dongba/Build Glyph Sprites (No Prefabs)")]
    public static void BuildGlyphSprites()
    {
        // Pick up raw files that were copied in while the editor was closed
        AssetDatabase.Refresh();

        if (!Directory.Exists(k_rawFolderPath))
        {
            Debug.LogWarning($"[DongbaGlyphSpriteBuilder] Raw folder not found: {k_rawFolderPath}");
            return;
        }

        int builtCount = 0;
        int skippedCount = 0;
        foreach (string rawPath in Directory.GetFiles(k_rawFolderPath))
        {
            if (!IsSupportedImage(rawPath))
            {
                continue;
            }

            string glyphName = Path.GetFileNameWithoutExtension(rawPath);
            if (s_skippedNames.Contains(glyphName))
            {
                skippedCount++;
                continue;
            }

            GlyphImport import = s_tunedImports.TryGetValue(glyphName, out GlyphImport tuned) ? tuned : s_defaultImport;
            BuildSprite(glyphName, import);
            builtCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[DongbaGlyphSpriteBuilder] Built {builtCount} glyph sprite(s) into {k_dongbaArtFolderPath} ({skippedCount} skipped).");
    }

    private static bool IsSupportedImage(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        foreach (string supported in s_extensions)
        {
            if (extension == supported)
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildSprite(string rawName, GlyphImport import)
    {
        string spriteAssetPath = $"{k_dongbaArtFolderPath}/{rawName}.png";
        Texture2D cropped = DongbaAssetBuilder.ExtractGlyphPixels(rawName, out _, out int contentHeight);

        File.WriteAllBytes(Path.GetFullPath(spriteAssetPath), cropped.EncodeToPNG());
        Object.DestroyImmediate(cropped);

        AssetDatabase.ImportAsset(spriteAssetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(spriteAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Derive ppu from the cropped height so raw image resolution never changes world size
        importer.spritePixelsPerUnit = contentHeight / import.HeightMeters;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = import.Pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    // Per-glyph import size and pivot; a glyph with no entry uses s_defaultImport
    private readonly struct GlyphImport
    {
        public readonly float HeightMeters;
        public readonly Vector2 Pivot;

        public GlyphImport(float heightMeters, Vector2 pivot)
        {
            HeightMeters = heightMeters;
            Pivot = pivot;
        }
    }
}
