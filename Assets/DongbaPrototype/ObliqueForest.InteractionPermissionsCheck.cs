using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator PressForestPermissionKey(Key key){
        if(XRTrackingControlsCamera){
            ProcessXRInput(Vector2.zero,false,false,false,false,false,false);
            ProcessXRInput(Vector2.zero,false,key==Key.J,key==Key.E,false,false,false);
            yield return null;
            ProcessXRInput(Vector2.zero,false,false,false,false,false,false);
        }else{
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        }
    }
    IEnumerator PermissionScreenshot(string name){
        if(XRTrackingControlsCamera){CaptureSceneToTexture("Verification/pico-integration/"+name+".png");yield break;}
        DongbaLanguageCapture.Write(cam,"Verification/story-interaction/"+name+".png");yield return null;
    }
    internal IEnumerator VerifyStoryInteractionPermissions(Action<bool,string> check){
        var locale=DongbaLanguage.Current;DongbaLanguage.Set(DongbaLocale.English,false);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        StartForestMode(true);SkipOpeningForVerification();ResetForestSubtitles();ResetNarrativePreview();
        storyOpeningState=StoryOpeningState.Off;storyFatherRoot.gameObject.SetActive(false);tonePreviewFreezeAnimals=true;
        yield return null;yield return null;
        check(ForestDamageAvailable&&!ForestCareAvailable,"Hunting permits destruction and locks all restoration");
        var tree=plants.Where(b=>b.tree).OrderBy(b=>Vector3.Distance(b.home,ForestSpawn)).First();
        var adult=animals.First(b=>b.age==2&&b.health>0);int originalPlants=plants.Count,originalAnimals=animals.Count;
        player.position=tree.home+Vector3.back*.65f;lastDryPosition=Feet;interactionTarget=tree;FollowPlayerCamera(100);
        yield return PressForestPermissionKey(Key.E);GrowAround(tree);Feed(adult);
        check(careCount==0&&!tree.cultivated&&plants.Count==originalPlants&&animals.Count==originalAnimals,"E / X and direct care paths cannot grow plants, feed or breed during hunt");
        interactionTarget=tree;RefreshInteractionCard();if(XRTrackingControlsCamera)UpdateXRHUD(true,"");
        check(!hudPrompt.text.Contains("E   ")&&hudPrompt.text.Contains("J   Chop"),"PC hunting prompt advertises only chopping");
        if(XRTrackingControlsCamera)check(!xrContext.text.Contains("X  ")&&xrContext.text.Contains("Axe"),"MR hunting prompt has no care button");
        yield return PermissionScreenshot("hunt-only");
        yield return PressForestPermissionKey(Key.J);check(attackAge>=0,"J / trigger still starts an early-story axe swing");yield return new WaitForSeconds(.8f);
        while(tree.health>0)ApplyEcologyHit(tree);int damage=damageCount;
        player.position=tree.home;interactionTarget=null;PerformCare();
        check(tree.health==0&&careCount==0&&damageCount==damage,"Early-story stump cannot be replanted to undo ecological damage");
        check(!ForestTargetAvailable(tree),"Locked stump is not advertised as an actionable object");
        foreach(var stage in new[]{JourneyStage.BoarPass,JourneyStage.Invitation,JourneyStage.Leaving,JourneyStage.ReturnForKnife,JourneyStage.Captured,JourneyStage.WaitingAtHearth,JourneyStage.Realization,JourneyStage.EastJourney,JourneyStage.ScrollTransition,JourneyStage.ScrollWalk}){
            journeyStage=stage;check(!ForestCareAvailable&&!ForestDamageAvailable,"No forest actions during "+stage);
        }
        journeyStage=JourneyStage.Boar;check(ForestDamageAvailable&&!ForestCareAvailable,"Boar hunt retains attack, never repair");
        journeyStage=JourneyStage.Hunting;careCount=FlourishThreshold+2;
        check(!ForestCareAvailable,"Ecological value alone cannot unlock story care");careCount=0;
        BeginRestorationEnding();
        check(!ForestCareAvailable&&!ForestDamageAvailable,"Return arrival keeps both actions locked through dialogue");
        float until=Time.realtimeSinceStartup+15;
        while(journeyStage!=JourneyStage.RestorationPlay&&Time.realtimeSinceStartup<until){
            if(ManualStoryDialogueActive){forestSubtitles.CompleteCurrentLine();AdvanceManualStoryDialogue();}
            yield return null;
        }
        check(journeyStage==JourneyStage.RestorationPlay&&ForestCareAvailable&&!ForestDamageAvailable,"Actual return dialogue completion unlocks restoration and puts axe away");
        yield return null;yield return null;
        player.position=tree.home;lastDryPosition=Feet;interactionTarget=null;
        int before=careCount;yield return PressForestPermissionKey(Key.E);
        check(tree.health>0&&tree.targetScale<1&&careCount==before+1,"E / X replants the original damaged tree after return");
        tree.careReady=0;GrowAround(tree);check(tree.cultivated,"Cultivation unlocks after return");
        adult.careReady=0;adult.root.position=ForestSpawn+Vector3.left*2;int population=animals.Count;Feed(adult);
        check(animals.Count==population+1,"Feeding and nearby birth unlock after return");
        if(animals.Count>population){var child=animals[animals.Count-1];before=careCount;child.careReady=0;Feed(child);child.careReady=0;Feed(child);check(child.age==2&&careCount==before+1,"Young animal growth retains original ecology reward");}
        int healthBefore=tree.health,damageBefore=damageCount;
        yield return PressForestPermissionKey(Key.J);BeginAxeAttack();ApplyEcologyHit(tree);
        check(attackAge<0&&!axe.gameObject.activeSelf&&tree.health==healthBefore&&damageCount==damageBefore,"Late-story input and direct damage paths cannot chop or attack");
        player.position=tree.home+Vector3.back*.6f;interactionTarget=tree;FollowPlayerCamera(100);yield return null;
        RefreshInteractionCard();if(XRTrackingControlsCamera)UpdateXRHUD(true,"");
        check(!hudPrompt.text.Contains("J   "),"PC restoration prompt removes attack");
        if(XRTrackingControlsCamera)check(!xrContext.text.Contains("Axe")&&xrContext.text.Contains("X  "),"MR restoration prompt offers care only");
        yield return PermissionScreenshot("return-care-only");
        journeyStage=JourneyStage.RestorationComplete;check(ForestCareAvailable&&!ForestDamageAvailable,"Completed story keeps peaceful restoration available");
        RestartFromInterface();check(StoryModeEnabled&&ForestDamageAvailable&&!ForestCareAvailable&&Balance==0,"Restart story relocks restoration and restores hunting");
        StartForestMode(false);check(ForestDamageAvailable&&ForestCareAvailable,"Sandbox still permits both interactions");
        BeginAxeAttack();check(attackAge>=0,"Sandbox axe still works");
        StartForestMode(false);GrowAround(plants.First(b=>b.tree));check(careCount==1,"Sandbox care remains available from normal ecology");
        StartForestMode(false);DongbaLanguage.Set(locale,false);
    }
    IEnumerator CheckStoryInteractionPermissions(){
        Directory.CreateDirectory("Verification/story-interaction");var results=new List<string>();bool passed=true;
        yield return new WaitForSeconds(2);DongbaInterface.Instance?.Close(false);yield return null;
        yield return VerifyStoryInteractionPermissions((ok,label)=>{passed&=ok;results.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Verification/story-interaction/checks.txt",results);Debug.Log(results[results.Count-1]);});
        Application.Quit(passed?0:1);
    }
}}
