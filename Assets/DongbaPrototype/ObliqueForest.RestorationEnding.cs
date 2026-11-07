using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    readonly List<Being> restorationStumps=new List<Being>();
    readonly HashSet<Being> restorationReplanted=new HashSet<Being>();
    float restorationAge,restorationWildlifeAt;Vector3 restorationClearing;int restorationSeeded;
    string restorationReturnCheck="not requested";
    bool IsRestorationEnding=>StoryModeEnabled&&(journeyStage==JourneyStage.RestorationArrival||journeyStage==JourneyStage.RestorationPlay||journeyStage==JourneyStage.RestorationComplete);
    bool RestorationArrivalLocked=>StoryModeEnabled&&journeyStage==JourneyStage.RestorationArrival;
    public event Action ForestRestorationCompleted;
    void ResetRestorationEnding(){restorationStumps.Clear();restorationReplanted.Clear();restorationAge=0;restorationSeeded=0;}
    // Call after the final rescue sequence has returned control to the forest scene.
    // This deliberately does not reset the player's earlier ecological damage.
    public void BeginRestorationEnding(){
        if(IsRestorationEnding)return;
        StoryModeEnabled=true;if(storyFatherRoot==null)CreateStoryFather();ResetJourneyReel();ResetForestSubtitles();ResetAfterCapture();ResetRestorationEnding();EnsureAfterCapturePresentation();
        fatherIntroActive=false;fatherHuntTarget=null;fatherSwingAge=-1;fatherJumpAge=-1;storyOpeningState=StoryOpeningState.Off;
        if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);if(storyFatherKnifeImage!=null)storyFatherKnifeImage.enabled=false;
        if(storyShu!=null)storyShu.gameObject.SetActive(false);if(storyBoar!=null)storyBoar.root.gameObject.SetActive(false);if(droppedKnife!=null)droppedKnife.gameObject.SetActive(false);
        Being closest=null;float distance=float.MaxValue;
        foreach(var tree in plants)if(tree.tree&&tree.health<=0){restorationStumps.Add(tree);float d=Vector3.Distance(tree.home,ForestSpawn);if(d<distance){distance=d;closest=tree;}}
        restorationClearing=closest!=null?closest.home:ForestSpawn+Vector3.left*4;
        Vector3 arrival=ForestSpawn;for(int i=0;i<24;i++){float angle=i*2.39996f;var candidate=restorationClearing+new Vector3(Mathf.Cos(angle)*2.2f,0,Mathf.Sin(angle)*2.2f);if(candidate.x<RiverX(candidate.z)-2&&Free(candidate)){arrival=candidate;break;}}
        player.position=arrival;lastDryPosition=arrival;jumpAge=attackAge=-1;axe.gameObject.SetActive(false);body.localPosition=Vector3.zero;playerImage.forceRenderingOff=false;
        facing=1;heading=Vector3.right;journeyStage=JourneyStage.RestorationArrival;restorationWildlifeAt=clock+5;notice="";noticeTime=0;
        storyCurtain=1;storyCurtainImage.color=new Color(.045f,.035f,.025f,1);FollowPlayerCamera(100);
    }
    bool UpdateRestorationEnding(float dt){
        if(!IsRestorationEnding)return false;
        if(journeyStage==JourneyStage.RestorationArrival){
            restorationAge+=dt;storyCurtain=1-Mathf.SmoothStep(0,1,restorationAge/1.2f);storyCurtainImage.color=new Color(.045f,.035f,.025f,storyCurtain);storyCurtainImage.transform.SetAsLastSibling();
            if(restorationAge>3){BeginManualStoryDialogue(new[]{"After the rescue, Gaolequ's father did not survive.","Gaolequ: We took from this place without care.","Gaolequ: I will begin again, by planting and caring for it."},()=>journeyStage=JourneyStage.RestorationPlay);}
            return true;
        }
        int grown=0;
        foreach(var tree in restorationStumps)if(tree.health>0){if(restorationReplanted.Add(tree)&&Vector3.Distance(Feet,tree.home)<.7f){
            for(int i=0;i<24;i++){float angle=i*2.39996f;var step=tree.home+new Vector3(Mathf.Cos(angle)*.85f,0,Mathf.Sin(angle)*.85f);if(Free(step)){player.position=step;lastDryPosition=step;break;}}
        }tree.targetScale=Mathf.MoveTowards(tree.targetScale,1,dt/14f);if(tree.targetScale>=.85f)grown++;}
        if(clock>=restorationWildlifeAt){restorationWildlifeAt=clock+(TryRestoreWildlife(grown)?12:3);}
        if(journeyStage==JourneyStage.RestorationPlay&&restorationReplanted.Count>=2&&grown>=2&&Balance>=FlourishThreshold&&ecologyVisual>.8f){
            journeyStage=JourneyStage.RestorationComplete;BeginManualStoryDialogue(new[]{"Gaolequ: I will keep caring for this forest."});ForestRestorationCompleted?.Invoke();
        }
        return true;
    }
    bool TryRestoreWildlife(int grown){
        restorationReturnCheck="eligibility: grown="+grown+", balance="+Balance+", guided="+restorationSeeded;
        if(grown<2||Balance<0||restorationSeeded>=2||animals.Count==0)return false;
        int local=0,living=0;Being returning=null,template=null;
        foreach(var a in animals)if(a.health>0){living++;if(template==null)template=a;
            if(a.migrationState!=2&&Vector3.Distance(a.root.position,restorationClearing)<15)local++;
            if(a.migrationState==2&&Vector3.Distance(a.home,restorationClearing)<22&&(returning==null||a.renderer.bounds.size.sqrMagnitude<returning.renderer.bounds.size.sqrMagnitude))returning=a;
        }
        restorationReturnCheck="nearby="+local;
        if(local>=2)return false;if(template==null)template=animals[0];if(returning!=null)template=returning;
        Vector3 entry=Vector3.zero,goal=Vector3.zero;bool foundEntry=false,foundGoal=false;
        for(int i=0;i<240;i++){
            float angle=i*2.39996f;float radius=17+(i%9)*.55f;var point=restorationClearing+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if((point.x-RiverX(point.z))*(restorationClearing.x-RiverX(restorationClearing.z))>0&&ClearAnimalStep(point,template)&&AnimalOutsideViewAt(template,point)){entry=point;foundEntry=true;break;}
        }
        for(int i=0;i<180;i++){
            float angle=i*2.39996f;float radius=2+(i%14)*.75f;var point=restorationClearing+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(ClearAnimalStep(point,template)){goal=point;foundGoal=true;break;}
        }
        restorationReturnCheck="entry="+foundEntry+", goal="+foundGoal;
        if(!foundEntry||!foundGoal)return false;
        bool created=returning==null;
        if(returning==null){
            if(living>=AnimalPopulationLimit)return false;
            AddAnimal(entry,template.speciesIndex);returning=animals[animals.Count-1];populationCapacity[returning.populationRegion]++;
        }
        var oldPosition=returning.root.position;var oldHome=returning.home;
        // Find an offscreen entry connected to the clearing, rather than choosing
        // two individually clear points that may lie in disconnected forest pockets.
        bool connected=false;
        for(int i=0;i<56;i++){
            float angle=i*2.39996f,radius=2+(i%14)*.75f;
            var candidate=restorationClearing+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(!ClearAnimalStep(candidate,returning))continue;
            returning.root.position=candidate;
            if(!PlanAnimalRoute(returning,candidate,true)||returning.route.Count==0)continue;
            entry=returning.route[returning.route.Count-1];
            if(!ClearAnimalStep(entry,returning))continue;
            returning.root.position=entry;
            if(!PlanAnimalRoute(returning,candidate))continue;
            goal=candidate;connected=true;break;
        }
        returning.home=goal;
        if(!connected){restorationReturnCheck="route failed: "+entry+" -> "+goal;if(created){populationCapacity[returning.populationRegion]=Mathf.Max(0,populationCapacity[returning.populationRegion]-1);animals.Remove(returning);Destroy(returning.root.gameObject);}else{returning.root.position=oldPosition;returning.home=oldHome;}return false;}
        int region=PopulationRegion(goal.x);if(region!=returning.populationRegion){populationCapacity[returning.populationRegion]=Mathf.Max(0,populationCapacity[returning.populationRegion]-1);populationCapacity[region]++;returning.populationRegion=region;}
        EndAnimalVisit(returning);PlanAnimalRoute(returning,goal);returning.migrationState=3;returning.opacity=1;returning.root.gameObject.SetActive(true);returning.fleeing=false;returning.angry=false;returning.protectedUntil=clock+20;returning.navRefresh=clock+2;
        restorationSeeded++;returnsStarted++;restorationReturnCheck="return started";return true;
    }
    string RestorationObjective(){
        if(journeyStage==JourneyStage.RestorationArrival)return "Back to the clearing";
        if(journeyStage==JourneyStage.RestorationComplete)return "The forest is renewed\nYou can keep caring for it.";
        if(restorationReplanted.Count<2)return "Restore the clearing\nE  Replant the stumps.";
        if(Balance<0)return "Let the forest recover\nReplant and tend the trees.";
        return "Life is returning\nCare for plants and animals.";
    }
    void PrepareRestorationPreview(){
        SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;
        var trees=new List<Being>();foreach(var tree in plants)if(tree.tree&&tree.home.x<RiverX(tree.home.z)-3)trees.Add(tree);
        trees.Sort((a,b)=>Vector3.Distance(a.home,ForestSpawn).CompareTo(Vector3.Distance(b.home,ForestSpawn)));
        for(int i=0;i<Mathf.Min(8,trees.Count);i++){while(trees[i].health>0)ApplyEcologyHit(trees[i]);trees[i].fall=1;}
        int killed=0;foreach(var a in animals)if(killed<2){while(a.health>0)ApplyEcologyHit(a);a.root.gameObject.SetActive(false);killed++;}
        ecologyVisual=-1;greenHeight=0;greenVelocity=0;UpdateFlourish(0);BeginRestorationEnding();
        // Preview bootstrap only: place already-departed animals outside the first shot.
        // Actual story entry never removes animals on screen; existing migration still handles it.
        foreach(var animal in animals)if(animal.health>0&&!AnimalOutsideViewAt(animal,animal.root.position)){
            for(int seed=0;seed<160;seed++){
                var point=new Vector3((seed%2==0?-1:1)*(ForestHalfWidth-3),0,Mathf.Lerp(-ForestHalfDepth+3,ForestHalfDepth-3,(seed/2%80)/79f));
                if(ClearAnimalStep(point,animal)&&AnimalOutsideViewAt(animal,point)){EndAnimalVisit(animal);animal.root.position=point;animal.migrationState=2;animal.root.gameObject.SetActive(false);break;}
            }
        }
    }
    IEnumerator PreviewRestorationEnding(){yield return new WaitForSeconds(1);PrepareRestorationPreview();}
}}
