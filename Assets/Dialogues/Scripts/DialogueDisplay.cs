using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Ivan.Scroll;

// 对话显示：场景级单例，负责把 DialogueLibrarySO 里的某段对话
// 用打字机效果呈现到一个极简字幕条上。
//
// 场景设置（一次性）：
//   1. 新建空物体命名 DialogueDisplay，挂本组件
//   2. 把对话库资产拖进 m_dialogueLibrary
//   3. 不需要手动建 Canvas/Text——Awake 时自动生成
//      （想改位置/大小/字体，在 Inspector 调下面的字段）
//
// Unity Events 调用：
//   PlayRange(from, to)          播第 from 到 to 句（含），播完按当前设置决定是否自动隐藏
//   PlayFromTo(from, to, autoHide, hideDelay)  同上，但临时覆盖自动隐藏设置
//   HideDialogue()               手动隐藏（任何时候都可调）
//
// 多说一句：PlayRange 是新开一段对话，旧对话如果还在放会被打断。
public class DialogueDisplay : MonoBehaviour
{
    public static DialogueDisplay Instance { get; private set; }

    [Header("对话库")]
    [Tooltip("当前使用的对话库资产")]
    [SerializeField] private DialogueLibrarySO m_dialogueLibrary;

    [Header("引用（留空则自动生成）")]
    [Tooltip("字幕用 Canvas；留空则代码创建一个 ScreenSpaceOverlay Canvas")]
    [SerializeField] private Canvas m_canvas;

    [Tooltip("黑底 Image；留空则代码创建")]
    [SerializeField] private Image m_background;

    [Tooltip("文字 TMP_Text；留空则代码创建")]
    [SerializeField] private TMP_Text m_text;

    [Header("分辨率适配")]
    [Tooltip("Canvas 参考分辨率：下面所有像素值都按这个分辨率写，其它分辨率由 CanvasScaler 等比缩放")]
    [SerializeField] private Vector2 m_referenceResolution = new Vector2(1920f, 1080f);

    [Header("字幕条布局")]
    [Tooltip("距离屏幕底部的像素距离")]
    [SerializeField] private float m_bottomMarginInPixels = 80f;

    [Tooltip("字幕条宽度占屏幕宽度的比例（0~1）")]
    [Range(0.2f, 1f)]
    [SerializeField] private float m_widthRatio = 0.6f;

    [Tooltip("字幕条最小高度（像素）")]
    [SerializeField] private float m_minHeightInPixels = 60f;

    [Tooltip("文字与黑底边缘的内边距（像素）")]
    [SerializeField] private Vector2 m_textPadding = new Vector2(24f, 14f);

    [Tooltip("黑底颜色与透明度（Alpha 越大底色越实，看不清字就调高它）")]
    [SerializeField] private Color m_backgroundColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("渐入渐出")]
    [Tooltip("字幕条淡入/淡出时长（秒）；0 = 立即显示/隐藏")]
    [SerializeField] private float m_fadeDuration = 0.25f;

    [Header("字体")]
    [Tooltip("字号")]
    [SerializeField] private float m_fontSize = 28f;

    [Tooltip("字体资产；留空则从 Resources/Fonts/NotoSerifCJKsc-Black SDF 加载")]
    [SerializeField] private TMP_FontAsset m_fontAsset;

    [Tooltip("首选字体资源的 Resources 路径")]
    [SerializeField] private string m_preferredFontResourcePath = "Fonts/NotoSerifCJKsc-Black SDF";

    [Tooltip("文字颜色")]
    [SerializeField] private Color m_textColor = Color.white;

    [Tooltip("说话人名字颜色；默认偏冷，避开正文里常用的金色高亮")]
    [SerializeField] private Color m_speakerColor = new Color(0.6f, 0.87f, 1f, 1f);

    [Header("打字机")]
    [Tooltip("每秒打出的字符数；越大越快")]
    [SerializeField] private float m_charactersPerSecond = 18f;

    [Tooltip("一句话打完到下一句话开始之间的停顿（秒）")]
    [SerializeField] private float m_delayBetweenLinesInSeconds = 0.5f;

    [Header("Unity Events 快捷入口（零参数绑法）")]
    [Tooltip("格式 \"起点/终点\"，例如 \"0/2\"；留空或写 \"all\" = 播全部")]
    [SerializeField] private string m_eventRange = "0/2";

    [Header("自动隐藏（默认设置，可被 PlayFromTo 覆盖）")]
    [Tooltip("默认：播完一段对话后是否自动隐藏")]
    [SerializeField] private bool m_autoHideByDefault = true;

    [Tooltip("默认：自动隐藏前停留的秒数")]
    [SerializeField] private float m_autoHideDelayInSeconds = 2.5f;

    private CancellationTokenSource m_playbackCts;
    private CancellationTokenSource m_fadeCts;     // 渐变专用：新的显隐请求立刻打断旧渐变
    private CanvasGroup m_fadeGroup;               // 挂在黑底上，文字是黑底子物体，一起淡
    private bool m_isPlaying;
    private int m_playbackGeneration;   // 播放代数：被新段打断的旧段收尾时，不得污染新段的 IsPlaying

    public bool IsPlaying => m_isPlaying;
    private bool m_revealCurrentLine;
    public bool IsTyping => m_isPlaying && m_text != null && m_text.maxVisibleCharacters < m_text.textInfo.characterCount;
    public TMP_Text SubtitleText => m_text;
    public void SetCharactersPerSecond(float speed) { m_charactersPerSecond = Mathf.Max(1, speed); }

    // Adapters can reuse this display without replacing the scene's dialogue library.
    public void PlayLine(string speaker, string content)
    {
        CancelPlayback();
        m_playbackCts = new CancellationTokenSource();
        int generation = ++m_playbackGeneration;
        var entry = new DialogueLibrarySO.DialogueEntry { speaker = speaker, content = content };
        _ = PlaySegmentAsync(new List<DialogueLibrarySO.DialogueEntry> { entry }, false, 0, m_playbackCts.Token, generation);
    }

    public bool CompleteCurrentLine()
    {
        if (!IsTyping) return false;
        m_revealCurrentLine = true;
        m_text.maxVisibleCharacters = m_text.textInfo.characterCount;
        return true;
    }

    // Keep the default overlay for teammate scenes; the forest uses its existing capture camera.
    public void SetRenderCamera(Camera camera)
    {
        EnsureUi();
        m_canvas.renderMode = RenderMode.ScreenSpaceCamera;
        m_canvas.worldCamera = camera;
        m_canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, .8f);
        m_canvas.sortingOrder = 1100;
        Canvas.ForceUpdateCanvases();
        ApplyLayout();
    }

    // ------------------------------------------------------------------
    // Unity Events 入口
    // ------------------------------------------------------------------

    // 【最常用】单参数入口：接受 "0/2" / "0-2" / "all" 这样的范围字符串。
    // UnityEvent<string> 下拉里能选，选完直接在 Inspector 填 "0/2" 即可。
    public void PlayRange(string rangeSpec)
    {
        if (m_dialogueLibrary == null || m_dialogueLibrary.Count == 0)
        {
            return;
        }

        if (!TryParseRange(rangeSpec, out int from, out int to))
        {
            Debug.LogWarning($"[{GetType().Name}] 无法解析范围字符串 \"{rangeSpec}\"，已跳过。", this);
            return;
        }

        PlayRange(from, to);
    }

    // 零参数入口：使用 Inspector 里配置的 m_eventRange 字段。
    // 和上面的 PlayRange(string) 等价，只是字符串写死在 Inspector 里。
    public void PlayEventRange()
    {
        PlayRange(m_eventRange);
    }

    // 播第 fromIndex 到 toIndex 句（含两端，从 0 开始计数）。
    // 代码调用用这个。
    public void PlayRange(int fromIndex, int toIndex)
    {
        PlayFromTo(fromIndex, toIndex, m_autoHideByDefault, m_autoHideDelayInSeconds);
    }

    // 解析 "0/2" / "0-2" / "all" 这种范围字符串。
    private bool TryParseRange(string spec, out int from, out int to)
    {
        from = 0;
        to = 0;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        string trimmed = spec.Trim();

        // "all" 简写 → 整段库
        if (trimmed.Equals("all", System.StringComparison.OrdinalIgnoreCase))
        {
            if (m_dialogueLibrary != null)
            {
                from = 0;
                to = m_dialogueLibrary.Count - 1;
                return true;
            }
            return false;
        }

        // 单数字 "3" → 只播第 3 句（写起来最省事，也不容易配错）
        if (int.TryParse(trimmed, out int single))
        {
            from = single;
            to = single;
            return true;
        }

        // 允许 "/" 或 "-" 做分隔符，写起来顺手
        string[] parts = trimmed.Replace('-', '/').Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0].Trim(), out from) && int.TryParse(parts[1].Trim(), out to);
    }

    // 完整版：显式指定是否自动隐藏和停留时间。
    // Unity Events 里选这个就能在 Inspector 里直接配四个参数。
    public void PlayFromTo(int fromIndex, int toIndex, bool autoHide, float hideDelayInSeconds)
    {
        if (m_dialogueLibrary == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 没有配置对话库资产。", this);
            return;
        }

        int count = m_dialogueLibrary.Count;
        if (count == 0)
        {
            Debug.LogWarning($"[{GetType().Name}] 对话库为空。", this);
            return;
        }

        // 钳制索引，越界只警告不异常
        int from = Mathf.Clamp(fromIndex, 0, count - 1);
        int to = Mathf.Clamp(toIndex, 0, count - 1);
        if (from > to)
        {
            (from, to) = (to, from);
        }

        // 收集这一段要播的句子
        List<DialogueLibrarySO.DialogueEntry> segment = new List<DialogueLibrarySO.DialogueEntry>();
        for (int i = from; i <= to; i++)
        {
            DialogueLibrarySO.DialogueEntry entry = m_dialogueLibrary.GetEntry(i);
            if (entry != null)
            {
                segment.Add(entry);
            }
        }

        if (segment.Count == 0)
        {
            return;
        }

        CancelPlayback();
        m_playbackCts = new CancellationTokenSource();
        // 代数 +1：旧段的 finally（取消后下一帧才跑）凭代数识别自己已过期，不再覆盖新段状态
        int generation = ++m_playbackGeneration;
        _ = PlaySegmentAsync(segment, autoHide, hideDelayInSeconds, m_playbackCts.Token, generation);
    }

    // 播整段对话库（便捷重载）
    public void PlayAll()
    {
        if (m_dialogueLibrary == null)
        {
            return;
        }
        PlayRange(0, m_dialogueLibrary.Count - 1);
    }

    // 等当前对话段播完：先等它真正开始（容忍 PlayRange 调用帧的空窗，超时放弃），
    // 再等 IsPlaying 结束。没有对话在播时最多等 startTimeoutSeconds 秒即返回。
    // 供 StorySequencePlayer 等外部流程把对话接进自己的时间轴
    public async Awaitable WaitUntilPlaybackStopsAsync(float startTimeoutSeconds = 1f)
    {
        // 阶段一：等开始（对话段可能为空/配置错误，超时后放弃，别把调用方吊死）
        float timeout = Mathf.Max(0f, startTimeoutSeconds);
        while (this != null && !m_isPlaying && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            await Awaitable.NextFrameAsync();
        }

        if (this == null || !m_isPlaying)
        {
            return;
        }

        // 阶段二：等结束
        while (this != null && m_isPlaying)
        {
            await Awaitable.NextFrameAsync();
        }
    }

    // 手动隐藏入口：任何时候调都会立即停下打字机并隐藏字幕条
    public void HideDialogue()
    {
        CancelPlayback();
        SetVisible(false);
    }

    // 换对话库（比如换关卡换一本）
    public void SetLibrary(DialogueLibrarySO newLibrary)
    {
        m_dialogueLibrary = newLibrary;
    }

    // ------------------------------------------------------------------
    // 生命周期
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{GetType().Name}] 场景里已有一个实例，重复挂载的会被禁用。", this);
            enabled = false;
            return;
        }
        Instance = this;

        EnsureUi();
        // 初始化必须立即隐藏：走渐变会在场景启动瞬间闪一帧全亮字幕条
        SetVisible(false, immediate: true);
    }

    private void OnDestroy()
    {
        CancelPlayback();
        m_fadeCts?.Cancel();
        m_fadeCts?.Dispose();
        m_fadeCts = null;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------
    // 播放流程
    // ------------------------------------------------------------------

    private async Awaitable PlaySegmentAsync(
        List<DialogueLibrarySO.DialogueEntry> segment,
        bool autoHide,
        float hideDelayInSeconds,
        CancellationToken token,
        int generation)
    {
        m_isPlaying = true;
        SetVisible(true);

        try
        {
            foreach (DialogueLibrarySO.DialogueEntry entry in segment)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                // 演出指令行（Content 以 @ 开头）：不进字幕，转给舞台执行
                if (StageCommandParser.TryParse(entry.content, out StageCommand command))
                {
                    SetVisible(false);
                    await ExecuteStageCommandAsync(command, entry.speaker, token);
                    continue;
                }

                SetVisible(true);
                string line = FormatLine(entry);
                await TypeLineAsync(line, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                // 句间停顿
                if (m_delayBetweenLinesInSeconds > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(m_delayBetweenLinesInSeconds, token);
                }
            }

            if (m_playbackGeneration == generation)
            {
                m_isPlaying = false;
            }

            if (autoHide)
            {
                if (hideDelayInSeconds > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(hideDelayInSeconds, token);
                }

                if (!token.IsCancellationRequested)
                {
                    SetVisible(false);
                }
            }
            // 不自动隐藏就保持显示，等外部调 HideDialogue
        }
        catch (System.Exception)
        {
            // WaitForSecondsAsync 取消时可能抛异常；只要 token 已 Cancel 就静默
            if (!token.IsCancellationRequested)
            {
                throw;
            }
        }
        finally
        {
            // 关键：只有自己仍是当前播放段时才清标志。
            // 旧段被 PlayFromTo 打断后，finally 要到下一帧才跑，
            // 不加代数检查会把新段刚设好的 IsPlaying=true 抹成 false
            if (m_playbackGeneration == generation)
            {
                m_isPlaying = false;
            }
        }
    }

    // 执行一条舞台指令：@等待 作用于对话流本身，其余按 Speaker 名找场景里的 StageActor
    private async Awaitable ExecuteStageCommandAsync(
        StageCommand command, string speaker, CancellationToken token)
    {
        switch (command.Type)
        {
            case StageCommandType.Wait:
                if (command.Value > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(command.Value, token);
                }
                break;

            case StageCommandType.FaceRight:
                StageActorRegistry.Find(speaker)?.Face(1f);
                break;

            case StageCommandType.FaceLeft:
                StageActorRegistry.Find(speaker)?.Face(-1f);
                break;

            case StageCommandType.Move:
                StageActorRegistry.Find(speaker)?.StartMove(command.Value);
                break;

            case StageCommandType.Stop:
                StageActorRegistry.Find(speaker)?.StopMove();
                break;

            case StageCommandType.MoveTo:
            {
                if (float.IsNaN(command.Value))
                {
                    Debug.LogWarning($"[{GetType().Name}] @移动到 缺少目标坐标，该行已跳过。", this);
                    break;
                }

                StageActor actor = StageActorRegistry.Find(speaker);
                if (actor != null)
                {
                    actor.MoveTo(command.Value);

                    // 走到目标才继续下一行（对话流停在指令行上，字幕隐藏中）；
                    // 演员中途被销毁时视为到达，避免对话流被吊死
                    while (actor != null && !actor.HasReachedMoveTarget)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                        await Awaitable.NextFrameAsync(token);
                    }
                }
                break;
            }

            case StageCommandType.Hide:
                StageActorRegistry.Find(speaker)?.Hide();
                break;
        }
    }

    // 打字机：逐字符填充
    private async Awaitable TypeLineAsync(string fullLine, CancellationToken token)
    {
        if (m_text == null)
        {
            return;
        }

        // 先把整行结构放进去（含富文本标签），但可见字符数为 0
        m_revealCurrentLine = false;
        m_text.text = fullLine;
        FitBackgroundToText(fullLine);
        m_text.maxVisibleCharacters = 0;
        m_text.ForceMeshUpdate();

        int totalVisible = m_text.textInfo.characterCount;
        if (totalVisible == 0)
        {
            return;
        }

        float secondsPerChar = 1f / Mathf.Max(1f, m_charactersPerSecond);
        float elapsed = 0f;
        int shown = 0;

        while (shown < totalVisible)
        {
            if (m_revealCurrentLine) break;
            if (token.IsCancellationRequested)
            {
                return;
            }

            elapsed += Time.deltaTime;
            int target = Mathf.Min(totalVisible, Mathf.FloorToInt(elapsed / secondsPerChar));
            if (target != shown)
            {
                shown = target;
                m_text.maxVisibleCharacters = shown;
            }

            await Awaitable.NextFrameAsync(token);
        }

        m_text.maxVisibleCharacters = totalVisible;
    }

    // 格式化为 "说话人：内容"；没说话人就是纯内容（旁白）
    private string FormatLine(DialogueLibrarySO.DialogueEntry entry)
    {
        if (string.IsNullOrEmpty(entry.speaker))
        {
            return entry.content ?? string.Empty;
        }

        string speakerHex = ColorUtility.ToHtmlStringRGB(m_speakerColor);
        return $"<color=#{speakerHex}>{entry.speaker}</color>：{entry.content}";
    }

    private void CancelPlayback()
    {
        if (m_playbackCts != null)
        {
            m_playbackCts.Cancel();
            m_playbackCts.Dispose();
            m_playbackCts = null;
        }
        m_isPlaying = false;
    }

    // 显隐字幕条。immediate = true 跳过渐变直接切（初始化/应急用）。
    // 已在目标透明度时（如连播多句台词）不重播渐入，避免每句闪一下
    private void SetVisible(bool visible, bool immediate = false)
    {
        if (m_background == null)
        {
            return;
        }

        // 新请求打断进行中的渐变（cts 归属 SetVisible，避免并发 FadeAsync 抢字段）
        m_fadeCts?.Cancel();
        m_fadeCts?.Dispose();
        m_fadeCts = null;

        if (m_fadeGroup == null || m_fadeDuration <= 0f || immediate)
        {
            m_background.gameObject.SetActive(visible);
            if (m_fadeGroup != null)
            {
                m_fadeGroup.alpha = visible ? 1f : 0f;
            }
            return;
        }

        float targetAlpha = visible ? 1f : 0f;
        if (Mathf.Approximately(m_fadeGroup.alpha, targetAlpha))
        {
            m_background.gameObject.SetActive(visible);
            return;
        }

        m_fadeCts = new CancellationTokenSource();
        _ = FadeAsync(visible, m_fadeCts.Token);
    }

    // 渐变本体：显示时先激活再淡入，隐藏时淡完再失活（失活期间不占渲染）
    private async Awaitable FadeAsync(bool visible, CancellationToken token)
    {
        float startAlpha = m_fadeGroup.alpha;
        float targetAlpha = visible ? 1f : 0f;

        if (visible)
        {
            m_background.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        try
        {
            while (elapsed < m_fadeDuration)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                m_fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / m_fadeDuration);
                await Awaitable.NextFrameAsync(token);
            }

            m_fadeGroup.alpha = targetAlpha;
            if (!visible)
            {
                m_background.gameObject.SetActive(false);
            }
        }
        catch (System.Exception)
        {
            // NextFrameAsync 取消时可能抛异常；token 已 Cancel 就静默
            if (!token.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    // ------------------------------------------------------------------
    // UI 生成（只在用户没拖引用时执行一次）
    // ------------------------------------------------------------------

    // 分辨率适配：字幕条的像素值（边距/高度/字号/内边距）都按 m_referenceResolution 写，
    // CanvasScaler 会按实际屏幕等比换算。缺失则补，已有但没开自适应则改成 ScaleWithScreenSize
    private void EnsureCanvasScaler()
    {
        CanvasScaler scaler = m_canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = m_canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
        {
            return; // 用户已经配好 ScaleWithScreenSize，尊重原设置
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = m_referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureUi()
    {
        if (m_canvas == null)
        {
            GameObject canvasObject = new GameObject("DialogueCanvas");
            m_canvas = canvasObject.AddComponent<Canvas>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_canvas.sortingOrder = 1000;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        // 自动创建的 Canvas 和用户自己拖进来的 Canvas 都统一走这里，
        // 保证一定有 ScaleWithScreenSize（否则在非参考分辨率下字会变成另一档大小）
        EnsureCanvasScaler();

        if (m_background == null)
        {
            GameObject bgObject = new GameObject("DialogueBackground");
            bgObject.transform.SetParent(m_canvas.transform, false);
            m_background = bgObject.AddComponent<Image>();
            m_background.color = m_backgroundColor;

            RectTransform rect = m_background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, m_bottomMarginInPixels);
        }

        if (m_text == null)
        {
            GameObject textObject = new GameObject("DialogueText");
            textObject.transform.SetParent(m_background.transform, false);
            m_text = textObject.AddComponent<TextMeshProUGUI>();
            m_text.alignment = TextAlignmentOptions.Center;
            m_text.richText = true;
            m_text.fontSize = m_fontSize;
            m_text.color = m_textColor;
            m_text.textWrappingMode = TextWrappingModes.Normal;
            m_text.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset font = ResolveFont();
            if (font != null)
            {
                m_text.font = font;
            }
            else
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] 没有可用字体，中文可能显示为方块。" +
                    "请在 Inspector 拖入字体资产，或确认 Resources/Fonts/ 下有 NotoSerifCJKsc-Black SDF。",
                    this);
            }

            RectTransform textRect = m_text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(m_textPadding.x, m_textPadding.y);
            textRect.offsetMax = new Vector2(-m_textPadding.x, -m_textPadding.y);
        }

        // 渐变组：挂在黑底上，文字是黑底的子物体，跟着一起淡入淡出。
        // 独立于上面的创建分支——用户拖自定义黑底引用时同样生效
        m_fadeGroup = m_background.GetComponent<CanvasGroup>();
        if (m_fadeGroup == null)
        {
            m_fadeGroup = m_background.gameObject.AddComponent<CanvasGroup>();
        }
        m_fadeGroup.blocksRaycasts = false;
        m_fadeGroup.ignoreParentGroups = false;

        ApplyLayout();
    }

    // 根据当前字段值刷新一次尺寸/位置，让用户在 Inspector 改参数能立刻看到效果。
    // 同时同步颜色/字号：黑底和文字是运行时生成的，不同步的话 Inspector 改色不会生效
    private void ApplyLayout()
    {
        if (m_background == null)
        {
            return;
        }

        RectTransform rect = m_background.rectTransform;
        float canvasWidth = (m_canvas.transform as RectTransform).rect.width;
        float targetWidth = canvasWidth * m_widthRatio;
        rect.sizeDelta = new Vector2(targetWidth, m_minHeightInPixels);
        rect.anchoredPosition = new Vector2(0f, m_bottomMarginInPixels);

        m_background.color = m_backgroundColor;
        if (m_text != null)
        {
            m_text.color = m_textColor;
            m_text.fontSize = m_fontSize;
        }
    }

    private void FitBackgroundToText(string text)
    {
        if (m_background == null || m_text == null) return;
        float width = Mathf.Max(100, (m_canvas.transform as RectTransform).rect.width * m_widthRatio);
        Vector2 preferred = m_text.GetPreferredValues(text, width - m_textPadding.x * 2, Mathf.Infinity);
        m_background.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(m_minHeightInPixels, preferred.y + m_textPadding.y * 2));
    }

    private TMP_FontAsset ResolveFont()
    {
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
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }
        }
        catch (System.Exception)
        {
            // TMP Settings 不存在时静默
        }

        return null;
    }

    // 编辑器里改参数时立即刷新布局/颜色，运行中也生效（方便边播边调色）
    private void OnValidate()
    {
        if (m_background == null)
        {
            return;
        }
        ApplyLayout();
    }
}
