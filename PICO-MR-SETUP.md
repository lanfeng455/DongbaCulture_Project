# PICO 4 Ultra MR

Current driver: native PICO XR. Startup scene: Assets/PicoMR/DongbaPicoMR.unity.
Editor: D:/Program Files (x86)/app/Unity/6000.6.7f1/Editor/Unity.exe.

Enabled: Android ARM64/IL2CPP/OpenGLES3, video passthrough, spatial mesh, spatial anchor capability and scene capture capability. DongbaPicoMR requests spatial-data permission, starts the mesh subsystem and creates invisible colliders from PICO spatial meshes. The SDK declares spatial permission during Android build.

Gaze at a scanned upward-facing surface and press the right trigger to place the forest. Right B resets placement. Status text covers scanning, permissions and tracking. Passthrough is restored after resume. Placement uses the current tracking coordinate frame; persistent anchors, recenter/relocalization recovery, surface boundary fitting and physical occlusion are not implemented. Scene capture is enabled as a capability; this placement flow uses live meshes, not a room-capture UI.

The MR scene uses built-in rendering for existing forest shaders. Default and quality render pipelines were set to built-in. The active build list contains only the PICO scene. Old Rokid packages were moved out of Packages into .utmp/Rokid-packages-disabled; their native plugins are excluded. Legacy scenes/source are inactive. Shared ARFoundation and pose driver dependencies are retained explicitly through Package Manager.

Menu: Dongba > PICO Ultra > Convert project to PICO MR.
Build: Dongba > XR > Build tabletop XR APK now targets Builds/Android/Dongba-PICO-MR.apk with PICO loader settings. Legacy loader commands delegate to PICO configuration.

SDK: PICO Integration 3.4.0, pinned to commit c93a59bab88d337fe0d54140499712897cedbe52 in the Unity Package Manager manifest. Compatibility patch for Unity 6000.6+: SpatialAudio/Runtime/PXR_Audio_Spatializer_SceneGeometry.cs uses GetEntityId for baked mesh filenames while retaining GetInstanceID on older Unity versions. Apply this patch in the downloaded package before building with Unity 6000.6+.

Initial conversion completed with exit code 0: .utmp/pico-convert.log. Final verification after removing Rokid packages: .utmp/pico-final-verify.log. No headset runtime or visual result has been verified yet. Verify spatial permission, passthrough, incoming meshes, placement cursor, trigger confirmation, B reset, tracking loss and resume on PICO 4 Ultra.

Upright artwork: XRUprightBillboard is registered by ObliqueForest.Billboard for non-flat artwork. In XR it rotates only around world Y toward the viewer, retaining in-plane animation and foot pivots. Child cards inherit their parent. Flat river/shadow artwork is excluded. Updated APK built and installed successfully; visual confirmation on headset remains user-dependent.

Clarity/UI update: runtime enforces full mip resolution, 4x MSAA, forced anisotropic filtering and eye texture scale 1.2. Forest HUD is a 0.68 m wide world-space panel beside the placed forest, with larger text; shared dialogue subtitles are reparented to that panel. Placement status moves from the head-relative label into the panel after placement. Resetting placement hides and repositions the panel for the next placement.

PICO controller input: Unity XR LeftHand/RightHand devices replace Gamepad.current in the forest MR input path. Left stick moves the character relative to horizontal headset forward, with a 0.18 radial deadzone and analog speed. Right A advances dialogue or jumps outside dialogue; right trigger swings the axe; left X tends/feeds/replants. Right B retains forest re-placement. Automatic idle patrol/care has been removed. Placement, tracking recovery and controller reconnect synchronize button state to prevent accidental actions. Desktop keyboard controls remain available outside MR. Actual button operation requires wearer confirmation.

MR artwork scale: upright forest sprites (trees, plants, animals, player and story figures) render at 2x their prior size, while the ground plane, flat ground marks and world-space UI keep their previous dimensions. This is a visual scale only; forest placement and movement coordinates are unchanged.

Opening flow: DongbaPicoMR starts in a panoramic ring of the existing dictionary plant glyphs with DONGBA / 东巴 and START. Right A begins a two-second fade that also lowers the camera background alpha to reveal PICO passthrough, then requests spatial access and enables surface placement. Right trigger places the forest. The existing father story starts after placement. Right B returns to placement mode. The transition is currently an alpha fade, not the desktop InkBloom shader. Validate the title text, perspective fade, placement and story start on the headset.
