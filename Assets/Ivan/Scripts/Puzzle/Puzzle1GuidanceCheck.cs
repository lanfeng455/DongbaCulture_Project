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
        const string Folder = "Verification/puzzle-guidance-20261122";
        readonly StringBuilder report = new();
        Keyboard keyboard;
        DioramaPlayerController player;
        Rigidbody body;
        Puzzle1Director director;
        Puzzle1Presentation presentation;
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
            Check("puzzle HUD visible", presentation.HintAlpha > .98f);
            var pair = FindFirstObjectByType<EntangledSnakePair>(); pair.Split();
            yield return new WaitForSeconds(.8f);
            Check("both snakes active", director.WhiteSnake.gameObject.activeInHierarchy && director.BlackSnake.gameObject.activeInHierarchy);
            Check("not solved by separating snakes", presentation.Locked && !director.IsCompleted);
            yield return Shot("02-hints-zh");
            foreach (var cave in FindObjectsByType<CaveSlot>(FindObjectsSortMode.None))
                report.AppendLine("CAVE " + cave.RequiredSide + " position=" + cave.transform.position + " rim=" + cave.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Cave breathing ink edge"));
            DongbaLanguage.Set(DongbaLocale.English, false); yield return new WaitForSeconds(.4f);
            Screen.SetResolution(1280, 720, false); yield return new WaitForSeconds(.5f); yield return Shot("03-hints-en-720");
            Screen.SetResolution(1920, 1080, false); yield return new WaitForSeconds(.5f);
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
            var rockSequence = sequences.First(s => s.name.Contains("巨石"));
            rockSequence.enabled = true; rockSequence.Fire();
            float deadline = Time.realtimeSinceStartup + 15f;
            bool sawGaze=false, sawRed=false, sawRule=false;
            while (Time.realtimeSinceStartup < deadline && !(sawRule && !display.IsPlaying))
            {
                string line=display.SubtitleText.text;
                sawGaze |= line.Contains("紧盯"); sawRed |= line.Contains("红色"); sawRule |= line.Contains("白的在");
                if(line.Contains("红色"))yield return Shot("05-red-rock-subtitle");
                yield return Key(UnityEngine.InputSystem.Key.Enter);
            }
            Check("rock sequence plays gaze, red rocks, white-high black-low", sawGaze && sawRed && sawRule);
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
            PlaceSnake(white, high.GetComponent<Collider>().bounds.center);
            PlaceSnake(black, low.GetComponent<Collider>().bounds.center);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.5f);
            Check("both actual cave triggers capture", white.IsCaptured && black.IsCaptured);
            report.AppendLine("FINAL SNAKES " + white.State + " " + white.transform.position + " / " + black.State + " " + black.transform.position);
            director.enabled=true; yield return new WaitForSeconds(.4f);
            Check("only complete puzzle opens exit", director.IsCompleted && !presentation.Locked);
            Place(new Vector3(94,0,0)); player.SetAutoMoveInput(Vector2.right); yield return new WaitForSeconds(1.1f);
            Check("completed exit permits walking east", body.position.x > 95f);
            player.ClearAutoMoveInput(); yield return Shot("07-exit-open");
            File.WriteAllText(Folder + "/runtime.txt", report.ToString());
            InputSystem.RemoveDevice(keyboard); DOTween.KillAll(); Application.Quit();
        }
    }
}
