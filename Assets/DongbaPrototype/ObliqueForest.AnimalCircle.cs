using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {public partial class ObliqueForest {
    readonly List<Being> circleAnimals=new List<Being>(3);
    const float CircleRadius=2.8f,CircleSpeed=.48f;
    void UpdateFlourishingCircle(){
        for(int i=circleAnimals.Count-1;i>=0;i--){var a=circleAnimals[i];if(!friendlyWeather||!animals.Contains(a)||a.health<=0||a.angry||a.follow<=0||a.migrationState!=0||Vector3.Distance(a.root.position,Feet)>FriendlyRadius+4)circleAnimals.RemoveAt(i);}
        if(!friendlyWeather)return;
        while(circleAnimals.Count<3){Being nearest=null;float distance=FriendlyRadius;
            foreach(var a in animals){if(a.health<=0||a.angry||a.follow<=0||a.migrationState!=0||circleAnimals.Contains(a))continue;float d=Vector3.Distance(a.root.position,Feet);if(d<distance){nearest=a;distance=d;}}
            if(nearest==null)break;circleAnimals.Add(nearest);
        }
    }
    void UpdateCircleDestination(Being b){
        int slot=circleAnimals.IndexOf(b);if(slot<0||!friendlyWeather||b.angry||b.follow<=0||b.migrationState!=0)return;
        float angle=clock*CircleSpeed+slot*Mathf.PI*2/3;
        var offset=new Vector3(Mathf.Cos(angle)*CircleRadius,0,Mathf.Sin(angle)*CircleRadius);
        var goal=Feet+offset;b.socialOffset=offset;
        // Follow the moving arc continuously when clear; retain the existing navigation
        // around trees, river banks and other animals when the straight segment is blocked.
        if(CircleStepClear(goal,b)&&CircleSegmentClear(b,b.root.position,goal)){
            if(b.route==null)b.route=new List<Vector3>(96);b.route.Clear();b.route.Add(goal);b.routeIndex=0;b.navGoal=goal;b.navRefresh=clock+.25f;
        }
    }
    bool PlanFriendlyRoute(Being b,Vector3 goal){
        if(CircleStepClear(goal,b)&&CircleSegmentClear(b,b.root.position,goal)){
            if(b.route==null)b.route=new List<Vector3>(96);b.route.Clear();b.route.Add(goal);b.routeIndex=0;b.navGoal=goal;return true;
        }
        return PlanAnimalRoute(b,goal);
    }
    bool CircleStepClear(Vector3 p,Being b){
        if(River(p)||Mathf.Abs(p.x)>ForestHalfWidth-1||Mathf.Abs(p.z)>ForestHalfDepth-.8f)return false;
        if(storyHome!=null&&storyHome.gameObject.activeSelf&&Mathf.Abs(p.x-storyHome.position.x)<1.9f&&p.z>storyHome.position.z-.65f&&p.z<storyHome.position.z+4.2f)return false;
        if(storyHearth!=null&&Vector3.Distance(p,HearthPosition)<.65f)return false;
        foreach(var plant in plants)if(plant.health>0&&Vector3.Distance(p,plant.home)<(plant.tree?.85f:.2f))return false;
        foreach(var other in animals)if(other!=b&&other.health>0&&other.migrationState!=2&&Vector3.Distance(p,other.root.position)<.95f)return false;
        return true;
    }
    bool CircleSegmentClear(Being b,Vector3 from,Vector3 to){int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.15f));for(int i=1;i<=n;i++)if(!CircleStepClear(Vector3.Lerp(from,to,(float)i/n),b))return false;return true;}
    IEnumerator CheckAnimalCircle(){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(false);RestartRun();player.position=new Vector3(-18,0,-3);lastDryPosition=Feet;FollowPlayerCamera(100);careCount=FlourishThreshold;damageCount=0;
        // Fixture: extra nearby adults exercise the outer followers as well as the three orbiters.
        for(int i=0;i<3;i++)if(FindLocal(Feet,4,7,100+i,true,out var spot)){AddAnimal(spot,i);populationCapacity[animals[animals.Count-1].populationRegion]++;}
        string folder="Verification/animal-circle";Directory.CreateDirectory(folder);
        yield return new WaitForSeconds(5);
        float age=0;int maxCircle=0,maxFollowers=0;float travel=0;Vector3 previous=Vector3.zero;Being tracked=null;
        while(age<24){yield return null;age+=Time.deltaTime;maxCircle=Mathf.Max(maxCircle,circleAnimals.Count);int followers=0;foreach(var a in animals)if(a.follow>0&&!circleAnimals.Contains(a))followers++;maxFollowers=Mathf.Max(maxFollowers,followers);
            if(tracked==null&&circleAnimals.Count>0){tracked=circleAnimals[0];previous=tracked.root.position;}if(tracked!=null){travel+=Vector3.Distance(previous,tracked.root.position);previous=tracked.root.position;}
        }
        CaptureSceneToTexture(folder+"/flourishing-circle.png");
        bool attacked=false;if(circleAnimals.Count>0){var a=circleAnimals[0];a.angry=true;UpdateAnimalEcology(a,.02f,0);UpdateFlourishingCircle();attacked=!circleAnimals.Contains(a)&&a.follow==0;}
        string diagnostic=tracked==null?"none":"position="+tracked.root.position+", blocked="+tracked.blockedTime+", route="+HasAnimalRoute(tracked)+", intro="+fatherIntroActive+", peaceful="+AfterCapturePeaceful;
        careCount=0;yield return new WaitForSeconds(4);
        File.WriteAllText(folder+"/checks.txt","Three circle participants="+(maxCircle==3)+"; max="+maxCircle+"\nOther followers retained="+(maxFollowers>0)+"; max="+maxFollowers+"\nCircle animal actually moves="+(travel>3)+"; distance="+travel+"\nDiagnostic="+diagnostic+"\nAttack cancels circle="+attacked+"\nLeaving flourishing clears circle="+(circleAnimals.Count==0)+"\n");Application.Quit();
    }
}}
