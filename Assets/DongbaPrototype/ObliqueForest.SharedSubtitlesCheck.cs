using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator PressStoryKey(Key key){var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return new WaitForEndOfFrame();}
    IEnumerator CheckSharedSubtitles(){
        yield return new WaitForSeconds(1);string folder="Verification/manual-dialogue";Directory.CreateDirectory(folder);var log=new StringBuilder();
        SetStoryModeEnabled(true);RestartRun();yield return new WaitForEndOfFrame();
        yield return PressStoryKey(Key.Enter);log.AppendLine("Survey ignores early Enter="+(OpeningSurveyActive&&fatherIntroLine==0&&forestSubtitleKey==null));
        yield return new WaitForSeconds(OpeningSurveySeconds+.1f);yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(3);log.AppendLine("Opening waits without timer="+(fatherIntroActive&&fatherIntroLine==0));
        var subtitleCanvas=forestSubtitles.SubtitleText.GetComponentInParent<Canvas>();bool aboveForest=true;foreach(var sprite in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))if(sprite.enabled&&sprite.gameObject.activeInHierarchy&&sprite.sortingOrder>=subtitleCanvas.sortingOrder)aboveForest=false;log.AppendLine("Subtitle layer above foreground ink="+aboveForest);
        log.AppendLine("Opening shown in full="+!forestSubtitles.IsTyping);CaptureSceneToTexture(folder+"/first-line.png");
        yield return PressStoryKey(Key.E);log.AppendLine("E does not advance="+(fatherIntroLine==0));
        yield return PressStoryKey(Key.Enter);log.AppendLine("One Enter advances exactly one line="+(fatherIntroLine==1&&!forestSubtitles.IsTyping));CaptureSceneToTexture(folder+"/opening.png");
        for(int intro=1;intro<FatherIntroLines.Length;intro++)yield return PressStoryKey(Key.Enter);
        log.AppendLine("Opening completes without attacking="+(!fatherIntroActive&&attackAge<0));
        journeyStage=JourneyStage.Invitation;storyFatherRoot.position=Feet+Vector3.left;yield return null;yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(3);log.AppendLine("Invitation waits for Enter="+(ManualStoryDialogueActive&&journeyStage==JourneyStage.Invitation));
        yield return PressStoryKey(Key.Enter);log.AppendLine("Invitation second line stays locked="+(ManualStoryDialogueActive&&manualStoryIndex==1&&attackAge<0));
        yield return PressStoryKey(Key.Enter);log.AppendLine("Invitation final reply still waits="+(ManualStoryDialogueActive&&manualStoryIndex==2&&journeyStage==JourneyStage.Invitation&&attackAge<0));
        yield return PressStoryKey(Key.Enter);log.AppendLine("Invitation completes into hunt="+(!ManualStoryDialogueActive&&journeyStage==JourneyStage.Boar&&attackAge<0));
        // Stage the return dialogue at the real west-bank trigger.
        knifeDropPoint=BoarClearing;player.position=new Vector3(-7,0,0);storyFatherRoot.position=Feet+Vector3.left;journeyStage=JourneyStage.Leaving;
        droppedKnife=Billboard("Check knife",null,Get("father_knife"),.2f);droppedKnife.position=knifeDropPoint;droppedKnife.GetComponent<SpriteRenderer>().sortingOrder=DepthOrder(knifeDropPoint.z);yield return null;yield return new WaitForEndOfFrame();var fatherStart=storyFatherRoot.position;
        // Graph dialogue nodes each expose one manual line; pauses advance on their own.
        float knifeUntil=Time.realtimeSinceStartup+4;
        while(GraphRunning&&!ManualStoryDialogueActive&&Time.realtimeSinceStartup<knifeUntil)yield return null;
        string firstKnifeNode=ActiveDialogueNode;
        yield return new WaitForSeconds(5);log.AppendLine("Knife dialogue never advances on timer="+(ManualStoryDialogueActive&&ActiveDialogueNode==firstKnifeNode&&manualStoryIndex==0&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));
        knifeUntil=Time.realtimeSinceStartup+8;
        while(GraphRunning&&ActiveDialogueNode!="knife_offer"&&Time.realtimeSinceStartup<knifeUntil){if(ManualStoryDialogueActive)yield return PressStoryKey(Key.Enter);else yield return null;}
        log.AppendLine("Son offers to accompany father before departure="+(ActiveDialogueNode=="knife_offer"&&ManualStoryDialogueActive&&forestGraphLine!=null&&forestGraphLine.speaker=="Gaolequ"&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));CaptureSceneToTexture(folder+"/son-offer.png");
        yield return PressStoryKey(Key.Enter);log.AppendLine("Father final reply shown before departure="+(ActiveDialogueNode=="knife_reply"&&ManualStoryDialogueActive&&forestGraphLine!=null&&forestGraphLine.speaker=="Father"&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));CaptureSceneToTexture(folder+"/knife.png");
        yield return PressStoryKey(Key.Enter);log.AppendLine("Departure pause keeps father still="+(ActiveDialogueNode=="knife_depart"&&!ManualStoryDialogueActive&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));
        knifeUntil=Time.realtimeSinceStartup+4;while(GraphRunning&&Time.realtimeSinceStartup<knifeUntil)yield return null;
        log.AppendLine("Knife dialogue releases father="+(!GraphRunning&&!ManualStoryDialogueActive&&returnWait==0));
        RestartRun();SkipOpeningForVerification();journeyStage=JourneyStage.Realization;EnsureAfterCapturePresentation();afterCaptureCaption.gameObject.SetActive(true);BeginRealizationDialogue();yield return new WaitForSeconds(4);
        log.AppendLine("Realization does not auto depart="+(journeyStage==JourneyStage.Realization&&ManualStoryDialogueActive));CaptureSceneToTexture(folder+"/realization.png");
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);log.AppendLine("Realization Enter enables east journey="+(journeyStage==JourneyStage.EastJourney&&!ManualStoryDialogueActive&&attackAge<0));
        BeginManualStoryDialogue(new[]{"Father: Test."});RestartRun();yield return new WaitForEndOfFrame();log.AppendLine("Restart clears pending dialogue="+(!ManualStoryDialogueActive&&fatherIntroActive));
        SetStoryModeEnabled(false);yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();log.AppendLine("Sandbox clears subtitles="+(!ManualStoryDialogueActive&&!forestSubtitles.SubtitleText.gameObject.activeInHierarchy));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
