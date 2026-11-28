using UnityEngine;
using UnityEngine.UI;

public partial class DialogueDisplay
{
    Camera headsetCamera;
    bool headsetPresentationVisible = true;
    Material headsetPaperMaterial;
    readonly System.Collections.Generic.List<Material> headsetTextMaterials = new();
    readonly System.Collections.Generic.HashSet<int> headsetStyledGraphics = new();
    readonly System.Collections.Generic.List<Graphic> headsetGraphics = new();
    internal bool UsesHeadsetPresentation => headsetCamera != null;
    internal string HeadsetContinuePrompt => DongbaPrototype.DongbaLanguage.Chinese ? "A   继续" : "A   Continue";

    // Reuse our paper strip, portraits, typography and dialogue playback. Only its
    // canvas placement changes for the headset; it no longer belongs to the side HUD.
    public void ConfigureHeadsetPresentation(Camera camera, Transform readingAnchor)
    {
        if (camera == null || readingAnchor == null || (headsetCamera == camera && m_canvas != null && m_canvas.transform.parent == readingAnchor)) return;
        EnsureUi();
        headsetCamera = camera;
        SetPaperPresentation(false, paperManual);
        m_canvas.renderMode = RenderMode.WorldSpace;
        m_canvas.worldCamera = camera;
        m_canvas.sortingOrder = 30010;
        var rect = (RectTransform)m_canvas.transform;
        rect.SetParent(readingAnchor, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = m_referenceResolution;
        // Same stable world frame as the forest UI, below the playable view.
        // The shared PC portrait, typewriter, bilingual text and padding remain.
        const float stripWidth = 1.12f;
        float metresPerPixel = stripWidth / (m_referenceResolution.x * m_widthRatio);
        float stripCentre = -m_referenceResolution.y * .5f + m_bottomMarginInPixels + m_minHeightInPixels * .5f;
        rect.localScale = Vector3.one * metresPerPixel;
        rect.localPosition = new Vector3(0, -.38f - stripCentre * metresPerPixel, -.002f);
        rect.localRotation = Quaternion.identity;
        var scaler = m_canvas.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4;
        ApplyLayout();
        if (m_visibleEntry != null) FitBackgroundToText(FormatLine(m_visibleEntry));
        ApplyHeadsetReadability();
    }

    public void SetHeadsetPresentationVisible(bool visible)
    {
        headsetPresentationVisible = visible;
        if (UsesHeadsetPresentation && m_canvas != null)
            m_canvas.enabled = visible && (DongbaPrototype.DongbaInterface.Instance == null || !DongbaPrototype.DongbaInterface.Instance.ModalOpen);
    }

    void ApplyHeadsetReadability()
    {
        if (!UsesHeadsetPresentation) return;
        // A real world-space canvas gives both eyes the same comfortable depth.
        // UI-only materials keep a nearby table or tree from cutting through it.
        if (headsetPaperMaterial == null)
            headsetPaperMaterial = new Material(Shader.Find("Dongba/Headset Paper UI"));
        m_canvas.GetComponentsInChildren(true, headsetGraphics);
        foreach (var graphic in headsetGraphics)
        {
            if (!headsetStyledGraphics.Add(graphic.GetInstanceID())) continue;
            if (graphic is TMPro.TMP_Text text)
            {
                var material = new Material(text.fontSharedMaterial);
                material.shader = Resources.Load<Material>("HeadsetTextOverlay").shader;
                text.fontSharedMaterial = material;
                headsetTextMaterials.Add(material);
            }
            else if (graphic is Image) graphic.material = headsetPaperMaterial;
        }
    }

    void ReleaseHeadsetPresentation()
    {
        if (headsetPaperMaterial != null) Destroy(headsetPaperMaterial);
        foreach (var material in headsetTextMaterials) if (material != null) Destroy(material);
        headsetTextMaterials.Clear();
        if (UsesHeadsetPresentation && m_canvas != null) Destroy(m_canvas.gameObject);
    }
}
