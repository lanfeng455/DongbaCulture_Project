using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DongbaPrototype {
public partial class ObliqueForest {
    // Existing migrationState: 0 roaming, 1 departing, 2 outside activity, 3 returning.
    [Header("Animal ecology response")]
    public float FriendlyRadius=11f;
    [Range(1,6)] public int MaxFriendlyAnimals=6;
    public float FriendlyApproachSpeed=2.2f, FriendlyCatchupSpeed=4.3f;
    [Header("Severe-ecology flight")]
    [Range(.25f,.9f)] public float SevereEcologyFleeLevel=.55f;
    [Range(2.5f,8f)] public float PlayerApproachFleeRadius=5.5f;
    [Range(2f,6f)] public float PanickedAnimalFleeSpeed=3.6f;
    bool friendlyWeather; float friendlyTransition,friendlyReleaseAt,nextVisitAt,nextProximityFleeCheckAt; int friendlyVisitSequence; float nextFriendlyRotationAt;
    int migrationSequence,departuresCompleted,returnsStarted,visitsStarted,visibleHideErrors,visibleSpawnErrors;
    const float NavCell=.6f; const int NavW=151,NavH=71,NavN=NavW*NavH;
    readonly bool[] navWalk=new bool[NavN],navClosed=new bool[NavN];
    readonly float[] navCost=new float[NavN];readonly int[] navParent=new int[NavN];
    readonly int[] navHeap=new int[NavN*8];readonly float[] navHeapScore=new float[NavN*8];int navHeapCount;
    Vector3 NavPoint(int n){return new Vector3(-45+(n%NavW)*NavCell,0,-21+(n/NavW)*NavCell);}
    int NavIndex(Vector3 p){return Mathf.Clamp(Mathf.RoundToInt((p.z+21)/NavCell),0,NavH-1)*NavW+Mathf.Clamp(Mathf.RoundToInt((p.x+45)/NavCell),0,NavW-1);}
    void HeapPush(int n,float score){if(navHeapCount>=navHeap.Length)return;int i=navHeapCount++;while(i>0){int parent=(i-1)/2;if(navHeapScore[parent]<=score)break;navHeap[i]=navHeap[parent];navHeapScore[i]=navHeapScore[parent];i=parent;}navHeap[i]=n;navHeapScore[i]=score;}
    int HeapPop(){int n=navHeap[0],last=--navHeapCount,id=navHeap[last];float score=navHeapScore[last];int i=0;while(i*2+1<last){int c=i*2+1;if(c+1<last&&navHeapScore[c+1]<navHeapScore[c])c++;if(navHeapScore[c]>=score)break;navHeap[i]=navHeap[c];navHeapScore[i]=navHeapScore[c];i=c;}navHeap[i]=id;navHeapScore[i]=score;return n;}
    void BuildAnimalWalkMap(Being animal){
        // Navigation and per-frame motion use ground footprints, not the projected canopy.
        // Screen-space ink clearance is retained for initial placement, not used as an invisible wall.
        for(int n=0;n<NavN;n++){var p=NavPoint(n);navWalk[n]=AnimalTerrainClear(p);navClosed[n]=false;navCost[n]=float.PositiveInfinity;navParent[n]=-1;}
        foreach(var plant in plants)StampNavCircle(plant.home,AnimalPlantRadius(plant)+.04f);
        foreach(var other in animals)if(other!=animal&&other.health>0&&other.migrationState!=2)StampNavCircle(other.root.position,.99f);
    }

    void StampNavCircle(Vector3 center,float radius){
        int x0=Mathf.Clamp(Mathf.FloorToInt((center.x-radius+45)/NavCell),0,NavW-1),x1=Mathf.Clamp(Mathf.CeilToInt((center.x+radius+45)/NavCell),0,NavW-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((center.z-radius+21)/NavCell),0,NavH-1),z1=Mathf.Clamp(Mathf.CeilToInt((center.z+radius+21)/NavCell),0,NavH-1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)if((NavPoint(z*NavW+x)-center).sqrMagnitude<radius*radius)navWalk[z*NavW+x]=false;
    }
    bool AnimalOutsideViewAt(Being b,Vector3 ground){
        Vector3 v=cam.WorldToViewportPoint(ground);float w=b.renderer.sprite.bounds.size.x*b.initialScale.x*Mathf.Max(.42f,b.birth)/(cam.orthographicSize*2*cam.aspect);
        float h=b.renderer.sprite.bounds.size.y*b.initialScale.y*Mathf.Max(.42f,b.birth)/(cam.orthographicSize*2);
        return v.x+w*.5f<-.10f||v.x-w*.5f>1.10f||v.y+h<-.10f||v.y>1.10f;
    }
    bool PlanAnimalRoute(Being b,Vector3 destination,bool exit=false){
        if(b.route==null)b.route=new List<Vector3>(96);b.route.Clear();b.routeIndex=0;
        if(!exit&&Vector3.Distance(b.root.position,destination)<2&&AnimalSegmentClear(b,b.root.position,destination)){b.route.Add(destination);b.navGoal=destination;b.blockedTime=0;return true;}
        BuildAnimalWalkMap(b);
        int start=NavIndex(b.root.position),goal=NavIndex(destination);navWalk[start]=true;
        if(!exit&&!navWalk[goal]){float best=4;int chosen=-1;for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++){int gx=goal%NavW+x,gz=goal/NavW+z;if(gx<0||gx>=NavW||gz<0||gz>=NavH)continue;int n=gz*NavW+gx;float d=(NavPoint(n)-destination).sqrMagnitude;if(navWalk[n]&&d<best){best=d;chosen=n;}}if(chosen<0)return false;goal=chosen;}
        navHeapCount=0;navCost[start]=0;HeapPush(start,0);int found=-1;
        while(navHeapCount>0){int n=HeapPop();if(navClosed[n])continue;navClosed[n]=true;var point=NavPoint(n);
            if(exit?AnimalOutsideViewAt(b,point)&&AnimalOutsideViewAt(b,point+(b.root.position-point).normalized*.25f)&&Vector3.Distance(point,b.root.position)>.6f:n==goal){found=n;break;}
            int nx=n%NavW,nz=n/NavW;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dz==0)continue;int x=nx+dx,z=nz+dz;if(x<0||x>=NavW||z<0||z>=NavH)continue;int next=z*NavW+x;if(!navWalk[next]||navClosed[next])continue;
                if(dx!=0&&dz!=0&&(!navWalk[nz*NavW+x]||!navWalk[z*NavW+nx]))continue;
                float cost=navCost[n]+(dx!=0&&dz!=0?.84853f:NavCell);if(cost>=navCost[next])continue;navCost[next]=cost;navParent[next]=n;HeapPush(next,cost+(exit?0:Vector3.Distance(NavPoint(next),NavPoint(goal))));
            }
        }
        if(found<0)return false;
        for(int n=found;n!=start&&n>=0;n=navParent[n])b.route.Add(NavPoint(n));b.route.Reverse();b.navGoal=NavPoint(found);
        if(!exit&&Vector3.Distance(b.navGoal,destination)<.7f&&ClearAnimalStep(destination,b)){b.route.Add(destination);b.navGoal=destination;}
        b.blockedTime=0;return true;
    }
    bool AnimalSegmentClear(Being b,Vector3 start,Vector3 end){int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/.12f));for(int i=1;i<=steps;i++)if(!ClearAnimalStep(Vector3.Lerp(start,end,(float)i/steps),b))return false;return true;}
    bool HasAnimalRoute(Being b){return b.route!=null&&b.routeIndex<b.route.Count;}
    void EndAnimalVisit(Being b){b.follow=0;b.visitOrder=0;b.socialReadyAt=clock+10+Mathf.Repeat(b.phase,5);b.navRefresh=clock+.3f;if(b.route!=null)b.route.Clear();}
    void SendFriendlyAnimalHome(Being b){
        EndAnimalVisit(b);b.migrationState=3;b.fleeing=false;b.navRefresh=clock+.05f;
        if(!PlanAnimalRoute(b,b.home)){b.migrationState=0;b.socialReadyAt=clock+5;}
    }
    bool TryFriendlyDestination(Being b,out Vector3 goal){
        for(int i=0;i<32;i++){float a=b.phase*1.37f+i*2.39996f;float r=(circleAnimals.Count>=3&&!circleAnimals.Contains(b)?4.2f:1.6f)+Mathf.Repeat(b.phase*.37f+i*.41f,1.8f);goal=Feet+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            if(!CircleStepClear(goal,b))continue;bool spaced=true;foreach(var other in animals)if(other!=b&&other.follow>0&&Vector3.Distance(goal,Feet+other.socialOffset)<1.35f){spaced=false;break;}if(spaced)return true;
        }goal=Vector3.zero;return false;
    }
    void UpdatePopulation(float dt){
        bool requested=friendlyWeather?Balance>FlourishThreshold-3:Balance>=FlourishThreshold;
        if(requested!=friendlyWeather){friendlyTransition+=dt;if(friendlyTransition>2){friendlyWeather=requested;friendlyReleaseAt=clock;friendlyTransition=0;}}else friendlyTransition=0;
        UpdateFlourishingCircle();
        if(clock>=nextVisitAt){nextVisitAt=clock+.65f+Mathf.Repeat(migrationSequence*.71f,.55f);int participants=0;foreach(var a in animals)if(a.follow>0)participants++;
            int limit=Mathf.Clamp(MaxFriendlyAnimals,1,6);
            if(friendlyWeather&&participants>0&&participants>=limit&&clock>=nextFriendlyRotationAt){
                // Keep a rotating group: when a new animal is attracted at capacity, the earliest
                // arrival returns to its home range before the new one takes its place.
                Being oldest=null;foreach(var a in animals)if(a.follow>0&&!circleAnimals.Contains(a)&&(oldest==null||a.visitOrder<oldest.visitOrder))oldest=a;
                Being next=null;float best=FriendlyRadius;foreach(var a in animals){float d=Vector3.Distance(a.root.position,Feet);if(a.health<=0||a.migrationState!=0||a.angry||a.age<2||a.follow>0||clock<a.socialReadyAt||clock<a.careReady)continue;if(d<best){best=d;next=a;}}
                if(oldest!=null&&next!=null){if(TryFriendlyDestination(next,out var goal)&&PlanFriendlyRoute(next,goal)){SendFriendlyAnimalHome(oldest);nextFriendlyRotationAt=clock+8f;next.socialOffset=goal-Feet;next.follow=1;next.visitOrder=++friendlyVisitSequence;next.navRefresh=clock+.4f;visitsStarted++;}else next.socialReadyAt=clock+5;}
            }else if(friendlyWeather&&participants<limit){Being choice=null;float best=FriendlyRadius;
                foreach(var a in animals){float d=Vector3.Distance(a.root.position,Feet);if(a.health<=0||a.migrationState!=0||a.angry||a.age<2||a.follow>0||clock<a.socialReadyAt||clock<a.careReady)continue;if(d<best){best=d;choice=a;}}
                if(choice!=null){if(TryFriendlyDestination(choice,out var goal)&&PlanFriendlyRoute(choice,goal)){choice.socialOffset=goal-Feet;choice.follow=1;choice.visitOrder=++friendlyVisitSequence;choice.navRefresh=clock+.4f;visitsStarted++;}else choice.socialReadyAt=clock+5;}
            }
            if(!friendlyWeather){foreach(var a in animals)if(a.follow>0){EndAnimalVisit(a);break;}}
        }
        // In a severely damaged forest, nearby adults react to the player's approach immediately.
        // Only one animal starts fleeing per short interval, and it uses the existing obstacle-aware
        // route to leave the camera before being hidden.
        if(clock>=nextProximityFleeCheckAt){nextProximityFleeCheckAt=clock+.32f;TryStartProximityFlee();}
        if(clock<populationCheckAt)return;populationCheckAt=clock+Mathf.Lerp(2.6f,.65f,Mathf.Clamp01(-ecologyVisual))+Mathf.Repeat(migrationSequence++*.73f,.25f);
        float decline=Mathf.Clamp01(-ecologyVisual);Being departure=null,returner=null;float bestDeparture=float.NegativeInfinity,bestReturn=float.PositiveInfinity;int movingOut=0;
        foreach(var a in animals)if(a.migrationState==1)movingOut++;
        for(int region=0;region<populationRegionCount;region++){
            int current=0;foreach(var a in animals)if(a.health>0&&a.populationRegion==region&&a.migrationState!=1&&a.migrationState!=2)current++;
            int target=Mathf.Max(populationCapacity[region]>0?1:0,Mathf.CeilToInt(populationCapacity[region]*Mathf.Lerp(1,.15f,decline)));
            foreach(var a in animals){if(a.health<=0||a.populationRegion!=region)continue;
                if(decline>.16f&&current>target&&a.migrationState==0&&!a.angry&&a.age==2&&a.birth>.98f&&clock>=a.protectedUntil&&clock>=a.careReady&&clock>=a.migrationTimer){float score=30-Vector3.Distance(a.root.position,Feet);if(!AnimalOutsideViewAt(a,a.root.position))score+=20;if(score>bestDeparture){bestDeparture=score;departure=a;}}
                if(current<target&&a.migrationState==2&&AnimalOutsideViewAt(a,a.root.position)&&ClearAnimalStep(a.root.position,a)){float d=Vector3.Distance(a.home,Feet);if(d<bestReturn){bestReturn=d;returner=a;}}
            }
        }
        // One global departure/return per tick: no simultaneous region-wide group turns.
        if(departure!=null&&movingOut<5){EndAnimalVisit(departure);departure.fleeing=true;if(PlanAnimalRoute(departure,departure.home,true)){departure.migrationState=1;departure.navRefresh=clock+2;}else departure.migrationTimer=clock+5;}
        else if(returner!=null){if(PlanAnimalRoute(returner,returner.home)){if(!AnimalOutsideViewAt(returner,returner.root.position))visibleSpawnErrors++;returner.migrationState=3;returner.opacity=1;returner.root.gameObject.SetActive(true);returner.navRefresh=clock+2;returnsStarted++;}}
    }
    void TryStartProximityFlee(){
        if(Mathf.Clamp01(-ecologyVisual)<SevereEcologyFleeLevel)return;
        // Local evasion must not depend on finding a complete route outside the camera.
        // Recently born/feeding animals stay protected, but a mature animal can evade even
        // while waiting for a migration slot or returning from its former home range.
        for(int started=0;started<2;started++){
            Being choice=null;float closest=PlayerApproachFleeRadius;
            foreach(var a in animals){
                if(a.health<=0||a.migrationState==2||a.angry||a.age<2||a.birth<.98f||clock<a.careReady||clock<a.nextProximityFleeAt)continue;
                float distance=Vector3.Distance(a.root.position,Feet);if(distance<closest){closest=distance;choice=a;}
            }
            if(choice==null)break;
            choice.nextProximityFleeAt=clock+.75f;
            if(!PlanLocalEscape(choice))continue;
            choice.follow=0;choice.visitOrder=0;choice.fleeing=true;choice.proximityFleeUntil=clock+1.1f;choice.navRefresh=choice.proximityFleeUntil;
            int departing=0;foreach(var a in animals)if(a.migrationState==1)departing++;
            if(choice.migrationState!=1)choice.migrationState=departing<5?1:0;
        }
    }
    bool PlanLocalEscape(Being b){
        var away=b.root.position-Feet;away.y=0;if(away.sqrMagnitude<.01f)away=Vector3.right;away.Normalize();
        Vector3 best=b.root.position;float bestGain=.12f;float startDistance=Vector3.Distance(best,Feet);
        for(int i=0;i<11;i++){
            float angle=i==0?0:((i+1)/2)*25f*(i%2==0?-1:1);
            var direction=Quaternion.Euler(0,angle,0)*away;var candidate=b.root.position;
            for(int step=1;step<=22;step++){var point=b.root.position+direction*(step*.15f);if(!CircleStepClear(point,b))break;candidate=point;}
            float gain=Vector3.Distance(candidate,Feet)-startDistance;
            if(gain>bestGain){bestGain=gain;best=candidate;}
        }
        if(bestGain<=.12f)return false;
        if(b.route==null)b.route=new List<Vector3>(96);b.route.Clear();b.route.Add(best);b.routeIndex=0;b.navGoal=best;b.blockedTime=0;return true;
    }
    void UpdateAnimalEcology(Being b,float dt,float hop){
        if(b.health<=0||b.migrationState==2||ManualStoryDialogueActive||fatherIntroActive)return;
        bool feeding=FeedingResponseActive(b);if(!feeding)b.birth=Mathf.MoveTowards(b.birth,b.age==0?.42f:b.age==1?.7f:1,dt*.65f);b.art.localScale=b.initialScale*b.birth;b.cooldown-=dt;
        if(b.angry){b.proximityFleeUntil=0;b.fleeing=false;if(b.follow>0||b.migrationState!=0){EndAnimalVisit(b);b.migrationState=0;b.fleeing=false;}b.socialReadyAt=clock+20;}
        if(b.follow>0){if(friendlyWeather)b.follow=1;else if(b.visitOrder>0){if(clock>=friendlyReleaseAt+Mathf.Repeat(b.phase,.8f))SendFriendlyAnimalHome(b);}else{b.follow=Mathf.Max(0,b.follow-dt);if(b.follow==0||Vector3.Distance(Feet,b.root.position)>FriendlyRadius+3)EndAnimalVisit(b);}}
        if(b.migrationState==1&&ecologyVisual>=-.1f){b.migrationState=3;b.fleeing=false;b.proximityFleeUntil=0;if(b.route!=null)b.route.Clear();b.navRefresh=clock;}
        if(b.fleeing&&b.migrationState==0&&clock>=b.proximityFleeUntil)b.fleeing=false;
        if(clock>=b.navRefresh&&clock>=b.proximityFleeUntil&&!circleAnimals.Contains(b)){
            b.navRefresh=clock+2.2f+Mathf.Repeat(b.phase,.8f);
            if(b.migrationState==1){if(!HasAnimalRoute(b))PlanAnimalRoute(b,b.home,true);}
            else if(b.migrationState==3){if(!HasAnimalRoute(b))PlanAnimalRoute(b,b.home);}
            else if(b.angry){PlanAnimalRoute(b,Feet);b.navRefresh=clock+.8f;}
            else if(b.follow>0&&!circleAnimals.Contains(b)){b.navRefresh=clock+.4f+Mathf.Repeat(b.phase,.2f);var goal=Feet+b.socialOffset+new Vector3(Mathf.Sin(clock*.67f+b.phase)*.24f,0,Mathf.Cos(clock*.43f+b.phase)*.20f);
                if(!CircleStepClear(goal,b)){if(!TryFriendlyDestination(b,out goal)){b.navRefresh=clock+.35f;goal=b.root.position;}b.socialOffset=goal-Feet;}
                if(Vector3.Distance(b.root.position,goal)>.18f)PlanFriendlyRoute(b,goal);
            }else if(b.follow<=0)PlanQuietAnimalRoam(b);
        }
        UpdateCircleDestination(b);
        float speed=feeding?0:b.angry?1.8f:b.fleeing?PanickedAnimalFleeSpeed:b.migrationState!=0?1.05f:b.follow>0?(circleAnimals.Contains(b)?FriendlyApproachSpeed:Vector3.Distance(b.root.position,Feet+b.socialOffset)<.5f?.5f:Mathf.Lerp(FriendlyApproachSpeed,FriendlyCatchupSpeed,Mathf.InverseLerp(1.5f,4.5f,Vector3.Distance(b.root.position,Feet+b.socialOffset)))):QuietAnimalSpeed(b);
        b.moveSpeed=Mathf.MoveTowards(b.moveSpeed,HasAnimalRoute(b)?speed:0,dt*(b.fleeing?8f:b.follow>0?2.5f:1.2f));
        if(HasAnimalRoute(b)){
            Vector3 waypoint=b.route[b.routeIndex],delta=waypoint-b.root.position;
            if(delta.magnitude<.11f)b.routeIndex++;
            else {Vector3 next=Vector3.MoveTowards(b.root.position,waypoint,b.moveSpeed*Mathf.Min(dt,.1f));bool clear=b.follow>0?CircleStepClear(next,b):ClearAnimalStep(next,b);
                if(clear){Vector3 actual=next-b.root.position;b.root.position=next;b.blockedTime=0;if(Mathf.Abs(actual.x)>.0001f&&b.speciesIndex!=4)b.renderer.flipX=actual.x<0;}
                else {b.blockedTime+=dt;if(b.blockedTime>1.3f){b.route.Clear();b.navRefresh=clock+.5f;if(b.follow>0){b.navRefresh=clock+.2f;Vector3 alternative;if(TryFriendlyDestination(b,out alternative))b.socialOffset=alternative-Feet;}}}
            }
        }
        if(b.migrationState==1&&clock>=b.proximityFleeUntil&&!HasAnimalRoute(b)&&Vector3.Distance(b.root.position,b.navGoal)<.35f){
            if(AnimalOutsideViewAt(b,b.root.position)){b.migrationState=2;b.root.gameObject.SetActive(false);departuresCompleted++;return;}
            b.navRefresh=Mathf.Min(b.navRefresh,clock+.5f);
        }
        if(b.migrationState==3&&!HasAnimalRoute(b)&&Vector3.Distance(b.root.position,b.home)<2){b.migrationState=0;b.fleeing=false;b.socialReadyAt=clock+4;}
        b.art.localPosition=new Vector3(0,.025f*Mathf.Sin(clock*3+b.phase),0);b.renderer.color=AnimalEcologyTint();b.renderer.sortingOrder=DepthOrder(b.root.position.z);ApplyFeedingPose(b);
        if(b.angry&&Vector3.Distance(Feet,b.root.position)<.85f&&b.cooldown<=0&&damageCooldown<=0&&hop<.4f){health--;damageCooldown=1;b.cooldown=1.5f;Tell("The animal is defending itself");if(health<=0){player.position=ForestSpawn;lastDryPosition=player.position;health=5;b.angry=false;}}
    }
}
}

