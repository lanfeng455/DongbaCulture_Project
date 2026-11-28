using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype { public partial class ObliqueForest {
    RectTransform xrBookStory,xrBookMenu;
    readonly Text[] xrGuideKeys=new Text[9],xrGuideRows=new Text[9];
    Text xrGuideBack;
    string xrBookStamp;

    void XRRect(RectTransform rect,float x,float y,float width,float height){
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);
    }
    void XRHeadingText(Text label,int size){
        XRReadingText(label,size,DongbaUIStyle.HeadingFont);
    }
    void XRStyleGraphics(Transform root){
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.material=xrHudInk;
    }
    void EnsureXRNotes(){
        if(xrSummary!=null)return;
        xrHudInk=new Material(Shader.Find("Dongba/Headset Paper UI"));
        xrSideFold=UIBlock("PICO paper fold",forestCanvas.transform,new Vector2(.5f,1),Vector2.one*.5f,new Vector2(0,-XRReadingCanvasHeight),new Vector2(1000,2));
        var fold=xrSideFold.gameObject.AddComponent<Image>();fold.sprite=DongbaUIStyle.BrushSprite;
        fold.color=new Color(.38f,.28f,.15f,.24f);fold.raycastTarget=false;fold.material=xrHudInk;

        // Adapt the existing PC chapter card, not a replacement text-only strip.
        EnsureQuietObjective();chapterTab.enabled=false;
        xrObjectiveNote=(RectTransform)uiObjectivePanel.transform;xrObjectiveNote.name="PICO chapter bookmark";
        XRRect(xrObjectiveNote,50,-28,900,182);
        var ribbon=(RectTransform)chapterNumber.transform.parent;XRRect(ribbon,18,4,64,160);
        XRTextBox(chapterNumber,4,-42,56,58,36);chapterNumber.alignment=TextAnchor.MiddleCenter;XRHeadingText(chapterNumber,36);
        XRTextBox(objectiveTag,108,-12,748,42,24);
        XRTextBox(journeyObjective,106,-54,748,66,36);XRHeadingText(journeyObjective,36);
        XRTextBox(objectiveDetail,108,-124,748,46,27);
        var expand=xrObjectiveNote.Find("Expand chapter note");if(expand!=null)expand.gameObject.SetActive(false);
        xrSummary=journeyObjective;

        // Preserve the PC selected-glyph leaf, heading, cooldown strip and result.
        xrContextNote=(RectTransform)promptPanel.transform;xrContextNote.name="PICO nearby glyph card";
        XRRect(xrContextNote,50,-234,900,258);
        var sample=(RectTransform)interactionGlyph.transform.parent;XRRect(sample,-8,-16,210,226);
        XRRect(interactionGlyph.rectTransform,26,-20,158,172);
        foreach(Transform child in sample)if(child.name=="Ink rule / interface")XRRect((RectTransform)child,26,-205,158,2);
        XRTextBox(hudPromptTitle,238,-14,624,66,36);XRHeadingText(hudPromptTitle,36);
        XRTextBox(hudPrompt,240,-96,620,100,28);hudPrompt.lineSpacing=1.2f;
        XRTextBox(interactionResult,240,-207,620,44,25);
        int ruleIndex=0;
        foreach(Transform child in xrContextNote)if(child.name=="Ink rule / interface"){
            XRRect((RectTransform)child,238,ruleIndex++==0?-80:-198,624,ruleIndex==1?2:3);
        }
        XRRect(interactionWait.rectTransform,0,0,624,3);xrContext=hudPrompt;

        xrHealthNote=(RectTransform)hudHealth.transform.parent;xrHealthNote.name="PICO health ink marks";
        XRRect(xrHealthNote,50,-510,350,64);XRTextBox(hudHealth,18,-10,100,44,27);
        for(int i=0;i<uiHealthPips.Length;i++){
            var dot=uiHealthPips[i].rectTransform;dot.anchorMin=dot.anchorMax=new Vector2(0,1);dot.pivot=Vector2.one*.5f;
            dot.anchoredPosition=new Vector2(144+i*40,-32);dot.sizeDelta=new Vector2(23,29);
        }
        xrControlsTab=(RectTransform)uiHelpShortcut.transform;xrControlsTab.name="PICO controls bookmark";
        XRRect(xrControlsTab,50,-612,260,64);xrPanelStatus=xrControlsTab.GetComponentInChildren<Text>();
        XRTextBox(xrPanelStatus,26,-6,218,50,27);XRHeadingText(xrPanelStatus,27);
        foreach(Transform child in xrControlsTab)if(child.name=="Ink rule / interface")XRRect((RectTransform)child,16,-14,3,36);

        xrHelpNote=DongbaUIStyle.OpenManuscript(forestCanvas.transform,"PICO open controls manuscript",new Vector2(.5f,1),new Vector2(0,-350),out xrBookStory,out xrBookMenu);
        var caption=DongbaUIStyle.Label("Page caption",xrBookMenu,"DONGBA / THE LIVING SCRIPT",new Vector2(34,-22),new Vector2(564,42),24);XRReadingText(caption,24);caption.color=uiMuted;
        xrGuideTitle=DongbaUIStyle.Label("Controls heading",xrBookMenu,"",new Vector2(34,-62),new Vector2(564,68),38);XRHeadingText(xrGuideTitle,38);
        DongbaUIStyle.Stroke(xrBookMenu,new Vector2(34,-129),new Vector2(564,2),new Color(.33f,.23f,.13f,.55f));
        for(int i=0;i<xrGuideKeys.Length;i++){
            float y=-151-i*47;
            var badge=DongbaUIStyle.Rect("Controller key "+i,xrBookMenu,new Vector2(0,1),new Vector2(0,1),new Vector2(34,y),new Vector2(132,42));DongbaUIStyle.Plate(badge,new Color(.88f,.85f,.77f));
            xrGuideKeys[i]=DongbaUIStyle.Label("Key",badge,"",new Vector2(6,-2),new Vector2(120,40),24);XRReadingText(xrGuideKeys[i],24);xrGuideKeys[i].alignment=TextAnchor.MiddleCenter;xrGuideKeys[i].color=uiAccent;
            xrGuideRows[i]=DongbaUIStyle.Label("Action "+i,xrBookMenu,"",new Vector2(181,y-1),new Vector2(417,41),26);XRReadingText(xrGuideRows[i],26);xrGuideRows[i].alignment=TextAnchor.MiddleLeft;
        }
        xrGuideBack=DongbaUIStyle.Label("Close the book",xrBookMenu,"",new Vector2(52,-590),new Vector2(536,56),27);XRHeadingText(xrGuideBack,27);xrGuideBack.color=uiAccent;
        DongbaUIStyle.Stroke(xrBookMenu,new Vector2(34,-601),new Vector2(4,28),uiAccent);
        foreach(var panel in new[]{xrObjectiveNote,xrContextNote,xrHealthNote,xrControlsTab,xrHelpNote})XRStyleGraphics(panel);
        RefreshXRBook();
    }
    void RefreshXRChapter(bool visible){
        string value=StoryModeEnabled?QuietStoryObjective():SandboxNote;
        xrObjectiveNote.gameObject.SetActive(visible&&!string.IsNullOrEmpty(value)&&!(journeyStage==JourneyStage.RestorationComplete&&endingAcknowledged));
        var localized=DongbaLanguage.Text(value);int split=localized.IndexOf('\n');
        journeyObjective.text=split>=0?localized.Substring(0,split):localized;
        objectiveDetail.text=split>=0?localized.Substring(split+1):"";
        objectiveTag.text=InterfaceChapter;chapterNumber.text=InterfaceChapterNumber;objectiveFade.alpha=1;
        var details=objectiveDetail.GetComponent<CanvasGroup>();if(details!=null)details.alpha=1;
    }
    void RefreshXRHealth(bool visible){
        bool threatened=damageCooldown>0||health<5;
        if(!threatened&&visible){foreach(var animal in animals)if(animal.health>0&&animal.angry&&Vector3.Distance(Feet,animal.root.position)<4){threatened=true;break;}}
        if(!threatened&&visible&&storyBoar!=null&&storyBoar.health>0&&Vector3.Distance(Feet,storyBoar.root.position)<6)threatened=true;
        xrHealthNote.gameObject.SetActive(visible&&threatened);hudHealth.text=N("Health","生命");
        for(int i=0;i<uiHealthPips.Length;i++)uiHealthPips[i].color=i<health?DongbaUIStyle.Accent:new Color(.37f,.30f,.21f,.2f);
    }
    void RefreshXRBook(){
        string stamp=InterfaceChapter+"|"+InterfaceFatherPresent+"|"+DongbaLanguage.Revision+"|"+ForestCareAvailable+"|"+ForestDamageAvailable;
        if(xrBookStamp==stamp)return;
        if(xrBookStamp!=stamp){
            xrBookStamp=stamp;
            foreach(Transform child in xrBookStory){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            DongbaUIStyle.Stroke(xrBookStory,new Vector2(329,-25),new Vector2(2,598),new Color(.27f,.18f,.10f,.2f));
            DongbaInterface.Instance.BuildStoryLeaf(xrBookStory,this);
            foreach(var label in xrBookStory.GetComponentsInChildren<Text>()){
                int size=Mathf.Max(24,label.fontSize);XRReadingText(label,size);
                if(label.name=="Tale title"||label.name=="Chapter number")XRHeadingText(label,size);
                if(label.name=="Current chapter")label.rectTransform.sizeDelta=new Vector2(212,58);
                if(label.name=="Cultural context"){label.rectTransform.anchoredPosition=new Vector2(28,-554);label.rectTransform.sizeDelta=new Vector2(292,84);}
            }
            XRStyleGraphics(xrBookStory);
        }
        xrGuideTitle.text=N("Controls","操作说明");xrGuideBack.text=N("B   Back to the forest","B   回到森林");
        string[] keys={N("L stick","左摇杆"),"A",N("R trigger","右扳机"),"X",N("R click","按右摇杆"),N("L click","按左摇杆"),N("Hold Y","按住 Y"),N("Hold X","按住 X"),N("Hold A","按住 A")};
        string[] actions={N("Move","移动"),N("Jump / dialogue","跳跃／继续对白"),ForestDamageAvailable?N("Chop / attack","砍伐／攻击"):N("Axe put away","此阶段收起斧头"),ForestCareAvailable?N("Care / replant / feed","照料／补种／喂养"):N("Care on your return","归来后解锁培育"),N("Chinese / English","切换中英"),StoryModeEnabled?N("Invitations: sandbox only","自然邀请：沙盒开放"):N("Invite & arrange","邀万物入画"),N("Reposition the forest","重新放置森林"),N("Switch mode (reset)","切换模式（重置）"),N("Restart this run","重新开始本局")};
        for(int i=0;i<keys.Length;i++){xrGuideKeys[i].text=keys[i];xrGuideRows[i].text=actions[i];}
    }
}}
