using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace DongbaPrototype {public partial class ObliqueForest {
    Sprite uiPaperSprite,uiInkDot;Font uiHeadingFont;
    Text uiMode,uiIntroSpeaker,uiIntroContinue,uiIntroProgress;
    Image[] uiHealthPips=new Image[5];
    GameObject uiHelpShortcut,uiHelpPanel,uiObjectivePanel,uiNoticePanel,uiCoreHints,uiBoarPlate;
    bool uiHelpOpen;
    Being careLimitTarget;string careLimitText;float careLimitUntil;
    void ShowCareLimit(Being target,string text){careLimitTarget=target;careLimitText=text;careLimitUntil=clock+1.6f;}
    void ClearCareLimit(){careLimitTarget=null;careLimitText="";careLimitUntil=0;}
    void ApplyCareLimit(Being target){if(target==careLimitTarget&&clock<careLimitUntil)hudPrompt.text=careLimitText;}
    readonly Color uiAccent=new Color(.53f,.22f,.15f),uiMuted=new Color(.45f,.37f,.27f);
    void PrepareQuietUIArt(){
        uiPaperSprite=DongbaUIStyle.PaperSprite;
        var dotTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);dotTexture.name="Vitality ink mark / UI, not a pictograph";var dotPixels=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float dx=(x-15.5f)/16,dy=(y-15.5f)/16;float angle=Mathf.Atan2(dy,dx);float radius=Mathf.Sqrt(dx*dx+dy*dy)+.035f*Mathf.Sin(angle*5)+.02f*Mathf.Cos(angle*3);dotPixels[y*32+x]=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.94f,radius)));}dotTexture.SetPixels(dotPixels);dotTexture.Apply();uiInkDot=Sprite.Create(dotTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
        uiHeadingFont=forestFont;
        if(Array.Exists(Font.GetOSInstalledFontNames(),name=>name.Equals("Georgia",StringComparison.OrdinalIgnoreCase)))uiHeadingFont=Font.CreateDynamicFontFromOSFont("Georgia",30);
    }
    Image QuietPaper(RectTransform rect,float opacity=.95f){var image=rect.gameObject.AddComponent<Image>();image.sprite=uiPaperSprite;image.type=Image.Type.Sliced;image.color=new Color(1,1,1,opacity);image.raycastTarget=false;return image;}
    void QuietRule(Transform parent,Vector2 position,Vector2 size,Color color){var rule=UIBlock("Ink rule / UI decoration",parent,new Vector2(0,1),new Vector2(0,1),position,size);rule.gameObject.AddComponent<Image>().color=color;}
    void BuildQuietForestHUD(GameObject go){
        PrepareQuietUIArt();
        var healthPlate=UIBlock("Contextual health",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(34,-186),new Vector2(238,44));QuietPaper(healthPlate);
        hudHealth=UILabel("Health",healthPlate,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-9),new Vector2(76,27),17,"Health");
        for(int i=0;i<5;i++){var dot=UIBlock("Health ink dot "+i,healthPlate,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(111+i*23,-22),new Vector2(17,21));uiHealthPips[i]=dot.gameObject.AddComponent<Image>();uiHealthPips[i].sprite=uiInkDot;uiHealthPips[i].raycastTarget=false;}
        uiMode=UILabel("Legacy mode / hidden",healthPlate,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");uiMode.gameObject.SetActive(false);
        var area=UIBlock("Legacy area / hidden",go.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);hudArea=UILabel("Area",area,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");area.gameObject.SetActive(false);
        var note=UIBlock("Legacy notice / hidden",go.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);uiNoticePanel=note.gameObject;hudNotice=UILabel("Notice",note,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");note.gameObject.SetActive(false);
        var card=UIBlock("Nearby interaction",go.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-30,30),new Vector2(440,202));promptPanel=card.gameObject;DongbaUIStyle.EdgeShadow(QuietPaper(card));
        var sample=UIBlock("Selected glyph leaf",card,new Vector2(0,1),new Vector2(0,1),new Vector2(-8,-16),new Vector2(132,166));sample.localRotation=Quaternion.Euler(0,0,-2);DongbaUIStyle.EdgeShadow(DongbaUIStyle.Plate(sample,new Color(.94f,.92f,.85f)),.1f);
        interactionGlyph=DongbaUIStyle.Picture(sample,"Exact selected object glyph",null,new Vector2(20,-16),new Vector2(92,118));
        DongbaUIStyle.Stroke(sample,new Vector2(20,-143),new Vector2(92,1.5f),new Color(.3f,.2f,.1f,.4f));
        hudPromptTitle=UILabel("Target",card,new Vector2(0,1),new Vector2(0,1),new Vector2(144,-26),new Vector2(275,40),25,"");DongbaUIStyle.Serif(hudPromptTitle);
        DongbaUIStyle.Stroke(card,new Vector2(144,-68),new Vector2(266,1.5f),new Color(.3f,.2f,.1f,.35f));
        hudPrompt=UILabel("Actions",card,new Vector2(0,1),new Vector2(0,1),new Vector2(146,-83),new Vector2(271,70),19,"");hudPrompt.supportRichText=true;hudPrompt.lineSpacing=1.4f;
        var wait=DongbaUIStyle.Stroke(card,new Vector2(146,-151),new Vector2(266,3),new Color(.3f,.2f,.1f,.12f));
        interactionWait=DongbaUIStyle.Stroke(wait.transform,Vector2.zero,new Vector2(266,3),DongbaUIStyle.Accent);interactionWait.type=Image.Type.Filled;interactionWait.fillMethod=Image.FillMethod.Horizontal;interactionWait.fillOrigin=0;
        var help=UIBlock("Help shortcut",go.transform,Vector2.zero,Vector2.zero,new Vector2(30,30),new Vector2(172,48));uiHelpShortcut=help.gameObject;QuietPaper(help,.98f);
        DongbaUIStyle.Stroke(help,new Vector2(13,-10),new Vector2(2,28),uiAccent);
        var key=UILabel("Controls shortcut",help,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(146,29),18,"H   Controls");key.alignment=TextAnchor.MiddleCenter;
        var tutorial=UIBlock("First action",go.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,30),new Vector2(550,54));uiCoreHints=tutorial.gameObject;QuietPaper(tutorial,.98f);
        DongbaUIStyle.Stroke(tutorial,new Vector2(17,-10),new Vector2(3,32),uiAccent);
        var hint=UILabel("Context tutorial",tutorial,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(510,34),20,"");hint.alignment=TextAnchor.MiddleCenter;
        uiHelpPanel=new GameObject("Legacy help / replaced by shared menu");uiHelpPanel.transform.SetParent(go.transform);uiHelpPanel.SetActive(false);BuildInteractionPolish();
    }
    void HandleQuietHUDInput(Keyboard keyboard){if(!DialogueAdvanceInput.GameplayInputBlocked&&keyboard!=null&&keyboard.hKey.wasPressedThisFrame)uiHelpOpen=!uiHelpOpen;}
    void RefreshQuietHUD(){RefreshContextHUD();}
    void EnsureQuietObjective(){
        if(journeyObjective!=null||forestCanvas==null)return;
        var panel=UIBlock("Current objective",forestCanvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-28),new Vector2(448,145));uiObjectivePanel=panel.gameObject;var paper=QuietPaper(panel,.98f);paper.raycastTarget=true;DongbaUIStyle.EdgeShadow(paper,.10f);
        objectiveFade=panel.gameObject.AddComponent<CanvasGroup>();
        var ribbon=UIBlock("Chapter bookmark",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(12,4),new Vector2(40,106));DongbaUIStyle.Plate(ribbon,DongbaUIStyle.Accent);
        chapterNumber=UILabel("Chapter number",ribbon,new Vector2(0,1),new Vector2(0,1),new Vector2(3,-28),new Vector2(34,38),25,"I");chapterNumber.color=new Color(.98f,.94f,.82f);chapterNumber.alignment=TextAnchor.MiddleCenter;DongbaUIStyle.Serif(chapterNumber);
        objectiveTag=UILabel("Chapter",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(71,-16),new Vector2(320,23),14,"");objectiveTag.color=uiAccent;
        journeyObjective=UILabel("Objective",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(69,-46),new Vector2(356,43),26,"");DongbaUIStyle.Serif(journeyObjective);
        objectiveDetail=UILabel("Objective detail",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(71,-95),new Vector2(349,42),18,"");objectiveDetail.color=DongbaUIStyle.Muted;
        var mark=UILabel("Expand chapter note",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(407,-15),new Vector2(22,25),18,"−");mark.color=uiAccent;
        chapterTab=panel.gameObject.AddComponent<DongbaChapterTab>();chapterTab.Initialize(panel,(RectTransform)hudHealth.transform.parent,ribbon,journeyObjective,objectiveDetail,mark);
    }
    RectTransform CreateQuietDialogue(string name,out Text speaker,out Text content,out Text advance,out Text progress){
        var panel=UIBlock(name,forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(1000,160));QuietPaper(panel,.98f);
        QuietRule(panel,new Vector2(28,-21),new Vector2(30,2),uiAccent);
        speaker=UILabel("Speaker",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(71,-13),new Vector2(600,25),16,"");speaker.fontStyle=FontStyle.Bold;speaker.color=uiAccent;
        content=UILabel("Conversation",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-49),new Vector2(944,68),25,"");content.lineSpacing=1.10f;
        advance=UILabel("Continue hint",panel,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,15),new Vector2(320,25),16,"ANY KEY   Continue");advance.alignment=TextAnchor.MiddleRight;advance.color=uiMuted;
        progress=UILabel("Dialogue progress",panel,Vector2.zero,Vector2.zero,new Vector2(28,15),new Vector2(180,25),16,"");progress.color=uiMuted;return panel;
    }
    string QuietStoryObjective(){
        if(IsRestorationEnding)return RestorationObjective();
        switch(journeyStage){
            case JourneyStage.Hunting:return "Hunt with Father\nExplore the western woods.";
            case JourneyStage.BoarPass:return "A great boar crosses the river.";
            case JourneyStage.Invitation:return "Return to Father\nHe wants to speak about the boar.";
            case JourneyStage.Boar:return "Hunt the great boar\nCross the river to the eastern clearing.";
            case JourneyStage.Leaving:return "Head home together\nCross back to the western bank.";
            case JourneyStage.ReturnForKnife:return "Wait for Father\nHe has gone back for his knife.";
            case JourneyStage.Captured:return "Father has been taken.";
            case JourneyStage.EastJourney:return "Find Father\nFollow the woods eastward.";
            case JourneyStage.ScrollWalk:return "The rescue journey\nContinue east within the scroll.";
            default:return "";
        }
    }
    void EnsureQuietBoarMarker(){
        if(boarMarker!=null||forestCanvas==null)return;
        var panel=UIBlock("Great boar",forestCanvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(208,66));uiBoarPlate=panel.gameObject;QuietPaper(panel,.96f);
        boarMarker=UILabel("Great boar name",panel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-9),new Vector2(176,28),20,"");boarMarker.alignment=TextAnchor.MiddleCenter;boarMarker.color=uiAccent;DongbaUIStyle.Serif(boarMarker);
        var track=UIBlock("Health track",panel,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,13),new Vector2(160,7));track.gameObject.AddComponent<Image>().color=new Color(.3f,.2f,.1f,.15f);
        var fill=UIBlock("Remaining health",track,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);fill.anchorMax=Vector2.one;boarHealthFill=fill.gameObject.AddComponent<Image>();boarHealthFill.color=uiAccent;
    }
}}
