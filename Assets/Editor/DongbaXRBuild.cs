using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace DongbaXR.Editor
{
    public static class DongbaXRBuild
    {
        // Junction to app/AndroidToolchain: Android batch scripts cannot parse '(x86)' in JAVA_HOME.
        const string Tools = "D:/DongbaTools";
        const string Forest = "Assets/DongbaPrototype/ObliqueForest.unity";

        [MenuItem("Dongba/XR/Configure Android tools and forest scene")]
        public static void Configure()
        {
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME", Tools + "/GradleCache");
            Environment.SetEnvironmentVariable("ANDROID_USER_HOME", Tools + "/User");
            Environment.SetEnvironmentVariable("TEMP", "D:/Program Files (x86)/app/_unity_downloads");
            Environment.SetEnvironmentVariable("TMP", "D:/Program Files (x86)/app/_unity_downloads");
            string[] jdk = Directory.GetDirectories(Tools + "/JDK", "jdk-*");
            if (jdk.Length != 1) throw new InvalidOperationException("Expected one JDK in " + Tools);
            Require(jdk[0] + "/bin/java.exe");
            Require(Tools + "/SDK/platform-tools/adb.exe");
            Require(Tools + "/SDK/platforms/android-36/android.jar");
            Require(Tools + "/SDK/build-tools/36.0.0/aapt2.exe");
            Require(Tools + "/NDK/android-ndk-r27c/source.properties");
            Require(Forest);
            AndroidExternalToolsSettings.sdkRootPath = Tools + "/SDK";
            AndroidExternalToolsSettings.ndkRootPath = Tools + "/NDK/android-ndk-r27c";
            AndroidExternalToolsSettings.jdkRootPath = jdk[0];
            AndroidExternalToolsSettings.Gradle.path = Path.GetDirectoryName(EditorApplication.applicationPath)
                + "/Data/PlaybackEngines/AndroidPlayer/Tools/gradle";
            // Keep the desktop build list intact. The XR build command uses its own scene list.
            Debug.Log("[DongbaXR] Android tools verified on D:. Forest: " + Forest);
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
            DongbaXRScene.ConfigureLoader();
            AssetDatabase.SaveAssets();
            Build(DongbaPicoSetup.ScenePath, "Dongba-PICO-MR.apk");
        }

        static void Build(string scene, string apk)
        {
            using var rendering = scene == DongbaXRScene.ScenePath ? new BuiltinRenderingScope() : null;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            Directory.CreateDirectory("Builds/Android");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
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

        sealed class BuiltinRenderingScope : IDisposable
        {
            readonly int quality = QualitySettings.GetQualityLevel();
            readonly UnityEngine.Rendering.RenderPipelineAsset defaultPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            readonly UnityEngine.Rendering.RenderPipelineAsset[] pipelines = new UnityEngine.Rendering.RenderPipelineAsset[QualitySettings.names.Length];
            public BuiltinRenderingScope()
            {
                for (int i = 0; i < pipelines.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    pipelines[i] = QualitySettings.renderPipeline;
                    QualitySettings.renderPipeline = null;
                }
                QualitySettings.SetQualityLevel(quality, false);
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            }
            public void Dispose()
            {
                for (int i = 0; i < pipelines.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipelines[i];
                }
                QualitySettings.SetQualityLevel(quality, false);
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = defaultPipeline;
            }
        }
    }
}
