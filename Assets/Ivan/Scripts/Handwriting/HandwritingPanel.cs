using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Runtime panel for the handwriting sandbox: Record mode lets the developer
// teach glyphs (each saved sample becomes a $P template, effective instantly),
// Recognize mode lets the player write and matches the ink against the library
// with a confidence score. A successful match raises GlyphRecognized and logs —
// wiring that into EnvironmentManager reactions is the gameplay TODO.
//
// Toggle the panel with a key press (default H). UIDocument must reference
// HandwritingPanel.uxml; the InkCanvas is created in code and mounted into the
// canvas host element.
[RequireComponent(typeof(UIDocument))]
public class HandwritingPanel : MonoBehaviour
{
    private const string k_activeClass = "handwriting-panel__button--active";

    private enum PanelMode
    {
        Recognize,
        Record
    }

    [Header("Input")]
    [Tooltip("开关手写面板的按键")]
    [SerializeField] private Key m_toggleKey = Key.H;

    [Header("Recognition")]
    [Tooltip("置信度阈值：最高候选分数达到该值才算识别成功")]
    [SerializeField] private float m_confidenceThreshold = 0.82f;

    [Tooltip("抬笔后无新笔画的停顿时长，超过即自动识别（0 = 关闭自动识别）")]
    [SerializeField] private float m_autoRecognizeDelay = 1.0f;

    [Tooltip("每个样本重采样的点数（$P 默认 32；笔画多的象形字可提到 64）")]
    [SerializeField] private int m_resamplePoints = PointCloudRecognizer.k_defaultResamplePoints;

    [Header("Storage")]
    [Tooltip("模板库 JSON 路径（留空 = Editor 用 Assets/Ivan/Handwriting，真机用 persistentDataPath）")]
    [SerializeField] private string m_libraryPath = "";

    // Raised when the player's handwriting matched a glyph above threshold.
    // name = glyph name, confidence = normalized $P score in [0, 1].
    public event Action<string, float> GlyphRecognized;

    private HandwritingLibrary m_library;
    private string m_resolvedLibraryPath;
    private PanelMode m_mode = PanelMode.Recognize;
    private bool m_isOpen;

    private VisualElement m_panelRoot;
    private InkCanvas m_canvas;
    private Button m_modeRecognizeButton;
    private Button m_modeRecordButton;
    private VisualElement m_recordControls;
    private VisualElement m_recognizeControls;
    private TextField m_nameField;
    private Label m_resultLabel;
    private Label m_recordHintLabel;
    private Label m_libraryStatsLabel;

    private CancellationTokenSource m_autoRecognizeCts;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        m_panelRoot = root.Q<VisualElement>("handwriting-panel");
        m_modeRecognizeButton = root.Q<Button>("mode-recognize");
        m_modeRecordButton = root.Q<Button>("mode-record");
        m_recordControls = root.Q<VisualElement>("record-controls");
        m_recognizeControls = root.Q<VisualElement>("recognize-controls");
        m_nameField = root.Q<TextField>("glyph-name-field");
        m_resultLabel = root.Q<Label>("result-label");
        m_recordHintLabel = root.Q<Label>("record-hint");
        m_libraryStatsLabel = root.Q<Label>("library-stats");

        // InkCanvas is a code-built custom control: mount it into the host
        VisualElement canvasHost = root.Q<VisualElement>("canvas-host");
        m_canvas = new InkCanvas();
        m_canvas.AddToClassList("handwriting-panel__canvas");
        canvasHost.Add(m_canvas);

        m_canvas.StrokeStarted += HandleStrokeStarted;
        m_canvas.StrokeEnded += HandleStrokeEnded;

        m_modeRecognizeButton.clicked += () => SetMode(PanelMode.Recognize);
        m_modeRecordButton.clicked += () => SetMode(PanelMode.Record);
        root.Q<Button>("undo-button").clicked += m_canvas.UndoLastStroke;
        root.Q<Button>("clear-button").clicked += HandleClear;
        root.Q<Button>("recognize-button").clicked += RecognizeNow;
        root.Q<Button>("save-sample-button").clicked += HandleSaveSample;
        root.Q<Button>("delete-glyph-button").clicked += HandleDeleteGlyph;

        SetMode(PanelMode.Recognize);
    }

    private void Start()
    {
        // Library load happens in Start: OnEnable may run before the file
        // system settles on first import, and one load per session is enough
        m_resolvedLibraryPath = string.IsNullOrEmpty(m_libraryPath)
            ? HandwritingLibrary.DefaultLibraryPath()
            : m_libraryPath;

        m_library = new HandwritingLibrary(m_resamplePoints);
        m_library.Load(m_resolvedLibraryPath);
        UpdateLibraryStats();
    }

    private void OnDestroy()
    {
        m_autoRecognizeCts?.Cancel();
        m_autoRecognizeCts?.Dispose();
        m_autoRecognizeCts = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        var toggleKey = keyboard[m_toggleKey];
        if (toggleKey != null && toggleKey.wasPressedThisFrame)
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        m_isOpen = !m_isOpen;
        m_panelRoot.style.display = m_isOpen ? DisplayStyle.Flex : DisplayStyle.None;

        if (!m_isOpen)
        {
            CancelAutoRecognize();
        }
    }

    private void SetMode(PanelMode mode)
    {
        m_mode = mode;
        bool isRecognize = mode == PanelMode.Recognize;

        m_modeRecognizeButton.EnableInClassList(k_activeClass, isRecognize);
        m_modeRecordButton.EnableInClassList(k_activeClass, !isRecognize);

        m_recordControls.style.display = isRecognize ? DisplayStyle.None : DisplayStyle.Flex;
        m_recognizeControls.style.display = isRecognize ? DisplayStyle.Flex : DisplayStyle.None;

        CancelAutoRecognize();
    }

    // Any new pen-down invalidates a pending auto-recognition of the previous
    // (unfinished) glyph
    private void HandleStrokeStarted()
    {
        CancelAutoRecognize();
    }

    private void HandleStrokeEnded()
    {
        if (m_mode == PanelMode.Recognize && m_autoRecognizeDelay > 0f)
        {
            ScheduleAutoRecognize();
        }
    }

    private void HandleClear()
    {
        m_canvas.ClearInk();
        CancelAutoRecognize();

        if (m_mode == PanelMode.Recognize)
        {
            m_resultLabel.text = "写字试试…";
        }
    }

    private void ScheduleAutoRecognize()
    {
        m_autoRecognizeCts?.Cancel();
        m_autoRecognizeCts?.Dispose();
        m_autoRecognizeCts = new CancellationTokenSource();
        RunAutoRecognizeAsync(m_autoRecognizeCts.Token);
    }

    private async void RunAutoRecognizeAsync(CancellationToken token)
    {
        try
        {
            await Awaitable.WaitForSecondsAsync(m_autoRecognizeDelay, token);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a new stroke / mode switch / panel close
        }

        if (this == null || !isActiveAndEnabled || !m_isOpen)
        {
            return;
        }

        RecognizeNow();
    }

    private void CancelAutoRecognize()
    {
        m_autoRecognizeCts?.Cancel();
    }

    private void RecognizeNow()
    {
        if (m_mode != PanelMode.Recognize || m_library == null)
        {
            return;
        }

        if (!m_canvas.HasInk)
        {
            return;
        }

        List<PointCloudRecognizer.RecognitionCandidate> results =
            m_library.Recognize(m_canvas.GetSnapshot(), maxResults: 3);

        if (results.Count == 0)
        {
            m_resultLabel.text = "模板库为空，先用录入模式教几个字";
            return;
        }

        // Build "雨 0.93 | 雪 0.61 | 云 0.40" for the label / log
        string candidates = "";
        foreach (PointCloudRecognizer.RecognitionCandidate candidate in results)
        {
            candidates += (candidates.Length == 0 ? "" : " | ") + $"{candidate.name} {candidate.score:F2}";
        }

        PointCloudRecognizer.RecognitionCandidate best = results[0];
        if (best.score >= m_confidenceThreshold)
        {
            m_resultLabel.text = $"识别：{best.name}（{best.score:F2}）";

            // TODO gameplay: route into EnvironmentManager / scene reactions
            Debug.Log($"[HandwritingPanel] 识别成功「{best.name}」置信度 {best.score:F2} → 环境互动占位");
            GlyphRecognized?.Invoke(best.name, best.score);
        }
        else
        {
            m_resultLabel.text = $"未达阈值（{candidates}）";
            Debug.Log($"[HandwritingPanel] 未识别：最高候选 {best.name} {best.score:F2} < 阈值 {m_confidenceThreshold}");
        }
    }

    private void HandleSaveSample()
    {
        string glyphName = m_nameField.value == null ? "" : m_nameField.value.Trim();
        if (string.IsNullOrEmpty(glyphName))
        {
            m_recordHintLabel.text = "请先填写字名";
            return;
        }

        if (!m_canvas.HasInk)
        {
            m_recordHintLabel.text = "请先在纸上写字";
            return;
        }

        m_library.AddSample(glyphName, m_canvas.GetSnapshot());
        m_library.Save(m_resolvedLibraryPath);

        // Keep the name field: teaching a glyph means writing it several times
        m_canvas.ClearInk();
        m_recordHintLabel.text = $"「{glyphName}」已有 {m_library.GetSampleCount(glyphName)} 个模板";
        UpdateLibraryStats();
    }

    private void HandleDeleteGlyph()
    {
        string glyphName = m_nameField.value == null ? "" : m_nameField.value.Trim();
        if (string.IsNullOrEmpty(glyphName))
        {
            return;
        }

        if (m_library.DeleteGlyph(glyphName))
        {
            m_library.Save(m_resolvedLibraryPath);
            m_recordHintLabel.text = $"已删除「{glyphName}」的全部模板";
            UpdateLibraryStats();
        }
    }

    private void UpdateLibraryStats()
    {
        m_libraryStatsLabel.text = $"模板库：{m_library.GlyphCount} 字 / {m_library.SampleCount} 样本";
    }
}
