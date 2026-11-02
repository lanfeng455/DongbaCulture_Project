using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DongbaPrototype {
public static class EcologyVisualBuilder {
    [MenuItem("Dongba Prototype/Open Current Forest")]
    public static void OpenCurrentForest() {
        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/DongbaPrototype/ObliqueForest.unity");
    }
    [MenuItem("Dongba Prototype/Build Current Forest (Windows x64)")]
    public static void Build() {
        const string destination = "Builds/Windows/ForestPrototype";
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory("Verification/current-build");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string file in Directory.GetFiles("Assets/DongbaPrototype/Resources/EcologyTreeV2", "*.png")) {
            string assetPath = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new System.Exception("Unable to import derived tree layer: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f,0);
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100;
            importer.spritePivot = new Vector2(.5f, 0);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/DongbaPrototype/ObliqueForest.unity" },
            locationPathName = destination + "/ForestPrototype.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        File.WriteAllText("Verification/current-build/build-result.txt", result.summary.result + " errors=" + result.summary.totalErrors);
        if (result.summary.result != BuildResult.Succeeded) throw new System.Exception("Ecology visual build failed");
    }
}
}




