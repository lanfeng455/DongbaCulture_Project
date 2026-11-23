using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    // Opt-in comparison build. The ordinary build retains its existing story timing.
    bool? narrativePreviewEnabled;
    bool NarrativePreview {get {if(!narrativePreviewEnabled.HasValue)narrativePreviewEnabled=Array.IndexOf(Environment.GetCommandLineArgs(),"--narrative-preview")>=0||File.Exists(Path.Combine(Application.dataPath,"../narrative-preview.flag"));return narrativePreviewEnabled.Value;}}
    enum NarrativeBeat {None,MissingKnife,Depart,Silence,EmptyClearing,AwaitWildlife,RenewedForest}
    NarrativeBeat narrativeBeat;float narrativeBeatAge;bool narrativeClosingDialogue;
    bool NarrativeInputLocked=>(GraphRunning||(narrativeBeat!=NarrativeBeat.None&&narrativeBeat!=NarrativeBeat.AwaitWildlife)||narrativeClosingDialogue);
    void ResetNarrativePreview(){ResetDialogueGraph();narrativeBeat=NarrativeBeat.None;narrativeBeatAge=0;narrativeClosingDialogue=false;if(storyFatherArt!=null)storyFatherArt.rotation=billboard;}
    void StartNarrativeBeat(NarrativeBeat beat){narrativeBeat=beat;narrativeBeatAge=0;notice="";noticeTime=0;}
    void BeginMissingKnifeBeat(){if(!BeginForestDialogueGraph(ForestDialogueGraph.Entry.MissingKnife))StartNarrativeBeat(NarrativeBeat.MissingKnife);}
    void BeginNarrativeConfrontation(){if(!BeginForestDialogueGraph(ForestDialogueGraph.Entry.ShuConfrontation))BeginManualStoryDialogue(ShuQuestionLines,()=>StartNarrativeBeat(NarrativeBeat.Silence));}
    void BeginNarrativeClosing(){StartNarrativeBeat(NarrativeBeat.AwaitWildlife);}
    bool UpdateNarrativeBeat(float dt){
        if(UpdateForestDialogueGraph(dt))return true;
        if(narrativeBeat==NarrativeBeat.None)return false;
        narrativeBeatAge+=dt;
        switch(narrativeBeat){
            case NarrativeBeat.MissingKnife:
                if(narrativeBeatAge>=1.8f){narrativeBeat=NarrativeBeat.None;BeginManualStoryDialogue(MissingKnifeLines,()=>StartNarrativeBeat(NarrativeBeat.Depart));}break;
            case NarrativeBeat.Depart:
                if(narrativeBeatAge>=.65f){returnWait=0;narrativeBeat=NarrativeBeat.None;}break;
            case NarrativeBeat.Silence:
                if(narrativeBeatAge>=1.35f){narrativeBeat=NarrativeBeat.None;BeginManualStoryDialogue(ShuAccusationLines,()=>shuConfrontationFinished=true);}break;
            case NarrativeBeat.EmptyClearing:
                if(narrativeBeatAge>=1.8f)narrativeBeat=NarrativeBeat.None;break;
            case NarrativeBeat.AwaitWildlife:
                // Let the existing return/follow AI enter the shot before greeting it.
                // If no route is available, use the honest alternative instead of spawning an actor.
                if(IsRestorationEnding&&Balance<FlourishThreshold-2){narrativeBeat=NarrativeBeat.None;journeyStage=JourneyStage.RestorationPlay;return false;}
                if(narrativeBeatAge>=2f&&(RestorationAnimalInShot()||narrativeBeatAge>=24f)){
                    narrativeBeat=NarrativeBeat.None;narrativeClosingDialogue=true;
                    BeginManualStoryDialogue(new[]{RestorationClosingLine()},()=>{narrativeClosingDialogue=false;StartNarrativeBeat(NarrativeBeat.RenewedForest);});
                }break;
            case NarrativeBeat.RenewedForest:
                if(narrativeBeatAge>=5.5f)narrativeBeat=NarrativeBeat.None;break;
        }
        // Waiting for wildlife is still playable. Keep the existing restoration return routes active.
        return narrativeBeat!=NarrativeBeat.AwaitWildlife;
    }
    void NarrativeCamera(ref Vector3 center,ref float zoom){
        if(!NarrativePreview)return;
        if(fatherIntroActive){center=HomeStoryCenter;zoom=HomeStoryZoom;}
        if(journeyStage==JourneyStage.RestorationArrival){
            float survey=restorationHomeHold?0:Mathf.SmoothStep(0,1,restorationSurveyAge/1.15f);
            center=Vector3.Lerp(HomeStoryCenter,Vector3.Lerp(HearthPosition,restorationClearing,.55f),survey);
            zoom=Mathf.Lerp(HomeStoryZoom,7.4f,survey);
        }
        if(narrativeBeat==NarrativeBeat.EmptyClearing){center=captureCameraCenter;zoom=6.8f;}
        if(journeyStage==JourneyStage.ReturnForKnife&&shuConfrontationStarted)zoom=5.8f;
        if(narrativeBeat==NarrativeBeat.RenewedForest){
            center=(Feet+HearthPosition)*.5f+Vector3.forward;
            var span=Feet-HearthPosition;
            zoom=Mathf.Max(10.2f,Mathf.Abs(Vector3.Dot(span,cam.transform.up))*.5f+4,
                Mathf.Abs(Vector3.Dot(span,cam.transform.right))*.5f/cam.aspect+4);
        }
    }
    DialogueDisplay narrativeSubtitleDisplay;
    internal void RefreshLanguagePresentation(){
        bool cinematic=StoryModeEnabled&&(NarrativeInputLocked||ManualStoryDialogueActive||fatherIntroActive||RestorationArrivalLocked||AfterCaptureLocked||journeyStage==JourneyStage.ReturnForKnife||journeyStage==JourneyStage.BoarPass||storyOpeningState==StoryOpeningState.CaptureBeat||storyCurtain>.001f);
        DongbaLanguageSwitch.Instance?.SetPresentationBlocked(this,cinematic);
    }
    void ApplyNarrativePose(){
        RefreshLanguagePresentation();
        if(!NarrativePreview)return;
        bool cinematic=StoryModeEnabled&&(NarrativeInputLocked||ManualStoryDialogueActive||fatherIntroActive||RestorationArrivalLocked||journeyStage==JourneyStage.ReturnForKnife);
        // RefreshContextHUD owns visibility; a narrative pose must never reopen hidden HUD.
        if(cinematic||ReelPresentationVisible){if(hudHealth!=null)hudHealth.transform.parent.gameObject.SetActive(false);if(hudArea!=null)hudArea.transform.parent.gameObject.SetActive(false);}
        if(cinematic){if(uiObjectivePanel!=null)uiObjectivePanel.SetActive(false);if(uiCoreHints!=null)uiCoreHints.SetActive(false);if(uiHelpShortcut!=null)uiHelpShortcut.SetActive(false);if(promptPanel!=null)promptPanel.SetActive(false);if(uiNoticePanel!=null)uiNoticePanel.SetActive(false);}
        if(forestSubtitles!=null&&narrativeSubtitleDisplay!=forestSubtitles){narrativeSubtitleDisplay=forestSubtitles;foreach(var canvas in forestSubtitles.GetComponentsInChildren<Canvas>(true))canvas.sortingOrder=30010;}
        if(storyFatherArt==null)return;
        storyFatherArt.rotation=billboard;
        if(narrativeBeat==NarrativeBeat.MissingKnife){
            var scale=storyFatherArt.localScale;float direction=narrativeBeatAge<1.1f?Feet.x-storyFatherRoot.position.x:knifeDropPoint.x-storyFatherRoot.position.x;scale.x=Mathf.Abs(scale.x)*(direction<0?-1:1);storyFatherArt.localScale=scale;
            float glance=Mathf.Sin(Mathf.Clamp01(narrativeBeatAge/1.1f)*Mathf.PI)*6;storyFatherArt.rotation=billboard*Quaternion.Euler(0,0,glance);
        }else if(narrativeBeat==NarrativeBeat.Silence)storyFatherArt.rotation=billboard*Quaternion.Euler(0,0,-3*Mathf.Sin(Mathf.Clamp01(narrativeBeatAge/1.35f)*Mathf.PI));
    }
    void StageNarrativeCapture(){
        SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;ResetForestSubtitles();
        knifeDropPoint=BoarClearing;player.position=DeparturePoint;storyFatherRoot.position=DeparturePoint+Vector3.right*1.3f;
        journeyStage=JourneyStage.Leaving;storyOpeningState=StoryOpeningState.Following;
        droppedKnife=Billboard("Preview lost knife",null,Get("father_knife"),.2f);droppedKnife.position=knifeDropPoint;droppedKnife.GetComponent<SpriteRenderer>().sharedMaterial=FatherKnifeInk;droppedKnife.GetComponent<SpriteRenderer>().sortingOrder=DepthOrder(knifeDropPoint.z);droppedKnife.gameObject.SetActive(false);FollowPlayerCamera(100);
    }
    IEnumerator PreviewNarrativeCapture(){yield return new WaitForSeconds(1);StageNarrativeCapture();}
    IEnumerator CheckNarrativePreview(){
        yield return new WaitForSeconds(1);StageNarrativeCapture();string folder="Verification/narrative-preview";Directory.CreateDirectory(folder);var log=new StringBuilder();
        yield return null;var fatherStart=storyFatherRoot.position;yield return new WaitForSeconds(.8f);
        log.AppendLine("Lost knife pauses before dialogue="+(narrativeBeat==NarrativeBeat.MissingKnife&&!ManualStoryDialogueActive&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.02f));CaptureSceneToTexture(folder+"/missing-knife.png");
        yield return new WaitForSeconds(1.3f);log.AppendLine("Lost knife dialogue waits for Enter="+(ManualStoryDialogueActive&&manualStoryLines.Length==MissingKnifeLines.Length));
        for(int i=0;i<MissingKnifeLines.Length;i++)AdvanceManualStoryDialogue();yield return new WaitForSeconds(.3f);log.AppendLine("Father pauses before departure="+(narrativeBeat==NarrativeBeat.Depart));
        float age=0;while(!shuConfrontationStarted&&age<50){age+=Time.deltaTime;yield return null;}
        log.AppendLine("Father reaches east-bank confrontation="+shuConfrontationStarted);
        if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return new WaitForSeconds(.6f);
        log.AppendLine("Silent reaction replaces descriptive subtitle="+(narrativeBeat==NarrativeBeat.Silence&&!ManualStoryDialogueActive&&!carryStarted));CaptureSceneToTexture(folder+"/silent-reaction.png");
        yield return new WaitForSeconds(1);log.AppendLine("Accusation still waits for Enter="+(ManualStoryDialogueActive&&!shuConfrontationFinished));if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();
        age=0;while(journeyStage!=JourneyStage.Captured&&age<15){age+=Time.deltaTime;yield return null;}
        log.AppendLine("Abduction leaves an empty shot="+(journeyStage==JourneyStage.Captured&&narrativeBeat==NarrativeBeat.EmptyClearing&&!ManualStoryDialogueActive));CaptureSceneToTexture(folder+"/empty-clearing.png");
        // Closing shot fixture only; ecological scoring/growth is checked separately.
        RestartRun();fatherIntroActive=false;ResetForestSubtitles();journeyStage=JourneyStage.RestorationComplete;storyOpeningState=StoryOpeningState.Off;storyFatherRoot.gameObject.SetActive(false);restorationClearing=Feet;careCount=FlourishThreshold;ecologyVisual=1;float startZoom=cam.orthographicSize;tonePreviewFreezeAnimals=true;foreach(var a in animals)a.root.gameObject.SetActive(false);BeginNarrativeClosing();
        yield return new WaitForSeconds(3);log.AppendLine("Closing remains playable while awaiting wildlife="+(cam.orthographicSize<startZoom+.3f&&!NarrativeInputLocked));CaptureSceneToTexture(folder+"/closing-camera.png");
        yield return new WaitForSeconds(22);log.AppendLine("Closing line waits for player="+(ManualStoryDialogueActive&&narrativeClosingDialogue));log.AppendLine("Empty clearing does not claim animals returned="+(manualStoryLines!=null&&manualStoryLines[0]=="Gaolequ: I'll come back tomorrow."));CaptureSceneToTexture(folder+"/closing-empty.png");AdvanceManualStoryDialogue();yield return new WaitForSeconds(6);
        log.AppendLine("Closing returns control="+!NarrativeInputLocked);
        var returned=animals[0];returned.root.position=Feet+Vector3.right*2;returned.root.gameObject.SetActive(true);returned.health=2;returned.migrationState=0;BeginNarrativeClosing();
        yield return new WaitForSeconds(6);log.AppendLine("Visible returning animal receives personal greeting="+(manualStoryLines!=null&&manualStoryLines[0]=="Gaolequ: You're back."));CaptureSceneToTexture(folder+"/closing-returned.png");AdvanceManualStoryDialogue();RestartRun();log.AppendLine("Restart clears preview beats="+(narrativeBeat==NarrativeBeat.None&&!narrativeClosingDialogue));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
