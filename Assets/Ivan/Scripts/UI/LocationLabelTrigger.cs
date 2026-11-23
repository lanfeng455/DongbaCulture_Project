using UnityEngine;
using UnityEngine.Events;

// 进入/离开 Trigger 时触发 LocationLabelHUD.SetLabel，用来在 Puzzle 场景里切换"森林/山顶"等定位标签。
// 可配置：一次触发、指定 Tag 过滤、Enter/Exit 各用独立 UnityEvent。
// Inspector 里把 LocationLabelHUD.SetLabel 拖到 onEnterLabel / onExitLabel 即可。
[RequireComponent(typeof(Collider))]
public class LocationLabelTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("可选：直接引用一个 LocationLabelHUD，Enter 时自动调 SetLabel(m_enterText)")]
    [SerializeField] private LocationLabelHUD m_hud;

    [Header("Enter behavior")]
    [Tooltip("进入 Trigger 时要显示的文字（hud 非空时自动 SetLabel；留空即不自动改）")]
    [SerializeField] private string m_enterText = "";
    [Tooltip("给外部（UnityEvent / 事件总线）额外留的回调")]
    [SerializeField] private UnityEvent<string> m_onEnterLabel;

    [Header("Exit behavior")]
    [Tooltip("离开 Trigger 时要恢复的文字；留空即不恢复")]
    [SerializeField] private string m_exitText = "";
    [SerializeField] private UnityEvent<string> m_onExitLabel;

    [Header("Filtering")]
    [Tooltip("留空 = 任何物体都能触发；填了 Tag 就只对带此 Tag 的物体响应")]
    [SerializeField] private string m_requiredTag = "";
    [Tooltip("true = 只对首次进入触发一次；false = 每次进/出都触发")]
    [SerializeField] private bool m_triggerOnce = false;

    bool m_hasTriggered;

    void Reset()
    {
        // Collider 上默认勾 isTrigger
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValid(other)) return;
        if (m_triggerOnce && m_hasTriggered) return;
        m_hasTriggered = true;

        ApplyLabel(m_enterText);
        m_onEnterLabel?.Invoke(m_enterText);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValid(other)) return;
        if (m_triggerOnce) return; // 一次模式 Exit 不恢复

        ApplyLabel(m_exitText);
        m_onExitLabel?.Invoke(m_exitText);
    }

    bool IsValid(Collider c)
    {
        return string.IsNullOrEmpty(m_requiredTag) || c.CompareTag(m_requiredTag);
    }

    void ApplyLabel(string text)
    {
        if (m_hud != null && !string.IsNullOrEmpty(text)) m_hud.SetLabel(text);
    }
}
