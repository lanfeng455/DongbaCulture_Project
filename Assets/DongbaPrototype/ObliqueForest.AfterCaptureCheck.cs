using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DongbaPrototype {public partial class ObliqueForest {
    bool afterCapturePreviewDone;
    IEnumerator PreviewAfterCapture(){yield return new WaitForSeconds(1);PrepareAfterCapturePreview();}
    void PrepareAfterCapturePreview(){SetStoryModeEnabled(true);RestartRun();for(int i=0;i<3;i++)AdvanceFatherIntro();while(Balance>-10)RegisterDamage();if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);storyOpeningState=StoryOpeningState.Captured;journeyStage=JourneyStage.Captured;}
    IEnumerator CheckAfterCapture(){
        yield return new WaitForSeconds(1);Directory.CreateDirectory("Verification/after-capture");
        PrepareAfterCapturePreview();
        File.WriteAllText("Verification/after-capture/checks.txt","Test starts at completed capture; prior hunt/capture covered by story-journey regression.\n");
        Time.captureFramerate=30;StartCoroutine(RecordAfterCapture());
        var dropped=new bool[6];var withdrawn=new bool[6];bool rodConnected=true;
        float timeout=0;bool dayShot=false,nightShot=false,realizationShot=false,standingDuringWait=true;var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
        while(journeyStage!=JourneyStage.EastJourney&&timeout<30){
            if(journeyStage==JourneyStage.WaitingAtHearth){
                standingDuringWait&=playerImage.enabled&&playerImage.sprite==Get("player_glyph");
                int skyBeat=Mathf.Clamp(waitingBeat,0,5);float skyPhase=Mathf.Repeat(waitingAge,HearthHalfDaySeconds)/HearthHalfDaySeconds;float skyY=((RectTransform)storyTimeSymbols.transform).anchoredPosition.y;
                if(skyPhase>.30f&&skyPhase<.70f)dropped[skyBeat]|=Mathf.Abs(skyY+140)<.1f;
                if(skyPhase>.98f)withdrawn[skyBeat]|=skyY>130;
                if(waitingBeat>=0)rodConnected&=storySkyRod!=null&&storySkyRod.rectTransform.sizeDelta.y>=400&&Mathf.Abs(storySkyRod.rectTransform.anchoredPosition.x-((skyBeat&1)==1?3.2f:-.2f))<.01f;
                if(!dayShot&&waitingBeat==0&&waitingAge>.9f){CaptureSceneToTexture("Verification/after-capture/day.png");dayShot=true;}
                if(!nightShot&&waitingBeat==1&&nightAmount>.95f){CaptureSceneToTexture("Verification/after-capture/night.png");nightShot=true;}
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
            }
            if(journeyStage==JourneyStage.Realization&&realizationAge>.7f&&!realizationShot){CaptureSceneToTexture("Verification/after-capture/realization.png");realizationShot=true;}
            timeout+=Time.deltaTime;yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        bool stayedAtHearth=Vector3.Distance(Feet,ForestSpawn)<.15f;bool sixPuppets=true;for(int i=0;i<6;i++)sixPuppets&=dropped[i]&&withdrawn[i];
        File.AppendAllText("Verification/after-capture/checks.txt","Six day/night props descend hold and retract="+sixPuppets+"\nSuspension rod connected="+rodConnected+"\n");
        File.AppendAllText("Verification/after-capture/checks.txt","Waiting keeps original standing glyph="+standingDuringWait+"\nSix day/night beats="+(waitingBeatsShown==6)+"; beats="+waitingBeatsShown+"\nWaiting locks held movement="+stayedAtHearth+"\nSun/night screenshots="+(dayShot&&nightShot)+"\n");
        Vector3 before=Feet;yield return new WaitForSeconds(.7f);File.AppendAllText("Verification/after-capture/checks.txt","East journey waits for player input="+(Vector3.Distance(before,Feet)<.02f&&journeyStage==JourneyStage.EastJourney)+"\n");
        float travel=0;bool jumped=false,retreatShot=false,blankShot=false,blankClean=false;int entries=0;RescueScrollEntered+=()=>entries++;
        while(journeyStage!=JourneyStage.ScrollWalk&&travel<30){
            KeyboardState keys;
            if(Feet.z<-.15f&&!InScrollWorld)keys=new KeyboardState(Key.W);
            else if(!jumped&&Feet.x>RiverX(Feet.z)-1.65f&&Feet.x<RiverX(Feet.z)&&jumpAge<0){keys=new KeyboardState(Key.D,Key.Space);jumped=true;}
            else keys=new KeyboardState(Key.D);
            InputSystem.QueueStateEvent(keyboard,keys);
            if(journeyStage==JourneyStage.EastJourney&&eastPaperProgress>.45f&&!retreatShot){yield return new WaitForEndOfFrame();CaptureSceneToTexture("Verification/after-capture/forest-retreat.png");retreatShot=true;}
            if(journeyStage==JourneyStage.EastJourney&&Feet.x>36&&!blankShot){
                yield return new WaitForEndOfFrame();CaptureSceneToTexture("Verification/after-capture/blank-paper.png");blankShot=true;blankClean=playerImage.enabled&&eastPaperProgress>=.999f;bool noHiddenRoots=true;foreach(var tree in plants)if(tree.tree&&tree.home.x>=EastFadeEnd&&tree.home.x<44&&Mathf.Abs(tree.home.z)<20)noHiddenRoots&=Free(tree.home);File.AppendAllText("Verification/after-capture/checks.txt","Blank paper removes hidden root collisions="+noHiddenRoots+"\n");
                foreach(var image in retreatSprites)if(image!=null&&image.enabled&&image.color.a>.001f)blankClean=false;
                foreach(var graphic in retreatGraphics)if(graphic!=null&&graphic.gameObject.activeInHierarchy&&graphic.color.a>.001f)blankClean=false;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;var stopped=Feet;yield return new WaitForSeconds(.45f);
                File.AppendAllText("Verification/after-capture/checks.txt","Blank paper still waits for movement="+(journeyStage==JourneyStage.EastJourney&&Vector3.Distance(stopped,Feet)<.02f)+"\n");
            }
            travel+=Time.deltaTime;yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        yield return new WaitForSeconds(.25f);CaptureSceneToTexture("Verification/after-capture/scroll.png");
        var screen=cam.WorldToViewportPoint(playerImage.bounds.center);File.AppendAllText("Verification/after-capture/checks.txt","Scroll player visible="+(playerImage.enabled&&screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1&&scrollWorld.GetComponentInChildren<MeshRenderer>().sortingOrder<playerImage.sortingOrder)+"\n");
        File.AppendAllText("Verification/after-capture/checks.txt","Forest fades and blank paper contains only player="+(retreatShot&&blankShot&&blankClean)+"\nNo entrance prop="+(GameObject.Find("Rescue scroll entrance / ordinary rolled paper, not a Dongba glyph")==null)+"\nKeyboard-controlled east walk reaches scroll="+(journeyStage==JourneyStage.ScrollWalk)+"; travel="+travel+"; feet="+Feet+"\nJumped across river="+jumped+"\nSingle scroll handoff="+(entries==1)+"\n");
        float oldX=Feet.x;for(int i=0;i<30;i++){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        File.AppendAllText("Verification/after-capture/checks.txt","Scroll remains player controlled="+(Feet.x>oldX+1)+"\n");
        afterCapturePreviewDone=true;yield return new WaitForEndOfFrame();SetStoryModeEnabled(false);yield return null;
        File.AppendAllText("Verification/after-capture/checks.txt","Switching from scroll to Sandbox returns to forest="+(!StoryModeEnabled&&Vector3.Distance(Feet,ForestSpawn)<.1f&&!InScrollWorld&&!scrollFrame.activeSelf)+"\n");
        SetStoryModeEnabled(true);RestartRun();yield return null;
        File.AppendAllText("Verification/after-capture/checks.txt","Restart clears montage and scroll="+(journeyStage==JourneyStage.Hunting&&fatherIntroActive&&!InScrollWorld&&nightAmount==0&&storyCurtain==0&&playerImage.enabled&&eastPaperProgress==0&&!retreatApplied&&!scrollFrame.activeSelf)+"\n");
        fatherIntroActive=false;journeyStage=JourneyStage.EastJourney;EnsureEastPaperContinuation();player.position=new Vector3(36,0,0);yield return null;yield return new WaitForEndOfFrame();var observed=plants[0].renderer;bool hadFade=eastPaperProgress>.99f&&observed.color.a<.001f;RestartRun();yield return null;
        File.AppendAllText("Verification/after-capture/checks.txt","Restart during blank paper restores ink="+(hadFade&&observed.color.a>.99f&&eastPaperProgress==0&&!retreatApplied)+"\n");
        SetStoryModeEnabled(false);yield return null;File.AppendAllText("Verification/after-capture/checks.txt","Sandbox clears overlays="+(!storyTimeSymbols.activeSelf&&!scrollFrame.activeSelf&&nightAmount==0)+"\n");Application.Quit();
    }
    IEnumerator RecordAfterCapture(){
        var folder="Verification/after-capture/frames";Directory.CreateDirectory(folder);int frame=0;
        while(!afterCapturePreviewDone&&frame<1800){yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/"+(frame++).ToString("D4")+".png",960,540);}
        Time.captureFramerate=0;
    }
}}
