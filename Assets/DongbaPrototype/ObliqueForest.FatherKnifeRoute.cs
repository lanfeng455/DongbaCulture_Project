using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Return only: use the existing animal navigation grid, heap and father's route
    // storage. Ordinary hunting/following retains its current steering behaviour.
    bool FatherRouteCrossesRiver(Vector3 from,Vector3 to){
        return (from.x-RiverX(from.z))*(to.x-RiverX(to.z))<0;
    }

    // An exact nearest-point test catches the thin intersections between the old
    // 0.1 m samples. Use it for planning, smoothing AND motion so they agree.
    static bool FatherRouteCircleClear(double ax,double az,double bx,double bz,double cx,double cz,double radius){
        double dx=bx-ax,dz=bz-az,length=dx*dx+dz*dz;
        double t=length>0?((cx-ax)*dx+(cz-az)*dz)/length:0;
        if(t<0)t=0;else if(t>1)t=1;
        double x=ax+dx*t-cx,z=az+dz*t-cz;
        return x*x+z*z>=radius*radius;
    }

    bool FatherKnifeSegmentClear(Vector3 from,Vector3 to){
        // Preserve the existing river, map boundary, home and hearth rules.
        if(!StoryFatherWalkingSegmentClear(from,to))return false;
        foreach(var tree in plants)if(tree.tree&&tree.health>0&&!FatherRouteCircleClear(from.x,from.z,to.x,to.z,tree.home.x,tree.home.z,.8f))return false;
        foreach(var animal in animals)if(animal.health>0&&animal.migrationState!=2&&!FatherRouteCircleClear(from.x,from.z,to.x,to.z,animal.root.position.x,animal.root.position.z,.65f))return false;
        return true;
    }

    void MoveFatherAlongKnifeRoute(Vector3 destination,float speed,float dt){
        var father=storyFather;
        bool changed=(father.navGoal-destination).sqrMagnitude>.01f;
        if(changed||!HasAnimalRoute(father)){
            if(!changed&&clock<father.navRefresh)return;
            father.navRefresh=clock+.65f;
            if(!PlanFatherKnifeRoute(destination))return;
        }
        var start=storyFatherRoot.position;
        while(HasAnimalRoute(father)&&Vector3.Distance(start,father.route[father.routeIndex])<.045f)father.routeIndex++;
        if(!HasAnimalRoute(father))return;
        var next=father.route[father.routeIndex];
        if(FatherRouteCrossesRiver(start,next)){
            // Crossing links are never ordinary movement: the existing central
            // jump tick owns both the ground anchor and the visible jump arc.
            if(!StoryFatherStepClear(start)||!StoryFatherStepClear(next)){
                father.route.Clear();father.navRefresh=clock+.35f;return;
            }
            fatherJumpFrom=start;fatherJumpTo=next;fatherJumpAge=0;father.routeIndex++;
            return;
        }
        // Remove staircase motion only where the complete ground segment is clear.
        int lookAheadEnd=Mathf.Min(father.route.Count-1,father.routeIndex+7);
        for(int i=father.routeIndex+1;i<=lookAheadEnd;i++){
            var candidate=father.route[i];
            if(FatherRouteCrossesRiver(start,candidate)||!FatherKnifeSegmentClear(start,candidate))break;
            next=candidate;father.routeIndex=i;
        }
        var point=Vector3.MoveTowards(start,next,speed*Mathf.Min(dt,.1f));
        if(FatherKnifeSegmentClear(start,point)){
            storyFatherRoot.position=point;father.blockedTime=0;
        }else{
            // A roaming animal can enter a previously valid route. Replan at a
            // bounded rate; never force through the animal, tree or river bank.
            father.blockedTime+=dt;
            if(father.blockedTime>=.18f){father.route.Clear();father.navRefresh=clock+.2f;}
        }
    }

    int ClosestFatherRouteNode(Vector3 position,bool needsConnection){
        // A tangent position may have no visible grid centre within one cell.
        // Search farther on the same bank, with an exact unobstructed connection;
        // the destination remains within the existing 0.9 m encounter distance.
        float radius=needsConnection?1.8f:.85f;
        int index=NavIndex(position),best=-1,reach=Mathf.CeilToInt(radius/NavCell);float distance=radius*radius;
        for(int dz=-reach;dz<=reach;dz++)for(int dx=-reach;dx<=reach;dx++){
            int x=index%NavW+dx,z=index/NavW+dz;
            if(x<0||x>=NavW||z<0||z>=NavH)continue;
            int node=z*NavW+x;if(!navWalk[node])continue;
            var point=NavPoint(node);float d=(point-position).sqrMagnitude;
            if(d>=distance||FatherRouteCrossesRiver(position,point))continue;
            if(needsConnection&&!FatherKnifeSegmentClear(position,point))continue;
            best=node;distance=d;
        }
        return best;
    }

    void OfferFatherRouteNode(int from,int next,int goal){
        if(next<0||next>=NavN||!navWalk[next]||navClosed[next])return;
        float cost=navCost[from]+Vector3.Distance(NavPoint(from),NavPoint(next));
        if(cost>=navCost[next])return;
        navCost[next]=cost;navParent[next]=from;
        HeapPush(next,cost+Vector3.Distance(NavPoint(next),NavPoint(goal)));
    }

    bool PlanFatherKnifeRoute(Vector3 destination){
        var father=storyFather;father.route.Clear();father.routeIndex=0;father.navGoal=destination;
        var origin=storyFatherRoot.position;
        if(!FatherRouteCrossesRiver(origin,destination)&&FatherKnifeSegmentClear(origin,destination)){
            father.route.Add(destination);father.blockedTime=0;return true;
        }
        // Exactly the father's existing footprint rules, not animal canopy bounds.
        for(int n=0;n<NavN;n++){
            navWalk[n]=StoryFatherStepClear(NavPoint(n));navClosed[n]=false;
            navCost[n]=float.PositiveInfinity;navParent[n]=-1;
        }
        int start=ClosestFatherRouteNode(origin,true),goal=ClosestFatherRouteNode(destination,false);
        if(start<0||goal<0)return false;
        navHeapCount=0;navCost[start]=0;HeapPush(start,0);int found=-1;
        while(navHeapCount>0){
            int n=HeapPop();if(navClosed[n])continue;navClosed[n]=true;
            if(n==goal){found=n;break;}
            var point=NavPoint(n);int nx=n%NavW,nz=n/NavW;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){
                if(dx==0&&dz==0)continue;int x=nx+dx,z=nz+dz;
                if(x<0||x>=NavW||z<0||z>=NavH)continue;
                int next=z*NavW+x;
                if(!navWalk[next]||navClosed[next])continue;
                if(dx!=0&&dz!=0&&(!navWalk[nz*NavW+x]||!navWalk[z*NavW+nx]))continue;
                if(!FatherKnifeSegmentClear(point,NavPoint(next)))continue;
                OfferFatherRouteNode(n,next,goal);
            }
            float bankOffset=point.x-RiverX(point.z);
            if(Mathf.Abs(bankOffset)>=1.3f&&Mathf.Abs(bankOffset)<=1.95f){
                var landing=new Vector3(RiverX(point.z)-Mathf.Sign(bankOffset)*1.5f,0,point.z);
                int across=NavIndex(landing);
                if(navWalk[across]&&FatherRouteCrossesRiver(point,NavPoint(across)))OfferFatherRouteNode(n,across,goal);
            }
        }
        if(found<0)return false;
        for(int n=found;n>=0;n=navParent[n]){father.route.Add(NavPoint(n));if(n==start)break;}
        father.route.Reverse();
        var last=father.route[father.route.Count-1];
        if(!FatherRouteCrossesRiver(last,destination)&&FatherKnifeSegmentClear(last,destination))father.route.Add(destination);
        father.blockedTime=0;return true;
    }
} }
