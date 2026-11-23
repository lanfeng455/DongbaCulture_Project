using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DongbaPrototype {
public sealed partial class DongbaLanguageSwitch : MonoBehaviour {
    public static DongbaLanguageSwitch Instance {get;private set;}
    public Canvas SwitchCanvas {get;private set;}
    public RectTransform HitRect {get;private set;}
    TMP_Text label;
    readonly HashSet<Behaviour> presentationBlocks=new HashSet<Behaviour>();
    public void SetPresentationBlocked(Behaviour owner,bool blocked){
        if(owner==null)return;
        if(blocked)presentationBlocks.Add(owner);else presentationBlocks.Remove(owner);
        RefreshVisibility();
    }
    void RefreshVisibility(){
        presentationBlocks.RemoveWhere(owner=>owner==null||!owner.isActiveAndEnabled);
        SwitchCanvas.enabled=presentationBlocks.Count==0;
    }
    void LateUpdate(){RefreshVisibility();}
    void Awake(){
        if(Instance!=null){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
        var canvasObject=new GameObject("Language switch",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasObject.transform.SetParent(transform,false);
        SwitchCanvas=canvasObject.GetComponent<Canvas>();SwitchCanvas.renderMode=RenderMode.ScreenSpaceOverlay;SwitchCanvas.sortingOrder=32000;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var plate=new GameObject("Click to switch Chinese / English",typeof(RectTransform),typeof(Image));plate.transform.SetParent(canvasObject.transform,false);
        HitRect=(RectTransform)plate.transform;HitRect.anchorMin=HitRect.anchorMax=HitRect.pivot=new Vector2(1,1);HitRect.anchoredPosition=new Vector2(-24,-78);HitRect.sizeDelta=new Vector2(190,36);
        var image=plate.GetComponent<Image>();image.color=new Color(.96f,.925f,.85f,.96f);image.raycastTarget=false;
        var text=new GameObject("Language choice",typeof(RectTransform));text.transform.SetParent(plate.transform,false);label=text.AddComponent<TextMeshProUGUI>();label.font=DongbaLanguage.ChineseSubtitleFont;label.fontSize=18;label.alignment=TextAlignmentOptions.Center;label.color=new Color(.28f,.20f,.12f);label.raycastTarget=false;
        var rect=label.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        DongbaLanguage.Changed+=Refresh;SceneManager.sceneLoaded+=BindScene;Refresh();
    }
    IEnumerator Start(){
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--language-transition-test")>=0)yield return CheckLanguagePresentation();
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--language-check")>=0){
            ObliqueForest forest=null;while(forest==null){forest=FindFirstObjectByType<ObliqueForest>();yield return null;}
            yield return forest.CheckLanguageSwitching();
        }
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--language-title-test")>=0){
            yield return new WaitForSeconds(8);System.IO.Directory.CreateDirectory("Verification/language");
            var prior=DongbaLanguage.Current;DongbaLanguage.Set(DongbaLocale.Chinese,false);yield return null;yield return new WaitForEndOfFrame();DongbaLanguageCapture.Write(Camera.main,"Verification/language/title-zh.png");
            ScreenCapture.CaptureScreenshot("Verification/language/title-backbuffer.png");
            bool zh=System.Array.Exists(FindObjectsByType<TMP_Text>(FindObjectsSortMode.None),t=>t.text=="开始");
            DongbaLanguage.Set(DongbaLocale.English,false);yield return null;yield return new WaitForEndOfFrame();DongbaLanguageCapture.Write(Camera.main,"Verification/language/title-en.png");bool en=System.Array.Exists(FindObjectsByType<TMP_Text>(FindObjectsSortMode.None),t=>t.text=="START");System.IO.File.WriteAllText("Verification/language/title-checks.txt","Chinese start button="+zh+"\nEnglish start button="+en+"\n");DongbaLanguage.Set(prior,false);Application.Quit(zh&&en?0:1);
        }
    }
    void OnDestroy(){if(Instance!=this)return;DongbaLanguage.Changed-=Refresh;SceneManager.sceneLoaded-=BindScene;Instance=null;}
    void Refresh(){label.text=DongbaLanguage.Chinese?"<b>中文</b> / EN   · F8":"中文 / <b>EN</b>   · F8";}
    void Update(){
        if(!DongbaInterface.GameplayBlocked&&DialogueAdvanceInput.GameplayInputBlocked)return;
        bool pressed=Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame;
        var mouse=Mouse.current;
        if(SwitchCanvas.enabled&&mouse!=null&&mouse.leftButton.wasPressedThisFrame&&RectTransformUtility.RectangleContainsScreenPoint(HitRect,mouse.position.ReadValue(),null))pressed=true;
        if(pressed)DongbaLanguage.Set(DongbaLanguage.Chinese?DongbaLocale.English:DongbaLocale.Chinese);
    }
    void BindScene(Scene scene,LoadSceneMode mode){foreach(var root in scene.GetRootGameObjects())BindRoot(root);StartCoroutine(BindAfterStart(scene));}
    IEnumerator BindAfterStart(Scene scene){yield return null;if(scene.IsValid()&&scene.isLoaded)foreach(var root in scene.GetRootGameObjects())BindRoot(root);}
    static void BindRoot(GameObject root){
        foreach(var text in root.GetComponentsInChildren<Text>(true))DongbaLocalizedLabel.Bind(text);
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(text.name!="DialogueText")DongbaLocalizedLabel.Bind(text);
    }
}
}
