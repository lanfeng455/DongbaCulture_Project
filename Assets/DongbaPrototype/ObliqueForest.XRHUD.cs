using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        bool xrPanelPlaced;
        UnityEngine.UI.Text xrPanelStatus;
        UnityEngine.UI.Text xrControllerHints;
        float xrUnits;

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
            forestCanvas.gameObject.SetActive(placed);
            if (!placed) { xrPanelPlaced = false; return; }
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
            XRPanelRect(panel.Find("Compact identity and vitality"), 24, 128);
            XRPanelRect(panel.Find("Place paper label"), 170, 64);
            XRPanelRect(panel.Find("Story objective paper"), 252, 112);
            XRPanelRect(panel.Find("Nearby interaction card"), 382, 140);
            XRPanelRect(panel.Find("Feedback paper"), 540, 110);
            if (hudNotice != null) hudNotice.rectTransform.sizeDelta = new Vector2(792, 90);
            if (hudArea != null) hudArea.rectTransform.sizeDelta = new Vector2(792, 52);
            if (journeyObjective != null) journeyObjective.rectTransform.sizeDelta = new Vector2(784, 92);
            if (hudPrompt != null) hudPrompt.rectTransform.sizeDelta = new Vector2(784, 70);
            if (hudPromptTitle != null) hudPromptTitle.rectTransform.sizeDelta = new Vector2(784, 40);
            if (hudPrompt != null) hudPrompt.text = hudPrompt.text.Replace("<b>E</b>", "<b>X</b>").Replace("<b>J</b>", "<b>R Trigger</b>");
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                text.fontSize = Mathf.Max(text.fontSize, 30);
                var size = text.rectTransform.sizeDelta;
                if (size.y > 0 && size.y < 42) text.rectTransform.sizeDelta = new Vector2(size.x, 42);
                text.color = new Color(.13f, .09f, .05f, 1);
            }
            if (uiMode != null) uiMode.rectTransform.sizeDelta = new Vector2(280, 42);
            if (uiHelpShortcut != null) uiHelpShortcut.SetActive(false);
            if (uiCoreHints != null) uiCoreHints.SetActive(false);
            if (uiHelpPanel != null) uiHelpPanel.SetActive(false);
            if (forestSubtitles != null) forestSubtitles.AttachToWorldPanel((RectTransform)panel);
            if (forestSubtitleAdvance != null)
            {
                XRPanelRect(forestSubtitleAdvance.transform, 880, 44);
                forestSubtitleAdvance.text = "A   Continue";
            }
            if (xrControllerHints == null)
            {
                xrControllerHints = UILabel("PICO controller guide", panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -932), new Vector2(820, 78), 30,
                    "Left stick: Move   |   A: Jump / Continue\nR Trigger: Axe   |   X: Care / Feed");
                xrControllerHints.alignment = TextAnchor.MiddleCenter;
            }
            if (xrPanelStatus == null)
            {
                xrPanelStatus = UILabel("PICO placement status", panel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -1020), new Vector2(820, 60), 30, "");
                xrPanelStatus.alignment = TextAnchor.MiddleCenter;
            }
            xrPanelStatus.text = status;
        }
    }
}
