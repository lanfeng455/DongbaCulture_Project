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
            rect.SetParent(cam.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1000, 760);
            float localUnits = units / Mathf.Max(.0001f, cam.transform.lossyScale.x);
            rect.localScale = Vector3.one * (.84f / 1000 * localUnits);
            rect.localPosition = new Vector3(0, 0, 1.1f) * localUnits;
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
            xrHelpNote=XRNote("folded controls",new Vector2(0,0),new Vector2(760,410));
            xrControllerHints=XRNoteText("Controller guide",xrHelpNote,Vector2.zero,new Vector2(688,350),29);
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
            if(forestSubtitles!=null){
                forestSubtitles.ConfigureHeadsetPresentation(cam,xrUnits);
                forestSubtitles.SetHeadsetPresentationVisible(placed&&XRExperienceReady);
            }
            bool ready=placed&&XRExperienceReady;
            forestCanvas.gameObject.SetActive(ready);
            if(!ready){xrHelpOpen=false;return;}
            foreach(Transform child in forestCanvas.transform)
                if(!child.name.StartsWith("PICO ")&&child.name!="DialogueCanvas")child.gameObject.SetActive(false);
            EnsureXRNotes();
            bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
            bool exploring=InterfaceExploring&&!ReelPresentationVisible&&!dialogue;
            if(!exploring)xrHelpOpen=false;
            string objective=DongbaLanguage.Text(QuietStoryObjective());
            xrSummary.text=objective;
            xrObjectiveNote.gameObject.SetActive(exploring&&StoryModeEnabled&&!string.IsNullOrEmpty(objective)&&!xrHelpOpen);
            xrContextNote.gameObject.SetActive(exploring&&interactionTarget!=null&&!xrHelpOpen);
            if(interactionTarget!=null){
                string name=interactionTarget.animal?AnimalDisplayName(interactionTarget):DongbaLanguage.Text(interactionTarget.health<=0?"Tree stump":"Forest tree");
                string care=interactionTarget.animal?DongbaInterface.Choose("Feed","喂养"):interactionTarget.health<=0?DongbaInterface.Choose("Replant","补种"):DongbaInterface.Choose("Care","培育");
                xrContext.text=name+"\nX  "+care+DongbaInterface.Choose("    R Trigger  Axe","    右扳机  挥斧");
            }
            xrHelpNote.gameObject.SetActive(exploring&&xrHelpOpen);
            xrControllerHints.text=DongbaInterface.Choose(
                "CONTROLS\n\nLeft stick   Move\nA   Jump / Continue dialogue\nRight Trigger   Axe     X   Care / Feed\nHold Y   Reposition forest\nRight stick click   English / 中文\n\nB   Close",
                "操作\n\n左摇杆  移动\nA  跳跃／继续对话\n右扳机  挥斧     X  培育／喂养\n按住 Y  重新放置森林\n按下右摇杆  切换中英\n\nB  收起");
            xrPanelStatus.text=DongbaInterface.Choose(xrHelpOpen?"B   Close controls":"B   Controls",xrHelpOpen?"B   收起操作说明":"B   操作说明");
            xrControlsTab.gameObject.SetActive(exploring);
            xrPanelStatus.gameObject.SetActive(exploring);
            xrHealth.text=DongbaInterface.Choose("Health  ","生命  ")+health+" / 5";
            xrHealthNote.gameObject.SetActive(exploring&&health<5&&!xrHelpOpen);
            if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);
        }
    }
}
