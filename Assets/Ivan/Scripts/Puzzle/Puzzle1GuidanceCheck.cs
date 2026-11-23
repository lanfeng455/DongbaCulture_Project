using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DG.Tweening;
using DongbaPrototype;
using Ivan.Diorama;
using Ivan.Scroll;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ivan.Puzzle
{
    // Opt-in, real-player smoke test. Never runs in normal gameplay.
    public sealed class Puzzle1GuidanceCheck : MonoBehaviour
    {
        const string Folder = "Verification/puzzle-clues-20261123";
        readonly StringBuilder report = new();
        Keyboard keyboard;
        DioramaPlayerController player;
        Rigidbody body;
        Puzzle1Director director;
        Puzzle1Presentation presentation;
        bool recordingExit;
        Coroutine exitRecording;
        static bool RecordExitRequested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--record-puzzle-exit") >= 0;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        void Check(string name, bool ok) { report.AppendLine((ok ? "PASS " : "FAIL ") + name); File.WriteAllText(Folder + "/runtime.txt", report.ToString()); }
        void Place(Vector3 p) { player.ClearAutoMoveInput(); body.linearVelocity = Vector3.zero; body.position = p; player.transform.position = p; Physics.SyncTransforms(); }
        void PlaceSnake(SnakeActor snake, Vector3 ground)
        {
            snake.transform.DOKill();
            var offset = ground - snake.GetComponent<DioramaObstacle>().Collider.bounds.center;
            offset.y = 0;
            var p = snake.transform.position + offset;
            snake.transform.position = p; snake.GetComponent<Rigidbody>().position = p;
            Physics.SyncTransforms();
        }
        IEnumerator Key(Key key) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.08f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.08f); }
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); var t = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Folder + "/" + name + ".png", t.EncodeToPNG()); Destroy(t); }
        IEnumerator RecordExitFrames()
        {
            const string folder = "Verification/playtest-fixes-20261123/exit-frames";
            Directory.CreateDirectory(folder);
            var screen = new RenderTexture(Screen.width, Screen.height, 0);
            var small = new RenderTexture(960, 540, 0);
            var output = new Texture2D(960, 540, TextureFormat.RGB24, false);
            screen.Create(); small.Create(); int frame = 0;
            while (recordingExit && frame < 450)
            {
                yield return new WaitForEndOfFrame();
                var previous = RenderTexture.active;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
                RenderTexture.active = small;
                output.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); output.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(folder + "/" + (frame++).ToString("D4") + ".png", output.EncodeToPNG());
            }
            File.WriteAllText(folder + "/frame-count.txt", frame + " frames, 960x540, 30 fps; simulated normal-time playback.");
            screen.Release(); small.Release(); Destroy(screen); Destroy(small); Destroy(output);
            Time.captureFramerate = 0;
        }
        void ReportExitState(string phase)
        {
            bool isFree = (bool)typeof(DioramaPlayerController).GetMethod("IsFree", Private).Invoke(player, new object[]{body.position + Vector3.right * .15f});
            string text = "EXIT " + phase + " feet=" + body.position.ToString("F3") + " velocity=" + body.linearVelocity.ToString("F3") +
                " nextIsFree=" + isFree + " stageLock=" + typeof(DioramaPlayerController).GetField("m_stageMovementLocked", Private).GetValue(player) +
                " move=" + typeof(DioramaPlayerController).GetField("m_moveInput", Private).GetValue(player) +
                " auto=" + typeof(DioramaPlayerController).GetField("m_useAutoMoveInput", Private).GetValue(player) +
                " cameraHandoff=" + player.CameraHandoff + " dialogueBlocked=" + DialogueAdvanceInput.GameplayInputBlocked +
                " interfaceBlocked=" + DongbaInterface.GameplayBlocked + " keyboardCurrent=" + (Keyboard.current == keyboard) +
                " d=" + (Keyboard.current != null && Keyboard.current.dKey.isPressed) + " gateLocked=" + presentation.Locked;
            report.AppendLine(text); Debug.Log(text);
            foreach (var hit in Physics.OverlapCapsule(body.position + Vector3.up * .23f, body.position + Vector3.up * .38f, .24f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                var path = hit.name; var parent = hit.transform.parent; while(parent != null){path=parent.name+"/"+path;parent=parent.parent;}
                text="EXIT contact " + path + " bounds=" + hit.bounds.ToString("F3"); report.AppendLine(text);Debug.Log(text);
            }
            File.WriteAllText(Folder + "/runtime.txt", report.ToString());
        }
        IEnumerator CheckActorFacing()
        {
            var fox = StageActorRegistry.Find("Fox");
            Check("fox actor is available", fox != null);
            if (fox == null) yield break;
            var sprite = fox.GetComponent<SpriteRenderer>();
            Vector3 origin = fox.transform.position;
            fox.StopMove(); fox.Show(); fox.Face(-1);
            yield return null;
            Check("fox faces left toward the player despite scene initial flip", sprite.flipX);
            fox.StartMove(2f); yield return new WaitForSeconds(.2f); fox.StopMove();
            Check("fox walks forward when facing left", fox.transform.position.x < origin.x - .1f);
            fox.Face(1); yield return null;
            Check("fox faces right for its departure", !sprite.flipX);
            float leftX = fox.transform.position.x;
            fox.StartMove(2f); yield return new WaitForSeconds(.2f); fox.StopMove();
            Check("fox walks forward when facing right", fox.transform.position.x > leftX + .1f);
            fox.transform.position = origin; fox.Face(-1);

            // Also cover a left-facing source with mixed multipart flips. Logical
            // movement must not invert simply because the source faces left.
            var root = new GameObject("Facing regression actor"); root.SetActive(false);
            var primary = root.AddComponent<SpriteRenderer>(); primary.flipX = true;
            var child = new GameObject("Opposite source part"); child.transform.SetParent(root.transform);
            var part = child.AddComponent<SpriteRenderer>(); part.flipX = false;
            var actor = root.AddComponent<StageActor>();
            typeof(StageActor).GetField("m_actorName", Private).SetValue(actor, "FacingCheck");
            typeof(StageActor).GetField("m_defaultFacesLeft", Private).SetValue(actor, true);
            root.SetActive(true); actor.Face(-1); yield return null;
            Check("left-native multipart actor retains relative part orientations", !primary.flipX && part.flipX);
            actor.StartMove(2f); yield return new WaitForSeconds(.2f); actor.StopMove();
            Check("left-native source keeps movement in the requested direction", root.transform.position.x < -.1f);
            actor.Face(1); yield return null;
            Check("left-native multipart actor turns as one group", primary.flipX && !part.flipX);
            Destroy(root);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            foreach (var trigger in FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None)) trigger.enabled = false;
            var sequences = FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None);
            foreach (var sequence in sequences) sequence.enabled = false;
            foreach (var auto in FindObjectsByType<ScrollPlayDirector>(FindObjectsSortMode.None)) auto.enabled = false;
            player = FindFirstObjectByType<DioramaPlayerController>(); body = player.GetComponent<Rigidbody>();
            director = GetComponent<Puzzle1Director>(); presentation = GetComponent<Puzzle1Presentation>();
            var display = DialogueDisplay.Instance;
            typeof(DialogueDisplay).GetMethod("CancelPlayback", Private).Invoke(display, null);
            display.ReleaseForSceneHandoff(); display.RestoreAfterSceneHandoff(false);
            player.SetStageMovementLock(false); player.ClearAutoMoveInput();
            Screen.SetResolution(1598, 898, false); yield return new WaitForSecondsRealtime(.3f);
            Screen.SetResolution(1600, 900, false); yield return new WaitForSecondsRealtime(.4f);
            yield return CheckActorFacing();
            Check("scene gate attached and initially locked", presentation.Locked);
            foreach (float z in new[] { 0f, 15f, -15f })
            {
                Place(new Vector3(94f, 0f, z)); player.SetAutoMoveInput(Vector2.right);
                yield return new WaitForSeconds(1.1f);
                Check("walk east blocked at z=" + z + " x=" + body.position.x.ToString("F3"), body.position.x < 95f);
            }
            Place(new Vector3(94f, 0f, 0f)); player.SetAutoMoveInput(new Vector2(1, .3f));
            yield return Key(UnityEngine.InputSystem.Key.Space); yield return new WaitForSeconds(.8f);
            Check("diagonal jump cannot bypass exit", body.position.x < 95f);
            player.ClearAutoMoveInput();
            yield return Shot("01-exit-locked");
            Place(new Vector3(79f, 0f, 0f));
            DongbaLanguage.Set(DongbaLocale.Chinese, false);
            yield return new WaitForSeconds(1.2f);
            Check("entering puzzle area does not reveal unread clues", presentation.HintAlpha < .01f && presentation.LearnedClueCount == 0 && presentation.Instruction.Length == 0);
            var pair = FindFirstObjectByType<EntangledSnakePair>(); pair.Split();
            yield return new WaitForSeconds(.8f);
            Check("both snakes active", director.WhiteSnake.gameObject.activeInHierarchy && director.BlackSnake.gameObject.activeInHierarchy);
            Check("not solved by separating snakes", presentation.Locked && !director.IsCompleted);
            Check("physical puzzle progress does not unlock dialogue clues", presentation.HintAlpha < .01f && presentation.LearnedClueCount == 0);
            yield return Shot("02-no-unread-hints");
            foreach (var cave in FindObjectsByType<CaveSlot>(FindObjectsSortMode.None))
                report.AppendLine("CAVE " + cave.RequiredSide + " position=" + cave.transform.position + " rim=" + cave.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Cave breathing ink edge"));
            var library = display.Library;
            display.SetCharactersPerSecond(20f);
            display.PlayRange(26, 26); yield return new WaitForSeconds(.15f);
            Check("typewriter start does not acquire a clue", display.IsTyping && presentation.LearnedClueCount == 0);
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            Check("Enter to reveal text is not Enter to acknowledge", display.IsPlaying && !display.IsTyping && presentation.LearnedClueCount == 0);
            display.HideDialogue(); yield return new WaitForSeconds(.25f);
            Check("cancelling an unacknowledged line does not acquire it", !display.HasCompletedLibraryLine(library, 26) && presentation.HintAlpha < .01f);
            display.SetCharactersPerSecond(1000f);
            display.PlayRange(35, 35); yield return new WaitForSeconds(.3f);
            display.PlayLine("Gao", "A passing thought."); yield return new WaitForSeconds(.8f);
            Check("replaced dialogue and ad-hoc lines do not acquire a library clue", presentation.LearnedClueCount == 0);
            display.HideDialogue();
            display.PlayRange(1, 1); yield return new WaitForSeconds(.4f);
            Check("stage commands are not completed spoken lines", !display.HasCompletedLibraryLine(library, 1));
            var otherLibrary = ScriptableObject.CreateInstance<DialogueLibrarySO>();
            typeof(DialogueLibrarySO).GetField("m_entries", Private).SetValue(otherLibrary, library.Entries.ToList());
            display.SetLibrary(otherLibrary);
            display.PlayRange(26, 26); yield return new WaitForSeconds(.4f); yield return Key(UnityEngine.InputSystem.Key.Enter);
            Check("same row from another library cannot reveal puzzle clues", display.HasCompletedLibraryLine(otherLibrary, 26) && presentation.LearnedClueCount == 0);
            display.SetLibrary(library); display.HideDialogue(); Destroy(otherLibrary);
            DongbaLanguage.Set(DongbaLocale.Chinese, false);
            display.SetCharactersPerSecond(1000f);
            display.PlayRange(26, 26); yield return new WaitForSeconds(.6f);
            Check("early rule in subtitle", display.SubtitleText.text.Contains("高处") && display.SubtitleText.text.Contains("低处"));
            Check("hint panel hidden during subtitle", presentation.HintAlpha < .01f);
            yield return Shot("04-early-rule-zh");
            yield return new WaitForSeconds(1.4f);
            Check("puzzle subtitle waits for Enter instead of timing out", display.IsPlaying);
            DongbaLanguage.Set(DongbaLocale.English, false); yield return null;
            Check("live language switch preserves readable emphasis", display.SubtitleText.text.Contains("#8A3E22") && display.SubtitleText.text.Contains("above"));
            DongbaLanguage.Set(DongbaLocale.Chinese, false); yield return null;
            yield return Key(UnityEngine.InputSystem.Key.Enter); yield return new WaitForSeconds(.3f);
            Check("enter advances subtitle", !display.IsPlaying);
            Check("clue is acquired but waits for lingering subtitle to disappear", presentation.LearnedClueCount == 1 && presentation.HintAlpha < .01f);
            yield return new WaitForSeconds(2.9f);
            Check("only acknowledged height clue appears", presentation.LearnedClueCount == 1 && presentation.HintAlpha > .98f && presentation.Instruction.Contains("白归高处") && !presentation.Instruction.Contains("相望"));
            yield return Shot("03-one-learned-clue");
            display.PlayRange(26, 26); yield return new WaitForSeconds(.3f); yield return Key(UnityEngine.InputSystem.Key.Enter);
            Check("replaying a line does not duplicate clues", presentation.LearnedClueCount == 1);
            var rockSequence = sequences.First(s => s.name.Contains("巨石"));
            rockSequence.enabled = true; rockSequence.Fire();
            float deadline = Time.realtimeSinceStartup + 15f;
            bool sawGaze=false, sawRedStones=false, sawSolution=false;
            while (Time.realtimeSinceStartup < deadline && !(sawGaze && !display.IsPlaying))
            {
                string line=display.SubtitleText.text;
                sawGaze |= line.Contains("目光");
                sawRedStones |= line.Contains("红色的石头") && line.Contains("派上用场");
                sawSolution |= line.Contains("隔在中间") || line.Contains("各自的洞");
                if(line.Contains("目光")) yield return Shot("05-observation-subtitle");
                yield return Key(UnityEngine.InputSystem.Key.Enter);
            }
            yield return new WaitForSeconds(3.1f);
            Check("rock conversation links the locked gaze to red stones without a full walkthrough", sawGaze && sawRedStones && !sawSolution && !display.IsPlaying);
            Check("gaze and red stone clue appear only after their conversation", presentation.LearnedClueCount == 2 && presentation.Instruction.Contains("相望") && presentation.Instruction.Contains("红石"));
            string remembered = presentation.Instruction;
            yield return Shot("06-learned-clues-zh-900");
            DongbaLanguage.Set(DongbaLocale.English, false); Screen.SetResolution(1280, 720, false);
            yield return new WaitForSeconds(.5f); yield return Shot("07-learned-clues-en-720");
            Check("language switch preserves knowledge and restrained wording", presentation.LearnedClueCount == 2 && presentation.Instruction.Contains("Neither snake") && presentation.Instruction.Contains("red stones") && !presentation.Instruction.Contains("Push"));
            Screen.SetResolution(1920, 1080, false); DongbaLanguage.Set(DongbaLocale.Chinese, false);
            yield return new WaitForSeconds(.5f); yield return Shot("08-learned-clues-zh-1080");
            rockSequence.enabled = false;
            // Exercise actual sight sensor and cave triggers with controlled initial positions.
            var blockers = FindObjectsByType<SightBlocker>(FindObjectsSortMode.None);
            foreach (var blocker in blockers) blocker.enabled = false;
            var rock = blockers.First(b => b.GetComponent<PushableRock>() != null);
            var rb = rock.GetComponent<Rigidbody>(); rb.isKinematic = true;
            var white = director.WhiteSnake; var black = director.BlackSnake;
            white.transform.DOKill(); black.transform.DOKill();
            PlaceSnake(white, new Vector3(74, 0, 2)); PlaceSnake(black, new Vector3(74, 0, -2));
            rock.transform.position = (white.SightPosition+black.SightPosition)*.5f; rb.position = rock.transform.position; rock.enabled = true;
            Physics.SyncTransforms(); yield return new WaitForSeconds(.3f);
            Check("red stone blocks sight and enables pushing", director.Stage == 2 && white.State == SnakeState.Blinded && black.State == SnakeState.Blinded);
            Check("blinding snakes does not replace clues with next-step instructions", presentation.Instruction == remembered);
            yield return Shot("06-sight-blocked");
            CaveSlot high = FindObjectsByType<CaveSlot>(FindObjectsSortMode.None).First(c => c.RequiredSide == SnakeSide.White);
            CaveSlot low = FindObjectsByType<CaveSlot>(FindObjectsSortMode.None).First(c => c.RequiredSide == SnakeSide.Black);
            Check("white cave higher than black cave", high.transform.position.z > low.transform.position.z);
            rock.enabled = false;
            // Wrong cave must not capture the white snake (actual trigger).
            director.enabled = false; white.EnterFree();
            PlaceSnake(white, low.GetComponent<Collider>().bounds.center);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.2f);
            Check("wrong cave rejects white snake", !white.IsCaptured);
            PlaceSnake(white, high.GetComponent<Collider>().bounds.center);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.4f);
            Check("correct cave captures white snake", white.IsCaptured);
            Check("one snake in cave does not open exit", presentation.Locked);
            director.enabled=true; yield return new WaitForSeconds(.15f);
            Check("restored sight releases snake", !white.IsCaptured && presentation.Locked);
            yield return new WaitForSeconds(1f);
            Check("snake finishes exiting animation", white.State != SnakeState.Exiting);
            // Set up both cave destinations and block the actual segment between them.
            director.enabled=false; white.transform.DOKill(); black.transform.DOKill();
            white.EnterFree(); black.EnterFree();
            PlaceSnake(white, new Vector3(74,0,2)); PlaceSnake(black, new Vector3(74,0,-2));
            Physics.SyncTransforms(); yield return new WaitForSeconds(.3f);
            var middle=(high.transform.position+low.transform.position)*.5f; middle.y=0;
            rock.transform.position=middle; rb.position=middle; rock.enabled=true;
            Place(new Vector3(88,0,0)); yield return new WaitForSeconds(.6f);
            if (RecordExitRequested) { recordingExit = true; Time.captureFramerate = 30; exitRecording = StartCoroutine(RecordExitFrames()); }
            PlaceSnake(white, high.GetComponent<Collider>().bounds.center);
            PlaceSnake(black, low.GetComponent<Collider>().bounds.center);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.5f);
            Check("both actual cave triggers capture", white.IsCaptured && black.IsCaptured);
            report.AppendLine("FINAL SNAKES " + white.State + " " + white.transform.position + " / " + black.State + " " + black.transform.position);
            director.enabled=true; yield return new WaitForSeconds(.4f);
            var exit = GetComponent<Puzzle1ExitJourney>();
            Check("solving begins a reveal without leaving the puzzle", director.IsCompleted && exit != null && !exit.HasExited && !exit.ExitReady && presentation.Locked);
            Check("completed puzzle hides remembered clues", presentation.HintAlpha < .01f);
            deadline = Time.time + 10;
            bool capturedOpening = false;
            while (exit != null && !exit.ExitReady && Time.time < deadline)
            {
                if (!capturedOpening && exit.StoneOpeningProgress > .2f && exit.StoneOpeningProgress < .5f) { capturedOpening = true; yield return Shot("07-stones-opening"); }
                yield return null;
            }
            Check("camera reveal opens the gate and visibly moves both original stones", exit != null && exit.ExitReady && !presentation.Locked && exit.UpperStoneTravel > 4f && exit.LowerStoneTravel > 4f);
            float stoppedX = body.position.x; yield return new WaitForSeconds(1.3f);
            Check("standing after solving does not auto-walk or auto-exit", Mathf.Abs(body.position.x-stoppedX) < .1f && exit != null && !exit.HasExited);
            yield return Shot("08-exit-awaits-player");
            ReportExitState("before walking");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));
            deadline = Time.time + 8; float nextDiagnostic = Time.time + .5f;
            while (exit != null && !exit.HasExited && Time.time < deadline)
            {
                keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));
                if (Time.time >= nextDiagnostic) { nextDiagnostic = Time.time + 1; ReportExitState("walking"); }
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            ReportExitState("after walking");
            Check("manual walk through the opened stones reaches the actual exit", body.position.x >= 101f && exit != null && exit.HasExited);
            Check("straight exit walk stays centered without side-wall displacement", Mathf.Abs(body.position.z) < .01f);
            yield return Shot("09-player-leaves-manually");
            if (recordingExit) { yield return new WaitForSeconds(.4f); recordingExit = false; yield return exitRecording; }
            File.WriteAllText(Folder + "/runtime.txt", report.ToString());
            InputSystem.RemoveDevice(keyboard); DOTween.KillAll(); Application.Quit();
        }
    }
}
