using System;
using System.Collections;
using System.IO;
using System.Text;
using DongbaXR;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Media;
#endif

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        IEnumerator CheckFirstAxe()
        {
#if UNITY_EDITOR
            yield return null;
            var report = new StringBuilder("Unity Editor play mode with simulated horizontal surface and controller button edges.\n");
            var steps = RunFirstAxeCheck(report);
            Exception failure = null;
            for (;;)
            {
                bool running;
                try { running = steps.MoveNext(); }
                catch (Exception error)
                {
                    failure = error;
                    break;
                }
                if (!running) break;
                yield return steps.Current;
            }
            if (failure != null)
            {
                Debug.LogException(failure);
                report.AppendLine("EXCEPTION: " + failure);
            }
            (steps as IDisposable)?.Dispose();
            Time.captureFramerate = 0;
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/firstaxe-check.txt", report.ToString());
            Debug.Log(report.ToString());
            UnityEditor.EditorApplication.Exit(failure == null && !report.ToString().Contains("FAIL ") ? 0 : 1);
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        IEnumerator RunFirstAxeCheck(StringBuilder report)
        {
            var pico = FindFirstObjectByType<DongbaPicoMR>();
            if (pico == null) throw new InvalidOperationException("DongbaPicoMR scene root is missing.");
            void Check(string name, bool condition) => report.AppendLine((condition ? "PASS " : "FAIL ") + name);

            yield return null;
            Check("Forest starts hidden before any surface is selected", !XRExperienceReady && !FirstAxeActive &&
                playerImage != null && (pico.xrCamera.cullingMask & (1 << playerImage.gameObject.layer)) == 0);
            pico.SimulateSurfacePlacementForVerification(new Vector3(2, -.4f, 3));
            yield return null;
            Check("Surface confirmation reveals the full forest and enters free movement", XRExperienceReady &&
                FirstAxeActive && playerImage.enabled && !fatherIntroActive && plants.Count > 7);
            Check("Placement does not swing or equip a tool", attackAge < 0 && !woodenAxe && carriedWood == 0 &&
                !axe.gameObject.activeSelf);
            DispatchXRButtons(false, false, false, false, false, false);
            DispatchXRButtons(false, false, false, true, false, false, connectedChanged: true);
            DispatchXRButtons(false, false, false, true, false, false);
            Check("Held placement trigger is latched and cannot swing", attackAge < 0);
            DispatchXRButtons(false, false, false, false, false, false);
            Check("Opening contains two chickens, one deer and one boar", animals.Count == 4 && riversideDeer != null && riversideBoar != null && riversideChickens[0] != null && riversideChickens[1] != null);
            bool fullTimeline = Array.IndexOf(Environment.GetCommandLineArgs(), "--firstaxe-capture") >= 0;
            Time.captureFramerate = 30;
            if (cam.TryGetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>(out var pose)) pose.enabled = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;

            player.position = storyFatherRoot.position + Vector3.right * Mathf.Min(1, FatherTalkRange * .5f);
            HandleXRPrimaryAction();
            Check("A near Father starts the English line without jumping or locking movement",
                FatherOpeningSubtitleVisible && jumpAge < 0 && !fatherIntroActive);
            float fatherFadeAt = fatherLineEndsAt;
            Check("Father's line has a three-second lifetime",
                Mathf.Abs(fatherFadeAt - Time.unscaledTime - 3f) < .08f);
            yield return null;
            Check("The actual world subtitle is exactly Father: We need an axe.",
                forestSubtitleKey == "Father\nWe need an axe.");
            while (Time.unscaledTime < fatherFadeAt + .05f) yield return null;
            Check("Father's line expires automatically", !FatherOpeningSubtitleVisible);

            var firstTree = firstAxeTeachingTrees[0];
            var secondTree = firstAxeTeachingTrees[1];
            if (firstTree == null || secondTree == null) throw new InvalidOperationException("Both teaching trees must exist.");
            Check("Both teaching trees start with three durability", firstTree.health == 3 && secondTree.health == 3);
            cam.backgroundColor = new Color(.83f, .80f, .72f);
            cam.fieldOfView = 60;
            var center = firstTree.home + Vector3.up;
            var seatedOffset = new Vector3(0, 3.6f, -4.1f);
            cam.transform.SetPositionAndRotation(center + seatedOffset, Quaternion.LookRotation(-seatedOffset, Vector3.up));
            using (var capture = fullTimeline ? new FirstAxeStrikeCapture(960, 540, report) : null)
            {
                for (int strike = 0; strike < 2; strike++)
                {
                    int direction = strike == 0 ? 1 : -1;
                    player.position = firstTree.home - Vector3.right * direction * Mathf.Min(1.1f, BareHandRange * .8f);
                    facing = direction;
                    heading = Vector3.right * direction;
                    Vector3 standingAt = Feet;
                    Vector3 originalTorso = body.localPosition;
                    for (int frame = 0; frame < 9; frame++)
                    {
                        yield return null;
                        capture?.AddFrame(cam);
                    }
                    int before = firstTree.health;
                    bool capturedContact = false;
                    bool feetStayed = true;
                    bool torsoStayed = true;
                    HandleXRTriggerAction();
                    while (attackAge >= 0)
                    {
                        HandleXRTriggerAction(); // Another edge while in the same swing must be ignored.
                        yield return null;
                        feetStayed &= Vector3.Distance(standingAt, Feet) < .001f;
                        torsoStayed &= Vector3.Distance(originalTorso, body.localPosition) < .06f;
                        string contactShot = null;
                        if (!capturedContact && firstTree.health < before)
                        {
                            capturedContact = true;
                            Check((direction > 0 ? "Right" : "Left") + " chest contacts the tree on the damage frame",
                                firstAxeArm.enabled && !firstAxeFist.enabled &&
                                Vector3.Distance(firstAxeStrikePoint, firstAxeArmTip) < .12f);
                            contactShot = direction > 0 ? "firstaxe-contact-right.png" : "firstaxe-contact-left.png";
                        }
                        capture?.AddFrame(cam, contactShot);
                    }
                    Check("Repeated trigger edges during " + (direction > 0 ? "right" : "left") + " swing cause one hit",
                        firstTree.health == before - 1 && capturedContact);
                    Check("Feet and torso stayed planted during " + (direction > 0 ? "right" : "left") + " swing",
                        feetStayed && torsoStayed);
                    for (int frame = 0; frame < 7; frame++)
                    {
                        yield return null;
                        capture?.AddFrame(cam);
                    }
                }
            }

            int preMiss = firstTree.health;
            player.position = firstTree.home - Vector3.right * 3;
            heading = Vector3.left;
            facing = -1;
            HandleXRTriggerAction();
            while (attackAge >= 0) yield return null;
            Check("Miss leaves tree durability and inventory unchanged", firstTree.health == preMiss && carriedWood == 0);

            player.position = firstTree.home + Vector3.right * 1.05f;
            heading = Vector3.left;
            HandleXRTriggerAction();
            while (attackAge >= 0) yield return null;
            Check("Third hit knocks the first tree down and produces exactly three pieces of wood",
                firstTree.health == 0 && looseWood.Count == 3 && carriedWood == 0);
            HandleXRTriggerAction();
            while (attackAge >= 0) yield return null;
            Check("Striking the stump produces no more wood", looseWood.Count == 3 && carriedWood == 0);

            if (looseWood.Count != 3) throw new InvalidOperationException("Drop checks require three physical wood pieces.");
            bool oneWoodSpot = TryFindSingleWoodPickup(out var pickup);
            Check("There is a walkable spot that collects just one piece", oneWoodSpot);
            if (!oneWoodSpot) throw new InvalidOperationException("Three drops cannot be picked up individually.");
            player.position = pickup;
            yield return null;
            Check("Walking into pickup range does not collect automatically", carriedWood == 0 && looseWood.Count == 3);
            UpdateXRContextHint();
            Check("Pickup prompt appears by wood in reach", xrTreeAction != null && xrTreeAction.text == "B · Pick Up Wood");
            DispatchXRButtons(false, true, false, false, false, false);
            DispatchXRButtons(false, true, false, false, false, false);
            Check("Pressing and holding B picks up exactly one piece", carriedWood == 1 && looseWood.Count == 2 && XRExperienceReady);
            DispatchXRButtons(false, false, false, false, false, false);
            var firstRemaining = looseWood[0].position;
            var secondRemaining = looseWood[1].position;
            Vector3 beforeReposition = Feet;
            pico.SimulateRepositionInputForVerification(false, 0);
            bool shortPress = pico.SimulateRepositionInputForVerification(true, pico.repositionHoldSeconds * .5f);
            bool reset = pico.SimulateRepositionInputForVerification(true, pico.repositionHoldSeconds * .6f);
            yield return null;
            Check("Short Y press stays placed, holding Y returns to scan mode", !shortPress && reset && !XRExperienceReady && carriedWood == 1);
            pico.SimulateSurfacePlacementForVerification(new Vector3(-4, .8f, 7));
            yield return null;
            Check("Reposition keeps items and actors in forest coordinates, preserves felled tree and dialogue progress",
                XRExperienceReady && carriedWood == 1 && looseWood.Count == 2 && !woodenAxe && firstTree.health == 0 &&
                Vector3.Distance(Feet, beforeReposition) < .001f &&
                Vector3.Distance(looseWood[0].position, firstRemaining) < .001f &&
                Vector3.Distance(looseWood[1].position, secondRemaining) < .001f && fatherSpoken && !FatherOpeningSubtitleVisible);

            for (int attempts = 0; looseWood.Count > 0 && attempts < 3; attempts++)
            {
                player.position = looseWood[0].position;
                DispatchXRButtons(false, false, false, false, false, false);
                DispatchXRButtons(false, true, false, false, false, false);
            }
            Check("Collecting remaining wood shows a Craft a Wooden Axe action",
                carriedWood == 3 && looseWood.Count == 0 && FirstAxeActionPrompt.Contains("Craft"));
            bool advancedDialogue = false;
            BeginManualStoryDialogue(new[] { "Father: Ready?" }, () => advancedDialogue = true);
            HandleXRPrimaryAction();
            Check("An active dialogue takes priority over crafting and jumping",
                advancedDialogue && !ManualStoryDialogueActive && carriedWood == 3 && !woodenAxe && jumpAge < 0);
            HandleXRPrimaryAction();
            yield return null;
            Check("A crafts a visible axe for three wood without jumping",
                woodenAxe && carriedWood == 0 && jumpAge < 0 && axe.gameObject.activeSelf);
            DispatchXRButtons(false, false, false, false, false, false);
            DispatchXRButtons(true, false, false, false, false, false);
            Check("A has no jump fallback", jumpAge < 0);
            while (ManualStoryDialogueActive) HandleXRPrimaryAction();
            DispatchXRButtons(false, false, true, false, false, false);
            Check("X starts a jump", jumpAge == 0);
            jumpAge = -1;

            while (ManualStoryDialogueActive) HandleXRPrimaryAction();
            player.position = secondTree.home - Vector3.right;
            heading = Vector3.right;
            facing = 1;
            HandleXRTriggerAction();
            while (attackAge >= 0) yield return null;
            Check("One axe swing fells the second teaching tree", secondTree.health == 0);
            pico.SimulateRepositionInputForVerification(false, 0);
            pico.SimulateRepositionInputForVerification(true, pico.repositionHoldSeconds + .01f);
            pico.SimulateSurfacePlacementForVerification(new Vector3(1, 0, 1));
            yield return null;
            Check("Reposition also preserves an equipped axe", woodenAxe && axe.gameObject.activeSelf);

            Being extra = plants.Find(tree => tree.tree && !IsFirstAxeTeachingTree(tree) && tree.health > 0);
            if (extra == null) throw new InvalidOperationException("No additional tree found.");
            player.position = extra.home - Vector3.right;
            heading = Vector3.right;
            while (extra.health > 0) ResolveFirstAxeStrike(extra);
            Check("Two extra trees advance to the feather task", riverTask == RiverTask.Feather);
            var chicken = riversideChickens[0];
            HitRiverside(chicken);
            Check("One hit drops one feather", chicken.health == 0 && featherDrops.Count == 1);
            player.position = System.Linq.Enumerable.First(featherDrops).position;
            TryPickUpWood();
            Check("Feather pickup advances the quest", carriedFeather == 1 && riverTask == RiverTask.Spear);
            while (ManualStoryDialogueActive) HandleXRPrimaryAction();
            foreach (var wood in looseWood.ToArray())
            {
                player.position = wood.position;
                TryPickUpWood();
            }
            HandleXRPrimaryAction();
            Check("A crafts and selects the spear with wood and stone", spearOwned && selectedSlot == 1 && riverTask == RiverTask.Boar);
            UpdateRiverHotbar(-1);
            Check("The left stick moves the selected slot", selectedSlot == 0);
            UpdateRiverHotbar(-1);
            Check("Holding the stick does not scroll", selectedSlot == 0);
            UpdateRiverHotbar(0);
            UpdateRiverHotbar(1);
            Check("Centering and flicking the stick selects the spear again", selectedSlot == 1);
            FaceRiversideAnimal(riversideBoar);
            int boarHealth = riversideBoar.health;
            HandleXRTriggerAction();
            while (!hit && attackAge >= 0) yield return null;
            Check("Axe does one damage to the boar and starts its warning",
                riversideBoar.health == boarHealth - 1 && boarPhase == BoarPhase.Warning);
            float chargeTimeout = Time.time + BoarWarning + .4f;
            while (boarPhase == BoarPhase.Warning && Time.time < chargeTimeout) yield return null;
            Check("Warning flows into a locked-direction charge", boarPhase == BoarPhase.Charge && chargeDirection.sqrMagnitude > .9f);
            int sonBeforeCharge = health;
            float restTimeout = Time.time + BoarChargeSeconds + .4f;
            while (boarPhase == BoarPhase.Charge && Time.time < restTimeout) yield return null;
            Check("Boar charge ends in recovery and inflicts at most one damage",
                boarPhase == BoarPhase.Rest && sonBeforeCharge - health <= 1);
            while (attackAge >= 0) yield return null;

            HitRiverside(riversideBoar);
            HitRiverside(riversideBoar);
            Check("Repelling the boar unlocks the river return", riverTask == RiverTask.Return);
            player.position = ForestSpawn;
            yield return null;
            while (ManualStoryDialogueActive) HandleXRPrimaryAction();
            Check("Returning leads to Father's capture without attacking the deer", riverTask == RiverTask.Done && !storyFatherRoot.gameObject.activeSelf);
            FaceRiversideAnimal(riversideDeer);
            Vector3 deerAt = riversideDeer.root.position;
            HandleXRTriggerAction();
            while (!hit && attackAge >= 0) yield return null;
            Check("Deer reacts with flight or a look-back if cornered",
                deerRunning || deerLookTimer > 0);
            float deerTimeout = Time.time + DeerFastSeconds + DeerBrakeSeconds + .5f;
            while (deerRunning && Time.time < deerTimeout) yield return null;
            Check("Deer actually runs, then looks back or continues if it cannot safely stop",
                Vector3.Distance(deerAt, riversideDeer.root.position) > .5f && (deerLookTimer > 0 || deerRunning));

            while (attackAge >= 0) yield return null;
            while (riversideDeer.health > 0) HitRiverside(riversideDeer);
            Check("Deer death drops exactly one antler", antlerDrops.Count == 1);
            HitRiverside(riversideDeer);
            Check("A dead deer does not drop another antler", antlerDrops.Count == 1);
            player.position = System.Linq.Enumerable.First(antlerDrops).position;
            TryPickUpWood();
            Check("Antler pickup has its own inventory count", carriedAntler == 1);
            player.position = new Vector3(-22, 0, -10);
            attackAge = jumpAge = -1;
            var initialDistance = gaitDistance;
            for (int frame = 0; frame < 40; frame++)
            {
                var previous = Feet;
                player.position += Vector3.right * .4f;
                UpdateFirstAxe(previous, .1f);
            }
            float fullSpeedFootfalls = (gaitDistance - initialDistance) / FootfallDistance;
            initialDistance = gaitDistance;
            for (int frame = 0; frame < 80; frame++)
            {
                var previous = Feet;
                player.position += Vector3.right * .2f;
                UpdateFirstAxe(previous, .1f);
            }
            float halfSpeedFootfalls = (gaitDistance - initialDistance) / FootfallDistance;
            Check("Sixteen actual units produce ten footfalls at full and half speed",
                Mathf.Abs(fullSpeedFootfalls - 10) < .001f && Mathf.Abs(halfSpeedFootfalls - 10) < .001f);
            initialDistance = gaitDistance;
            UpdateFirstAxe(Feet, .2f);
            Check("No displacement does not advance gait and returns to a planted pose",
                Mathf.Approximately(gaitDistance, initialDistance) && Mathf.Approximately(walkBlend, 0));
            report.AppendLine("Physical PICO tracking, scanned room geometry, chest contact visibility at headset scale, and vibration require headset verification.");
        }

        bool TryFindSingleWoodPickup(out Vector3 position)
        {
            foreach (var wood in looseWood)
            {
                for (int direction = 0; direction < 16; direction++)
                {
                    var radial = Quaternion.Euler(0, direction * 22.5f, 0) * Vector3.forward;
                    var candidate = wood.position + radial * (WoodPickupRadius * .87f);
                    if (River(candidate) || !Free(candidate)) continue;
                    int nearby = 0;
                    foreach (var other in looseWood)
                        if (Vector3.Distance(candidate, other.position) <= WoodPickupRadius) nearby++;
                    if (nearby == 1)
                    {
                        position = candidate;
                        return true;
                    }
                }
            }
            position = default;
            return false;
        }

        void FaceRiversideAnimal(Being actor)
        {
            player.position = actor.root.position - Vector3.right;
            heading = Vector3.right;
            facing = 1;
        }

        sealed class FirstAxeStrikeCapture : IDisposable
        {
            readonly RenderTexture render;
            readonly Texture2D frame;
            readonly StringBuilder report;
            MediaEncoder encoder;
            int frameCount;
            public FirstAxeStrikeCapture(int width, int height, StringBuilder report)
            {
                this.report = report;
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    report.AppendLine("Capture unavailable without a graphics device.");
                    return;
                }
                Directory.CreateDirectory("Verification");
                render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    encoder = new MediaEncoder(Path.GetFullPath("Verification/firstaxe-seated-strikes.mp4"),
                        new VideoTrackAttributes { width = (uint)width, height = (uint)height, frameRate = new MediaRational(30) });
                }
                catch (Exception error)
                {
                    report.AppendLine("Video encoder unavailable; writing contact PNGs: " + error.Message);
                }
            }
            public void AddFrame(Camera camera, string contactShot = null)
            {
                if (render == null || (encoder == null && contactShot == null)) return;
                var oldTarget = camera.targetTexture;
                var oldActive = RenderTexture.active;
                try
                {
                    camera.targetTexture = render;
                    camera.Render();
                    RenderTexture.active = render;
                    frame.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                    frame.Apply();
                    if (contactShot != null) File.WriteAllBytes(Path.Combine("Verification", contactShot), frame.EncodeToPNG());
                    if (encoder != null && encoder.AddFrame(frame)) frameCount++;
                }
                finally
                {
                    camera.targetTexture = oldTarget;
                    RenderTexture.active = oldActive;
                }
            }

            public void Dispose()
            {
                encoder?.Dispose();
                if (render != null)
                {
                    render.Release();
                    UnityEngine.Object.Destroy(render);
                    UnityEngine.Object.Destroy(frame);
                }
                report.AppendLine("Seated editor camera, silent MP4 frames encoded: " + frameCount);
            }
        }
#endif
    }
}
