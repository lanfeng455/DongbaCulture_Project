using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
namespace DongbaPrototype {
public static class EcologyReleaseBuilder {
 [MenuItem("Dongba Prototype/Legacy/Build Old Atmosphere")]
 public static void Build(){
 const string destination="Release/DongbaForest-Oblique-11";
 Directory.CreateDirectory(destination);
 Directory.CreateDirectory("Verification/ecology-v11");
 var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/DongbaPrototype/ObliqueForest.unity"},locationPathName=destination+"/ForestPrototype.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
 File.WriteAllText("Verification/ecology-v11/build-result.txt",result.summary.result+" errors="+result.summary.totalErrors);
 if(result.summary.result!=BuildResult.Succeeded)throw new System.Exception("Ecology build failed");
 }
}
}

