using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    IEnumerator HoldForestAudioKey(Key key,float duration){var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return new WaitForSeconds(duration);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
    int AudioCount(string prefix){int n=0;foreach(var pair in forestSoundEvents)if(pair.Key.StartsWith(prefix,StringComparison.Ordinal))n+=pair.Value;return n;}
    void AudioAssert(string name,bool ok){File.AppendAllText("Verification/audio-redesign-20261122/runtime/checks.txt",name+"="+ok+"\n");if(!ok)throw new InvalidOperationException("Forest audio check: "+name);}
    IEnumerator CheckForestAudio(){
        Directory.CreateDirectory("Verification/audio-redesign-20261122/runtime");File.WriteAllText("Verification/audio-redesign-20261122/runtime/checks.txt","");
        Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.AddDevice<Keyboard>("AudioCheckKeyboard").MakeCurrent();
        yield return new WaitForSecondsRealtime(1);AudioListener.volume=1;AudioListener.pause=false;SetStoryModeEnabled(false);RestartRun();
        Screen.SetResolution(1598,898,FullScreenMode.Windowed);yield return new WaitForSeconds(.2f);Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);
        var capture=cam.gameObject.AddComponent<ForestAudioCapture>();capture.Begin("Verification/audio-redesign-20261122/runtime",false);
        AudioAssert("All 31 Foley clips loaded",forestSounds.Count==31&&forestSounds.Values.All(c=>c.samples>0&&c.loadState==AudioDataLoadState.Loaded));
        player.position=ForestSpawn;FollowPlayerCamera(100);yield return new WaitForSeconds(.5f);
        int idle=AudioCount("step_");yield return new WaitForSeconds(.6f);AudioAssert("Idle has no footsteps",AudioCount("step_")==idle);
        int downswing=AudioCount("axe_");BeginAxeAttack();yield return new WaitForSeconds(.07f);AudioAssert("Windup silent before downswing",AudioCount("axe_")==downswing);yield return new WaitForSeconds(.26f);AudioAssert("One swing at downswing",AudioCount("axe_")==downswing+1);yield return new WaitForSeconds(.5f);
        int steps=AudioCount("step_");yield return HoldForestAudioKey(Key.D,.65f);
        AudioAssert("Keyboard movement emits steps",AudioCount("step_")>steps);
        int swing=AudioCount("axe_"),jumps=AudioCount("jump"),lands=AudioCount("land");
        yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.7f);
        yield return PressStoryKey(Key.Space);yield return new WaitForSeconds(1);
        AudioAssert("Keyboard swing jump landing",AudioCount("axe_")>swing&&AudioCount("jump")>jumps&&AudioCount("land")>lands);
        var tree=plants.Find(t=>t.tree);player.position=tree.home-Vector3.right*.7f;heading=Vector3.right;FollowPlayerCamera(100);yield return null;
        int wood=AudioCount("wood_"),fall=AudioCount("tree_fall");
        ApplyEcologyHit(tree);yield return new WaitForSeconds(.15f);ApplyEcologyHit(tree);yield return new WaitForSeconds(.15f);ApplyEcologyHit(tree);
        AudioAssert("Three chops yield two hits and one fall",AudioCount("wood_")==wood+2&&AudioCount("tree_fall")==fall+1);
        yield return new WaitForSeconds(1.5f);int plant=AudioCount("plant"),grow=AudioCount("grow");interactionTarget=null;Care();
        AudioAssert("Replant emits success",AudioCount("plant")==plant+1);
        Care();AudioAssert("Care cooldown silent",AudioCount("grow")==grow);
        var sample=animals[0];sample.age=0;sample.careReady=0;player.position=sample.root.position;FollowPlayerCamera(100);yield return null;
        int fed=AudioCount("feed");Feed(sample);Feed(sample);
        AudioAssert("Feeding emits once and cooldown silent",AudioCount("feed")==fed+1);
        int hit=AudioCount("hit_"),down=AudioCount("animal_down");ApplyEcologyHit(sample);ApplyEcologyHit(sample);sample.root.gameObject.SetActive(false);
        AudioAssert("Animal hit and death distinct",AudioCount("hit_")==hit+1&&AudioCount("animal_down")==down+1);
        AudioAssert("Death tail survives hidden animal",forestVoices.Any(v=>v.key=="animal_down"&&v.source.isPlaying));
        yield return new WaitForSeconds(1);
        int hurt=AudioCount("player_hurt");var attacker=animals[1];attacker.root.position=Feet;attacker.angry=true;attacker.cooldown=damageCooldown=0;attacker.follow=0;attacker.migrationState=0;
        UpdateAnimalEcology(attacker,0,0);AudioAssert("Actual animal attack emits player hurt",AudioCount("player_hurt")==hurt+1);
        yield return new WaitForSeconds(1);
        // Verify the real listener output, not only the event counters.
        float[] output=new float[1024];float energy=0;
        PlayForestSfx("tree_fall",Feet);
        for(int i=0;i<20;i++){yield return new WaitForSeconds(.025f);AudioListener.GetOutputData(output,0);foreach(float s in output)energy+=s*s;}
        AudioAssert("Listener receives nonzero mixed audio",energy>.00001f);
        float audible=ForestAudibility(Feet);AudioAssert("Elevated camera retains nearby audio",audible>.8f);
        AudioAssert("Left right pan distinct",ForestPan(ForestAudioFocus()+Vector3.left*4)<0&&ForestPan(ForestAudioFocus()+Vector3.right*4)>0);
        int before=forestSoundEvents.Values.Sum();ForestSoundEnabled=false;PlayForestSfx("plant",Feet);yield return null;
        AudioAssert("Mute stops effects and loops",forestSoundEvents.Values.Sum()==before&&forestVoices.All(v=>!v.source.isPlaying)&&!forestWind.isPlaying&&!forestRiver.isPlaying);
        ForestSoundEnabled=true;yield return new WaitForSeconds(.2f);
        enabled=false;AudioAssert("Component disable silences owned sources",forestVoices.All(v=>!v.source.isPlaying)&&!forestWind.isPlaying&&!forestRiver.isPlaying);enabled=true;
        yield return new WaitForSeconds(.2f);AudioAssert("Component reenable resumes ambience",forestWind.isPlaying&&forestRiver.isPlaying);
        RestartRun();AudioAssert("Restart clears transients",forestVoices.All(v=>!v.source.isPlaying)&&!audioGrabbed&&!audioCarried);
        // Set up the real confrontation, then use its normal dialogue and capture logic.
        SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;knifeDropPoint=BoarClearing;
        player.position=DeparturePoint;storyFatherRoot.position=knifeDropPoint+Vector3.left*.5f;
        journeyStage=JourneyStage.ReturnForKnife;storyOpeningState=StoryOpeningState.CaptureBeat;returnWait=0;
        droppedKnife=Billboard("Audio test dropped knife",null,Get("father_knife"),.2f);droppedKnife.position=knifeDropPoint;
        int grabbed=AudioCount("shu_grab"),carried=AudioCount("shu_depart");
        float elapsed=0;while(journeyStage!=JourneyStage.Captured&&elapsed<15){elapsed+=Time.deltaTime;if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return null;}
        AudioAssert("Real capture emits grab and departure once",journeyStage==JourneyStage.Captured&&AudioCount("shu_grab")==grabbed+1&&AudioCount("shu_depart")==carried+1);
        yield return CheckForestAudioMixRules();
        yield return capture.Finish();Destroy(capture);
        File.AppendAllText("Verification/audio-redesign-20261122/runtime/checks.txt","Listener energy="+energy+"\nPASS\n");Application.Quit();
    }
    IEnumerator CheckForestAudioMixRules(){
        SetStoryModeEnabled(false);RestartRun();yield return new WaitForSeconds(.3f);
        int far=AudioCount("wood_");PlayForestVariant("wood",3,ForestAudioFocus()+Vector3.right*50);AudioAssert("Offscreen distant impacts inaudible",AudioCount("wood_")==far);
        string previous="";bool repeats=false;
        for(int i=0;i<8;i++){PlayForestVariant("step",4,Feet);string current=forestVoices.Where(v=>v.source.isPlaying&&v.key.StartsWith("step_")).OrderByDescending(v=>v.started).First().key;repeats|=current==previous;previous=current;yield return new WaitForSeconds(.36f);}
        AudioAssert("Recorded footstep takes never repeat consecutively",!repeats);
        int duplicates=AudioCount("plant");for(int i=0;i<16;i++)PlayForestSfx("plant",Feet);AudioAssert("Duplicate event coalesced",AudioCount("plant")==duplicates+1);
        yield return new WaitForSeconds(.6f);int voices=forestVoices.Count;
        for(int i=0;i<20;i++)PlayForestSfx("wood_"+(i%3),Feet+Vector3.right*(i%5)*.7f);
        AudioAssert("Impact polyphony capped",forestVoices.Count(v=>v.source.isPlaying&&v.group==ForestSoundGroup.Impact)<=4&&forestVoices.Count==voices);
        AudioAssert("Story cue retains reserved voice",PlayForestSfx("shu_grab",Feet));yield return new WaitForSeconds(1);
        var ui=DongbaInterface.Instance;ui.Open(DongbaInterface.Page.Pause);yield return new WaitForSecondsRealtime(.2f);int sample=forestRiver.timeSamples;int count=forestSoundEvents.Values.Sum();yield return new WaitForSecondsRealtime(.3f);
        AudioAssert("Pause freezes loop playhead and triggers",AudioListener.pause&&Math.Abs(forestRiver.timeSamples-sample)<2048&&forestSoundEvents.Values.Sum()==count);ui.Close(false);yield return new WaitForSeconds(.25f);AudioAssert("Resume continues ambience",forestRiver.isPlaying&&!AudioListener.pause);
        float baseWind=forestWind.volume;fatherIntroActive=true;yield return new WaitForSeconds(1);AudioAssert("Dialogue gently ducks ambience",forestWind.volume<baseWind*.90f);fatherIntroActive=false;yield return new WaitForSeconds(1.5f);AudioAssert("Ambience recovers after dialogue",forestWind.volume>baseWind*.90f);
        int teleport=AudioCount("step_");player.position+=Vector3.right*6;yield return null;AudioAssert("Teleport does not stamp footsteps",AudioCount("step_")==teleport);
        SpawnStoryBoar();storyBoar.root.position=Feet+Vector3.right*3;yield return null;int rush=AudioCount("boar_rush");
        for(int i=0;i<30;i++){storyBoar.root.position+=Vector3.forward*.03f;yield return null;}
        AudioAssert("Ordinary boar movement does not trigger charge",AudioCount("boar_rush")==rush);
        float minPitch=forestVoices.Where(v=>v.source.clip!=null).Min(v=>v.source.pitch),maxPitch=forestVoices.Where(v=>v.source.clip!=null).Max(v=>v.source.pitch);AudioAssert("Pitch variation stays within 1.5 percent",minPitch>=.985f&&maxPitch<=1.015f);
    }
    IEnumerator PreviewForestAudio(){
        Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.AddDevice<Keyboard>("AudioPreviewKeyboard").MakeCurrent();
        yield return new WaitForSecondsRealtime(1);AudioListener.volume=1;SetStoryModeEnabled(false);RestartRun();
        Screen.SetResolution(1598,898,FullScreenMode.Windowed);yield return new WaitForSeconds(.2f);Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return new WaitForSeconds(.4f);
        var tree=plants.Where(t=>t.tree&&t.home.x<-5&&Mathf.Abs(t.home.z)<8).OrderBy(t=>Vector3.Distance(t.home,ForestSpawn)).First();
        player.position=tree.home+Vector3.left*2.8f;lastDryPosition=Feet;facing=1;heading=Vector3.right;
        var animal=animals[0];animal.root.position=animal.home=tree.home+new Vector3(-4,0,-.3f);animal.age=0;animal.careReady=0;animal.protectedUntil=clock+60;animal.route.Clear();MomentFor(animal).pauseUntil=clock+60;
        FollowPlayerCamera(100);yield return new WaitForSeconds(.6f);
        var capture=cam.gameObject.AddComponent<ForestAudioCapture>();capture.Begin("Verification/audio-redesign-20261122/preview",true);
        yield return new WaitForSeconds(2);yield return HoldForestAudioKey(Key.D,.45f);yield return new WaitForSeconds(.4f);
        for(int i=0;i<3;i++){yield return PressStoryKey(Key.J);yield return new WaitForSeconds(.9f);}
        yield return new WaitForSeconds(1.2f);yield return PressStoryKey(Key.E);yield return new WaitForSeconds(2);
        yield return HoldForestAudioKey(Key.A,.75f);yield return new WaitForSeconds(.3f);yield return PressStoryKey(Key.E);yield return new WaitForSeconds(1.8f);yield return PressStoryKey(Key.E);yield return new WaitForSeconds(2);
        yield return HoldForestAudioKey(Key.S,.65f);yield return PressStoryKey(Key.Space);yield return HoldForestAudioKey(Key.A,.5f);yield return new WaitForSeconds(2);
        yield return capture.Finish();Destroy(capture);
        File.WriteAllText("Verification/audio-redesign-20261122/preview/events.txt",string.Join("\n",forestSoundEvents.Select(p=>p.Key+"="+p.Value)));
        Application.Quit();
    }
}}
