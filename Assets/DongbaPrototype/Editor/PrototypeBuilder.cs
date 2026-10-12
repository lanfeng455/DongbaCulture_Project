using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DongbaPrototype
{
    [InitializeOnLoad]
    public static class PrototypeBuilder
    {
        const string ScenePath="Assets/DongbaPrototype/ForestPrototype.unity";
        static PrototypeBuilder() { EditorApplication.delayCall += AutoBuild; }
        static void AutoBuild()
        {
            if(!File.Exists("Verification/build.request")) return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=AutoBuild; return; }
            File.Delete("Verification/build.request");
            try { Build(); } catch(System.Exception e) { File.WriteAllText("Verification/build-result.txt",e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Dongba Prototype/Build Windows Player")]
        public static void Build()
        {
            if(!File.Exists(ScenePath)) {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                var root=new GameObject("Dongba Forest Prototype"); SceneManager.MoveGameObjectToScene(root,scene); root.AddComponent<ForestPrototype>();
                var camera=new GameObject("Camera"); SceneManager.MoveGameObjectToScene(camera,scene);
                camera.AddComponent<Camera>().backgroundColor=new Color(.1f,.12f,.1f); camera.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene,ScenePath); EditorSceneManager.CloseScene(scene,true);
            }
            Directory.CreateDirectory("Builds/ForestPrototype");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/ForestPrototype/ForestPrototype.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            File.WriteAllText("Verification/build-result.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nOutput: "+report.summary.outputPath);
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Prototype build failed");
        }
        public static void BuildShare()
        {
            if(!File.Exists(ScenePath)) throw new FileNotFoundException("Prototype scene is missing",ScenePath);
            const string output="ReleaseStaging/DongbaForestPrototype-Windows/ForestPrototype.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ScenePath},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
            });
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/share-build-result.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nOutput: "+report.summary.outputPath);
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Share build failed");
        }
        [MenuItem("Dongba Prototype/Open Prototype Scene")]
        public static void Open() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
    }
}
