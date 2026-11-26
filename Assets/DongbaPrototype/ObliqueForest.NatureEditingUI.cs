using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace DongbaPrototype { public partial class ObliqueForest {
    enum NaturePage { Home, Object, Plants, Animals, Sleeping, Companions, Position, Remove }
    RectTransform naturePanel,natureContents,natureTab; Text natureTitle,natureDetail,natureStatus,natureTabText;
    readonly List<Button> natureButtons=new List<Button>();
    NaturePage naturePage; NatureAction natureAction; Being natureSelected;
    string natureKey,natureUIStamp=""; int natureSpecies,naturePageIndex,natureFocus;
    Vector3 natureCandidate; bool natureHasGround,natureValid,natureToggleHeld,natureBackHeld,natureConfirmHeld,natureUndoHeld;
    float natureNavAt; int natureInputFrame=-1; LineRenderer natureRing,natureRay; SpriteRenderer natureGhost;
    bool NatureOwnsInput=>NatureEditorOpen||NatureWelcomeOwnsInput||natureInputFrame==Time.frameCount;
    string N(string en,string zh)=>DongbaInterface.Choose(en,zh);
    string NatureName(Being b)=>b.animal?AnimalDisplayName(b):NaturePlantName(b.glyphKey);
    string NaturePlantName(string key){
        if(DongbaLanguage.Chinese)return dictionaryLabels.TryGetValue(key??"",out var label)?label:"植物";
        switch(key){case "plant_00":return "Pine";case "plant_01":return "Cypress";case "plant_02":return "Fir";case "plant_03":return "Rice";case "plant_04":return "Wheat";case "plant_05":return "Highland barley";case "plant_06":return "Soybean";case "plant_07":return "Garlic";case "plant_08":return "Sorghum";default:return "Plant";}
    }
    string NatureActionName(NatureAction a){switch(a){case NatureAction.Move:return N("Move & root again","邀请移栽");case NatureAction.Plant:return N("Invite a plant","邀请萌芽");case NatureAction.Sleep:return N("Rest as a seed","收拢休眠");case NatureAction.Restore:return N("Awaken this plant","唤醒这株植物");case NatureAction.Range:return N("Choose a living area","安排活动区域");case NatureAction.Leave:return N("Return to the forest","送回林中");case NatureAction.Recall:return N("Call this companion","召回这位伙伴");default:return N("Invite a new companion","招引新伙伴");}}
    void BuildNatureUI(){
        if(forestCanvas.GetComponent<GraphicRaycaster>()==null)forestCanvas.gameObject.AddComponent<GraphicRaycaster>();
        naturePanel=UIBlock("PICO Nature invitation",forestCanvas.transform,Vector2.one,Vector2.one,new Vector2(-30,-155),new Vector2(530,450));
        var paper=QuietPaper(naturePanel,.98f);paper.raycastTarget=true;DongbaUIStyle.EdgeShadow(paper,.10f);
        DongbaUIStyle.Stroke(naturePanel,new Vector2(22,-22),new Vector2(4,38),uiAccent);
        natureTitle=UILabel("Invitation title",naturePanel,new Vector2(0,1),new Vector2(0,1),new Vector2(40,-18),new Vector2(465,40),27,"");DongbaUIStyle.Serif(natureTitle);
        natureDetail=UILabel("Invitation step",naturePanel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-65),new Vector2(482,54),18,"");
        natureContents=UIBlock("Choices",naturePanel,new Vector2(0,1),new Vector2(0,1),new Vector2(22,-123),new Vector2(486,208));
        natureStatus=UILabel("Invitation feedback",naturePanel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-388),new Vector2(482,54),17,"");natureStatus.color=uiMuted;
        natureTab=UIBlock("PICO Nature bookmark",forestCanvas.transform,Vector2.zero,Vector2.zero,new Vector2(218,30),new Vector2(285,48));
        QuietPaper(natureTab,.98f).raycastTarget=true;var tabButton=natureTab.gameObject.AddComponent<Button>();tabButton.navigation=new Navigation{mode=Navigation.Mode.None};tabButton.onClick.AddListener(ToggleNatureEditor);
        natureTabText=UILabel("Nature shortcut",natureTab,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(264,36),18,"");natureTabText.alignment=TextAnchor.MiddleCenter;
        var ring=new GameObject("Nature ground preview / not a glyph");natureRing=ring.AddComponent<LineRenderer>();natureRing.sharedMaterial=Mat(Color.white);natureRing.loop=true;natureRing.useWorldSpace=false;natureRing.positionCount=48;natureRing.widthMultiplier=.035f;natureRing.sortingOrder=GroundDepthOrder(0)+5;
        var ghost=new GameObject("Nature preview / original glyph");natureGhost=ghost.AddComponent<SpriteRenderer>();ghost.AddComponent<XRUprightBillboard>().Initialize(this,cam,billboard);
        var ray=new GameObject("Nature MR pointer");natureRay=ray.AddComponent<LineRenderer>();natureRay.sharedMaterial=Mat(Color.white);natureRay.positionCount=2;natureRay.widthMultiplier=.025f;natureRay.startColor=natureRay.endColor=new Color(.35f,.25f,.12f,.4f);
        CloseNatureEditor();
    }
    void ToggleNatureEditor(){
        if(StoryModeEnabled||DongbaInterface.GameplayBlocked)return;
        if(NatureEditorOpen){CloseNatureEditor();return;}
        if(NatureAvailable&&NatureFirstHint&&!natureFirstLesson){BeginNatureFirstInvitation();return;}
        NatureEditorOpen=true;natureInputFrame=Time.frameCount;uiHelpOpen=xrHelpOpen=false;natureSelected=null;naturePage=NaturePage.Home;naturePageIndex=0;natureFocus=0;natureUIStamp="";
    }
    public bool CancelNatureEditor(){
        if(!NatureEditorOpen)return false;
        if(naturePage==NaturePage.Home)CloseNatureEditor();else {naturePage=NaturePage.Home;natureSelected=null;natureUIStamp="";natureFocus=0;naturePageIndex=0;}
        return true;
    }
    void CloseNatureEditor(){
        if(NatureEditorOpen)natureInputFrame=Time.frameCount;
        NatureEditorOpen=false;natureSelected=null;natureUIStamp="";
        if(naturePanel!=null)naturePanel.gameObject.SetActive(false);
        if(natureRing!=null)natureRing.gameObject.SetActive(false);
        if(natureGhost!=null)natureGhost.gameObject.SetActive(false);
        if(natureRay!=null)natureRay.gameObject.SetActive(false);
    }
    Button NatureButton(string label,Vector2 pos,Vector2 size,Action click,Sprite sprite=null){
        var rect=UIBlock(label,natureContents,new Vector2(0,1),new Vector2(0,1),pos,size);
        var bg=rect.gameObject.AddComponent<Image>();bg.color=new Color(.38f,.28f,.15f,.075f);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>click());
        var colors=button.colors;colors.highlightedColor=new Color(.78f,.68f,.48f);colors.pressedColor=new Color(.6f,.5f,.32f);button.colors=colors;
        if(sprite!=null){var icon=UIBlock("Dictionary glyph",rect,new Vector2(0,1),new Vector2(0,1),new Vector2(6,-6),new Vector2(48,size.y-12));var image=icon.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;}
        var text=UILabel("Choice",rect,new Vector2(0,1),new Vector2(0,1),new Vector2(sprite!=null?62:12,-4),new Vector2(size.x-(sprite!=null?67:24),size.y-8),18,label);text.alignment=TextAnchor.MiddleLeft;
        natureButtons.Add(button);return button;
    }
    void NatureGo(NaturePage page){naturePage=page;naturePageIndex=natureFocus=0;natureUIStamp="";}
    void NatureChoose(NatureAction action,Being b,string key=null,int species=0){
        natureAction=action;natureSelected=b;natureKey=key;natureSpecies=species;natureHasGround=false;NatureGo(action==NatureAction.Leave||action==NatureAction.Sleep?NaturePage.Remove:NaturePage.Position);
    }
    void NatureSelect(Being b){
        if(b==null)return;
        if(b.editBusy){NatureSay("This invitation is already on its way.","它正在回应刚才的邀请。");return;}
        if(b.angry){NatureSay("Let this frightened companion settle first.","先让这位受惊的伙伴平静下来吧。");return;}
        if(b.health<=0)return;
        natureSelected=b;NatureIdentity(b);NatureGo(NaturePage.Object);
    }
    void BuildNatureChoices(){
        foreach(Transform child in natureContents){child.gameObject.SetActive(false);Destroy(child.gameObject);}natureButtons.Clear();
        string title=N("Invite the living script","邀万物入画"),detail="";
        if(!NatureAvailable){
            detail=N("Care for the forest until it first flourishes.\nYour invitations will stay unlocked afterwards.","先照料自然，让森林第一次繁盛起来。\n此后便可反复安排，无需消耗材料。");
            NatureButton(N("Return to the forest","继续照料森林"),new Vector2(0,-44),new Vector2(486,60),CloseNatureEditor);
        }else if(naturePage==NaturePage.Home){
            detail=XRTrackingControlsCamera?N("Trigger: point and select a life. Right stick + A: menu.\nLeft stick: keep walking.","右扳机指选生命，右摇杆选择菜单、A 确认。\n左摇杆仍可走动。"):N("Select a life in the world, or invite one below.\nGaolequ can still walk while you arrange.","点选场景中的生命，或从下方邀请。\n布置时，高勒趣仍可走动与观察。");
            NatureButton(N("Plants","植物字形"),Vector2.zero,new Vector2(237,64),()=>NatureGo(NaturePage.Plants),dictionarySprites["plant_00"]);
            NatureButton(N("Animals","动物字形"),new Vector2(249,0),new Vector2(237,64),()=>NatureGo(NaturePage.Animals),dictionarySprites["animal_00"]);
            NatureButton(N("Sleeping plants","休眠植物")+"  "+sleepingPlants.Count,new Vector2(0,-76),new Vector2(237,52),()=>NatureGo(NaturePage.Sleeping));
            NatureButton(N("Forest companions","林中伙伴")+"  "+forestCompanions.Count,new Vector2(249,-76),new Vector2(237,52),()=>NatureGo(NaturePage.Companions));
        }else if(naturePage==NaturePage.Object&&natureSelected!=null){
            title=NatureName(natureSelected);detail=N("Same life, same growth. Choose an invitation.","保留它的成长与照料记录，选择一种邀请。");
            var b=natureSelected;
            NatureButton(NatureActionName(b.animal?NatureAction.Range:NatureAction.Move),Vector2.zero,new Vector2(486,64),()=>NatureChoose(b.animal?NatureAction.Range:NatureAction.Move,b),b.animal?b.renderer.sprite:b.fullGlyph);
            NatureButton(NatureActionName(b.animal?NatureAction.Leave:NatureAction.Sleep),new Vector2(0,-76),new Vector2(486,52),()=>NatureChoose(b.animal?NatureAction.Leave:NatureAction.Sleep,b));
        }else if(naturePage==NaturePage.Position||naturePage==NaturePage.Remove){
            title=NatureActionName(natureAction);detail=naturePage==NaturePage.Remove?N("This life will be saved. Invite it back whenever you like.","这位伙伴会被保留，之后可随时邀请回来。"):N("Point at open ground. The ring shows where it can settle.\nClick the ground to confirm; right-click to cancel.","指向空地，地面墨圈预览落点。\n点击地面确认，右键取消。");
            if(XRTrackingControlsCamera&&naturePage==NaturePage.Position)detail=N("Point the right controller at open ground.\nA / Trigger: confirm. B: cancel. Left stick: walk.","右手指向空地，A／右扳机确认，B 取消。\n左摇杆仍可移动高勒趣。");
            NatureButton(N("Confirm invitation","确认邀请"),Vector2.zero,new Vector2(486,52),CommitNatureChoice);
            NatureButton(N("Cancel","取消"),new Vector2(0,-64),new Vector2(486,52),()=>CancelNatureEditor());
        }else{
            bool plant=naturePage==NaturePage.Plants||naturePage==NaturePage.Sleeping;
            bool archive=naturePage==NaturePage.Sleeping||naturePage==NaturePage.Companions;
            var lives=plant?sleepingPlants:forestCompanions;
            int count=archive?lives.Count:plant?9:AnimalKinds;int pages=Mathf.Max(1,(count+5)/6);naturePageIndex=Mathf.Clamp(naturePageIndex,0,pages-1);
            title=archive?N(plant?"Sleeping plants":"Forest companions",plant?"休眠植物":"林中伙伴"):N(plant?"Plant pictographs":"Animal pictographs",plant?"植物字形":"动物字形");
            detail=archive?N("Choose the same individual to return.","选择原来的个体，让它重新回来。"):N("Existing dictionary artwork · choose, then place.","沿用现有字典素材，选中后指定位置。");
            if(count==0)detail=N("No one is resting here yet.","这里还没有休眠或回林的伙伴。");
            for(int slot=0;slot<6;slot++){
                int index=naturePageIndex*6+slot;if(index>=count)break;
                Being b=archive?lives[index]:null;string key=archive?b.glyphKey:(plant?"plant_":"animal_")+index.ToString("00");
                Sprite icon=archive?(b.animal?b.renderer.sprite:b.fullGlyph):dictionarySprites[key];
                string name=archive?NatureName(b)+" #"+NatureIdentity(b):plant?NaturePlantName(key):DongbaLanguage.Chinese?animalNamesZh[index]:animalNamesEn[index];
                NatureButton(name,new Vector2(slot%2*249,-(slot/2)*55),new Vector2(237,49),()=>NatureChoose(archive?(plant?NatureAction.Restore:NatureAction.Recall):(plant?NatureAction.Plant:NatureAction.Invite),b,key,index),icon);
            }
            if(pages>1)NatureButton(N("Next page","下一页")+"  "+(naturePageIndex+1)+" / "+pages,new Vector2(249,-168),new Vector2(237,34),()=>{naturePageIndex=(naturePageIndex+1)%pages;natureUIStamp="";});
        }
        natureTitle.text=title;natureDetail.text=detail;
        bool placing=naturePage==NaturePage.Position||naturePage==NaturePage.Remove;
        bool catalog=naturePage==NaturePage.Plants||naturePage==NaturePage.Animals||naturePage==NaturePage.Sleeping||naturePage==NaturePage.Companions;
        float footer=catalog?211:145;
        naturePanel.sizeDelta=new Vector2(530,placing?322:catalog?450:384);
        natureStatus.rectTransform.anchoredPosition=new Vector2(24,placing?-260:catalog?-388:-322);
        if(NatureAvailable&&!placing){
            NatureButton(N("Back","返回"),new Vector2(0,-footer),new Vector2(100,38),()=>CancelNatureEditor());
            NatureButton(N("Undo","撤销")+"  "+natureHistory.Count,new Vector2(110,-footer),new Vector2(166,38),()=>{UndoNature();natureUIStamp="";});
            NatureButton(N("Close","收起"),new Vector2(286,-footer),new Vector2(200,38),CloseNatureEditor);
        }
        natureFocus=Mathf.Clamp(natureFocus,0,Mathf.Max(0,natureButtons.Count-1));
    }
    void CommitNatureChoice(){
        if(naturePage!=NaturePage.Position&&naturePage!=NaturePage.Remove)return;
        if(naturePage==NaturePage.Position&&(!natureHasGround||!natureValid))return;
        if(NatureRequest(natureAction,natureSelected,natureKey,natureSpecies,natureCandidate))NatureGo(NaturePage.Home);
    }
    void UpdateNatureUI(){
        if(!natureReady)return;
        bool showing=!StoryModeEnabled&&!DongbaInterface.GameplayBlocked&&(!XRTrackingControlsCamera||XRExperienceReady);
        natureTab.gameObject.SetActive(showing&&!NatureEditorOpen);
        natureTabText.text=NatureAvailable?N(XRTrackingControlsCamera?"L stick click · Invite":"B   Invite & arrange",XRTrackingControlsCamera?"按左摇杆 · 邀万物入画":"B   邀万物入画"):N("Care unlocks invitations","照料至繁盛 · 解锁邀请");
        naturePanel.gameObject.SetActive(showing&&NatureEditorOpen);
        if(XRTrackingControlsCamera){
            naturePanel.anchorMin=naturePanel.anchorMax=naturePanel.pivot=Vector2.one*.5f;naturePanel.anchoredPosition=new Vector2(0,-125);naturePanel.localScale=Vector3.one*1.18f;
            natureTab.anchorMin=natureTab.anchorMax=natureTab.pivot=Vector2.one*.5f;natureTab.anchoredPosition=new Vector2(0,-295);
        }
        else{naturePanel.anchorMin=naturePanel.anchorMax=naturePanel.pivot=Vector2.one;naturePanel.anchoredPosition=new Vector2(-30,-155);naturePanel.localScale=Vector3.one;natureTab.anchorMin=natureTab.anchorMax=natureTab.pivot=Vector2.zero;natureTab.anchoredPosition=new Vector2(218,30);}
        UpdateNatureWelcome(showing);
        if(!showing||!NatureEditorOpen){natureRing.gameObject.SetActive(false);natureGhost.gameObject.SetActive(false);natureRay.gameObject.SetActive(false);return;}
        string stamp=naturePage+"|"+naturePageIndex+"|"+natureUnlocked+"|"+DongbaLanguage.Revision+"|"+sleepingPlants.Count+"|"+forestCompanions.Count+"|"+natureHistory.Count+"|"+(natureSelected==null?0:NatureIdentity(natureSelected));
        if(natureUIStamp!=stamp){BuildNatureChoices();natureUIStamp=stamp;if(XRTrackingControlsCamera&&xrHudInk!=null)foreach(var graphic in naturePanel.GetComponentsInChildren<Graphic>(true))graphic.material=xrHudInk;}
        ApplyNatureFirstStep();
        for(int i=0;i<natureButtons.Count;i++)natureButtons[i].targetGraphic.color=XRTrackingControlsCamera&&i==natureFocus?new Color(.58f,.39f,.16f,.28f):new Color(.38f,.28f,.15f,.075f);
        natureStatus.text=clock<natureMessageUntil?N(natureMessageEn,natureMessageZh):N(XRTrackingControlsCamera?"Right stick: choose · A: select · X: undo":"WASD: walk · Ctrl+Z: undo · B: close",XRTrackingControlsCamera?"右摇杆选择 · A 确认 · X 撤销":"WASD 走动 · Ctrl+Z 撤销 · B 收起");
        if(natureMotions.Count>0&&clock>=natureMessageUntil)natureStatus.text=N("Invitations on their way: ","正在回应：")+natureMotions.Count+N(" · You may keep arranging."," · 可继续安排其他伙伴。");
        bool position=naturePage==NaturePage.Position;
        natureRing.gameObject.SetActive(position&&natureHasGround);natureGhost.gameObject.SetActive(position&&natureHasGround);
        if(position&&natureHasGround){
            bool animal=natureAction==NatureAction.Range||natureAction==NatureAction.Invite||natureAction==NatureAction.Recall;
            bool tree=natureSelected!=null?natureSelected.tree:natureKey=="plant_00"||natureKey=="plant_01"||natureKey=="plant_02";
            natureValid=NaturePlacement(natureCandidate,natureSelected,animal,tree,out string reason);
            if(!natureValid)natureStatus.text=reason;
            Color color=natureValid?new Color(.27f,.43f,.26f,.75f):new Color(.64f,.20f,.12f,.8f);
            natureRing.startColor=natureRing.endColor=color;natureRing.transform.position=natureCandidate+Vector3.up*.08f;
            float radius=animal?1.5f:tree?.8f:.42f;
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;natureRing.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            Sprite sprite=natureSelected!=null?(animal?natureSelected.renderer.sprite:natureSelected.fullGlyph):dictionarySprites[natureKey];natureGhost.sprite=sprite;
            natureGhost.transform.SetPositionAndRotation(natureCandidate,billboard);
            natureGhost.transform.localScale=natureSelected!=null?natureSelected.initialScale*(animal?natureSelected.birth:natureSelected.targetScale):Vector3.one*((animal?1:tree?2.3f:.65f)/sprite.bounds.size.y);
            natureGhost.color=new Color(color.r,color.g,color.b,.4f);natureGhost.sortingOrder=DepthOrder(natureCandidate.z)+10;
        }
    }
    bool NatureGround(Ray ray,out Vector3 p){var plane=new Plane(Vector3.up,Vector3.zero);if(plane.Raycast(ray,out float distance)&&distance>0&&distance<300){p=ray.GetPoint(distance);p.y=0;return true;}p=Vector3.zero;return false;}
    Being NaturePick(Ray ray){
        Being picked=null;float best=float.MaxValue;
        foreach(var list in new[]{plants,animals})foreach(var b in list){
            if(b.health<=0||!b.root.gameObject.activeInHierarchy||b.animal&&b.migrationState==2)continue;
            Bounds bounds=b.renderer.bounds;if(b.tree){bounds=b.renderer.bounds;foreach(var leaf in b.leafImages)if(leaf.enabled)bounds.Encapsulate(leaf.bounds);}
            bounds.Expand(.16f);
            if(bounds.IntersectRay(ray,out float d)&&d<best){best=d;picked=b;}
        }
        return picked;
    }
    void UpdateNaturePC(){
        if(!natureReady||XRTrackingControlsCamera)return;
        var k=Keyboard.current;
        if(NatureWelcomeOwnsInput){natureInputFrame=Time.frameCount;if(k!=null&&(k.bKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame))BeginNatureFirstInvitation();return;}
        if(k!=null){if(k.bKey.wasPressedThisFrame)ToggleNatureEditor();if(NatureAvailable&&(k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed)&&k.zKey.wasPressedThisFrame)UndoNature();}
        if(!NatureEditorOpen)return;
        var mouse=Mouse.current;if(mouse==null)return;
        if(mouse.rightButton.wasPressedThisFrame){CancelNatureEditor();return;}
        bool overUI=RectTransformUtility.RectangleContainsScreenPoint(naturePanel,mouse.position.ReadValue(),forestCanvas.worldCamera)||(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject());
        var ray=cam.ScreenPointToRay(mouse.position.ReadValue());
        // Moving the pointer onto Confirm must retain the chosen ground, not select
        // an unrelated position underneath the paper menu.
        if(!overUI)natureHasGround=NatureGround(ray,out natureCandidate);
        UpdateNatureUI();
        if(mouse.leftButton.wasPressedThisFrame&&!overUI&&NatureAvailable){if(naturePage==NaturePage.Position)CommitNatureChoice();else if(naturePage==NaturePage.Home||naturePage==NaturePage.Object)NatureSelect(NaturePick(ray));}
    }
    void ReadNatureXR(InputDevice left,InputDevice right,bool ready){
        bool toggle=XRButton(left,CommonUsages.primary2DAxisClick),back=XRButton(right,CommonUsages.secondaryButton);
        bool trigger=XRButton(right,CommonUsages.triggerButton);if(right.TryGetFeatureValue(CommonUsages.trigger,out float pressure))trigger|=pressure>.65f;
        bool confirm=XRButton(right,CommonUsages.primaryButton)||trigger;
        bool undo=XRButton(left,CommonUsages.primaryButton);right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 navigation);
        Ray ray=new Ray(cam.transform.position,cam.transform.forward);
        if(right.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p)&&right.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q)){
            var origin=cam.transform.parent;ray=new Ray(origin!=null?origin.TransformPoint(p):p,origin!=null?origin.TransformDirection(q*Vector3.forward):q*Vector3.forward);
        }
        ProcessNatureXR(toggle,back,confirm,undo,navigation,ray,ready,trigger);
    }
    void ProcessNatureXR(bool toggle,bool back,bool confirm,bool undo,Vector2 navigation,Ray ray,bool ready,bool selectWorld=false){
        bool toggleDown=ready&&toggle&&!natureToggleHeld,backDown=ready&&back&&!natureBackHeld,confirmDown=ready&&confirm&&!natureConfirmHeld,undoDown=ready&&undo&&!natureUndoHeld;
        natureToggleHeld=toggle;natureBackHeld=back;natureConfirmHeld=confirm;natureUndoHeld=undo;
        if(NatureWelcomeOwnsInput){natureInputFrame=Time.frameCount;if(backDown)DeferNatureWelcome();else if(confirmDown||toggleDown)BeginNatureFirstInvitation();return;}
        if(toggleDown)ToggleNatureEditor();if(!NatureEditorOpen)return;natureInputFrame=Time.frameCount;
        if(backDown){CancelNatureEditor();return;}
        natureHasGround=NatureGround(ray,out natureCandidate);
        bool aiming=naturePage==NaturePage.Home||naturePage==NaturePage.Object||naturePage==NaturePage.Position;
        natureRay.gameObject.SetActive(aiming&&natureHasGround);natureRay.SetPosition(0,ray.origin);natureRay.SetPosition(1,natureHasGround?natureCandidate:ray.GetPoint(3));
        if(navigation.sqrMagnitude>.4f&&clock>=natureNavAt){natureFocus=(natureFocus+(navigation.y>0||navigation.x<0?-1:1)+Mathf.Max(1,natureButtons.Count))%Mathf.Max(1,natureButtons.Count);natureNavAt=clock+.25f;}
        UpdateNatureUI();
        if(undoDown){UndoNature();natureUIStamp="";return;}
        if(confirmDown){
            if(naturePage==NaturePage.Position){CommitNatureChoice();return;}
            // Pointing at a life selects it. Aim away / use the stick to choose a menu action.
            var b=aiming&&NatureAvailable?NaturePick(ray):null;
            if(selectWorld&&b!=null&&(naturePage==NaturePage.Home||naturePage==NaturePage.Object))NatureSelect(b);
            else if(natureButtons.Count>0)natureButtons[Mathf.Clamp(natureFocus,0,natureButtons.Count-1)].onClick.Invoke();
        }
    }
}}
