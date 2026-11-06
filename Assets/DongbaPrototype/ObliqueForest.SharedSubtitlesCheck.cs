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
        yield return new WaitForSeconds(3);log.AppendLine("Opening waits without timer="+(fatherIntroActive&&fatherIntroLine==0));
        log.AppendLine("Opening shown in full="+!forestSubtitles.IsTyping);
        yield return PressStoryKey(Key.E);log.AppendLine("E does not advance="+(fatherIntroLine==0));
        yield return PressStoryKey(Key.Enter);log.AppendLine("One Enter advances exactly one line="+(fatherIntroLine==1&&!forestSubtitles.IsTyping));CaptureSceneToTexture(folder+"/opening.png");
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);log.AppendLine("Opening completes without attacking="+(!fatherIntroActive&&attackAge<0));
        journeyStage=JourneyStage.Invitation;storyFatherRoot.position=Feet+Vector3.left;yield return null;yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(3);log.AppendLine("Invitation waits for Enter="+(ManualStoryDialogueActive&&journeyStage==JourneyStage.Invitation));
        yield return PressStoryKey(Key.Enter);log.AppendLine("Invitation second line stays locked="+(ManualStoryDialogueActive&&manualStoryIndex==1&&attackAge<0));
        yield return PressStoryKey(Key.Enter);log.AppendLine("Invitation completes into hunt="+(!ManualStoryDialogueActive&&journeyStage==JourneyStage.Boar&&attackAge<0));
        // Stage the return dialogue at the real west-bank trigger.
        knifeDropPoint=BoarClearing;player.position=new Vector3(-7,0,0);storyFatherRoot.position=Feet+Vector3.left;journeyStage=JourneyStage.Leaving;
        droppedKnife=new GameObject("Check knife").transform;yield return null;yield return new WaitForEndOfFrame();var fatherStart=storyFatherRoot.position;
        yield return new WaitForSeconds(5);log.AppendLine("Knife dialogue never advances on timer="+(ManualStoryDialogueActive&&manualStoryIndex==0&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);log.AppendLine("Knife third line shown before departure="+(ManualStoryDialogueActive&&manualStoryIndex==2));CaptureSceneToTexture(folder+"/knife.png");
        yield return PressStoryKey(Key.Enter);log.AppendLine("Knife dialogue releases father="+!ManualStoryDialogueActive);
        RestartRun();fatherIntroActive=false;journeyStage=JourneyStage.Realization;EnsureAfterCapturePresentation();afterCaptureCaption.gameObject.SetActive(true);BeginRealizationDialogue();yield return new WaitForSeconds(4);
        log.AppendLine("Realization does not auto depart="+(journeyStage==JourneyStage.Realization&&ManualStoryDialogueActive));
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);log.AppendLine("Realization Enter enables east journey="+(journeyStage==JourneyStage.EastJourney&&!ManualStoryDialogueActive&&attackAge<0));
        BeginManualStoryDialogue(new[]{"Father: Test."});RestartRun();yield return new WaitForEndOfFrame();log.AppendLine("Restart clears pending dialogue="+(!ManualStoryDialogueActive&&fatherIntroActive));
        SetStoryModeEnabled(false);yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();log.AppendLine("Sandbox clears subtitles="+(!ManualStoryDialogueActive&&!forestSubtitles.SubtitleText.gameObject.activeInHierarchy));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
