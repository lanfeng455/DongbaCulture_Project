using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using System.IO;
namespace DongbaPrototype {
[InitializeOnLoad] public static class ObliqueBuilder { static ObliqueBuilder(){ EditorApplication.update += Tick; } static void Tick(){ if(!File.Exists("Verification/oblique.request") || EditorApplication.isCompiling || EditorApplication.isUpdating) return; File.Delete("Verification/oblique.request"); EditorApplication.update -= Tick; try { Build(); EditorApplication.Exit(0); } catch(System.Exception e) { File.WriteAllText("Verification/oblique-failure.txt",e.ToString()); EditorApplication.Exit(1); } }
 [MenuItem("Dongba Prototype/Legacy/Recreate And Build Old Oblique Preview")]
 public static void Build(){
 var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 new GameObject("Oblique forest").AddComponent<ObliqueForest>();var go=new GameObject("Main Camera");go.tag="MainCamera";go.AddComponent<Camera>();go.AddComponent<AudioListener>();
 EditorSceneManager.SaveScene(scene,"Assets/DongbaPrototype/ObliqueForest.unity");
 Directory.CreateDirectory("Release/DongbaForest-Oblique-09");
 var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/DongbaPrototype/ObliqueForest.unity"},locationPathName="Release/DongbaForest-Oblique-09/ForestPrototype.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
 Directory.CreateDirectory("Verification");File.WriteAllText("Verification/oblique-build.txt",r.summary.result+" errors="+r.summary.totalErrors);if(r.summary.result!=BuildResult.Succeeded)throw new System.Exception("Build failed");
 }
}
}



