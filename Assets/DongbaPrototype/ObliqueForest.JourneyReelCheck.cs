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
        string folder=Path.GetFullPath("Verification/journey-reel");Directory.CreateDirectory(folder);var log=new StringBuilder();bool record=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--reel-record")>=0;if(record){Time.captureFramerate=30;reelCheckRecording=true;reelPortalHost.StartCoroutine(RecordJourneyReel(folder));}
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(30,0,0);FollowPlayerCamera(100);
        var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();bool blended=false,third=false;float age=0;
        while(!reelPortalHost.InLevel&&age<35){
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
            if(Feet.x>34&&!blended){yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/forest-to-reel.png");blended=true;}
            if(Feet.x>65&&!third){yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/third-page.png");third=true;}
            age+=Time.unscaledDeltaTime;yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        log.AppendLine("Walking progressively reveals reel="+(blended&&third));log.AppendLine("First three pages reach real level="+reelPortalHost.InLevel);log.AppendLine("Reel textures above paper layer="+(journeyReelPanels[0].texture!=null&&journeyReelCanvas.sortingOrder>-100));
        if(reelPortalHost.InLevel){
            yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();WriteJourneyFrame(folder+"/actual-level.png");yield return null;
            var director=FindFirstObjectByType<PuzzleDirectorBase>();log.AppendLine("Real puzzle and own subtitles active="+(director!=null&&DialogueDisplay.Instance!=null));
            var feetBefore=Feet;
            // Integration-only simulation: emit the real completion event, not a claim of solving the puzzle.
            if(director!=null)typeof(PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);
            age=0;while(reelPortalHost.IsBusy&&age<20){age+=Time.unscaledDeltaTime;yield return null;}
            yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/returned-to-reel.png");
            log.AppendLine("Completion signal restores same forest and progress="+(!reelPortalHost.InLevel&&journeyStage==JourneyStage.EastJourney&&Vector3.Distance(feetBefore,Feet)<.01f&&journeyReelPassedPortal));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return new WaitForSeconds(1);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
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
        Directory.CreateDirectory(folder+"/frames");int frame=0;var rt=new RenderTexture(960,540,24);var output=new Texture2D(960,540,TextureFormat.RGB24,false);
        while(reelCheckRecording&&frame<1500){
            yield return new WaitForEndOfFrame();RenderJourneyFrame(rt,output);File.WriteAllBytes(folder+"/frames/"+(frame++).ToString("D4")+".png",output.EncodeToPNG());
        }
        rt.Release();Destroy(rt);Destroy(output);
    }

}}
