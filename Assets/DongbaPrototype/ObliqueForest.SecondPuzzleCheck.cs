using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Ivan.Puzzle;
using Ivan.Diorama;
using Ivan.Scroll;

namespace DongbaPrototype { public partial class ObliqueForest {
    static bool SecondResetCheck=>Array.IndexOf(Environment.GetCommandLineArgs(),"--second-puzzle-reset-check")>=0;
    static string SecondCheckFolder=>"Verification/second-puzzle-20261123/"+(SecondResetCheck?"reset-runtime":"runtime");
    bool secondChecksPassed=true;
    static object SecondField(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(target);
    void SecondAssert(bool ok,string message){secondChecksPassed&=ok;File.AppendAllText(SecondCheckFolder+"/checks.txt",(ok?"PASS ":"FAIL ")+message+"\n");Debug.Log((ok?"PASS ":"FAIL ")+message);}
    IEnumerator SecondShot(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(SecondCheckFolder+"/"+name+".png",image.EncodeToPNG());Destroy(image);}
    IEnumerator SecondWait(Func<bool> ready,float timeout){float until=Time.realtimeSinceStartup+timeout;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    IEnumerator SecondAdvance(Keyboard keyboard,float seconds){
        float end=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<end){
            DialogueDisplay.Instance?.CompleteCurrentLine();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.18f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return new WaitForSeconds(.08f);
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
    }
    // Set up the end of the puzzle, not its result: the actual controller must still
    // walk through the opened stones and invoke the scene's player-driven exit.
    void PreparePuzzleExitForVerification(PuzzleDirectorBase puzzle){
        foreach(var sequence in FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None))if(sequence.gameObject.scene==puzzle.gameObject.scene)sequence.enabled=false;
        foreach(var tripwire in FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None))if(tripwire.gameObject.scene==puzzle.gameObject.scene)tripwire.enabled=false;
        DialogueDisplay.Instance?.HideDialogue();
        var actor=FindFirstObjectByType<DioramaPlayerController>();
        if(actor!=null){actor.ClearAutoMoveInput();actor.SetStageMovementLock(false);var p=new Vector3(88,0,0);var body=actor.GetComponent<Rigidbody>();if(body!=null){body.linearVelocity=Vector3.zero;body.position=p;}actor.transform.position=p;Physics.SyncTransforms();}
        FindFirstObjectByType<ScrollPlayDirector>()?.SetMode(ScrollPlayDirector.PlayMode.Manual);
    }
    IEnumerator WalkSolvedPuzzleExitForVerification(PuzzleDirectorBase puzzle,Keyboard keyboard){
        var exit=puzzle.GetComponent<Puzzle1ExitJourney>();if(exit==null)yield break;
        yield return SecondWait(()=>exit.ExitReady,12);
        float deadline=Time.time+9;
        while(exit!=null&&!exit.HasExited&&Time.time<deadline){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return null;}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
    }
    IEnumerator CheckSecondPuzzle(){
        Directory.CreateDirectory(SecondCheckFolder);File.WriteAllText(SecondCheckFolder+"/checks.txt","");
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        yield return new WaitForSeconds(1);Screen.SetResolution(1598,898,false);yield return new WaitForSeconds(.2f);Screen.SetResolution(1600,900,false);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(30,0,0);FollowPlayerCamera(100);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return SecondWait(()=>reelPortalHost.InLevel,45);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        SecondAssert(reelPortalHost.InLevel&&SceneManager.GetActiveScene().name=="Beat_2_Cleansing_Impurity","First three pages still enter the two-snakes level");
        if(!reelPortalHost.InLevel){Application.Quit(1);yield break;}
        float firstProgress=JourneyReelProgress;
        var firstDirector=FindFirstObjectByType<PuzzleDirectorBase>();
        PreparePuzzleExitForVerification(firstDirector);yield return new WaitForSeconds(.35f);
        // Integration fixture emits solving; exit itself is still driven by actual keyboard walking.
        typeof(PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(firstDirector,null);
        var firstExit=firstDirector.GetComponent<Puzzle1ExitJourney>();
        yield return new WaitForSeconds(.4f);
        SecondAssert(reelPortalHost.InLevel&&firstExit!=null&&!firstExit.HasExited&&!firstExit.ExitReady,"Solving snakes keeps player in level and starts exit reveal");
        yield return SecondWait(()=>firstExit!=null&&firstExit.ExitReady,12);
        SecondAssert(firstExit!=null&&firstExit.ExitReady&&firstExit.UpperStoneTravel>4&&firstExit.LowerStoneTravel>4,"Camera shows both gate stones moving aside before control returns");
        yield return SecondShot("00-opened-stones-player-control");
        var firstPlayer=FindFirstObjectByType<DioramaPlayerController>();float stillX=firstPlayer.transform.position.x;
        yield return new WaitForSeconds(1);
        SecondAssert(reelPortalHost.InLevel&&!firstExit.HasExited&&Mathf.Abs(firstPlayer.transform.position.x-stillX)<.1f,"Standing after solve does not trigger auto-walk or return");
        yield return WalkSolvedPuzzleExitForVerification(firstDirector,keyboard);
        yield return SecondWait(()=>!reelPortalHost.IsBusy,25);
        SecondAssert(!reelPortalHost.InLevel&&JourneyReelNextDestinationIndex==1&&Mathf.Abs(JourneyReelProgress-firstProgress)<1,"Return preserves original reel position and advances route once");
        SecondAssert(journeyReelIndices[0]>=3,"Return shows continued original page, not page one");
        SecondAssert(Mathf.Abs(JourneyReelNextCheckpoint-JourneyReelReturnProgress-1810)<.1f,"Interlude is one page / 1810 pixels");
        yield return SecondShot("01-same-reel-return");yield return new WaitForSeconds(1);
        SecondAssert(!reelPortalHost.InLevel&&!reelPortalHost.IsBusy,"Standing still on reel does not auto-enter second level");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));yield return new WaitForSeconds(.35f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        SecondAssert(JourneyReelNextDestinationIndex==1&&!reelPortalHost.IsBusy,"Walking back cannot replay completed first checkpoint");
        float began=Time.realtimeSinceStartup;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
        yield return SecondWait(()=>reelPortalHost.IsBusy,12);float walkingSeconds=Time.realtimeSinceStartup-began;
        SecondAssert(walkingSeconds>1&&walkingSeconds<6,"Short player-controlled interlude: "+walkingSeconds.ToString("F2")+" seconds");
        yield return SecondWait(()=>reelPortalHost.InLevel,35);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        var puzzle=FindFirstObjectByType<Beat4Puzzle>();var controller=FindFirstObjectByType<DioramaPlayerController>();
        SecondAssert(reelPortalHost.InLevel&&SceneManager.GetActiveScene().name=="Beat_4 Bear and Pig"&&puzzle!=null,"Second checkpoint enters real bear / pig level");
        if(puzzle==null||controller==null){Application.Quit(1);yield break;}
        SecondAssert(puzzle.gameObject.scene==controller.gameObject.scene,"Puzzle and player belong to destination, not parked forest");
        SecondAssert(controller.BodyRenderer!=null&&!controller.BodyRenderer.forceRenderingOff&&controller.BodyRenderer.color.a>.95f,"New level actor is visible after camera handoff");
        SecondAssert(reelPortalHost.MaxReelUvError<.0001f,"Reel fades without being scaled");
        SecondAssert(reelPortalHost.LastPositionErrorPixels<3&&reelPortalHost.LastHeightErrorPixels<3,"New level actor position/size handoff is continuous");
        yield return SecondShot("02-new-level-entry");
        var display=DialogueDisplay.Instance;
        SecondAssert(display!=null&&display.Library.Count==29,"New scene owns the 29-line bilingual subtitle library");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return SecondWait(()=>display.IsPlaying,5);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        SecondAssert(display.IsPlaying,"Approaching animals triggers the real early encounter");
        display.CompleteCurrentLine();yield return new WaitForSeconds(.5f);string paused=display.SubtitleText.text;yield return new WaitForSeconds(1);
        SecondAssert(display.IsPlaying&&display.SubtitleText.text==paused,"New dialogue waits for Enter rather than skipping on timer");
        yield return SecondAdvance(keyboard,.28f);yield return SecondShot("03-new-dialogue-zh");
        DongbaLanguage.Set(DongbaLocale.English,false);display.CompleteCurrentLine();yield return new WaitForSeconds(.3f);
        SecondAssert(!string.IsNullOrWhiteSpace(display.SubtitleText.text)&&display.PaperSpeakerGlyph!=null,"English dialogue retains readable text and animal portrait");yield return SecondShot("04-new-dialogue-en");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return SecondAdvance(keyboard,5);
        if(SecondResetCheck){
            // Use the actual menu confirmation, with an independent runner surviving host disposal.
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());DongbaInterface.Instance.Confirm();
            yield return SecondWait(()=>reelPortalHost==null&&gameObject.activeInHierarchy&&!DongbaInterface.GameplayBlocked,20);
            SecondAssert(reelPortalHost==null&&!SceneManager.GetSceneByName("Beat_4 Bear and Pig").isLoaded&&!SceneManager.GetSceneByName("Beat_2_Cleansing_Impurity").isLoaded,"Restart menu unloads second puzzle and all parked levels");
            SecondAssert(gameObject.activeInHierarchy&&JourneyReelNextDestinationIndex==0&&JourneyReelNextScene=="Beat_2_Cleansing_Impurity"&&Time.timeScale==1,"Restart returns to first chapter and clears route state");
            yield return SecondShot("reset-to-opening");InputSystem.RemoveDevice(keyboard);Application.Quit(secondChecksPassed?0:1);yield break;
        }
        var rb=controller.GetComponent<Rigidbody>();rb.position=new Vector3(68,0,-7);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
        // Fixture places actual physics bodies into authored trigger volumes; no solved flags are changed.
        var axeTransform=(Transform)SecondField(puzzle,"m_axe");var zone=(Beat4TriggerZone)SecondField(puzzle,"m_nutZone");
        var axeBody=axeTransform.GetComponent<Rigidbody>();var nutSphere=zone.GetComponent<SphereCollider>();
        axeBody.position=zone.transform.TransformPoint(nutSphere.center);axeBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();
        yield return SecondAdvance(keyboard,3);
        var halves=(IList)SecondField(puzzle,"m_halves");
        SecondAssert(halves.Count==2,"Real axe/nut trigger splits fruit into two pushable halves");
        if(halves.Count!=2){yield return SecondShot("failed-fruit-split");Application.Quit(1);yield break;}
        foreach(var half in halves){
            var target=SecondField(half,"Target");var targetZone=(Beat4TriggerZone)SecondField(target,"Zone");var sphere=targetZone.GetComponent<SphereCollider>();
            var root=(Transform)SecondField(half,"Root");var foodBody=root.GetComponent<Rigidbody>();
            foodBody.position=targetZone.transform.TransformPoint(sphere.center);foodBody.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForSeconds(.25f);
        }
        yield return SecondWait(()=>puzzle.IsSolved,12);
        SecondAssert(puzzle.IsSolved&&!puzzle.IsJourneyCompleted&&reelPortalHost.InLevel,"Feeding both beasts opens path but does not prematurely leave before grain");
        yield return SecondShot("05-beasts-fed");
        var grainTripwire=(StorySequencePlayer)SecondField(puzzle,"m_grainSequence");
        rb.position=new Vector3(grainTripwire.transform.position.x+.35f,0,0);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
        yield return SecondAdvance(keyboard,6);
        var grain=(GameObject)SecondField(puzzle,"m_grainObject");
        SecondAssert((bool)SecondField(puzzle,"m_grainDelivered")&&grain.activeSelf,"Authored giveaway graph delivers physical golden grain");
        yield return new WaitForSeconds(2);yield return SecondShot("06-golden-grain");
        rb.position=grain.transform.position;Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForSeconds(.4f);
        SecondAssert((bool)SecondField(puzzle,"m_hasGrain")&&!puzzle.IsJourneyCompleted,"Pickup waits for closing dialogue before completing journey");
        float secondProgress=JourneyReelProgress;yield return SecondAdvance(keyboard,2);
        yield return SecondWait(()=>!reelPortalHost.IsBusy,25);
        SecondAssert(!reelPortalHost.InLevel&&JourneyReelNextDestinationIndex==2&&Mathf.Abs(JourneyReelProgress-secondProgress)<1,"Second completion returns to the next continuous reel position");
        SecondAssert(!SceneManager.GetSceneByName("Beat_4 Bear and Pig").isLoaded,"Completed second level unloads cleanly");
        yield return SecondShot("07-continued-reel-after-grain");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return new WaitForSeconds(1);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        SecondAssert(!reelPortalHost.IsBusy&&JourneyReelProgress>secondProgress+200,"Player continues reel without repeating second level");
        RestartRun();yield return null;
        SecondAssert(JourneyReelNextDestinationIndex==0&&JourneyReelNextScene=="Beat_2_Cleansing_Impurity","Restart clears both checkpoint completions");
        File.AppendAllText(SecondCheckFolder+"/checks.txt","First puzzle completion simulated. Second puzzle tested with physics-body placement fixtures plus actual triggers, dialogue and return; not a full manual solve.\n");
        InputSystem.RemoveDevice(keyboard);Application.Quit(secondChecksPassed?0:1);
    }
}}
namespace DongbaPrototype { public sealed class SecondPuzzleCheckRunner:MonoBehaviour {} }
