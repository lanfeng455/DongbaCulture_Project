using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        bool xrPanelPlaced;
        UnityEngine.UI.Text xrPanelStatus;
        UnityEngine.UI.Text xrControllerHints;
        UnityEngine.UI.Text xrTreeAction;
        float xrUnits;
        UnityEngine.UI.Text xrSummary, xrContext;

        void ConfigureWorldXRHUD(float units)
        {
            if (forestCanvas == null) return;
            xrUnits = units;
            forestCanvas.renderMode = RenderMode.WorldSpace;
            forestCanvas.worldCamera = cam;
            var scaler = forestCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.enabled = true;
            scaler.dynamicPixelsPerUnit = 4;
            scaler.referencePixelsPerUnit = 100;
            var rect = (RectTransform)forestCanvas.transform;
            rect.SetParent(null, false);
            rect.sizeDelta = new Vector2(900, 1100);
            rect.localScale = Vector3.one * (units * .68f / 900);
            forestCanvas.gameObject.SetActive(false);
        }

        static void XRPanelRect(Transform item, float top, float height)
        {
            if (item == null) return;
            var rect = item as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(852, height);
        }

        public void UpdateXRHUD(bool placed, string status)
        {
            if (forestCanvas == null || forestCanvas.renderMode != RenderMode.WorldSpace) return;
            if(forestSubtitles!=null){
                forestSubtitles.ConfigureHeadsetPresentation(cam,xrUnits);
                forestSubtitles.SetHeadsetPresentationVisible(placed&&XRExperienceReady);
            }
            forestCanvas.gameObject.SetActive(placed&&XRExperienceReady);
            if (!placed)
            {
                xrPanelPlaced = false;
                if (xrTreeAction != null) xrTreeAction.transform.parent.gameObject.SetActive(false);
                return;
            }
            var panel = forestCanvas.transform;
            if (!xrPanelPlaced)
            {
                var towardViewer = cam.transform.position;
                towardViewer.y = 0;
                if (towardViewer.sqrMagnitude < .01f) towardViewer = Vector3.back;
                towardViewer.Normalize();
                var side = Vector3.Cross(Vector3.up, -towardViewer);
                panel.position = (side * 1.02f + towardViewer * .25f + Vector3.up * .48f) * xrUnits;
                xrPanelPlaced = true;
            }
            var away = panel.position - cam.transform.position;
            away.y = 0;
            if (away.sqrMagnitude > .001f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
            // Desktop manuscript tabs have a different layout. Keep their logic, present
            // only the MR summary here; our subtitle strip belongs to the headset view.
            foreach (Transform child in panel)
                if (!child.name.StartsWith("PICO ") && child.name != "DialogueCanvas")
                    child.gameObject.SetActive(false);
            if (xrSummary == null) {
                xrSummary = UILabel("PICO objective", panel, new Vector2(.5f,1), new Vector2(.5f,1),
                    new Vector2(0,-70), new Vector2(820,230), 36, "");
                xrSummary.alignment = TextAnchor.UpperCenter;
                xrContext = UILabel("PICO nearby interaction", panel, new Vector2(.5f,1), new Vector2(.5f,1),
                    new Vector2(0,-350), new Vector2(820,160), 32, "");
                xrContext.alignment = TextAnchor.UpperCenter;
            }
            xrSummary.text = "DONGBA / FOREST\n" + QuietStoryObjective();
            xrContext.text = interactionTarget == null ? "" :
                (interactionTarget.animal ? AnimalDisplayName(interactionTarget) : interactionTarget.health <= 0 ? "Tree stump" : "Forest tree") +
                "\nX · Care / feed     Right Trigger · " + "Axe";
            // The subtitle and its A prompt use our own paper presentation in the
            // lower view. Do not duplicate it on the optional side control panel.
            if(forestSubtitleAdvance!=null)forestSubtitleAdvance.gameObject.SetActive(false);
            if (xrControllerHints == null)
            {
                xrControllerHints = UILabel("PICO controller guide", panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -932), new Vector2(820, 78), 30, "");
                xrControllerHints.alignment = TextAnchor.MiddleCenter;
            }
            if (xrPanelStatus == null)
            {
                xrPanelStatus = UILabel("PICO placement status", panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -1020), new Vector2(820, 60), 30, "");
                xrPanelStatus.alignment = TextAnchor.MiddleCenter;
            }
            xrPanelStatus.text = status;
            xrControllerHints.text = "Left stick: Move | A: Jump / Continue\nR Trigger: Axe | X: Care / Feed | Hold Y: Reposition | R stick click: EN / 中文";
        }
    }
}
