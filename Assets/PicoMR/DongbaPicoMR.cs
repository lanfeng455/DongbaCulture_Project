using System;
using System.Collections;
using System.Collections.Generic;
using DongbaPrototype;
using Unity.XR.CoreUtils;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace DongbaXR
{
    [DefaultExecutionOrder(-10000)]
    public sealed partial class DongbaPicoMR : MonoBehaviour
    {
        public XROrigin origin;
        public Camera xrCamera;
        public float worldUnitsPerMetre = 60;
        public bool playPanoramicOpening = false;
        public bool startInSandbox = false;
        bool modeHeld;
        [Range(1f, 3f)] public float artworkScale = 1f;
        [Min(.25f)] public float repositionHoldSeconds = .65f;
        const int ScanLayer = 30;
        const string ScanPrompt = "Look at a clear spot on the floor or table.";
        const string PlacePrompt = "Right Trigger · Place Forest";
        const string RepositionPrompt = "Hold Y · Reposition Forest";
        readonly Dictionary<Guid, GameObject> surfaces = new Dictionary<Guid, GameObject>();
        readonly List<XRMeshSubsystem> subsystems = new List<XRMeshSubsystem>();
        readonly List<InputDevice> devices = new List<InputDevice>();
        ObliqueForest forest;
        GameObject marker;
        Text message;
        RectTransform scanReadingFrame;
        bool scanReadingAnchored;
        readonly List<SpriteRenderer> forestRenderers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> titleTrees = new List<SpriteRenderer>();
        CanvasGroup titleGroup;
        bool titleActive = true, titleTransitioning;
        int mask;
        bool placed, forestStarted, triggerHeld, startHeld, permission;
        bool repositionArmed;
        float repositionHeldSeconds;
        Vector3 scanningOrigin;
        Quaternion scanningRotation = Quaternion.identity;
#if UNITY_EDITOR
        bool simulateTracking;
#endif
        XRMeshSubsystem meshSubsystem;
        string status = "Allow spatial data access to scan your room";

        void OnEnable() { PXR_Manager.SpatialMeshDataUpdated += UpdateMeshes; }
        void OnDisable()
        {
            PXR_Manager.SpatialMeshDataUpdated -= UpdateMeshes;
            if (meshSubsystem != null && meshSubsystem.running) meshSubsystem.Stop();
            PXR_Manager.EnableVideoSeeThrough = false;
        }
        IEnumerator Start()
        {
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            XRSettings.eyeTextureResolutionScale = 1.2f;
            forest = FindFirstObjectByType<ObliqueForest>();
            if (forest == null) { Debug.LogError("[DongbaPICO] Forest missing"); yield break; }
            forest.XRTrackingControlsCamera = true;
            forest.XRArtworkScale = artworkScale;
            mask = xrCamera.cullingMask;
            if (!playPanoramicOpening) xrCamera.cullingMask = 1 << ScanLayer;
            yield return null;
            foreach (var artwork in FindObjectsByType<DongbaPrototype.XRUprightBillboard>(FindObjectsSortMode.None))
                artwork.ApplyArtworkScale(artworkScale);
            origin.transform.localScale = Vector3.one * worldUnitsPerMetre;
            scanningOrigin = origin.transform.position;
            scanningRotation = origin.transform.rotation;
            xrCamera.orthographic = false;
            // Passthrough is composited underneath the eye buffer. Keep alpha
            // intact; desktop HDR / post-processing may produce an opaque buffer.
            xrCamera.allowHDR = false;
            xrCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = Color.clear;
            xrCamera.nearClipPlane = .01f * worldUnitsPerMetre;
            xrCamera.farClipPlane = 20 * worldUnitsPerMetre;
            xrCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var pose = xrCamera.GetComponent<TrackedPoseDriver>();
            if (pose == null) pose = xrCamera.gameObject.AddComponent<TrackedPoseDriver>();
            pose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            // Preserve our paper ground: it is part of the forest, not an alternate MR floor.
            forest.ConfigureXRHUD(worldUnitsPerMetre);
            forest.ConfigureXRPaper();
            xrCamera.cullingMask = mask | (1 << ScanLayer);
            foreach (var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                forestRenderers.Add(renderer);
                renderer.enabled = false;
            }
            titleActive = playPanoramicOpening;
            if (playPanoramicOpening)
            {
                CreatePanoramicTitle();
                xrCamera.backgroundColor = new Color(.92f, .86f, .73f, 1f);
            }
            else
            {
                xrCamera.backgroundColor = Color.clear;
                xrCamera.cullingMask = 1 << ScanLayer;
            }
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "PICO surface placement cursor";
            marker.layer = ScanLayer;
            Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = Vector3.one * .025f * worldUnitsPerMetre;
            marker.SetActive(false);
            BuildPlacementManuscript();
            PXR_Manager.EnableVideoSeeThrough = true;
            status = titleActive ? "Press right A to begin" : ScanPrompt;
            if (!titleActive) RequestSpatialAccess();
        }
        void RequestSpatialAccess()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PXR_PermissionRequest.RequestUserPermissionMR(
                _ => status = "Spatial access denied: enable it in app permissions",
                _ => { permission = true; status = ScanPrompt; Debug.Log("[DongbaPICO] Spatial permission granted"); },
                _ => status = "Spatial access denied: enable it in app permissions");
#else
            status = "Room scanning requires PICO 4 Ultra";
#endif
        }
        void CreatePanoramicTitle()
        {
            var root = new GameObject("Dongba panoramic title").transform;
            root.SetParent(origin.transform, false);
            var groupObject = new GameObject("Title text", typeof(Canvas), typeof(CanvasGroup));
            var canvas = groupObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            titleGroup = groupObject.GetComponent<CanvasGroup>();
            var rect = groupObject.GetComponent<RectTransform>();
            rect.SetParent(root, false); rect.sizeDelta = new Vector2(1000, 360);
            rect.localScale = Vector3.one * (.75f / 1000f);
            rect.localPosition = new Vector3(0, 1.25f * worldUnitsPerMetre, 1.45f * worldUnitsPerMetre);
            var headForward = Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up).normalized;
            root.rotation = Quaternion.LookRotation(headForward, Vector3.up);
            AddTitleText(rect, "DONGBA", 88, new Vector2(0, 95));
            AddTitleText(rect, "FOREST", 48, new Vector2(0, 15));
            AddTitleText(rect, "START", 46, new Vector2(0, -100));
            AddTitleText(rect, "Press right controller A", 24, new Vector2(0, -158));
            for (int i = 0; i < 32; i++)
            {
                var sprite = forest.GetPicoOpeningGlyph("plant_" + (i % 9).ToString("00"));
                if (sprite == null) continue;
                var tree = new GameObject("Panoramic Dongba tree");
                var angle = i * Mathf.PI * (3f - Mathf.Sqrt(5f));
                float radius = i < 10 ? 1.35f : 2.5f;
                float size = (i < 10 ? .48f : .72f) * worldUnitsPerMetre;
                tree.transform.SetParent(root, false);
                tree.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius) * worldUnitsPerMetre;
                tree.transform.localScale = Vector3.one * (size / sprite.bounds.size.y);
                var image = tree.AddComponent<SpriteRenderer>(); image.sprite = sprite; image.sortingOrder = i;
                titleTrees.Add(image);
            }
        }
        static void AddTitleText(RectTransform parent, string value, int size, Vector2 position)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(900, 64); rect.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.25f, .14f, .08f); text.raycastTarget = false;
        }
        IEnumerator BeginTitleTransition()
        {
            if (titleTransitioning) yield break;
            titleTransitioning = true;
            float elapsed = 0;
            while (elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float reveal = Mathf.Clamp01(elapsed / 2f);
                titleGroup.alpha = 1f - reveal;
                xrCamera.backgroundColor = new Color(.92f, .86f, .73f, 1f - reveal);
                foreach (var tree in titleTrees)
                {
                    if (tree == null) continue;
                    float depth = Vector3.Distance(tree.transform.position, xrCamera.transform.position) / (2.5f * worldUnitsPerMetre);
                    var color = tree.color; color.a = Mathf.Clamp01(1f - reveal * Mathf.Lerp(.45f, 1f, depth)); tree.color = color;
                }
                yield return null;
            }
            titleActive = false; titleGroup.gameObject.SetActive(false);
            foreach (var tree in titleTrees) if (tree != null) Destroy(tree.gameObject);
            xrCamera.backgroundColor = Color.clear;
            xrCamera.cullingMask = 1 << ScanLayer;
            RequestSpatialAccess();
            status = permission ? ScanPrompt : "Allow spatial access to scan your room";
        }
        void OnApplicationPause(bool paused)
        {
            if (!paused && isActiveAndEnabled) PXR_Manager.EnableVideoSeeThrough = true;
        }
        void UpdateMeshes(List<PxrSpatialMeshInfo> changes)
        {
            foreach (var block in changes)
            {
                if (block.state == MeshChangeState.Removed)
                {
                    if (surfaces.TryGetValue(block.uuid, out var old)) { Destroy(old.GetComponent<MeshCollider>().sharedMesh); Destroy(old); surfaces.Remove(block.uuid); }
                    continue;
                }
                if (!surfaces.TryGetValue(block.uuid, out var go))
                {
                    go = new GameObject("PICO scanned surface", typeof(MeshCollider));
                    go.layer = ScanLayer;
                    go.transform.SetParent(origin.transform, false);
                    surfaces.Add(block.uuid, go);
                }
                go.transform.SetLocalPositionAndRotation(block.position, block.rotation);
                var collider = go.GetComponent<MeshCollider>();
                var previous = collider.sharedMesh;
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(block.vertices);
                mesh.SetTriangles(block.indices, 0);
                mesh.RecalculateNormals();
                collider.sharedMesh = mesh;
                if (previous != null) Destroy(previous);
            }
        }
        void Update()
        {
            if (message == null) return;
            foreach (var tree in titleTrees)
            {
                if (tree == null) continue;
                var away = tree.transform.position - xrCamera.transform.position;
                away.y = 0;
                if (away.sqrMagnitude > .001f) tree.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
            if (permission && meshSubsystem == null)
            {
                SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0) { meshSubsystem = subsystems[0]; meshSubsystem.Start(); }
            }
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            bool trigger = false, start = false;
            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool t)) trigger |= t;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float pressure)) trigger |= pressure > .65f;
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool a)) start |= a;
            }
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool reposition = left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool y) && y;
            ProcessModeChoice(left.TryGetFeatureValue(CommonUsages.primaryButton,out bool x)&&x);
            bool confirm = trigger && !triggerHeld;
            if (titleActive && start && !startHeld) StartCoroutine(BeginTitleTransition());
            if (UpdateRepositionInput(reposition, Time.unscaledDeltaTime)) confirm = false;
            triggerHeld = trigger; startHeld = start;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracking) && tracking;
#if UNITY_EDITOR
            tracked |= simulateTracking;
#endif
            if(tracked&&!placed&&!scanReadingAnchored)AnchorScanReadingFrame();
            UpdatePlacementVisibility(tracked);
            marker.SetActive(false);
            if (!titleActive && !placed && permission && tracked)
            {
                status = ScanPrompt;
                if (Physics.Raycast(xrCamera.transform.position, xrCamera.transform.forward, out var hit, 8 * worldUnitsPerMetre, 1 << ScanLayer)
                    && Vector3.Dot(hit.normal, Vector3.up) > .9f)
                {
                    status = PlacePrompt;
                    marker.SetActive(true); marker.transform.position = hit.point;
                    if (confirm) PlaceOnSurface(hit.point);
                }
            }
            if (placed) status = tracked ? RepositionPrompt : "Tracking lost: look around slowly";
            RefreshPlacementMessage();
        }
        bool UpdateRepositionInput(bool held, float elapsed)
        {
            if (!held || titleActive || !placed)
            {
                repositionHeldSeconds = 0;
                repositionArmed = !held;
                return false;
            }
            if (!repositionArmed) return false;
            repositionHeldSeconds += elapsed;
            if (repositionHeldSeconds < repositionHoldSeconds) return false;
            repositionArmed = false;
            ResetPlacement();
            return true;
        }
        void PlaceOnSurface(Vector3 worldPoint)
        {
            // Keep the authored forest coordinates (east = +X, front = -Z).
            // Map the tracked room into that frame with a rigid yaw + translation,
            // so the surface hit becomes the origin and the viewer is at the front.
            // Rotating/mirroring forest sprites instead would break world-space AI,
            // river collisions and the controller ray's scanned-surface alignment.
            var towardsSurface = Vector3.ProjectOnPlane(worldPoint - xrCamera.transform.position, Vector3.up);
            if (towardsSurface.sqrMagnitude < .0001f)
                towardsSurface = Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up);
            if (towardsSurface.sqrMagnitude < .0001f)
                towardsSurface = origin.transform.forward;
            float yaw = Vector3.SignedAngle(towardsSurface, Vector3.forward, Vector3.up);
            var align = Quaternion.AngleAxis(yaw, Vector3.up);
            origin.transform.SetPositionAndRotation(align * (origin.transform.position - worldPoint), align * origin.transform.rotation);
            Physics.SyncTransforms();
            placed = true;
            Debug.Log("[DongbaPICO] Forest placed on scanned surface; front aligned, yaw correction=" + yaw.ToString("F1"));
            marker.SetActive(false);
            if (!forestStarted)
            {
                foreach (var renderer in forestRenderers) if (renderer != null) renderer.enabled = true;
                forest.StartForestMode(!startInSandbox);
                forestStarted = true;
            }
            forest.AnchorXRReadingFrame();
            UpdatePlacementVisibility(true);
        }

        void AnchorScanReadingFrame(){
            if(scanReadingFrame==null)return;
            var forward=xrCamera.transform.forward;var up=Vector3.ProjectOnPlane(Vector3.up,forward);
            if(up.sqrMagnitude<.001f)up=xrCamera.transform.up;
            var rotation=Quaternion.LookRotation(forward,up);
            scanReadingFrame.SetPositionAndRotation(xrCamera.transform.position+rotation*new Vector3(0,-.08f,1.15f)*worldUnitsPerMetre,rotation);
            scanReadingAnchored=true;
        }

        void ResetPlacement()
        {
            placed = false;scanReadingAnchored=false;
            Debug.Log("[DongbaPICO] Forest reposition requested");
            origin.transform.SetPositionAndRotation(scanningOrigin, scanningRotation);
            Physics.SyncTransforms();
            UpdatePlacementVisibility(false);
            status = ScanPrompt;
            RefreshPlacementMessage();
        }

        void UpdatePlacementVisibility(bool tracked)
        {
            forest.XRExperienceReady = placed && tracked && !titleActive;
            if (!titleActive)
                xrCamera.cullingMask = forest.XRExperienceReady ? mask | (1 << ScanLayer) : 1 << ScanLayer;
        }

        void ProcessModeChoice(bool held) {
            if(!forestStarted&&!titleActive&&held&&!modeHeld)startInSandbox=!startInSandbox;
            modeHeld=held;
        }
        void RefreshPlacementMessage()
        {
            string prompt=status==ScanPrompt?DongbaInterface.Choose("Look at a clear spot on the floor or table.","看向地面或桌面的一块空处。"):status==PlacePrompt?DongbaInterface.Choose("Right Trigger · Place forest","右扳机 · 放置森林"):status;
            RefreshPlacementManuscript();
            message.text=prompt;
            scanReadingFrame.gameObject.SetActive(!titleActive && !placed && scanReadingAnchored);
        }

#if UNITY_EDITOR
        public Transform ScanReadingFrameForVerification=>scanReadingFrame;
        public void SimulateScanReadingForVerification(){simulateTracking=true;AnchorScanReadingFrame();RefreshPlacementMessage();}
        public void SimulateModeChoiceForVerification(bool held){ProcessModeChoice(held);RefreshPlacementMessage();}
        public void SimulateSurfacePlacementForVerification(Vector3 point)
        {
            simulateTracking = true;
            PlaceOnSurface(point);
            RefreshPlacementMessage();
        }

        public bool SimulateRepositionInputForVerification(bool held, float elapsed) =>
            UpdateRepositionInput(held, elapsed);
#endif
        void LateUpdate()
        {
            if (forest != null && message != null && !titleActive) forest.UpdateXRHUD(placed, status);
        }
        void OnDestroy()
        {
            if (entryInk != null) Destroy(entryInk);
            if (marker != null) Destroy(marker);
            foreach (var go in surfaces.Values) if (go != null) { Destroy(go.GetComponent<MeshCollider>().sharedMesh); Destroy(go); }
        }
    }
}
