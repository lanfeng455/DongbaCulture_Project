using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DongbaPrototype { public partial class ObliqueForest {
    IEnumerator VerifyNatureWelcome(Action<bool,string> check){
        StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);yield return null;
        check(!natureWelcomePending&&!natureFirstComplete&&!natureWelcome.gameObject.activeSelf,"Awakening: fresh forest has no premature announcement");
        foreach(var p in plants.ToArray()){if(p.tree&&!p.cultivated)GrowAround(p);if(Balance>=FlourishThreshold-1)break;}
        yield return null;check(!natureUnlocked&&!natureWelcomePending,"Awakening: does not appear before actual flourishing");
        var finalCare=plants.First(p=>p.tree&&!p.cultivated);GrowAround(finalCare);yield return null;yield return null;
        int score=Balance;int count=plants.Count;
        check(natureUnlocked&&NatureWelcomeVisible&&natureWelcome.gameObject.activeSelf&&!NatureEditorOpen,"Awakening: visible without finding or opening the editor");
        check(natureWelcomeCount==1&&forestSounds.ContainsKey("grow"),"Awakening: one response moment with an existing care sound");
        yield return new WaitForSeconds(.65f);
        check(natureAnswerWinds.Any(w=>w.transform.parent.gameObject.activeSelf),"Awakening: nearby existing wind glyphs visibly answer");
        check(natureAnswerTrees.Where(t=>t!=null).All(t=>t.root.position==t.home),"Awakening: responding tree roots do not slide");
        Directory.CreateDirectory("Verification/nature-welcome");
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;UpdateNatureUI();
        if(!XRTrackingControlsCamera)DongbaLanguageCapture.Write(cam,"Verification/nature-welcome/unlock-zh.png");
        else CaptureSceneToTexture("Verification/nature-welcome/unlock-mr.png");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;UpdateNatureUI();
        check(natureWelcomeTitle.preferredHeight<=natureWelcomeTitle.rectTransform.rect.height&&natureWelcomeCopy.preferredHeight<=natureWelcomeCopy.rectTransform.rect.height,"Awakening: English title and explanation fit their text bounds");
        check(natureWelcomeAcceptText.text.Contains(XRTrackingControlsCamera?"A   ":"B   "),"Awakening: primary action matches the current device");
        if(!XRTrackingControlsCamera)DongbaLanguageCapture.Write(cam,"Verification/nature-welcome/unlock-en.png");
        yield return new WaitForSeconds(5);
        check(NatureWelcomeVisible&&natureWelcomeFade.alpha>.99f,"Awakening: announcement waits for the player instead of timing out");
        if(!XRTrackingControlsCamera){
            DongbaInterface.Instance.Open(DongbaInterface.Page.Pause);yield return null;UpdateNatureUI();
            check(!natureWelcome.gameObject.activeSelf&&natureWelcomePending,"Awakening: pause hides presentation without losing it");
            DongbaInterface.Instance.Close(false);yield return null;
            var rect=(RectTransform)natureWelcomeLater.transform;yield return NatureClick(RectTransformUtility.WorldToScreenPoint(forestCanvas.worldCamera,rect.TransformPoint(rect.rect.center)));
        }else{
            var ray=new Ray(Feet+Vector3.up*30,Vector3.down);ProcessNatureXR(false,false,false,false,Vector2.zero,ray,true);ProcessNatureXR(false,true,false,false,Vector2.zero,ray,true);
        }
        yield return null;UpdateNatureUI();
        check(!natureWelcomePending&&!NatureEditorOpen&&natureTab.gameObject.activeSelf&&natureTab.sizeDelta.x>=370,"Awakening: Later keeps a prominent NEW entrance");
        damageCount++;yield return null;damageCount--;yield return null;
        check(natureUnlocked&&natureWelcomeCount==1&&!natureWelcomePending,"Awakening: crossing the threshold again never repeats the announcement");
        if(!XRTrackingControlsCamera)yield return NatureKey(Key.B);
        else {var ray=new Ray(Feet+Vector3.up*30,Vector3.down);ProcessNatureXR(false,false,false,false,Vector2.zero,ray,true);ProcessNatureXR(true,false,false,false,Vector2.zero,ray,true);yield return null;}
        check(NatureEditorOpen&&natureFirstLesson&&naturePage==NaturePage.Plants,"Awakening: one shortcut starts at the first glyph choice");
        if(!XRTrackingControlsCamera)yield return NatureClickButton(1);else {natureFocus=1;var ray=new Ray(Feet+Vector3.up*30,Vector3.down);ProcessNatureXR(false,false,false,false,Vector2.zero,ray,true);ProcessNatureXR(false,false,true,false,Vector2.zero,ray,true);yield return null;}
        check(naturePage==NaturePage.Position&&natureKey=="plant_01","Awakening: selected glyph leads to a ground placement step");
        CancelNatureEditor();yield return null;
        check(plants.Count==count&&!natureFirstComplete,"Awakening: cancelling the lesson creates nothing and does not mark completion");
        NatureGo(NaturePage.Plants);yield return null;
        if(!XRTrackingControlsCamera)yield return NatureClickButton(1);else NatureChoose(NatureAction.Plant,null,"plant_01");
        Vector3 target=NatureTestSpot(Feet+Vector3.left*3,null,false,true);
        if(!XRTrackingControlsCamera)yield return NatureClick(cam.WorldToScreenPoint(target));
        else {var ray=new Ray(target+Vector3.up*30,Vector3.down);ProcessNatureXR(false,false,false,false,Vector2.zero,ray,true);ProcessNatureXR(false,false,true,false,Vector2.zero,ray,true);yield return null;}
        var added=plants.Last();
        check(plants.Count==count+1&&!natureFirstComplete&&natureFirstResponding,"Awakening: confirmation starts a real response, not an instant completion toast");
        check(UndoNature(),"Awakening: a first invitation can be undone before arrival");yield return NatureWait(added);
        check(plants.Count==count&&!natureFirstComplete&&!natureFirstResponding,"Awakening: early undo leaves no endless waiting lesson");
        NatureChoose(NatureAction.Plant,null,"plant_01");natureCandidate=target;natureHasGround=true;UpdateNatureUI();CommitNatureChoice();added=plants.Last();
        yield return NatureWait(added);yield return null;
        check(natureFirstComplete&&!natureFirstLesson&&(XRTrackingControlsCamera?NatureEditorOpen&&!natureWelcome.gameObject.activeSelf&&natureMessageUntil>clock:natureWelcome.gameObject.activeSelf),"Awakening: completion feedback appears after settling without overlapping the MR editor");
        check(Balance==score&&natureWelcomeCount==1,"Awakening: welcome and lesson do not award ecology or duplicate objects");
        yield return new WaitForSeconds(.65f);
        if(!XRTrackingControlsCamera)DongbaLanguageCapture.Write(cam,"Verification/nature-welcome/first-answer.png");
        check(UndoNature(),"Awakening: first invitation is undoable");yield return NatureWait(added);
        check(plants.Count==count&&natureFirstComplete,"Awakening: undo removes the addition without replaying onboarding");
        StartForestMode(true);yield return null;UpdateNatureUI();
        check(!natureWelcomePending&&!natureFirstComplete&&!natureWelcome.gameObject.activeSelf&&!natureTab.gameObject.activeSelf,"Awakening: switching to story removes every onboarding element");
        check(!ForestCareAvailable&&ForestDamageAvailable,"Awakening: original story interaction restrictions preserved");
        if(XRTrackingControlsCamera){
            StartForestMode(false);yield return null;careCount=FlourishThreshold;yield return null;
            var ray=new Ray(Feet+Vector3.up*30,Vector3.down);
            ProcessNatureXR(false,false,false,false,Vector2.zero,ray,true);ProcessNatureXR(false,false,true,false,Vector2.zero,ray,true);
            float beforeJump=jumpAge;ProcessXRInput(Vector2.zero,true,false,false,false,false,false);
            check(NatureEditorOpen&&naturePage==NaturePage.Plants&&!natureWelcomePending&&jumpAge==beforeJump,"Awakening MR: A accepts the visible invitation without jumping");
            ProcessNatureXR(false,false,true,false,Vector2.zero,ray,true);
            check(naturePage==NaturePage.Plants,"Awakening MR: holding A does not also select a glyph");
        }
        StartForestMode(false);yield return null;
    }
    IEnumerator PreviewNatureWelcome(){
        yield return new WaitForSeconds(1);StartForestMode(false);DongbaInterface.Instance?.Close(false);ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        player.position=NatureDemoFooting();lastDryPosition=Feet;FollowPlayerCamera(100);
        foreach(var tree in plants.ToArray()){if(tree.tree&&!tree.cultivated)GrowAround(tree);if(Balance>=FlourishThreshold-1)break;}
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return new WaitForSeconds(1);
        string folder="Verification/nature-welcome";Directory.CreateDirectory(folder+"/frames");
        int savedRate=Time.captureFramerate;Time.captureFramerate=30;
        var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try{
            for(int frame=0;frame<600;frame++){
                if(frame==30)GrowAround(plants.First(t=>t.tree&&!t.cultivated));
                if(frame==180)BeginNatureFirstInvitation();
                if(frame==245){NatureChoose(NatureAction.Plant,null,"plant_01");var target=NatureTestSpot(Feet+Vector3.left*3,null,false,true);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=cam.WorldToScreenPoint(target)});}
                if(frame==300)CommitNatureChoice();
                if(frame==455)CloseNatureEditor();
                if(frame==525)ToggleNatureEditor();
                yield return null;yield return new WaitForEndOfFrame();
                texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply(false);File.WriteAllBytes(folder+"/frames/"+frame.ToString("D4")+".jpg",texture.EncodeToJPG(88));
            }
        }finally{Time.captureFramerate=savedRate;Destroy(texture);}
        Application.Quit();
    }
}}
