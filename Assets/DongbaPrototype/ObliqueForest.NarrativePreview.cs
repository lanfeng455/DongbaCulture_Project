using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    // Opt-in comparison build. The ordinary build retains its existing story timing.
    bool? narrativePreviewEnabled;
    bool NarrativePreview {get {if(!narrativePreviewEnabled.HasValue)narrativePreviewEnabled=Array.IndexOf(Environment.GetCommandLineArgs(),"--narrative-preview")>=0||File.Exists(Path.Combine(Application.dataPath,"../narrative-preview.flag"));return narrativePreviewEnabled.Value;}}
    enum NarrativeBeat {None,MissingKnife,Depart,Silence,EmptyClearing,RenewedForest}
    NarrativeBeat narrativeBeat;float narrativeBeatAge;bool narrativeClosingDialogue;
    bool NarrativeInputLocked=>(narrativeBeat!=NarrativeBeat.None||narrativeClosingDialogue);
    void ResetNarrativePreview(){narrativeBeat=NarrativeBeat.None;narrativeBeatAge=0;narrativeClosingDialogue=false;if(storyFatherArt!=null)storyFatherArt.rotation=billboard;}
    void StartNarrativeBeat(NarrativeBeat beat){narrativeBeat=beat;narrativeBeatAge=0;notice="";noticeTime=0;}
    void BeginMissingKnifeBeat(){StartNarrativeBeat(NarrativeBeat.MissingKnife);}
    void BeginNarrativeConfrontation(){BeginManualStoryDialogue(ShuQuestionLines,()=>StartNarrativeBeat(NarrativeBeat.Silence));}
    void BeginNarrativeClosing(){StartNarrativeBeat(NarrativeBeat.RenewedForest);}
    bool UpdateNarrativeBeat(float dt){
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
            case NarrativeBeat.RenewedForest:
                if(narrativeBeatAge>=5.5f){narrativeBeat=NarrativeBeat.None;narrativeClosingDialogue=true;BeginManualStoryDialogue(new[]{RestorationClosingLine()},()=>narrativeClosingDialogue=false);}break;
        }
        return true;
    }
    void NarrativeCamera(ref Vector3 center,ref float zoom){
        if(!NarrativePreview)return;
        if(narrativeBeat==NarrativeBeat.EmptyClearing){center=captureCameraCenter;zoom=6.8f;}
        if(journeyStage==JourneyStage.ReturnForKnife&&shuConfrontationStarted)zoom=5.8f;
        if(narrativeBeat==NarrativeBeat.RenewedForest||narrativeClosingDialogue){center=Vector3.Lerp(Feet,restorationClearing,.35f);zoom=10.2f;}
    }
    DialogueDisplay narrativeSubtitleDisplay;
    void ApplyNarrativePose(){
        if(!NarrativePreview)return;
        bool cinematic=StoryModeEnabled&&(NarrativeInputLocked||ManualStoryDialogueActive||fatherIntroActive||journeyStage==JourneyStage.ReturnForKnife);
        if(hudHealth!=null)hudHealth.transform.parent.gameObject.SetActive(!cinematic);
        if(hudArea!=null)hudArea.transform.parent.gameObject.SetActive(!cinematic);
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
        yield return new WaitForSeconds(1.3f);log.AppendLine("Lost knife dialogue waits for Enter="+(ManualStoryDialogueActive&&manualStoryLines.Length==2));
        AdvanceManualStoryDialogue();AdvanceManualStoryDialogue();yield return new WaitForSeconds(.3f);log.AppendLine("Father pauses before departure="+(narrativeBeat==NarrativeBeat.Depart));
        float age=0;while(!shuConfrontationStarted&&age<50){age+=Time.deltaTime;yield return null;}
        log.AppendLine("Father reaches east-bank confrontation="+shuConfrontationStarted);
        if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return new WaitForSeconds(.6f);
        log.AppendLine("Silent reaction replaces descriptive subtitle="+(narrativeBeat==NarrativeBeat.Silence&&!ManualStoryDialogueActive&&!carryStarted));CaptureSceneToTexture(folder+"/silent-reaction.png");
        yield return new WaitForSeconds(1);log.AppendLine("Accusation still waits for Enter="+(ManualStoryDialogueActive&&!shuConfrontationFinished));if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();
        age=0;while(journeyStage!=JourneyStage.Captured&&age<15){age+=Time.deltaTime;yield return null;}
        log.AppendLine("Abduction leaves an empty shot="+(journeyStage==JourneyStage.Captured&&narrativeBeat==NarrativeBeat.EmptyClearing&&!ManualStoryDialogueActive));CaptureSceneToTexture(folder+"/empty-clearing.png");
        // Closing shot fixture only; ecological scoring/growth is checked separately.
        RestartRun();fatherIntroActive=false;ResetForestSubtitles();journeyStage=JourneyStage.RestorationComplete;storyOpeningState=StoryOpeningState.Off;storyFatherRoot.gameObject.SetActive(false);restorationClearing=Feet;float startZoom=cam.orthographicSize;tonePreviewFreezeAnimals=true;foreach(var a in animals)a.root.gameObject.SetActive(false);BeginNarrativeClosing();
        yield return new WaitForSeconds(3);log.AppendLine("Closing view widens while controls are held="+(cam.orthographicSize>startZoom+1&&NarrativeInputLocked));CaptureSceneToTexture(folder+"/closing-camera.png");
        yield return new WaitForSeconds(3);log.AppendLine("Closing line waits for player="+(ManualStoryDialogueActive&&narrativeClosingDialogue));log.AppendLine("Empty clearing does not claim animals returned="+(manualStoryLines!=null&&manualStoryLines[0]=="Gaolequ: I'll come back tomorrow."));CaptureSceneToTexture(folder+"/closing-empty.png");AdvanceManualStoryDialogue();yield return null;
        log.AppendLine("Closing returns control="+!NarrativeInputLocked);
        var returned=animals[0];returned.root.position=Feet+Vector3.right*2;returned.root.gameObject.SetActive(true);returned.health=2;returned.migrationState=0;BeginNarrativeClosing();
        yield return new WaitForSeconds(6);log.AppendLine("Visible returning animal receives personal greeting="+(manualStoryLines!=null&&manualStoryLines[0]=="Gaolequ: You're back."));CaptureSceneToTexture(folder+"/closing-returned.png");AdvanceManualStoryDialogue();RestartRun();log.AppendLine("Restart clears preview beats="+(narrativeBeat==NarrativeBeat.None&&!narrativeClosingDialogue));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
