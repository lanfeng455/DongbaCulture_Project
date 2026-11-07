using System;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    [Serializable] class JourneyReelBeat {public int x,width;}
    [Serializable] class JourneyReelLayout {public int tileHeight,totalWidth;public JourneyReelBeat[] beats;}
    JourneyReelLayout journeyReelData;Canvas journeyReelCanvas;CanvasGroup journeyReelGroup;RectTransform journeyReelRoot;RawImage[] journeyReelPanels;int[] journeyReelIndices={-1,-1,-1};
    float journeyReelFocus;
    public void SetReelTransitionFocus(float value){journeyReelFocus=value;}
    bool journeyReelPassedPortal;ReelPortalHost reelPortalHost;
    const float JourneyReelStart=30,JourneyReelPixelsPerUnit=90;
    System.Collections.IEnumerator PreviewJourneyReel(){
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;
        while(Balance>-10)RegisterDamage();if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(28,0,0);FollowPlayerCamera(100);
    }
    void ResetJourneyReel(){journeyReelFocus=0;journeyReelPassedPortal=false;if(journeyReelGroup!=null)journeyReelGroup.alpha=0;}
    void EnsureJourneyReel(){
        if(journeyReelCanvas!=null)return;
        journeyReelData=JsonUtility.FromJson<JourneyReelLayout>(Resources.Load<TextAsset>("JourneyReel/timeline").text);
        var go=new GameObject("Journey reel / source image sequence",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
        journeyReelCanvas=go.GetComponent<Canvas>();journeyReelCanvas.renderMode=RenderMode.ScreenSpaceCamera;journeyReelCanvas.worldCamera=cam;journeyReelCanvas.planeDistance=1;journeyReelCanvas.sortingOrder=-50;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        journeyReelGroup=go.GetComponent<CanvasGroup>();journeyReelGroup.blocksRaycasts=false;journeyReelGroup.alpha=0;journeyReelRoot=(RectTransform)go.transform;
        journeyReelPanels=new RawImage[3];for(int i=0;i<3;i++){var panel=new GameObject("Original reel tile",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));panel.transform.SetParent(go.transform,false);journeyReelPanels[i]=panel.GetComponent<RawImage>();journeyReelPanels[i].raycastTarget=false;}
    }
    void UpdateJourneyReel(){
        if(!StoryModeEnabled||(journeyStage!=JourneyStage.EastJourney&&journeyStage!=JourneyStage.ScrollTransition)){if(journeyReelGroup!=null)journeyReelGroup.alpha=0;return;}
        if(Feet.x<JourneyReelStart){if(journeyReelGroup!=null)journeyReelGroup.alpha=0;return;}
        EnsureJourneyReel();float progress=Mathf.Max(0,(Feet.x-JourneyReelStart)*JourneyReelPixelsPerUnit);
        journeyReelGroup.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(JourneyReelStart,38,Feet.x));
        float height=journeyReelRoot.rect.height*.83f*(1+journeyReelFocus*.10f),scale=height/journeyReelData.tileHeight;
        float position=Mathf.Min(progress,Mathf.Max(0,journeyReelData.totalWidth-journeyReelRoot.rect.width/scale));int first=0;
        for(int i=0;i<journeyReelData.beats.Length;i++)if(journeyReelData.beats[i].x<=position)first=i;
        for(int slot=0;slot<3;slot++){int idx=first+slot;var panel=journeyReelPanels[slot];bool valid=idx<journeyReelData.beats.Length;panel.gameObject.SetActive(valid);if(!valid)continue;
            if(journeyReelIndices[slot]!=idx){var previous=panel.texture;panel.texture=Resources.Load<Texture2D>("JourneyReel/page_"+(idx+1).ToString("D2"));journeyReelIndices[slot]=idx;if(previous!=null){bool shared=false;foreach(var other in journeyReelPanels)shared|=other!=panel&&other.texture==previous;if(!shared)Resources.UnloadAsset(previous);}}
            var beat=journeyReelData.beats[idx];var rect=panel.rectTransform;rect.pivot=new Vector2(0,.5f);rect.anchorMin=rect.anchorMax=new Vector2(0,.53f);rect.sizeDelta=new Vector2(beat.width*scale,height);rect.anchoredPosition=new Vector2((beat.x-position)*scale,0);
        }
        if(eastPaperContinuation!=null&&Feet.x>38)eastPaperContinuation.transform.position=new Vector3(Feet.x,-.015f,0);
        if(!journeyReelPassedPortal&&progress>=journeyReelData.beats[3].x){
            journeyReelPassedPortal=true;journeyStage=JourneyStage.ScrollTransition;jumpAge=attackAge=-1;axe.gameObject.SetActive(false);
            if(reelPortalHost==null){var host=new GameObject("Reel level handoff");reelPortalHost=host.AddComponent<ReelPortalHost>();}
            reelPortalHost.Enter(this,"Beat_2_Cleansing_Impurity",()=>{journeyStage=JourneyStage.EastJourney;storyCurtain=0;scrollFadeAge=0;FollowPlayerCamera(100);});
        }
    }
}}
