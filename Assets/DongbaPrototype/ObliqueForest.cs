using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DongbaPrototype {
public partial class ObliqueForest : MonoBehaviour {
    class Being { public SpriteRenderer groundRange; public MaterialPropertyBlock groundRangeProperties; public float groundRangeVitality=1; public Sprite fullGlyph,stumpGlyph; public Transform root,art,shadow; public SpriteRenderer renderer; public SpriteRenderer[] leafImages,greenLeafImages; public float[] leafAmounts; public Vector3 home; public string glyphKey; public float phase,inkOpacity=1,stumpScale,opacity=1,migrationTimer; public bool animal,angry,tree,cultivated; public int age=2,speciesIndex,clusterIndex,populationRegion,migrationState; public float fall,shake,follow,careReady,birth=1,targetScale=1; public Vector3 initialScale,initialShadowScale; public int health=3,growth; public float cooldown,protectedUntil,socialReadyAt,navRefresh,moveSpeed,blockedTime; public Vector3 navGoal,socialOffset; public List<Vector3> route; public int routeIndex; }
    class TreeGlyphSet { public Sprite trunk; public Sprite[] leaves=new Sprite[6],greenLeaves=new Sprite[6],pieces=new Sprite[6]; public Vector3[] pieceOffsets=new Vector3[6]; }
    class EcologyParticle { public Transform transform; public SpriteRenderer renderer; public float age,duration,spin,scale,inkOpacity; public Vector3 velocity; public Quaternion initialRotation; public bool flower; }
    readonly List<Being> plants=new List<Being>(),animals=new List<Being>();
    readonly List<SpriteRenderer> riverWashImages=new List<SpriteRenderer>();
    readonly List<Transform> water=new List<Transform>(); readonly List<SpriteRenderer> waterImages=new List<SpriteRenderer>(); readonly List<Vector3> waterOrigins=new List<Vector3>();
    readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    readonly Dictionary<string,AudioClip> naxiWordVoiceClips=new Dictionary<string,AudioClip>();
    readonly HashSet<string> missingNaxiWordVoiceWarnings=new HashSet<string>();
    readonly Dictionary<string,TreeGlyphSet> treeGlyphSets=new Dictionary<string,TreeGlyphSet>();
    readonly List<EcologyParticle> fallingLeaves=new List<EcologyParticle>();
    readonly List<EcologyParticle> flowerTrail=new List<EcologyParticle>();
    Material rootGrowthInk,plainTreeInk,flowerInk; float greenHeight,greenVelocity;
    Sprite flowerGlyph; float flowerDistance; int flowerSide=1,flowerMarksSpawned; int populationRegionCount=6; int[] populationCapacity=new int[6]; float populationCheckAt; bool ecologyPreviewWalking; Vector3 ecologyPreviewDirection;
    Camera cam; Transform player,body,axe,hand; SpriteRenderer playerImage; Material groundMaterial; Texture2D paper,soft; Sprite shadow;
    Vector3 heading=Vector3.right,lastDryPosition; float clock,jumpAge=-1,attackAge=-1,damageCooldown,noticeTime; int health=5; bool hit;
    GUIStyle text,small; string notice=""; bool capture; int facing=1; Quaternion billboard;
    [Header("Flourishing feedback")] public int FlourishThreshold=10;
    [Range(1.5f,4f)] public float EcologyTransitionSeconds=2.5f;
    [Range(.2f,.95f)] public float FlowerTrailSpacing=.72f;
    [Header("Player glyph grip point")]
    [Tooltip("Normalized hand point in player_glyph.png when facing right, measured from the image rectangle's bottom-left. Adjust this when replacing the glyph; the axe follows this point and mirrors it for left-facing attacks.")]
    public Vector2 PlayerHandAnchorNormalized=new Vector2(.72f,.39f);
    [Tooltip("Normalized grip point in axe_glyph.png, measured from its bottom-left. Keep aligned with the handle end if replacing the axe art.")]
    public Vector2 AxeGripAnchorNormalized=new Vector2(32f/256f,62f/256f);
    [Header("Naxi word response audio")]
    [Range(0f,1f)] public float NaxiWordVoiceVolume=.8f;
    [Header("Story mode prototype")]
    [Tooltip("Enable the father-following story opening. Keep disabled for Sandbox Mode.")]
    public bool StoryModeEnabled;
    [Tooltip("Existing ecology balance at which the father leaves to retrieve the lost axe and the capture beat begins.")]
    [Range(-30,-1)] public int StoryCaptureBalanceThreshold=-10;
    [Tooltip("Optional verified father glyph. When empty, the prototype uses a clearly labeled temporary marker.")]
    public Sprite StoryFatherGlyph;
    [Tooltip("Other story systems can subscribe here to begin the rescue sequence when the capture beat ends.")]
    public event Action FatherCaptured;
    Being storyFather; Transform storyFatherRoot,storyFatherArt; SpriteRenderer storyFatherImage; GUIStyle storyPlaceholderStyle;
    enum StoryOpeningState { Off, Following, CaptureBeat, Captured }
    StoryOpeningState storyOpeningState=StoryOpeningState.Off; float storyCaptureTimer; Vector3 storyDepartureTarget;
    int careCount,damageCount; float flourishAge=-1,ecologyVisual; int Balance { get { return careCount-damageCount; } }
    [Header("Whole-scene ecology atmosphere")]
    [Range(.3f,2f)] public float EcologyEffectStrength=1;
    [Range(.1f,1f)] public float GoldenLightSize=.3f;
    [Range(.1f,1f)] public float FallingInkSize=.38f;
    readonly Transform[] motes=new Transform[128],moteHalos=new Transform[128];
    readonly SpriteRenderer[] moteImages=new SpriteRenderer[128],haloImages=new SpriteRenderer[128];
    readonly Vector3[] moteOrigins=new Vector3[128];
    readonly float[] moteSizes=new float[128],moteSpeeds=new float[128];
    Sprite goldenInk,fallingInk;
    [Header("Withering wind glyphs")]
    [Range(.1f,.8f)] public float WitheringWindOpacity=.65f;
    [Range(4f,12f)] public float WitheringWindPassSeconds=7f;
    readonly Transform[] witheringWindGlyphs=new Transform[4];
    readonly SpriteRenderer[] witheringWindImages=new SpriteRenderer[4];
    readonly float[] witheringWindPhases=new float[4],witheringWindDepths=new float[4],witheringWindScales=new float[4];
    Sprite witheringWindSprite;
    Vector3 Feet { get { return player.position; } }
    void Start(){
        Application.runInBackground=true; Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--story-mode-preview")>=0)StoryModeEnabled=true;
        cam=Camera.main; cam.orthographic=true; cam.orthographicSize=8.5f; cam.transform.position=new Vector3(0,17,-20); cam.transform.LookAt(new Vector3(0,0,1)); cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.9f,.85f,.73f); billboard=cam.transform.rotation;
        rootGrowthInk=new Material(Resources.Load<Shader>("RootGrowthInk"));plainTreeInk=new Material(Shader.Find("Sprites/Default"));
        LoadDictionaryArt(); MakePaper(); Ground(); SetupFlourish(); // Wind glyph effect disabled; source asset retained.
        player=new GameObject("Player foot anchor").transform; player.position=ForestSpawn;lastDryPosition=player.position;
        // This user-provided glyph is foot-centered in its transparent canvas; keep its full inscription intact.
        body=Billboard("Player glyph",player,Get("player_glyph"),1.8f); playerImage=body.GetComponent<SpriteRenderer>();
        Billboard("Ground shadow",player,shadow,.8f,true);
        axe=Billboard("Axe",player,Get("axe_glyph"),1.05f); axe.GetComponent<SpriteRenderer>().sprite=Sprite.Create(axe.GetComponent<SpriteRenderer>().sprite.texture,new Rect(0,0,axe.GetComponent<SpriteRenderer>().sprite.texture.width,axe.GetComponent<SpriteRenderer>().sprite.texture.height),AxeGripAnchorNormalized,100);hand=new GameObject("Hand - visible glyph endpoint").transform;hand.SetParent(body,false);hand.localPosition=PlayerHandLocalPosition();axe.SetParent(hand,true);axe.localPosition=Vector3.zero;axe.localRotation=Quaternion.identity;axe.GetComponent<SpriteRenderer>().material=new Material(Resources.Load<Shader>("InkStroke"));axe.gameObject.SetActive(false);
        ComposeForest();
        var layout=new System.Random(148);
        PlaceForestAnimals();
        if(StoryModeEnabled)CreateStoryFather();
        for(int i=0;i<161;i++){
            float z=-20+i*.25f+((float)layout.NextDouble()-.5f)*.24f;
            float offset=((float)layout.NextDouble()-.5f)*1.55f;
            var go=new GameObject("Water glyph "+i);var sr=go.AddComponent<SpriteRenderer>();sr.sortingOrder=-80;sr.sprite=Get("ambient_water_0"+(i%10));sr.color=new Color(.48f,.66f,.73f,.86f);waterImages.Add(sr);
            go.transform.rotation=Quaternion.Euler(90,(float)layout.NextDouble()*16-8,0);go.transform.position=new Vector3(RiverX(z)+offset,.04f,z);
            float scale=(.85f+(float)layout.NextDouble()*.75f)/sr.sprite.bounds.size.x;go.transform.localScale=Vector3.one*scale;water.Add(go.transform);waterOrigins.Add(go.transform.position);
            if(i%3==0){riverWashImages.Add(Wash(new Vector3(RiverX(z),0,z),2.8f,new Color(.32f,.54f,.62f,.25f)));Wash(new Vector3(RiverX(z)-1.4f,0,z),1.1f,new Color(.49f,.37f,.2f,.15f));Wash(new Vector3(RiverX(z)+1.4f,0,z),1.1f,new Color(.49f,.37f,.2f,.15f));}
        }
        var floor=GameObject.Find("Stationary paper ground");if(floor!=null)floor.transform.localScale=new Vector3(9.2f,1,4.2f);
        for(int i=0;i<animals.Count;i++){animals[i].populationRegion=PopulationRegion(animals[i].home.x);populationCapacity[animals[i].populationRegion]++;}populationCheckAt=3.5f;
        FollowPlayerCamera(100); SetupForestHUD();
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--tree-ranges-preview")>=0)StartCoroutine(CaptureTreeRanges());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--tone-preview")>=0)StartCoroutine(CaptureTonePreview());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--friendly-preview")>=0)StartCoroutine(CaptureFriendlyPreview());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--animal-ecology-preview")>=0)StartCoroutine(CaptureAnimalEcology());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--cultivation-check")>=0)StartCoroutine(CheckCultivationOnce());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--flower-stages-preview")>=0)StartCoroutine(CaptureFlowerStages());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--flower-walk-preview")>=0)StartCoroutine(CaptureFlowerWalk());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--root-growth-preview")>=0)StartCoroutine(CaptureRootGrowth());
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ecology-loss-check")>=0)StartCoroutine(CheckEcologyLossEvents());
        capture=Array.IndexOf(Environment.GetCommandLineArgs(),"--oblique-capture")>=0;if(capture)StartCoroutine(Capture());else if(Array.IndexOf(Environment.GetCommandLineArgs(),"--forest-layout-capture")>=0)StartCoroutine(CaptureForestLayout());else if(Array.IndexOf(Environment.GetCommandLineArgs(),"--dictionary-capture")>=0)StartCoroutine(CaptureDictionaryArt());else if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ecology-preview")>=0)StartCoroutine(CaptureEcologyPreview());
    }
    Sprite Get(string name){if(!sprites.TryGetValue(name,out var s)){var t=Resources.Load<Texture2D>("ApprovedArt/"+name);if(t==null)throw new Exception("Missing "+name);s=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,0),100);sprites.Add(name,s);}return s;}
    Transform Billboard(string name,Transform parent,Sprite sprite,float height,bool flat=false){var t=new GameObject(name).transform;t.SetParent(parent,false);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=sprite;t.rotation=flat?Quaternion.Euler(90,0,0):billboard;t.localScale=Vector3.one*(height/(flat?sprite.bounds.size.x:sprite.bounds.size.y));if(flat){r.sortingOrder=-70;t.localPosition=new Vector3(0,.025f,-.15f);r.color=new Color(.28f,.23f,.17f,.15f);}return t;}
    void MakePaper(){
        const int size=1024;paper=new Texture2D(size,size,TextureFormat.RGB24,true);var pixels=new Color[size*size];var rng=new System.Random(31);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float n=((float)rng.NextDouble()-.5f)*.055f;bool fiber=rng.NextDouble()<.055;float f=fiber?.12f:0;pixels[y*size+x]=new Color(.945f+n-f,.904f+n-f,.811f+n-f);}
        paper.SetPixels(pixels);paper.Apply();paper.wrapMode=TextureWrapMode.Repeat;paper.filterMode=FilterMode.Trilinear;paper.anisoLevel=8;
        soft=new Texture2D(64,64,TextureFormat.RGBA32,false);var c=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=new Vector2((x-31.5f)/32,(y-31.5f)/32).magnitude;c[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2));}soft.SetPixels(c);soft.Apply();shadow=Sprite.Create(soft,new Rect(0,0,64,64),new Vector2(.5f,0),64);
    }
    Material Mat(Color color,Texture texture=null){var m=new Material(Shader.Find("Sprites/Default"));m.color=color;if(texture!=null)m.mainTexture=texture;return m;}
    void Ground(){
        var g=GameObject.CreatePrimitive(PrimitiveType.Plane);g.name="Stationary paper ground";g.transform.localScale=new Vector3(9.2f,1,4.2f);groundMaterial=new Material(Resources.Load<Shader>("EcologyPaper"));groundMaterial.mainTexture=paper;var groundMesh=g.GetComponent<MeshFilter>().mesh;var uv=groundMesh.uv;for(int i=0;i<uv.Length;i++)uv[i]*=2;groundMesh.uv=uv;g.GetComponent<Renderer>().material=groundMaterial;g.GetComponent<Renderer>().sortingOrder=-100;
        // A continuous pale-blue ribbon follows the whole map and fades softly at its banks.
        const int segments=160,rows=7;var verts=new Vector3[(segments+1)*rows];var colors=new Color[verts.Length];var tris=new int[segments*6*(rows-1)];int k=0;
        for(int i=0;i<=segments;i++){float z=-20+i*.25f;for(int j=0;j<rows;j++){float u=(j-3)/3f;verts[i*rows+j]=new Vector3(RiverX(z)+u*1.35f,.015f,z);colors[i*rows+j]=new Color(.39f,.59f,.64f,(1-Mathf.Abs(u))*.28f);if(i<segments&&j<rows-1){int a=i*rows+j;tris[k++]=a;tris[k++]=a+rows;tris[k++]=a+1;tris[k++]=a+1;tris[k++]=a+rows;tris[k++]=a+rows+1;}}}
        var mesh=new Mesh();mesh.vertices=verts;mesh.colors=colors;mesh.triangles=tris;mesh.RecalculateNormals();var river=new GameObject("Stationary river wash / full map");river.AddComponent<MeshFilter>().mesh=mesh;river.AddComponent<MeshRenderer>().material=Mat(Color.white);river.GetComponent<MeshRenderer>().sortingOrder=-90;
    }
    float RiverX(float z){return 2+Mathf.Sin(z*.18f)*2.05f;} bool River(Vector3 p){return Mathf.Abs(p.x-RiverX(p.z))<1.05f;}
    void AddPlant(Vector3 p,bool tree,int index){AddDictionaryPlant(p,tree,index);}
    void AddAnimal(Vector3 p,int species=-1){AddDictionaryAnimal(p,species);}
    bool Free(Vector3 p){if(Mathf.Abs(p.x)>ForestHalfWidth||Mathf.Abs(p.z)>ForestHalfDepth)return false;if(jumpAge<0&&River(p))return false;foreach(var b in plants)if(b.tree&&b.health>0){if(Vector3.Distance(p,b.home)<.50f)return false;}return true;}
    void Update(){if(player==null)return;float dt=Time.deltaTime;UpdateFlourish(dt);clock+=dt;damageCooldown-=dt;noticeTime-=dt;UpdateStoryMode(dt);bool storyInputLocked=storyOpeningState==StoryOpeningState.CaptureBeat;var kb=Keyboard.current;Vector3 move=Vector3.zero;if(kb!=null){if(kb.f5Key.wasPressedThisFrame)SetStoryModeEnabled(!StoryModeEnabled);move.x=(kb.dKey.isPressed||kb.rightArrowKey.isPressed?1:0)-(kb.aKey.isPressed||kb.leftArrowKey.isPressed?1:0);move.z=(kb.wKey.isPressed||kb.upArrowKey.isPressed?1:0)-(kb.sKey.isPressed||kb.downArrowKey.isPressed?1:0);if(!storyInputLocked&&kb.spaceKey.wasPressedThisFrame&&jumpAge<0)jumpAge=0;if(!storyInputLocked&&(kb.jKey.wasPressedThisFrame||kb.enterKey.wasPressedThisFrame)&&attackAge<0){attackAge=0;hit=false;}if(!storyInputLocked&&kb.eKey.wasPressedThisFrame)Care();if(kb.rKey.wasPressedThisFrame){player.position=ForestSpawn;lastDryPosition=player.position;health=5;foreach(var b in animals)b.angry=false;}}
        if(storyInputLocked)move=Vector3.zero;
        if(ecologyPreviewWalking)move=ecologyPreviewDirection;Vector3 previousFeet=Feet;move=Vector3.ClampMagnitude(move,1);if(move.sqrMagnitude>0){if(attackAge<0){heading=move;if(move.x!=0)facing=move.x>0?1:-1;}var p=Feet+move*4*dt;if(Free(p))player.position=p;}
        if(!River(Feet))lastDryPosition=Feet;
        float hop=0;if(jumpAge>=0){jumpAge+=dt;hop=Mathf.Sin(Mathf.Clamp01(jumpAge/.85f)*Mathf.PI)*1.45f;if(jumpAge>=.85f){jumpAge=-1;if(River(Feet)){player.position=lastDryPosition;Tell("Back to the riverbank");}}}
        UpdateFlowerTrail(previousFeet,move,dt);
        body.localPosition=new Vector3(0,hop+.015f*Mathf.Sin(clock*3.4f),0);playerImage.flipX=facing<0;playerImage.color=damageCooldown>0?new Color(1,.55f,.45f):Color.white;
        hand.localPosition=PlayerHandLocalPosition();
        if(attackAge>=0){attackAge+=dt;axe.gameObject.SetActive(true);axe.localPosition=Vector3.zero;float swing=attackAge<.16f?Mathf.Lerp(20,85,Mathf.SmoothStep(0,1,attackAge/.16f)):Mathf.Lerp(85,-110,Mathf.SmoothStep(0,1,(attackAge-.16f)/.3f));axe.localRotation=Quaternion.Euler(0,0,facing*swing);var axeScale=axe.localScale;axeScale.x=Mathf.Abs(axeScale.x)*facing;axe.localScale=axeScale;if(attackAge>.25f&&!hit){hit=true;StrikeTrees();foreach(var b in animals){Vector3 d=b.root.position-Feet;if(b.health>0&&d.magnitude<2&&Vector3.Dot(d.normalized,heading)>.15f){ApplyEcologyHit(b);b.angry=true;var p=b.root.position+heading*.35f;if(ClearAnimalStep(p,b))b.root.position=p;if(b.health<=0)b.root.gameObject.SetActive(false);}}}if(attackAge>.62f){attackAge=-1;axe.gameObject.SetActive(false);}}
        foreach(var b in plants){b.shake=Mathf.Max(0,b.shake-dt);if(b.health<=0)b.fall=Mathf.Min(1,b.fall+dt*1.8f);float angle=Mathf.Sin(clock*Mathf.PI*2/2.2f-b.phase)*1.3f+Mathf.Sin(clock*65)*b.shake*14;
            if(b.tree){bool stump=b.health<=0&&b.fall>=1;b.renderer.sprite=stump?b.stumpGlyph:treeGlyphSets[b.glyphKey].trunk;b.renderer.sharedMaterial=stump?plainTreeInk:rootGrowthInk;var tint=TreeTrunkTint();tint.a=b.health<=0&&!stump?(1-Mathf.SmoothStep(0,1,b.fall))*b.inkOpacity:b.inkOpacity;b.renderer.color=tint;UpdateTreeLeaves(b,dt,stump);b.art.rotation=stump?billboard:billboard*Quaternion.Euler(0,0,angle+b.fall*82);if(stump){b.art.localScale=Vector3.one*b.stumpScale;if(b.shadow!=null)b.shadow.localScale=b.initialShadowScale*.48f;}else{b.art.localScale=Vector3.Lerp(b.art.localScale,b.initialScale*b.targetScale,1-Mathf.Exp(-dt*3));if(b.shadow!=null)b.shadow.localScale=b.initialShadowScale;}}else{b.art.rotation=billboard*Quaternion.Euler(0,0,angle);b.art.localScale=Vector3.Lerp(b.art.localScale,b.initialScale*b.targetScale,1-Mathf.Exp(-dt*3));b.renderer.color=SmallPlantTint();}
            b.renderer.sortingOrder=100-Mathf.RoundToInt(b.home.z*10);if(!b.tree)Fade(b);
        }
        playerImage.sortingOrder=100-Mathf.RoundToInt(Feet.z*10);axe.GetComponent<SpriteRenderer>().sortingOrder=playerImage.sortingOrder+1;
        if(!tonePreviewFreezeAnimals)foreach(var b in animals)UpdateAnimalEcology(b,dt,hop);
        UpdatePopulation(dt);UpdateEcologyParticles(dt);UpdateStoryFatherFollow(dt);
        for(int i=0;i<water.Count;i++)water[i].position=waterOrigins[i]+Vector3.right*(.06f*Mathf.Sin(clock*Mathf.PI*2/2.2f-i*.35f));
        FollowPlayerCamera(dt);UpdateForestPresentation(dt);
    }
    void Fade(Being b){var p=cam.WorldToScreenPoint(body.position+Vector3.up*.7f);var q=cam.WorldToScreenPoint(b.root.position+Vector3.up);bool blocks=b.root.position.z<Feet.z&&Mathf.Abs(p.x-q.x)<65&&Mathf.Abs(p.y-q.y)<140;var c=b.renderer.color;c.a=Mathf.MoveTowards(c.a,blocks?.22f:b.inkOpacity,Time.deltaTime*3);b.renderer.color=c;}
    void Care(){Being nearest=NearestForestInteraction();if(nearest==null){Tell("Move closer to a plant or animal");return;}if(nearest.animal){Feed(nearest);}else{if(nearest.health<=0){nearest.health=3;nearest.fall=0;nearest.targetScale=.35f;nearest.art.localScale=nearest.initialScale*.35f;nearest.art.rotation=billboard;nearest.renderer.sprite=nearest.fullGlyph;var freshColor=nearest.renderer.color;freshColor.a=1;nearest.renderer.color=freshColor;nearest.growth=0;nearest.careReady=clock+1;RegisterCare();PlayNaxiWordVoice("tree",nearest);Tell("Replanted here");}else {GrowAround(nearest);}}}
    bool FindLocal(Vector3 origin,float min,float max,int seed,bool animal,out Vector3 found){
        for(int i=0;i<48;i++){float angle=(seed*1.73f+i*2.39996f);float radius=Mathf.Lerp(min,max,(i%7)/6f);var p=origin+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(Mathf.Abs(p.x)>ForestHalfWidth-1.2f||Mathf.Abs(p.z)>ForestHalfDepth-1.0f||River(p)||Mathf.Abs(p.x-RiverX(p.z))<1.35f)continue;
            if(animal&&!ClearAnimalStep(p))continue;
            bool clear=true;foreach(var b in plants)if(Vector3.Distance(p,b.home)<(b.tree?.65f:animal?.6f:.33f)){clear=false;break;}
            foreach(var b in animals)if(b.health>0&&Vector3.Distance(p,b.root.position)<.75f){clear=false;break;}
            if(clear){found=p;return true;}
        }found=origin;return false;
    }
    [Min(25)] public int AnimalPopulationLimit=64;
    void Feed(Being b){
        b.angry=false;b.health=3;EndAnimalVisit(b);b.migrationState=0;b.protectedUntil=clock+12;
        if(clock<b.careReady){Tell("Let this animal finish feeding");return;}
        if(b.age<2){b.age++;b.careReady=clock+1.5f;if(b.age==2)RegisterCare();PlayNaxiWordVoice("animal_"+b.speciesIndex.ToString("00"),b);Tell(b.age==2?"Grown up - next feeding brings a youngster":"Fed - growing bigger");return;}
        int living=0;foreach(var a in animals)if(a.health>0)living++;
        if(living>=Mathf.Max(25,AnimalPopulationLimit)){Tell("The clearing is full of animals");return;}
        // Keep offspring close to the fed adult while retaining the usual local clearance checks.
        if(FindLocal(b.root.position,.7f,1.35f,animals.Count+b.speciesIndex*17,true,out var p)){
            AddAnimal(p,b.speciesIndex);var child=animals[animals.Count-1];child.age=0;child.birth=.42f;child.art.localScale=child.initialScale*.42f;child.careReady=clock+1.5f;child.protectedUntil=clock+18;populationCapacity[child.populationRegion]++;b.careReady=clock+3;PlayNaxiWordVoice("animal_"+b.speciesIndex.ToString("00"),b);
            Tell("Fed - a new animal joins nearby");
        }else Tell("This spot is crowded - feed in an open area");
    }
    void GrowAround(Being b){
        if(b.cultivated){Tell("Already tended - choose another tree");return;}
        if(clock<b.careReady){Tell("New plants are still growing");return;}int count=0;
        for(int i=0;i<4;i++)if(FindLocal(b.home,.75f,2.0f,plants.Count+i,false,out var p)){AddPlant(p,false,plants.Count);var sprout=plants[plants.Count-1];sprout.art.localScale=sprout.initialScale*.08f;sprout.renderer.color=new Color(1,1,1,.92f);count++;}
        if(count>0){b.cultivated=true;b.growth++;if(b.targetScale<1)b.targetScale=Mathf.Min(1,b.targetScale+.22f);RegisterCare();b.careReady=clock+2;b.shake=.18f;PlayNaxiWordVoice("tree",b);Tell("New plants are growing around this tree");}else Tell("This patch is full - tend another tree");
    }
    // Abstract ink particles, not Dongba characters. Textures are created once.
    Sprite MakeEcologyInk(bool falling){
        const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);var data=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float u=(x+ .5f)/size*2-1,v=(y+.5f)/size*2-1;
            float width=falling?.29f*Mathf.Pow(Mathf.Max(0,1-v*v),.65f):.55f;
            float bend=falling?.18f*Mathf.Sin(v*2.7f):.035f*Mathf.Sin(v*9);
            float edge=falling?(width-Mathf.Abs(u-bend))*25: (1-Mathf.Sqrt((u-bend)*(u-bend)/.32f+v*v/.55f))*8;
            float alpha=Mathf.Clamp01(edge)*Mathf.Clamp01((.96f-Mathf.Abs(v))*20);
            alpha*=.87f+.13f*Mathf.Sin(x*1.3f+y*.8f)*Mathf.Sin(y*2.1f);
            data[y*size+x]=new Color(1,1,1,alpha);
        }
        tex.SetPixels(data);tex.Apply();tex.filterMode=FilterMode.Bilinear;tex.wrapMode=TextureWrapMode.Clamp;
        return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
    }
    void SetupFlourish(){
        flowerInk=new Material(Resources.Load<Shader>("FlowerInk"));
        var texture=Resources.Load<Texture2D>("ApprovedArt/ecology_flower_v2");if(texture==null)throw new InvalidOperationException("Missing user-provided ecology flower glyph");
        flowerGlyph=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,0),64);
        flowerGlyph.name="User-provided flower pictograph / transparent";
        for(int i=0;i<64;i++)flowerTrail.Add(CreateEcologyParticle("Walk flower mark",true));
        for(int i=0;i<256;i++)fallingLeaves.Add(CreateEcologyParticle("Falling original tree stroke",false));
    }
    EcologyParticle CreateEcologyParticle(string name,bool flower){var t=new GameObject(name).transform;t.rotation=billboard;var r=t.gameObject.AddComponent<SpriteRenderer>();if(flower)r.sharedMaterial=flowerInk;r.sortingOrder=0;r.enabled=false;t.gameObject.SetActive(false);return new EcologyParticle{transform=t,renderer=r,flower=flower};}
    // Former wind-glyph creation and animation removed; original texture retained.
    // Hand coordinates are in the upright glyph plane, not the world's vertical plane.
    Vector3 PlayerHandLocalPosition(){
        if(playerImage==null||playerImage.sprite==null)return Vector3.zero;
        var sprite=playerImage.sprite;
        Vector2 anchor=PlayerHandAnchorNormalized;
        if(facing<0)anchor.x=1-anchor.x; // SpriteRenderer.flipX mirrors the drawn glyph, not child transforms.
        float ppu=sprite.pixelsPerUnit;
        return new Vector3((anchor.x*sprite.rect.width-sprite.pivot.x)/ppu,(anchor.y*sprite.rect.height-sprite.pivot.y)/ppu,0);
    }
    Vector3 HandPosition(){return body.TransformPoint(PlayerHandLocalPosition());}
    void RegisterCare(){careCount++;}
    void PlayNaxiWordVoice(string key,Being source){
        if(!naxiWordVoiceClips.TryGetValue(key,out var clip)){
            clip=Resources.Load<AudioClip>("Audio/NaxiWords/"+key);
            naxiWordVoiceClips[key]=clip;
        }
        if(clip==null){if(missingNaxiWordVoiceWarnings.Add(key))Debug.LogWarning("Missing verified Naxi word recording at Resources/Audio/NaxiWords/"+key+".wav; action audio hook is ready, but no invented voice is played.");return;}
        if(source==null||source.root==null)return;
        var speaker=source.root.GetComponent<AudioSource>();
        if(speaker==null)speaker=source.root.gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake=false;speaker.loop=false;speaker.spatialBlend=1f;speaker.dopplerLevel=0f;
        speaker.rolloffMode=AudioRolloffMode.Linear;speaker.minDistance=1.1f;speaker.maxDistance=14f;
        speaker.PlayOneShot(clip,NaxiWordVoiceVolume);
    }
    void RegisterDamage(){damageCount++;}
    void ApplyEcologyHit(Being target){
        if(target.health<=0)return;
        if(target.animal){EndAnimalVisit(target);target.migrationState=0;target.socialReadyAt=clock+30;}
        target.health--;
        // One ecological loss per felled tree / killed animal, never per axe hit.
        if(target.health==0)RegisterDamage();
    }
    IEnumerator CheckEcologyLossEvents(){
        yield return null;
        var log=new System.Text.StringBuilder();
        foreach(bool isAnimal in new[]{false,true}){
            var target=new Being{tree=!isAnimal,animal=isAnimal,health=3};int start=damageCount;
            ApplyEcologyHit(target);log.AppendLine((isAnimal?"Animal":"Tree")+" first hit: "+(damageCount-start));
            ApplyEcologyHit(target);log.AppendLine("Second hit: "+(damageCount-start));
            ApplyEcologyHit(target);log.AppendLine("Death/felling: "+(damageCount-start));
            ApplyEcologyHit(target);log.AppendLine("Repeated hit after death: "+(damageCount-start));
            if(damageCount-start!=1)throw new InvalidOperationException("Ecology loss counted more than once");
        }
        Directory.CreateDirectory("Verification");File.WriteAllText("Verification/ecology-loss-events.txt",log.ToString());Application.Quit();
    }
    void UpdateFlourish(float dt){
        float greenTarget=Mathf.Clamp01((float)Balance/Mathf.Max(1,FlourishThreshold));
        greenHeight=Mathf.SmoothDamp(greenHeight,greenTarget,ref greenVelocity,.16f,Mathf.Infinity,dt);
        rootGrowthInk.SetFloat("_Growth",greenHeight);
        float desired=Balance>=FlourishThreshold?1:Balance<0?-Mathf.Clamp01(-Balance/10f):0;
        ecologyVisual=Mathf.MoveTowards(ecologyVisual,desired,dt/Mathf.Max(1.5f,EcologyTransitionSeconds));flourishAge=Balance>=FlourishThreshold?clock:-1;
        float decline=Mathf.Clamp01(-ecologyVisual);
        groundMaterial.SetFloat("_Decline",decline);UpdateTreeGroundRanges(dt,decline);
        Color waterTone=Color.Lerp(new Color(.48f,.66f,.73f,.86f),new Color(.59f,.62f,.63f,.70f),decline);for(int i=0;i<waterImages.Count;i++)waterImages[i].color=waterTone;
        foreach(var wash in riverWashImages)wash.color=Color.Lerp(new Color(.32f,.54f,.62f,.25f),new Color(.58f,.60f,.59f,.14f),decline);
    }
    Color TreeTrunkTint(){return Color.Lerp(Color.white,new Color(.88f,.91f,.94f),Mathf.Clamp01(-ecologyVisual)*.20f);}
    Color SmallPlantTint(){return Color.white;}
    Color AnimalEcologyTint(){return Color.Lerp(Color.white,new Color(.90f,.92f,.94f,1),Mathf.Clamp01(-ecologyVisual)*.20f);}
    void UpdateTreeLeaves(Being tree,float dt,bool stump){
        if(tree.leafImages==null)return;
        float decline=Mathf.Clamp01(-ecologyVisual);
        float cutFade=tree.health<=0&&!stump?1-Mathf.SmoothStep(0,1,tree.fall):tree.health<=0?0:1;
        for(int i=0;i<tree.leafImages.Length;i++){
            float threshold=.16f+i*.125f+Mathf.Repeat(tree.phase*.173f,.08f);
            bool attached=tree.leafAmounts[i]>.5f;
            // Hysteresis prevents threshold chatter; attached strokes never become ghost images.
            bool next=attached?decline<threshold:decline<threshold-.045f;
            if(attached&&!next&&!stump&&tree.health>0&&Vector3.Distance(tree.root.position,Feet)<22f)SpawnFallingLeaf(tree,i);
            tree.leafAmounts[i]=next?1:0;
            var image=tree.leafImages[i];image.sortingOrder=tree.renderer.sortingOrder;
            image.color=new Color(1,1,1,cutFade*tree.inkOpacity);image.enabled=next&&!stump&&cutFade>0;
            tree.greenLeafImages[i].enabled=false;
        }
    }
    void SpawnFallingLeaf(Being tree,int group){
        EcologyParticle p=null;for(int i=0;i<fallingLeaves.Count;i++)if(!fallingLeaves[i].transform.gameObject.activeSelf){p=fallingLeaves[i];break;}if(p==null)return;
        var layers=treeGlyphSets[tree.glyphKey];p.renderer.sprite=layers.pieces[group];p.renderer.enabled=true;p.renderer.color=Color.white;p.renderer.sortingOrder=tree.renderer.sortingOrder+1;
        p.transform.position=tree.art.TransformPoint(layers.pieceOffsets[group]);p.initialRotation=tree.art.rotation;p.transform.rotation=p.initialRotation;p.transform.localScale=tree.art.lossyScale;p.age=0;p.duration=1.25f+(group%3)*.12f;p.spin=(group%2==0?1:-1)*(42f+group*7f);
        p.velocity=Vector3.down*(.30f+(group%3)*.08f)+cam.transform.right*((group%2==0?1:-1)*(.18f+(group%3)*.04f));p.transform.gameObject.SetActive(true);
    }
    float FlowerInkOpacity(){return Mathf.Lerp(.15f,1f,Mathf.InverseLerp(1,6,Balance));}
    float CurrentFlowerSpacing(){
        // Above six, add density without changing size, opacity or lifetime.
        return Mathf.Max(.32f,FlowerTrailSpacing*(5f/6f)/(1+.12f*Mathf.Max(0,Balance-6)));
    }
    void UpdateFlowerTrail(Vector3 previousFeet,Vector3 inputMove,float dt){
        if(Balance<1||jumpAge>=0||inputMove.sqrMagnitude<.01f){flowerDistance=0;return;}
        Vector3 delta=Feet-previousFeet;delta.y=0;float distance=delta.magnitude;
        // Reject teleports, not ordinary movement during slow frames.
        if(distance>Mathf.Max(1f,4f*dt*1.5f)){flowerDistance=0;return;}
        if(distance<.0001f)return;
        Vector3 direction=delta/distance;float spacing=Mathf.Max(.2f,CurrentFlowerSpacing());
        flowerDistance=Mathf.Min(flowerDistance,spacing);
        float next=spacing-flowerDistance;
        while(next<=distance){
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
            Vector3 anchor=previousFeet+direction*next-direction*.18f;
            // Fixed per-pair offsets: natural spacing, no animation jitter or glyph rotation.
            int seed=flowerMarksSpawned/2;
            float left=.13f+.09f*FlowerScatter(seed,1),right=.13f+.09f*FlowerScatter(seed,2);
            float ahead=(FlowerScatter(seed,3)-.5f)*.05f;
            float behind=spacing*.5f+(FlowerScatter(seed,4)-.5f)*.05f;
            SpawnFlower(anchor+side*left+direction*ahead);
            SpawnFlower(anchor-side*right-direction*behind);
            next+=spacing;
        }
        flowerDistance=(flowerDistance+distance)%spacing;
    }
    static float FlowerScatter(int index,int salt){unchecked{uint h=(uint)(index*374761393+salt*668265263);h=(h^(h>>13))*1274126177u;return (h&65535)/65535f;}}
    void SpawnFlower(Vector3 groundPosition){
        EcologyParticle p=null;for(int i=0;i<flowerTrail.Count;i++)if(!flowerTrail[i].transform.gameObject.activeSelf){p=flowerTrail[i];break;}if(p==null)return;
        p.renderer.sprite=flowerGlyph;p.renderer.enabled=true;p.renderer.color=new Color(.82f,.34f,.49f,0);p.renderer.sortingOrder=98-Mathf.RoundToInt(groundPosition.z*10);
        p.transform.position=new Vector3(groundPosition.x,.035f,groundPosition.z);p.transform.rotation=billboard;p.transform.localScale=Vector3.zero;p.scale=(.85f*.25f)/Mathf.Max(.001f,flowerGlyph.bounds.size.y);p.age=0;p.duration=2f;p.inkOpacity=FlowerInkOpacity();p.transform.gameObject.SetActive(true);flowerMarksSpawned++;
    }
    Vector3 PreviewWalkDirection(){
        Vector3[] directions={Vector3.left,Vector3.right,Vector3.forward,Vector3.back,new Vector3(-1,0,1).normalized,new Vector3(1,0,1).normalized,new Vector3(-1,0,-1).normalized,new Vector3(1,0,-1).normalized};
        foreach(var direction in directions){bool clear=true;for(float d=.25f;d<=3.25f;d+=.25f)if(!Free(ForestSpawn+direction*d)){clear=false;break;}if(clear)return direction;}
        return Vector3.zero;
    }
    void UpdateEcologyParticles(float dt){
        foreach(var p in fallingLeaves){if(!p.transform.gameObject.activeSelf)continue;p.age+=dt;if(p.age>=p.duration){p.transform.gameObject.SetActive(false);continue;}p.velocity+=Vector3.down*(.34f*dt);p.transform.position+=p.velocity*dt;p.transform.rotation=p.initialRotation*Quaternion.Euler(0,0,p.spin*p.age);var color=p.renderer.color;color.a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,p.duration,p.age));p.renderer.color=color;}
        foreach(var p in flowerTrail){if(!p.transform.gameObject.activeSelf)continue;p.age+=dt;if(p.age>=p.duration){p.transform.gameObject.SetActive(false);continue;}float appear=Mathf.SmoothStep(0,1,p.age/.2f);float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.5f,p.duration,p.age));p.transform.localScale=Vector3.one*(p.scale*appear);var color=p.renderer.color;if(Balance>0)p.inkOpacity=Mathf.MoveTowards(p.inkOpacity,FlowerInkOpacity(),dt*2);color.a=p.inkOpacity*appear*fade;p.renderer.color=color;}
    }
    int PopulationRegion(float x){return Mathf.Clamp(Mathf.FloorToInt((x+ForestHalfWidth)/(2*ForestHalfWidth)*populationRegionCount),0,populationRegionCount-1);}
    SpriteRenderer Wash(Vector3 p,float size,Color color){var t=new GameObject("Soft ground ink").transform;t.position=p+Vector3.up*.02f;t.rotation=Quaternion.Euler(90,0,0);t.localScale=new Vector3(size,size*.7f,1);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=shadow;r.color=color;r.sortingOrder=-85;return r;}
    void StrikeTrees(){foreach(var b in plants){Vector3 d=b.home-Feet;if(b.tree&&b.health>0&&d.magnitude<2&&Vector3.Dot(d.normalized,heading)>.15f){ApplyEcologyHit(b);b.shake=.45f;Tell(b.health==0?"Felled - E to replant":"Axe struck the trunk");break;}}}
    void Tell(string s){notice=s;noticeTime=2.5f;}
    void DrawWitheringWindGlyphs(){
        if(cam==null||witheringWindSprite==null)return;
        float pixelsPerUnit=Screen.height/(cam.orthographicSize*2f);
        Texture texture=witheringWindSprite.texture;
        Vector2 glyphSize=witheringWindSprite.bounds.size;
        Color previous=GUI.color;
        for(int i=0;i<witheringWindGlyphs.Length;i++){
            if(!witheringWindGlyphs[i].gameObject.activeSelf)continue;
            Vector3 screen=cam.WorldToScreenPoint(witheringWindGlyphs[i].position);
            if(screen.z<=0)continue;
            float width=glyphSize.x*witheringWindGlyphs[i].localScale.x*pixelsPerUnit*1.35f;
            float height=glyphSize.y*witheringWindGlyphs[i].localScale.y*pixelsPerUnit*1.35f;
            float decline=Mathf.InverseLerp(0f,.5f,-ecologyVisual);
            GUI.color=new Color(1,1,1,Mathf.Lerp(.52f,.84f,decline));
            GUI.DrawTexture(new Rect(screen.x-width*.5f,Screen.height-screen.y-height*.5f,width,height),texture,ScaleMode.StretchToFill,true);
        }
        GUI.color=previous;
    }
    void OnGUI(){DrawWitheringWindGlyphs();DrawStoryFatherPlaceholder();}
    void CreateStoryFather(){
        storyFather=new Being{animal=true,age=2,birth=1,phase=2.17f,route=new List<Vector3>(96)};
        storyFatherRoot=new GameObject("Story Father foot anchor").transform;
        storyFatherRoot.position=player.position+new Vector3(-1.25f,0,-.75f);storyFather.root=storyFatherRoot;storyFather.home=storyFatherRoot.position;
        Sprite glyph=StoryFatherGlyph!=null?StoryFatherGlyph:shadow;
        float height=StoryFatherGlyph!=null?1.8f:.72f;
        storyFatherArt=Billboard(StoryFatherGlyph!=null?"Father glyph (user supplied)":"Father placeholder marker - not a Dongba glyph",storyFatherRoot,glyph,height);
        storyFatherImage=storyFatherArt.GetComponent<SpriteRenderer>();storyFather.renderer=storyFatherImage;storyFather.art=storyFatherArt;
        storyFather.initialScale=storyFatherArt.localScale;storyFatherImage.color=StoryFatherGlyph!=null?Color.white:new Color(.34f,.23f,.13f,.9f);
        storyFatherImage.sortingOrder=100-Mathf.RoundToInt(storyFatherRoot.position.z*10);
        storyOpeningState=StoryOpeningState.Following;
    }
    void SetStoryModeEnabled(bool enabled){
        StoryModeEnabled=enabled;
        if(enabled){if(storyFatherRoot==null)CreateStoryFather();else{storyFatherRoot.position=player.position+new Vector3(-1.25f,0,-.75f);storyFatherRoot.gameObject.SetActive(true);storyFatherImage.color=StoryFatherGlyph!=null?Color.white:new Color(.34f,.23f,.13f,.9f);storyFather.route.Clear();storyFather.navRefresh=clock;storyOpeningState=StoryOpeningState.Following;}Tell("Story Mode enabled — Father is following");}
        else{if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);storyOpeningState=StoryOpeningState.Off;Tell("Sandbox Mode enabled");}
    }
    void UpdateStoryMode(float dt){
        if(!StoryModeEnabled)return;
        if(storyOpeningState==StoryOpeningState.Following&&Balance<=StoryCaptureBalanceThreshold){
            storyOpeningState=StoryOpeningState.CaptureBeat;storyCaptureTimer=2.35f;
            storyDepartureTarget=storyFatherRoot.position+(storyFatherRoot.position-Feet).normalized*1.8f;
            if(storyDepartureTarget.sqrMagnitude<.01f)storyDepartureTarget=storyFatherRoot.position+Vector3.right*1.8f;
            Tell("Father leaves to retrieve the lost axe...");
        }
        if(storyOpeningState!=StoryOpeningState.CaptureBeat)return;
        storyCaptureTimer-=dt;
        if(storyFatherRoot!=null){
            if(storyCaptureTimer>1.35f){Vector3 next=Vector3.MoveTowards(storyFatherRoot.position,storyDepartureTarget,1.1f*dt);if(ClearAnimalStep(next,storyFather))storyFatherRoot.position=next;}
            float alpha=Mathf.Clamp01(storyCaptureTimer/1.35f);storyFatherImage.color=StoryFatherGlyph!=null?new Color(1,1,1,alpha):new Color(.34f,.23f,.13f,.9f*alpha);
        }
        if(storyCaptureTimer<=0){
            storyOpeningState=StoryOpeningState.Captured;if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);
            Tell("The mountain keeper has captured Father. The rescue journey begins.");
            FatherCaptured?.Invoke();
        }
    }
    void UpdateStoryFatherFollow(float dt){
        if(!StoryModeEnabled||storyOpeningState!=StoryOpeningState.Following||storyFatherRoot==null)return;
        Vector3 back=heading.sqrMagnitude>.01f?-heading.normalized:Vector3.left;
        Vector3 goal=Feet+back*1.35f+new Vector3(0,0,-.18f);
        if(clock>=storyFather.navRefresh){
            storyFather.navRefresh=clock+.48f;
            if(!ClearAnimalStep(goal,storyFather)||Vector3.Distance(storyFatherRoot.position,goal)>1.2f)PlanAnimalRoute(storyFather,goal);
        }
        if(HasAnimalRoute(storyFather)){
            Vector3 waypoint=storyFather.route[storyFather.routeIndex];Vector3 next=Vector3.MoveTowards(storyFatherRoot.position,waypoint,2.05f*dt);
            if(ClearAnimalStep(next,storyFather)){storyFatherRoot.position=next;if(Vector3.Distance(next,waypoint)<.12f)storyFather.routeIndex++;}
            else if(storyFather.blockedTime<1.5f){storyFather.blockedTime+=dt;}else{storyFather.route.Clear();storyFather.navRefresh=clock+.7f;storyFather.blockedTime=0;}
        }
        if(storyFatherImage!=null){storyFatherArt.localPosition=new Vector3(0,.02f*Mathf.Sin(clock*2.4f+storyFather.phase),0);storyFatherImage.sortingOrder=100-Mathf.RoundToInt(storyFatherRoot.position.z*10);}
    }
    void DrawStoryFatherPlaceholder(){
        if(!StoryModeEnabled||StoryFatherGlyph!=null||storyFatherRoot==null||!storyFatherRoot.gameObject.activeSelf)return;
        Vector3 screen=cam.WorldToScreenPoint(storyFatherRoot.position+Vector3.up*.55f);if(screen.z<=0)return;
        if(storyPlaceholderStyle==null){storyPlaceholderStyle=new GUIStyle(GUI.skin.label);storyPlaceholderStyle.fontSize=Mathf.Clamp(Screen.height/65,12,20);storyPlaceholderStyle.fontStyle=FontStyle.Bold;storyPlaceholderStyle.normal.textColor=new Color(.27f,.17f,.09f);storyPlaceholderStyle.alignment=TextAnchor.MiddleCenter;}
        GUI.Label(new Rect(screen.x-75,Screen.height-screen.y-25,150,24),"父亲（形象占位）",storyPlaceholderStyle);
    }
    IEnumerator CheckCultivationOnce(){
        yield return null;Directory.CreateDirectory("Verification");var report=new System.Text.StringBuilder();
        Being first=null,second=null;int initialPlants=plants.Count,initialCare=careCount;
        for(int i=0;i<initialPlants;i++){var t=plants[i];if(!t.tree)continue;GrowAround(t);if(t.cultivated){if(first==null)first=t;else{second=t;break;}}}
        if(first==null||second==null)throw new InvalidOperationException("Need two valid cultivation sites");
        report.AppendLine("Separate trees each cultivated once="+(careCount==initialCare+2));
        int count=plants.Count,value=careCount;first.careReady=0;GrowAround(first);GrowAround(first);
        report.AppendLine("Repeated cultivation adds no plants or value="+(plants.Count==count&&careCount==value));
        player.position=first.home;first.health=0;Care();bool replanted=first.health==3;count=plants.Count;value=careCount;first.careReady=0;GrowAround(first);
        report.AppendLine("Replant works, but cannot reset used cultivation="+(replanted&&first.cultivated&&plants.Count==count&&careCount==value));
        var young=animals[0];young.age=0;young.careReady=0;Feed(young);young.careReady=0;Feed(young);
        report.AppendLine("Animal feeding unaffected="+(young.age==2));
        player.position=second.home-Vector3.right*.8f;FollowPlayerCamera(100);yield return null;UpdateForestPresentation(0);
        CaptureSceneToTexture("Verification/cultivated-tree.png");
        File.WriteAllText("Verification/cultivation-once.txt",report.ToString());Application.Quit();
    }
    IEnumerator CaptureFlowerStages(){
        Time.captureFramerate=30;string folder="Verification/flower-stages";Directory.CreateDirectory(folder+"/frames");
        int[] values={1,3,6,10,16};int frame=0;
        foreach(int value in values){
            while(Balance<value)RegisterCare();
            player.position=ForestSpawn;FollowPlayerCamera(100);flowerDistance=0;
            foreach(var flower in flowerTrail)flower.transform.gameObject.SetActive(false);
            ecologyPreviewWalking=true;ecologyPreviewDirection=PreviewWalkDirection();int before=flowerMarksSpawned;
            for(int step=0;step<96;step++,frame++){
                if(step==44){CaptureSceneToTexture(folder+"/value-"+value+".png");File.AppendAllText(folder+"/checks.txt","value="+value+"; opacity="+FlowerInkOpacity().ToString("F2")+"; spacing="+CurrentFlowerSpacing().ToString("F3")+"; flowers during equal walk="+(flowerMarksSpawned-before)+"\n");}
                if(step==46)ecologyPreviewDirection=-ecologyPreviewDirection;
                if(step==90)ecologyPreviewWalking=false;
                CaptureSceneToTexture(folder+"/frames/"+frame.ToString("D4")+".png",960,540);yield return null;
            }
        }
        int count=flowerMarksSpawned;jumpAge=.1f;UpdateFlowerTrail(Feet-Vector3.right*.4f,Vector3.right,.1f);bool airborne=flowerMarksSpawned==count;
        jumpAge=-1;UpdateFlowerTrail(Feet-Vector3.right*.4f,Vector3.zero,.1f);bool stopped=flowerMarksSpawned==count;
        while(Balance>0)RegisterDamage();for(int i=0;i<10;i++)UpdateFlowerTrail(Feet-Vector3.right*.4f,Vector3.right,.1f);
        File.AppendAllText(folder+"/checks.txt","Airborne blocked="+airborne+"; stopped blocked="+stopped+"; zero blocked="+(flowerMarksSpawned==count));Application.Quit();
    }
    IEnumerator CaptureFlowerWalk(){
        Time.captureFramerate=30;string folder="Verification/flower-walk";Directory.CreateDirectory(folder+"/frames");
        while(Balance<FlourishThreshold)RegisterCare();
        player.position=ForestSpawn;FollowPlayerCamera(100);Vector3 direction=PreviewWalkDirection();
        int stoppedAt=0,jumpAt=0;
        for(int frame=0;frame<360;frame++){
            if(frame==30){ecologyPreviewWalking=true;ecologyPreviewDirection=direction;}
            if(frame==80){ecologyPreviewDirection=-direction;}
            if(frame==130){ecologyPreviewWalking=false;stoppedAt=flowerMarksSpawned;CaptureSceneToTexture(folder+"/flower-trail.png");}
            if(frame==160){File.AppendAllText(folder+"/checks.txt","Stopped adds no flowers="+(stoppedAt==flowerMarksSpawned)+"\n");ecologyPreviewWalking=true;ecologyPreviewDirection=direction;jumpAge=0;jumpAt=flowerMarksSpawned;}
            if(frame==180)File.AppendAllText(folder+"/checks.txt","Airborne adds no flowers="+(jumpAt==flowerMarksSpawned)+"\n");
            if(frame==205)ecologyPreviewDirection=-direction;
            if(frame==250){while(Balance>0)RegisterDamage();stoppedAt=flowerMarksSpawned;}
            if(frame==280){ecologyPreviewWalking=false;File.AppendAllText(folder+"/checks.txt","Leaving flourish stops flowers="+(stoppedAt==flowerMarksSpawned)+"\n");}
            CaptureSceneToTexture(folder+"/frames/"+frame.ToString("D4")+".png",960,540);yield return null;
        }
        // Verify the formerly rejected 10 FPS-sized movement with the same distance sampler.
        RegisterCare();flowerDistance=0;int count=flowerMarksSpawned;
        for(int i=0;i<4;i++)UpdateFlowerTrail(Feet-Vector3.right*.4f,Vector3.right,.1f);
        File.AppendAllText(folder+"/checks.txt","Slow-frame movement generates flowers="+(flowerMarksSpawned>count)+"\nTotal spawned="+flowerMarksSpawned);
        Application.Quit();
    }
    IEnumerator CaptureRootGrowth(){
        Time.captureFramerate=30;string folder="Verification/root-growth";Directory.CreateDirectory(folder+"/frames");
        player.position=ForestSpawn;FollowPlayerCamera(100);
        for(int frame=0;frame<240;frame++){
            if(frame==30)RegisterCare();
            if(frame==65){RegisterCare();RegisterCare();}
            if(frame==100)for(int i=0;i<3;i++)RegisterCare();
            if(frame==140)for(int i=0;i<4;i++)RegisterCare();
            if(frame==195)for(int i=0;i<5;i++)RegisterDamage();
            if(frame==25||frame==60||frame==95||frame==135||frame==185||frame==235){CaptureSceneToTexture(folder+"/balance-"+Balance+".png");File.AppendAllText(folder+"/checks.txt","balance="+Balance+"; green height="+greenHeight.ToString("F3")+"\n");}
            CaptureSceneToTexture(folder+"/frames/"+frame.ToString("D4")+".png",960,540);
            yield return null;
        }
        Application.Quit();
    }
    IEnumerator CaptureEcologyPreview(){
        Time.captureFramerate=30; string folder=Path.GetFullPath("Verification/ecology-v2/runtime");string frames=Path.Combine(folder,"frames");Directory.CreateDirectory(frames);
        File.WriteAllText(Path.Combine(folder,"state-checks.txt"),"Ecology preview build 2026-10-25B started; planned frames=360.\n");
        player.position=ForestSpawn;FollowPlayerCamera(100);yield return new WaitForSeconds(.5f);CaptureSceneToTexture(Path.Combine(folder,"normal.png"),1600,900);
        RenderTexture oldTarget=cam.targetTexture;RenderTexture target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);Texture2D frameTexture=new Texture2D(960,540,TextureFormat.RGB24,false);RenderTexture oldActive=RenderTexture.active;
        try{
            for(int frame=0;frame<660;frame++){
                if(frame==45)for(int i=0;i<FlourishThreshold;i++)RegisterCare();
                if(frame==130){ecologyPreviewWalking=true;ecologyPreviewDirection=PreviewWalkDirection();File.AppendAllText(Path.Combine(folder,"state-checks.txt"),"Preview path direction="+ecologyPreviewDirection+"; start="+player.position+".\n");}
                if(frame==180){ecologyPreviewWalking=false;player.position=ForestSpawn;FollowPlayerCamera(100);var debug=new System.Text.StringBuilder("Preview path end reset to="+player.position+"; camera="+cam.transform.position+"; flower marks so far="+flowerMarksSpawned+".\n");foreach(var f in flowerTrail)if(f.transform.gameObject.activeSelf)debug.Append("flower "+f.transform.position+" screen="+cam.WorldToScreenPoint(f.transform.position)+" color="+f.renderer.color+" sprite="+f.renderer.sprite+"\n");File.AppendAllText(Path.Combine(folder,"state-checks.txt"),debug.ToString());CaptureSceneToTexture(Path.Combine(folder,"flourishing.png"),1600,900);}
                if(frame==200){ecologyPreviewWalking=true;ecologyPreviewDirection=-PreviewWalkDirection();}
                if(frame==210)jumpAge=0;
                if(frame==225)ecologyPreviewWalking=false;
                if(frame==235){ecologyPreviewWalking=false;player.position=ForestSpawn;FollowPlayerCamera(100);while(Balance>=0)RegisterDamage();while(Balance>-10)RegisterDamage();}
                if(frame==335){player.position=ForestSpawn;FollowPlayerCamera(100);CaptureSceneToTexture(Path.Combine(folder,"withering-light.png"),1600,900);}
                if(frame==355){player.position=ForestSpawn;FollowPlayerCamera(100);CaptureSceneToTexture(Path.Combine(folder,"withering-medium.png"),1600,900);}
                if(frame==430){player.position=ForestSpawn;FollowPlayerCamera(100);CaptureSceneToTexture(Path.Combine(folder,"withering-severe.png"),1600,900);CaptureSceneToTexture(Path.Combine(folder,"withering.png"),1600,900);}
                if(frame==460){while(Balance<FlourishThreshold)RegisterCare();File.AppendAllText(Path.Combine(folder,"state-checks.txt"),"Recovery initiated through existing care counter; balance="+Balance+".\n");}
                if(frame==640){player.position=ForestSpawn;FollowPlayerCamera(100);CaptureSceneToTexture(Path.Combine(folder,"restored.png"),1600,900);}
                cam.targetTexture=target;cam.Render();RenderTexture.active=target;frameTexture.ReadPixels(new Rect(0,0,960,540),0,0);frameTexture.Apply(false,false);File.WriteAllBytes(Path.Combine(frames,"frame-"+frame.ToString("D4")+".jpg"),frameTexture.EncodeToJPG(88));
                yield return null;
            }
            int full=0,missing=0;foreach(var t in plants)if(t.tree){bool complete=true;foreach(var amount in t.leafAmounts)complete&=amount==1;if(complete)full++;else missing++;}
            File.AppendAllText(Path.Combine(folder,"state-checks.txt"),"Restored complete trees="+full+"; incomplete="+missing+".\n");
            int oldMarks=flowerMarksSpawned;UpdateFlowerTrail(Feet-Vector3.right*.15f,Vector3.zero,1f/60);bool noStopped=flowerMarksSpawned==oldMarks;float savedJump=jumpAge;jumpAge=.1f;UpdateFlowerTrail(Feet-Vector3.right*.15f,Vector3.right,1f/60);bool noAir=flowerMarksSpawned==oldMarks;jumpAge=savedJump;
            File.AppendAllText(Path.Combine(folder,"state-checks.txt"),"No flowers when stopped="+noStopped+"; airborne="+noAir+".\n");
            int flowers=0;foreach(var p in flowerTrail)if(p.transform.gameObject.activeSelf)flowers++;
            File.AppendAllText(Path.Combine(folder,"state-checks.txt"),"Frame captures=660 at 30 fps (22 seconds), fixed simulation timestep.\nNormal balance=0.\nFlourish balance="+FlourishThreshold+"; flower marks spawned while moving="+flowerMarksSpawned+"; ecology visual at lush capture="+ecologyVisual.ToString("F2")+".\nWither balance=-10 at severe capture; animals still moving out="+animals.FindAll(a=>a.migrationState==1).Count+"; animals outside active region="+animals.FindAll(a=>a.migrationState==2).Count+"; animals returning="+animals.FindAll(a=>a.migrationState==3).Count+"; healthy animals="+animals.FindAll(a=>a.health>0).Count+".\nRecovery balance="+Balance+"; ecology visual after recovery="+ecologyVisual.ToString("F2")+".\nTree glyphs preserve source alpha in unaltered cache; foliage layers are derived from their original raster strokes.\nFlower source=the user-provided flower pictograph; white background removed by alpha conversion.");
        }finally{ecologyPreviewWalking=false;cam.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Destroy(target);Destroy(frameTexture);}
        Application.Quit();
    }
    IEnumerator Capture(){yield return new WaitForSeconds(2);Directory.CreateDirectory("Verification");CaptureSceneToTexture("Verification/oblique-forest-preview.png");yield return new WaitForSeconds(1);bool movement=Free(new Vector3(-2,0,-1)),riverBlocked=!Free(new Vector3(RiverX(0),0,0));File.WriteAllText("Verification/oblique-smoke.txt","Runtime started; assets loaded.\nGround movement="+movement+"\nRiver blocked="+riverBlocked+"\nPlants="+plants.Count+"; animals="+animals.Count+"; animated water="+water.Count);var tree=plants[0];player.position=tree.home-Vector3.right;heading=Vector3.right;StrikeTrees();StrikeTrees();StrikeTrees();bool felled=tree.health==0;yield return new WaitForSeconds(.8f);bool stumpOnly=tree.renderer.sprite==tree.stumpGlyph;CaptureSceneToTexture("Verification/oblique-stump.png");File.AppendAllText("Verification/oblique-smoke.txt","\nOnly stump after felling="+stumpOnly);Care();bool replanted=tree.health==3&&tree.targetScale<1;bool startsSmall=Vector3.Distance(tree.art.localScale,tree.initialScale*.35f)<.001f;bool neverShrinks=true;float lastSize=tree.art.localScale.y;for(int frame=0;frame<45;frame++){yield return null;if(tree.art.localScale.y<lastSize-.0001f)neverShrinks=false;lastSize=tree.art.localScale.y;}File.AppendAllText("Verification/oblique-smoke.txt","\nReplant starts small="+startsSmall+"; no shrinking="+neverShrinks);
        var bird=animals[0];interactionTarget=null;player.position=bird.root.position;int beforeAnimals=animals.Count;int animalCareBefore=careCount;Care();bool birthNoCare=careCount==animalCareBefore;var newborn=animals[animals.Count-1];float newbornDistance=Vector3.Distance(bird.root.position,newborn.root.position);bool fed=animals.Count==beforeAnimals+1&&!bird.angry&&newborn.age==0&&newbornDistance<=1.36f;
        var baby=animals[animals.Count-1];int initialCount=animals.Count;float babySize=baby.birth;yield return new WaitForSeconds(2);bool staysYoung=baby.age==0&&Mathf.Abs(baby.birth-babySize)<.01f;
        baby.careReady=0;Feed(baby);bool grows= baby.age==1&&animals.Count==initialCount;bool intermediateNoCare=careCount==animalCareBefore;baby.careReady=0;Feed(baby);bool adult=baby.age==2&&animals.Count==initialCount;bool adultCareOnce=careCount==animalCareBefore+1;baby.careReady=0;Feed(baby);bool cycle=animals.Count==initialCount+1;bool repeatNoCare=careCount==animalCareBefore+1;File.AppendAllText("Verification/oblique-smoke.txt","\nAnimal ecology: birth gives zero="+birthNoCare+"; intermediate growth gives zero="+intermediateNoCare+"; maturity gives one="+adultCareOnce+"; adult breeding gives no extra="+repeatNoCare);if(!birthNoCare||!intermediateNoCare||!adultCareOnce||!repeatNoCare)throw new InvalidOperationException("Animal ecology scoring regression failed");File.AppendAllText("Verification/oblique-smoke.txt","\nInitial population="+beforeAnimals+"; limit="+AnimalPopulationLimit+"; first birth="+fed+"; stays young until fed="+staysYoung+"; grows="+grows+"; adult="+adult+"; breeds again="+cycle+"; population after cycle="+animals.Count);if(!fed||!staysYoung||!grows||!adult||!cycle)throw new InvalidOperationException("Feeding regression failed");
        int beforePlants=plants.Count;tree.careReady=0;GrowAround(tree);bool propagated=plants.Count>beforePlants;bool local=true;for(int i=beforePlants;i<plants.Count;i++)if(Vector3.Distance(tree.home,plants[i].home)>2.01f||River(plants[i].home))local=false;
        bool opaque=true;foreach(var plant in plants)if(plant.tree&&Mathf.Abs(plant.renderer.color.a-plant.inkOpacity)>.001f)opaque=false;
        File.AppendAllText("Verification/oblique-smoke.txt","\nFelling="+felled+"; replanting="+replanted+"; feeding="+fed+"; newborn adjacent="+(newbornDistance<=1.36f)+"; newborn distance="+newbornDistance.ToString("F2")+"; trees opaque="+opaque+"; local propagation="+(propagated&&local));
        while(Balance<FlourishThreshold)RegisterCare();yield return new WaitForSeconds(6);bool sustained=flourishAge>=0;RegisterDamage();yield return new WaitForSeconds(2);bool stopped=flourishAge<0;while(Balance>-8)RegisterDamage();yield return new WaitForSeconds(2);bool negative=ecologyVisual<0;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/oblique-withering-wind.png"));
        int visibleWindGlyphs=0;foreach(var glyph in witheringWindGlyphs)if(glyph!=null&&glyph.gameObject.activeSelf)visibleWindGlyphs++;
        File.AppendAllText("Verification/oblique-smoke.txt","\nPersistent positive="+sustained+"; stops below threshold="+stopped+"; negative feedback="+negative+"; wind glyphs visible="+visibleWindGlyphs+"/"+witheringWindGlyphs.Length);
        File.AppendAllText("Verification/oblique-smoke.txt","; ecology="+ecologyVisual.ToString("0.00")+"; balance="+Balance);
        while(Balance<FlourishThreshold)RegisterCare();
        player.position=ForestSpawn;yield return new WaitForSeconds(2);ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/oblique-after-care.png"));yield return new WaitForSeconds(1);attackAge=0;hit=false;yield return new WaitForSeconds(.2f);ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/oblique-axe-right.png"));bool rightGrip=Vector3.Distance(axe.position,HandPosition())<.001f;yield return new WaitForSeconds(.8f);facing=-1;heading=Vector3.left;attackAge=0;hit=false;yield return new WaitForSeconds(.2f);ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/oblique-axe-left.png"));bool leftGrip=Vector3.Distance(axe.position,HandPosition())<.001f;File.AppendAllText("Verification/oblique-smoke.txt","\nRight grip="+rightGrip+"; left grip="+leftGrip);yield return new WaitForSeconds(1);
        float maxError=0;for(int side=-1;side<=1;side+=2){facing=side;heading=Vector3.right*side;attackAge=0;hit=false;for(int frame=0;frame<35;frame++){yield return null;maxError=Mathf.Max(maxError,Vector3.Distance(axe.TransformPoint(Vector3.zero),body.TransformPoint(PlayerHandLocalPosition())));}}
        File.AppendAllText("Verification/oblique-smoke.txt","\nVisible hand endpoint error across both swings="+maxError);Application.Quit();}
}
}








