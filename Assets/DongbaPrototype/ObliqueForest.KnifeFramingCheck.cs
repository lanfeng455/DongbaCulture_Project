using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    bool KnifeActorInFrame(SpriteRenderer image){
        var b=image.bounds;
        for(int i=0;i<8;i++){
            var p=cam.WorldToViewportPoint(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));
            if(p.z<=0||p.x<.08f||p.x>.92f||p.y<.24f||p.y>.9f)return false;
        }
        return true;
    }
    IEnumerator CheckKnifeFraming(){
        const string folder="Verification/playtest-fixes-20261123/knife-hud";
        Directory.CreateDirectory(folder);File.WriteAllText(folder+"/checks.txt","");bool passed=true;
        Action<string,bool> check=(name,ok)=>{passed&=ok;File.AppendAllText(folder+"/checks.txt",(ok?"PASS ":"FAIL ")+name+"\n");};
        yield return new WaitForSeconds(1);Screen.SetResolution(1598,898,false);yield return new WaitForSeconds(.2f);Screen.SetResolution(1600,900,false);yield return new WaitForSeconds(.4f);
        DongbaLanguage.Set(DongbaLocale.Chinese,false);SetStoryModeEnabled(true);
        var points=new[]{new Vector3(43.5f,0,19.5f),new Vector3(43.5f,0,-19.5f),new Vector3(7,0,19.5f),new Vector3(-43.5f,0,-19.5f)};
        for(int i=0;i<points.Length;i++){
            RestartRun();SkipOpeningForVerification();ResetForestSubtitles();tonePreviewFreezeAnimals=true;
            journeyStage=JourneyStage.Boar;SpawnStoryBoar();boarStun=100;storyBoar.root.position=points[i];
            player.position=points[i]+Vector3.left*2;storyFatherRoot.position=points[i]+Vector3.left*.6f;
            for(int hit=0;hit<6;hit++)ApplyEcologyHit(storyBoar);
            storyBoar.root.gameObject.SetActive(false); // Same post-kill visibility as AxeContact.
            yield return null;yield return null;
            check("Kill event "+i+" drops knife at exact death point",droppedKnife!=null&&Vector3.Distance(droppedKnife.position,points[i])<.001f);
            while(ManualStoryDialogueActive)AdvanceManualStoryDialogue();ResetForestSubtitles();
            // Frame fixture: skip the long walk back, not the actual kill/drop or confrontation.
            player.position=DeparturePoint;storyFatherRoot.position=i==1?new Vector3(-7,0,points[i].z):points[i]+Vector3.left*.6f;
            journeyStage=JourneyStage.ReturnForKnife;storyOpeningState=StoryOpeningState.CaptureBeat;returnWait=0;
            droppedKnife.gameObject.SetActive(true);
            float travelUntil=Time.realtimeSinceStartup+65,travelReport=0;bool jumped=false;
            while(!shuConfrontationStarted&&Time.realtimeSinceStartup<travelUntil){
                jumped|=fatherJumpAge>=0;
                if(i==1&&Time.realtimeSinceStartup>=travelReport){travelReport=Time.realtimeSinceStartup+10;File.AppendAllText(folder+"/checks.txt","ROUTE father="+storyFatherRoot.position+" goal="+knifeDropPoint+" index="+storyFather.routeIndex+" count="+storyFather.route.Count+" next="+(HasAnimalRoute(storyFather)?storyFather.route[storyFather.routeIndex].ToString():"none")+" jump="+fatherJumpAge+"\n");}
                yield return null;
            }
            if(i==1)check("Father walks from west bank and jumps river to actual edge death site",shuConfrontationStarted&&jumped);
            yield return new WaitForSeconds(3);
            check("Edge "+i+" knife / father / Shu safely above subtitles",shuConfrontationStarted&&KnifeActorInFrame(droppedKnife.GetComponent<SpriteRenderer>())&&KnifeActorInFrame(storyFatherImage)&&KnifeActorInFrame(storyShu.GetComponent<SpriteRenderer>()));
            check("Edge "+i+" drop never moved to clearing",Vector3.Distance(knifeDropPoint,points[i])<.001f&&Vector3.Distance(droppedKnife.position,points[i])<.001f);
            check("Edge "+i+" paper backing and blood active",knifeShotPaper.activeSelf&&knifeGroundBlood.gameObject.activeInHierarchy);
            yield return new WaitForEndOfFrame();CaptureSceneToTexture(folder+"/edge-"+i+".png",1600,900);
            if(i==0){
                float until=Time.realtimeSinceStartup+18;
                while(journeyStage==JourneyStage.ReturnForKnife&&Time.realtimeSinceStartup<until){if(ManualStoryDialogueActive)AdvanceManualStoryDialogue();yield return new WaitForSeconds(.15f);}
                check("Framed abduction still exits camera before hiding",journeyStage==JourneyStage.Captured&&!storyFatherRoot.gameObject.activeSelf&&!storyShu.gameObject.activeSelf);
                bool heldPaper=true;until=Time.realtimeSinceStartup+8;
                while(journeyStage==JourneyStage.Captured&&Time.realtimeSinceStartup<until){heldPaper&=knifeShotPaper.activeSelf;yield return null;}
                check("Paper remains through capture until black-screen hearth handoff",heldPaper&&journeyStage==JourneyStage.WaitingAtHearth);
            }
        }
        RestartRun();SkipOpeningForVerification();ResetForestSubtitles();health=2;damageCooldown=2;
        RefreshContextHUD();check("Wounded forest player has contextual health",hudHealth.transform.parent.gameObject.activeSelf);
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();
        foreach(float x in new[]{30.1f,33f,40f}){
            player.position=new Vector3(x,0,0);FollowPlayerCamera(100);yield return null;yield return new WaitForEndOfFrame();
            check("Reel at "+x+" hides wounded health and forest prompts",!hudHealth.transform.parent.gameObject.activeInHierarchy&&!promptPanel.activeInHierarchy&&!uiCoreHints.activeInHierarchy);
        }
        CaptureSceneToTexture(folder+"/reel-no-health.png",1600,900);
        RestartRun();yield return null;check("Restart removes cinematic paper",knifeShotPaper==null||!knifeShotPaper.activeSelf);
        Application.Quit(passed?0:1);
    }
} }
