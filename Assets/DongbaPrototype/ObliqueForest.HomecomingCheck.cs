using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckHomecoming(){
        const string folder="Verification/homecoming";Directory.CreateDirectory(folder);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--record-homecoming")>=0){yield return RecordHomecoming();yield break;}
        string report=folder+"/checks.txt";File.WriteAllText(report,"");bool passed=true;
        Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(report,name+"="+ok+"\n");};
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();
        DongbaLanguage.Set(DongbaLocale.Chinese,false);FollowPlayerCamera(100);yield return null;yield return new WaitForEndOfFrame();
        Vector3 homeCamera=cam.transform.position,openingFeet=Feet;float homeZoom=cam.orthographicSize;
        DongbaLanguageCapture.Write(cam,folder+"/opening-home.png");
        check("Home opening includes both people",fatherIntroActive&&storyFatherRoot.gameObject.activeSelf&&Vector3.Distance(storyFatherRoot.position,FatherHomeStart)<.01f);
        for(int i=0;i<3;i++)yield return PressStoryKey(Key.Enter);
        var trees=plants.Where(b=>b.tree).OrderBy(b=>Vector3.Distance(b.home,HearthPosition)).ToArray();
        while(trees[0].health>0)ApplyEcologyHit(trees[0]);while(trees[1].health>0)ApplyEcologyHit(trees[1]);
        var victim=animals[0];while(victim.health>0)ApplyEcologyHit(victim);
        int damage=damageCount,count=plants.Count;var healthBefore=trees.Select(t=>t.health).ToArray();
        BeginRestorationEnding();yield return new WaitForSeconds(.35f);
        check("Return narration is on transition",ManualStoryDialogueActive&&storyCurtain>.99f&&forestSubtitles.SubtitleText.text.Contains("父亲终究"));
        check("Return blackout keeps Enter hint above it",forestSubtitleAdvance.transform.GetSiblingIndex()>storyCurtainImage.transform.GetSiblingIndex()&&forestSubtitleAdvance.gameObject.activeInHierarchy&&forestSubtitleAdvance.color.r>.8f);
        check("Actual tree and animal history retained",damageCount==damage&&plants.Count==count&&trees.Select(t=>t.health).SequenceEqual(healthBefore)&&victim.health==0);
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(2);
        check("Home composition is reused",Vector3.Distance(homeCamera,cam.transform.position)<.015f&&Mathf.Abs(homeZoom-cam.orthographicSize)<.015f&&Vector3.Distance(Feet,openingFeet)<.001f);
        check("Empty father place is held without explanation",!storyFatherRoot.gameObject.activeSelf&&restorationHomeHold&&!ManualStoryDialogueActive&&storyCurtain<.01f);
        DongbaLanguageCapture.Write(cam,folder+"/return-home.png");
        Vector3 held=Feet;yield return PressStoryKey(Key.D);check("Home hold keeps movement locked",Vector3.Distance(held,Feet)<.001f);
        yield return new WaitForSeconds(2.5f);
        check("Damage survey leads to two short lines",ManualStoryDialogueActive&&manualStoryLines.Length==2&&manualStoryLines[0]==RestorationArrivalLines[1]);
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;check("Return dialogue switches language in place",forestSubtitles.SubtitleText.text.Contains("Back then, I thought"));
        DongbaLanguageCapture.Write(cam,folder+"/return-dialogue-en.png");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;DongbaLanguageCapture.Write(cam,folder+"/return-dialogue-zh.png");
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);
        check("Return hands back control",journeyStage==JourneyStage.RestorationPlay&&!ManualStoryDialogueActive);
        player.position=trees[0].home;interactionTarget=null;int careBefore=careCount;Care();
        check("Replant retains footing and starts small",trees[0].health>0&&trees[0].targetScale==.35f&&careCount==careBefore+1&&Free(Feet));
        yield return new WaitForSeconds(1.1f);player.position=trees[0].home;interactionTarget=null;
        int plantsBefore=plants.Count;Care();var sprouts=plants.Skip(plantsBefore).ToArray();
        check("Care creates local staggered sprouts",sprouts.Length>=2&&sprouts.All(p=>Vector3.Distance(p.home,trees[0].home)<=2.01f)&&sproutRevealStarts[sprouts[1]]>sproutRevealStarts[sprouts[0]]);
        int score=careCount;Care();check("Repeated care produces no success",careCount==score&&plants.Count==plantsBefore+sprouts.Length);
        yield return new WaitForSeconds(.22f);
        check("Sprouts unfold at distinct times",sprouts.Length>=2&&sprouts[0].art.localScale.x/sprouts[0].initialScale.x>sprouts[1].art.localScale.x/sprouts[1].initialScale.x);
        DongbaLanguageCapture.Write(cam,folder+"/local-sprouts.png");
        // Normal roaming, feeding, and growth share the same ordinary animal instances.
        SetStoryModeEnabled(false);RestartRun();Being adult=null;
        foreach(var a in animals)if(FindLocal(a.root.position,.7f,1.35f,animals.Count+a.speciesIndex*17,true,out var unused)){adult=a;break;}
        check("An adult has room for offspring",adult!=null);
        if(adult!=null){
            player.position=adult.root.position+Vector3.left;FollowPlayerCamera(100);int before=animals.Count;Feed(adult);
            check("Successful feeding turns and pauses target",animals.Count==before+1&&FeedingResponseActive(adult)&&!HasAnimalRoute(adult));
            var child=animals[animals.Count-1];check("Newborn remains beside fed adult",child.age==0&&Vector3.Distance(child.root.position,adult.root.position)<1.36f&&child.protectedUntil>clock+10);
            int responses=feedingResponses;score=careCount;Feed(adult);check("Cooldown has no repeat response or score",feedingResponses==responses&&careCount==score&&animals.Count==before+1);
            yield return new WaitForSeconds(1.6f);player.position=child.root.position+Vector3.left;Feed(child);score=careCount;
            yield return new WaitForSeconds(1.6f);Feed(child);check("Only growing to adulthood earns care",child.age==2&&careCount==score+1);
            ApplyEcologyHit(child);child.angry=true;check("Being attacked cancels feeding pose",!FeedingResponseActive(child));
        }
        RestartRun();var starts=animals.Select(a=>a.root.position).ToArray();yield return new WaitForSeconds(10);
        int moved=animals.Where((a,i)=>Vector3.Distance(a.root.position,starts[i])>.25f).Count();
        check("Normal animals walk and pause",moved>3&&animalMoments.Values.Any(m=>m.travelling)&&animalMoments.Values.Any(m=>m.pauseUntil>clock));
        check("Normal animals do not gather",animals.All(a=>a.follow==0)&&!friendlyWeather);
        // A story completed mostly by hunting must neither invent cut trees nor deadlock on two stumps.
        SetStoryModeEnabled(true);RestartRun();BeginRestorationEnding();yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(4.6f);
        check("Uncut forest gets truthful dialogue",restorationStumps.Count==0&&ManualStoryDialogueActive&&manualStoryLines[0].Contains("quiet"));
        yield return PressStoryKey(Key.Enter);yield return PressStoryKey(Key.Enter);
        careCount=FlourishThreshold;ecologyVisual=1;yield return null;
        check("Zero-stump return does not require invented stumps",journeyStage==JourneyStage.RestorationComplete&&RestorationRequiredReplants==0);
        damageCount=careCount+3;yield return null;
        check("Renewed damage cancels a pending closing",journeyStage==JourneyStage.RestorationPlay&&narrativeBeat==NarrativeBeat.None&&!NarrativeInputLocked);
        careCount=damageCount+FlourishThreshold;ecologyVisual=1;yield return null;
        // Closing fixture: no teleporting a visitor; wait for an existing route to enter the shot.
        ResetNarrativePreview();ResetForestSubtitles();tonePreviewFreezeAnimals=true;foreach(var a in animals)a.root.gameObject.SetActive(false);
        BeginNarrativeClosing();float startZoom=cam.orthographicSize;yield return new WaitForSeconds(3);
        check("Closing waits before greeting and pulling back",narrativeBeat==NarrativeBeat.AwaitWildlife&&!NarrativeInputLocked&&!ManualStoryDialogueActive&&cam.orthographicSize<8.6f);
        var returned=animals[0];returned.health=2;returned.root.position=Feet+Vector3.right*3;returned.root.gameObject.SetActive(true);returned.migrationState=0;
        yield return null;yield return new WaitForEndOfFrame();
        check("Visible visitor is greeted before wide shot",narrativeClosingDialogue&&manualStoryLines[0]=="Gaolequ: You're back."&&narrativeBeat==NarrativeBeat.None);
        DongbaLanguageCapture.Write(cam,folder+"/greeting-before-wide.png");
        yield return PressStoryKey(Key.Enter);yield return new WaitForSeconds(3);
        check("Wide home shot follows greeting",narrativeBeat==NarrativeBeat.RenewedForest&&cam.orthographicSize>startZoom+1&&!ManualStoryDialogueActive);
        DongbaLanguageCapture.Write(cam,folder+"/closing-wide.png");
        yield return new WaitForSeconds(3);check("Closing returns free control",!NarrativeInputLocked);
        yield return PressStoryKey(Key.R);check("R clears all new beats and local reactions",fatherIntroActive&&restorationStumps.Count==0&&!restorationHomeHold&&animalMoments.Count==0&&sproutRevealStarts.Count==0&&Balance==0);
        File.AppendAllText(report,"Closing visitor arrival above is a visibility fixture; actual migration is tested separately.\n");
        Application.Quit(passed?0:1);
    }
    IEnumerator RecordHomecoming(){
        const string folder="Verification/homecoming/frames";Directory.CreateDirectory(folder);
        yield return new WaitForSeconds(1);DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(true);RestartRun();
        int previousRate=Time.captureFramerate;Time.captureFramerate=24;
        // This preview arranges the already-tested damaged return, not a recorded full rescue run.
        for(int frame=0;frame<336;frame++){
            if(frame==72)PrepareRestorationPreview();
            if(frame==120||frame==240||frame==276)AdvanceManualStoryDialogue();
            yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/"+frame.ToString("D4")+".png",1280,720);
        }
        Time.captureFramerate=previousRate;Application.Quit();
    }
}}
