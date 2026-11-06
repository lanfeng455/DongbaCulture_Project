using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    enum JourneyStage { Off, Hunting, BoarPass, Invitation, Boar, Leaving, ReturnForKnife, Captured, WaitingAtHearth, Realization, EastJourney, ScrollTransition, ScrollWalk }
    JourneyStage journeyStage;Transform storyShu;bool shuPreviewCaptured; Vector3 captureCameraCenter;bool carryStarted;Vector3 carryOffset;
    Material storyBoarInk; Being storyBoar; Transform droppedKnife; Text journeyObjective;
    float fatherJumpAge=-1,boarAidReady,boarStun;Vector3 fatherJumpFrom,fatherJumpTo;
    Vector3 knifeDropPoint;float returnWait,captureBeatAge;bool captureEventSent;
    static readonly Vector3 BoarClearing=new Vector3(23,0,0);
    static readonly Vector3 DeparturePoint=new Vector3(-7,0,0);
    void ResetStoryJourney(){
        ResetForestSubtitles();
        bool returningFromScroll=scrollRelocated||journeyStage==JourneyStage.ScrollTransition;ResetAfterCapture();if(returningFromScroll){player.position=ForestSpawn;lastDryPosition=ForestSpawn;jumpAge=attackAge=-1;FollowPlayerCamera(100);}
        if(storyShu!=null){Destroy(storyShu.gameObject);storyShu=null;}shuPreviewCaptured=false;carryStarted=false;
        if(storyBoar!=null){Destroy(storyBoar.root.gameObject);storyBoar=null;}
        if(droppedKnife!=null){Destroy(droppedKnife.gameObject);droppedKnife=null;}
        journeyStage=StoryModeEnabled?JourneyStage.Hunting:JourneyStage.Off;fatherHuntTarget=null;fatherSwingAge=-1;fatherNextSwing=0;fatherSwingHit=false;fatherAIFacing=0;invitationAge=0;captureEventSent=false;returnWait=captureBeatAge=0;fatherJumpAge=-1;boarAidReady=0;boarStun=0;
        EnsureStoryHome();boarPassComplete=false;boarPassAge=0;fatherIntroActive=StoryModeEnabled;fatherIntroLine=0;if(storyFatherKnifeImage!=null)storyFatherKnifeImage.enabled=false;
        if(StoryModeEnabled&&Vector3.Distance(Feet,ForestSpawn)<3f){storyFatherRoot.position=FatherHomeStart;storyFather.home=FatherHomeStart;facing=-1;heading=Vector3.left;}
        if(StoryModeEnabled)Tell("Father: Gather wood and hunt with me in the western woods.");
    }
    void SpawnStoryBoar(){
        AddDictionaryAnimal(BoarClearing,9);storyBoar=animals[animals.Count-1];animals.RemoveAt(animals.Count-1);
        storyBoar.root.name="Story large boar / existing pig pictograph adaptation";storyBoar.health=6;
        if(storyBoarInk==null)storyBoarInk=new Material(Resources.Load<Shader>("StoryBoarInk"));storyBoar.renderer.sharedMaterial=storyBoarInk;
        storyBoar.initialScale*=2.15f;storyBoar.art.localScale=storyBoar.initialScale;storyBoar.protectedUntil=float.PositiveInfinity;
    }
    void UpdateStoryJourney(float dt){
        if(!StoryModeEnabled)return;
        if(journeyStage==JourneyStage.Off)ResetStoryJourney();
        if(fatherIntroActive||ManualStoryDialogueActive)return;
        if(UpdateAfterCaptureStory(dt))return;
        if(journeyStage==JourneyStage.Hunting&&Balance<=StoryCaptureBalanceThreshold){SpawnStoryBoar();journeyStage=JourneyStage.BoarPass;fatherHuntTarget=null;fatherSwingAge=-1;boarPassStart=new Vector3(Mathf.Min(Feet.x-12,RiverX(0)-10),0,0);storyBoar.root.position=boarPassStart;Tell("A great boar bursts out of the western woods!");}
        if(journeyStage==JourneyStage.BoarPass){UpdateBoarPass(dt);return;}
        if(journeyStage==JourneyStage.Invitation){
            if(Vector3.Distance(storyFatherRoot.position,Feet)>2.2f)MoveStoryFatherToward(Feet,3f,dt);
            else{BeginManualStoryDialogue(new[]{"Father: Did you see that boar? It crossed the river!", "Father: Let us hunt it together."},()=>journeyStage=JourneyStage.Boar);return;}
        }
        if(journeyStage==JourneyStage.Boar&&storyBoar!=null&&storyBoar.health>0){
            float distance=Vector3.Distance(Feet,storyBoar.root.position);
            if(distance<7)storyBoar.angry=true;
            boarStun=Mathf.Max(0,boarStun-dt);
            if(storyFatherRoot!=null&&Vector3.Distance(storyFatherRoot.position,storyBoar.root.position)<5&&clock>=boarAidReady&&distance<6){boarAidReady=clock+7;boarStun=1.6f;Tell("Father: I have its attention — strike now!");}
            if(storyBoar.angry&&boarStun<=0)UpdateAnimalEcology(storyBoar,dt,jumpAge<0?0:1);
        }
        if(journeyStage==JourneyStage.Boar&&storyBoar!=null&&storyBoar.health<=0){
            journeyStage=JourneyStage.Leaving;knifeDropPoint=storyBoar.root.position;
            var source=Get("father_knife");droppedKnife=Billboard("Lost father knife",null,source,.20f);droppedKnife.position=knifeDropPoint;
            droppedKnife.GetComponent<SpriteRenderer>().sortingOrder=100-Mathf.RoundToInt(knifeDropPoint.z*10);droppedKnife.gameObject.SetActive(false);
            if(storyFatherKnifeImage!=null)storyFatherKnifeImage.enabled=false;
            BeginManualStoryDialogue(new[]{"Father: We have enough. Let us cross back over the river and go home."});return;
        }
        if(journeyStage==JourneyStage.Leaving&&Feet.x< -4&&storyFatherRoot.position.x<RiverX(storyFatherRoot.position.z)-1.05f&&Vector3.Distance(Feet,knifeDropPoint)>11){
            journeyStage=JourneyStage.ReturnForKnife;storyOpeningState=StoryOpeningState.CaptureBeat;returnWait=4.2f;
            droppedKnife.gameObject.SetActive(true);EnsureStoryShu();storyFather.route.Clear();storyFather.navRefresh=clock;
            BeginManualStoryDialogue(new[]{"Father: Wait a moment...", "Father: My knife... I left it in the clearing.", "Father: Stay here. I will go back for it."},()=>returnWait=0);return;
        }
        if(journeyStage!=JourneyStage.ReturnForKnife)return;
        if(Vector3.Distance(storyFatherRoot.position,knifeDropPoint)>.9f&&captureBeatAge==0){
            MoveStoryFatherToward(knifeDropPoint,2.5f,dt);
            return;
        }
        EnsureStoryShu();
        if(captureBeatAge==0)captureCameraCenter=Vector3.Lerp(storyFatherRoot.position,storyShu.position,.5f);
        captureBeatAge+=dt;Tell("Shu seizes Father and carries him away!");
        var shuImage=storyShu.GetComponent<SpriteRenderer>();shuImage.color=Color.white;
        if(droppedKnife!=null)droppedKnife.gameObject.SetActive(false);
        if(captureBeatAge<.55f){storyFatherRoot.position=Vector3.Lerp(storyFatherRoot.position,storyShu.position+new Vector3(-.85f,0,0),1-Mathf.Exp(-dt*9));}
        else{
            if(!carryStarted){carryStarted=true;carryOffset=storyFatherRoot.position-storyShu.position;}
            float speed=Mathf.Lerp(6,25,Mathf.SmoothStep(0,1,(captureBeatAge-.55f)/.65f));Vector3 travel=new Vector3(1,0,.18f).normalized*speed*dt;
            storyShu.position+=travel;storyFatherRoot.position=storyShu.position+carryOffset;
        }
        shuImage.sortingOrder=100-Mathf.RoundToInt(storyShu.position.z*10);storyFatherImage.color=Color.white;storyFatherImage.sortingOrder=shuImage.sortingOrder+1;
        if(captureBeatAge>.7f&&!shuPreviewCaptured){shuPreviewCaptured=true;if(Array.IndexOf(Environment.GetCommandLineArgs(),"--story-journey-check")>=0)CaptureSceneToTexture("Verification/story-journey/shu-capture.png");}
        if(carryStarted&&SpriteOutsideStoryView(shuImage)&&SpriteOutsideStoryView(storyFatherImage)){
            storyShu.gameObject.SetActive(false);storyFatherRoot.gameObject.SetActive(false);journeyStage=JourneyStage.Captured;storyOpeningState=StoryOpeningState.Captured;
            if(!captureEventSent){captureEventSent=true;FatherCaptured?.Invoke();}BeginManualStoryDialogue(new[]{"Father has been taken by Shu."});
        }
    }
    void EnsureStoryShu(){if(storyShu!=null)return;storyShu=Billboard("Shu / dictionary PDF409 printed337",null,Get("shu_glyph"),2.6f);storyShu.position=knifeDropPoint+new Vector3(1.5f,0,.5f);var r=storyShu.GetComponent<SpriteRenderer>();r.color=Color.white;r.sortingOrder=100-Mathf.RoundToInt(storyShu.position.z*10);}
    bool SpriteOutsideStoryView(SpriteRenderer r){var b=r.bounds;float minX=10,maxX=-10,minY=10,maxY=-10;for(int i=0;i<8;i++){var p=cam.WorldToViewportPoint(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}return maxX<-.12f||minX>1.12f||maxY<-.12f||minY>1.12f;}

    static readonly float[] FatherSteeringAngles={0,35,-35,70,-70,90,-90};
    bool StoryFatherStepClear(Vector3 p){if(storyHearth!=null&&Mathf.Abs(p.x-HearthPosition.x)<.62f&&Mathf.Abs(p.z-HearthPosition.z)<.4f)return false;
        if(storyHome!=null&&Mathf.Abs(p.x-storyHome.position.x)<1.2f&&Mathf.Abs(p.z-storyHome.position.z)<.65f)return false;
        if(River(p)||Mathf.Abs(p.x)>ForestHalfWidth-.8f||Mathf.Abs(p.z)>ForestHalfDepth-.8f)return false;
        foreach(var t in plants)if(t.tree&&t.health>0&&Vector3.Distance(t.home,p)<.8f)return false;
        foreach(var a in animals)if(a.health>0&&a.migrationState!=2&&Vector3.Distance(a.root.position,p)<.65f)return false;
        return true;
    }
    void MoveStoryFatherToward(Vector3 goal,float speed,float dt){
        if(fatherJumpAge>=0)return; // The central tick completes a jump even if AI changes its goal.
        var start=storyFatherRoot.position;float river=RiverX(start.z);
        if((start.x-river)*(goal.x-RiverX(goal.z))<0){
            float sign=start.x<river?1:-1;var takeoff=new Vector3(river-sign*1.4f,0,start.z);var landing=new Vector3(river+sign*1.4f,0,start.z);
            if(Vector3.Distance(start,takeoff)<.25f&&StoryFatherStepClear(landing)){fatherJumpFrom=start;fatherJumpTo=landing;fatherJumpAge=0;return;}goal=takeoff;
        }
        Vector3 direction=goal-storyFatherRoot.position;direction.y=0;if(direction.magnitude<.15f)return;
        float step=Mathf.Min(direction.magnitude,speed*Mathf.Min(dt,.1f));
        foreach(float angle in FatherSteeringAngles){Vector3 trial=storyFatherRoot.position+Quaternion.Euler(0,angle,0)*direction.normalized*step;if(StoryFatherWalkingSegmentClear(storyFatherRoot.position,trial)){storyFatherRoot.position=trial;return;}}
    }
    void StrikeStoryBoar(){
        if(journeyStage!=JourneyStage.Boar||storyBoar==null||storyBoar.health<=0)return;
        Vector3 d=storyBoar.root.position-Feet;if(d.magnitude>=2.8f||Vector3.Dot(d.normalized,heading)<=.15f)return;
        ApplyEcologyHit(storyBoar);storyBoar.angry=true;Tell("Great boar: "+storyBoar.health+" / 6");if(storyBoar.health<=0)storyBoar.root.gameObject.SetActive(false);
    }
    void UpdateStoryObjectiveHUD(){
        EnsureQuietObjective();if(journeyObjective==null)return;
        journeyObjective.text=StoryModeEnabled?QuietStoryObjective():"";DrawBoarTargetMarker();DrawFatherIntro();
    }
    IEnumerator CheckStoryJourney(){
        yield return new WaitForSeconds(2);SetStoryModeEnabled(true);RestartRun();Directory.CreateDirectory("Verification/story-journey");StartCoroutine(RecordStoryCapture());
        File.WriteAllText("Verification/story-journey/checks.txt","Opening hunts first="+(journeyStage==JourneyStage.Hunting)+"\n");
        int priorDamage=damageCount;yield return new WaitForSeconds(1.2f);File.AppendAllText("Verification/story-journey/checks.txt","Opening waits with hidden knife="+(fatherIntroActive&&fatherHuntTarget==null&&damageCount==priorDamage&&!storyFatherKnifeImage.enabled)+"\n");CaptureSceneToTexture("Verification/story-journey/opening-dialogue.png");for(int intro=0;intro<3;intro++)AdvanceFatherIntro();File.AppendAllText("Verification/story-journey/checks.txt","Conversation unlocks hunting="+(!fatherIntroActive)+"\n");
        var sample=animals[0];int oldDamage=damageCount;ApplyEcologyHit(sample);bool oneAlive=sample.health==1;ApplyEcologyHit(sample);File.AppendAllText("Verification/story-journey/checks.txt","Ordinary animal two hits and loss two="+(oneAlive&&sample.health==0&&damageCount-oldDamage==2)+"\n");RestartRun();for(int intro=0;intro<3;intro++)AdvanceFatherIntro();
        var fatherVictim=animals[1];int beforeFatherDamage=damageCount;ApplyFatherLightHit(fatherVictim);bool fatherFirst=fatherVictim.health==2;ApplyFatherLightHit(fatherVictim);bool fatherSecond=fatherVictim.health==1;ApplyFatherLightHit(fatherVictim);bool fatherThird=fatherVictim.health==1;ApplyFatherLightHit(fatherVictim);File.AppendAllText("Verification/story-journey/checks.txt","Father needs four strikes="+(fatherFirst&&fatherSecond&&fatherThird&&fatherVictim.health==0&&damageCount-beforeFatherDamage==2)+"\n");RestartRun();for(int intro=0;intro<3;intro++)AdvanceFatherIntro();
        var aiVictim=animals[2];foreach(var a in animals)a.protectedUntil=float.PositiveInfinity;aiVictim.protectedUntil=0;aiVictim.root.position=storyFatherRoot.position+Vector3.right*1.15f;float aiTime=0;bool sawBlade=false;while(aiTime<5){yield return null;aiTime+=Time.deltaTime;sawBlade|=storyFatherKnifeImage.enabled;}yield return new WaitForSeconds(.7f);File.AppendAllText("Verification/story-journey/checks.txt","Knife appears during attack and hides after="+(sawBlade&&!storyFatherKnifeImage.enabled)+"\n");File.AppendAllText("Verification/story-journey/checks.txt","Autonomous father selects swings and kills="+(aiVictim.health==0&&aiVictim.fatherHalfHits==4)+"; halfHits="+aiVictim.fatherHalfHits+"\n");RestartRun();for(int intro=0;intro<3;intro++)AdvanceFatherIntro();
        float rx=RiverX(0);storyFatherRoot.position=new Vector3(rx-1.4f,0,0);player.position=new Vector3(rx+4,0,0);yield return new WaitForSeconds(6);File.AppendAllText("Verification/story-journey/checks.txt","Father follows by jumping across river="+(storyFatherRoot.position.x>rx+1.05f)+"; father="+storyFatherRoot.position+"\n");RestartRun();for(int intro=0;intro<3;intro++)AdvanceFatherIntro();
        while(Balance>StoryCaptureBalanceThreshold)RegisterDamage();float passWait=0;while(journeyStage!=JourneyStage.Boar&&passWait<20){passWait+=Time.deltaTime;if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return null;}File.AppendAllText("Verification/story-journey/checks.txt","Boar passed west to east before invitation="+boarPassComplete+"\n");
        File.AppendAllText("Verification/story-journey/checks.txt","Threshold spawns boar before capture="+(journeyStage==JourneyStage.Boar&&storyOpeningState==StoryOpeningState.Following)+"\n");
        player.position=BoarClearing+Vector3.left*1.8f;heading=Vector3.right;storyFatherRoot.position=BoarClearing+Vector3.back*2;yield return null;File.AppendAllText("Verification/story-journey/checks.txt","Father support creates opening="+(boarStun>0)+"\n");FollowPlayerCamera(100);CaptureSceneToTexture("Verification/story-journey/boar.png");
        for(int i=0;i<6;i++)StrikeStoryBoar();yield return null;
        File.AppendAllText("Verification/story-journey/checks.txt","Boar death unlocks departure="+(journeyStage==JourneyStage.Leaving)+"\n");
        player.position=DeparturePoint;FollowPlayerCamera(100);float homeWait=0;while(journeyStage==JourneyStage.Leaving&&homeWait<30){homeWait+=Time.deltaTime;if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return null;}File.AppendAllText("Verification/story-journey/checks.txt","Father actually returns to west bank="+(storyFatherRoot.position.x<RiverX(storyFatherRoot.position.z)-1.05f)+"; wait="+homeWait+"\n");
        File.AppendAllText("Verification/story-journey/checks.txt","Departure triggers lost knife="+(journeyStage==JourneyStage.ReturnForKnife)+"\n");
        int events=0;FatherCaptured+=()=>events++;float elapsed=0;
        while(journeyStage!=JourneyStage.Captured&&elapsed<45){elapsed+=Time.deltaTime;if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return null;}
        yield return new WaitForSeconds(.15f);File.AppendAllText("Verification/story-journey/checks.txt","Father returns and captured="+(journeyStage==JourneyStage.Captured)+"; seconds="+elapsed+"; father="+storyFatherRoot.position+"; knife="+knifeDropPoint+"; route="+HasAnimalRoute(storyFather)+"; event="+events+"\n");CaptureSceneToTexture("Verification/story-journey/captured.png");
        RestartRun();File.AppendAllText("Verification/story-journey/checks.txt","Restart restores opening and hidden knife="+(journeyStage==JourneyStage.Hunting&&fatherIntroActive&&storyBoar==null&&droppedKnife==null&&!storyFatherKnifeImage.enabled)+"\n");Application.Quit();
    }
    IEnumerator RecordStoryCapture(){while(journeyStage!=JourneyStage.ReturnForKnife)yield return null;Time.captureFramerate=30;Directory.CreateDirectory("Verification/story-journey/frames");int n=0;while(journeyStage==JourneyStage.ReturnForKnife&&n<900){yield return new WaitForEndOfFrame();CaptureSceneToTexture("Verification/story-journey/frames/"+(n++).ToString("D4")+".png",960,540);}Time.captureFramerate=0;File.AppendAllText("Verification/story-journey/checks.txt","Shu present before capture="+(storyShu!=null)+"; both fully outside before hiding="+(storyShu!=null&&SpriteOutsideStoryView(storyShu.GetComponent<SpriteRenderer>())&&SpriteOutsideStoryView(storyFatherImage))+"; recordedFrames="+n+"\n");}

}}
