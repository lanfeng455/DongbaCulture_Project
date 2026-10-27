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
    public int MaxFriendlyAnimals=4;
    public float FriendlyApproachSpeed=2.2f, FriendlyCatchupSpeed=4.3f;
    bool friendlyWeather; float friendlyTransition,nextVisitAt;
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
        for(int n=0;n<NavN;n++){Vector3 p=NavPoint(n);navWalk[n]=Mathf.Abs(p.x)<ForestHalfWidth-1&&Mathf.Abs(p.z)<ForestHalfDepth-.8f&&Mathf.Abs(p.x-RiverX(p.z))>1.23f;navClosed[n]=false;navCost[n]=float.PositiveInfinity;navParent[n]=-1;}
        float aw=animal.renderer.sprite.bounds.size.x*animal.initialScale.x*animal.birth;
        float ah=animal.renderer.sprite.bounds.size.y*animal.initialScale.y*animal.birth;
        float projection=Mathf.Max(.2f,cam.transform.up.z);
        // Same upright-glyph clearance as AnimalInkClear, converted to ground coordinates.
        // Individual source silhouettes retain their actual width and height.
        foreach(var plant in plants){if(plant.health<=0)continue;var size=plant.fullGlyph.bounds.size;var scale=plant.art.localScale;
            float pw=size.x*scale.x*.92f,ph=size.y*scale.y*.95f;
            float left=plant.home.x-(pw+aw)*.5f-.13f,right=plant.home.x+(pw+aw)*.5f+.13f;
            float bottom=plant.home.z-ah/projection-.15f,top=plant.home.z+ph/projection+.15f;
            int x0=Mathf.Clamp(Mathf.CeilToInt((left+45)/NavCell),0,NavW-1),x1=Mathf.Clamp(Mathf.FloorToInt((right+45)/NavCell),0,NavW-1);
            int z0=Mathf.Clamp(Mathf.CeilToInt((bottom+21)/NavCell),0,NavH-1),z1=Mathf.Clamp(Mathf.FloorToInt((top+21)/NavCell),0,NavH-1);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)navWalk[z*NavW+x]=false;
        }
        // Match the existing root clearances as well as the drawn glyph silhouette.
        foreach(var plant in plants){float radius=plant.tree?1.08f:.50f;StampNavCircle(plant.home,radius);}
        // Route around other animals instead of repeatedly choosing a blocked corridor.
        foreach(var other in animals){if(other==animal||other.health<=0||other.migrationState==2)continue;
            float ow=other.renderer.sprite.bounds.size.x*other.initialScale.x*other.birth;
            float oh=other.renderer.sprite.bounds.size.y*other.initialScale.y*other.birth;
            float left=other.root.position.x-(ow+aw)*.5f-.12f,right=other.root.position.x+(ow+aw)*.5f+.12f;
            float bottom=other.root.position.z-ah/projection-.12f,top=other.root.position.z+oh/projection+.12f;
            int x0=Mathf.Clamp(Mathf.CeilToInt((left+45)/NavCell),0,NavW-1),x1=Mathf.Clamp(Mathf.FloorToInt((right+45)/NavCell),0,NavW-1);
            int z0=Mathf.Clamp(Mathf.CeilToInt((bottom+21)/NavCell),0,NavH-1),z1=Mathf.Clamp(Mathf.FloorToInt((top+21)/NavCell),0,NavH-1);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)navWalk[z*NavW+x]=false;
            StampNavCircle(other.root.position,.75f);
        }
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
    void EndAnimalVisit(Being b){b.follow=0;b.socialReadyAt=clock+10+Mathf.Repeat(b.phase,5);b.navRefresh=clock+.3f;if(b.route!=null)b.route.Clear();}
    bool TryFriendlyDestination(Being b,out Vector3 goal){
        for(int i=0;i<12;i++){float a=b.phase*1.37f+i*2.39996f;float r=1.4f+Mathf.Repeat(b.phase*.37f+i*.41f,1.0f);goal=Feet+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            if(!ClearAnimalStep(goal,b))continue;bool spaced=true;foreach(var other in animals)if(other!=b&&other.follow>0&&Vector3.Distance(goal,Feet+other.socialOffset)<1.35f){spaced=false;break;}if(spaced)return true;
        }goal=Vector3.zero;return false;
    }
    void UpdatePopulation(float dt){
        bool requested=friendlyWeather?Balance>FlourishThreshold-3:Balance>=FlourishThreshold;
        if(requested!=friendlyWeather){friendlyTransition+=dt;if(friendlyTransition>2){friendlyWeather=requested;friendlyTransition=0;}}else friendlyTransition=0;
        if(clock>=nextVisitAt){nextVisitAt=clock+.65f+Mathf.Repeat(migrationSequence*.71f,.55f);int participants=0;foreach(var a in animals)if(a.follow>0)participants++;
            int limit=Mathf.Clamp(3+(Balance-FlourishThreshold)/6,1,MaxFriendlyAnimals);
            if(friendlyWeather&&participants<limit){Being choice=null;float best=FriendlyRadius;
                foreach(var a in animals){float d=Vector3.Distance(a.root.position,Feet);if(a.health<=0||a.migrationState!=0||a.angry||a.age<2||a.follow>0||clock<a.socialReadyAt||clock<a.protectedUntil||clock<a.careReady)continue;if(d<best){best=d;choice=a;}}
                if(choice!=null){if(TryFriendlyDestination(choice,out var goal)&&PlanAnimalRoute(choice,goal)){choice.socialOffset=goal-Feet;choice.follow=20+Mathf.Repeat(choice.phase,6);choice.navRefresh=clock+.4f;visitsStarted++;}else choice.socialReadyAt=clock+5;}
            }
            if(!friendlyWeather){foreach(var a in animals)if(a.follow>0){EndAnimalVisit(a);break;}}
        }
        if(clock<populationCheckAt)return;populationCheckAt=clock+2.6f+Mathf.Repeat(migrationSequence++*.73f,1.2f);
        float decline=Mathf.Clamp01(-ecologyVisual);Being departure=null,returner=null;float bestDeparture=float.NegativeInfinity,bestReturn=float.PositiveInfinity;int movingOut=0;
        foreach(var a in animals)if(a.migrationState==1)movingOut++;
        for(int region=0;region<populationRegionCount;region++){
            int current=0;foreach(var a in animals)if(a.health>0&&a.populationRegion==region&&a.migrationState!=1&&a.migrationState!=2)current++;
            int target=Mathf.Max(populationCapacity[region]>0?1:0,Mathf.CeilToInt(populationCapacity[region]*Mathf.Lerp(1,.35f,decline)));
            foreach(var a in animals){if(a.health<=0||a.populationRegion!=region)continue;
                if(decline>.16f&&current>target&&a.migrationState==0&&!a.angry&&a.age==2&&a.birth>.98f&&clock>=a.protectedUntil&&clock>=a.careReady&&clock>=a.migrationTimer){float score=30-Vector3.Distance(a.root.position,Feet);if(!AnimalOutsideViewAt(a,a.root.position))score+=20;if(score>bestDeparture){bestDeparture=score;departure=a;}}
                if(current<target&&a.migrationState==2&&AnimalOutsideViewAt(a,a.root.position)&&ClearAnimalStep(a.root.position,a)){float d=Vector3.Distance(a.home,Feet);if(d<bestReturn){bestReturn=d;returner=a;}}
            }
        }
        // One global departure/return per tick: no simultaneous region-wide group turns.
        if(departure!=null&&movingOut<3){EndAnimalVisit(departure);if(PlanAnimalRoute(departure,departure.home,true)){departure.migrationState=1;departure.navRefresh=clock+2;}else departure.migrationTimer=clock+5;}
        else if(returner!=null){if(PlanAnimalRoute(returner,returner.home)){if(!AnimalOutsideViewAt(returner,returner.root.position))visibleSpawnErrors++;returner.migrationState=3;returner.opacity=1;returner.root.gameObject.SetActive(true);returner.navRefresh=clock+2;returnsStarted++;}}
    }
    void UpdateAnimalEcology(Being b,float dt,float hop){
        if(b.health<=0||b.migrationState==2)return;
        b.birth=Mathf.MoveTowards(b.birth,b.age==0?.42f:b.age==1?.7f:1,dt*.65f);b.art.localScale=b.initialScale*b.birth;b.cooldown-=dt;
        if(b.angry){if(b.follow>0||b.migrationState!=0){EndAnimalVisit(b);b.migrationState=0;}b.socialReadyAt=clock+20;}
        if(b.follow>0){b.follow=Mathf.Max(0,b.follow-dt);if(b.follow==0||Vector3.Distance(Feet,b.root.position)>FriendlyRadius+3)EndAnimalVisit(b);}
        if(b.migrationState==1&&ecologyVisual>=-.1f){b.migrationState=3;if(b.route!=null)b.route.Clear();b.navRefresh=clock;}
        if(clock>=b.navRefresh){
            b.navRefresh=clock+2.2f+Mathf.Repeat(b.phase,.8f);
            if(b.migrationState==1){if(!HasAnimalRoute(b))PlanAnimalRoute(b,b.home,true);}
            else if(b.migrationState==3){if(!HasAnimalRoute(b))PlanAnimalRoute(b,b.home);}
            else if(b.angry){PlanAnimalRoute(b,Feet);b.navRefresh=clock+.8f;}
            else if(b.follow>0){b.navRefresh=clock+.4f+Mathf.Repeat(b.phase,.2f);var goal=Feet+b.socialOffset+new Vector3(Mathf.Sin(clock*.67f+b.phase)*.24f,0,Mathf.Cos(clock*.43f+b.phase)*.20f);
                if(!ClearAnimalStep(goal,b)){if(!TryFriendlyDestination(b,out goal)){EndAnimalVisit(b);return;}b.socialOffset=goal-Feet;}
                if(Vector3.Distance(b.root.position,goal)>.18f)PlanAnimalRoute(b,goal);
            }else{Vector3 goal=b.home+new Vector3(Mathf.Sin(clock*.5f+b.phase)*.6f,0,Mathf.Cos(clock*.4f+b.phase)*.4f);PlanAnimalRoute(b,goal);}
        }
        float speed=b.angry?1.8f:b.migrationState!=0?1.05f:b.follow>0?(Vector3.Distance(b.root.position,Feet+b.socialOffset)<.5f?.5f:Mathf.Lerp(FriendlyApproachSpeed,FriendlyCatchupSpeed,Mathf.InverseLerp(1.5f,4.5f,Vector3.Distance(b.root.position,Feet+b.socialOffset)))):.3f;
        b.moveSpeed=Mathf.MoveTowards(b.moveSpeed,HasAnimalRoute(b)?speed:0,dt*(b.follow>0?2.5f:1.2f));
        if(HasAnimalRoute(b)){
            Vector3 waypoint=b.route[b.routeIndex],delta=waypoint-b.root.position;
            if(delta.magnitude<.11f)b.routeIndex++;
            else {Vector3 next=Vector3.MoveTowards(b.root.position,waypoint,b.moveSpeed*Mathf.Min(dt,.1f));bool clear=ClearAnimalStep(next,b);
                if(clear){Vector3 actual=next-b.root.position;b.root.position=next;b.blockedTime=0;if(Mathf.Abs(actual.x)>.0001f&&b.speciesIndex!=4)b.renderer.flipX=actual.x<0;}
                else {b.blockedTime+=dt;if(b.blockedTime>1.3f){b.route.Clear();b.navRefresh=clock+.5f;if(b.follow>0)EndAnimalVisit(b);}}
            }
        }
        if(b.migrationState==1&&!HasAnimalRoute(b)&&Vector3.Distance(b.root.position,b.navGoal)<.35f){
            if(AnimalOutsideViewAt(b,b.root.position)){b.migrationState=2;b.root.gameObject.SetActive(false);departuresCompleted++;return;}
            b.navRefresh=Mathf.Min(b.navRefresh,clock+.5f);
        }
        if(b.migrationState==3&&!HasAnimalRoute(b)&&Vector3.Distance(b.root.position,b.home)<2){b.migrationState=0;b.socialReadyAt=clock+4;}
        b.art.localPosition=new Vector3(0,.025f*Mathf.Sin(clock*3+b.phase),0);b.renderer.color=AnimalEcologyTint();b.renderer.sortingOrder=100-Mathf.RoundToInt(b.root.position.z*10);
        if(b.angry&&Vector3.Distance(Feet,b.root.position)<.85f&&b.cooldown<=0&&damageCooldown<=0&&hop<.4f){health--;damageCooldown=1;b.cooldown=1.5f;Tell("The animal is defending itself");if(health<=0){player.position=ForestSpawn;lastDryPosition=player.position;health=5;b.angry=false;}}
    }
}
}

