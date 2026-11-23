# PICO MR environment integration

This entry uses our current forest, father-and-son opening, ecology, axe, care/feeding and bilingual subtitles. The teammate's incomplete first-axe/riverside tutorial is deliberately excluded. Desktop title, forest and puzzle scenes remain the normal Windows build route.

## Open and build

- Main project: Unity 6000.3.12f1, as recorded in ProjectSettings/ProjectVersion.txt. Do not upgrade it to the VR branch's 6000.6 editor just to open MR.
- Scene: Assets/PicoMR/DongbaPicoMR.unity. Menu: Dongba > PICO Ultra > Open MR scene.
- Package Manager resolves PICO Integration 3.4.0 from pinned commit c93a59bab88d337fe0d54140499712897cedbe52. Git and network access are required on first import.
- Install Android Build Support, SDK, NDK and OpenJDK for your Unity installation. Use Unity's External Tools settings; there are no developer-specific D: paths.
- Run Dongba > PICO Ultra > Prepare PICO Android settings, then Dongba > XR > Build tabletop XR APK.
- Output: Builds/Android/Dongba-PICO-MR.apk. ARM64, IL2CPP, OpenGLES3, minimum Android API 29. The build starts in PICO MR and includes the existing scene dependencies.
- PICO is registered for Android only. The desktop scene list and existing render pipeline are preserved. OpenXR and AR simulation settings from the remote branch were not adopted.

## Forest controls

Look at an upward-facing scanned surface and press the right trigger to place the forest. Our story begins only after placement.

- Left stick: movement relative to the headset's horizontal facing.
- Right A: advance forest dialogue; jump outside dialogue.
- Right trigger: existing axe attack.
- Left X: existing care, replant and feed action.
- Hold left Y for 0.65 seconds: return to placement, preserving progress.
- Right stick click: EN / Chinese outside dialogue.

Placement/tracking loss suspends forest input. Button latches prevent a held placement trigger from immediately attacking. The manuscript ground and current glyph proportions remain intact. The room scale is controlled by worldUnitsPerMetre (60 by default), not by changing gameplay distances. HUD and shared subtitles are presented on a world-space panel.

## Verified scope and remaining hardware work

Editor simulation verifies placement, original opening, controller edges, movement, axe, spatial subtitles and repositioning. This is not proof of headset tracking, passthrough or Android performance.

The local machine has Unity 6000.3.0b1 and Windows build support only. It cannot produce or run a verified Android APK. The existing compatibility validation workspace is used for local PC checks; the team project version is unchanged.

This merge establishes the MR forest environment and controls. The desktop reel/camera handoffs and puzzle scenes are included for development, but are NOT yet an end-to-end headset experience. Their XR camera, spatial UI and puzzle controller adaptation still need implementation and headset validation before calling the whole story MR-ready.

On PICO hardware, check permission prompts, scan/placement, tracking loss/recovery, resume, text size, frame rate and physical clearance. Persistent anchors, relocalization recovery, fitting the whole forest within scanned surface bounds and physical occlusion are not implemented.

Editor regression entry: DongbaXR.Editor.DongbaPicoIntegrationCheck.Run with --pico-integration-check. It exits Unity when finished and writes Verification/pico-integration; use a closed validation workspace, not an unsaved editing session.
