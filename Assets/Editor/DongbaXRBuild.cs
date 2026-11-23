using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DongbaXR.Editor
{
    public static class DongbaXRBuild
    {
        const string Forest = "Assets/DongbaPrototype/ObliqueForest.unity";

        [MenuItem("Dongba/XR/Configure Android tools and forest scene")]
        public static void Configure()
        {
            DongbaPicoSetup.ConfigureAndroid();
            // BuildPipeline validates Unity Hub / External Tools SDK, NDK and JDK settings.
            Require(Forest);
        }

        [MenuItem("Dongba/XR/Build Android forest baseline (no tracking yet)")]
        public static void BuildBaseline()
        {
            Configure();
            Build(Forest, "Dongba-Forest-Baseline.apk");
        }

        [MenuItem("Dongba/XR/Build tabletop XR APK")]
        public static void BuildXR()
        {
            Configure();
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetApplicationIdentifier(android, "com.dongba.culture.picomr");
            PlayerSettings.GetScriptingDefineSymbols(android, out var defines);
            PlayerSettings.SetScriptingDefineSymbols(android, System.Array.FindAll(defines, value => value != "USE_ROKID_OPENXR"));
            Require(DongbaPicoSetup.ScenePath);
            DongbaPicoSetup.ConfigureLoader();
            AssetDatabase.SaveAssets();
            Build(DongbaPicoSetup.ScenePath, "Dongba-PICO-MR.apk");
        }

        static void Build(string scene, string apk)
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            Directory.CreateDirectory("Builds/Android");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scene == DongbaPicoSetup.ScenePath ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Concat(new[] { scene }, System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(EditorBuildSettings.scenes, s => s.enabled), s => s.path)))) : new[] { scene },
                locationPathName = "Builds/Android/" + apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + result.summary.result);
        }

        static void Require(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required XR build file is missing", path);
        }

    }
}
