using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckShuConfrontation(){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;
        string folder="Verification/shu-confrontation";Directory.CreateDirectory(folder);var log=new StringBuilder();
        knifeDropPoint=BoarClearing;player.position=DeparturePoint;storyFatherRoot.position=knifeDropPoint+Vector3.left*.5f;journeyStage=JourneyStage.ReturnForKnife;storyOpeningState=StoryOpeningState.CaptureBeat;returnWait=0;
        droppedKnife=Billboard("Test dropped knife",null,Get("father_knife"),.2f);droppedKnife.position=knifeDropPoint;
        yield return null;yield return new WaitForSeconds(3);var fatherStart=storyFatherRoot.position;
        log.AppendLine("Question waits for Enter="+(ManualStoryDialogueActive&&manualStoryIndex==0&&shuConfrontationStarted&&!shuConfrontationFinished&&captureBeatAge==0));CaptureSceneToTexture(folder+"/question.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(2);
        log.AppendLine("Father freezes before accusation="+(ManualStoryDialogueActive&&manualStoryIndex==1&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));CaptureSceneToTexture(folder+"/silence.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(2);log.AppendLine("Accusation waits before anger="+(ManualStoryDialogueActive&&manualStoryIndex==2&&!carryStarted&&captureBeatAge==0));CaptureSceneToTexture(folder+"/accusation.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(.35f);log.AppendLine("Anger begins after final Enter="+(shuConfrontationFinished&&!ManualStoryDialogueActive&&!carryStarted&&captureBeatAge>0&&captureBeatAge<.8f));CaptureSceneToTexture(folder+"/anger.png");
        float elapsed=0;while(journeyStage!=JourneyStage.Captured&&elapsed<10){elapsed+=Time.deltaTime;yield return null;}
        log.AppendLine("Father and Shu carried off screen="+(journeyStage==JourneyStage.Captured&&!storyFatherRoot.gameObject.activeSelf&&!storyShu.gameObject.activeSelf));
        RestartRun();yield return new WaitForEndOfFrame();log.AppendLine("Restart clears confrontation="+(!shuConfrontationStarted&&!shuConfrontationFinished&&captureBeatAge==0&&fatherIntroActive&&!ManualStoryDialogueActive));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
