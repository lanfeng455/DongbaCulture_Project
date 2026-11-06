using UnityEngine;

// 信号信道：ScriptableObject 资产，天然单例。
// Sender 和 Receiver 各自在 Inspector 里引用同一个资产，零直接依赖。
// Send 在信道派发时，所有订阅者按订阅顺序同步执行；如果某个 Receiver
// 正在跑延迟链（CancellationTokenSource 持有），它自己会负责取消旧链。
[CreateAssetMenu(fileName = "SignalChannel", menuName = "Signals/Signal Channel", order = 0)]
public class SignalChannel : ScriptableObject
{
    // 信道里可以携带一个可选的消息字符串（供调试日志 / 外部系统参考）
    public delegate void SignalHandler(string message);

    private event SignalHandler m_Handlers;

#if UNITY_EDITOR
    [Header("调试")]
    [Tooltip("Send 时在 Console 打一条消息；开发时开，发布时关")]
    [SerializeField] private bool m_logSignals = false;
#endif

    // 发送信号——所有订阅者同步收到通知
    public void Send(string message = null)
    {
#if UNITY_EDITOR
        if (m_logSignals)
        {
            // 没人订阅是最常见的"发了没反应"原因：接收方可能挂在未激活的物体上，
            // 它的 OnEnable 没跑过，自然没订阅
            if (m_Handlers == null)
            {
                Debug.LogWarning(
                    $"[{nameof(SignalChannel)}:{name}] 发信号时没有任何订阅者，" +
                    "检查 Receiver 所在 GameObject 是否处于激活状态", this);
            }

            Debug.Log($"[{nameof(SignalChannel)}:{name}] Send(\"{message}\")", this);
        }
#endif
        m_Handlers?.Invoke(message);
    }

    // 订阅信道——返回一个 unsubscribe 委托，方便一次性退订
    public void Subscribe(SignalHandler handler)
    {
        m_Handlers += handler;
    }

    public void Unsubscribe(SignalHandler handler)
    {
        m_Handlers -= handler;
    }

    // 清空所有订阅（仅用于场景切换/测试重置，正常流程不需要）
    public void Clear()
    {
        m_Handlers = null;
    }
}
