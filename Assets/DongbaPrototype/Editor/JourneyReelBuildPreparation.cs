using UnityEditor;
namespace DongbaPrototype {public static class JourneyReelBuildPreparation {
 public static void Prepare(){
    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    foreach(string file in System.IO.Directory.GetFiles("Assets/DongbaPrototype/Resources/JourneyReel","*.png")){
      var importer=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter;if(importer==null)continue;
      if(importer.textureShape!=TextureImporterShape.Texture2D||importer.mipmapEnabled||importer.wrapMode!=UnityEngine.TextureWrapMode.Clamp||importer.textureCompression!=TextureImporterCompression.Uncompressed||!importer.alphaIsTransparency){
       importer.textureShape=TextureImporterShape.Texture2D;importer.mipmapEnabled=false;importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.SaveAndReimport();
      }
    }
 }
}}
