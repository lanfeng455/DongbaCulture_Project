using System.Collections;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Ivan.Puzzle;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckJourneyReel(){
        PrepareJourneyDestination();
        bool rescueEnding=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--rescue-ending-check")>=0;string folder=Path.GetFullPath(rescueEnding?"Verification/rescue-ending-handoff":"Verification/journey-reel");Directory.CreateDirectory(folder);var log=new StringBuilder();bool record=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--reel-record")>=0;if(record){Time.captureFramerate=30;reelCheckRecording=true;reelPortalHost.StartCoroutine(RecordJourneyReel(folder));}
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(30,0,0);FollowPlayerCamera(100);
        var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();bool blended=false,third=false;float age=0;
        while(!reelPortalHost.InLevel&&age<35){
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--portal-transition-reset-check")>=0&&reelPortalHost.IsBusy){
                yield return new WaitForSeconds(.8f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return PressStoryKey(Key.R);
                float resetWait=0;while(reelPortalHost.IsBusy&&resetWait<15){resetWait+=Time.unscaledDeltaTime;yield return null;}
                log.AppendLine("R during camera handoff restores start="+(!reelPortalHost.IsBusy&&gameObject.activeInHierarchy&&fatherIntroActive&&Balance==0&&Vector3.Distance(Feet,ForestSpawn)<.01f));
                log.AppendLine("No puzzle left loaded after interrupted entry="+!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Beat_2_Cleansing_Impurity").isLoaded);
                File.WriteAllText(folder+"/transition-reset-checks.txt",log.ToString());Application.Quit();yield break;
            }
            if(Feet.x>34&&!blended){yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/forest-to-reel.png");blended=true;}
            if(journeyReelData!=null&&Feet.x>JourneyReelStart+journeyReelData.beats[2].x/JourneyReelPixelsPerUnit&&!third){yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/third-page.png");third=true;}
            age+=Time.unscaledDeltaTime;yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        log.AppendLine("Walking progressively reveals reel="+(blended&&third));log.AppendLine("First three pages reach real level="+reelPortalHost.InLevel);log.AppendLine("Reel textures above paper layer="+(journeyReelPanels[0].texture!=null&&journeyReelCanvas.sortingOrder>-100));
        if(reelPortalHost.InLevel){
            var liveActor=FindFirstObjectByType<Ivan.Diorama.DioramaPlayerController>();
            log.AppendLine("Preloaded level actor stays visible="+(liveActor!=null&&liveActor.BodyRenderer.color.a>.95f&&!liveActor.BodyRenderer.forceRenderingOff));
            log.AppendLine("Preloaded subtitles stay enabled="+(DialogueDisplay.Instance!=null&&DialogueDisplay.Instance.isActiveAndEnabled));
            log.AppendLine("Held walk moves real puzzle actor during entry="+(reelPortalHost.EntryTravel>1)+"; meters="+reelPortalHost.EntryTravel);
            log.AppendLine("Entry matches real actor within 2 pixels="+(reelPortalHost.LastPositionErrorPixels<2&&reelPortalHost.LastHeightErrorPixels<2)+"; position="+reelPortalHost.LastPositionErrorPixels+"; height="+reelPortalHost.LastHeightErrorPixels);
            yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();WriteJourneyFrame(folder+"/actual-level.png");yield return null;
            var director=FindFirstObjectByType<PuzzleDirectorBase>();log.AppendLine("Real puzzle and own subtitles active="+(director!=null&&DialogueDisplay.Instance!=null));
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--portal-restart-check")>=0){
                yield return PressStoryKey(Key.R);age=0;while(reelPortalHost.IsBusy&&age<15){age+=Time.unscaledDeltaTime;yield return null;}
                log.AppendLine("R in puzzle restores forest opening="+(!reelPortalHost.InLevel&&!reelPortalHost.IsBusy&&gameObject.activeInHierarchy&&fatherIntroActive&&Balance==0&&Vector3.Distance(Feet,ForestSpawn)<.01f));
                log.AppendLine("R unloads puzzle and releases overlay="+(!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Beat_2_Cleansing_Impurity").isLoaded&&!playerImage.forceRenderingOff));
                PrepareJourneyDestination();yield return new WaitForSeconds(3);log.AppendLine("Destination can preload again after reset="+(reelPortalHost!=null&&!reelPortalHost.IsBusy));
                File.WriteAllText(folder+"/restart-checks.txt",log.ToString());Application.Quit();yield break;
            }
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--puzzle-art-check")>=0){yield return CheckPuzzleArt();yield break;}
            var feetBefore=Feet;
            // Integration-only simulation: emit the real completion event, not a claim of solving the puzzle.
            if(rescueEnding){
                reelPortalHost.ReturnToForestForRestoration();age=0;while(reelPortalHost.IsBusy&&age<20){age+=Time.unscaledDeltaTime;yield return null;}
                log.AppendLine("Final rescue callback enters restoration="+IsRestorationEnding);
                log.AppendLine("Father remains absent after final return="+!storyFatherRoot.gameObject.activeSelf);
                log.AppendLine("Forest scene and controls restored="+(gameObject.activeInHierarchy&&enabled));
                File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();yield break;
            }
            if(director!=null){PreparePuzzleExitForVerification(director);yield return new WaitForSeconds(.35f);typeof(PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);yield return WalkSolvedPuzzleExitForVerification(director,keyboard);}
            age=0;while(reelPortalHost.IsBusy&&age<20){age+=Time.unscaledDeltaTime;yield return null;}
            yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/returned-to-reel.png");
            log.AppendLine("Return matches real actor within 2 pixels="+(reelPortalHost.LastPositionErrorPixels<2&&reelPortalHost.LastHeightErrorPixels<2)+"; position="+reelPortalHost.LastPositionErrorPixels+"; height="+reelPortalHost.LastHeightErrorPixels);
            log.AppendLine("Completion signal restores same forest and progress="+(!reelPortalHost.InLevel&&journeyStage==JourneyStage.EastJourney&&Vector3.Distance(feetBefore,Feet)<.01f&&journeyReelPassedPortal));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return new WaitForSeconds(1);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            log.AppendLine("Reel stays full-screen at fixed scale during both handoffs="+(reelPortalHost.MaxReelUvError<.0001f));
            log.AppendLine("Walking resumes without repeating portal="+(!reelPortalHost.IsBusy&&Feet.x>feetBefore.x+2));
        }
        RestartRun();yield return new WaitForEndOfFrame();log.AppendLine("Restart hides reel and resets checkpoint="+(!journeyReelPassedPortal&&journeyReelGroup.alpha==0&&fatherIntroActive));
        reelCheckRecording=false;Time.captureFramerate=0;File.WriteAllText(folder+"/checks.txt",log.ToString());yield return new WaitForEndOfFrame();Application.Quit();
    }
    void RenderJourneyFrame(RenderTexture rt,Texture2D output){
        var activeCamera=Camera.main;if(activeCamera==null)activeCamera=cam;var target=activeCamera.targetTexture;
        var overlays=new System.Collections.Generic.List<UnityEngine.Canvas>();
        foreach(var canvas in FindObjectsByType<UnityEngine.Canvas>(FindObjectsSortMode.None))if(canvas.isActiveAndEnabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay){overlays.Add(canvas);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=activeCamera;canvas.planeDistance=1;}
        Canvas.ForceUpdateCanvases();activeCamera.targetTexture=rt;activeCamera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;output.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);output.Apply();RenderTexture.active=previous;activeCamera.targetTexture=target;
        foreach(var canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
    }
    void WriteJourneyFrame(string path){var rt=new RenderTexture(1280,720,24);var output=new Texture2D(1280,720,TextureFormat.RGB24,false);RenderJourneyFrame(rt,output);File.WriteAllBytes(path,output.EncodeToPNG());rt.Release();Destroy(rt);Destroy(output);}
    bool reelCheckRecording;
    IEnumerator RecordJourneyReel(string folder){
        Directory.CreateDirectory(folder+"/frames");int frame=0;var rt=new RenderTexture(Screen.width,Screen.height,24);var output=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        while(reelCheckRecording&&frame<1500){
            yield return new WaitForEndOfFrame();RenderJourneyFrame(rt,output);File.WriteAllBytes(folder+"/frames/"+(frame++).ToString("D4")+".png",output.EncodeToPNG());
        }
        rt.Release();Destroy(rt);Destroy(output);
    }

}}
