using System;
using System.Collections.Generic;
using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Composition keeps the actual Being, not a species-only replacement. Inactive lives
    // are deliberately absent from the ecology population and collision lists.
    enum NatureAction { Move, Plant, Sleep, Restore, Range, Leave, Recall, Invite }
    sealed class NatureUndo {
        public Being being; public Vector3 home; public bool active, resident, created;
    }
    sealed class NatureMotion {
        public Being being; public NatureAction action; public Vector3 from, to, gate, outside;
        public Vector3 scale; public float age, blocked, retry; public int phase;
        public bool destroyAfter; public NatureMark mark;
    }
    sealed class NatureMark { public Transform root; public SpriteRenderer seed,wind; }
    readonly List<Being> sleepingPlants=new List<Being>(),forestCompanions=new List<Being>();
    readonly List<Being> natureInitialPlants=new List<Being>(),natureInitialAnimals=new List<Being>();
    readonly Dictionary<Being,Vector3> natureOriginalHomes=new Dictionary<Being,Vector3>();
    readonly Dictionary<Being,int> natureIdentities=new Dictionary<Being,int>();
    readonly List<NatureMotion> natureMotions=new List<NatureMotion>();
    readonly List<NatureUndo> natureHistory=new List<NatureUndo>(12);
    readonly List<NatureMark> natureMarks=new List<NatureMark>(12);
    bool natureUnlocked,natureReady; int natureNextIdentity;
    public bool NatureEditorOpen { get; private set; }
    bool NatureAvailable=>natureReady&&!StoryModeEnabled&&natureUnlocked;
    string natureMessage="",natureMessageEn="",natureMessageZh=""; float natureMessageUntil;
    const int NatureHistoryLimit=12, NatureExtraPlantLimit=120;

    void SetupNatureEditing(){
        natureInitialPlants.AddRange(plants);natureInitialAnimals.AddRange(animals);
        foreach(var b in plants){natureOriginalHomes[b]=b.home;NatureIdentity(b);}
        foreach(var b in animals)NatureIdentity(b);
        for(int i=0;i<12;i++){
            var root=new GameObject("Nature invitation / pooled ink seed").transform;
            var seed=Billboard("Ink seed (animation, not a glyph)",root,uiInkDot,.18f).GetComponent<SpriteRenderer>();
            var wind=Billboard("Existing wind glyph",root,stormMarkSprite,1f).GetComponent<SpriteRenderer>();
            seed.color=new Color(.28f,.20f,.10f,1);wind.color=new Color(1,1,1,.16f);
            root.gameObject.SetActive(false);natureMarks.Add(new NatureMark{root=root,seed=seed,wind=wind});
        }
        natureReady=true;BuildNatureUI();
    }
    int NatureIdentity(Being b){if(!natureIdentities.TryGetValue(b,out int id)){id=++natureNextIdentity;natureIdentities.Add(b,id);}return id;}
    void NatureSay(string en,string zh){natureMessageEn=en;natureMessageZh=zh;natureMessage=DongbaInterface.Choose(en,zh);natureMessageUntil=clock+4;}
    bool NatureActive(Being b)=>b.animal?animals.Contains(b):plants.Contains(b);
    bool NatureReserved(Vector3 p,Being moving=null,bool playerStep=false){
        foreach(var m in natureMotions){
            if(m.being==moving||m.action==NatureAction.Sleep||m.action==NatureAction.Leave)continue;
            if(playerStep&&!m.being.tree)continue;
            float radius=playerStep?.55f:m.being.animal?.95f:AnimalPlantRadius(m.being);
            if(Vector3.Distance(p,m.to)<radius)return true;
        }
        return false;
    }
    void UpdateNatureUnlock(){
        if(StoryModeEnabled){if(NatureEditorOpen)CloseNatureEditor();return;}
        if(!natureUnlocked&&Balance>=FlourishThreshold){natureUnlocked=true;NatureSay("The forest answers. B: invite and arrange.","森林愿意回应了。按 B，邀请万物一起布置。 ");}
    }
    void NatureWake(Being b){
        sleepingPlants.Remove(b);forestCompanions.Remove(b);
        if(!NatureActive(b)){if(b.animal){animals.Add(b);populationCapacity[b.populationRegion]++;}else plants.Add(b);}
        b.root.gameObject.SetActive(true);
    }
    void NatureArchive(Being b){
        if(b.animal){if(animals.Remove(b))populationCapacity[b.populationRegion]=Mathf.Max(0,populationCapacity[b.populationRegion]-1);if(!forestCompanions.Contains(b))forestCompanions.Add(b);}
        else{plants.Remove(b);if(!sleepingPlants.Contains(b))sleepingPlants.Add(b);}
        b.root.gameObject.SetActive(false);b.editBusy=false;
    }
    void NatureForget(Being b){
        if(b.animal){if(animals.Remove(b))populationCapacity[b.populationRegion]=Mathf.Max(0,populationCapacity[b.populationRegion]-1);forestCompanions.Remove(b);animalMoments.Remove(b);circleAnimals.Remove(b);}
        else{plants.Remove(b);sleepingPlants.Remove(b);sproutRevealStarts.Remove(b);stormBindings.Remove(b);}
        natureIdentities.Remove(b);if(b.root!=null)Destroy(b.root.gameObject);
    }
    void ResetNatureEditing(){
        if(!natureReady)return;
        CloseNatureEditor();foreach(var m in natureMotions)if(m.mark!=null)m.mark.root.gameObject.SetActive(false);natureMotions.Clear();natureHistory.Clear();
        var all=new HashSet<Being>(plants);all.UnionWith(animals);all.UnionWith(sleepingPlants);all.UnionWith(forestCompanions);
        foreach(var b in all)if(!natureInitialPlants.Contains(b)&&!natureInitialAnimals.Contains(b))NatureForget(b);
        plants.Clear();plants.AddRange(natureInitialPlants);animals.Clear();animals.AddRange(natureInitialAnimals);
        foreach(var b in plants){b.home=natureOriginalHomes[b];b.editBusy=b.editResident=false;b.root.localScale=Vector3.one;if(b.groundRange!=null)b.groundRange.enabled=true;}
        foreach(var b in animals){b.editBusy=b.editResident=false;b.root.localScale=Vector3.one;}
        sleepingPlants.Clear();forestCompanions.Clear();natureUnlocked=false;natureMessage="";natureMessageUntil=0;
    }
    bool NaturePlacement(Vector3 p,Being moving,bool animal,bool tree,out string reason){
        reason="";p.y=0;
        if(!AnimalTerrainClear(p)){reason=DongbaInterface.Choose("Choose dry ground inside the forest.","请选择林地内的干燥地面。");return false;}
        if(Vector3.Distance(p,Feet)<(tree?.9f:animal?1.05f:.45f)){reason=DongbaInterface.Choose("Leave room for Gaolequ.","给高勒趣留一点走动空间。");return false;}
        foreach(var b in plants)if(b!=moving&&Vector3.Distance(p,b.home)<(tree?(b.tree?1.75f:.65f):animal?AnimalPlantRadius(b)+.35f:b.tree?.8f:.37f)){
            reason=DongbaInterface.Choose("Another plant needs this space.","这里离其他植物太近了。");return false;
        }
        foreach(var b in animals)if(b!=moving&&b.health>0&&b.migrationState!=2&&Vector3.Distance(p,b.root.position)<(animal?1.1f:tree?1.25f:.5f)){
            reason=DongbaInterface.Choose("An animal is passing here.","有动物正在这里活动。");return false;
        }
        foreach(var m in natureMotions)if(m.being!=moving&&m.action!=NatureAction.Sleep&&m.action!=NatureAction.Leave&&Vector3.Distance(p,m.to)<(tree?1.6f:1f)){
            reason=DongbaInterface.Choose("This place is already reserved.","另一位伙伴正要到这里来。");return false;
        }
        return true;
    }
    bool NatureRoute(Being b,Vector3 target){return PlanAnimalRoute(b,target)&&Vector3.Distance(b.navGoal,target)<.85f;}
    bool NatureGate(Being b,Vector3 target,bool arriving,out Vector3 gate,out Vector3 outside){
        // Real forest perimeter, not the current camera edge: also readable from MR's overview.
        var candidates=new List<Vector3>(44);
        for(int i=0;i<11;i++){
            float shift=(i==0?0:((i+1)/2)*(i%2==0?-1:1)*2.4f);
            candidates.Add(new Vector3(Mathf.Clamp(target.x+shift,-ForestHalfWidth+2,ForestHalfWidth-2),0,ForestHalfDepth-1.5f));
            candidates.Add(new Vector3(Mathf.Clamp(target.x+shift,-ForestHalfWidth+2,ForestHalfWidth-2),0,-ForestHalfDepth+1.5f));
            candidates.Add(new Vector3(-ForestHalfWidth+1.5f,0,Mathf.Clamp(target.z+shift,-ForestHalfDepth+2,ForestHalfDepth-2)));
            candidates.Add(new Vector3(ForestHalfWidth-1.5f,0,Mathf.Clamp(target.z+shift,-ForestHalfDepth+2,ForestHalfDepth-2)));
        }
        candidates.Sort((a,c)=>(a-target).sqrMagnitude.CompareTo((c-target).sqrMagnitude));
        var original=b.root.position;
        foreach(var candidate in candidates){
            if(!CircleStepClear(candidate,b))continue;
            var edgeDirection=Mathf.Abs(candidate.x)>ForestHalfWidth-2?new Vector3(Mathf.Sign(candidate.x),0,0):new Vector3(0,0,Mathf.Sign(candidate.z));
            bool clearEdge=true;for(int step=1;step<=16;step++)if(!NatureBoundaryClear(candidate+edgeDirection*(step*.25f),b)){clearEdge=false;break;}
            if(!clearEdge)continue;
            if(arriving)b.root.position=candidate;
            bool path=NatureRoute(b,arriving?target:candidate);b.root.position=original;
            if(!path)continue;
            gate=candidate;var outward=Mathf.Abs(candidate.x)>ForestHalfWidth-2?new Vector3(Mathf.Sign(candidate.x),0,0):new Vector3(0,0,Mathf.Sign(candidate.z));
            outside=gate+outward*4f;return true;
        }
        gate=outside=Vector3.zero;b.root.position=original;return false;
    }
    bool NatureBoundaryClear(Vector3 p,Being moving){
        if(River(p))return false;
        foreach(var b in plants)if(Vector3.Distance(p,b.home)<AnimalPlantRadius(b)+.05f)return false;
        foreach(var b in animals)if(b!=moving&&b.health>0&&b.migrationState!=2&&Vector3.Distance(p,b.root.position)<.95f)return false;
        return true;
    }
    void NatureSetHome(Being b,Vector3 p){
        if(b.animal){int region=PopulationRegion(p.x);if(region!=b.populationRegion){if(animals.Contains(b)){populationCapacity[b.populationRegion]=Mathf.Max(0,populationCapacity[b.populationRegion]-1);populationCapacity[region]++;}b.populationRegion=region;}}
        b.home=p;
    }
    void NaturePrepare(Being b){
        b.editBusy=true;b.shake=0;
        if(stormLoadingTree==b)CancelStormLoading(true);
        foreach(var mark in fruitMarks)if(mark.source==b)mark.root.gameObject.SetActive(false);
        if(b.animal){EndAnimalVisit(b);circleAnimals.Remove(b);b.migrationState=0;b.fleeing=false;b.angry=false;b.proximityFleeUntil=0;b.moveSpeed=0;b.blockedTime=0;}
        if(b.groundRange!=null)b.groundRange.enabled=false;
    }
    void NaturePushUndo(Being b,bool created){
        if(natureHistory.Count==NatureHistoryLimit)natureHistory.RemoveAt(0);
        natureHistory.Add(new NatureUndo{being=b,home=b.home,active=NatureActive(b),resident=b.editResident,created=created});
    }
    bool NatureRequest(NatureAction action,Being b,string key,int species,Vector3 position,bool record=true){
        if(!NatureAvailable||natureMotions.Count>=12)return false;
        bool isAnimal=action==NatureAction.Range||action==NatureAction.Leave||action==NatureAction.Recall||action==NatureAction.Invite;
        bool creating=action==NatureAction.Plant||action==NatureAction.Invite;
        if(!creating&&(b==null||b.health<=0||b.editBusy||b.angry))return false;
        if(!creating){bool archived=b.animal?forestCompanions.Contains(b):sleepingPlants.Contains(b);bool restoring=action==NatureAction.Restore||action==NatureAction.Recall;if(restoring!=archived)return false;}
        bool tree=creating&&!isAnimal?key=="plant_00"||key=="plant_01"||key=="plant_02":b!=null&&b.tree;
        bool removing=action==NatureAction.Sleep||action==NatureAction.Leave;
        if(!removing&&!NaturePlacement(position,b,isAnimal,tree,out var invalid)){NatureSay(invalid,invalid);return false;}
        if(creating){
            if(isAnimal){if(animals.Count+forestCompanions.Count>=AnimalPopulationLimit){NatureSay("The forest has enough animal companions for now.","当前伙伴数量已达上限，先调整已有伙伴吧。");return false;}AddDictionaryAnimal(position,species);b=animals[animals.Count-1];populationCapacity[b.populationRegion]++;}
            else{
                if(plants.Count+sleepingPlants.Count>=initialPlantCount+NatureExtraPlantLimit||!dictionarySprites.ContainsKey(key))return false;
                float height=tree?2.3f:.65f;foreach(var sample in plants)if(sample.glyphKey==key){height=sample.fullGlyph.bounds.size.y*sample.initialScale.y;break;}
                b=AddDictionaryPlant(position,tree,plants.Count,key,height);
                if(tree){var binding=new StormBinding();for(int i=0;i<7;i++)binding.blocks[i]=new MaterialPropertyBlock();stormBindings[b]=binding;}
            }
            NatureIdentity(b);b.root.gameObject.SetActive(false);
        }
        Vector3 gate=Vector3.zero,outside=Vector3.zero;
        bool arrives=action==NatureAction.Invite||action==NatureAction.Recall;
        if(isAnimal){
            bool valid=arrives?NatureGate(b,position,true,out gate,out outside):action==NatureAction.Leave?NatureGate(b,b.root.position,false,out gate,out outside):NatureRoute(b,position);
            if(!valid){if(creating)NatureForget(b);NatureSay("No clear route. Try another place on this bank.","暂时找不到通路，请试试这边河岸的其他空地。");return false;}
        }
        if(record)NaturePushUndo(b,creating);
        var motion=new NatureMotion{being=b,action=action,from=b.root.position,to=removing?b.home:position,gate=gate,outside=outside,scale=b.art.localScale};
        NaturePrepare(b);NatureWake(b);
        if(arrives){b.root.position=outside;b.renderer.color=Color.clear;motion.phase=0;}
        else if(isAnimal)motion.phase=1;
        else{
            foreach(var mark in natureMarks)if(!mark.root.gameObject.activeSelf){motion.mark=mark;mark.root.gameObject.SetActive(true);break;}
            if(action==NatureAction.Plant||action==NatureAction.Restore){b.root.position=position;b.art.localScale=Vector3.zero;motion.scale=b.initialScale*b.targetScale;}
        }
        natureMotions.Add(motion);
        if(arrives){
            playerMotion.Care();if(Mathf.Abs(position.x-Feet.x)>.1f)facing=position.x>Feet.x?1:-1;PlayForestSfx("feed",Feet);
            Being edgePlant=null;float distance=7;foreach(var plant in plants)if(!plant.editBusy&&plant.health>0){float d=Vector3.Distance(plant.home,gate);if(d<distance){distance=d;edgePlant=plant;}}
            if(edgePlant!=null)edgePlant.shake=.09f;
        }
        NatureSay("An invitation, answered.","邀请已送出。");return true;
    }
    void NatureCancelMotion(Being b){
        for(int i=natureMotions.Count-1;i>=0;i--)if(natureMotions[i].being==b){var m=natureMotions[i];if(m.mark!=null)m.mark.root.gameObject.SetActive(false);b.art.localScale=m.scale;b.art.localPosition=Vector3.zero;b.root.position=new Vector3(b.root.position.x,0,b.root.position.z);natureMotions.RemoveAt(i);}
        b.editBusy=false;if(b.groundRange!=null)b.groundRange.enabled=true;
    }
    bool UndoNature(){
        if(!NatureAvailable||natureHistory.Count==0)return false;
        var old=natureHistory[natureHistory.Count-1];var b=old.being;
        if(b.root==null||b.health<=0){natureHistory.RemoveAt(natureHistory.Count-1);NatureSay("Later damage superseded this invitation. Skipped this undo entry.","这次安排已被之后的破坏改变，已跳过该条撤销记录。");return false;}
        NatureCancelMotion(b);
        NatureAction action=old.created||!old.active?(b.animal?NatureAction.Leave:NatureAction.Sleep):NatureActive(b)?(b.animal?NatureAction.Range:NatureAction.Move):(b.animal?NatureAction.Recall:NatureAction.Restore);
        // A just-invited animal is still beyond the perimeter. Reverse its entrance,
        // rather than asking the normal in-forest navigator to teleport it back.
        bool beyond=b.animal&&!AnimalTerrainClear(b.root.position);
        if((old.created||!old.active)&&beyond){
            var gate=b.root.position;Vector3 outward;
            if(Mathf.Abs(gate.x)>ForestHalfWidth-2){outward=new Vector3(Mathf.Sign(gate.x),0,0);gate.x=Mathf.Sign(gate.x)*(ForestHalfWidth-1.5f);}
            else{outward=new Vector3(0,0,Mathf.Sign(gate.z));gate.z=Mathf.Sign(gate.z)*(ForestHalfDepth-1.5f);}
            NaturePrepare(b);natureMotions.Add(new NatureMotion{being=b,action=NatureAction.Leave,phase=2,gate=gate,outside=gate+outward*4,to=old.home,scale=b.art.localScale,destroyAfter=old.created});
        }else if(!NatureRequest(action,b,null,0,old.home,false))return false;
        else if(old.created)natureMotions[natureMotions.Count-1].destroyAfter=true;
        b.editResident=old.resident;natureHistory.RemoveAt(natureHistory.Count-1);NatureSay("Last invitation undone.","已撤销上一次安排。");return true;
    }
    void UpdateNatureMotions(float dt){
        for(int i=natureMotions.Count-1;i>=0;i--){
            var m=natureMotions[i];m.age+=dt;
            bool done=m.being.animal?TickNatureAnimal(m,dt):TickNaturePlant(m);
            if(!done)continue;
            var b=m.being;b.editBusy=false;
            if(m.mark!=null)m.mark.root.gameObject.SetActive(false);
            if(m.destroyAfter)NatureForget(b);
            else if(m.action==NatureAction.Sleep||m.action==NatureAction.Leave)NatureArchive(b);
            else{
                NatureSetHome(b,m.to);b.root.position=m.to;b.art.localScale=m.scale;b.art.localPosition=Vector3.zero;
                if(b.tree)ClearReplantedFooting(b);
                if(b.groundRange!=null)b.groundRange.enabled=true;
                if(b.animal){b.editResident=true;b.migrationState=0;b.opacity=1;b.renderer.color=Color.white;b.protectedUntil=Mathf.Max(b.protectedUntil,clock+8);b.navRefresh=clock+1;b.route?.Clear();animalMoments.Remove(b);}
            }
            natureMotions.RemoveAt(i);natureUIStamp="";
        }
    }
    bool TickNaturePlant(NatureMotion m){
        var b=m.being;float u=Mathf.Clamp01(m.age/1.05f),s=Mathf.SmoothStep(0,1,u);
        if(m.action==NatureAction.Move){b.root.position=Vector3.Lerp(m.from,m.to,s)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.35f);b.art.localScale=m.scale*(1-.09f*Mathf.Sin(u*Mathf.PI));}
        else if(m.action==NatureAction.Sleep){b.art.localScale=m.scale*(1-s);}
        else{float unfold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,u));b.art.localScale=m.scale*unfold;}
        b.art.rotation=billboard*Quaternion.Euler(0,0,Mathf.Sin(u*Mathf.PI*2)*1.5f);
        b.renderer.sortingOrder=DepthOrder(b.root.position.z);
        if(b.leafImages!=null)for(int j=0;j<b.leafImages.Length;j++)b.leafImages[j].sortingOrder=b.renderer.sortingOrder+j+1;
        if(m.mark!=null){
            var mark=m.mark;bool arrival=m.action==NatureAction.Plant||m.action==NatureAction.Restore;
            mark.root.position=arrival?Vector3.Lerp(m.to+new Vector3(-5,1.6f,1),m.to,Mathf.SmoothStep(0,1,u/.42f)):b.root.position;
            mark.seed.enabled=m.action!=NatureAction.Move;mark.seed.color=new Color(.28f,.20f,.10f,arrival?1-Mathf.SmoothStep(0,1,(u-.4f)/.4f):Mathf.Sin(u*Mathf.PI));
            mark.seed.sortingOrder=b.renderer.sortingOrder+9;
            mark.wind.enabled=arrival;mark.wind.color=new Color(1,1,1,.16f*(1-u));mark.wind.transform.localPosition=new Vector3(-.55f,.1f,0);mark.wind.sortingOrder=b.renderer.sortingOrder+8;
        }
        return u>=1;
    }
    bool TickNatureAnimal(NatureMotion m,float dt){
        var b=m.being;bool leaving=m.action==NatureAction.Leave;
        b.art.localScale=b.initialScale*b.birth; m.scale=b.art.localScale;
        Vector3 previous=b.root.position;float speed=2.5f;
        if(m.phase==0){
            if(m.age<.4f)return false;
            var next=Vector3.MoveTowards(previous,m.gate,dt*speed);if(NatureBoundaryClear(next,b))b.root.position=next;
            float progress=1-Vector3.Distance(b.root.position,m.gate)/4f;
            // Ink emerges only at the outer boundary; no spawn inside the selected area.
            b.renderer.color=new Color(1,1,1,Mathf.SmoothStep(0,1,progress));
            if(Vector3.Distance(b.root.position,m.gate)<.04f){m.phase=1;b.renderer.color=Color.white;NatureRoute(b,m.to);}
        }else if(m.phase==2){
            var next=Vector3.MoveTowards(previous,m.outside,dt*speed);if(NatureBoundaryClear(next,b))b.root.position=next;
            float progress=Vector3.Distance(b.root.position,m.gate)/4f;b.renderer.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,progress));
            if(Vector3.Distance(b.root.position,m.outside)<.04f)return true;
        }else{
            Vector3 destination=leaving?m.gate:m.to;
            if(Vector3.Distance(b.root.position,destination)<.16f){if(leaving)m.phase=2;else return true;}
            else{
                if(!HasAnimalRoute(b)&&clock>=m.retry){NatureRoute(b,destination);m.retry=clock+1;}
                if(HasAnimalRoute(b)){
                    var waypoint=b.route[b.routeIndex];var next=Vector3.MoveTowards(previous,waypoint,dt*speed);
                    if(CircleStepClear(next,b)){b.root.position=next;m.blocked=0;if(Vector3.Distance(next,waypoint)<.10f)b.routeIndex++;}
                    else{m.blocked+=dt;if(m.blocked>.6f&&clock>=m.retry){NatureRoute(b,destination);m.retry=clock+.8f;}}
                }
            }
            b.renderer.color=Color.white;
        }
        Vector3 delta=b.root.position-previous;if(Mathf.Abs(delta.x)>.0001f&&b.speciesIndex!=4)b.renderer.flipX=delta.x<0;
        b.art.rotation=billboard;b.art.localPosition=Vector3.up*(delta.sqrMagnitude>.000001f?.025f*Mathf.Abs(Mathf.Sin(m.age*9)):0);
        b.renderer.sortingOrder=DepthOrder(b.root.position.z);
        if(b.shadow!=null){var image=b.shadow.GetComponent<SpriteRenderer>();var tint=image.color;tint.a=.15f*b.renderer.color.a;image.color=tint;}
        return false;
    }
}}
