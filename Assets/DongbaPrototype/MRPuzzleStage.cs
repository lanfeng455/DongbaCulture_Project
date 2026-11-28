using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DongbaPrototype {
// Native physics remain untouched. Renderer-only mirrors make a real spatial tabletop;
// the authored camera directs the content window, never the tracked head pose.
[DefaultExecutionOrder(-9000)]
public sealed partial class MRPuzzleStage:MonoBehaviour {
    public Camera ViewCamera {get;private set;}
    public Canvas Surface {get;private set;}
    public bool Ready;
    public ObliqueForest Source;
    Scene scene;Camera eyes;float units;CanvasGroup group;
    readonly List<GameObject> roots=new();readonly List<Renderer> renderers=new();readonly List<Canvas> canvases=new();readonly List<Transform> children=new();
    readonly List<Text> sceneLabels=new();
    readonly Dictionary<Renderer,bool> originalVisibility=new();readonly HashSet<Canvas> adapted=new();
    string eyeTag;bool released;RectTransform helpBook;Text helpTab;bool lastChinese;
    readonly List<Text> helpLabels=new();
    public void Initialize(Scene target,Camera native,Camera headset,Vector3 centre,float width,float worldUnits){
        scene=target;ViewCamera=native;eyes=headset;units=worldUnits;eyeTag=eyes.tag;eyes.tag="Untagged";
        ViewCamera.enabled=false;ViewCamera.targetTexture=null;
        ViewCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        ViewCamera.aspect=16f/9;
        foreach(var root in scene.GetRootGameObjects())foreach(var audio in root.GetComponentsInChildren<AudioListener>(true))audio.enabled=false;
        var go=new GameObject("Fixed MR puzzle paper",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));go.transform.SetParent(transform,false);go.layer=30;
        Surface=go.GetComponent<Canvas>();Surface.renderMode=RenderMode.WorldSpace;Surface.worldCamera=eyes;Surface.sortingOrder=29500;
        var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1600,900);rect.localScale=Vector3.one*(width/1600);rect.SetPositionAndRotation(centre,Quaternion.identity);
        group=go.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;
        InitializeGeometry();gameObject.AddComponent<MRPuzzleVisualSync>().Stage=this;
        BuildControls(rect);MRPuzzleInput.Begin();RefreshPresentation();
    }
    public void SetAlpha(float value){stageAlpha=value;if(group!=null)group.alpha=value;}
    void Update(){
        MRPuzzleInput.Poll(Ready&&(Source==null||Source.XRExperienceReady));
        if(Surface!=null)Surface.enabled=Source==null||Source.XRExperienceReady;
        if(MRPuzzleInput.HelpPressed){MRPuzzleInput.MenuOpen=!MRPuzzleInput.MenuOpen;helpBook.gameObject.SetActive(MRPuzzleInput.MenuOpen);}
        if(lastChinese!=DongbaLanguage.Chinese)RefreshControls();
    }
    void BuildControls(RectTransform page){
        var tab=DongbaUIStyle.Rect("MR controls bookmark",page,new Vector2(1,0),new Vector2(1,0),new Vector2(-18,14),new Vector2(270,70));DongbaUIStyle.Plate(tab,Color.white);
        helpTab=DongbaUIStyle.Label("MR controller guide",tab,"",new Vector2(16,-7),new Vector2(240,58),27);
        helpBook=DongbaUIStyle.OpenManuscript(page,"Puzzle controls manuscript",Vector2.one*.5f,Vector2.zero,out var left,out var right);
        helpLabels.Add(DongbaUIStyle.Label("Journey",left,"",new Vector2(30,-44),new Vector2(286,120),36));
        helpLabels.Add(DongbaUIStyle.Label("Paper stage",left,"",new Vector2(30,-220),new Vector2(286,250),28));
        helpLabels.Add(DongbaUIStyle.Label("Controls",right,"",new Vector2(38,-38),new Vector2(550,54),36));
        for(int i=0;i<6;i++)helpLabels.Add(DongbaUIStyle.Label("Controller row "+i,right,"",new Vector2(38,-125-i*70),new Vector2(550,60),28));
        page.GetComponentsInChildren(true,children);foreach(var t in children)t.gameObject.layer=30;
        foreach(var text in page.GetComponentsInChildren<Text>(true))DongbaLocalizedLabel.SetReadingFont(text,Resources.Load<Font>("Fonts/NotoSansSC-Regular")??DongbaLanguage.ChineseUIFont);
        RefreshControls();helpBook.gameObject.SetActive(false);
    }
    void RefreshControls(){
        lastChinese=DongbaLanguage.Chinese;helpTab.text=lastChinese?"B   操作说明":"B   Controls";
        string[] text=lastChinese?new[]{"救父之旅","卷轴展开为立体纸上场景。\n\n用左摇杆控制高勒趣。\n\nB  合上手册","操作说明","左摇杆      移动／推动","A               跳跃／继续对话","X               与身边对象互动","右扳机      挥斧","按右摇杆  切换中英文","按住 Y      重新放置舞台"}:new[]{"The rescue journey","The reel opens into a spatial paper world.\n\nMove Gaolequ with the left stick.\n\nB   Close book","Controls","Left stick     Move / push","A                   Jump / dialogue","X                   Interact nearby","Right trigger   Axe","Right stick click   Language","Hold Y           Reposition"};
        for(int i=0;i<helpLabels.Count;i++)helpLabels[i].text=text[i];
    }
    internal void SynchronizeVisuals(){RefreshPresentation();UpdateGeometry();}
    void RefreshPresentation(){
        if(!scene.IsValid()||!scene.isLoaded)return;
        scene.GetRootGameObjects(roots);
        foreach(var root in roots){
            root.GetComponentsInChildren(true,renderers);
            foreach(var r in renderers)if(!originalVisibility.ContainsKey(r)){originalVisibility.Add(r,r.forceRenderingOff);r.forceRenderingOff=true;RegisterGeometry(r);}
            root.GetComponentsInChildren(true,canvases);
            foreach(var c in canvases){
                if(c.name=="ScrollUnrollCanvas"){c.enabled=false;continue;}
                if(c==Surface||adapted.Contains(c)||c.transform.IsChildOf(eyes.transform)||c.transform.IsChildOf(Surface.transform))continue;
                // Scene clues use the same fixed far-side sheet as forest UI.
                if(c.GetComponentInParent<DialogueDisplay>()!=null)continue;
                c.renderMode=RenderMode.WorldSpace;c.worldCamera=eyes;
                var scaler=c.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
                var rect=(RectTransform)c.transform;
                rect.SetParent(Surface.transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
                rect.sizeDelta=new Vector2(1600,900);rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
                c.GetComponentsInChildren(true,children);foreach(var t in children)t.gameObject.layer=30;
                adapted.Add(c);
            }
        }
        foreach(var c in adapted){
            if(c==null)continue;
            if(c.name=="Forest HUD / fixed safe margins")c.enabled=false;
            if(c.name=="LocationLabelHUD"){
                c.GetComponentsInChildren(true,sceneLabels);bool hasText=false;
                foreach(var label in sceneLabels)hasText|=!string.IsNullOrWhiteSpace(label.text);
                c.enabled=hasText;
            }
        }
        // Most scene dialogue canvases are created outside their director hierarchy.
        var dialogue=DialogueDisplay.Instance;
        if(dialogue!=null&&dialogue.gameObject.scene==scene){
            dialogue.ConfigureHeadsetPresentation(eyes,units);dialogue.SetHeadsetPresentationVisible(Ready&&!MRPuzzleInput.MenuOpen&&(Source==null||Source.XRExperienceReady));
            var text=dialogue.SubtitleText;if(text!=null){var canvas=text.GetComponentInParent<Canvas>();canvas.GetComponentsInChildren(true,children);foreach(var t in children)t.gameObject.layer=30;}
        }
        eyes.cullingMask&=~(1<<29);
    }
    public void Release(){
        if(released)return;released=true;Ready=false;MRPuzzleInput.Reset();ReleaseGeometry();
        foreach(var pair in originalVisibility)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;originalVisibility.Clear();
        if(eyes!=null)eyes.tag=eyeTag??"MainCamera";
        if(ViewCamera!=null){ViewCamera.enabled=false;ViewCamera.targetTexture=null;}
    }
    void OnDestroy(){Release();}
}}
