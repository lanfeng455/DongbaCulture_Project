using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        float xrUnits;
        Text xrSummary, xrContext, xrControllerHints, xrPanelStatus, xrHealth, xrGuideTitle, xrGuideActions, xrGuideSettings;
        RectTransform xrObjectiveNote, xrContextNote, xrHelpNote, xrControlsTab, xrHealthNote, xrSideFold;
        Material xrHudInk;
        bool xrHelpOpen, xrHelpHeld;
        float xrModeHold,xrRestartHold;

        void ConfigureWorldXRHUD(float units)
        {
            if (forestCanvas == null) return;
            xrUnits = units;
            forestCanvas.renderMode = RenderMode.WorldSpace;
            forestCanvas.worldCamera = cam;
            // The upright sheet is behind the forest, not an overlay over its trees.
            forestCanvas.sortingOrder = DepthOrder(ForestHalfDepth + 1f) - 100;
            var scaler = forestCanvas.GetComponent<CanvasScaler>();
            scaler.enabled = true; scaler.dynamicPixelsPerUnit = 4;
            scaler.referencePixelsPerUnit = 100;
            var rect = (RectTransform)forestCanvas.transform;
            EnsureXRReadingAnchor();
            rect.SetParent(xrReadingAnchor, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,1);
            rect.sizeDelta = new Vector2(1000, XRReadingCanvasHeight);
            rect.localScale = Vector3.one * (XRReadingWidthMetres / 1000);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            forestCanvas.gameObject.SetActive(false);
        }

        RectTransform XRNote(string name, Vector2 position, Vector2 size)
        {
            var rect = UIBlock("PICO " + name, forestCanvas.transform, new Vector2(.5f,1), Vector2.one*.5f, position, size);
            var paper = rect.gameObject.AddComponent<Image>();
            paper.sprite = DongbaUIStyle.ManuscriptPaper; paper.type = Image.Type.Sliced;
            paper.color = new Color(1,1,1,.94f); paper.raycastTarget = false; paper.material = xrHudInk;
            return rect;
        }

        Text XRNoteText(string name, RectTransform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label=UILabel(name,parent,Vector2.one*.5f,Vector2.one*.5f,position,size,fontSize,"");
            label.alignment=TextAnchor.MiddleLeft;XRReadingText(label,fontSize);
            return label;
        }

        void EnsureXRNotes()
        {
            if (xrSummary != null) return;
            xrHudInk = new Material(Shader.Find("Dongba/Headset Paper UI"));
            // An ordinary paper crease, not an invented Dongba pictograph.
            // It touches the far ground edge and makes the floor/side join legible.
            xrSideFold=UIBlock("PICO paper fold",forestCanvas.transform,new Vector2(.5f,1),Vector2.one*.5f,new Vector2(0,-XRReadingCanvasHeight),new Vector2(1000,2));
            var fold=xrSideFold.gameObject.AddComponent<Image>();fold.sprite=DongbaUIStyle.BrushSprite;
            fold.color=new Color(.38f,.28f,.15f,.24f);fold.raycastTarget=false;fold.material=xrHudInk;
            xrObjectiveNote=XRNote("chapter note",new Vector2(0,-180),new Vector2(900,108));
            xrSummary=XRNoteText("Chapter",xrObjectiveNote,new Vector2(0,-3),new Vector2(824,84),32);
            var stroke=DongbaUIStyle.Stroke(xrObjectiveNote,new Vector2(30,-20),new Vector2(4,68),new Color(.49f,.22f,.12f));stroke.material=xrHudInk;
            xrContextNote=XRNote("nearby note",new Vector2(0,-310),new Vector2(900,120));
            xrContext=XRNoteText("Nearby action",xrContextNote,Vector2.zero,new Vector2(824,100),30);
            xrHelpNote=XRNote("controls sheet",new Vector2(0,-320),new Vector2(900,600));
            xrGuideTitle=XRNoteText("Controls heading",xrHelpNote,new Vector2(0,242),new Vector2(820,58),38);
            xrControllerHints=XRNoteText("Movement and language",xrHelpNote,new Vector2(-218,52),new Vector2(390,262),28);
            xrControllerHints.alignment=TextAnchor.UpperLeft;xrControllerHints.lineSpacing=1.2f;
            xrGuideActions=XRNoteText("Inviting and arranging",xrHelpNote,new Vector2(218,52),new Vector2(390,262),28);
            xrGuideActions.alignment=TextAnchor.UpperLeft;xrGuideActions.lineSpacing=1.2f;
            xrGuideSettings=XRNoteText("Placement and reset",xrHelpNote,new Vector2(0,-199),new Vector2(820,150),26);
            xrGuideSettings.lineSpacing=1.2f;
            var rule=DongbaUIStyle.Stroke(xrHelpNote,new Vector2(38,-408),new Vector2(824,2),new Color(.38f,.28f,.15f,.25f));rule.material=xrHudInk;
            xrControlsTab=XRNote("controls tab",new Vector2(-232,-430),new Vector2(436,68));
            xrPanelStatus=XRNoteText("Help tab",xrControlsTab,Vector2.zero,new Vector2(380,56),28);
            xrHealthNote=XRNote("health note",new Vector2(0,-525),new Vector2(900,58));
            xrHealth=XRNoteText("Health",xrHealthNote,Vector2.zero,new Vector2(824,48),28);
        }

        void UpdateXRHelpInput(bool pressed, bool ready)
        {
            if(ready&&pressed&&!xrHelpHeld)xrHelpOpen=!xrHelpOpen;
            xrHelpHeld=pressed;
            if(!ready)xrHelpOpen=false;
        }

        public void UpdateXRHUD(bool placed, string status)
        {
            if(forestCanvas==null||forestCanvas.renderMode!=RenderMode.WorldSpace)return;
            if(placed&&XRExperienceReady&&!xrReadingAnchored)AnchorXRReadingFrame();
            if(forestSubtitles!=null){
                forestSubtitles.ConfigureHeadsetPresentation(cam,xrUnits);
                forestSubtitles.SetHeadsetPresentationVisible(placed&&XRExperienceReady&&!xrHelpOpen&&!NatureEditorOpen&&!NatureWelcomeVisible);
            }
            bool ready=placed&&XRExperienceReady;
            forestCanvas.gameObject.SetActive(ready);
            if(!ready){xrHelpOpen=false;return;}
            foreach(Transform child in forestCanvas.transform)
                if(!child.name.StartsWith("PICO ")&&child.name!="DialogueCanvas")child.gameObject.SetActive(false);
            EnsureXRNotes();
            bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
            bool exploring=InterfaceExploring&&!ReelPresentationVisible&&!dialogue&&!NatureEditorOpen&&!NatureWelcomeVisible&&!NatureAnswerNoticeVisible;
            if(!exploring)xrHelpOpen=false;
            xrSideFold.gameObject.SetActive(exploring||NatureEditorOpen||NatureWelcomeVisible||NatureAnswerNoticeVisible);
            string objective=StoryModeEnabled?DongbaLanguage.Text(QuietStoryObjective()):DongbaInterface.Choose("Sandbox Mode","沙盒模式")+" · "+SandboxState;
            xrSummary.text=objective;
            xrObjectiveNote.gameObject.SetActive(exploring&&!NatureAnswerNoticeVisible&&!string.IsNullOrEmpty(objective)&&!xrHelpOpen);
            xrContextNote.gameObject.SetActive(exploring&&ForestTargetAvailable(interactionTarget)&&!xrHelpOpen);
            if(interactionTarget!=null){
                string name=interactionTarget.animal?AnimalDisplayName(interactionTarget):DongbaLanguage.Text(interactionTarget.health<=0?"Tree stump":"Forest tree");
                string care=interactionTarget.animal?DongbaInterface.Choose("Feed","喂养"):interactionTarget.health<=0?DongbaInterface.Choose("Replant","补种"):DongbaInterface.Choose("Care","培育");
                string actions=ForestCareAvailable?"X  "+care:"";
                if(ForestDamageAvailable&&interactionTarget.health>0)actions+=(actions.Length>0?"    ":"")+DongbaInterface.Choose("R Trigger  Axe","右扳机  挥斧");
                xrContext.text=name+"\n"+actions;
            }
            xrHelpNote.gameObject.SetActive(exploring&&xrHelpOpen);
            xrGuideTitle.text=DongbaInterface.Choose("Forest controls", "林间操作");
            xrControllerHints.text=DongbaInterface.Choose(
                "Left stick   Move\nA   Jump / dialogue\n"+XRForestActionGuide+"\nR stick click   Language",
                "左摇杆　移动\nA　跳跃／继续对白\n"+XRForestActionGuide+"\n按下右摇杆　切换中英");
            xrGuideActions.text=DongbaInterface.Choose(
                "L stick click   Invite nature\nIn the invitation menu:\nRight stick   Choose\nA   Confirm    B   Back\nX   Undo",
                "按下左摇杆　邀万物入画\n邀请菜单中：\n右摇杆　选择\nA　确认　 B　返回\nX　撤销");
            if(StoryModeEnabled)xrGuideActions.text=DongbaInterface.Choose(
                "Invitations belong to sandbox.\n\nFollow the story.\nCare returns in the final act.",
                "自然邀请在沙盒中开放。\n\n跟随故事前行，\n在终章重新照料森林。");
            xrGuideSettings.text=DongbaInterface.Choose(
                "Hold Y   Place forest and side UI again\nHold X   Switch mode (reset)    Hold A   Restart\nB   Close controls",
                "按住 Y　重新放置森林与侧面界面\n按住 X　切换模式（重置）　按住 A　重新开始\nB　收起操作说明");
            xrPanelStatus.text=DongbaInterface.Choose(xrHelpOpen?"B   Close controls":"B   Controls",xrHelpOpen?"B   收起操作说明":"B   操作说明");
            xrControlsTab.gameObject.SetActive(exploring&&!xrHelpOpen);
            xrPanelStatus.gameObject.SetActive(exploring&&!xrHelpOpen);
            if(natureTab!=null&&xrHelpOpen)natureTab.gameObject.SetActive(false);
            xrHealth.text=DongbaInterface.Choose("Health  ","生命  ")+health+" / 5";
            xrHealthNote.gameObject.SetActive(exploring&&health<5&&!xrHelpOpen);
            if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);
        }
    }
}
