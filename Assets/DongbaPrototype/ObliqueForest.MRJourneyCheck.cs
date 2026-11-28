#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ivan.Diorama;
using Ivan.Puzzle;
using Ivan.Scroll;
namespace DongbaPrototype {
[DefaultExecutionOrder(32000)]
public sealed class MRJourneyCheckRunner:MonoBehaviour{
    public string Pending;public Camera View;public Transform Actor;public Vector3 CapturedActor;
    void LateUpdate(){
        if(Pending==null||View==null)return;CapturedActor=Actor!=null?Actor.position:Vector3.zero;
        var target=View.targetTexture;var rt=RenderTexture.GetTemporary(1600,1000,24);View.targetTexture=rt;View.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var im=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        im.ReadPixels(new Rect(0,0,1600,1000),0,0);im.Apply();File.WriteAllBytes(Pending,im.EncodeToPNG());Destroy(im);
        RenderTexture.active=previous;View.targetTexture=target;RenderTexture.ReleaseTemporary(rt);Pending=null;
    }
}
public partial class ObliqueForest {
    IEnumerator CheckMRJourney(){
        const string folder="Verification/mr-journey";Directory.CreateDirectory(folder);
        var report=new StringBuilder();bool passed=true;
        void Check(bool ok,string text){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);File.WriteAllText(folder+"/checks.txt",report.ToString());Debug.Log((ok?"PASS ":"FAIL ")+text);}
        IEnumerator Wait(Func<bool> condition,float timeout=30){float until=Time.realtimeSinceStartup+timeout;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
        IEnumerator Shot(string name){var runner=FindFirstObjectByType<MRJourneyCheckRunner>();runner.View=cam;runner.Actor=body;runner.Pending=folder+"/"+name+".png";while(runner.Pending!=null)yield return null;}
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        cam.transform.position=new Vector3(0,46,-55);cam.transform.LookAt(new Vector3(0,2,0));cam.fieldOfView=63;
        yield return VerifyLivingWeather(Check);
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        BeginHearthWaiting();yield return new WaitForSeconds(HearthHalfDaySeconds*.52f);
        Check(xrDayNightSlot!=null&&storyTimeSymbols.transform.parent==xrDayNightSlot&&!xrDayNightSlot.IsChildOf(cam.transform),"Sun/moon hang in the fixed UI side, outside tracked eye hierarchy");
        yield return Shot("day-hanging-ui");yield return new WaitForSeconds(HearthHalfDaySeconds);
        Check(nightAmount>.7f&&storyMoonImage.gameObject.activeInHierarchy&&Shader.GetGlobalFloat("_DongbaMRNight")>.7f,"Moon appears before tabletop darkening; room and UI are not shaded");
        yield return Shot("night-hanging-ui");yield return Wait(()=>journeyStage==JourneyStage.Realization,18);
        Check(waitingBeatsShown==6,"Three days and three nights retain six sequential beats");
        ResetForestSubtitles();nightAmount=0;Shader.SetGlobalFloat("_DongbaMRNight",0);
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(31,0,0);
        xrSimStick=Vector2.right;yield return new WaitForSeconds(.9f);xrSimStick=Vector2.zero;yield return null;
        Check(journeyReelCanvas!=null&&journeyReelCanvas.renderMode==RenderMode.WorldSpace,"Reel is a world-space paper stage, never a plane at the eyes");
        Vector3 fixedPage=journeyReelRoot.position;Quaternion eyeRotation=cam.transform.rotation;
        cam.transform.rotation*=Quaternion.Euler(0,25,0);yield return null;
        Check(Vector3.Distance(journeyReelRoot.position,fixedPage)<.001f&&!journeyReelRoot.IsChildOf(cam.transform),"Head turn cannot move the reel");cam.transform.rotation=eyeRotation;
        Check(Vector3.Distance(body.position,cam.transform.position)>xrUnits*.8f,"Paper actor stays at stage depth, outside near-eye space");
        yield return Shot("01-reel");xrSimStick=Vector2.right;
        yield return Wait(()=>reelPortalHost!=null&&reelPortalHost.InLevel,50);xrSimStick=Vector2.zero;
        Check(reelPortalHost!=null&&reelPortalHost.InLevel,"Joystick reel walking enters first puzzle");
        if(reelPortalHost==null||!reelPortalHost.InLevel){UnityEditor.EditorApplication.Exit(1);yield break;}
        MRPuzzleInput.Simulate=true;yield return null;yield return null;
        var origin=cam.transform.root;Vector3 originPosition=origin.position;Quaternion originRotation=origin.rotation;
        for(int index=0;index<2;index++){
            var host=reelPortalHost;var stage=host.MRStage;
            Check(stage!=null&&stage.Surface.renderMode==RenderMode.WorldSpace&&!stage.Surface.transform.IsChildOf(cam.transform),"Level "+index+" uses fixed world-space surface");
            Check(!stage.ViewCamera.enabled&&stage.SpatialVisualCount>20&&cam.targetTexture==null,"Level "+index+" uses spatial geometry, no camera texture or replacement eye camera");
            Check(cam.gameObject.activeInHierarchy&&origin.gameObject.activeInHierarchy&&XRTrackingControlsCamera,"Tracking origin survives scene handoff "+index);
            var stagePosition=stage.Surface.transform.position;cam.transform.rotation*=Quaternion.Euler(0,18,0);yield return null;
            Check(Vector3.Distance(stage.Surface.transform.position,stagePosition)<.001f,"Level "+index+" does not follow head turns");cam.transform.rotation=eyeRotation;
            var actor=FindFirstObjectByType<DioramaPlayerController>();
            foreach(var sequence in FindObjectsByType<StorySequencePlayer>(FindObjectsSortMode.None))sequence.enabled=false;
            foreach(var wire in FindObjectsByType<StoryTripwire>(FindObjectsSortMode.None))wire.enabled=false;
            FindFirstObjectByType<ScrollPlayDirector>()?.SetMode(ScrollPlayDirector.PlayMode.Manual);
            actor.ClearAutoMoveInput();actor.SetStageMovementLock(false);DialogueDisplay.Instance?.HideDialogue();yield return null;
            var rb=actor.GetComponent<Rigidbody>();Vector3 start=actor.transform.position;
            MRPuzzleInput.TestMove=Vector2.right;yield return new WaitForSeconds(.5f);MRPuzzleInput.TestMove=Vector2.zero;yield return null;
            Check(actor.transform.position.x>start.x+.2f,"Level "+index+" moves using controller without keyboard emulation");
            MRPuzzleInput.TestA=true;yield return null;yield return null;
            Check((float)SecondField(actor,"m_jumpAge")>=0,"Level "+index+" A starts original jump");MRPuzzleInput.TestA=false;yield return new WaitForSeconds(1);
            MRPuzzleInput.TestTrigger=true;yield return null;yield return null;
            Check((float)SecondField(actor,"m_attackAge")>=0,"Level "+index+" trigger starts original axe");MRPuzzleInput.TestTrigger=false;yield return new WaitForSeconds(.7f);
            var dialogue=DialogueDisplay.Instance;dialogue.PlayBilingualLine("高勒趣","Where is Father?","父亲在哪儿？",true);yield return new WaitForSeconds(.2f);dialogue.CompleteCurrentLine();yield return null;
            MRPuzzleInput.TestA=true;yield return null;yield return null;
            Check((float)SecondField(actor,"m_jumpAge")<0,"Level "+index+" dialogue A does not leak into jump");MRPuzzleInput.TestA=false;yield return null;dialogue.HideDialogue();
            bool oldLanguage=DongbaLanguage.Chinese;MRPuzzleInput.TestLanguage=true;yield return null;yield return null;MRPuzzleInput.TestLanguage=false;yield return null;
            Check(DongbaLanguage.Chinese!=oldLanguage,"Level "+index+" right-stick press switches language");
            int overlayCount=0;foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isActiveAndEnabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay){bool visible=false;foreach(var g in canvas.GetComponentsInChildren<Graphic>())if(g.isActiveAndEnabled&&g.color.a>.01f){visible=true;break;}if(visible){overlayCount++;Debug.Log("Unexpected overlay: "+canvas.name);}}
            Check(overlayCount==0,"No enabled screen overlay remains in level "+index+" (count="+overlayCount+")");
            Check(actor.BodyRenderer.forceRenderingOff&&stage.FindVisual(actor.BodyRenderer.transform)!=null,"Native physics actor is hidden and represented by a spatial mirror "+index);
            yield return Shot(index==0?"02-snakes-stage":"04-bear-pig-stage");
            var visual=stage.FindVisual(actor.BodyRenderer.transform);
            Check(visual!=null&&Mathf.Abs(Vector3.Dot(visual.forward,Vector3.up))<.05f,"Player glyph stands upright on the spatial ground "+index);
            Check(stage.GetComponentsInChildren<RawImage>().Length==0,"Puzzle contains no flat camera-image panel "+index);
            Debug.Log("[SpatialCheck] level="+index+" focus="+stage.ContentFocus+" actor="+actor.transform.position+" visual="+visual.position+" objects="+stage.SpatialVisualCount+" sourceColor="+actor.BodyRenderer.color+" sourceMaterial="+actor.BodyRenderer.sharedMaterial.name+" height="+actor.BodyRenderer.bounds.size);
            // Mirroring must also follow runtime reparenting of carried props.
            var prop=new GameObject("MR carried-prop fixture");SceneManager.MoveGameObjectToScene(prop,actor.gameObject.scene);
            var propImage=prop.AddComponent<SpriteRenderer>();propImage.sprite=actor.BodyRenderer.sprite;propImage.enabled=false;
            yield return null;yield return null;prop.transform.SetParent(actor.BodyRenderer.transform,false);yield return null;yield return null;
            Check(stage.FindVisual(prop.transform)!=null&&stage.FindVisual(prop.transform).parent==visual,"Carried prop mirrors follow native reparenting "+index);Destroy(prop);
            var eyePoint=cam.transform.position;cam.transform.position+=Vector3.right*8;yield return Shot("parallax-"+index);cam.transform.position=eyePoint;

            MRPuzzleInput.TestB=true;yield return null;yield return null;MRPuzzleInput.TestB=false;yield return null;
            Check(MRPuzzleInput.MenuOpen,"B opens shared paper controls in level "+index);yield return Shot("controls-"+index);
            MRPuzzleInput.TestB=true;yield return null;yield return null;MRPuzzleInput.TestB=false;yield return null;
            Check(!MRPuzzleInput.MenuOpen,"B closes controls in level "+index);
            foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(c.name=="ScrollUnrollCanvas")Check(!c.enabled,"Desktop scroll edge cannot become a vertical pole in MR");
            // X executes the existing near-object interaction (story cue gate bypassed only in fixture).
            var pair=FindFirstObjectByType<EntangledSnakePair>();
            if(pair!=null){
                pair.GetType().GetField("m_waitForStoryCue",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pair,false);
                var pos=pair.transform.position+Vector3.left*1.65f;pos.y=0;rb.position=pos;rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return null;
                MRPuzzleInput.TestX=true;yield return null;yield return null;MRPuzzleInput.TestX=false;yield return new WaitForSeconds(1.5f);
                Check((bool)SecondField(pair,"m_hasSplit"),"X invokes actual paired-snakes interaction");
                yield return Shot("snakes-interaction");
                var ui=new StringBuilder();foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)){
                    ui.AppendLine(c.name+" enabled="+c.enabled+" pos="+c.transform.position+" scale="+c.transform.lossyScale);
                    foreach(var label in c.GetComponentsInChildren<Text>())ui.AppendLine("  "+label.name+" "+label.text+" size="+label.rectTransform.rect.size+" pos="+label.transform.position);
                }File.WriteAllText(folder+"/spatial-ui.txt",ui.ToString());

            }
            // Push a real movable prop with the original Rigidbody motor.
            var rock=FindFirstObjectByType<PushableRock>();
            if(rock!=null){
                Vector3 initial=rock.transform.position;var collider=rock.GetComponent<DioramaObstacle>().Collider;
                rb.position=new Vector3(initial.x-collider.bounds.extents.x-.3f,0,initial.z);rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                MRPuzzleInput.TestMove=Vector2.right;yield return new WaitForSeconds(1.2f);MRPuzzleInput.TestMove=Vector2.zero;
                Check(rock.transform.position.x>initial.x+.06f,"Controller pushes an actual puzzle rock using unchanged collisions");
            }
            float before=JourneyReelProgress;
            if(index==0){
                var puzzle=FindFirstObjectByType<PuzzleDirectorBase>();PreparePuzzleExitForVerification(puzzle);
                typeof(PuzzleDirectorBase).GetMethod("CompletePuzzle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(puzzle,null);
                var exit=puzzle.GetComponent<Puzzle1ExitJourney>();yield return Wait(()=>exit.ExitReady,16);
                MRPuzzleInput.TestMove=Vector2.right;yield return Wait(()=>!host.IsBusy,18);MRPuzzleInput.TestMove=Vector2.zero;
                Check(!host.InLevel&&!host.IsBusy,"After solve, controller walks through real exit before return");
            }else{
                // Existing PC second-puzzle regression exercises actual fruit/grain triggers.
                typeof(ReelPortalHost).GetMethod("OnCompleted",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(host,null);yield return Wait(()=>!host.IsBusy,15);
                Check(!host.InLevel,"Second puzzle completion uses MR return path");
            }
            Check(!XRPuzzleActive&&!MRPuzzleInput.Active&&cam.CompareTag("MainCamera")&&cam.targetTexture==null,"Return releases input and restores eye ownership "+index);
            Check(Vector3.Distance(origin.position,originPosition)<.001f&&Quaternion.Angle(origin.rotation,originRotation)<.001f,"Scene transitions never move the physical origin "+index);
            Check(JourneyReelNextDestinationIndex==index+1&&Mathf.Abs(JourneyReelProgress-before)<1,"Reel returns to preserved progress after level "+index);
            if(index==0){yield return Shot("03-continuing-reel");Check(Mathf.Abs(FindFirstObjectByType<MRJourneyCheckRunner>().CapturedActor.x)<XRStageWidth*.5f&&Mathf.Abs(FindFirstObjectByType<MRJourneyCheckRunner>().CapturedActor.z-ForestHalfDepth)<2,"Returned paper actor stays on fixed reel after all LateUpdate poses");xrSimStick=Vector2.right;yield return Wait(()=>host.InLevel,30);xrSimStick=Vector2.zero;Check(host.InLevel&&host.MRStage!=null&&SceneManager.GetActiveScene().name=="Beat_4 Bear and Pig","Short continuing reel enters second authored level");if(!host.InLevel)break;}
        }
        MRPuzzleInput.Simulate=false;yield return Shot("05-returned-reel");
        report.AppendLine("Editor simulated headset/controllers. First solve state injected; actual first exit walked. Second completion injected; native PC regression covers fruit/grain mechanics. No worn-headset claim.");File.WriteAllText(folder+"/checks.txt",report.ToString());
        UnityEditor.EditorApplication.Exit(passed?0:1);
    }
}}
#endif
