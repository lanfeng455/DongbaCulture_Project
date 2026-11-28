using System;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    [Serializable] class JourneyReelBeat {public int x,width;}
    [Serializable] class JourneyReelLayout {public int tileHeight,totalWidth;public JourneyReelBeat[] beats;}
    JourneyReelLayout journeyReelData;Canvas journeyReelCanvas;CanvasGroup journeyReelGroup;RectTransform journeyReelRoot;RawImage[] journeyReelPanels;int[] journeyReelIndices={-1,-1,-1};
    Material reelPlayerInk,reelOriginalPlayerInk;Vector3 reelOriginalPlayerScale;bool reelPlayerStyled;Vector3 reelLastFeet;float reelStepPhase,reelWalkWeight;
    // 卷轴纸必须盖住所有森林物件（树/叶最深约 28407），但要在 HUD(30000) 与对白(31000) 之下；
    // 卷轴演员（白色纸面人）又要盖在卷轴纸之上，所以单独给一档更高的排序
    const int k_reelCanvasSortingOrder=29500,k_reelActorSortingOrder=29600;
    void RestoreReelPlayer(){if(!reelPlayerStyled)return;playerImage.sharedMaterial=reelOriginalPlayerInk;body.localScale=reelOriginalPlayerScale;body.localPosition=Vector3.zero;body.rotation=billboard;reelPlayerStyled=false;}
    void StyleReelPlayer(float blend){
        if(!reelPlayerStyled){reelOriginalPlayerInk=playerImage.sharedMaterial;reelOriginalPlayerScale=body.localScale;reelPlayerStyled=true;reelLastFeet=Feet;reelStepPhase=reelWalkWeight=0;}
        if(reelPlayerInk==null){reelPlayerInk=new Material(Resources.Load<Shader>("ReelPlayerInk"));reelPlayerInk.SetTexture("_PaperTex",paper);var size=playerImage.sprite.bounds.size;reelPlayerInk.SetVector("_GlyphSize",new Vector4(size.x,size.y,0,0));}reelPlayerInk.SetFloat("_Blend",blend);reelPlayerInk.SetFloat("_PaperDecline",groundMaterial.GetFloat("_Decline"));playerImage.sharedMaterial=reelPlayerInk;
        // Update 里每帧会把演员排序重置为 DepthOrder，这里在 LateUpdate（UpdateJourneyReel）再盖回来，
        // 保证卷轴演员始终画在卷轴纸（k_reelCanvasSortingOrder）之上
        playerImage.sortingOrder=k_reelActorSortingOrder;axe.GetComponent<SpriteRenderer>().sortingOrder=k_reelActorSortingOrder-1;
        float originalHeight=playerImage.sprite.bounds.size.y*reelOriginalPlayerScale.y;
        float factor=(XRTrackingControlsCamera ? xrUnits*.22f : cam.orthographicSize*2/3)/Mathf.Max(.01f,originalHeight);
        body.localScale=reelOriginalPlayerScale*Mathf.Lerp(1,factor,blend)*reelActorScale;
        var view=cam.WorldToViewportPoint(player.position);var bottom=cam.ViewportToWorldPoint(new Vector3(view.x,.055f,cam.nearClipPlane+3f));
        float distance=Vector3.Distance(Feet,reelLastFeet);reelLastFeet=Feet;reelStepPhase+=distance*2.8f;
        reelWalkWeight=Mathf.Lerp(reelWalkWeight,distance>.001f?1:0,1-Mathf.Exp(-Time.deltaTime*8));
        float tilt=(Mathf.Sin(reelStepPhase)*1.25f*reelWalkWeight+Mathf.Sin(clock*2.1f)*.22f*(1-reelWalkWeight))*blend;
        float lift=(Mathf.Abs(Mathf.Sin(reelStepPhase))*2.5f*reelWalkWeight+Mathf.Sin(clock*2.1f)*.5f*(1-reelWalkWeight))*cam.orthographicSize*2/Screen.height;
        if(XRTrackingControlsCamera){bottom=new Vector3(-xrUnits*.22f,xrUnits*.025f,ForestHalfDepth-xrUnits*.018f);lift=Mathf.Abs(Mathf.Sin(reelStepPhase))*.035f*reelWalkWeight;body.position=Vector3.Lerp(player.position,bottom,blend)+Vector3.up*lift;body.rotation=Quaternion.Euler(0,0,tilt);}
        else {body.position=Vector3.Lerp(player.position,bottom,blend)+cam.transform.up*(lift*blend);body.rotation=billboard*Quaternion.Euler(0,0,tilt);}
    }

    public SpriteRenderer ReelActorRenderer=>playerImage;
    public Camera ReelViewCamera=>cam;
    float reelActorScale=1;
    public void SetReelActorScale(float value){reelActorScale=Mathf.Clamp(value,.15f,1);}
    float journeyReelFocus;
    public void SetReelTransitionFocus(float value){journeyReelFocus=value;}
    static readonly string[] JourneyDestinations={"Beat_2_Cleansing_Impurity","Beat_4 Bear and Pig"};
    int journeyReelNextDestination;
    float journeyReelNextCheckpoint=-1,journeyReelReturnProgress;
    // Compatibility for the existing first-portal regression checks.
    bool journeyReelPassedPortal=>journeyReelNextDestination>0;
    ReelPortalHost reelPortalHost;
    const float JourneyReelStart=30,JourneyReelPixelsPerUnit=180,JourneyReelInterludePixels=1810;
    public float JourneyReelProgress=>Mathf.Max(0,(Feet.x-JourneyReelStart)*JourneyReelPixelsPerUnit);
    public float JourneyReelReturnProgress=>journeyReelReturnProgress;
    public float JourneyReelNextCheckpoint=>journeyReelNextCheckpoint;
    public int JourneyReelNextDestinationIndex=>journeyReelNextDestination;
    public string JourneyReelNextScene=>journeyReelNextDestination<JourneyDestinations.Length?JourneyDestinations[journeyReelNextDestination]:null;
    // Hide forest-only HUD from the first paper fade, including wounded-player health.
    bool ReelPresentationVisible=>StoryModeEnabled&&(journeyStage==JourneyStage.ScrollTransition||journeyStage==JourneyStage.ScrollWalk||(journeyStage==JourneyStage.EastJourney&&Feet.x>=JourneyReelStart));
    void PrepareJourneyDestination(){
        if(JourneyReelNextScene==null)return;
        if(reelPortalHost==null)reelPortalHost=new GameObject("Reel level handoff").AddComponent<ReelPortalHost>();
        reelPortalHost.Prepare(JourneyReelNextScene);
    }
    System.Collections.IEnumerator PreviewJourneyReel(){
        PrepareJourneyDestination();
        yield return new WaitForSeconds(1);SetStoryModeEnabled(true);RestartRun();fatherIntroActive=false;
        while(Balance>-10)RegisterDamage();if(storyFatherRoot!=null)storyFatherRoot.gameObject.SetActive(false);
        journeyStage=JourneyStage.EastJourney;EnsureAfterCapturePresentation();EnsureEastPaperContinuation();player.position=new Vector3(28,0,0);FollowPlayerCamera(100);
    }
    void ResetJourneyReel(){reelActorScale=1;RestoreReelPlayer();journeyReelFocus=0;journeyReelNextDestination=0;journeyReelNextCheckpoint=-1;journeyReelReturnProgress=0;if(journeyReelGroup!=null)journeyReelGroup.alpha=0;}
    void EnsureJourneyReel(){
        if(journeyReelCanvas!=null)return;
        journeyReelData=JsonUtility.FromJson<JourneyReelLayout>(Resources.Load<TextAsset>("JourneyReel/timeline").text);
        var go=new GameObject("Journey reel / source image sequence",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
        journeyReelCanvas=go.GetComponent<Canvas>();journeyReelCanvas.renderMode=RenderMode.ScreenSpaceCamera;journeyReelCanvas.worldCamera=cam;journeyReelCanvas.planeDistance=1;journeyReelCanvas.sortingOrder=k_reelCanvasSortingOrder;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        journeyReelGroup=go.GetComponent<CanvasGroup>();journeyReelGroup.blocksRaycasts=false;journeyReelGroup.alpha=0;journeyReelRoot=(RectTransform)go.transform;
        var backing=new GameObject("Matching warm paper",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));backing.transform.SetParent(go.transform,false);var wash=backing.GetComponent<RawImage>();wash.texture=paper;wash.color=new Color(.65f,.54f,.36f);wash.raycastTarget=false;var br=wash.rectTransform;br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;
        if(XRTrackingControlsCamera){
            journeyReelCanvas.renderMode=RenderMode.WorldSpace;scaler.enabled=false;
            journeyReelRoot.sizeDelta=new Vector2(1600,900);journeyReelRoot.localScale=Vector3.one*(XRStageWidth/1600f);
            journeyReelRoot.SetPositionAndRotation(XRStageCentre,Quaternion.identity);go.layer=30;go.AddComponent<RectMask2D>();
        }
        journeyReelPanels=new RawImage[3];for(int i=0;i<3;i++){var panel=new GameObject("Original reel tile",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));panel.transform.SetParent(go.transform,false);if(XRTrackingControlsCamera)panel.layer=30;journeyReelPanels[i]=panel.GetComponent<RawImage>();journeyReelPanels[i].raycastTarget=false;}
    }
    void UpdateJourneyReel(){
        if(!StoryModeEnabled||(journeyStage!=JourneyStage.EastJourney&&journeyStage!=JourneyStage.ScrollTransition)){RestoreReelPlayer();if(journeyReelGroup!=null)journeyReelGroup.alpha=0;return;}
        if(Feet.x<JourneyReelStart){RestoreReelPlayer();if(journeyReelGroup!=null)journeyReelGroup.alpha=0;return;}
        EnsureJourneyReel();float progress=JourneyReelProgress;
        if(journeyReelNextCheckpoint<0)journeyReelNextCheckpoint=journeyReelData.beats[3].x;
        journeyReelGroup.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(JourneyReelStart,35,Feet.x));
        StyleReelPlayer(journeyReelGroup.alpha);
        if(JourneyReelNextScene!=null&&Feet.x>35&&(reelPortalHost==null||!reelPortalHost.IsBusy))PrepareJourneyDestination();
        float height=journeyReelRoot.rect.height*1.05f*(1+journeyReelFocus*.10f),scale=height/journeyReelData.tileHeight;
        float position=Mathf.Min(progress,Mathf.Max(0,journeyReelData.totalWidth-journeyReelRoot.rect.width/scale));int first=0;
        for(int i=0;i<journeyReelData.beats.Length;i++)if(journeyReelData.beats[i].x<=position)first=i;
        for(int slot=0;slot<3;slot++){int idx=first+slot;var panel=journeyReelPanels[slot];bool valid=idx<journeyReelData.beats.Length;panel.gameObject.SetActive(valid);if(!valid)continue;
            if(journeyReelIndices[slot]!=idx){var previous=panel.texture;panel.texture=Resources.Load<Texture2D>("JourneyReel/page_"+(idx+1).ToString("D2"));journeyReelIndices[slot]=idx;if(previous!=null){bool shared=false;foreach(var other in journeyReelPanels)shared|=other!=panel&&other.texture==previous;if(!shared)Resources.UnloadAsset(previous);}}
            var beat=journeyReelData.beats[idx];var rect=panel.rectTransform;rect.pivot=new Vector2(0,.5f);rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.sizeDelta=new Vector2(beat.width*scale,height);rect.anchoredPosition=new Vector2((beat.x-position)*scale,0);
        }
        if(eastPaperContinuation!=null&&Feet.x>38)eastPaperContinuation.transform.position=new Vector3(Feet.x,-.015f,0);
        if(JourneyReelNextScene!=null&&journeyStage==JourneyStage.EastJourney&&progress>=journeyReelNextCheckpoint&&(reelPortalHost==null||!reelPortalHost.IsBusy)){
            journeyStage=JourneyStage.ScrollTransition;jumpAge=attackAge=-1;axe.gameObject.SetActive(false);
            if(reelPortalHost==null){var host=new GameObject("Reel level handoff");reelPortalHost=host.AddComponent<ReelPortalHost>();}
            int destinationIndex=journeyReelNextDestination;
            bool entered=reelPortalHost.Enter(this,JourneyReelNextScene,()=>{
                // The forest stays loaded: preserve its feet, current page, and walk phase.
                // This callback prepares the return image; IsBusy remains true until the
                // camera handoff finishes, preventing an immediate second entry.
                journeyReelReturnProgress=JourneyReelProgress;
                journeyReelNextDestination=destinationIndex+1;
                journeyReelNextCheckpoint=journeyReelReturnProgress+JourneyReelInterludePixels;
                journeyStage=JourneyStage.EastJourney;storyCurtain=0;scrollFadeAge=0;FollowPlayerCamera(100);
            });
            if(!entered)journeyStage=JourneyStage.EastJourney;
        }
    }
}}
