using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator CheckCharacterPresentation(){
        const string folder="Verification/character-presentation";
        Directory.CreateDirectory(folder);var report=new List<string>();int failures=0;
        void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;File.WriteAllLines(folder+"/checks.txt",report);}
        yield return new WaitForSecondsRealtime(1);DongbaInterface.Instance?.Close(false);
        SetStoryModeEnabled(false);RestartRun();ResetForestSubtitles();tonePreviewFreezeAnimals=true;
        for(int kind=0;kind<AnimalKinds;kind++){
            var animal=animals.Find(b=>b.speciesIndex==kind);
            // The live layout may deliberately omit a library species. Instantiate
            // that resource only in this opt-in fixture to cover the whole catalog.
            if(animal==null){AddAnimal(new Vector3(-30+kind,0,-15),kind);animal=animals[animals.Count-1];}
            int age=animal.age;animal.age=2;
            DongbaLanguage.Set(DongbaLocale.Chinese,false);
            Check(AnimalDisplayName(animal)==dictionaryLabels["animal_"+kind.ToString("00")],"Dictionary name matches sprite "+kind+" / "+AnimalDisplayName(animal));
            DongbaLanguage.Set(DongbaLocale.English,false);
            Check(AnimalDisplayName(animal)==animalNamesEn[kind]&&!string.IsNullOrEmpty(AnimalDisplayName(animal)),"English species "+kind);
            animal.age=age;
        }
        DongbaLanguage.Set(DongbaLocale.Chinese,false);
        var cat=animals.Find(b=>b.speciesIndex==4);player.position=ForestSpawn;cat.root.position=Feet+Vector3.right*1.1f;
        FollowPlayerCamera(100);yield return new WaitForSeconds(.3f);yield return Application.isBatchMode?null:new WaitForEndOfFrame();
        Check(interactionTarget==cat&&hudPromptTitle.text=="猫","Nearby PC card identifies the actual cat");
        DongbaLanguageCapture.Write(cam,folder+"/cat-zh.png");
        cat.careReady=0;int count=animals.Count;Feed(cat);yield return null;
        var baby=animals[animals.Count-1];
        Check(animals.Count==count+1&&baby.speciesIndex==cat.speciesIndex&&AnimalDisplayName(baby)=="猫（幼兽）","Newborn retains the parent's dictionary name");
        cat.root.position=ForestSpawn+Vector3.right*4;baby.root.position=Feet+Vector3.right;
        yield return null;yield return Application.isBatchMode?null:new WaitForEndOfFrame();
        Check(interactionTarget==baby&&hudPromptTitle.text=="猫（幼兽）","Young species is readable in the real care card");
        DongbaLanguageCapture.Write(cam,folder+"/cat-young-zh.png");
        baby.careReady=0;Feed(baby);baby.careReady=0;Feed(baby);yield return null;
        Check(baby.age==2&&AnimalDisplayName(baby)=="猫","Growth removes juvenile qualifier, keeps species");
        DongbaLanguage.Set(DongbaLocale.English,false);yield return null;yield return Application.isBatchMode?null:new WaitForEndOfFrame();
        Check(hudPromptTitle.text=="Cat","Live language switch preserves target species");
        DongbaLanguageCapture.Write(cam,folder+"/cat-en.png");
        var donkey=animals.Find(b=>b.speciesIndex==8);donkey.root.position=baby.root.position;baby.root.position=ForestSpawn+Vector3.right*5;
        DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;yield return Application.isBatchMode?null:new WaitForEndOfFrame();
        Check(interactionTarget==donkey&&hudPromptTitle.text=="驴子","Donkey is not mislabeled as horse");
        DongbaLanguageCapture.Write(cam,folder+"/donkey-zh.png");
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        var keyboard=InputSystem.AddDevice<Keyboard>("CharacterMotionCheck");InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();
        // A real movement press starts the same animation in the normal Update loop.
        facing=1;playerTurn.Snap(1);yield return new WaitForSeconds(.6f);
        report.Add("Before input: current="+Keyboard.current.name+" enabled="+keyboard.enabled+" menu="+DongbaInterface.GameplayBlocked+" dialogue="+DialogueAdvanceInput.GameplayInputBlocked+" story="+fatherIntroActive+" narrative="+NarrativeInputLocked);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        report.Add("After A: facing="+facing+" turn="+playerTurn.Value+" dt="+Time.deltaTime+" same keyboard="+(Keyboard.current==keyboard)+" A="+keyboard.aKey.isPressed);
        Check(playerTurn.Width<.99f&&playerTurn.Width>.03f,"Reverse movement begins a continuous paper turn");
        var foot=Feet;float maxFootError=0;int intermediateFrames=0;
        for(int i=0;i<22;i++){yield return Application.isBatchMode?null:new WaitForEndOfFrame();if(playerTurn.Width<.98f)intermediateFrames++;maxFootError=Mathf.Max(maxFootError,Mathf.Abs(body.position.x-Feet.x));}
        Check(intermediateFrames>=3&&playerImage.flipX&&playerTurn.Width>.98f,"Turn completes without an instant sign flip");
        Check(maxFootError<.0001f&&Vector3.Distance(Feet,foot)<.0001f,"Turning alone cannot slide the foot anchor");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.J));yield return null;yield return null;
        report.Add("After D+J: facing="+facing+" turn="+playerTurn.Value+" attackAge="+attackAge+" J="+keyboard.jKey.isPressed+" block="+DialogueAdvanceInput.GameplayInputBlocked);
        Check(attackAge>=0&&swingFacing==1&&!playerImage.flipX&&playerTurn.Width==1,"Reverse plus attack completes facing before axe contact");
        Check(Vector3.Distance(axe.position,body.TransformPoint(PlayerHandLocalPosition()))<.0001f,"Axe grip remains attached after turn interruption");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.8f);
        for(int i=0;i<12;i++){InputSystem.QueueStateEvent(keyboard,new KeyboardState(i%2==0?Key.A:Key.D));yield return null;yield return null;}
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.45f);
        Check(!playerImage.flipX&&Mathf.Abs(playerTurn.Value-1)<.002f&&Mathf.Abs(body.localScale.x-playerUnturnedScale.x)<.002f,"Rapid alternating turns settle without cumulative shrink");
        SetStoryModeEnabled(true);RestartRun();SkipOpeningForVerification();ResetForestSubtitles();
        journeyStage=JourneyStage.Boar;tonePreviewFreezeAnimals=true;storyFatherRoot.position=Feet+Vector3.left*2;
        facing=1;fatherTurn.Snap(1);yield return new WaitForSeconds(.1f);facing=-1;yield return null;yield return null;
        Check(fatherTurn.Value<.99f&&fatherTurn.Value>-.99f,"Father uses a continuous turn in the active story");
        yield return new WaitForSeconds(.4f);
        Check(fatherTurn.Facing==-1&&fatherTurn.Width>.98f,"Father completes the matching turn without changing scale");
        // Capture normal-camera animation at a fixed time step for normal-speed review.
        Directory.CreateDirectory(folder+"/frames");Time.captureFramerate=30;player.position=ForestSpawn;
        for(int frame=0;frame<(Array.IndexOf(Environment.GetCommandLineArgs(),"--character-motion-preview")>=0?210:0);frame++){
            int phase=frame%60;Key key=phase<22?Key.D:phase<30?Key.None:phase<52?Key.A:Key.None;
            InputSystem.QueueStateEvent(keyboard,key==Key.None?new KeyboardState():new KeyboardState(key));
            yield return Application.isBatchMode?null:new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/frames/"+frame.ToString("D4")+".png",1280,720);
        }
        Time.captureFramerate=0;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.5f);
        float elapsed=0;int frames=0;while(elapsed<3){yield return null;elapsed+=Time.unscaledDeltaTime;frames++;}
        report.Add("Measured uncaptured PC FPS="+(frames/elapsed).ToString("F1"));
        SetStoryModeEnabled(false);RestartRun();yield return null;yield return Application.isBatchMode?null:new WaitForEndOfFrame();
        Check(playerTurn.Value==1&&!playerImage.flipX,"Restart restores full width and right-facing state");
        InputSystem.RemoveDevice(keyboard);File.WriteAllLines(folder+"/checks.txt",report);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(failures==0?0:1);
#else
        Application.Quit(failures==0?0:1);
#endif
    }
}}
