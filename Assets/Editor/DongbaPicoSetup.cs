using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.PXR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.ARFoundation;

namespace DongbaXR.Editor
{
    // Keep PICO preparation separate from the existing Rokid build commands.
    public static class DongbaPicoSetup
    {
        public const string ScenePath = "Assets/PicoMR/DongbaPicoMR.unity";

        public static void ConfigureLoader()
        {
            if (PXR_Settings.GetSettings() == null)
            {
                var picoSettings = AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/PicoMR/PICOSettings.asset");
                if (picoSettings == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/PicoMR")) AssetDatabase.CreateFolder("Assets", "PicoMR");
                    picoSettings = ScriptableObject.CreateInstance<PXR_Settings>();
                    picoSettings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.MultiPass;
                    AssetDatabase.CreateAsset(picoSettings, "Assets/PicoMR/PICOSettings.asset");
                }
                EditorBuildSettings.AddConfigObject("Unity.XR.PXR.Settings", picoSettings, true);
            }
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget store))
                throw new InvalidOperationException("XR settings missing.");
            if (!store.HasSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!store.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var settings = store.SettingsForBuildTarget(BuildTargetGroup.Android);
            foreach (var loader in new System.Collections.Generic.List<XRLoader>(settings.Manager.activeLoaders))
                XRPackageMetadataStore.RemoveLoader(settings.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(settings.Manager, "Unity.XR.PXR.PXR_Loader", BuildTargetGroup.Android))
                throw new InvalidOperationException("PICO loader assignment failed.");
            settings.InitManagerOnStart = true;
            var config = PXR_ProjectSetting.GetProjectConfig();
            config.videoSeeThrough = true;
            config.spatialMesh = true;
            config.spatialAnchor = true;
            config.sceneCapture = true;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(settings.Manager);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dongba/PICO Ultra/Convert project to PICO MR")]
        public static void ConvertProject()
        {
            ConfigureAndroid();
            DongbaXRBuild.Configure();
            ConfigureLoader();
            if (!AssetDatabase.IsValidFolder("Assets/PicoMR")) AssetDatabase.CreateFolder("Assets", "PicoMR");
            var scene = EditorSceneManager.OpenScene(File.Exists(ScenePath) ? ScenePath : DongbaXRScene.ScenePath);
            foreach (var old in UnityEngine.Object.FindObjectsByType<DongbaTabletopXR>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ARPlaneManager>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ARRaycastManager>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ARAnchorManager>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) throw new InvalidOperationException("XR Origin missing.");
            origin.name = "PICO MR Origin";
            if (origin.GetComponent<PXR_Manager>() == null) origin.gameObject.AddComponent<PXR_Manager>();
            var runtime = origin.GetComponent<DongbaPicoMR>();
            if (runtime == null) runtime = origin.gameObject.AddComponent<DongbaPicoMR>();
            runtime.origin = origin;
            runtime.xrCamera = origin.Camera;
            origin.Camera.clearFlags = CameraClearFlags.SolidColor;
            origin.Camera.backgroundColor = Color.clear;
            var urp = origin.Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urp != null) { urp.renderPostProcessing = false; UnityEngine.Object.DestroyImmediate(urp); }
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            int quality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; }
            QualitySettings.SetQualityLevel(quality, false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            if (settingsLoaderCount() != 1) throw new InvalidOperationException("Expected one Android loader.");
            Debug.Log("[DongbaPICO] CONVERSION VERIFIED: PICO loader, passthrough, spatial mesh, spatial permissions and MR startup scene configured.");
        }
        static int settingsLoaderCount()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget store);
            return store.SettingsForBuildTarget(BuildTargetGroup.Android).Manager.activeLoaders.Count;
        }

        [MenuItem("Dongba/PICO Ultra/1 - Prepare Android platform")]
        public static void ConfigureAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK, NDK and OpenJDK for this Unity Editor first.");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android platform switch failed. Check the Unity Console.");
            AssetDatabase.SaveAssets();
            Debug.Log("[DongbaPICO] Android ARM64 / IL2CPP configured. PICO provider setup is still required.");
        }

        [MenuItem("Dongba/PICO Ultra/2 - Create separate MR scene template")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("PICO scene already exists; preserving your edits.");
            if (!File.Exists(DongbaXRScene.ScenePath))
                throw new InvalidOperationException("Existing tabletop XR scene is missing.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/PicoMR")) AssetDatabase.CreateFolder("Assets", "PicoMR");
            // Copy through AssetDatabase so references remain intact and Unity creates a new GUID.
            if (!AssetDatabase.CopyAsset(DongbaXRScene.ScenePath, ScenePath))
                throw new IOException("Could not copy tabletop scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var placement = UnityEngine.Object.FindFirstObjectByType<DongbaTabletopXR>();
            if (placement != null)
            {
                placement.controllerConfirmation = true;
                EditorUtility.SetDirty(placement);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[DongbaPICO] MR scene template created. Configure the PICO provider and passthrough before building; Rokid loader settings were not changed.");
        }

        [MenuItem("Dongba/PICO Ultra/3 - Open PICO SDK setup guide")]
        public static void Guide()
        {
            Application.OpenURL("https://developer.picoxr.com/document/unity/import-the-sdk/");
        }
    }
}
