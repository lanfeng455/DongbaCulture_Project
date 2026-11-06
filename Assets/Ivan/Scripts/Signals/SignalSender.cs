using UnityEngine;

// 信号发送器：在指定时机向某个 SignalChannel 发送信号。
// 典型用途：门打开时 OnEnable 发信号 → 场景里所有接收器各自执行自己的响应链
public class SignalSender : MonoBehaviour
{
    public enum TriggerMode
    {
        OnEnable,  // GameObject 被激活时（最常用，门被打开/机关被触发）
        Start,     // 场景启动时发一次
        Manual     // 只通过 Public API 调用 Send()
    }

    [Header("信道")]
    [Tooltip("要发送到的 SignalChannel 资产（ScriptableObject）")]
    [SerializeField] private SignalChannel m_channel;

    [Header("触发时机")]
    [SerializeField] private TriggerMode m_triggerMode = TriggerMode.OnEnable;

    [Tooltip("发送时附带的消息文本；Receiver 可以在日志里看到")]
    [SerializeField] private string m_message;

    [Tooltip("防止 OnEnable/Start 多次触发：默认只发一次")]
    [SerializeField] private bool m_sendOnce = true;

    private bool m_hasSent;

    // 让其他脚本可以直接调——比如某个机关被触发了，从代码里主动发信号
    public void Send()
    {
        if (m_channel == null)
        {
            Debug.LogWarning($"[{GetType().Name}] m_channel 没赋值，信号发不出去。", this);
            return;
        }

        // m_sendOnce 只用来挡 OnEnable / Start 的重复触发。
        // Manual 模式下发送节奏完全由调用方决定，不能被"已发过"卡住
        if (m_sendOnce && m_triggerMode != TriggerMode.Manual && m_hasSent)
        {
            return;
        }

        m_hasSent = true;
        m_channel.Send(m_message);
    }

    // Manual 模式下可以调这个重置，让下一次 Send() 又能发
    public void Reset()
    {
        m_hasSent = false;
    }

    private void OnEnable()
    {
        if (m_triggerMode == TriggerMode.OnEnable)
        {
            Send();
        }
    }

    private void Start()
    {
        if (m_triggerMode == TriggerMode.Start)
        {
            Send();
        }
    }
}
