using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    const string FeelFolder="Verification/character-feel-20261124/runtime";
    int feelFailures,feelFrame;
    void FeelCheck(bool ok,string label){if(!ok)feelFailures++;File.AppendAllText(FeelFolder+"/checks.txt",(ok?"PASS ":"FAIL ")+label+"\n");}
    IEnumerator FeelWait(float seconds,bool captureFrames=false){
        float elapsed=0;while(elapsed<seconds){yield return new WaitForEndOfFrame();elapsed+=Time.deltaTime;if(captureFrames)CaptureSceneToTexture(FeelFolder+"/frames/"+(feelFrame++).ToString("D4")+".png",1280,720);}
    }
    IEnumerator CheckCharacterFeel(){
        Directory.CreateDirectory(FeelFolder+"/frames");File.WriteAllText(FeelFolder+"/checks.txt","");
        yield return new WaitForSeconds(1);DongbaInterface.Instance?.Close(false);SetStoryModeEnabled(false);RestartRun();ResetForestSubtitles();
        tonePreviewFreezeAnimals=true;player.position=ForestSpawn;FollowPlayerCamera(100);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>("CharacterFeelCheck");InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
        yield return FeelWait(.6f);
        // Shared curve invariants run at both supported frame rates, independent of the motor.
        foreach(int rate in new[]{30,60}){
            var motion=new PaperActorMotion();motion.Reset();var p=Vector3.zero;float maxStep=0;
            for(int i=0;i<rate;i++){p.x+=4f/rate;var pose=motion.Tick(1f/rate,p,1,-1,-1);maxStep=Mathf.Max(maxStep,pose.lift);}
            FeelCheck(maxStep>.035f,"Distance-driven steps visible at "+rate+" FPS");
            for(int i=0;i<rate;i++)motion.Tick(1f/rate,p,1,-1,-1);
            FeelCheck(motion.MovingWeight<.001f&&motion.Current.lift<.001f,"Stopping / blocked movement settles at "+rate+" FPS");
            var left=new PaperActorMotion();var right=new PaperActorMotion();left.Reset();right.Reset();float mirror=0;
            for(int i=0;i<rate;i++){float age=i/(float)rate;mirror=Mathf.Max(mirror,Mathf.Abs(left.Tick(1f/rate,Vector3.zero,-1,-1,age).roll+right.Tick(1f/rate,Vector3.zero,1,-1,age).roll));}
            FeelCheck(mirror<.0001f,"Attack body reactions mirror exactly at "+rate+" FPS");
        }
        var glyph=playerImage.sprite;var feet=Feet;
        yield return FeelWait(.5f);FeelCheck(Vector3.Distance(feet,Feet)<.00001f&&Mathf.Abs(body.position.x-Feet.x)<.00001f,"Idle cannot move the physical or visual foot sideways");
        bool preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--character-feel-preview")>=0;
        Time.captureFramerate=preview?30:0;
        yield return FeelWait(.5f,preview);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return FeelWait(.9f,preview);
        FeelCheck(playerMotion.MovingWeight>.7f&&Vector3.Distance(feet,Feet)>2,"Actual keyboard walking drives paper steps");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));yield return FeelWait(.7f,preview);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return FeelWait(.6f,preview);
        FeelCheck(playerMotion.MovingWeight<.01f,"Release movement settles without skating");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space,Key.D));yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
        float peak=0,minY=2,until=Time.time+1.3f;
        while(Time.time<until){yield return new WaitForEndOfFrame();peak=Mathf.Max(peak,body.localPosition.y);if(jumpAge<0)minY=Mathf.Min(minY,playerMotion.Current.scale.y);if(preview)CaptureSceneToTexture(FeelFolder+"/frames/"+(feelFrame++).ToString("D4")+".png",1280,720);}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return FeelWait(.45f,preview);
        FeelCheck(peak>1.35f&&minY<.97f&&playerMotion.Current.scale.y>.99f,"Jump retains height; landing compresses then restores proportions");
        // Use one existing animal, keeping the visible environment intact.
        var victim=animals[0];foreach(var a in animals)if(a!=victim&&Vector3.Distance(a.root.position,Feet)<3)a.root.position+=Vector3.forward*4;
        float gripError=0;
        for(int side=-1;side<=1;side+=2){
            victim.root.gameObject.SetActive(true);victim.health=2;victim.age=2;victim.art.localScale=victim.initialScale;victim.root.position=Feet+Vector3.right*(side*.9f);
            facing=side;heading=Vector3.right*side;BeginAxeAttack();until=Time.time+.75f;
            while(Time.time<until){yield return new WaitForEndOfFrame();gripError=Mathf.Max(gripError,Vector3.Distance(axe.position,body.TransformPoint(PlayerHandLocalPosition())));if(preview)CaptureSceneToTexture(FeelFolder+"/frames/"+(feelFrame++).ToString("D4")+".png",1280,720);}
            FeelCheck(victim.health==1,(side<0?"Left":"Right")+" posed axe hits the real glyph once");
        }
        FeelCheck(gripError<.0001f,"Weapon grip stays attached throughout body windup / follow-through");
        victim.health=2;victim.careReady=0;victim.age=0;victim.root.position=Feet+Vector3.right*.9f;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        float bow=0;until=Time.time+.8f;
        while(Time.time<until){yield return new WaitForEndOfFrame();bow=Mathf.Max(bow,Mathf.Abs(playerMotion.Current.roll));if(preview)CaptureSceneToTexture(FeelFolder+"/frames/"+(feelFrame++).ToString("D4")+".png",1280,720);}
        FeelCheck(victim.age==1&&bow>3,"Successful feeding produces a brief care bow without changing growth rules");
        victim.angry=true;victim.cooldown=0;damageCooldown=0;victim.root.position=Feet+Vector3.right*.5f;int hp=health;
        UpdateAnimalEcology(victim,.016f,0);victim.angry=false;float flinch=0;until=Time.time+.6f;
        while(Time.time<until){yield return new WaitForEndOfFrame();flinch=Mathf.Max(flinch,Mathf.Abs(playerMotion.Current.roll));if(preview)CaptureSceneToTexture(FeelFolder+"/frames/"+(feelFrame++).ToString("D4")+".png",1280,720);}
        FeelCheck(health==hp-1&&flinch>4,"Actual animal damage triggers one recoil");
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();journeyStage=JourneyStage.Boar;
        tonePreviewFreezeAnimals=true;storyFatherRoot.position=Feet+Vector3.left*3;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return FeelWait(1.2f,preview);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return FeelWait(.4f,preview);
        ReactForestSpeech("Father");yield return FeelWait(.8f,preview);
        FeelCheck(storyFatherImage.sprite!=null&&Mathf.Abs(storyFatherArt.position.x-storyFatherRoot.position.x)<.0001f,"Father keeps his ground anchor during follow and speech");
        FeelCheck(playerImage.sprite==glyph,"All motions preserve the original protagonist glyph");
        Time.captureFramerate=0;yield return FeelWait(.5f);
        float elapsed=0;int count=0;while(elapsed<3){yield return null;elapsed+=Time.unscaledDeltaTime;count++;}
        File.AppendAllText(FeelFolder+"/checks.txt","Uncaptured PC FPS="+(count/elapsed).ToString("F1")+"\n");
        SetStoryModeEnabled(false);RestartRun();yield return FeelWait(.5f);
        FeelCheck(playerMotion.MovingWeight<.001f&&Mathf.Abs(playerMotion.Current.roll)<.001f,"Restart clears all transient action reactions");
        InputSystem.RemoveDevice(keyboard);
        Application.Quit(feelFailures==0?0:1);
    }
}}
