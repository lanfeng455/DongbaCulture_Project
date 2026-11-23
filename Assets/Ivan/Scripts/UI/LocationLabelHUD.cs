using UnityEngine;
using UnityEngine.UI;

// 可复用的"当前位置"标签——Forest 风格宣纸小标签、右上角定位、自动淡入淡出。
// 挂一个就能用：Canvas、纸纹理、文字全部运行时生成，不依赖任何美术资源。
// 外部（Trigger / 事件 / 其他脚本）调 SetLabel("山顶") 即可切换文字。
[DisallowMultipleComponent]
public class LocationLabelHUD : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("不填就自动在本 GameObject 上建一个 ScreenSpaceOverlay Canvas")]
    [SerializeField] private Canvas m_hostCanvas;
    [Tooltip("用于排序覆盖其他 UI；Forest 的 forestCanvas.sortingOrder=30000，这里默认 31000 保证在上层")]
    [SerializeField] private int m_sortingOrder = 31000;

    [Header("Placement")]
    [Tooltip("标签距离屏幕右上角的像素偏移（基于 1080p 参考）")]
    [SerializeField] private Vector2 m_offsetFromCorner = new Vector2(24, -24);
    [Tooltip("标签尺寸（宽×高，像素，参考 1080p）")]
    [SerializeField] private Vector2 m_labelSize = new Vector2(216, 46);

    [Header("Label")]
    [SerializeField] private string m_defaultLabel = "";
    [SerializeField] private int m_fontSize = 19;
    [SerializeField] private Color m_textColor = new Color(.45f, .37f, .27f); // 墨棕
    [SerializeField] private Color m_paperColor = new Color(.96f, .925f, .85f, .85f); // 米黄宣纸底
    [Tooltip("文字切换时 fade 时长；0 即硬切")]
    [SerializeField] private float m_fadeDuration = .35f;

    CanvasGroup m_canvasGroup;
    Text m_labelText;
    Image m_paperImage;

    // 当前显示的文字（已经过 fade）
    public string CurrentLabel => m_labelText != null ? m_labelText.text : m_defaultLabel;

    // 外部 API：设置位置标签文字
    public void SetLabel(string labelText)
    {
        if (m_labelText == null) return;
        if (string.Equals(m_labelText.text, labelText, System.StringComparison.Ordinal)) return;

        if (m_fadeDuration <= 0f)
        {
            m_labelText.text = labelText;
            m_canvasGroup.alpha = 1f;
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeTextCo(labelText));
    }

    public void SetVisible(bool visible)
    {
        if (m_canvasGroup != null) m_canvasGroup.alpha = visible ? 1f : 0f;
    }

    void Awake()
    {
        EnsureBuilt();
        if (!string.IsNullOrEmpty(m_defaultLabel)) SetLabel(m_defaultLabel);
    }

    void EnsureBuilt()
    {
        Canvas canvas = m_hostCanvas != null ? m_hostCanvas : gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = m_sortingOrder;
        canvas.overrideSorting = true;
        m_hostCanvas = canvas;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        if (m_canvasGroup == null)
        {
            m_canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (m_canvasGroup == null) m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
            m_canvasGroup.alpha = 1f;
            m_canvasGroup.blocksRaycasts = false;
            m_canvasGroup.interactable = false;
        }

        // 宣纸底色（Sliced 9-patch，程序化生成）
        var paperGo = new GameObject("Location label paper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        paperGo.transform.SetParent(transform, false);
        var paperRect = (RectTransform)paperGo.transform;
        paperRect.anchorMin = new Vector2(1, 1);
        paperRect.anchorMax = new Vector2(1, 1);
        paperRect.pivot = new Vector2(1, 1);
        paperRect.anchoredPosition = m_offsetFromCorner;
        paperRect.sizeDelta = m_labelSize;
        m_paperImage = paperGo.GetComponent<Image>();
        m_paperImage.sprite = GeneratePaperSprite();
        m_paperImage.type = Image.Type.Sliced;
        m_paperImage.color = m_paperColor;
        m_paperImage.raycastTarget = false;

        // 标签文字
        var textGo = new GameObject("Location label text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(paperGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        m_labelText = textGo.GetComponent<Text>();
        m_labelText.font = GetPreferredFont();
        m_labelText.fontSize = m_fontSize;
        m_labelText.alignment = TextAnchor.MiddleCenter;
        m_labelText.color = m_textColor;
        m_labelText.raycastTarget = false;
        m_labelText.text = m_defaultLabel ?? "";
    }

    // 与 Forest QuietPaper 同款：Sliced 9-patch，边缘自然透明过渡模拟宣纸切割
    static Sprite GeneratePaperSprite()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.name = "LocationLabel paper / runtime generated";
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                float rough = .45f * Mathf.Sin(y * 1.7f) + .3f * Mathf.Sin(x * 2.3f);
                float alpha = Mathf.SmoothStep(0, 1, (edge + rough) / 4f);
                pixels[y * n + x] = new Color(1, 1, 1, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
    }

    static Font GetPreferredFont()
    {
        // 优先用 Georgia（Forest 里已经在用的），退回到 LegacyRuntime.ttf
        if (System.Array.Exists(Font.GetOSInstalledFontNames(),
            n => n.Equals("Georgia", System.StringComparison.OrdinalIgnoreCase)))
            return Font.CreateDynamicFontFromOSFont("Georgia", 30);
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    System.Collections.IEnumerator FadeTextCo(string newLabel)
    {
        m_canvasGroup.alpha = 1f;
        // 先淡出，再改字，再淡入——制造"纸被墨晕然后新字浮现"的感觉
        float half = Mathf.Max(.05f, m_fadeDuration * .5f);
        float age = 0;
        while (age < half)
        {
            age += Time.unscaledDeltaTime;
            m_canvasGroup.alpha = Mathf.Clamp01(1f - age / half);
            yield return null;
        }
        m_labelText.text = newLabel;
        age = 0;
        while (age < half)
        {
            age += Time.unscaledDeltaTime;
            m_canvasGroup.alpha = Mathf.Clamp01(age / half);
            yield return null;
        }
        m_canvasGroup.alpha = 1f;
    }
}
