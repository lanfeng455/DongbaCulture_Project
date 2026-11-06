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
        var header=UIBlock("Compact identity and vitality",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-20),new Vector2(302,104));QuietPaper(header);
        var title=UILabel("Project name",header,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-12),new Vector2(134,38),29,"DONGBA");title.font=uiHeadingFont;
        uiMode=UILabel("Current mode",header,new Vector2(1,1),new Vector2(1,1),new Vector2(-18,-22),new Vector2(137,23),16,"");uiMode.alignment=TextAnchor.MiddleRight;uiMode.color=uiMuted;
        QuietRule(header,new Vector2(20,-55),new Vector2(262,1),new Color(.40f,.29f,.18f,.20f));
        hudHealth=UILabel("Health",header,new Vector2(0,1),new Vector2(0,1),new Vector2(20,-67),new Vector2(90,25),19,"Health");
        for(int i=0;i<5;i++){var dot=UIBlock("Vitality ink dot "+i,header,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(145+i*28,-79),new Vector2(24,24));uiHealthPips[i]=dot.gameObject.AddComponent<Image>();uiHealthPips[i].sprite=uiInkDot;uiHealthPips[i].raycastTarget=false;}
        var area=UIBlock("Place paper label",go.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-24),new Vector2(216,46));QuietPaper(area,.85f);
        hudArea=UILabel("Location",area,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(194,28),19,"");hudArea.alignment=TextAnchor.MiddleCenter;hudArea.color=uiMuted;
        var note=UIBlock("Feedback paper",go.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,87),new Vector2(660,78));uiNoticePanel=note.gameObject;QuietPaper(note,.95f);
        hudNotice=UILabel("Feedback",note,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(612,58),21,"");hudNotice.alignment=TextAnchor.MiddleCenter;
        var card=UIBlock("Nearby interaction card",go.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(328,128));promptPanel=card.gameObject;QuietPaper(card);
        QuietRule(card,new Vector2(18,-18),new Vector2(3,88),new Color(.53f,.22f,.15f,.60f));
        hudPromptTitle=UILabel("Target name",card,new Vector2(0,1),new Vector2(0,1),new Vector2(34,-15),new Vector2(274,30),23,"");hudPromptTitle.fontStyle=FontStyle.Bold;
        hudPrompt=UILabel("Available actions",card,new Vector2(0,1),new Vector2(0,1),new Vector2(34,-55),new Vector2(274,63),19,"");hudPrompt.supportRichText=true;hudPrompt.lineSpacing=1.15f;
        var toggle=UIBlock("Controls shortcut paper",go.transform,Vector2.zero,Vector2.zero,new Vector2(24,24),new Vector2(150,42));uiHelpShortcut=toggle.gameObject;QuietPaper(toggle,.87f);var help=UILabel("Controls shortcut",toggle,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(130,28),18,"H   Controls");help.alignment=TextAnchor.MiddleCenter;
        var hints=UIBlock("First steps hint",go.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,24),new Vector2(610,42));uiCoreHints=hints.gameObject;QuietPaper(hints,.84f);var hintText=UILabel("Core keys",hints,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(582,28),18,"WASD  Move     SPACE  Jump     E  Care     J  Axe");hintText.alignment=TextAnchor.MiddleCenter;
        var controls=UIBlock("Expanded controls",go.transform,Vector2.zero,Vector2.zero,new Vector2(24,78),new Vector2(350,326));uiHelpPanel=controls.gameObject;QuietPaper(controls,.98f);
        var controlTitle=UILabel("Help title",controls,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-18),new Vector2(302,35),25,"Controls");controlTitle.font=uiHeadingFont;
        QuietRule(controls,new Vector2(24,-61),new Vector2(302,1),new Color(.40f,.29f,.18f,.2f));
        var keys=UILabel("Keys",controls,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-79),new Vector2(146,234),18,"WASD / Arrows\nSpace\nJ / Enter\nE\nR\nF5\nEsc\nH");keys.lineSpacing=1.23f;keys.fontStyle=FontStyle.Bold;
        var actions=UILabel("Key actions",controls,new Vector2(0,1),new Vector2(0,1),new Vector2(176,-79),new Vector2(150,234),18,"Move\nJump\nAxe\nCare / feed\nRestart\nStory / sandbox\nQuit\nClose controls");actions.lineSpacing=1.23f;
        uiHelpPanel.SetActive(false);uiNoticePanel.SetActive(false);
    }
    void HandleQuietHUDInput(Keyboard keyboard){if(keyboard!=null&&keyboard.hKey.wasPressedThisFrame)uiHelpOpen=!uiHelpOpen;}
    void RefreshQuietHUD(){
        uiMode.text=StoryModeEnabled?"STORY MODE":"SANDBOX";
        for(int i=0;i<uiHealthPips.Length;i++)uiHealthPips[i].color=i<health?new Color(.57f,.20f,.14f,1):new Color(.37f,.30f,.21f,.22f);
        bool dialogue=fatherIntroActive||ManualStoryDialogueActive||journeyStage==JourneyStage.WaitingAtHearth||journeyStage==JourneyStage.Realization;
        uiHelpShortcut.SetActive(!dialogue&&!AfterCaptureLocked&&storyOpeningState!=StoryOpeningState.CaptureBeat);uiHelpPanel.SetActive(uiHelpOpen&&!dialogue&&!AfterCaptureLocked&&storyOpeningState!=StoryOpeningState.CaptureBeat);uiCoreHints.SetActive(clock<12&&!dialogue&&!uiHelpOpen&&!AfterCaptureLocked&&storyOpeningState!=StoryOpeningState.CaptureBeat);
        uiNoticePanel.SetActive(!dialogue&&hudNotice.text.Length>0);
        if(uiObjectivePanel!=null)uiObjectivePanel.SetActive(StoryModeEnabled&&!dialogue&&journeyStage!=JourneyStage.ScrollTransition);
    }
    void EnsureQuietObjective(){
        if(journeyObjective!=null||forestCanvas==null)return;
        var panel=UIBlock("Story objective paper",forestCanvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-136),new Vector2(462,92));uiObjectivePanel=panel.gameObject;QuietPaper(panel,.91f);
        QuietRule(panel,new Vector2(18,-18),new Vector2(3,56),new Color(.53f,.22f,.15f,.55f));journeyObjective=UILabel("Story objective",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(34,-15),new Vector2(410,65),21,"");journeyObjective.lineSpacing=1.12f;
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
        if(boarMarker!=null||forestCanvas==null)return;var panel=UIBlock("Great boar marker paper",forestCanvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(190,68));uiBoarPlate=panel.gameObject;QuietPaper(panel,.92f);
        boarMarker=UILabel("Great boar target",panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(170,54),20,"");boarMarker.alignment=TextAnchor.MiddleCenter;boarMarker.color=uiAccent;
    }
}}
