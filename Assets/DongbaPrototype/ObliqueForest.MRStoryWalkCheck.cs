#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Ivan.Diorama;
using Ivan.Scroll;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckMRStoryWalk(){
        const string folder="Verification/mr-story-walk";Directory.CreateDirectory(folder);var trace=new StringBuilder();bool passed=true;
        void Log(string s){trace.AppendLine(s);File.WriteAllText(folder+"/trace.txt",trace.ToString());Debug.Log("[MRStoryWalk] "+s);}
        void Check(bool ok,string text){passed&=ok;Log((ok?"PASS ":"FAIL ")+text);}
        object Field(object o,string n)=>o==null?null:o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(o);
        IEnumerator Shot(string name){var r=FindFirstObjectByType<MRJourneyCheckRunner>();r.View=cam;r.Actor=body;r.Pending=folder+"/"+name+".png";while(r.Pending!=null)yield return null;}
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        cam.transform.position=new Vector3(0,46,-55);cam.transform.LookAt(new Vector3(0,2,0));cam.fieldOfView=63;
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();player.position=new Vector3(31,0,0);xrSimStick=Vector2.right;
        float deadline=Time.realtimeSinceStartup+65;
        while((reelPortalHost==null||!reelPortalHost.InLevel)&&Time.realtimeSinceStartup<deadline)yield return null;
        xrSimStick=Vector2.zero;
        if(reelPortalHost==null||!reelPortalHost.InLevel){Log("FAIL initial puzzle handoff");UnityEditor.EditorApplication.Exit(1);yield break;}
        var actor=FindFirstObjectByType<DioramaPlayerController>();var director=FindFirstObjectByType<ScrollPlayDirector>();
        var display=DialogueDisplay.Instance;var fox=StageActorRegistry.Find("Fox");var foxImage=fox.GetComponentInChildren<SpriteRenderer>();
        MRPuzzleInput.Simulate=true;MRPuzzleInput.TestLeftValid=MRPuzzleInput.TestRightValid=true;MRPuzzleInput.TestMove=Vector2.right;bool pulse=false;float pressAt=0,logAt=0,still=0;Vector3 prev=actor.transform.position;bool foxGone=false,rightDropped=false;float rightDropX=0;deadline=Time.realtimeSinceStartup+240;
        Log("Native story active: no tripwire, sequence, command or auto-walk bypass.");
        while(Time.realtimeSinceStartup<deadline&&actor.transform.position.x<70){
            float now=Time.realtimeSinceStartup;
            if(display.AcceptsManualAdvance&&now>=pressAt){pulse=!pulse;MRPuzzleInput.TestA=pulse;pressAt=now+.35f;}else if(!display.AcceptsManualAdvance){pulse=false;MRPuzzleInput.TestA=false;}
            if(!foxGone&&!foxImage.enabled){foxGone=true;Log("Fox hidden at player "+actor.transform.position);yield return Shot("01-fox-departure");}
            if(!rightDropped&&director.HasFinished&&actor.transform.position.x>=49){
                rightDropped=true;rightDropX=actor.transform.position.x;MRPuzzleInput.TestRightValid=false;
                MRPuzzleInput.LatchDialogue();Log("Right controller lost immediately after automatic walking; left stick remains held.");
            }
            var pos=actor.transform.position;bool isStill=(pos-prev).sqrMagnitude<.00001f;prev=pos;
            still=isStill&&!display.IsPlaying?still+Time.unscaledDeltaTime:0;
            if(now>=logAt){logAt=now+1;Log("x="+pos.x.ToString("F2")+" z="+pos.z.ToString("F2")+" auto="+Field(actor,"m_useAutoMoveInput")+" lock="+Field(actor,"m_stageMovementLocked")+" end="+director.HasFinished+" hold="+Field(director,"m_holdForDialogue")+" dialogue="+display.IsPlaying+" accepts="+display.AcceptsManualAdvance+" blocked="+DialogueAdvanceInput.GameplayInputBlocked+" menu="+DongbaInterface.GameplayBlocked+" input="+MRPuzzleInput.Move+" focus="+reelPortalHost.MRStage.ContentFocus);}
            if(still>7){
                foreach(var col in Physics.OverlapSphere(pos+Vector3.up*.3f,.7f))Log("near collider="+col.name+" trigger="+col.isTrigger+" bounds="+col.bounds+" parent="+col.transform.parent?.name);
                Log("FAIL stuck after story: "+pos);yield return Shot("stuck");UnityEditor.EditorApplication.Exit(1);yield break;
            }
            yield return null;
        }
        MRPuzzleInput.TestA=false;MRPuzzleInput.TestMove=Vector2.zero;
        bool success=foxGone&&director.HasFinished&&actor.transform.position.x>=70;
        Check(success,"Real fox story, clue dialogue and auto-to-manual handoff reach snake area x="+actor.transform.position.x);
        Check(rightDropped&&actor.transform.position.x>rightDropX+15&&!MRPuzzleInput.DialogueHold,"Left stick continues after right controller loss; stale dialogue latch releases");
        yield return Shot("02-after-fox-walking");
        yield return new WaitForSeconds(1.2f);
        var stage=reelPortalHost.MRStage;var visual=stage.FindVisual(actor.BodyRenderer.transform);
        var stationaryFocus=stage.ContentFocus;MRPuzzleInput.TestMove=Vector2.left;
        yield return new WaitForSeconds(1.5f);MRPuzzleInput.TestMove=Vector2.zero;yield return new WaitForSeconds(.8f);
        Check(Vector3.Distance(stationaryFocus,stage.ContentFocus)<.2f,"Turning back into paper interior leaves scenery stationary");
        yield return Shot("03-walk-start");
        var start=actor.transform.position;var visualStart=visual.position;var focusStart=stage.ContentFocus;
        MRPuzzleInput.TestMove=Vector2.left;yield return new WaitForSeconds(1);
        MRPuzzleInput.TestMove=Vector2.zero;yield return null;yield return null;
        float travel=start.x-actor.transform.position.x;
        Check(travel>2&&visualStart.x-visual.position.x>travel*2.2f&&Vector3.Distance(focusStart,stage.ContentFocus)<.08f,"Player visibly walks across fixed paper instead of running on the spot");
        yield return Shot("04-walk-end");
        yield return new WaitForSeconds(.8f);var stoppedFocus=stage.ContentFocus;yield return new WaitForSeconds(.8f);
        Check(Vector3.Distance(stoppedFocus,stage.ContentFocus)<.02f,"Stopping does not recenter or drift the scene");
        Check((stage.MapPoint(actor.transform.position)).magnitude<42,"Player stays within tabletop; sizes unchanged");
        MRPuzzleInput.TestRightValid=true;MRPuzzleInput.TestA=true;yield return null;yield return null;
        Check((float)Field(actor,"m_jumpAge")<0,"Right-hand reconnect with A held does not create a ghost jump");
        MRPuzzleInput.TestA=false;yield return null;yield return null;MRPuzzleInput.TestA=true;yield return null;yield return null;
        Check((float)Field(actor,"m_jumpAge")>=0,"Fresh A press works after right-hand reconnect");
        MRPuzzleInput.TestA=false;yield return new WaitForSeconds(1);
        MRPuzzleInput.TestLeftValid=false;MRPuzzleInput.TestMove=Vector2.right;MRPuzzleInput.TestTrigger=true;yield return null;yield return null;
        Check(MRPuzzleInput.Move==Vector2.zero&&(float)Field(actor,"m_attackAge")>=0,"Missing left hand stops movement without disabling right-hand actions");
        MRPuzzleInput.TestTrigger=false;MRPuzzleInput.TestLeftValid=true;yield return null;yield return null;
        Check(MRPuzzleInput.Move.x>.9f,"Left joystick recovers independently on reconnect");
        MRPuzzleInput.TestMove=Vector2.right;deadline=Time.realtimeSinceStartup+65;pressAt=0;pulse=false;still=0;prev=actor.transform.position;
        var pair=FindFirstObjectByType<Ivan.Puzzle.EntangledSnakePair>();
        while(actor.transform.position.x<83&&Time.realtimeSinceStartup<deadline){
            float now=Time.realtimeSinceStartup;
            if(display.AcceptsManualAdvance&&now>=pressAt){pulse=!pulse;MRPuzzleInput.TestA=pulse;pressAt=now+.35f;}
            else if(!display.AcceptsManualAdvance){pulse=false;MRPuzzleInput.TestA=false;}
            bool isStill=(actor.transform.position-prev).sqrMagnitude<.00001f;prev=actor.transform.position;
            still=isStill&&!display.IsPlaying?still+Time.unscaledDeltaTime:0;
            if(now>=logAt){logAt=now+1;Log("Snake approach x="+prev.x+" lock="+Field(actor,"m_stageMovementLocked")+" dialogue="+display.IsPlaying+" accepts="+display.AcceptsManualAdvance+" input="+MRPuzzleInput.Move);}
            if(still>6){foreach(var c in Physics.OverlapSphere(prev+Vector3.up*.3f,.8f))Log("Obstacle "+c.name+" trigger="+c.isTrigger+" bounds="+c.bounds);break;}
            yield return null;
        }
        MRPuzzleInput.TestMove=Vector2.zero;MRPuzzleInput.TestA=false;
        // The newly separated snakes and movable rocks have real footprints. Do not
        // assert that holding right can walk through them: steer around the obstacle.
        if(actor.transform.position.x<83&&!display.IsPlaying&&!(bool)Field(actor,"m_stageMovementLocked")){
            var beforeDetour=actor.transform.position;
            MRPuzzleInput.TestMove=Vector2.down;yield return new WaitForSeconds(.9f);
            Log("Steered around physical footprint: "+beforeDetour+" -> "+actor.transform.position);
            MRPuzzleInput.TestMove=Vector2.right;deadline=Time.realtimeSinceStartup+8;
            while(actor.transform.position.x<83&&Time.realtimeSinceStartup<deadline)yield return null;
            MRPuzzleInput.TestMove=Vector2.zero;
        }
        Check(actor.transform.position.x>=83&&!(bool)Field(actor,"m_stageMovementLocked"),"Real snake dialogue releases movement; joystick can steer around native footprints");
        Check(pair!=null&&(bool)Field(pair,"m_hasSplit"),"Authored story event splits snakes without bypassing cue");
        yield return Shot("05-native-snake-encounter");
        Log("Editor uses real first-puzzle narrative and simulated device availability. No worn-headset claim.");
        UnityEditor.EditorApplication.Exit(passed?0:1);
    }
}}
#endif
