using UnityEngine;
using System.Collections.Generic;

namespace DongbaPrototype { public partial class ObliqueForest {
    [Header("Living forest weather")]
    [Range(1.3f,2.2f)] public float FlourishingWindPeriod=1.65f;
    [Range(.08f,.35f)] public float StormGlyphOpacity=.22f;
    [Range(1.5f,6f)] public float FruitDropInterval=2.8f;
    [Range(1,8)] public int StormBreakLimit=5;
    [Range(.5f,5f)] public float StormTreeLean=3.5f;

    sealed class FruitMark { public Transform root;public SpriteRenderer image;public Vector3 origin,landing;public float age,size; }
    sealed class WindMark { public Transform root;public SpriteRenderer image;public Vector3 from,to;public float age,duration; }
    sealed class BreakArt { public Sprite lower,upper;public float joint; }
    sealed class BrokenCrown { public Transform root;public SpriteRenderer trunk;public SpriteRenderer[] leaves;public Vector3 origin;public Quaternion rotation;public float age,fallSign; }
    readonly List<FruitMark> fruitMarks=new List<FruitMark>();
    readonly List<WindMark> stormMarks=new List<WindMark>();
    readonly List<BrokenCrown> brokenCrowns=new List<BrokenCrown>();
    readonly Dictionary<string,BreakArt> breakArt=new Dictionary<string,BreakArt>();
    Sprite fruitMarkSprite,stormMarkSprite;
    float ecologyWindPhase,weatherLife,weatherStorm,weatherGust,fruitAt,windAt,breakAt;
    int fruitSequence,windSequence,stormBrokenCount,weatherFruitCount,weatherWindCount;
    bool WeatherAllowed=>!InScrollWorld&&eastPaperProgress<=0&&(!StoryModeEnabled||journeyStage==JourneyStage.Hunting||journeyStage==JourneyStage.Boar||IsRestorationEnding);
    float LivingWindPeriod=>Mathf.Lerp(2.2f,FlourishingWindPeriod,weatherLife);

    void SetupLivingWeather(){
        fruitMarkSprite=Resources.Load<Sprite>("ApprovedArt/forest_fruit_existing");
        stormMarkSprite=Get("dongba_wind_cc_by_sa");
        for(int i=0;i<12;i++){
            var root=new GameObject("Falling fruit / existing teammate glyph").transform;
            var image=root.gameObject.AddComponent<SpriteRenderer>();image.sprite=fruitMarkSprite;
            var upright=root.gameObject.AddComponent<XRUprightBillboard>();upright.Initialize(this,cam,billboard);
            root.gameObject.SetActive(false);fruitMarks.Add(new FruitMark{root=root,image=image});
        }
        for(int i=0;i<6;i++){
            var root=new GameObject("Translucent Dongba wind / world space").transform;
            var image=root.gameObject.AddComponent<SpriteRenderer>();image.sprite=stormMarkSprite;image.color=Color.clear;
            var upright=root.gameObject.AddComponent<XRUprightBillboard>();upright.Initialize(this,cam,billboard);
            root.gameObject.SetActive(false);stormMarks.Add(new WindMark{root=root,image=image});
        }
        foreach(var entry in treeGlyphSets){
            var sprite=entry.Value.trunk;var r=sprite.rect;int cut=Mathf.RoundToInt(r.height*.30f);
            var art=new BreakArt{joint=(cut-sprite.pivot.y)/sprite.pixelsPerUnit};
            art.lower=Sprite.Create(sprite.texture,new Rect(r.x,r.y,r.width,cut),new Vector2(sprite.pivot.x/r.width,sprite.pivot.y/cut),sprite.pixelsPerUnit);
            art.upper=Sprite.Create(sprite.texture,new Rect(r.x,r.y+cut,r.width,r.height-cut),new Vector2(sprite.pivot.x/r.width,0),sprite.pixelsPerUnit);
            breakArt.Add(entry.Key,art);
        }
        for(int i=0;i<6;i++){
            var root=new GameObject("Storm detached original crown").transform;
            var image=root.gameObject.AddComponent<SpriteRenderer>();
            var crown=new BrokenCrown{root=root,trunk=image,leaves=new SpriteRenderer[6]};
            for(int j=0;j<6;j++){var part=new GameObject("Original foliage "+j).transform;part.SetParent(root,false);crown.leaves[j]=part.gameObject.AddComponent<SpriteRenderer>();}
            root.gameObject.SetActive(false);brokenCrowns.Add(crown);
        }
        ResetLivingWeather();
    }

    void ResetLivingWeather(){
        ecologyWindPhase=weatherLife=weatherStorm=weatherGust=0;
        fruitAt=1.4f;windAt=.25f;breakAt=2;fruitSequence=windSequence=stormBrokenCount=weatherFruitCount=weatherWindCount=0;
        foreach(var mark in fruitMarks)mark.root.gameObject.SetActive(false);
        foreach(var mark in stormMarks)mark.root.gameObject.SetActive(false);
        foreach(var crown in brokenCrowns)crown.root.gameObject.SetActive(false);
    }

    void TickLivingWind(float dt){
        float follow=1-Mathf.Exp(-dt/1.4f);
        weatherLife=Mathf.Lerp(weatherLife,Mathf.Max(0,ecologyVisual),follow);
        weatherStorm=Mathf.Lerp(weatherStorm,Mathf.Max(0,-ecologyVisual),follow);
        // Integrate frequency. Multiplying absolute time by a changing frequency would jump phase.
        ecologyWindPhase+=dt*Mathf.PI*2/LivingWindPeriod;
        weatherGust=Mathf.Pow(Mathf.Max(0,Mathf.Sin(clock*Mathf.PI*2/6.3f-.65f)),2);
    }

    float LivingPlantAngle(Being plant){
        float delay=Mathf.Repeat(plant.phase,.65f);
        float wave=Mathf.Sin(ecologyWindPhase-delay);
        if(!plant.tree)wave=.78f*wave+.22f*Mathf.Sin(ecologyWindPhase*2-delay);
        float amplitude=Mathf.Lerp(1.3f,1.9f,weatherLife);
        float gustLean=weatherStorm*weatherStorm*weatherGust*StormTreeLean;
        return wave*(amplitude+weatherStorm*.65f)-gustLean+Mathf.Sin(clock*65)*plant.shake*14;
    }

    void UpdateLivingWeather(float dt){
        if(!WeatherAllowed){HideWeatherMarks();return;}
        if(weatherLife>.30f&&Balance>=FlourishThreshold&&clock>=fruitAt){
            fruitAt=clock+FruitDropInterval*Mathf.Lerp(.78f,1.24f,FlowerScatter(fruitSequence,14));
            var tree=SelectWeatherTree(false,fruitSequence++);if(tree!=null)SpawnFruit(tree);
        }
        if(weatherStorm>.12f&&Balance<0&&clock>=windAt){
            windAt=clock+Mathf.Lerp(3.5f,1.35f,weatherStorm);SpawnStormWind(windSequence++);
        }
        // Wind damage is finite and funded only by the player's existing damage counter.
        // It never increments damageCount or advances narrative thresholds on its own.
        int quota=Mathf.Min(StormBreakLimit,Mathf.Max(0,damageCount-3)/3);
        if(weatherStorm>.53f&&Balance<=-5&&weatherGust>.6f&&clock>=breakAt&&stormBrokenCount<quota&&!ManualStoryDialogueActive&&!fatherIntroActive&&!OpeningSurveyActive){
            var tree=SelectWeatherTree(true,stormBrokenCount);if(tree!=null&&BreakTreeInStorm(tree)){stormBrokenCount++;breakAt=clock+3.4f;}
        }
        foreach(var fruit in fruitMarks)UpdateFruit(fruit,dt);
        foreach(var wind in stormMarks)UpdateStormWind(wind,dt);
        foreach(var crown in brokenCrowns)UpdateBrokenCrown(crown,dt);
    }

    void HideWeatherMarks(){
        foreach(var fruit in fruitMarks)fruit.root.gameObject.SetActive(false);
        foreach(var wind in stormMarks)wind.root.gameObject.SetActive(false);
        foreach(var crown in brokenCrowns)crown.root.gameObject.SetActive(false);
    }

    Being SelectWeatherTree(bool breaking,int sequence){
        Being selected=null;float score=float.MaxValue;
        for(int i=0;i<plants.Count;i++){
            var tree=plants[i];if(!tree.tree||tree.health<=0||tree.targetScale<.95f||(breaking&&tree.cultivated)||tree.windBroken)continue;
            Vector3 view=cam.WorldToViewportPoint(tree.home+Vector3.up*1.5f);
            if(view.z<0||view.x<.08f||view.x>.92f||view.y<.15f||view.y>.85f)continue;
            if(breaking){
                var tip=cam.WorldToViewportPoint(tree.art.TransformPoint(Vector3.up*tree.fullGlyph.bounds.max.y));
                // Choose an entire visible tree, away from the chapter note, so the break can actually be read.
                if(tip.z<=0||tip.y>.87f||tip.x<.10f||tip.x>.90f||(tip.x<.34f&&tip.y>.70f))continue;
            }
            float distance=Vector3.Distance(Feet,tree.home);if(breaking&&(distance<2.2f||distance>19))continue;
            if(!breaking&&distance>24)continue;
            float candidate=FlowerScatter(i+sequence*19,31)*10+distance*.08f;
            if(candidate<score){score=candidate;selected=tree;}
        }
        return selected;
    }

    void SpawnFruit(Being tree){
        if(fruitMarkSprite==null)return;
        FruitMark mark=null;foreach(var item in fruitMarks)if(!item.root.gameObject.activeSelf){mark=item;break;}if(mark==null)return;
        var layers=treeGlyphSets[tree.glyphKey];int group=0;
        for(int i=1;i<tree.leafImages.Length;i++)if(tree.leafAttached[i]&&layers.pieceOffsets[i].y>layers.pieceOffsets[group].y)group=i;
        mark.origin=tree.art.TransformPoint(layers.pieceOffsets[group]);
        mark.landing=new Vector3(mark.origin.x+.25f,.04f,tree.home.z-.30f);
        mark.age=0;mark.size=.38f/fruitMarkSprite.bounds.size.y;
        mark.root.SetPositionAndRotation(mark.origin,billboard);mark.root.localScale=Vector3.zero;
        mark.image.color=new Color(.48f,.29f,.10f,1);mark.image.sortingOrder=DepthOrder(tree.home.z)+8;
        mark.root.gameObject.SetActive(true);weatherFruitCount++;
    }

    void UpdateFruit(FruitMark mark,float dt){
        if(!mark.root.gameObject.activeSelf)return;mark.age+=dt;
        const float duration=4.4f;if(mark.age>=duration){mark.root.gameObject.SetActive(false);return;}
        float fall=Mathf.Clamp01((mark.age-.55f)/1.15f);
        Vector3 position=Vector3.Lerp(mark.origin,mark.landing,fall*fall);
        if(mark.age>1.7f){float bounce=Mathf.Clamp01((mark.age-1.7f)/.46f);position=mark.landing+Vector3.up*(Mathf.Sin(bounce*Mathf.PI)*.12f);}
        mark.root.position=position;mark.root.localScale=Vector3.one*(mark.size*Mathf.SmoothStep(0,1,mark.age/.20f));
        mark.root.rotation=billboard*Quaternion.Euler(0,0,Mathf.Sin(fall*Mathf.PI)*12);
        var color=mark.image.color;color.a=1-Mathf.SmoothStep(0,1,(mark.age-3.4f));mark.image.color=color;
        if(fall>=1)mark.image.sortingOrder=GroundDepthOrder(mark.landing.z)+1;
    }

    void SpawnStormWind(int sequence){
        WindMark mark=null;foreach(var item in stormMarks)if(!item.root.gameObject.activeSelf){mark=item;break;}if(mark==null)return;
        // An actual horizontal pass inside the forest, never an OnGUI or eye-locked overlay.
        Vector3 centre=Feet+Vector3.up*(1.1f+FlowerScatter(sequence,4)*2.0f);
        float width=cam.orthographic?cam.orthographicSize*cam.aspect+3:19;
        float depth=(FlowerScatter(sequence,5)-.5f)*(cam.orthographic?13:24);
        centre.z=Mathf.Clamp(Feet.z+depth,-ForestHalfDepth+2,ForestHalfDepth-2);
        mark.from=centre+Vector3.left*width;mark.to=centre+Vector3.right*width;
        mark.age=0;mark.duration=Mathf.Lerp(8.4f,6.0f,weatherStorm);
        mark.root.SetPositionAndRotation(mark.from,billboard);
        mark.root.localScale=Vector3.one*((2.6f+FlowerScatter(sequence,7)*1.2f)/stormMarkSprite.bounds.size.x);
        mark.image.sortingOrder=DepthOrder(centre.z)-8;mark.image.color=new Color(1,1,1,0);
        mark.root.gameObject.SetActive(true);weatherWindCount++;
    }

    void UpdateStormWind(WindMark mark,float dt){
        if(!mark.root.gameObject.activeSelf)return;mark.age+=dt;
        float u=mark.age/mark.duration;if(u>=1||weatherStorm<.015f){mark.root.gameObject.SetActive(false);return;}
        mark.root.position=Vector3.Lerp(mark.from,mark.to,u);mark.root.rotation=billboard;
        float envelope=Mathf.SmoothStep(0,1,u/.18f)*(1-Mathf.SmoothStep(0,1,(u-.78f)/.22f));
        mark.image.color=new Color(1,1,1,StormGlyphOpacity*weatherStorm*envelope);
    }

    bool BreakTreeInStorm(Being tree){
        BrokenCrown crown=null;foreach(var item in brokenCrowns)if(!item.root.gameObject.activeSelf){crown=item;break;}if(crown==null)return false;
        var pieces=breakArt[tree.glyphKey];crown.age=0;crown.fallSign=-1;
        crown.origin=tree.art.TransformPoint(Vector3.up*pieces.joint);crown.rotation=tree.art.rotation;
        crown.root.SetPositionAndRotation(crown.origin,crown.rotation);crown.root.localScale=tree.art.lossyScale;
        crown.trunk.sprite=pieces.upper;crown.trunk.color=tree.renderer.color;crown.trunk.sortingOrder=tree.renderer.sortingOrder;
        for(int i=0;i<crown.leaves.Length;i++){
            var image=crown.leaves[i];var original=tree.leafImages[i];image.sprite=original.sprite;image.color=original.color;
            image.enabled=original.enabled;image.sortingOrder=original.sortingOrder;
            image.transform.localPosition=original.transform.localPosition-Vector3.up*pieces.joint;image.transform.localRotation=Quaternion.identity;image.transform.localScale=original.transform.localScale;
        }
        crown.root.gameObject.SetActive(true);
        tree.health=0;tree.windBroken=true;tree.fall=1;tree.shake=0;
        foreach(var image in tree.leafImages)image.enabled=false;
        PlayForestSfx("tree_fall",tree.home);
        return true;
    }

    void UpdateBrokenCrown(BrokenCrown crown,float dt){
        if(!crown.root.gameObject.activeSelf)return;crown.age+=dt;
        float u=Mathf.Clamp01(crown.age/1.65f);
        crown.root.rotation=crown.rotation*Quaternion.Euler(0,0,crown.fallSign*86*Mathf.SmoothStep(0,1,u));
        crown.root.position=crown.origin+Vector3.right*(1.15f*u*u)-Vector3.up*(.65f*u*u);
        float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,2.4f,crown.age));
        crown.trunk.color=new Color(1,1,1,alpha);foreach(var leaf in crown.leaves)leaf.color=new Color(1,1,1,alpha);
        if(crown.age>=2.4f)crown.root.gameObject.SetActive(false);
    }
}}
