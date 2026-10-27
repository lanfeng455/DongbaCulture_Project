using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DongbaPrototype {
public partial class ObliqueForest {
    [Serializable] class GlyphRecord { public string key,label,source,sha256; public int x,y,width,height; }
    [Serializable] class LayoutLayer { public string key; public float x,y,height; public bool tree; }
    [Serializable] class LayoutCluster { public string name; public LayoutLayer[] layers,marks; }
    [Serializable] class GlyphCatalog { public GlyphRecord[] glyphs; public LayoutCluster[] clusters; }
    GlyphCatalog glyphCatalog;
    readonly Dictionary<string,Sprite> dictionarySprites=new Dictionary<string,Sprite>();
    readonly Dictionary<string,string> dictionaryLabels=new Dictionary<string,string>();
    readonly List<int> loadedClusterIndices=new List<int>();
    const int AnimalKinds=12;

    [Serializable] class PieceRecord { public string key; public float x,y; }
    [Serializable] class PieceCatalog { public PieceRecord[] pieces; }
    PieceCatalog pieceCatalog;
    TreeGlyphSet TreeLayers(string key,Sprite source){
        if(treeGlyphSets.TryGetValue(key,out var cached))return cached;
        var set=new TreeGlyphSet();set.trunk=Resources.Load<Sprite>("EcologyTreeV2/"+key+"_trunk");if(set.trunk==null)throw new InvalidOperationException("Missing derived source tree trunk layer: "+key);
        for(int i=0;i<6;i++){set.leaves[i]=Resources.Load<Sprite>("EcologyTreeV2/"+key+"_leaf_"+i);set.greenLeaves[i]=set.leaves[i];if(set.leaves[i]==null||set.greenLeaves[i]==null)throw new InvalidOperationException("Missing derived source foliage layer: "+key+" / "+i);}
        if(pieceCatalog==null)pieceCatalog=JsonUtility.FromJson<PieceCatalog>(Resources.Load<TextAsset>("EcologyTreeV2/pieces").text);
        for(int i=0;i<6;i++){set.pieces[i]=Resources.Load<Sprite>("EcologyTreeV2/"+key+"_leaf_"+i+"_piece");foreach(var p in pieceCatalog.pieces)if(p.key==key+"_leaf_"+i)set.pieceOffsets[i]=new Vector3(p.x,p.y,0);}
        treeGlyphSets.Add(key,set);return set;
    }

    void LoadDictionaryArt(){
        var json=Resources.Load<TextAsset>("DictionaryArt/catalog");
        if(json==null)throw new InvalidOperationException("Missing dictionary-art catalog");
        glyphCatalog=JsonUtility.FromJson<GlyphCatalog>(json.text);
        foreach(var entry in glyphCatalog.glyphs){
            var texture=Resources.Load<Texture2D>("DictionaryArt/"+entry.key);
            if(texture==null)throw new InvalidOperationException("Missing glyph "+entry.key);
            var sprite=Sprite.Create(texture,new Rect(entry.x,entry.y,entry.width,entry.height),new Vector2(.5f,0),100);
            sprite.name=entry.label;dictionarySprites.Add(entry.key,sprite);dictionaryLabels.Add(entry.key,entry.label);
        }
    }

    Being AddDictionaryPlant(Vector3 position,bool tree,int index,string key=null,float height=-1){
        int clusterIndex=Mathf.Abs(index)%glyphCatalog.clusters.Length;
        if(key==null){
            if(tree){foreach(var layer in glyphCatalog.clusters[clusterIndex].layers)if(layer.tree){key=layer.key;break;}}
            else key="plant_"+(3+Mathf.Abs(index)%6).ToString("00");
        }
        if(height<0)height=tree?3.7f+(Mathf.Abs(index*7)%5)*.22f:.55f+(index%3)*.08f;
        var b=new Being{home=position,phase=index*.19f,tree=tree,clusterIndex=clusterIndex,glyphKey=key};
        b.root=new GameObject((tree?"Dictionary tree / ":"Dictionary plant / ")+dictionaryLabels[key]).transform;b.root.position=position;
        b.art=Billboard("Glyph",b.root,dictionarySprites[key],height);
        b.renderer=b.art.GetComponent<SpriteRenderer>();b.renderer.color=Color.white;b.renderer.flipX=false;
        b.shadow=Billboard("Shadow",b.root,shadow,tree?1.1f:.3f,true);b.initialShadowScale=b.shadow.localScale;
        b.initialScale=b.art.localScale;b.fullGlyph=b.renderer.sprite;
        if(tree){var set=TreeLayers(key,b.fullGlyph);b.renderer.sprite=set.trunk;b.leafImages=new SpriteRenderer[set.leaves.Length];b.greenLeafImages=new SpriteRenderer[set.leaves.Length];b.leafAmounts=new float[set.leaves.Length];for(int i=0;i<set.leaves.Length;i++){var leaf=new GameObject("Original tree foliage stroke / " + i).transform;leaf.SetParent(b.art,false);leaf.localPosition=Vector3.zero;leaf.localRotation=Quaternion.identity;leaf.localScale=Vector3.one;var image=leaf.gameObject.AddComponent<SpriteRenderer>();image.sprite=set.leaves[i];image.sharedMaterial=rootGrowthInk;image.sortingOrder=b.renderer.sortingOrder;image.color=Color.white;b.leafImages[i]=image;var green=new GameObject("Toned foliage overlay / " + i).transform;green.SetParent(b.art,false);green.localPosition=Vector3.zero;green.localRotation=Quaternion.identity;green.localScale=Vector3.one;var greenImage=green.gameObject.AddComponent<SpriteRenderer>();greenImage.sprite=set.greenLeaves[i];greenImage.sortingOrder=b.renderer.sortingOrder+1;greenImage.enabled=false;b.greenLeafImages[i]=greenImage;b.leafAmounts[i]=1;}var r=b.fullGlyph.rect;b.stumpGlyph=Sprite.Create(b.fullGlyph.texture,new Rect(r.x+r.width*.445f,r.y,r.width*.11f,r.height*.075f),new Vector2(.5f,0),100);b.stumpScale=.42f/b.stumpGlyph.bounds.size.y;}
        if(tree)AddTreeGroundRange(b);
        plants.Add(b);return b;
    }

    void AddDictionaryCluster(Vector3 center,int index){
        var layout=glyphCatalog.clusters[index%glyphCatalog.clusters.Length];LayoutLayer trunk=null;
        foreach(var layer in layout.layers)if(layer.tree){trunk=layer;break;}
        float height=2.12f+((index*7)%5)*.065f;
        // Keep the approved smaller glyphs readable while reducing only the tall plant.
        float unit=(2.8f+(index%3)*.12f)/trunk.height;
        var placement=new System.Random(173+index*97);
        float spreadX=.84f+(float)placement.NextDouble()*.28f;
        float spreadZ=.82f+(float)placement.NextDouble()*.30f;
        AddDictionaryPlant(center,true,index,trunk.key,height);
        loadedClusterIndices.Add(index%glyphCatalog.clusters.Length);
        foreach(var layer in layout.layers){
            if(layer.tree)continue;
            // Project the approved screen layout onto the ground. Every glyph stays camera-facing.
            var p=center+new Vector3((layer.x-342)*unit*spreadX+((float)placement.NextDouble()-.5f)*.24f,0,(390-layer.y)*unit/cam.transform.up.z*spreadZ+((float)placement.NextDouble()-.5f)*.28f);
            AddDictionaryPlant(p,false,index,layer.key,layer.height*unit*(.92f+(float)placement.NextDouble()*.16f));
        }
        foreach(var mark in layout.marks){
            if(mark.key=="mark_00")continue;
            var p=center+new Vector3((mark.x-342)*unit,0,(390-mark.y)*unit/cam.transform.up.z);
            var anchor=new GameObject("Ground decoration (not a Dongba character)").transform;anchor.position=p;
            var art=Billboard("Ground strokes",anchor,dictionarySprites[mark.key],Mathf.Max(.015f,mark.height*unit));
            art.GetComponent<SpriteRenderer>().sortingOrder=98-Mathf.RoundToInt(p.z*10);
        }
        // Each large tree now owns a centered ground range instead of an offset cluster wash.
    }

    void AddDictionaryAnimal(Vector3 p,int species=-1){
        if(species<0)species=animals.Count%AnimalKinds;
        string key="animal_"+species.ToString("00");
        var b=new Being{home=p,animal=true,phase=animals.Count*1.7f,speciesIndex=species,populationRegion=PopulationRegion(p.x)};
        b.root=new GameObject("Peaceful animal / "+dictionaryLabels[key]).transform;b.root.position=p;
        // Selectively boost fine, low-ink adult silhouettes while retaining the base size hierarchy.
        // Values scale uniformly so every source glyph keeps its original proportions.
        var sprite=dictionarySprites[key];float[] kindWidths={.98f,.96f,1.12f,.98f,1.00f,1.03f,1.18f,.98f,1.32f,1.18f,1.32f,.92f};
        float[] readabilityScale={1.15f,1.15f,1.00f,1.18f,1.00f,1.15f,1.00f,1.18f,1.00f,1.15f,1.00f,1.18f};
        float individualVariation=.96f+((species*7+animals.Count*13)%5)*.02f;float width=kindWidths[species]*readabilityScale[species]*individualVariation;
        float height=Mathf.Max(.44f,width*sprite.bounds.size.y/sprite.bounds.size.x);
        b.art=Billboard("Animal glyph",b.root,sprite,height);b.renderer=b.art.GetComponent<SpriteRenderer>();
        Billboard("Shadow",b.root,shadow,.55f,true);b.initialScale=b.art.localScale;animals.Add(b);
    }

    bool ClearAnimalStep(Vector3 p,Being moving=null){
        if(placingForestAnimals&&Vector3.Distance(p,ForestSpawn)<2.6f)return false;
        if(River(p)||Mathf.Abs(p.x)>ForestHalfWidth-1||Mathf.Abs(p.z)>ForestHalfDepth-.8f)return false;
        foreach(var plant in plants)if(Vector3.Distance(p,plant.home)<(plant.tree?1.0f:.42f))return false;
        foreach(var animal in animals)if(animal!=moving&&animal.health>0&&animal.migrationState!=2&&Vector3.Distance(p,animal.root.position)<.66f)return false;
        if(!AnimalInkClear(p,moving))return false;
        return true;
    }

    IEnumerator CaptureDictionaryArt(){
        yield return new WaitForSeconds(2);
        Directory.CreateDirectory("Verification");
        var report=new System.Text.StringBuilder();
        report.AppendLine("Dictionary clusters="+loadedClusterIndices.Count+"; trees="+plants.FindAll(b=>b.tree).Count+"; small plants="+plants.FindAll(b=>!b.tree).Count+"; animals="+animals.Count);
        report.AppendLine("All 12 layouts="+(new HashSet<int>(loadedClusterIndices).Count==12));
        var species=new HashSet<int>();foreach(var animal in animals)species.Add(animal.speciesIndex);
        report.AppendLine("All 12 animal kinds="+(species.Count==12));
        bool unflipped=true;foreach(var plant in plants)unflipped&=!plant.renderer.flipX;
        report.AppendLine("Plants never mirrored="+unflipped);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/dictionary-overview.png"));
        CaptureSceneToTexture("Verification/dictionary-camera-overview.png");
        yield return new WaitForSeconds(1); // Exclude the synchronous verification capture from FPS sampling.
        float elapsed=0;int frames=0;while(elapsed<3){yield return null;elapsed+=Time.unscaledDeltaTime;frames++;}
        report.AppendLine("Measured FPS="+(frames/elapsed).ToString("F1"));
        var adult=animals[0];int before=animals.Count;Feed(adult);
        bool born=animals.Count==before+1;var baby=animals[animals.Count-1];
        report.AppendLine("New young inherits parent glyph="+(born&&baby.speciesIndex==adult.speciesIndex&&baby.renderer.sprite==adult.renderer.sprite));
        baby.careReady=0;Feed(baby);baby.careReady=0;Feed(baby);
        report.AppendLine("Young grows to adult="+(baby.age==2));
        File.WriteAllText("Verification/dictionary-art-checks.txt",report.ToString());
        yield return Capture();
    }

    // Explicit camera render for verification when a background window has no presentable backbuffer.
    // Runs only in the command-line capture routine, never in normal gameplay or Update.
    void CaptureSceneToTexture(string path){CaptureSceneToTexture(path,1600,900);}
    void CaptureSceneToTexture(string path,int width,int height){
        var previousTarget=cam.targetTexture;var previousActive=RenderTexture.active;
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try {
            cam.targetTexture=target;cam.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            File.WriteAllBytes(Path.GetFullPath(path),image.EncodeToPNG());
        } finally {cam.targetTexture=previousTarget;RenderTexture.active=previousActive;target.Release();Destroy(target);Destroy(image);}
    }
}
}

