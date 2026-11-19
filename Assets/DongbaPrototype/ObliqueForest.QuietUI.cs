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
        const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);tex.name="UI paper edges / not a pictograph";var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){float edge=Mathf.Min(Mathf.Min(x,n-1-x),Mathf.Min(y,n-1-y));float rough=.45f*Mathf.Sin(y*1.7f)+.3f*Mathf.Sin(x*2.3f);float alpha=Mathf.SmoothStep(0,1,(edge+rough)/4f);pixels[y*n+x]=new Color(1,1,1,alpha);}
        tex.SetPixels(pixels);tex.Apply();uiPaperSprite=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));
        var dotTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);dotTexture.name="Vitality ink mark / UI, not a pictograph";var dotPixels=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float dx=(x-15.5f)/16,dy=(y-15.5f)/16;float angle=Mathf.Atan2(dy,dx);float radius=Mathf.Sqrt(dx*dx+dy*dy)+.035f*Mathf.Sin(angle*5)+.02f*Mathf.Cos(angle*3);dotPixels[y*32+x]=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.94f,radius)));}dotTexture.SetPixels(dotPixels);dotTexture.Apply();uiInkDot=Sprite.Create(dotTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
        uiHeadingFont=forestFont;
        if(Array.Exists(Font.GetOSInstalledFontNames(),name=>name.Equals("Georgia",StringComparison.OrdinalIgnoreCase)))uiHeadingFont=Font.CreateDynamicFontFromOSFont("Georgia",30);
    }
    Image QuietPaper(RectTransform rect,float opacity=.95f){var image=rect.gameObject.AddComponent<Image>();image.sprite=uiPaperSprite;image.type=Image.Type.Sliced;image.color=new Color(.96f,.925f,.85f,opacity);image.raycastTarget=false;return image;}
    void QuietRule(Transform parent,Vector2 position,Vector2 size,Color color){var rule=UIBlock("Ink rule / UI decoration",parent,new Vector2(0,1),new Vector2(0,1),position,size);rule.gameObject.AddComponent<Image>().color=color;}
    void BuildQuietForestHUD(GameObject go){
        PrepareQuietUIArt();
        var healthPlate=UIBlock("Contextual health",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-138),new Vector2(252,48));QuietPaper(healthPlate);
        hudHealth=UILabel("Health",healthPlate,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-10),new Vector2(76,27),18,"Health");
        for(int i=0;i<5;i++){var dot=UIBlock("Health ink dot "+i,healthPlate,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(112+i*25,-24),new Vector2(20,20));uiHealthPips[i]=dot.gameObject.AddComponent<Image>();uiHealthPips[i].sprite=uiInkDot;uiHealthPips[i].raycastTarget=false;}
        uiMode=UILabel("Legacy mode / hidden",healthPlate,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");uiMode.gameObject.SetActive(false);
        var area=UIBlock("Legacy area / hidden",go.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);hudArea=UILabel("Area",area,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");area.gameObject.SetActive(false);
        var note=UIBlock("Legacy notice / hidden",go.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);uiNoticePanel=note.gameObject;hudNotice=UILabel("Notice",note,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one,16,"");note.gameObject.SetActive(false);
        var card=UIBlock("Nearby interaction",go.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,28),new Vector2(360,156));promptPanel=card.gameObject;QuietPaper(card);
        QuietRule(card,new Vector2(22,-20),new Vector2(30,2),uiAccent);
        hudPromptTitle=UILabel("Target",card,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-28),new Vector2(312,30),24,"");hudPromptTitle.fontStyle=FontStyle.Bold;
        hudPrompt=UILabel("Actions",card,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-65),new Vector2(312,55),20,"");hudPrompt.supportRichText=true;hudPrompt.lineSpacing=1.12f;
        var help=UIBlock("Help shortcut",go.transform,Vector2.zero,Vector2.zero,new Vector2(28,28),new Vector2(158,44));uiHelpShortcut=help.gameObject;QuietPaper(help,.92f);
        var key=UILabel("Controls shortcut",help,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(138,28),18,"H   Controls");key.alignment=TextAnchor.MiddleCenter;
        var tutorial=UIBlock("First action",go.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(560,52));uiCoreHints=tutorial.gameObject;QuietPaper(tutorial,.94f);
        var hint=UILabel("Context tutorial",tutorial,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(526,34),20,"");hint.alignment=TextAnchor.MiddleCenter;
        uiHelpPanel=new GameObject("Legacy help / replaced by shared menu");uiHelpPanel.transform.SetParent(go.transform);uiHelpPanel.SetActive(false);
        BuildInteractionPolish();
    }
    void HandleQuietHUDInput(Keyboard keyboard){if(keyboard!=null&&keyboard.hKey.wasPressedThisFrame)uiHelpOpen=!uiHelpOpen;}
    void RefreshQuietHUD(){RefreshContextHUD();}
    void EnsureQuietObjective(){
        if(journeyObjective!=null||forestCanvas==null)return;
        var panel=UIBlock("Current objective",forestCanvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-28),new Vector2(430,98));uiObjectivePanel=panel.gameObject;QuietPaper(panel,.94f);
        objectiveFade=panel.gameObject.AddComponent<CanvasGroup>();
        objectiveTag=UILabel("Objective update",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(22,-12),new Vector2(382,23),16,"Current objective");objectiveTag.color=uiAccent;
        journeyObjective=UILabel("Objective",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(22,-38),new Vector2(382,55),21,"");journeyObjective.lineSpacing=1.12f;
    }
    RectTransform CreateQuietDialogue(string name,out Text speaker,out Text content,out Text advance,out Text progress){
        var panel=UIBlock(name,forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(1000,160));QuietPaper(panel,.98f);
        QuietRule(panel,new Vector2(28,-21),new Vector2(30,2),uiAccent);
        speaker=UILabel("Speaker",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(71,-13),new Vector2(600,25),16,"");speaker.fontStyle=FontStyle.Bold;speaker.color=uiAccent;
        content=UILabel("Conversation",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-49),new Vector2(944,68),25,"");content.lineSpacing=1.10f;
        advance=UILabel("Continue hint",panel,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,15),new Vector2(320,25),16,"ENTER   Continue");advance.alignment=TextAnchor.MiddleRight;advance.color=uiMuted;
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
        var panel=UIBlock("Great boar",forestCanvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(200,64));uiBoarPlate=panel.gameObject;QuietPaper(panel,.94f);
        boarMarker=UILabel("Great boar name",panel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-9),new Vector2(176,28),20,"");boarMarker.alignment=TextAnchor.MiddleCenter;boarMarker.color=uiAccent;
        var track=UIBlock("Health track",panel,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,13),new Vector2(160,7));track.gameObject.AddComponent<Image>().color=new Color(.3f,.2f,.1f,.15f);
        var fill=UIBlock("Remaining health",track,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);fill.anchorMax=Vector2.one;boarHealthFill=fill.gameObject.AddComponent<Image>();boarHealthFill.color=uiAccent;
    }
}}
