using UnityEngine;
using UnityEngine.UI;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        float xrUnits;
        Text xrSummary, xrContext, xrPanelStatus, xrGuideTitle;
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

        void UpdateXRHelpInput(bool pressed, bool ready)
        {
            if(ready&&pressed&&!xrHelpHeld)xrHelpOpen=!xrHelpOpen;
            xrHelpHeld=pressed;
            if(!ready)xrHelpOpen=false;
        }

        public void UpdateXRHUD(bool placed, string status)
        {
            if(forestCanvas==null||forestCanvas.renderMode!=RenderMode.WorldSpace)return;
            if(XRPuzzleActive){forestCanvas.gameObject.SetActive(false);return;}
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
            UpdateXRDayNight();
            bool dialogue=fatherIntroActive||ManualStoryDialogueActive;
            bool exploring=InterfaceExploring&&!ReelPresentationVisible&&!dialogue&&!NatureEditorOpen&&!NatureWelcomeVisible&&!NatureAnswerNoticeVisible;
            if(!exploring)xrHelpOpen=false;
            xrSideFold.gameObject.SetActive(exploring||NatureEditorOpen||NatureWelcomeVisible||NatureAnswerNoticeVisible);
            RefreshXRChapter(exploring&&!xrHelpOpen);
            RefreshInteractionCard(true,exploring&&!xrHelpOpen);
            RefreshXRHealth(exploring&&!xrHelpOpen);
            xrHelpNote.gameObject.SetActive(exploring&&xrHelpOpen);
            if(xrHelpOpen)RefreshXRBook();
            xrPanelStatus.text=DongbaInterface.Choose("B   Controls","B   操作说明");
            xrControlsTab.gameObject.SetActive(exploring&&!xrHelpOpen);
            xrPanelStatus.gameObject.SetActive(exploring&&!xrHelpOpen);
            if(natureTab!=null&&xrHelpOpen)natureTab.gameObject.SetActive(false);
            if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);
        }
    }
}
