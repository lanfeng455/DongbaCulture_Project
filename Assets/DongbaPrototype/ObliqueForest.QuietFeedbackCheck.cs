using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool NoActionNarration()=>!uiNoticePanel.activeInHierarchy&&hudNotice.text.Length==0&&
        (forestSubtitles==null||!forestSubtitles.SubtitleText.gameObject.activeInHierarchy);
    IEnumerator CheckQuietFeedback(){
        const string folder="Verification/quiet-feedback";Directory.CreateDirectory(folder);
        string report=folder+"/checks.txt";File.WriteAllText(report,"");bool passed=true;
        Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(report,name+"="+ok+"\n");};
        yield return new WaitForSeconds(1);DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(false);RestartRun();
        // The shared dialogue presenter fades out over .25 seconds when leaving Story Mode.
        yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();check("Reset and mode switch have no action banner",NoActionNarration());
        check("No automatic bottom controls strip",!uiCoreHints.activeSelf&&uiHelpShortcut.activeSelf);
        yield return PressStoryKey(Key.H);check("Controls remain available on H",uiHelpPanel.activeSelf);yield return PressStoryKey(Key.H);
        // Isolate one existing trunk to exercise the real axe mesh contacts and notifications.
        tonePreviewFreezeAnimals=true;foreach(var a in animals)a.root.gameObject.SetActive(false);foreach(var p in plants)p.root.gameObject.SetActive(false);
        var tree=plants.Find(p=>p.tree);tree.root.gameObject.SetActive(true);player.position=new Vector3(-18,0,-3);
        tree.root.position=Feet+Vector3.right*.75f;facing=1;heading=Vector3.right;FollowPlayerCamera(100);
        int loss=damageCount;yield return null;
        for(int i=0;i<3;i++){
            yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);
            check("Axe strike "+(i+1)+" changes tree without narration",tree.health==2-i&&NoActionNarration());
        }
        check("Felling still changes ecology once",damageCount==loss+1);
        RestartRun();tonePreviewFreezeAnimals=true;tree.health=0;tree.fall=1;player.position=tree.home;interactionTarget=null;FollowPlayerCamera(100);
        int care=careCount;yield return PressStoryKey(Key.E);
        check("Replant succeeds silently",tree.health==3&&tree.targetScale<.4f&&careCount==care+1&&NoActionNarration());
        DongbaLanguageCapture.Write(cam,folder+"/replant.png");
        yield return new WaitForSeconds(1.1f);player.position=tree.home;interactionTarget=null;int plantsBefore=plants.Count;
        yield return PressStoryKey(Key.E);yield return new WaitForSeconds(.25f);
        check("Local plant growth succeeds silently",plants.Count>plantsBefore&&tree.cultivated&&NoActionNarration());
        DongbaLanguageCapture.Write(cam,folder+"/cultivation.png");
        care=careCount;player.position=tree.home;interactionTarget=null;yield return PressStoryKey(Key.E);
        check("Repeated care reports only in target card",careCount==care&&promptPanel.activeSelf&&hudPrompt.text==DongbaLanguage.Text("Already tended")&&NoActionNarration());
        DongbaLanguageCapture.Write(cam,folder+"/limit-on-card.png");
        player.position=tree.home+Vector3.left*4;yield return null;yield return new WaitForEndOfFrame();
        check("Local limit does not follow a different target",interactionTarget!=tree&&(!promptPanel.activeSelf||hudPrompt.text!=DongbaLanguage.Text("Already tended"))&&NoActionNarration());
        RestartRun();tonePreviewFreezeAnimals=true;Being adult=null;
        foreach(var a in animals)if(FindLocal(a.root.position,.7f,1.35f,animals.Count+a.speciesIndex*17,true,out var unused)){adult=a;break;}
        check("Adult has room for young",adult!=null);
        if(adult!=null){
            player.position=adult.root.position;FollowPlayerCamera(100);int count=animals.Count;care=careCount;
            yield return PressStoryKey(Key.E);
            check("Feeding produces nearby young without narration",animals.Count==count+1&&careCount==care&&NoActionNarration());
            var child=animals[animals.Count-1];yield return PressStoryKey(Key.E);
            check("Feeding cooldown remains local",animals.Count==count+1&&hudPrompt.text==DongbaLanguage.Text("Still feeding")&&NoActionNarration());
            yield return new WaitForSeconds(1.6f);player.position=child.root.position;interactionTarget=null;yield return PressStoryKey(Key.E);
            check("Young animal grows silently",child.age==1&&careCount==care&&NoActionNarration());
            yield return new WaitForSeconds(1.6f);player.position=child.root.position;interactionTarget=null;yield return PressStoryKey(Key.E);
            check("Adulthood scores once without narration",child.age==2&&careCount==care+1&&NoActionNarration());
            ClearCareLimit();player.position=child.root.position+Vector3.left*.7f;FollowPlayerCamera(100);yield return new WaitForSeconds(.8f);
            DongbaLanguageCapture.Write(cam,folder+"/animals.png");
        }
        while(Balance<FlourishThreshold)RegisterCare();yield return new WaitForSeconds(3);
        check("Flourishing has no persistent explanatory caption",flourishAge>=0&&NoActionNarration());
        DongbaLanguageCapture.Write(cam,folder+"/flourishing.png");
        while(Balance>-10)RegisterDamage();yield return new WaitForSeconds(3);
        check("Decline remains visual without narration",ecologyVisual<0&&NoActionNarration());
        SetStoryModeEnabled(true);RestartRun();for(int i=0;i<FatherIntroLines.Length;i++)AdvanceFatherIntro();
        SpawnStoryBoar();journeyStage=JourneyStage.Boar;player.position=BoarClearing+Vector3.left*1.4f;
        storyFatherRoot.position=Feet+Vector3.back*.5f;FollowPlayerCamera(100);yield return new WaitForEndOfFrame();yield return null;yield return new WaitForEndOfFrame();
        check("Father help is speech rather than an action banner",boarAidSpoken&&forestSubtitles.SubtitleText.text.Contains("我来引住它")&&!uiNoticePanel.activeSelf);
        check("Spoken combat aside does not demand Enter",!forestSubtitleAdvance.gameObject.activeSelf&&!ManualStoryDialogueActive);
        yield return PressStoryKey(Key.J);check("Combat continues during spoken aside",attackAge>=0);
        yield return new WaitForSeconds(2.7f);
        storyBoar.root.position=Feet+Vector3.right*1.4f;storyFatherRoot.position=Feet+Vector3.back*.5f;boarAidReady=clock;
        yield return null;yield return new WaitForEndOfFrame();
        check("Repeated help stays mechanical without repeating speech",boarStun>0&&noticeTime<=0&&NoActionNarration());
        BeginManualStoryDialogue(RealizationLines);yield return null;yield return new WaitForEndOfFrame();
        check("Real dialogue and Enter hint remain",ManualStoryDialogueActive&&forestSubtitles.SubtitleText.gameObject.activeInHierarchy&&forestSubtitleAdvance.gameObject.activeSelf);
        RestartRun();yield return new WaitForEndOfFrame();
        check("Restart clears local status and speech memory",careLimitTarget==null&&!boarAidSpoken&&noticeTime<=0&&fatherIntroActive);
        File.AppendAllText(report,"Interaction positions and state values are staged; actions use real keyboard input.\n");
        Application.Quit(passed?0:1);
    }
}}
