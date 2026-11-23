using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DongbaPrototype {
// A single owner arbitrates menu input across the forest, reel and additive puzzle.
[DefaultExecutionOrder(-9000)]
public sealed partial class DongbaInterface : MonoBehaviour {
    public enum Page {None,Pause,Help,Settings,Restart,ReturnTitle,Quit,Ending,Loading}
    public static DongbaInterface Instance {get;private set;}
    public static bool GameplayBlocked=>Instance!=null&&(Instance.page!=Page.None||Instance.onTitle||Time.frameCount<=Instance.consumeThroughFrame);
    public bool ModalOpen=>page!=Page.None;
    public Page CurrentPage=>page;
    public static string Choose(string en,string zh)=>DongbaLanguage.Chinese?zh:en;
    Canvas canvas;RectTransform safe,panel,menuPage;GameObject modal,titleMenu;Text volumeReadout;Slider volumeSlider;Button muteButton;
    Page page,returnPage;bool onTitle,muted,loading,startStoryOnLoad;int consumeThroughFrame=-1;float savedTimeScale=1,masterVolume=1;
    ObliqueForest forest;InkBloomTransition titleTransition;EventSystem ownedEventSystem;
    const string VolumeKey="Dongba.Audio.Master",MuteKey="Dongba.Audio.Muted";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic(){Instance=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Instance==null)new GameObject("Dongba interface / menus and input owner").AddComponent<DongbaInterface>();}
    void Awake(){
        if(Instance!=null){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
        masterVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));muted=PlayerPrefs.GetInt(MuteKey,0)!=0;ApplyVolume();
        var root=new GameObject("Paper interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32700;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        safe=DongbaUIStyle.Rect("Safe area",root.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);safe.anchorMax=Vector2.one;
        SceneManager.sceneLoaded+=Loaded;DongbaLanguage.Changed+=RefreshLanguage;StartCoroutine(BindScene(SceneManager.GetActiveScene()));
    }
    void Loaded(Scene scene,LoadSceneMode mode){if(mode==LoadSceneMode.Single)StartCoroutine(BindScene(scene));}
    IEnumerator BindScene(Scene scene){
        yield return null;
        onTitle=scene.name=="Title";loading=false;forest=null;Close(false);EnsureEventSystem();
        if(titleMenu!=null)Destroy(titleMenu);
        if(onTitle){
            titleTransition=FindFirstObjectByType<InkBloomTransition>();
            if(titleTransition!=null){var old=titleTransition.GetComponent<Button>();if(old!=null){old.interactable=false;foreach(var graphic in old.GetComponentsInChildren<Graphic>())graphic.enabled=false;}}
            BuildTitleMenu();
        }else if(scene.name=="ObliqueForest"){
            forest=FindFirstObjectByType<ObliqueForest>();
            if(startStoryOnLoad&&forest!=null){startStoryOnLoad=false;forest.RestartFromInterface();}
        }
    }
    void EnsureEventSystem(){
        if(EventSystem.current!=null){if(ownedEventSystem!=null&&EventSystem.current!=ownedEventSystem)ownedEventSystem.gameObject.SetActive(false);return;}
        if(ownedEventSystem==null){var root=new GameObject("Menu EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));root.transform.SetParent(transform,false);ownedEventSystem=root.GetComponent<EventSystem>();}
        ownedEventSystem.gameObject.SetActive(true);
    }
    public void AttachForest(ObliqueForest value){forest=value;onTitle=false;}
    void Update(){
        var area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
        if(panel!=null){float scale=Mathf.Min(1,(safe.rect.width-32)/panel.sizeDelta.x,(safe.rect.height-32)/panel.sizeDelta.y);panel.localScale=Vector3.one*Mathf.Max(.5f,scale);}
        if(loading)return;
        var kb=Keyboard.current;if(kb==null)return;
        if(kb.escapeKey.wasPressedThisFrame){
            if(page!=Page.None)Back();else if(onTitle)Open(Page.Quit);else Open(Page.Pause);return;
        }
        if(page!=Page.None){if(page==Page.Help&&kb.hKey.wasPressedThisFrame)Back();return;}
        if(onTitle)return;
        if(kb.rKey.wasPressedThisFrame){Open(Page.Restart);return;}
        if(kb.hKey.wasPressedThisFrame)Open(Page.Help);
    }
    void LateUpdate(){
        DongbaLanguageSwitch.Instance?.SetPresentationBlocked(this,true); // Language lives in Settings.
        if(forest!=null&&forest.isActiveAndEnabled&&forest.InterfaceEndingReady&&page==Page.None&&!loading){forest.AcknowledgeEndingInterface();Open(Page.Ending);}
    }
    public void Open(Page next){
        if(next==Page.None){Close();return;}if(loading&&next!=Page.Loading)return;
        if(page==Page.None){savedTimeScale=Time.timeScale;Time.timeScale=0;AudioListener.pause=true;}
        returnPage=page;page=next;consumeThroughFrame=Time.frameCount+1;BuildModal();
        if(titleMenu!=null)titleMenu.SetActive(false);
    }
    public void Close(bool focus=true){
        if(page!=Page.None){Time.timeScale=savedTimeScale;AudioListener.pause=false;}
        page=Page.None;if(modal!=null){modal.SetActive(false);Destroy(modal);}panel=null;
        consumeThroughFrame=Time.frameCount+1;if(titleMenu!=null)titleMenu.SetActive(onTitle&&!loading);
        if(focus&&onTitle&&titleMenu!=null)SelectFirst(titleMenu);else EventSystem.current?.SetSelectedGameObject(null);
    }
    public void Back(){
        if(page==Page.Loading)return;
        if(page==Page.Ending||page==Page.Pause){Close();return;}
        var next=returnPage;
        if(next==Page.None){Close();return;}
        if(next==Page.Settings||next==Page.Help||next==Page.Restart||next==Page.ReturnTitle||next==Page.Quit)next=onTitle?Page.None:Page.Pause;
        if(next==Page.None)Close();else{page=next;returnPage=Page.None;BuildModal();}
    }
    void BuildTitleMenu(){
        var root=DongbaUIStyle.Rect("Story title actions",safe,new Vector2(.075f,.33f),new Vector2(0,.5f),Vector2.zero,new Vector2(340,250));titleMenu=root.gameObject;
        var intro=DongbaUIStyle.Label("Story title caption",root,Choose("A NAXI TALE","纳西故事"),new Vector2(10,-2),new Vector2(320,28),16);intro.color=new Color(.92f,.86f,.73f);
        string[] names={"Begin story","Settings","Quit game"};
        for(int i=0;i<3;i++){
            var leaf=DongbaUIStyle.Rect("Cut title leaf",root,new Vector2(0,1),new Vector2(0,1),new Vector2(i*5,-40-i*68),new Vector2(330-i*26,58));DongbaUIStyle.EdgeShadow(DongbaUIStyle.Plate(leaf,Color.white));
            int action=i;DongbaUIStyle.Button(leaf,names[i],new Vector2(10,-3),new Vector2(306-i*26,50),()=>{if(action==0)BeginStory();else Open(action==1?Page.Settings:Page.Quit);});
        }
        SelectFirst(root.gameObject);
    }
    public void BeginStory(){
        if(loading)return;Close(false);loading=true;startStoryOnLoad=true;consumeThroughFrame=Time.frameCount+2;if(titleMenu!=null)titleMenu.SetActive(false);
        if(titleTransition!=null)titleTransition.BeginTransition();else StartCoroutine(LoadSingle("ObliqueForest"));
    }
    void BuildModal(){
        if(modal!=null){modal.SetActive(false);Destroy(modal);}volumeSlider=null;volumeReadout=null;muteButton=null;
        var root=DongbaUIStyle.Rect("Modal "+page,canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);root.anchorMax=Vector2.one;
        modal=root.gameObject;var shade=modal.AddComponent<Image>();shade.color=new Color(.12f,.095f,.065f,page==Page.Loading?1:.62f);
        panel=DongbaUIStyle.Rect("Open manuscript",root,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(970,660));
        var under=DongbaUIStyle.Rect("Underlying leaf",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(6,-7),new Vector2(962,650));var underside=DongbaUIStyle.Plate(under,new Color(.84f,.80f,.69f));DongbaUIStyle.EdgeShadow(underside,.25f);
        var left=DongbaUIStyle.Rect("Story leaf",panel,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(348,652));DongbaUIStyle.Plate(left,new Color(.94f,.91f,.83f));
        menuPage=DongbaUIStyle.Rect("Menu leaf",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(340,0),new Vector2(630,656));DongbaUIStyle.Plate(menuPage,Color.white,true);
        DongbaUIStyle.Stroke(left,new Vector2(329,-25),new Vector2(2,598),new Color(.27f,.18f,.10f,.2f));
        BuildStoryLeaf(left);
        var small=DongbaUIStyle.Label("Page caption",menuPage,"DONGBA  /  THE LIVING SCRIPT",new Vector2(38,-27),new Vector2(550,24),13);small.color=DongbaUIStyle.Muted;
        string title=page==Page.Pause?"Paused":page==Page.Help?"Controls":page==Page.Settings?"Settings":page==Page.Restart?"Restart the story?":page==Page.ReturnTitle?"Return to the title?":page==Page.Quit?"Quit game?":page==Page.Ending?"Story complete":"Opening the story…";
        var heading=DongbaUIStyle.Label("Heading",menuPage,title,new Vector2(36,-57),new Vector2(554,66),40);DongbaUIStyle.Serif(heading);
        DongbaUIStyle.Stroke(menuPage,new Vector2(36,-128),new Vector2(550,2),new Color(.33f,.23f,.13f,.55f));
        if(page==Page.Pause){
            MenuButton("Resume",151,()=>Close(),1);MenuButton("Controls",215,()=>Open(Page.Help),2);MenuButton("Settings",279,()=>Open(Page.Settings),3);
            MenuButton("Restart story",343,()=>Open(Page.Restart),4);MenuButton("Return to title",407,()=>Open(Page.ReturnTitle),5);MenuButton("Quit game",471,()=>Open(Page.Quit),6);
            var foot=DongbaUIStyle.Label("Return key",menuPage,Choose("ESC  ·  Return to the forest","ESC  ·  回到故事"),new Vector2(54,-588),new Vector2(510,26),16);foot.color=DongbaUIStyle.Muted;
        }else if(page==Page.Help){
            bool puzzle=SceneManager.GetActiveScene().name=="Beat_2_Cleansing_Impurity";
            string[] keys={"WASD","SPACE","J","E","ENTER","TAB","F8","ESC","H","R"};
            string[] en={"Move / Arrow keys","Jump",puzzle?"Attack":"Chop / attack",puzzle?"Interact with nearby objects":"Tend / replant / feed","Continue dialogue",puzzle?"—":"Expand / fold chapter note","Switch Chinese / English","Pause / back","Controls","Restart the story"};
            string[] zh={"移动 / 方向键","跳跃",puzzle?"攻击":"砍伐 / 攻击",puzzle?"与附近对象交互":"照料 / 补种 / 喂养","继续对白",puzzle?"—":"展开 / 收起章签","切换中英文","暂停 / 返回","操作说明","重新开始故事"};
            int row=0;for(int i=0;i<keys.Length;i++){
                if(puzzle&&keys[i]=="TAB")continue;
                var badge=DongbaUIStyle.Rect("Key "+keys[i],menuPage,new Vector2(0,1),new Vector2(0,1),new Vector2(40,-153-row*39),new Vector2(86,34));DongbaUIStyle.Plate(badge,new Color(.88f,.85f,.77f));
                var key=DongbaUIStyle.Label("Key text",badge,keys[i],new Vector2(4,-4),new Vector2(78,26),16);key.alignment=TextAnchor.MiddleCenter;key.color=DongbaUIStyle.Accent;
                DongbaUIStyle.Label("Control action",menuPage,Choose(en[i],zh[i]),new Vector2(146,-156-row*39),new Vector2(436,34),22);row++;
            }
            MenuButton("Back",566,Back);
        }else if(page==Page.Settings){
            DongbaUIStyle.Label("Volume label",menuPage,"Master volume",new Vector2(40,-161),new Vector2(310,36),24);
            volumeReadout=DongbaUIStyle.Label("Volume value",menuPage,"",new Vector2(405,-161),new Vector2(143,36),22);volumeReadout.alignment=TextAnchor.MiddleRight;
            BuildVolumeSlider();muteButton=MenuButton("Mute",280,()=>{muted=!muted;ApplyVolume();RefreshVolume();});
            var language=MenuButton("Language / 语言",356,()=>DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese));language.GetComponentInChildren<Text>().text=Choose("Language: English","语言：简体中文");
            var hint=DongbaUIStyle.Label("Audio scope",menuPage,"Controls all game audio. Settings are saved on this computer.",new Vector2(54,-441),new Vector2(510,74),18);hint.color=DongbaUIStyle.Muted;
            MenuButton("Back",566,Back);RefreshVolume();
        }else if(page==Page.Ending){
            DongbaUIStyle.Label("Ending",menuPage,"You can stay in the forest and keep exploring.",new Vector2(40,-163),new Vector2(540,105),26);
            MenuButton("Keep exploring",358,()=>Close(),1);MenuButton("Return to title",438,()=>Open(Page.ReturnTitle),2);
        }else if(page!=Page.Loading){
            string warning=page==Page.Restart?"Return to the opening by the hearth. All progress in this run will be lost.":onTitle?"Close the game?":"There is no saved progress. Leaving will end this run.";
            DongbaUIStyle.Label("Consequence",menuPage,warning,new Vector2(40,-174),new Vector2(540,160),25);
            MenuButton("Cancel",389,Back,1);MenuButton("Confirm",465,Confirm,2);
        }
        EnsureEventSystem();SelectFirst(modal);
    }
    void BuildStoryLeaf(RectTransform leaf){
        string chapter=forest!=null?forest.InterfaceChapter:Choose("THE HUNT","入林");
        var tag=DongbaUIStyle.Rect("Chapter ribbon",leaf,new Vector2(0,1),new Vector2(0,1),new Vector2(34,8),new Vector2(48,100));DongbaUIStyle.Plate(tag,DongbaUIStyle.Accent);
        var numeral=DongbaUIStyle.Label("Chapter number",tag,forest!=null?forest.InterfaceChapterNumber:"I",new Vector2(4,-26),new Vector2(40,44),28);numeral.color=new Color(.98f,.94f,.82f);numeral.alignment=TextAnchor.MiddleCenter;DongbaUIStyle.Serif(numeral);
        var part=DongbaUIStyle.Label("Current chapter",leaf,chapter,new Vector2(102,-40),new Vector2(212,35),18);part.color=DongbaUIStyle.Accent;
        var name=DongbaUIStyle.Label("Tale title",leaf,Choose("The Father\nand Son","父与子"),new Vector2(38,-126),new Vector2(278,103),32);DongbaUIStyle.Serif(name);
        DongbaUIStyle.Picture(leaf,"Gaolequ / actual game glyph",DongbaUIStyle.Glyph("player_glyph"),new Vector2(55,-262),new Vector2(180,202));
        if(forest==null||forest.InterfaceFatherPresent)DongbaUIStyle.Picture(leaf,"Father / actual game glyph",forest!=null?forest.InterfaceFatherSprite:DongbaUIStyle.Glyph("father_glyph"),new Vector2(204,-327),new Vector2(78,133));
        DongbaUIStyle.Stroke(leaf,new Vector2(48,-483),new Vector2(245,2),new Color(.3f,.2f,.1f,.42f));
        DongbaUIStyle.Picture(leaf,"Hearth / existing glyph",DongbaUIStyle.Glyph("hearth_glyph"),new Vector2(129,-505),new Vector2(68,42));
        var caption=DongbaUIStyle.Label("Cultural context",leaf,Choose("A NAXI TALE","纳西故事"),new Vector2(38,-585),new Vector2(275,30),15);caption.alignment=TextAnchor.MiddleCenter;caption.color=DongbaUIStyle.Muted;
    }
    Button MenuButton(string text,float y,UnityEngine.Events.UnityAction action,int number=0)=>DongbaUIStyle.Button(menuPage,text,new Vector2(36,-y),new Vector2(548,52),action,number);
    void BuildVolumeSlider(){
        var rect=DongbaUIStyle.Rect("Volume slider",menuPage,new Vector2(0,1),new Vector2(0,1),new Vector2(40,-218),new Vector2(548,34));
        var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        volumeSlider=rect.gameObject.AddComponent<Slider>();var track=DongbaUIStyle.Rect("Track",rect,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(520,6));DongbaUIStyle.Plate(track,new Color(.5f,.4f,.3f,.35f));
        var slideArea=DongbaUIStyle.Rect("Handle area",rect,Vector2.zero,Vector2.zero,new Vector2(12,0),new Vector2(-24,0));slideArea.anchorMax=Vector2.one;
        var handle=DongbaUIStyle.Rect("Handle",slideArea,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(22,25));var image=DongbaUIStyle.Plate(handle,DongbaUIStyle.Accent,true);
        volumeSlider.handleRect=handle;volumeSlider.targetGraphic=image;volumeSlider.minValue=0;volumeSlider.maxValue=1;volumeSlider.SetValueWithoutNotify(masterVolume);
        volumeSlider.onValueChanged.AddListener(value=>{masterVolume=value;ApplyVolume();RefreshVolume();});
    }
    void ApplyVolume(){AudioListener.volume=muted?0:masterVolume;PlayerPrefs.SetFloat(VolumeKey,masterVolume);PlayerPrefs.SetInt(MuteKey,muted?1:0);}
    void RefreshVolume(){if(volumeReadout!=null)volumeReadout.text=muted||masterVolume<=0?Choose("Muted","已静音"):Mathf.RoundToInt(masterVolume*100)+"%";if(muteButton!=null)muteButton.GetComponentInChildren<Text>().text=muted?"Unmute":"Mute";}
    void RefreshLanguage(){
        string selected=EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null?EventSystem.current.currentSelectedGameObject.name:null;
        if(page!=Page.None)BuildModal();
        if(modal!=null&&selected!=null)foreach(var item in modal.GetComponentsInChildren<Selectable>())if(item.name==selected){EventSystem.current?.SetSelectedGameObject(item.gameObject);break;}
    }
    void SelectFirst(GameObject root){var first=root.GetComponentInChildren<Selectable>();if(first!=null&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);}
    public void Confirm(){if(page==Page.Quit){PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
        return;}if(page==Page.Restart||page==Page.ReturnTitle)StartCoroutine(Navigate(page==Page.Restart));}
    IEnumerator Navigate(bool restart){
        Close(false);loading=true;Open(Page.Loading);Time.timeScale=1;AudioListener.pause=false;
        var host=FindFirstObjectByType<ReelPortalHost>();
        if(host!=null){yield return host.LeaveForInterface();Destroy(host.gameObject);}
        if(restart&&forest!=null&&forest.isActiveAndEnabled){forest.RestartFromInterface();loading=false;Close();Time.timeScale=1;yield break;}
        startStoryOnLoad=restart;yield return LoadSingle(restart?"ObliqueForest":"Title");
    }
    IEnumerator LoadSingle(string name){
        // Title owns an EventSystem; release our forest fallback before its OnEnable.
        if(ownedEventSystem!=null)ownedEventSystem.gameObject.SetActive(false);
        var operation=SceneManager.LoadSceneAsync(name);while(operation!=null&&!operation.isDone)yield return null;
    }
    void OnApplicationQuit(){PlayerPrefs.Save();}
    void OnDestroy(){if(Instance!=this)return;SceneManager.sceneLoaded-=Loaded;DongbaLanguage.Changed-=RefreshLanguage;Time.timeScale=1;AudioListener.pause=false;Instance=null;}
}
}
