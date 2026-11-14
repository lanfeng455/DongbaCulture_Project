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
        droppedKnife=Billboard("Test dropped knife",null,Get("father_knife"),.2f);droppedKnife.position=knifeDropPoint;droppedKnife.GetComponent<SpriteRenderer>().sortingOrder=DepthOrder(knifeDropPoint.z);droppedKnife.GetComponent<SpriteRenderer>().sharedMaterial=FatherKnifeInk;
        log.AppendLine("Held and dropped knife share blood ink="+(storyFatherKnifeImage.sharedMaterial==droppedKnife.GetComponent<SpriteRenderer>().sharedMaterial&&FatherKnifeInk.shader.isSupported));
        yield return null;yield return new WaitForSeconds(3);
        var blood=knifeGroundBlood.GetComponent<SpriteRenderer>();log.AppendLine("Ground blood visible with correct scale="+(blood.enabled&&blood.sharedMaterial.shader.isSupported&&blood.bounds.size.x>.9f)+"; bounds="+blood.bounds+"; alpha="+blood.color.a);
        var fatherStart=storyFatherRoot.position;
        log.AppendLine("Question waits for Enter="+(ManualStoryDialogueActive&&manualStoryIndex==0&&shuConfrontationStarted&&!shuConfrontationFinished&&captureBeatAge==0));CaptureSceneToTexture(folder+"/question.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(.5f);
        log.AppendLine("Father freezes without descriptive subtitle="+(!ManualStoryDialogueActive&&narrativeBeat==NarrativeBeat.Silence&&Vector3.Distance(fatherStart,storyFatherRoot.position)<.01f));CaptureSceneToTexture(folder+"/silence.png");
        yield return new WaitForSeconds(1.5f);log.AppendLine("Accusation waits before anger="+(ManualStoryDialogueActive&&!carryStarted&&captureBeatAge==0));CaptureSceneToTexture(folder+"/accusation.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(.35f);log.AppendLine("Anger begins after final Enter="+(shuConfrontationFinished&&!ManualStoryDialogueActive&&!carryStarted&&captureBeatAge>0&&captureBeatAge<.8f));CaptureSceneToTexture(folder+"/anger.png");
        float elapsed=0;while(journeyStage!=JourneyStage.Captured&&elapsed<10){elapsed+=Time.deltaTime;yield return null;}
        log.AppendLine("Father and Shu carried off screen="+(journeyStage==JourneyStage.Captured&&!storyFatherRoot.gameObject.activeSelf&&!storyShu.gameObject.activeSelf));
        RestartRun();yield return new WaitForEndOfFrame();log.AppendLine("Restart clears confrontation="+(!shuConfrontationStarted&&!shuConfrontationFinished&&captureBeatAge==0&&fatherIntroActive&&!ManualStoryDialogueActive));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
