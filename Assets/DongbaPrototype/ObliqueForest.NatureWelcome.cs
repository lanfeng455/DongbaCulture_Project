using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype { public partial class ObliqueForest {
    RectTransform natureWelcome; CanvasGroup natureWelcomeFade;
    Text natureWelcomeEyebrow,natureWelcomeTitle,natureWelcomeCopy,natureWelcomeAcceptText,natureWelcomeLaterText;
    Button natureWelcomeAccept,natureWelcomeLater;
    Image natureWelcomeGlyph,natureBookmarkPaper;
    readonly Being[] natureAnswerTrees=new Being[3];
    readonly SpriteRenderer[] natureAnswerWinds=new SpriteRenderer[3];
    readonly Vector3[] natureAnswerOrigins=new Vector3[3];
    bool natureWelcomePending,natureFirstComplete,natureFirstLesson,natureFirstResponding;
    float natureUnlockedAt=-100,natureAnsweredAt=-100;
    int natureWelcomeCount;
    bool NatureWelcomeVisible=>natureWelcomePending&&!NatureEditorOpen&&!StoryModeEnabled;
    bool NatureWelcomeOwnsInput=>NatureWelcomeVisible&&!DongbaInterface.GameplayBlocked;
    bool NatureAnswerNoticeVisible=>!StoryModeEnabled&&natureFirstComplete&&clock-natureAnsweredAt<4.5f;
    bool NatureFirstHint=>natureUnlocked&&!natureFirstComplete;

    void BuildNatureWelcome(){
        natureBookmarkPaper=natureTab.GetComponent<Image>();
        natureWelcome=UIBlock("PICO Nature awakening",forestCanvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,86),new Vector2(850,250));
        var paper=QuietPaper(natureWelcome,.99f);paper.raycastTarget=true;DongbaUIStyle.EdgeShadow(paper,.15f);
        natureWelcomeFade=natureWelcome.gameObject.AddComponent<CanvasGroup>();
        DongbaUIStyle.Stroke(natureWelcome,new Vector2(28,-29),new Vector2(5,62),uiAccent);
        natureWelcomeEyebrow=UILabel("Awakening / new possibility",natureWelcome,new Vector2(0,1),new Vector2(0,1),new Vector2(50,-23),new Vector2(610,27),17,"");natureWelcomeEyebrow.color=uiAccent;
        natureWelcomeTitle=UILabel("The forest answers",natureWelcome,new Vector2(0,1),new Vector2(0,1),new Vector2(48,-48),new Vector2(620,64),36,"");DongbaUIStyle.Serif(natureWelcomeTitle);
        natureWelcomeCopy=UILabel("A different way to play",natureWelcome,new Vector2(0,1),new Vector2(0,1),new Vector2(50,-118),new Vector2(626,58),20,"");
        var glyph=UIBlock("A life answering",natureWelcome,new Vector2(1,1),new Vector2(1,1),new Vector2(-30,-27),new Vector2(112,130));
        natureWelcomeGlyph=glyph.gameObject.AddComponent<Image>();natureWelcomeGlyph.sprite=dictionarySprites["plant_01"];natureWelcomeGlyph.preserveAspect=true;natureWelcomeGlyph.raycastTarget=false;natureWelcomeGlyph.color=new Color(.38f,.44f,.23f,.85f);
        natureWelcomeAccept=NatureWelcomeButton("First invitation",new Vector2(50,-188),new Vector2(385,46),true,out natureWelcomeAcceptText);
        natureWelcomeAccept.onClick.AddListener(BeginNatureFirstInvitation);
        natureWelcomeLater=NatureWelcomeButton("Later",new Vector2(453,-188),new Vector2(230,46),false,out natureWelcomeLaterText);
        natureWelcomeLater.onClick.AddListener(DeferNatureWelcome);
        for(int i=0;i<3;i++){
            var root=new GameObject("Forest response / existing wind "+i).transform;
            natureAnswerWinds[i]=Billboard("Wind glyph",root,stormMarkSprite,.8f).GetComponent<SpriteRenderer>();
            root.gameObject.SetActive(false);
        }
        natureWelcome.gameObject.SetActive(false);
    }
    Button NatureWelcomeButton(string label,Vector2 position,Vector2 size,bool primary,out Text text){
        var rect=UIBlock(label,natureWelcome,new Vector2(0,1),new Vector2(0,1),position,size);
        var ink=rect.gameObject.AddComponent<Image>();ink.color=primary?new Color(.32f,.23f,.13f):new Color(.38f,.28f,.15f,.08f);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=ink;button.navigation=new Navigation{mode=Navigation.Mode.None};
        text=UILabel(label,rect,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,size-new Vector2(18,4),21,"");text.alignment=TextAnchor.MiddleCenter;text.color=primary?new Color(.98f,.94f,.83f):uiMuted;
        return button;
    }
    void BeginNatureWelcome(){
        natureWelcomePending=true;natureUnlockedAt=clock;natureWelcomeCount++;
        uiHelpOpen=xrHelpOpen=false;playerMotion.Care();PlayForestSfx("grow",Feet,.95f);
        if(XRTrackingControlsCamera&&xrRightDevice.isValid)xrRightDevice.SendHapticImpulse(0,.25f,.12f);
        for(int i=0;i<3;i++){
            Being closest=null;float best=12;
            foreach(var plant in plants){
                if(!plant.tree||plant.health<=0||plant.editBusy)continue;
                bool used=false;for(int j=0;j<i;j++)if(natureAnswerTrees[j]==plant)used=true;if(used)continue;
                var view=cam.WorldToViewportPoint(plant.home+Vector3.up);if(view.z<=0||view.x<.02f||view.x>.98f||view.y<.06f||view.y>.94f)continue;
                float d=Vector3.Distance(Feet,plant.home);if(d<best){best=d;closest=plant;}
            }
            natureAnswerTrees[i]=closest;natureAnswerOrigins[i]=closest!=null?closest.home+Vector3.up*.75f:Feet+new Vector3(-4-i,1,i-1);
        }
    }
    void ResetNatureWelcome(){
        natureWelcomePending=natureFirstComplete=natureFirstLesson=natureFirstResponding=false;
        natureUnlockedAt=natureAnsweredAt=-100;natureWelcomeCount=0;
        if(natureWelcome!=null)natureWelcome.gameObject.SetActive(false);
        for(int i=0;i<3;i++){natureAnswerTrees[i]=null;if(natureAnswerWinds[i]!=null)natureAnswerWinds[i].transform.parent.gameObject.SetActive(false);}
    }
    void BeginNatureFirstInvitation(){
        if(!NatureAvailable)return;
        natureWelcomePending=false;natureFirstLesson=!natureFirstComplete;
        if(!NatureEditorOpen)ToggleNatureEditor();
        NatureGo(NaturePage.Plants);natureMessageUntil=0;
    }
    public bool DismissNatureWelcome(){if(!NatureWelcomeVisible)return false;DeferNatureWelcome();return true;}
    void DeferNatureWelcome(){natureWelcomePending=false;natureInputFrame=Time.frameCount;}
    void FinishNatureFirstInvitation(Being being){
        if(StoryModeEnabled||natureFirstComplete)return;
        natureFirstComplete=true;natureFirstLesson=natureFirstResponding=natureWelcomePending=false;natureAnsweredAt=clock;
        natureWelcomeGlyph.sprite=being.animal?being.renderer.sprite:being.fullGlyph;
        PlayForestSfx("plant",being.home,.9f);natureUIStamp="";
        if(XRTrackingControlsCamera&&xrRightDevice.isValid)xrRightDevice.SendHapticImpulse(0,.18f,.09f);
    }
    float NatureWelcomeAngle(Being plant){
        if(StoryModeEnabled)return 0;
        for(int i=0;i<3;i++)if(natureAnswerTrees[i]==plant){float u=(clock-natureUnlockedAt-i*.22f)/1.7f;if(u>0&&u<1)return Mathf.Sin(u*Mathf.PI*2)*Mathf.Sin(u*Mathf.PI)*2.7f;}
        return 0;
    }
    void UpdateNatureWelcome(bool showing){
        if(natureWelcome==null)return;
        float age=clock-natureUnlockedAt;
        for(int i=0;i<3;i++){
            var image=natureAnswerWinds[i];float u=(age-.25f-i*.28f)/1.6f;
            bool alive=showing&&u>0&&u<1;image.transform.parent.gameObject.SetActive(alive);if(!alive)continue;
            var target=Feet+new Vector3((i-1)*.30f,.65f,0);image.transform.parent.position=Vector3.Lerp(natureAnswerOrigins[i],target,Mathf.SmoothStep(0,1,u));
            image.transform.rotation=billboard;image.color=new Color(.6f,.65f,.42f,.29f*Mathf.Sin(u*Mathf.PI));image.sortingOrder=DepthOrder(Feet.z)+15;
        }
        bool success=NatureAnswerNoticeVisible;
        bool visible=showing&&(NatureWelcomeVisible||success);
        natureWelcome.gameObject.SetActive(visible);
        bool eager=NatureAvailable&&!natureFirstComplete;
        natureTab.sizeDelta=new Vector2(eager?370:325,XRTrackingControlsCamera?(eager?78:70):(eager?64:52));
        natureTabText.rectTransform.sizeDelta=natureTab.sizeDelta-new Vector2(22,8);natureTabText.fontSize=eager?21:19;
        natureBookmarkPaper.color=NatureAvailable?new Color(.37f,.26f,.14f,.97f):new Color(1,1,1,.97f);
        natureTabText.color=NatureAvailable?new Color(.99f,.96f,.84f):uiMuted;
        if(eager)natureTabText.text=N(XRTrackingControlsCamera?"NEW · L stick · Invite nature":"NEW · B   Invite nature",XRTrackingControlsCamera?"新能力 · 按左摇杆回应森林":"新能力 · B  邀万物入画");
        float pulse=eager?1+.025f*Mathf.Sin((clock-natureUnlockedAt)*3):1;natureTab.localScale=Vector3.one*pulse;
        if(!visible)return;
        natureWelcomeAccept.gameObject.SetActive(!success);natureWelcomeLater.gameObject.SetActive(!success);
        natureWelcome.sizeDelta=new Vector2(850,success?190:250);
        float reveal=Mathf.SmoothStep(0,1,(success?clock-natureAnsweredAt:age)/.55f);
        natureWelcomeFade.alpha=reveal*(success?1-Mathf.SmoothStep(0,1,(clock-natureAnsweredAt-3.8f)/.7f):1);
        natureWelcomeFade.blocksRaycasts=!success;natureWelcomeFade.interactable=!success;
        natureWelcome.anchorMin=natureWelcome.anchorMax=new Vector2(.5f,XRTrackingControlsCamera?1:0);natureWelcome.pivot=new Vector2(.5f,XRTrackingControlsCamera?.5f:0);
        natureWelcome.anchoredPosition=XRTrackingControlsCamera?new Vector2(0,(success?-112:-155)-24*(1-reveal)):new Vector2(0,24-24*(1-reveal));
        natureWelcome.localScale=Vector3.one*(XRTrackingControlsCamera?.96f:1);
        natureWelcomeEyebrow.text=success?N("AN INVITATION, ANSWERED","一次邀请，一次回应"):N("A NEW WAY TO SHAPE THE FOREST","新的能力 · 邀万物入画");
        natureWelcomeTitle.text=success?N("It answered you.","它回应了你的邀请。"):N("The forest answers you.","你照料的森林，回应了你。");
        natureWelcomeCopy.text=success?N("Move, awaken and call. Make room for your own world.\nCtrl+Z / X: undo. Your care is remembered.","移栽、唤醒、呼唤，让森林成为你心中的模样。\n可以随时撤销，照料与成长仍会保留。"):N("Invite plants to move. Call animals to join you.\nChoose where a new plant grows.","植物愿意迁移、展开，动物愿意应召而来。\n试着邀请一株植物，在你选的地方生长。");
        natureWelcomeAcceptText.text=N(XRTrackingControlsCamera?"A   Make your first invitation":"B   Make your first invitation",XRTrackingControlsCamera?"A  发出第一次邀请":"B  发出第一次邀请");
        natureWelcomeLaterText.text=N(XRTrackingControlsCamera?"B   Later":"Later",XRTrackingControlsCamera?"B  稍后再试":"稍后再试");
        if(XRTrackingControlsCamera&&xrHudInk!=null&&natureWelcomeGlyph.material!=xrHudInk)foreach(var graphic in natureWelcome.GetComponentsInChildren<Graphic>(true))graphic.material=xrHudInk;
        if(NatureWelcomeVisible)natureTab.gameObject.SetActive(false);
    }
    void ApplyNatureFirstStep(){
        if(!natureFirstLesson||natureFirstComplete)return;
        if(naturePage==NaturePage.Plants){natureTitle.text=N("First invitation · Choose a pictograph","第一次邀请 · 选择字形");natureDetail.text=N("Choose a plant you would like to welcome.\nNext, you will decide where it grows.","选一株你想邀请的植物。\n接下来，由你决定它在哪里生长。");}
        if(naturePage==NaturePage.Position){natureTitle.text=N("First invitation · Choose its place","第一次邀请 · 选择落点");natureDetail.text=N(XRTrackingControlsCamera?"Point at clear ground. A / Trigger: invite it here.":"Move your pointer onto clear ground, then click to invite.",XRTrackingControlsCamera?"指向一处空地，按 A／扳机邀请它在这里生长。":"把鼠标移到空地，点击邀请它在这里生长。");}
        if(natureFirstResponding){natureTitle.text=N("Watch the forest answer","看，森林正在回应");natureDetail.text=N("Your invitation is becoming part of the forest.","你刚才的选择，正在成为森林的一部分。");}
    }
}}
