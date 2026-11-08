using System.Collections;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    const float FatherRiverJumpDuration=.85f;
    void AdvanceFatherRiverJump(float dt){
        if(!StoryModeEnabled||storyFatherRoot==null||fatherJumpAge<0)return;
        fatherJumpAge=Mathf.Min(FatherRiverJumpDuration,fatherJumpAge+dt);
        storyFatherRoot.position=Vector3.Lerp(fatherJumpFrom,fatherJumpTo,fatherJumpAge/FatherRiverJumpDuration);
        if(fatherJumpAge>=FatherRiverJumpDuration)fatherJumpAge=-1;
    }
    void UpdateFatherVisualPose(){
        if(storyFatherArt==null||storyFatherImage==null||!storyFatherRoot.gameObject.activeInHierarchy)return;
        float lift=fatherJumpAge>=0?Mathf.Sin(fatherJumpAge/FatherRiverJumpDuration*Mathf.PI)*1.45f:carryStarted?0:.02f*Mathf.Sin(clock*2.4f+storyFather.phase);
        storyFatherArt.localPosition=Vector3.up*lift;
        if(!carryStarted)storyFatherImage.sortingOrder=DepthOrder(storyFatherRoot.position.z);
    }
    bool StoryFatherWalkingSegmentClear(Vector3 from,Vector3 to){
        int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.1f));
        for(int i=1;i<=samples;i++)if(!StoryFatherStepClear(Vector3.Lerp(from,to,(float)i/samples)))return false;
        return true;
    }
    IEnumerator CheckFatherRiver(){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();for(int i=0;i<3;i++)AdvanceFatherIntro();
        var folder="Verification/father-river";Directory.CreateDirectory(folder);File.WriteAllText(folder+"/checks.txt","");
        tonePreviewFreezeAnimals=true;foreach(var a in animals){a.root.gameObject.SetActive(false);a.health=0;}
        // Keep obstacles intact except the test's landing footprint, so each role can use a clear bank.
        float z=0,river=RiverX(z);var west=new Vector3(river-1.4f,0,z);var east=new Vector3(river+1.4f,0,z);
        foreach(var tree in plants)if(tree.tree&&(Vector3.Distance(tree.home,west)<1.1f||Vector3.Distance(tree.home,east)<1.1f))tree.health=0;
        string[] names={"Follow west to east","Return east to west","Knife retrieval west to east","Hunting interrupted in flight"};
        bool allVisible=true;Time.captureFramerate=30;Directory.CreateDirectory(folder+"/frames");int frame=0;
        for(int scenario=0;scenario<4;scenario++){
            bool eastward=scenario!=1;var start=eastward?west:east;var end=eastward?east:west;
            journeyStage=scenario==2?JourneyStage.ReturnForKnife:JourneyStage.Boar;
            storyOpeningState=scenario==2?StoryOpeningState.CaptureBeat:StoryOpeningState.Off;
            storyFatherRoot.position=start;storyFatherArt.localPosition=Vector3.zero;fatherJumpAge=-1;fatherHuntTarget=null;captureBeatAge=0;returnWait=100;
            knifeDropPoint=end+Vector3.right*10;player.position=start+new Vector3(-2,0,-3);FollowPlayerCamera(100);
            MoveStoryFatherToward(end,2.7f,1f/30);
            bool began=fatherJumpAge>=0;float age=0,maxLift=0;bool groundedOverWater=false,interrupted=false;
            while(age<1.05f){
                // Do not call movement again: jump must finish without depending on the caller/AI.
                if(scenario==3&&age>.2f&&!interrupted){journeyStage=JourneyStage.Hunting;fatherIntroActive=true;fatherHuntTarget=null;interrupted=true;}
                yield return new WaitForEndOfFrame();maxLift=Mathf.Max(maxLift,storyFatherArt.localPosition.y);
                if(River(storyFatherRoot.position)&&storyFatherArt.localPosition.y<.25f)groundedOverWater=true;
                if(storyFatherArt.localPosition.y>1.4f)CaptureRiverApexOnce(folder,scenario);
                CaptureSceneToTexture(folder+"/frames/"+(frame++).ToString("D4")+".png",960,540);age+=Time.deltaTime;
            }
            bool landed=fatherJumpAge<0&&Vector3.Distance(storyFatherRoot.position,end)<.01f&&Mathf.Abs(storyFatherArt.localPosition.y)<.03f;
            bool pass=began&&landed&&maxLift>1.4f&&!groundedOverWater;allVisible&=pass;
            File.AppendAllText(folder+"/checks.txt",names[scenario]+"="+pass+"; max visible lift="+maxLift.ToString("F3")+"; landing="+landed+"\n");
        }
        File.AppendAllText(folder+"/checks.txt","Ground walk cannot cut across river="+!StoryFatherWalkingSegmentClear(west,east)+"\nAll crossings visibly airborne="+allVisible+"\n");
        RestartRun();yield return null;File.AppendAllText(folder+"/checks.txt","Restart clears jump="+(fatherJumpAge<0&&Mathf.Abs(storyFatherArt.localPosition.y)<.03f)+"\n");Time.captureFramerate=0;Application.Quit();
    }
    void CaptureRiverApexOnce(string folder,int scenario){var path=folder+"/apex-"+scenario+".png";if(!File.Exists(path))CaptureSceneToTexture(path);}
}}
