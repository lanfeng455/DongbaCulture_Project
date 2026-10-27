using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DongbaPrototype {
public class DictionaryArtImporter : AssetPostprocessor {
    void OnPreprocessTexture(){
        if(!assetPath.StartsWith("Assets/DongbaPrototype/Resources/DictionaryArt/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
        importer.isReadable=false;
    }
}
public static class DictionaryArtBuilder {
    [MenuItem("Dongba Prototype/Legacy/Build Dictionary Preview")]
    public static void Build(){
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        const string destination="Release/DongbaForest-Forest-18";
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory("Verification/forest-layout-v18");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/DongbaPrototype/ObliqueForest.unity"},
            locationPathName=destination+"/ForestPrototype.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
        });
        File.WriteAllText("Verification/forest-layout-v18/build-result.txt",result.summary.result+" errors="+result.summary.totalErrors+" warnings="+result.summary.totalWarnings);
        if(result.summary.result!=BuildResult.Succeeded)throw new System.Exception("Dictionary art build failed");
    }
}
}

