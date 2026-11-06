using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DongbaPrototype {
public partial class ObliqueForest {
    [Header("After-capture story")]
    [Range(1.5f,5f)] public float HearthHalfDaySeconds=2.4f;
    [Tooltip("Optional teammate rescue scene, included in Build Settings. Empty uses the playable scroll staging area.")]
    public string RescueScrollScene="";
    public event Action RescueScrollEntered;
    float afterCaptureAge,waitingAge,realizationAge,scrollFadeAge,nightAmount,storyCurtain;
    int waitingBeat=-1,waitingBeatsShown;
    bool scrollEntered,scrollRelocated;
    Image storyNightShade,storyCurtainImage,storySunImage,storyMoonImage,storyCaptionPaper,storySkyRod;
    GameObject storyTimeSymbols,scrollFrame,scrollWorld;
    Text afterCaptureCaption;
    GameObject eastPaperContinuation;
    float eastPaperProgress;
    SpriteRenderer[] retreatSprites;float[] retreatSpriteAlpha;
    Graphic[] retreatGraphics;float[] retreatGraphicAlpha;
    LineRenderer[] retreatLines;Color[] retreatLineStart,retreatLineEnd;
    bool retreatApplied;
    const float EastFadeStart=20,EastFadeEnd=34,EastTransitionX=44;
    static readonly Vector3 ScrollArrival=new Vector3(96,0,70);
    bool AfterCaptureLocked { get { return StoryModeEnabled&&(journeyStage==JourneyStage.Captured||journeyStage==JourneyStage.WaitingAtHearth||journeyStage==JourneyStage.Realization||journeyStage==JourneyStage.ScrollTransition); } }
    bool InScrollWorld { get { return scrollRelocated&&(journeyStage==JourneyStage.ScrollTransition||journeyStage==JourneyStage.ScrollWalk); } }
    bool AfterCapturePeaceful { get { return AfterCaptureLocked||InScrollWorld; } }

    Image StoryOverlay(string name,Transform parent,Color color){
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
    }
    void EnsureAfterCapturePresentation(){
        if(storyNightShade!=null||forestCanvas==null)return;
        storyNightShade=StoryOverlay("Story night / smooth cool dimming",forestCanvas.transform,Color.clear);storyNightShade.transform.SetAsFirstSibling();
        storyTimeSymbols=new GameObject("Dictionary day and night glyphs",typeof(RectTransform));storyTimeSymbols.transform.SetParent(forestCanvas.transform,false);
        var symbols=(RectTransform)storyTimeSymbols.transform;symbols.anchorMin=symbols.anchorMax=new Vector2(.72f,1);symbols.pivot=new Vector2(.5f,1);symbols.anchoredPosition=new Vector2(0,140);symbols.sizeDelta=new Vector2(130,100);
        var rod=UIBlock("Stage suspension rod / ordinary prop, not a Dongba stroke",symbols,new Vector2(.5f,1),new Vector2(.5f,0),new Vector2(0,-20),new Vector2(3,400));storySkyRod=rod.gameObject.AddComponent<Image>();storySkyRod.color=new Color(.208f,.153f,.094f,.92f);storySkyRod.raycastTarget=false;
        storySunImage=StoryOverlay("Sun / PDF77 printed5",symbols,Color.white);storySunImage.sprite=Get("story_sun");storySunImage.preserveAspect=true;
        storyMoonImage=StoryOverlay("Moon / PDF82 printed10",symbols,Color.white);storyMoonImage.sprite=Get("story_moon");storyMoonImage.preserveAspect=true;
        afterCaptureCaption=UILabel("After-capture story caption",forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,48),new Vector2(944,88),24,"");afterCaptureCaption.alignment=TextAnchor.MiddleCenter;
        var backing=UIBlock("Story caption paper",forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(1000,128));storyCaptionPaper=QuietPaper(backing,.98f);backing.SetSiblingIndex(afterCaptureCaption.transform.GetSiblingIndex());
        scrollFrame=new GameObject("Scroll rolled edges / non-script page frame",typeof(RectTransform));var frame=(RectTransform)scrollFrame.transform;frame.SetParent(forestCanvas.transform,false);frame.anchorMin=Vector2.zero;frame.anchorMax=Vector2.one;frame.offsetMin=frame.offsetMax=Vector2.zero;
        for(int side=0;side<2;side++){
            var rail=UIBlock("Rolled paper edge",frame,new Vector2(side,.5f),new Vector2(side,.5f),new Vector2(side==0?35:-35,0),new Vector2(36,700));rail.gameObject.AddComponent<Image>().color=new Color(.57f,.45f,.29f,.72f);
            var highlight=UIBlock("Paper fold",rail,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(22,700));highlight.gameObject.AddComponent<Image>().color=new Color(.88f,.81f,.66f,.95f);
        }
        storyCurtainImage=StoryOverlay("Story cut / blackout",forestCanvas.transform,Color.clear);
        storyTimeSymbols.SetActive(false);scrollFrame.SetActive(false);afterCaptureCaption.gameObject.SetActive(false);
    }
    void ResetAfterCapture(){
        RestoreJourneyInk();retreatSprites=null;retreatGraphics=null;retreatLines=null;eastPaperProgress=0;if(eastPaperContinuation!=null)eastPaperContinuation.SetActive(false);
        afterCaptureAge=waitingAge=realizationAge=scrollFadeAge=nightAmount=storyCurtain=0;waitingBeat=-1;waitingBeatsShown=0;scrollEntered=scrollRelocated=false;
        if(storyNightShade!=null)storyNightShade.color=Color.clear;if(storyCurtainImage!=null)storyCurtainImage.color=Color.clear;
        if(storyCaptionPaper!=null)storyCaptionPaper.gameObject.SetActive(false);if(storyTimeSymbols!=null)storyTimeSymbols.SetActive(false);if(scrollFrame!=null)scrollFrame.SetActive(false);if(scrollWorld!=null)scrollWorld.SetActive(false);
        if(afterCaptureCaption!=null)afterCaptureCaption.gameObject.SetActive(false);
        if(playerImage!=null)playerImage.enabled=true;
    }
    void BeginHearthWaiting(){
        journeyStage=JourneyStage.WaitingAtHearth;waitingAge=0;waitingBeat=-1;waitingBeatsShown=0;
        player.position=ForestSpawn;lastDryPosition=Feet;heading=Vector3.right;facing=1;jumpAge=attackAge=-1;axe.gameObject.SetActive(false);body.localPosition=Vector3.zero;
        foreach(var animal in animals)animal.angry=false;
        playerImage.enabled=true;FollowPlayerCamera(100);
    }
    void UpdateAfterCapturePose(){
        if(storyCaptionPaper!=null)storyCaptionPaper.gameObject.SetActive(afterCaptureCaption.gameObject.activeSelf);
        playerImage.enabled=true;
        ApplyJourneyInk();
    }
    bool UpdateAfterCaptureStory(float dt){
        if(journeyStage!=JourneyStage.Captured&&journeyStage!=JourneyStage.WaitingAtHearth&&journeyStage!=JourneyStage.Realization&&journeyStage!=JourneyStage.EastJourney&&journeyStage!=JourneyStage.ScrollTransition&&journeyStage!=JourneyStage.ScrollWalk)return false;
        EnsureAfterCapturePresentation();
        if(journeyStage==JourneyStage.Captured){
            afterCaptureAge+=dt;storyCurtain=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,1.5f,afterCaptureAge));
            if(afterCaptureAge>=1.6f)BeginHearthWaiting();
        }else if(journeyStage==JourneyStage.WaitingAtHearth){
            waitingAge+=dt;storyCurtain=1-Mathf.SmoothStep(0,1,waitingAge/.7f);
            int beat=Mathf.Min(5,Mathf.FloorToInt(waitingAge/HearthHalfDaySeconds));
            if(beat!=waitingBeat){waitingBeat=beat;waitingBeatsShown++;}
            bool night=(beat&1)==1;
            float daylightPhase=Mathf.Repeat(waitingAge,HearthHalfDaySeconds)/HearthHalfDaySeconds;
            float previousLight=beat==0?0:night?0:1;
            nightAmount=Mathf.Lerp(previousLight,night?1:0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.45f,daylightPhase)));
            storyTimeSymbols.SetActive(true);storySunImage.gameObject.SetActive(!night);storyMoonImage.gameObject.SetActive(night);
            storySunImage.color=storyMoonImage.color=new Color(1,1,1,.92f);
            float dropY;
            if(daylightPhase<.28f){float t=Mathf.Clamp01(daylightPhase/.28f)-1;const float spring=1.25f;float ease=1+(spring+1)*t*t*t+spring*t*t;dropY=Mathf.LerpUnclamped(140,-140,ease);}
            else if(daylightPhase<.72f)dropY=-140;
            else dropY=Mathf.Lerp(-140,140,Mathf.SmoothStep(0,1,(daylightPhase-.72f)/.28f));
            ((RectTransform)storyTimeSymbols.transform).anchoredPosition=new Vector2(0,dropY);
            storySkyRod.rectTransform.anchoredPosition=new Vector2(night?3.2f:-.2f,night?-4.8f:-21.2f);
            afterCaptureCaption.gameObject.SetActive(true);afterCaptureCaption.text="Gaolequ waits beside the hearth.\nThree days and three nights pass...";
            if(waitingAge>=HearthHalfDaySeconds*6){journeyStage=JourneyStage.Realization;realizationAge=0;storyTimeSymbols.SetActive(false);BeginRealizationDialogue();}
        }else if(journeyStage==JourneyStage.Realization){
            realizationAge+=dt;nightAmount=Mathf.MoveTowards(nightAmount,0,dt/1.1f);storyCurtain=0;
            afterCaptureCaption.text="Father should have returned by now.\nSomething is wrong. I must go east and find him.";

        }else if(journeyStage==JourneyStage.EastJourney){
            nightAmount=Mathf.MoveTowards(nightAmount,0,dt);storyCurtain=0;
            eastPaperProgress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(EastFadeStart,EastFadeEnd,Feet.x));
            if(eastPaperProgress<=0){retreatSprites=null;retreatGraphics=null;retreatLines=null;}
            if(Feet.x>=EastTransitionX){journeyStage=JourneyStage.ScrollTransition;scrollFadeAge=0;jumpAge=attackAge=-1;axe.gameObject.SetActive(false);}
        }else if(journeyStage==JourneyStage.ScrollTransition){
            scrollFadeAge+=dt;storyCurtain=scrollFadeAge<1?Mathf.SmoothStep(0,1,scrollFadeAge):1-Mathf.SmoothStep(0,1,(scrollFadeAge-1.25f)/1f);
            if(scrollFadeAge>=1&&!scrollRelocated){
                scrollRelocated=true;eastPaperProgress=0;if(!scrollEntered){scrollEntered=true;RescueScrollEntered?.Invoke();}
                if(!string.IsNullOrWhiteSpace(RescueScrollScene)&&Application.CanStreamedLevelBeLoaded(RescueScrollScene)){SceneManager.LoadSceneAsync(RescueScrollScene);return true;}
                EnsureScrollWorld();player.position=ScrollArrival;lastDryPosition=Feet;FollowPlayerCamera(100);scrollFrame.SetActive(true);
            }
            if(scrollFadeAge>=2.25f){journeyStage=JourneyStage.ScrollWalk;storyCurtain=0;Tell("The rescue journey continues within the scroll.");}
        }
        storyNightShade.color=new Color(.10f,.16f,.27f,nightAmount*.56f);storyCurtainImage.color=new Color(.045f,.035f,.025f,storyCurtain);storyCurtainImage.transform.SetAsLastSibling();
        return true;
    }
    // Continue the same paper tile past the map edge, without an entrance prop or doorway.
    void EnsureEastPaperContinuation(){
        if(eastPaperContinuation==null){var source=GameObject.Find("Stationary paper ground");eastPaperContinuation=Instantiate(source,new Vector3(92,0,0),source.transform.rotation);eastPaperContinuation.name="Eastward paper continuation / no glyphs";}
        eastPaperContinuation.SetActive(true);
    }
    void CacheRetreatInk(){
        var all=FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var selected=new System.Collections.Generic.List<SpriteRenderer>();foreach(var image in all)if(!image.transform.IsChildOf(player))selected.Add(image);
        retreatSprites=selected.ToArray();retreatSpriteAlpha=new float[retreatSprites.Length];
        var graphics=forestCanvas.GetComponentsInChildren<Graphic>(true);var selectedUI=new System.Collections.Generic.List<Graphic>();
        foreach(var graphic in graphics)if(graphic!=storyCurtainImage&&graphic!=storyNightShade)selectedUI.Add(graphic);
        retreatGraphics=selectedUI.ToArray();retreatGraphicAlpha=new float[retreatGraphics.Length];
        retreatLines=FindObjectsByType<LineRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);retreatLineStart=new Color[retreatLines.Length];retreatLineEnd=new Color[retreatLines.Length];
    }
    // Undo only our last-frame multiplier before gameplay updates its own colors, preventing cumulative fading.
    void RestoreJourneyInk(){
        if(!retreatApplied)return;
        for(int i=0;i<retreatSprites.Length;i++)if(retreatSprites[i]!=null){var color=retreatSprites[i].color;color.a=retreatSpriteAlpha[i];retreatSprites[i].color=color;}
        for(int i=0;i<retreatGraphics.Length;i++)if(retreatGraphics[i]!=null){var color=retreatGraphics[i].color;color.a=retreatGraphicAlpha[i];retreatGraphics[i].color=color;}
        for(int i=0;i<retreatLines.Length;i++)if(retreatLines[i]!=null){retreatLines[i].startColor=retreatLineStart[i];retreatLines[i].endColor=retreatLineEnd[i];}
        retreatApplied=false;
    }
    void ApplyJourneyInk(){
        if(!StoryModeEnabled||InScrollWorld||(journeyStage!=JourneyStage.EastJourney&&journeyStage!=JourneyStage.ScrollTransition)||eastPaperProgress<=0)return;
        if(retreatSprites==null)CacheRetreatInk();float visible=1-eastPaperProgress;
        for(int i=0;i<retreatSprites.Length;i++)if(retreatSprites[i]!=null){var color=retreatSprites[i].color;retreatSpriteAlpha[i]=color.a;color.a*=visible;retreatSprites[i].color=color;}
        for(int i=0;i<retreatGraphics.Length;i++)if(retreatGraphics[i]!=null){var color=retreatGraphics[i].color;retreatGraphicAlpha[i]=color.a;color.a*=visible;retreatGraphics[i].color=color;}
        for(int i=0;i<retreatLines.Length;i++)if(retreatLines[i]!=null){retreatLineStart[i]=retreatLines[i].startColor;retreatLineEnd[i]=retreatLines[i].endColor;var a=retreatLineStart[i];var z=retreatLineEnd[i];a.a*=visible;z.a*=visible;retreatLines[i].startColor=a;retreatLines[i].endColor=z;}
        retreatApplied=true;
    }
    void EnsureScrollWorld(){
        if(scrollWorld!=null){scrollWorld.SetActive(true);return;}scrollWorld=new GameObject("Playable rescue scroll staging area / puzzle scene integration pending");
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Scroll paper ground";ground.transform.SetParent(scrollWorld.transform);ground.transform.position=new Vector3(108,-.015f,70);ground.transform.localScale=new Vector3(6,1,3);var material=new Material(groundMaterial);material.SetFloat("_Decline",0);ground.GetComponent<Renderer>().sharedMaterial=material;ground.GetComponent<Renderer>().sortingOrder=-10000;
        for(int i=0;i<6;i++){var tree=Billboard("Existing dictionary tree on scroll",scrollWorld.transform,dictionarySprites["plant_"+(i%3).ToString("00")],2.8f+(i%2)*.3f);tree.position=new Vector3(99+i*4.5f,0,70+(i%2==0?5:-5));tree.GetComponent<SpriteRenderer>().sortingOrder=100-Mathf.RoundToInt(tree.position.z*10);}
    }
    string AfterCaptureObjective(){
        if(journeyStage==JourneyStage.Captured||journeyStage==JourneyStage.WaitingAtHearth)return "STORY  Waiting for Father — three days and three nights";
        if(journeyStage==JourneyStage.Realization)return "STORY  Father has not returned";
        if(journeyStage==JourneyStage.EastJourney)return "STORY  Go east through the woods to find Father";
        if(journeyStage==JourneyStage.ScrollTransition)return "STORY  Entering the scroll";
        if(journeyStage==JourneyStage.ScrollWalk)return "STORY  Rescue journey — continue east within the scroll";
        return "";
    }
}
}
