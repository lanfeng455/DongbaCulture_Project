using UnityEngine;
using System.Collections.Generic;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Only original texture pixels are used. The mesh seam partitions the trunk;
    // shader bending moves those pixels without stretching the roots or rewriting glyphs.
    sealed class StormBinding {
        public MaterialPropertyBlock[] blocks=new MaterialPropertyBlock[7];
        public bool applied;
        public float fruitShakeAt=-100;
    }
    readonly Dictionary<Being,StormBinding> stormBindings=new Dictionary<Being,StormBinding>();
    readonly Dictionary<BrokenCrown,MaterialPropertyBlock[]> crownBlocks=new Dictionary<BrokenCrown,MaterialPropertyBlock[]>();
    Material stormBendInk;
    Being stormLoadingTree;
    float stormLoadAge;
    bool stormLoadReleasing;
    const float StormLoadSeconds=2.1f;
    float stormEligibleAge;
    float furthestStormBreak,largestFruitShake,maximumFractureAnchorError;
    int stormLoadsStarted,fruitShakesObserved;

    void SetupStormBending(){
        stormBendInk=new Material(Resources.Load<Shader>("StormBendInk"));
        foreach(var entry in treeGlyphSets){
            var source=entry.Value.trunk;var r=source.rect;
            var art=new BreakArt{joint=(r.height*.30f-source.pivot.y)/source.pixelsPerUnit,height=(r.height-source.pivot.y)/source.pixelsPerUnit,seam=new float[33],flexibleLeaves=new Sprite[6]};
            for(int x=0;x<art.seam.Length;x++){
                float u=(float)x/(art.seam.Length-1);
                // An oblique, splintered seam, never a straight horizontal chop.
                art.seam[x]=r.height*(.30f+(u-.5f)*.10f+((x%3)==0?.028f:(x%3)==1?-.024f:.006f));
            }
            art.lower=WeatherMesh(source,art.seam,-1);
            art.upper=WeatherMesh(source,art.seam,1);
            art.flexibleTrunk=WeatherMesh(source,null,0);
            for(int i=0;i<6;i++)art.flexibleLeaves[i]=WeatherMesh(entry.Value.leaves[i],null,0);
            breakArt.Add(entry.Key,art);
        }
        foreach(var tree in plants)if(tree.tree){
            var binding=new StormBinding();for(int i=0;i<7;i++)binding.blocks[i]=new MaterialPropertyBlock();
            stormBindings.Add(tree,binding);
        }
    }

    Sprite WeatherMesh(Sprite source,float[] seam,int side){
        const int columns=33,rows=25;
        var r=source.rect;var pivot=source.pivot;float ppu=source.pixelsPerUnit;
        var vertices=new Vector2[columns*rows];var indices=new ushort[(columns-1)*(rows-1)*6];
        for(int x=0;x<columns;x++)for(int y=0;y<rows;y++){
            float low=side>0?seam[x]:0,high=side<0?seam[x]:r.height;
            float px=Mathf.Clamp(r.width*x/(columns-1),.01f,r.width-.01f),py=Mathf.Clamp(Mathf.Lerp(low,high,(float)y/(rows-1)),.01f,r.height-.01f);
            // OverrideGeometry accepts pixel rect coordinates; Unity applies the pivot and PPU.
            vertices[x*rows+y]=new Vector2(px,py);
        }
        int t=0;
        for(int x=0;x<columns-1;x++)for(int y=0;y<rows-1;y++){
            int a=x*rows+y,b=a+rows;indices[t++]=(ushort)a;indices[t++]=(ushort)b;indices[t++]=(ushort)(a+1);
            indices[t++]=(ushort)(a+1);indices[t++]=(ushort)b;indices[t++]=(ushort)(b+1);
        }
        var sprite=Sprite.Create(source.texture,r,new Vector2(pivot.x/r.width,pivot.y/r.height),ppu,0,SpriteMeshType.FullRect);
        sprite.name=source.name+" / wind mesh "+side;sprite.OverrideGeometry(vertices,indices);return sprite;
    }

    MaterialPropertyBlock GetCrownProperties(BrokenCrown crown,int index){return crownBlocks[crown][index];}
    float StormPeakBend(BreakArt art){return art.height*.24f;}
    Vector3 StormJoint(BreakArt art){float h=art.joint/art.height;return new Vector3(StormPeakBend(art)*h*h,art.joint,0);}
    bool SameWeatherBank(Vector3 position){return (position.x-RiverX(position.z))*(Feet.x-RiverX(Feet.z))>0;}

    void SetStormInk(SpriteRenderer image,MaterialPropertyBlock block,BreakArt art,float bend,float scale,float offset,float growth=-1){
        image.sharedMaterial=stormBendInk;
        // Explicit per-renderer texture also avoids the old full-canvas foliage batching bug.
        block.SetTexture("_MainTex",image.sprite.texture);block.SetFloat("_Growth",growth<0?greenHeight:growth);
        block.SetColor("_Green",rootGrowthInk.GetColor("_Green"));block.SetFloat("_Bend",bend);
        block.SetFloat("_Height",art.height);block.SetFloat("_PartScale",scale);block.SetFloat("_PartOffset",offset);
        image.SetPropertyBlock(block);
    }

    void RestoreStormTree(Being tree,StormBinding binding){
        if(!binding.applied)return;
        tree.renderer.SetPropertyBlock(null);
        tree.renderer.sharedMaterial=tree.health<=0?plainTreeInk:rootGrowthInk;
        var art=treeGlyphSets[tree.glyphKey];
        for(int i=0;i<tree.leafImages.Length;i++){tree.leafImages[i].sprite=art.leaves[i];tree.leafImages[i].sharedMaterial=rootGrowthInk;tree.leafImages[i].SetPropertyBlock(null);}
        binding.applied=false;
    }

    void ResetStormBending(){
        stormLoadingTree=null;stormLoadAge=0;stormLoadReleasing=false;
        furthestStormBreak=largestFruitShake=maximumFractureAnchorError=0;stormEligibleAge=0;stormLoadsStarted=fruitShakesObserved=0;
        foreach(var entry in stormBindings){RestoreStormTree(entry.Key,entry.Value);entry.Value.fruitShakeAt=-100;}
        // Crown bindings are allocated once, after the fixed pool is constructed.
        foreach(var crown in brokenCrowns)if(!crownBlocks.ContainsKey(crown)){
            var blocks=new MaterialPropertyBlock[7];for(int i=0;i<7;i++)blocks[i]=new MaterialPropertyBlock();crownBlocks.Add(crown,blocks);
        }
    }

    void CancelStormLoading(bool immediate){
        if(stormLoadingTree==null)return;
        if(!immediate){stormLoadReleasing=true;return;}
        RestoreStormTree(stormLoadingTree,stormBindings[stormLoadingTree]);
        stormLoadingTree=null;stormLoadAge=0;stormLoadReleasing=false;
    }

    void UpdateStormLoading(float dt,int quota){
        bool allowed=weatherStorm>.53f&&Balance<=-5&&!ManualStoryDialogueActive&&!fatherIntroActive&&!OpeningSurveyActive;
        stormEligibleAge=allowed?stormEligibleAge+dt:0;
        if(stormLoadingTree!=null){
            var tree=stormLoadingTree;
            if(!allowed||tree.health<=0||tree.cultivated||Vector3.Distance(Feet,tree.home)>StormBreakRadius||!SameWeatherBank(tree.home))CancelStormLoading(false);
            if(stormLoadReleasing){
                stormLoadAge=Mathf.Max(0,stormLoadAge-dt*3);
                if(stormLoadAge<=0||tree.health<=0)CancelStormLoading(true);
                return;
            }
            if(stormLoadAge>=StormLoadSeconds){
                if(BreakTreeInStorm(tree)){
                    stormBrokenCount++;furthestStormBreak=Mathf.Max(furthestStormBreak,Vector3.Distance(Feet,tree.home));
                    breakAt=clock+StormBreakInterval*Mathf.Lerp(.9f,1.2f,FlowerScatter(stormBrokenCount,71));
                }
                stormLoadingTree=null;stormLoadAge=0;return;
            }
            stormLoadAge=Mathf.Min(StormLoadSeconds,stormLoadAge+dt);return;
        }
        if(!allowed||stormEligibleAge<5f||clock<breakAt||stormBrokenCount>=quota)return;
        // A sustained storm must build before an occasional nearby fracture.
        // Entering a new tree radius never resets this global cooldown.
        var next=SelectWeatherTree(true,stormBrokenCount);
        if(next==null){breakAt=clock+.5f;return;}
        stormLoadingTree=next;stormLoadAge=0;stormLoadReleasing=false;stormLoadsStarted++;
    }

    void ApplyLivingTreePresentation(){
        foreach(var entry in stormBindings){
            var tree=entry.Key;if(tree.editBusy||!tree.root.gameObject.activeSelf)continue;var binding=entry.Value;var art=breakArt[tree.glyphKey];
            bool loading=tree==stormLoadingTree&&tree.health>0;
            if(!loading&&!tree.windBroken){RestoreStormTree(tree,binding);continue;}
            binding.applied=true;
            float load=loading?Mathf.SmoothStep(0,1,stormLoadAge/StormLoadSeconds):1;
            if(loading){
                tree.renderer.sprite=art.flexibleTrunk;
                tree.art.rotation=Quaternion.Slerp(tree.art.rotation,billboard,Mathf.Clamp01(stormLoadAge/.30f));
            }
            SetStormInk(tree.renderer,binding.blocks[0],art,StormPeakBend(art)*load,1,0,loading?greenHeight:0);
            if(loading)for(int i=0;i<6;i++){
                var leaf=tree.leafImages[i];leaf.sprite=art.flexibleLeaves[i];
                SetStormInk(leaf,binding.blocks[i+1],art,StormPeakBend(art)*load,leaf.transform.localScale.x,leaf.transform.localPosition.y);
            }
        }
    }

    void ReleaseStormMeshes(){
        foreach(var art in breakArt.Values){
            Destroy(art.lower);Destroy(art.upper);Destroy(art.flexibleTrunk);
            foreach(var leaf in art.flexibleLeaves)Destroy(leaf);
        }
        if(stormBendInk!=null)Destroy(stormBendInk);
    }

    float FruitReleaseShake(Being tree){
        if(!tree.tree||tree.health<=0||!stormBindings.TryGetValue(tree,out var binding))return 0;
        float age=clock-binding.fruitShakeAt;
        if(age<0||age>1.05f)return 0;
        float envelope=Mathf.SmoothStep(0,1,age/.14f)*(1-Mathf.SmoothStep(0,1,(age-.20f)/.85f));
        float shake=1.65f*envelope*Mathf.Sin(age*Mathf.PI*2*3.1f);
        largestFruitShake=Mathf.Max(largestFruitShake,Mathf.Abs(shake));fruitShakesObserved++;
        return shake;
    }
}}
