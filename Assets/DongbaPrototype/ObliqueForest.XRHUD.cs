using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        float xrUnits;
        Text xrSummary, xrContext, xrControllerHints, xrPanelStatus, xrHealth;
        RectTransform xrObjectiveNote, xrContextNote, xrHelpNote, xrControlsTab, xrHealthNote;
        Material xrHudInk;
        bool xrHelpOpen, xrHelpHeld;
        float xrModeHold,xrRestartHold;

        void ConfigureWorldXRHUD(float units)
        {
            if (forestCanvas == null) return;
            xrUnits = units;
            forestCanvas.renderMode = RenderMode.WorldSpace;
            forestCanvas.worldCamera = cam;
            var scaler = forestCanvas.GetComponent<CanvasScaler>();
            scaler.enabled = true; scaler.dynamicPixelsPerUnit = 4;
            scaler.referencePixelsPerUnit = 100;
            var rect = (RectTransform)forestCanvas.transform;
            EnsureXRReadingAnchor();
            rect.SetParent(xrReadingAnchor, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1000, 760);
            rect.localScale = Vector3.one * (1.18f / 1000);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            forestCanvas.gameObject.SetActive(false);
        }

        RectTransform XRNote(string name, Vector2 position, Vector2 size)
        {
            var rect = UIBlock("PICO " + name, forestCanvas.transform, Vector2.one*.5f, Vector2.one*.5f, position, size);
            var paper = rect.gameObject.AddComponent<Image>();
            paper.sprite = DongbaUIStyle.ManuscriptPaper; paper.type = Image.Type.Sliced;
            paper.color = new Color(1,1,1,.94f); paper.raycastTarget = false; paper.material = xrHudInk;
            return rect;
        }

        Text XRNoteText(string name, RectTransform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label=UILabel(name,parent,Vector2.one*.5f,Vector2.one*.5f,position,size,fontSize,"");
            label.alignment=TextAnchor.MiddleCenter;label.material=xrHudInk;
            return label;
        }

        void EnsureXRNotes()
        {
            if (xrSummary != null) return;
            xrHudInk = new Material(Shader.Find("Dongba/Headset Paper UI"));
            xrObjectiveNote=XRNote("chapter note",new Vector2(0,300),new Vector2(760,112));
            xrSummary=XRNoteText("Chapter",xrObjectiveNote,new Vector2(0,-2),new Vector2(700,88),30);
            var stroke=DongbaUIStyle.Stroke(xrObjectiveNote,new Vector2(28,-9),new Vector2(58,3),new Color(.49f,.22f,.12f));stroke.material=xrHudInk;
            xrContextNote=XRNote("nearby note",new Vector2(0,-230),new Vector2(700,126));
            xrContext=XRNoteText("Nearby action",xrContextNote,Vector2.zero,new Vector2(650,108),30);
            xrHelpNote=XRNote("folded controls",new Vector2(0,0),new Vector2(760,520));
            xrControllerHints=XRNoteText("Controller guide",xrHelpNote,Vector2.zero,new Vector2(688,480),28);
            xrControlsTab=XRNote("controls tab",new Vector2(0,-360),new Vector2(300,52));
            xrPanelStatus=XRNoteText("Help tab",xrControlsTab,Vector2.zero,new Vector2(274,40),25);
            xrHealthNote=XRNote("health note",new Vector2(0,215),new Vector2(230,48));
            xrHealth=XRNoteText("Health",xrHealthNote,Vector2.zero,new Vector2(204,36),25);
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
                forestSubtitles.ConfigureHeadsetPresentation(cam,xrReadingAnchor);
                forestSubtitles.SetHeadsetPresentationVisible(placed&&XRExperienceReady);
            }
            bool ready=placed&&XRExperienceReady;
            forestCanvas.gameObject.SetActive(ready);
            if(!ready){xrHelpOpen=false;return;}
            foreach(Transform child in forestCanvas.transform)
                if(!child.name.StartsWith("PICO ")&&child.name!="DialogueCanvas")child.gameObject.SetActive(false);
            EnsureXRNotes();
            bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
            bool exploring=InterfaceExploring&&!ReelPresentationVisible&&!dialogue&&!NatureEditorOpen&&!NatureWelcomeVisible;
            if(!exploring)xrHelpOpen=false;
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
            xrControllerHints.text=DongbaInterface.Choose(
                (StoryModeEnabled?"STORY MODE":"SANDBOX MODE")+"\n\nLeft stick   Move\nA   Jump / Continue dialogue\n"+XRForestActionGuide+"\nHold Y   Reposition forest and UI\nRight stick click   English / 中文\nLeft stick click   Nature invitations (sandbox)\n\nHold X   "+(StoryModeEnabled?"Start sandbox":"Start story")+" (reset)\nHold A   Restart this mode\nB   Close",
                (StoryModeEnabled?"剧情模式":"沙盒模式")+"\n\n左摇杆  移动\nA  跳跃／继续对话\n"+XRForestActionGuide+"\n按住 Y  重新放置森林与界面\n按下右摇杆  切换中英\n按下左摇杆  自然邀请（沙盒）\n\n按住 X  "+(StoryModeEnabled?"进入沙盒":"进入剧情")+"（清空本次进度）\n按住 A  重新开始当前模式\nB  收起");
            xrPanelStatus.text=DongbaInterface.Choose(xrHelpOpen?"B   Close controls":"B   Controls",xrHelpOpen?"B   收起操作说明":"B   操作说明");
            xrControlsTab.gameObject.SetActive(exploring);
            xrPanelStatus.gameObject.SetActive(exploring);
            xrHealth.text=DongbaInterface.Choose("Health  ","生命  ")+health+" / 5";
            xrHealthNote.gameObject.SetActive(exploring&&health<5&&!xrHelpOpen);
            if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);
        }
    }
}
