using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace DongbaXR
{
    // Development APK only. Never adds overlays or collects room imagery.
    public sealed class DongbaPicoDeviceCheck : MonoBehaviour
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD
        float elapsed;
        int frames;
        bool previousTracking;
        bool previousLeft, previousRight;
        DongbaPrototype.ObliqueForest forest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<DongbaPicoMR>() == null) return;
            var go = new GameObject("PICO development diagnostics");
            go.AddComponent<DongbaPicoDeviceCheck>();
            DontDestroyOnLoad(go);
        }

        IEnumerator Start()
        {
            yield return null;
            forest = FindFirstObjectByType<DongbaPrototype.ObliqueForest>();
            yield return null;
            var camera = Camera.main;
            Debug.Log($"[DongbaDevice] PassthroughRequested={Unity.XR.PXR.PXR_Manager.EnableVideoSeeThrough} preserveAlpha={Graphics.preserveFramebufferAlpha} clearAlpha={camera.backgroundColor.a} HDR={camera.allowHDR}");
            Debug.Log($"[DongbaDevice] Unity={Application.unityVersion} app={Application.version} model={SystemInfo.deviceModel} graphics={SystemInfo.graphicsDeviceType} XR={XRSettings.enabled} eyes={XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} stereo={XRSettings.stereoRenderingMode}");
        }

        void Update()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracking = head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            bool left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid;
            bool right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
            if (tracking != previousTracking || left != previousLeft || right != previousRight)
            {
                Debug.Log($"[DongbaDevice] Tracking={tracking} leftController={left} rightController={right}");
                previousTracking = tracking; previousLeft = left; previousRight = right;
            }
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 5f) return;
            if (forest != null) Debug.Log("[DongbaDevice] Story " + forest.PicoStoryDiagnostic);
            Debug.Log($"[DongbaDevice] FPS={frames / elapsed:F1} tracked={tracking} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            elapsed = 0; frames = 0;
        }
#endif
    }
}
