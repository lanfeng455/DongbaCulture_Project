using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

// 融合过程提示：在目标字旁边用世界空间文字，把"组词/改义"的过程一步步演出来。
//
// 时序（每步都可在 Inspector 调）：
//   白            ← 目标字
//   白 ＋ 石      ← 手中字加入
//   （停顿）
//   白石          ← 合成结果，同时触发回调去真正改写手中的字
//
// 字体优先级：Resources/Fonts/NotoSerifCJKsc-Black SDF → Inspector 拖入 → TMP 默认 → 系统字体动态生成。
// 默认用项目自带的 Noto Serif CJK SC Black，中文能正常显示。
public class GlyphFusionHintDisplay : MonoBehaviour
{
    [Header("字体")]
    [Tooltip("世界空间字号；配合下面的 Text Scale 控制实际大小")]
    [SerializeField] private TMP_FontAsset m_fontAsset;

    [SerializeField] private float m_fontSize = 28f;

    [Tooltip("整体缩放；世界空间里一般 0.35 ~ 0.5 比较醒目")]
    [SerializeField] private float m_textScale = 0.42f;

    [Tooltip("首选字体资产的 Resources 路径；优先用这个，找不到才用上面 Inspector 拖入的")]
    [SerializeField] private string m_preferredFontResourcePath = "Fonts/NotoSerifCJKsc-Black SDF";

    [Header("位置")]
    [Tooltip("提示文字相对目标字的偏移；放在右上方足够远，避开融合的两个象形文字")]
    [SerializeField] private Vector3 m_offsetFromTarget = new Vector3(2.6f, 2.2f, 0f);

    [Tooltip("排序层级，越大越靠前")]
    [SerializeField] private int m_sortingOrder = 600;

    [Header("节奏（秒）")]
    [Tooltip("每个字单独展示的时长")]
    [SerializeField] private float m_stepDurationInSeconds = 0.7f;

    [Tooltip("两个字都出现后停顿多久，再出结果")]
    [SerializeField] private float m_holdDurationInSeconds = 0.9f;

    [Tooltip("结果展示多久后整体淡出")]
    [SerializeField] private float m_resultHoldInSeconds = 1.2f;

    [Tooltip("结果字出现时弹出动画的时长")]
    [SerializeField] private float m_resultPopDurationInSeconds = 0.55f;

    [Tooltip("结果字出现时的最终放大量（弹出后保持的大小）")]
    [SerializeField] private float m_resultPopScale = 1.5f;

    [Header("配色")]
    [SerializeField] private Color m_targetColor = new Color(1f, 0.55f, 0.35f, 1f);
    [SerializeField] private Color m_heldColor = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private Color m_plusColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color m_resultColor = new Color(1f, 0.92f, 0.4f, 1f);
    [SerializeField] private Color m_failColor = new Color(1f, 0.3f, 0.3f, 1f);

    private const string k_plusSign = "＋";
    private const string k_failMark = "✕";

    private GameObject m_root;
    private TextMeshPro m_text;
    private Camera m_camera;
    private Tween m_popTween;
    private Vector3 m_anchorWorldPosition;

    // 播放完整时序。
    // resultWord 为空（不匹配 / 自定义结果）时，最后一步改为把组合抖动一下再淡出，
    // onResultShown 也照常回调，让上层自己决定改不改字。
    public async Awaitable PlayAsync(
        Vector3 anchorWorldPosition,
        string targetWord,
        string heldWord,
        string resultWord,
        Action onResultShown)
    {
        if (string.IsNullOrEmpty(targetWord) || string.IsNullOrEmpty(heldWord))
        {
            return;
        }

        EnsureText();
        ShowAt(anchorWorldPosition);

        string stepText = Colorize(targetWord, m_targetColor);

        SetText(stepText);
        await Awaitable.WaitForSecondsAsync(m_stepDurationInSeconds);

        if (!IsAlive())
        {
            return;
        }

        // 第二个字加入，用"＋"把来源关系讲清楚
        SetText(
            $"{Colorize(targetWord, m_targetColor)} " +
            $"{Colorize(k_plusSign, m_plusColor)} " +
            $"{Colorize(heldWord, m_heldColor)}");

        await Awaitable.WaitForSecondsAsync(m_stepDurationInSeconds);

        if (!IsAlive())
        {
            return;
        }

        await Awaitable.WaitForSecondsAsync(m_holdDurationInSeconds);

        if (!IsAlive())
        {
            return;
        }

        if (string.IsNullOrEmpty(resultWord))
        {
            // 合不成：把组合划掉并加叉，明确表达"这条路不通"
            SetText(
                $"<s>{Colorize(targetWord, m_targetColor)} " +
                $"{Colorize(k_plusSign, m_plusColor)} " +
                $"{Colorize(heldWord, m_heldColor)}</s> " +
                $"{Colorize(k_failMark, m_failColor)}");
        }
        else
        {
            // 合成结果：放大强调，同时通知上层改写手中的字
            SetText(Colorize(resultWord, m_resultColor), m_fontSize * m_resultPopScale, playBounce: false);
            onResultShown?.Invoke();

            // 弹出动画：从 0 放大到目标大小，带 overshoot，让"产生了新字"这一瞬间有冲击感
            m_popTween?.Kill();
            m_root.transform.localScale = Vector3.zero;
            m_popTween = m_root.transform
                .DOScale(Vector3.one * m_textScale * m_resultPopScale, m_resultPopDurationInSeconds)
                .SetEase(Ease.OutBack);
        }

        await Awaitable.WaitForSecondsAsync(m_resultHoldInSeconds);

        if (!IsAlive())
        {
            return;
        }

        Hide();
    }

    // 中止播放（被打断时调用），不影响下次播放
    public void Hide()
    {
        m_popTween?.Kill();
        m_popTween = null;

        if (m_root != null)
        {
            m_root.SetActive(false);
        }
    }

    private bool IsAlive()
    {
        return this != null && m_root != null;
    }

    private void ShowAt(Vector3 anchorWorldPosition)
    {
        m_anchorWorldPosition = anchorWorldPosition;
        m_root.SetActive(true);
        m_root.transform.position = anchorWorldPosition + m_offsetFromTarget;
        FaceCamera();
    }

    private void SetText(string richText, float? fontSizeOverride = null, bool playBounce = true)
    {
        m_text.fontSize = fontSizeOverride ?? m_fontSize;
        m_text.text = richText;

        // 结果步骤已经设置了自己的弹出动画，这里跳过轻微弹跳，避免两次动画叠加打架
        if (!playBounce)
        {
            return;
        }

        // 每次换字给一个轻微弹跳，视觉上"这就是新的一步"
        // Yoyo 让缩放回到基准，避免越弹越大
        m_popTween?.Kill();
        m_root.transform.localScale = Vector3.one * m_textScale;
        m_popTween = m_root.transform
            .DOScale(Vector3.one * m_textScale * 1.08f, 0.08f)
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo);
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    // 提示对象不在玩家层级下，必须自己负责销毁，否则关掉玩家/切场景会留在场景里
    private void OnDisable()
    {
        DestroyHintObject();
    }

    private void OnDestroy()
    {
        DestroyHintObject();
    }

    private void DestroyHintObject()
    {
        m_popTween?.Kill();
        m_popTween = null;

        if (m_root != null)
        {
            Destroy(m_root);
            m_root = null;
        }

        m_text = null;
    }

    // 始终正对相机，斜俯视下文字也清晰可读；
    // 位置每帧对齐锚点，玩家在提示期间走动也不会把文字带走
    private void FaceCamera()
    {
        if (m_root == null || !m_root.activeSelf)
        {
            return;
        }

        m_root.transform.position = m_anchorWorldPosition + m_offsetFromTarget;

        if (m_camera == null)
        {
            m_camera = Camera.main;
        }

        if (m_camera != null)
        {
            m_root.transform.rotation = m_camera.transform.rotation;
        }
    }

    private string Colorize(string word, Color color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hex}>{word}</color>";
    }

    private void EnsureText()
    {
        if (m_root == null)
        {
            // 刻意不挂在玩家下面：玩家若有缩放会连带改变字号，
            // 而这个提示是纯世界空间 UI，应该独立定位
            m_root = new GameObject("FusionHint");
        }

        if (m_text != null)
        {
            return;
        }

        m_text = m_root.GetComponent<TextMeshPro>();
        if (m_text == null)
        {
            m_text = m_root.AddComponent<TextMeshPro>();
        }

        m_text.alignment = TextAlignmentOptions.Center;
        m_text.richText = true;
        m_text.fontSize = m_fontSize;
        m_text.color = Color.white;
        m_text.sortingOrder = m_sortingOrder;

        TMP_FontAsset font = ResolveFont();
        if (font != null)
        {
            m_text.font = font;
        }
        else
        {
            Debug.LogWarning(
                $"[{GetType().Name}] 没有任何可用字体，融合提示不会显示。" +
                "请在 Inspector 指定字体，或先导入 TMP Essential Resources。",
                this);
        }

        m_root.transform.localScale = Vector3.one * m_textScale;
        m_root.SetActive(false);
    }

    // 字体获取链：Resources 中的 NotoSerifCJKsc-Black SDF → Inspector 拖入 → TMP 默认 → 系统字体动态生成
    private TMP_FontAsset ResolveFont()
    {
        // 最优先：从 Resources 加载项目自带的中文字体资产
        if (!string.IsNullOrEmpty(m_preferredFontResourcePath))
        {
            TMP_FontAsset resourceFont = Resources.Load<TMP_FontAsset>(m_preferredFontResourcePath);
            if (resourceFont != null)
            {
                return resourceFont;
            }
        }

        if (m_fontAsset != null)
        {
            return m_fontAsset;
        }

        try
        {
            // 没导入 TMP Essential Resources 时，TMP Settings 可能不存在，
            // 这里直接访问会抛异常，所以整段包起来
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }
        }
        catch (Exception)
        {
            // 忽略：继续走系统字体兜底
        }

        // 最后兜底：用系统字体建一个动态字体资产
        try
        {
            Font osFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 64);
            if (osFont == null)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] 找不到系统字体，中文可能显示为方块。",
                    this);
                return null;
            }

            return TMP_FontAsset.CreateFontAsset(osFont);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[{GetType().Name}] 动态创建 TMP 字体失败：{exception.Message}",
                this);
            return null;
        }
    }
}
