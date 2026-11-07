using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckRestorationEnding(){
        yield return new WaitForSeconds(1);PrepareRestorationPreview();string folder="Verification/restoration-ending";Directory.CreateDirectory(folder);var log=new StringBuilder();
        Vector3 arrival=Feet;int damage=damageCount;yield return new WaitForSeconds(3.5f);CaptureSceneToTexture(folder+"/return-to-clearing.png");
        log.AppendLine("Return keeps actual damage and stumps="+(damageCount==damage&&restorationStumps.Count>=2&&Balance<0));
        log.AppendLine("Father absent and return dialogue waits="+(!storyFatherRoot.gameObject.activeSelf&&ManualStoryDialogueActive&&journeyStage==JourneyStage.RestorationArrival));
        for(int i=0;i<3;i++)yield return PressStoryKey(Key.Enter);
        log.AppendLine("Enter unlocks restoration play="+(journeyStage==JourneyStage.RestorationPlay&&!ManualStoryDialogueActive));
        int restored=0;foreach(var tree in new List<Being>(restorationStumps)){
            player.position=tree.home;interactionTarget=null;int before=careCount;Care();if(tree.health>0&&careCount==before+1)restored++;
            yield return null;
        }
        log.AppendLine("Real care replants original stumps="+(restored>=2));
        yield return new WaitForSeconds(1.2f);
        var initialTrees=new List<Being>();foreach(var tree in plants)if(tree.tree)initialTrees.Add(tree);
        foreach(var tree in initialTrees){if(Balance>=FlourishThreshold)break;tree.careReady=0;player.position=tree.home;interactionTarget=null;Care();yield return null;}
        log.AppendLine("Restoration uses existing scoring to reach flourishing="+(Balance>=FlourishThreshold&&damageCount==damage));
        player.position=arrival;FollowPlayerCamera(100);yield return new WaitForSeconds(3);CaptureSceneToTexture(folder+"/replanting.png");
        float wait=0;while(journeyStage!=JourneyStage.RestorationComplete&&wait<25){wait+=Time.deltaTime;yield return null;}
        int grown=0;foreach(var tree in restorationStumps)if(tree.health>0&&tree.targetScale>.85f)grown++;
        log.AppendLine("Replanted trees gradually grow="+(grown>=2));
        log.AppendLine("Flourishing completes chapter without recapture="+(journeyStage==JourneyStage.RestorationComplete&&!storyFatherRoot.gameObject.activeSelf));
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(12);CaptureSceneToTexture(folder+"/renewed-clearing.png");
        int live=0;float wildlifeWait=0;
        while(live==0&&wildlifeWait<30){
            foreach(var animal in animals)if(animal.health>0&&animal.root.gameObject.activeSelf&&Vector3.Distance(animal.root.position,restorationClearing)<16)live++;
            if(live==0){wildlifeWait+=1;yield return new WaitForSeconds(1);}
        }
        log.AppendLine("Local wildlife available after recovery="+(live>0)+"; extraWait="+wildlifeWait+"; guidedReturns="+restorationSeeded+"; returns="+returnsStarted+"; diagnostic="+restorationReturnCheck+"; dialogue="+ManualStoryDialogueActive);
        CaptureSceneToTexture(folder+"/renewed-clearing.png");
        yield return PressStoryKey(Key.R);log.AppendLine("R restores beginning and clears ending="+(!IsRestorationEnding&&fatherIntroActive&&Balance==0&&restorationReplanted.Count==0));
        File.WriteAllText(folder+"/checks.txt",log.ToString());Application.Quit();
    }
}}
