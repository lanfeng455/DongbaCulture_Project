using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {
public partial class ObliqueForest {
    [Header("Forest scale and interaction")]
    [Range(.8f,1.25f)] public float ForestTreeScale=.8f;
    [Range(.8f,1.4f)] public float InteractionRange=1;
    const float ForestHalfWidth=45f, ForestHalfDepth=21f;
    static readonly Vector3 ForestSpawn=new Vector3(-1.6f,0,-1.5f);
    static readonly Vector3 CameraOffset=new Vector3(0,17,-21);
    Being interactionTarget;
    Transform selectionRing;
    Canvas forestCanvas;
    Text hudHealth,hudNotice,hudArea,hudPrompt,hudPromptTitle;
    GameObject promptPanel;
    Font forestFont;
    readonly Color uiInk=new Color(.27f,.21f,.14f);
    int displayedHealth=-1;
    bool placingForestAnimals;
    readonly int[] groveLayouts={0,5,10,3,8,1,6,11,4,9,2,7};
    void FollowPlayerCamera(float dt){
        if(player==null||cam==null)return;
        float visibleHalfWidth=cam.orthographicSize*cam.aspect;
        float visibleHalfDepth=cam.orthographicSize/Mathf.Max(.5f,Mathf.Abs(cam.transform.up.z));
        var center=player.position;
        center.x=Mathf.Clamp(center.x,-ForestHalfWidth+visibleHalfWidth,ForestHalfWidth-visibleHalfWidth);
        center.z=Mathf.Clamp(center.z,-ForestHalfDepth+visibleHalfDepth,ForestHalfDepth-visibleHalfDepth);
        var goal=center+CameraOffset;
        cam.transform.position=dt>50?goal:Vector3.Lerp(cam.transform.position,goal,1-Mathf.Exp(-dt*5.5f));
    }
    bool InClearing(Vector3 p){
        Vector2 q=new Vector2(p.x,p.z);
        Vector2[] centers={new Vector2(-1.6f,-1.5f),new Vector2(-32,0),new Vector2(-19,0),new Vector2(-8,-2),new Vector2(-8,3),new Vector2(8,-2),new Vector2(8,3),new Vector2(19,0),new Vector2(32,0)};
        foreach(var c in centers){float r=c.x==-1.6f?5.6f:Mathf.Abs(c.x)>25?4.8f:3.6f;if(Vector2.Distance(q,c)<r)return true;}return false;
    }
    void ComposeForest(){
        Patch(new Vector3(-38,0,8.8f),7,5.4f,4.3f,101);Patch(new Vector3(-27,0,10.4f),5,4.5f,3.2f,112);Patch(new Vector3(-16,0,9.3f),5,4.8f,3.7f,123);
        Patch(new Vector3(-39,0,-7.8f),7,5f,4.3f,134);Patch(new Vector3(-28,0,-10.5f),6,4.5f,3.4f,145);Patch(new Vector3(-16,0,-8.8f),5,4.7f,3.7f,156);
        Patch(new Vector3(-7,0,-12f),4,3.9f,2.6f,167);Patch(new Vector3(-7,0,12f),4,4.2f,2.6f,178);Patch(new Vector3(9,0,-11.5f),5,4.4f,3.2f,189);
        Patch(new Vector3(21,0,-10.4f),6,4.6f,3.5f,201);Patch(new Vector3(12,0,-1.2f),5,4.1f,3.4f,213);Patch(new Vector3(25,0,1.4f),6,4.6f,3.8f,225);
        Patch(new Vector3(10,0,10.6f),5,4.3f,3.3f,237);Patch(new Vector3(22,0,11.1f),6,4.8f,3.6f,249);Patch(new Vector3(37,0,8.1f),6,5f,4f,261);
        Patch(new Vector3(38,0,-8.5f),6,4.8f,4f,273);Patch(new Vector3(-4.2f,0,-5.4f),3,2.6f,3.1f,284);Patch(new Vector3(-3.1f,0,7.1f),3,2.7f,3f,295);
        Patch(new Vector3(7.1f,0,-6.8f),3,2.5f,3f,306);Patch(new Vector3(7.4f,0,7.4f),3,2.6f,3.2f,317);
        Patch(new Vector3(-39,0,17.2f),4,4.4f,3.5f,328);Patch(new Vector3(-24,0,17.6f),4,4f,3.3f,339);Patch(new Vector3(-8,0,17.2f),4,3.6f,3.2f,350);
        Patch(new Vector3(10,0,17.4f),4,3.7f,3.4f,361);Patch(new Vector3(25,0,17.5f),4,4.2f,3.5f,372);Patch(new Vector3(39,0,17f),4,4f,3.4f,383);
        Patch(new Vector3(-39,0,-17.2f),4,4.4f,3.5f,394);Patch(new Vector3(-24,0,-17.6f),4,4f,3.3f,405);Patch(new Vector3(-8,0,-17.2f),4,3.6f,3.2f,416);
        Patch(new Vector3(10,0,-17.4f),4,3.7f,3.4f,427);Patch(new Vector3(25,0,-17.5f),4,4.2f,3.5f,438);Patch(new Vector3(39,0,-17f),4,4f,3.4f,449);
    }
    void Patch(Vector3 center,int count,float rx,float rz,int seed){
        var rng=new System.Random(seed);int added=0;
        for(int attempt=0;attempt<count*18&&added<count;attempt++){
            float a=(float)rng.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)rng.NextDouble());var p=center+new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r);
            if(Mathf.Abs(p.x)>43.7f||Mathf.Abs(p.z)>ForestHalfDepth-.4f||InClearing(p)||River(p))continue;
            int index=groveLayouts[(seed+added)%groveLayouts.Length];var layout=glyphCatalog.clusters[index%glyphCatalog.clusters.Length];LayoutLayer trunk=null;
            foreach(var layer in layout.layers)if(layer.tree){trunk=layer;break;}
            float h=3.8f+(float)rng.NextDouble()*1.0f;
            if(!TreeGlyphClear(p,dictionarySprites[trunk.key],h*ForestTreeScale))continue;
            var tree=AddDictionaryPlant(p,true,index,trunk.key,h*ForestTreeScale);tree.inkOpacity=1;tree.renderer.color=Color.white;added++;
            AddUndergrowth(p,index,trunk,rng,1+(int)(rng.NextDouble()*3));if(added%3==1&&layout.marks.Length>0)AddSparseMark(p,layout.marks[(int)(rng.NextDouble()*layout.marks.Length)],rng);
        }
    }
    bool TreeGlyphClear(Vector3 p,Sprite glyph,float height){
        var size=glyph.bounds.size;var candidate=ProjectedInkRect(p,height*size.x/size.y,height);
        foreach(var b in plants)if(b.tree&&b.health>0){var bounds=b.renderer.sprite.bounds.size;var scale=b.art.localScale;var other=ProjectedInkRect(b.home,bounds.x*scale.x*.94f,bounds.y*scale.y*.97f);if(candidate.Overlaps(other)){var intersection=Rect.MinMaxRect(Mathf.Max(candidate.xMin,other.xMin),Mathf.Max(candidate.yMin,other.yMin),Mathf.Min(candidate.xMax,other.xMax),Mathf.Min(candidate.yMax,other.yMax));float smaller=Mathf.Min(candidate.width*candidate.height,other.width*other.height);if(intersection.width*intersection.height>smaller*.10f)return false;}}
        return true;
    }
    void AddUndergrowth(Vector3 center,int index,LayoutLayer trunk,System.Random rng,int count){
        var layout=glyphCatalog.clusters[index%glyphCatalog.clusters.Length];var layers=new System.Collections.Generic.List<LayoutLayer>();foreach(var layer in layout.layers)if(!layer.tree)layers.Add(layer);if(layers.Count==0)return;
        int placed=0;for(int n=0;n<layers.Count*3&&placed<count;n++){
            var layer=layers[(int)(rng.NextDouble()*layers.Count)];float a=(float)rng.NextDouble()*Mathf.PI*2,r=.65f+(float)rng.NextDouble()*1.25f;var p=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            if(River(p)||InClearing(p)||Mathf.Abs(p.x)>44||Mathf.Abs(p.z)>ForestHalfDepth-.2f)continue;
            bool close=false;foreach(var b in plants)if(Vector3.Distance(p,b.home)<(b.tree?1.05f:.56f)){close=true;break;}if(close)continue;
            float size=.58f+(float)rng.NextDouble()*.32f;var low=AddDictionaryPlant(p,false,index,layer.key,size);low.inkOpacity=1;low.renderer.color=Color.white;placed++;
        }
    }
    void AddSparseMark(Vector3 center,LayoutLayer mark,System.Random rng){
        float a=(float)rng.NextDouble()*Mathf.PI*2,r=.8f+(float)rng.NextDouble()*1.4f;var p=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);if(River(p)||InClearing(p))return;
        float markHeight=.16f+(float)rng.NextDouble()*.12f;if(mark.key=="mark_00")return; // Preserve placement RNG, omit oversized horizontal decoration.
        var root=new GameObject("Dictionary ground strokes / source mark").transform;root.position=p;var art=Billboard("Small ground strokes",root,dictionarySprites[mark.key],markHeight);
        var image=art.GetComponent<SpriteRenderer>();image.color=new Color(1,1,1,.72f);image.sortingOrder=98-Mathf.RoundToInt(p.z*10);
    }
    void PlaceForestAnimals(){
        Vector3[] spots={new Vector3(-32,0,-.2f),new Vector3(-33.8f,0,.9f),new Vector3(-30.6f,0,1.2f),new Vector3(-19,0,-1.1f),new Vector3(-20.6f,0,.3f),new Vector3(-17.2f,0,.6f),new Vector3(-8,0,-3f),new Vector3(-9.3f,0,-1.7f),new Vector3(-7,0,4.2f),new Vector3(-9,0,4.7f),new Vector3(8,0,-3.6f),new Vector3(9.5f,0,-2.6f),new Vector3(7,0,-1.4f),new Vector3(8,0,4f),new Vector3(9.4f,0,3.1f),new Vector3(6.5f,0,2.6f),new Vector3(19,0,-.6f),new Vector3(20.6f,0,.3f),new Vector3(17.4f,0,.8f),new Vector3(32,0,-.8f),new Vector3(33.5f,0,.4f),new Vector3(-38,0,0),new Vector3(-37,0,-1.8f),new Vector3(38,0,.2f),new Vector3(37,0,-1.7f)};
        placingForestAnimals=true;for(int i=0;i<spots.Length;i++){var p=spots[i];if(!ClearAnimalStep(p)&&!FindLocal(p,.35f,1.9f,i,true,out p))continue;AddAnimal(p,i%12);}placingForestAnimals=false;
    }
    Rect ProjectedInkRect(Vector3 foot,float width,float height){var p=cam.WorldToScreenPoint(foot);float pixels=cam.pixelHeight/(cam.orthographicSize*2);return new Rect(p.x-width*pixels*.5f,p.y,width*pixels,height*pixels);}
    bool AnimalInkClear(Vector3 p,Being moving=null){
        float w=moving==null?1.05f:moving.renderer.sprite.bounds.size.x*moving.initialScale.x*moving.birth;float h=moving==null?1f:moving.renderer.sprite.bounds.size.y*moving.initialScale.y*moving.birth;var ink=ProjectedInkRect(p,w,h);
        foreach(var plant in plants)if(plant.health>0){var size=plant.fullGlyph.bounds.size;var s=plant.art.localScale;if(ink.Overlaps(ProjectedInkRect(plant.home,size.x*s.x*.92f,size.y*s.y*.95f)))return false;}
        foreach(var other in animals)if(other!=moving&&other.health>0&&other.migrationState!=2){var size=other.renderer.sprite.bounds.size;if(ink.Overlaps(ProjectedInkRect(other.root.position,size.x*other.art.localScale.x,size.y*other.art.localScale.y)))return false;}return true;
    }
    RectTransform UIBlock(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size){var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=anchor;t.pivot=pivot;t.anchoredPosition=pos;t.sizeDelta=size;return t;}
    Text UILabel(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,int fontSize,string content){var r=UIBlock(name,parent,anchor,pivot,pos,size);var label=r.gameObject.AddComponent<Text>();label.font=forestFont;label.fontSize=fontSize;label.color=uiInk;label.text=content;label.raycastTarget=false;label.supportRichText=false;return label;}
    void SetupForestHUD(){
        forestFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var go=new GameObject("Forest HUD / fixed safe margins",typeof(RectTransform));forestCanvas=go.AddComponent<Canvas>();forestCanvas.renderMode=RenderMode.ScreenSpaceCamera;forestCanvas.worldCamera=cam;forestCanvas.planeDistance=1;forestCanvas.sortingOrder=1000;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        UILabel("Project name",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,-24),new Vector2(600,35),25,"DONGBA / THE LIVING SCRIPT");
        UILabel("Controls",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(33,-65),new Vector2(1500,26),17,"WASD / arrows  move     Space  jump     J / Enter  axe     E  care / feed     R  reset     F5  story / sandbox     Esc  quit");
        hudArea=UILabel("Location",go.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-32,-28),new Vector2(365,35),21,"");hudArea.alignment=TextAnchor.UpperRight;
        hudHealth=UILabel("Health",go.transform,Vector2.zero,Vector2.zero,new Vector2(33,28),new Vector2(230,30),19,"");hudNotice=UILabel("Feedback",go.transform,Vector2.zero,Vector2.zero,new Vector2(33,64),new Vector2(700,35),18,"");
        var card=UIBlock("Interaction card",go.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-32,26),new Vector2(405,92));promptPanel=card.gameObject;var bg=card.gameObject.AddComponent<Image>();bg.color=new Color(.95f,.91f,.82f,.94f);bg.raycastTarget=false;
        var rule=UIBlock("Fine ink rule",card,new Vector2(0,1),new Vector2(0,1),new Vector2(0,0),new Vector2(405,2));rule.gameObject.AddComponent<Image>().color=new Color(.44f,.32f,.20f,.43f);
        hudPromptTitle=UILabel("Target name",card,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-12),new Vector2(370,29),21,"");hudPrompt=UILabel("Action",card,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-50),new Vector2(370,30),18,"");
        var ring=new GameObject("Nearby interaction / non-script ink ring");selectionRing=ring.transform;var line=ring.AddComponent<LineRenderer>();line.sharedMaterial=Mat(Color.white);line.useWorldSpace=false;line.loop=true;line.positionCount=40;line.widthMultiplier=.022f;line.startColor=line.endColor=new Color(.44f,.28f,.13f,.50f);line.sortingOrder=-59;
        for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.55f,.09f,Mathf.Sin(a)*.43f));}UpdateForestPresentation(0);
    }
    void UpdateForestPresentation(float dt){
        if(forestCanvas==null)return;interactionTarget=NearestForestInteraction();promptPanel.SetActive(interactionTarget!=null);selectionRing.gameObject.SetActive(interactionTarget!=null);
        if(interactionTarget!=null){var b=interactionTarget;selectionRing.position=b.root.position;hudPromptTitle.text=b.animal?(b.age<2?"Young animal":"Forest animal"):b.health<=0?"Tree stump":"Forest tree";hudPrompt.text=b.animal?(b.age<2?"E  Feed / grow":"E  Feed / new young"):b.health<=0?"E  Replant here":b.cultivated?"Already tended       J  Chop":"E  Tend       J  Chop";}
        if(displayedHealth!=health){displayedHealth=health;hudHealth.text="Health  "+new string('|',health);}hudNotice.text=noticeTime>0?notice:flourishAge>=0?"The forest is flourishing":"";
        float bank=Feet.x-RiverX(Feet.z);hudArea.text=Feet.x<-28?"Western woods":Feet.x>28?"Eastern woods":Mathf.Abs(bank)<2.5f?"Riverbank":Feet.z>7?"Northern woods":Feet.z<-7?"Southern woods":"Open woodland";
    }
    Being NearestForestInteraction(){
        Being near=null;float best=1.8f*InteractionRange;foreach(var b in plants)if(b.tree){float d=Vector3.Distance(Feet,b.home);if(d<best){best=d;near=b;}}
        foreach(var b in animals)if(b.health>0&&b.migrationState!=2){float d=Vector3.Distance(Feet,b.root.position);if(d<best){best=d;near=b;}}
        if(interactionTarget!=null&&(!interactionTarget.animal||interactionTarget.health>0)){float current=Vector3.Distance(Feet,interactionTarget.root.position);if(current<1.8f*InteractionRange&&current<best+.18f)return interactionTarget;}return near;
    }
    IEnumerator CaptureForestLayout(){
        Directory.CreateDirectory("Verification");yield return new WaitForSeconds(2);
        var oldPos=cam.transform.position;var oldRot=cam.transform.rotation;float oldSize=cam.orthographicSize;
        cam.orthographicSize=17.5f;cam.transform.position=new Vector3(0,39,-44);cam.transform.LookAt(Vector3.zero);Canvas.ForceUpdateCanvases();CaptureSceneToTexture("Verification/forest-map-overview.png",2400,900);
        cam.orthographicSize=oldSize;cam.transform.SetPositionAndRotation(oldPos,oldRot);
        var report=new StringBuilder();report.AppendLine("Forest trees="+plants.FindAll(b=>b.tree).Count+"; undergrowth="+plants.FindAll(b=>!b.tree).Count+"; animals="+animals.Count);
        float minAnimalWidth=float.MaxValue,maxAnimalWidth=0;foreach(var a in animals){float w=a.renderer.sprite.bounds.size.x*a.initialScale.x;minAnimalWidth=Mathf.Min(minAnimalWidth,w);maxAnimalWidth=Mathf.Max(maxAnimalWidth,w);}report.AppendLine("Animal glyph widths min="+minAnimalWidth.ToString("F2")+" max="+maxAnimalWidth.ToString("F2")+" world units");
        report.AppendLine("Map bounds x=+/-"+ForestHalfWidth+"; z=+/-"+ForestHalfDepth+"; play orthographic size="+cam.orthographicSize);
        bool boundary=true;foreach(var p in new[]{new Vector3(-44,0,0),new Vector3(44,0,0),new Vector3(0,0,-20),new Vector3(0,0,20)})boundary&=Free(p);report.AppendLine("Walkable interior near map edges="+boundary);
        report.AppendLine("Deep river blocks walking="+!Free(new Vector3(RiverX(0),0,0)));
        var west=new Vector3(RiverX(-8)-1.4f,0,-8);player.position=west;FollowPlayerCamera(100);jumpAge=0;float elapsed=0;bool canCross=true;float destX=RiverX(-8)+1.4f;
        while(player.position.x<destX&&elapsed<.84f){float dt=Mathf.Max(.001f,Time.deltaTime);var next=player.position+Vector3.right*(4*dt);if(!Free(next)){canCross=false;break;}player.position=next;elapsed+=dt;yield return null;}
        canCross&=player.position.x>=destX-.15f;jumpAge=-1;report.AppendLine("Jump crosses full river width="+canCross);
        var viewpoints=new[]{ForestSpawn,new Vector3(-28,0,7),new Vector3(RiverX(-8)-2f,0,-8),new Vector3(20,0,4)};
        var files=new[]{"Verification/forest-play-01-start.png","Verification/forest-play-02-woods.png","Verification/forest-play-03-river.png","Verification/forest-play-04-east.png"};
        for(int i=0;i<viewpoints.Length;i++){player.position=viewpoints[i];FollowPlayerCamera(100);yield return new WaitForSeconds(.2f);UpdateForestPresentation(0);Canvas.ForceUpdateCanvases();CaptureSceneToTexture(files[i]);}
        yield return new WaitForSeconds(1);float secs=0;int frames=0;while(secs<3){yield return null;secs+=Time.unscaledDeltaTime;frames++;}report.AppendLine("Measured FPS="+(frames/secs).ToString("F1"));
        File.WriteAllText("Verification/forest-layout-checks.txt",report.ToString());yield return Capture();
    }
}
}

