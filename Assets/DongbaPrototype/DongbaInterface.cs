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
    Canvas canvas;RectTransform safe,panel;GameObject modal,titleMenu;Text volumeReadout;Slider volumeSlider;Button muteButton;
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
        var root=DongbaUIStyle.Rect("Story title actions",safe,new Vector2(.12f,.30f),new Vector2(0,.5f),Vector2.zero,new Vector2(360,202));titleMenu=root.gameObject;
        DongbaUIStyle.Plate(root,new Color(.96f,.93f,.86f,.94f));
        DongbaUIStyle.Button(root,"Begin story",new Vector2(18,-18),new Vector2(324,48),BeginStory);
        DongbaUIStyle.Button(root,"Settings",new Vector2(18,-76),new Vector2(324,48),()=>Open(Page.Settings));
        DongbaUIStyle.Button(root,"Quit game",new Vector2(18,-134),new Vector2(324,48),()=>Open(Page.Quit));SelectFirst(root.gameObject);
    }
    public void BeginStory(){
        if(loading)return;Close(false);loading=true;startStoryOnLoad=true;consumeThroughFrame=Time.frameCount+2;if(titleMenu!=null)titleMenu.SetActive(false);
        if(titleTransition!=null)titleTransition.BeginTransition();else StartCoroutine(LoadSingle("ObliqueForest"));
    }
    void BuildModal(){
        if(modal!=null){modal.SetActive(false);Destroy(modal);}volumeSlider=null;volumeReadout=null;muteButton=null;
        var root=DongbaUIStyle.Rect("Modal "+page,canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);root.anchorMax=Vector2.one;
        modal=root.gameObject;var shade=modal.AddComponent<Image>();shade.color=new Color(.14f,.11f,.08f,page==Page.Loading?1:.46f);
        var height=page==Page.Help?650:page==Page.Pause?560:page==Page.Settings?530:page==Page.Ending?340:350;
        panel=DongbaUIStyle.Rect("Paper",root,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(620,height));DongbaUIStyle.Plate(panel,DongbaUIStyle.Paper,true);
        string title=page==Page.Pause?"Paused":page==Page.Help?"Controls":page==Page.Settings?"Settings":page==Page.Restart?"Restart the story?":page==Page.ReturnTitle?"Return to the title?":page==Page.Quit?"Quit game?":page==Page.Ending?"Story complete":"Opening the story…";
        var heading=DongbaUIStyle.Label("Heading",panel,title,new Vector2(36,-28),new Vector2(548,46),32);heading.fontStyle=FontStyle.Bold;
        var rule=DongbaUIStyle.Rect("Ink rule",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(36,-87),new Vector2(548,1));DongbaUIStyle.Plate(rule,new Color(.4f,.3f,.2f,.25f));
        if(page==Page.Pause){
            MenuButton("Resume",108,()=>Close());MenuButton("Controls",174,()=>Open(Page.Help));MenuButton("Settings",240,()=>Open(Page.Settings));
            MenuButton("Restart story",306,()=>Open(Page.Restart));MenuButton("Return to title",372,()=>Open(Page.ReturnTitle));MenuButton("Quit game",438,()=>Open(Page.Quit));
        }else if(page==Page.Help){
            bool puzzle=SceneManager.GetActiveScene().name=="Beat_2_Cleansing_Impurity";
            string help=puzzle?"WASD / Arrows   Move\nSPACE   Jump\nJ   Attack\nE   Interact with the nearby object\nESC   Pause / back\nH   Open / close controls\nR   Restart the entire story":"WASD / Arrows   Move\nSPACE   Jump across the river\nJ   Chop / attack\nE   Replant / tend / feed\nENTER   Continue dialogue\nESC   Pause / back\nH   Open / close controls\nR   Restart the entire story";
            var body=DongbaUIStyle.Label("Controls",panel,help,new Vector2(36,-112),new Vector2(548,410),24);body.lineSpacing=1.45f;
            MenuButton("Back",566,Back);
        }else if(page==Page.Settings){
            DongbaUIStyle.Label("Volume label",panel,"Master volume",new Vector2(36,-114),new Vector2(310,36),24);
            volumeReadout=DongbaUIStyle.Label("Volume value",panel,"",new Vector2(405,-114),new Vector2(143,36),22);volumeReadout.alignment=TextAnchor.MiddleRight;
            BuildVolumeSlider();muteButton=MenuButton("Mute",226,()=>{muted=!muted;ApplyVolume();RefreshVolume();});
            var language=MenuButton("Language / 语言",300,()=>{DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese);});
            language.GetComponentInChildren<Text>().text=Choose("Language: English","语言：简体中文");
            var hint=DongbaUIStyle.Label("Audio scope",panel,"Controls all game audio. Settings are saved on this computer.",new Vector2(36,-369),new Vector2(548,57),19);hint.color=DongbaUIStyle.Muted;
            MenuButton("Back",446,Back);RefreshVolume();
        }else if(page==Page.Ending){
            DongbaUIStyle.Label("Ending",panel,"You can stay in the forest and keep exploring.",new Vector2(36,-108),new Vector2(548,64),23);
            MenuButton("Keep exploring",184,()=>Close());MenuButton("Return to title",250,()=>Open(Page.ReturnTitle));
        }else if(page!=Page.Loading){
            string warning=page==Page.Restart?"Return to the opening by the hearth. All progress in this run will be lost.":onTitle?"Close the game?":"There is no saved progress. Leaving will end this run.";
            DongbaUIStyle.Label("Consequence",panel,warning,new Vector2(36,-112),new Vector2(548,104),23);
            DongbaUIStyle.Button(panel,"Cancel",new Vector2(36,-255),new Vector2(264,54),Back);
            DongbaUIStyle.Button(panel,"Confirm",new Vector2(320,-255),new Vector2(264,54),Confirm);
        }
        EnsureEventSystem();SelectFirst(modal);
    }
    Button MenuButton(string text,float y,UnityEngine.Events.UnityAction action)=>DongbaUIStyle.Button(panel,text,new Vector2(36,-y),new Vector2(548,52),action);
    void BuildVolumeSlider(){
        var rect=DongbaUIStyle.Rect("Volume slider",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(36,-167),new Vector2(548,34));
        var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        volumeSlider=rect.gameObject.AddComponent<Slider>();var track=DongbaUIStyle.Rect("Track",rect,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(520,6));DongbaUIStyle.Plate(track,new Color(.5f,.4f,.3f,.35f));
        var slideArea=DongbaUIStyle.Rect("Handle area",rect,Vector2.zero,Vector2.zero,new Vector2(12,0),new Vector2(-24,0));slideArea.anchorMax=Vector2.one;
        var handle=DongbaUIStyle.Rect("Handle",slideArea,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(24,28));var image=DongbaUIStyle.Plate(handle,DongbaUIStyle.Accent,true);
        volumeSlider.handleRect=handle;volumeSlider.targetGraphic=image;volumeSlider.minValue=0;volumeSlider.maxValue=1;volumeSlider.SetValueWithoutNotify(masterVolume);
        volumeSlider.onValueChanged.AddListener(value=>{masterVolume=value;ApplyVolume();RefreshVolume();});
    }
    void ApplyVolume(){AudioListener.volume=muted?0:masterVolume;PlayerPrefs.SetFloat(VolumeKey,masterVolume);PlayerPrefs.SetInt(MuteKey,muted?1:0);}
    void RefreshVolume(){if(volumeReadout!=null)volumeReadout.text=muted||masterVolume<=0?Choose("Muted","已静音"):Mathf.RoundToInt(masterVolume*100)+"%";if(muteButton!=null)muteButton.GetComponentInChildren<Text>().text=muted?"Unmute":"Mute";}
    void RefreshLanguage(){if(page!=Page.None)BuildModal();}
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
