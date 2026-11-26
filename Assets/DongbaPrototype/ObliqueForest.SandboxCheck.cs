using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace DongbaPrototype { public partial class ObliqueForest {
    internal IEnumerator VerifySandbox(Action<bool,string> check) {
        StartForestMode(false);yield return null;yield return null;
        check(!StoryModeEnabled&&!fatherIntroActive&&!ManualStoryDialogueActive&&journeyStage==JourneyStage.Off,"Sandbox starts without dialogue or journey");
        check((storyFatherRoot==null||!storyFatherRoot.gameObject.activeSelf)&&!storyHome.gameObject.activeSelf&&!storyHearth.gameObject.activeSelf&&!storyHomeContact.gameObject.activeSelf,"No father, house, hearth or house contact in sandbox");
        check(Free(HearthPosition),"Hidden hearth no longer blocks walking");
        check(Balance==0&&plants.Count==initialPlantCount&&animals.Count==initialAnimalCount,"Normal forest keeps original population and layout");
        if(!XRTrackingControlsCamera){
            var inputUI=DongbaInterface.Instance;var feetBefore=Feet;
            yield return inputUI.KeyPress(UnityEngine.InputSystem.Key.D);
            check(Vector3.Distance(Feet,feetBefore)>.01f,"Keyboard movement works immediately in sandbox");
            yield return inputUI.KeyPress(UnityEngine.InputSystem.Key.Space);
            check(jumpAge>=0,"Space triggers a sandbox jump");yield return new WaitForSeconds(1.3f);
            yield return inputUI.KeyPress(UnityEngine.InputSystem.Key.J);yield return new WaitForSeconds(.12f);
            check(attackAge>=0&&axe.gameObject.activeSelf,"J animates the existing hand-held axe");yield return new WaitForSeconds(.8f);
        }
        var first=plants.Where(b=>b.tree).OrderBy(b=>Vector3.Distance(b.home,ForestSpawn)).First();
        int before=damageCount;ApplyEcologyHit(first);ApplyEcologyHit(first);
        check(damageCount==before,"Nonfatal tree hits do not reduce ecology");ApplyEcologyHit(first);
        check(damageCount==before+1&&first.health==0,"One felled tree reduces ecology by one");
        var victim=animals[0];before=damageCount;ApplyEcologyHit(victim);
        check(damageCount==before,"Nonfatal animal hit does not reduce ecology");ApplyEcologyHit(victim);
        check(damageCount==before+2&&victim.health==0,"Killed animal reduces ecology by two");
        foreach(var tree in plants.Where(b=>b.tree&&b.health>0).OrderBy(b=>Vector3.Distance(b.home,ForestSpawn)).Take(9).ToArray())
            while(tree.health>0)ApplyEcologyHit(tree);
        yield return new WaitForSeconds(4);
        check(Balance<=-10&&ecologyVisual<-.9f,"Repeated felling reaches heavy decline through existing events");
        check(journeyStage==JourneyStage.Off&&storyBoar==null&&!fatherIntroActive&&!ManualStoryDialogueActive,"Crossing story threshold cannot summon boar, father or lock input");
        check(plants.Exists(b=>b.tree&&b.health>0&&b.leafAttached.Any(attached=>!attached)),"Decline removes actual leaf fragments on living trees");
        var nearby=animals.First(b=>b.health>0&&b.age==2&&b.migrationState!=2);
        if(FindLocal(nearby.root.position,1.4f,2.3f,71,true,out var approach)){player.position=approach;lastDryPosition=Feet;}
        bool fled=false;float watch=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<watch){fled|=animals.Exists(b=>b.health>0&&(b.fleeing||b.proximityFleeUntil>clock));yield return null;}
        check(fled,"Nearby animals evade the player in decline");player.position=ForestSpawn;lastDryPosition=Feet;FollowPlayerCamera(100);
        if(XRTrackingControlsCamera)CaptureSceneToTexture("Verification/pico-integration/sandbox-decline.png");
        else yield return DongbaInterface.Instance.Snap("sandbox-decline");
        foreach(var tree in plants.Where(b=>b.tree&&b.health<=0).ToArray()){
            player.position=tree.home;lastDryPosition=Feet;interactionTarget=null;int old=careCount;Care();
            check(tree.health==3&&tree.targetScale<.5f&&careCount==old+1,"Replant actual nearby stump with care action");
        }
        var adult=animals.First(b=>b.health>0&&b.age==2&&b.root.gameObject.activeSelf);
        adult.root.position=ForestSpawn+Vector3.left*2;adult.careReady=0;adult.migrationState=0;adult.angry=false;
        int population=animals.Count;int points=careCount;Feed(adult);
        check(animals.Count==population+1,"Feeding adult produces nearby young");
        if(animals.Count>population){var young=animals[animals.Count-1];
            check(young.age==0&&Vector3.Distance(young.root.position,adult.root.position)<1.5f,"Young stays near fed adult, smaller than adult");
            young.careReady=0;Feed(young);check(careCount==points&&young.age==1,"First young feed changes growth only");
            young.careReady=0;Feed(young);check(careCount==points+1&&young.age==2,"Growing to adult awards one care point");
        }
        var candidates=plants.Where(b=>b.tree&&b.health>0&&!b.cultivated).ToArray();
        foreach(var tree in candidates){if(Balance>=FlourishThreshold+2)break;tree.careReady=0;GrowAround(tree);}
        var cared=plants.FirstOrDefault(b=>b.tree&&b.cultivated);points=careCount;
        if(cared!=null){cared.careReady=0;GrowAround(cared);}
        check(cared!=null&&careCount==points,"A tree can be tended only once");
        player.position=ForestSpawn;lastDryPosition=Feet;FollowPlayerCamera(100);
        yield return new WaitForSeconds(6);
        check(Balance>=FlourishThreshold&&ecologyVisual>.9f&&greenHeight>.8f,"Care restores decline and reaches flourishing without story completion");
        check(friendlyWeather,"Flourishing enables existing friendly animal AI");
        int marks=flowerMarksSpawned;
        ecologyPreviewDirection=PreviewWalkDirection();ecologyPreviewWalking=true;
        yield return new WaitForSeconds(.8f);ecologyPreviewWalking=false;
        check(flowerMarksSpawned>marks,"Walking in flourishing creates the existing flower trail");
        if(XRTrackingControlsCamera)CaptureSceneToTexture("Verification/pico-integration/sandbox-flourish.png");
        else yield return DongbaInterface.Instance.Snap("sandbox-flourish");
        marks=flowerMarksSpawned;yield return new WaitForSeconds(.25f);
        check(flowerMarksSpawned==marks,"Stopping creates no new flowers");
        if(XRTrackingControlsCamera){
            // The first flourishing now asks for an invitation. Dismiss it as a player would before opening controls.
            if(NatureWelcomeVisible){var aim=new Ray(Feet+Vector3.up*30,Vector3.down);ProcessNatureXR(false,false,false,false,Vector2.zero,aim,true);ProcessNatureXR(false,true,false,false,Vector2.zero,aim,true);yield return null;}
            xrHelpOpen=true;xrModeHold=xrRestartHold=0;
            #if UNITY_EDITOR
            xrSimA=true;yield return new WaitForSecondsRealtime(1.2f);xrSimA=false;
#else
            RestartFromInterface();
#endif
            ProcessXRInput(Vector2.zero,false,false,false,false,false,false);
        }else RestartFromInterface();
        yield return null;
        check(!StoryModeEnabled&&Balance==0&&animals.Count==initialAnimalCount&&plants.Count==initialPlantCount&&health==5,"Restart resets complete ecology and preserves sandbox");
        check(storyOpeningState==StoryOpeningState.Off&&storyBoar==null&&!fatherIntroActive&&attackAge<0,"Restart leaves no story or input state behind");
    }
}}
