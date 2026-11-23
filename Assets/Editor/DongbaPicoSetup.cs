using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.PXR;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;

namespace DongbaXR.Editor
{
    // A separate entry point: preparing PICO never replaces the desktop scene list.
    public static class DongbaPicoSetup
    {
        public const string ScenePath = "Assets/PicoMR/DongbaPicoMR.unity";
        public static void ConfigureLoader()
        {
            var pico = AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/PicoMR/PICOSettings.asset");
            if (pico == null) throw new InvalidOperationException("PICOSettings.asset is missing.");
            EditorBuildSettings.AddConfigObject("Unity.XR.PXR.Settings", pico, true);
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget store))
            {
                store = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(store, "Assets/PicoMR/XRGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, store, true);
            }
            if (!store.HasSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!store.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var settings = store.SettingsForBuildTarget(BuildTargetGroup.Android);
            foreach (var loader in new System.Collections.Generic.List<XRLoader>(settings.Manager.activeLoaders))
                XRPackageMetadataStore.RemoveLoader(settings.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(settings.Manager, "Unity.XR.PXR.PXR_Loader", BuildTargetGroup.Android))
                throw new InvalidOperationException("PICO loader assignment failed.");
            settings.InitManagerOnStart = true;
            var config = PXR_ProjectSetting.GetProjectConfig();
            config.videoSeeThrough = config.spatialMesh = config.spatialAnchor = config.sceneCapture = true;
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(settings.Manager);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Dongba/PICO Ultra/Prepare PICO Android settings")]
        public static void ConvertProject()
        {
            ConfigureAndroid();
            ConfigureLoader();
            Debug.Log("PICO prepared. Desktop scenes and rendering settings preserved.");
        }
        public static void ConfigureAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK, NDK and OpenJDK for this Unity Editor first.");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        }
        [MenuItem("Dongba/PICO Ultra/Open MR scene")]
        public static void OpenScene()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
