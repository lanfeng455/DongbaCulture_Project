using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    readonly Dictionary<Renderer,bool> reelHiddenRenderers=new();
    Button storySkipButton;Text storySkipLabel;float skipConfirmUntil,skipHold;bool skipBReleased=true;
    public bool CanSkipToReel=>StoryModeEnabled&&enabled&&!XRPuzzleActive&&journeyStage>=JourneyStage.Hunting&&journeyStage<=JourneyStage.EastJourney&&!ReelPresentationVisible&&(reelPortalHost==null||!reelPortalHost.IsBusy);
    public void SkipStoryToReel(){if(!CanSkipToReel)return;xrHelpOpen=false;skipHold=skipConfirmUntil=0;JumpToDeveloperReel();}
    void UpdateStorySkip(){
        if(forestCanvas==null)return;
        if(storySkipButton==null){
            var rect=UIBlock("PICO Skip story",forestCanvas.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-26,-88),new Vector2(330,52));
            var paper=QuietPaper(rect,.97f);storySkipButton=rect.gameObject.AddComponent<Button>();storySkipButton.targetGraphic=paper;
            storySkipLabel=UILabel("Skip story label",rect,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(318,48),20,"");storySkipLabel.alignment=TextAnchor.MiddleCenter;
            storySkipButton.onClick.AddListener(()=>{if(Time.unscaledTime<skipConfirmUntil)SkipStoryToReel();else skipConfirmUntil=Time.unscaledTime+4;});
        }
        bool show=CanSkipToReel&&(!XRTrackingControlsCamera||XRExperienceReady);
        storySkipButton.gameObject.SetActive(show);if(!show)return;
        if(XRTrackingControlsCamera){
            var rect=(RectTransform)storySkipButton.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-24,8);rect.sizeDelta=new Vector2(440,60);
            storySkipLabel.rectTransform.sizeDelta=new Vector2(426,56);XRReadingText(storySkipLabel,26);
            storySkipLabel.text=N("Hold B · Skip to scroll","长按 B · 跳过剧情至卷轴")+(skipHold>0?"  "+Mathf.RoundToInt(skipHold/1.3f*100)+"%":"");
        }else storySkipLabel.text=Time.unscaledTime<skipConfirmUntil?N("Confirm · Go to scroll","确认跳至卷轴（再点一次）"):N("Skip story → Scroll","跳过前置剧情 → 卷轴");
    }
    void ReadStorySkipHold(bool held,bool ready){
        if(!ready||!CanSkipToReel){skipHold=0;skipBReleased=!held;return;}
        if(!held){skipHold=0;skipBReleased=true;return;}
        if(!skipBReleased)return;
        skipHold+=Time.unscaledDeltaTime;
        if(skipHold>=1.3f){skipBReleased=false;SkipStoryToReel();}
    }
    void RestoreReelEnvironment(){
        foreach(var item in reelHiddenRenderers)if(item.Key!=null)item.Key.forceRenderingOff=item.Value;
        reelHiddenRenderers.Clear();
    }
    void UpdateReelEnvironment(){
        bool hide=ReelPresentationVisible&&journeyReelGroup!=null&&journeyReelGroup.alpha>=.999f;
        if(!hide){if(reelHiddenRenderers.Count>0)RestoreReelEnvironment();return;}
        if(reelHiddenRenderers.Count==0)foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
            if(renderer.gameObject.scene!=gameObject.scene||renderer.transform.IsChildOf(player)||renderer.GetComponentInParent<Canvas>()!=null)continue;
            reelHiddenRenderers[renderer]=renderer.forceRenderingOff;
        }
        foreach(var item in reelHiddenRenderers)if(item.Key!=null)item.Key.forceRenderingOff=true;
    }
}}
