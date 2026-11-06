using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

// 信号响应条目：一个可序列化的 class（Unity 不能序列化泛型 struct 列表里的 UnityEvent）
// 表示"收到信号后 delayInSeconds 秒，触发 response"
[System.Serializable]
public class SignalResponseEntry
{
    [Tooltip("延迟多少秒触发；0 = 收到信号立即执行")]
    public float delayInSeconds;

    [Tooltip("到时执行的操作；可以挂多个 Unity 事件（SetActive / PlayClipAtPoint / DOTween / ...）")]
    public UnityEvent response;
}

// 信号接收器：订阅某个 SignalChannel，收到信号后按 List 顺序逐条延迟执行 UnityEvent。
//
// 典型用法（门打开信号）：
//   Entry 0: delay=0s,   response=门体 SetActive(false)
//   Entry 1: delay=2s,   response=室内灯 SetActive(true) + 播放环境音
//   Entry 2: delay=3s,   response=触发下一个机关的 SetActive(true)
//
// 重入语义：如果上一轮延迟链还在跑时又收到信号，**先 Cancel 旧链再开新链**
// （OnDisable 也会 Cancel，避免物体销毁后事件还在执行）。
public class SignalReceiver : MonoBehaviour
{
    [Header("信道")]
    [Tooltip("要订阅的 SignalChannel 资产（ScriptableObject）")]
    [SerializeField] private SignalChannel m_channel;

    [Header("响应序列")]
    [Tooltip("收到信号后，按此顺序逐条执行；delay 是累积延迟（条目 0 从 0 秒开始算）")]
    [SerializeField] private List<SignalResponseEntry> m_responses = new List<SignalResponseEntry>();

    private CancellationTokenSource m_activeCts;

    // 让其他脚本可以运行时往响应序列里追加条目
    public void AddResponse(SignalResponseEntry entry)
    {
        if (entry != null)
        {
            m_responses.Add(entry);
        }
    }

    private void OnEnable()
    {
        if (m_channel != null)
        {
            m_channel.Subscribe(HandleSignal);
        }
    }

    private void OnDisable()
    {
        // 先取消正在跑的延迟链——物体关掉了，后续事件别执行了
        CancelActiveChain();

        if (m_channel != null)
        {
            m_channel.Unsubscribe(HandleSignal);
        }
    }

    // 信道派发时的入口——同步执行，然后开一个 async 延迟链
    private void HandleSignal(string message)
    {
        // 重入保护：上一轮还没跑完？先 Cancel 掉
        CancelActiveChain();

        if (m_responses == null || m_responses.Count == 0)
        {
            return;
        }

        m_activeCts = new CancellationTokenSource();
        _ = RunResponseChainAsync(m_activeCts.Token);
    }

    // 延迟链逐条执行：累计 delay，每条 await 后再 Invoke
    private async Awaitable RunResponseChainAsync(CancellationToken token)
    {
        float accumulatedDelay = 0f;

        try
        {
            foreach (SignalResponseEntry entry in m_responses)
            {
                if (entry == null)
                {
                    continue;
                }

                accumulatedDelay += Mathf.Max(0f, entry.delayInSeconds);

                if (accumulatedDelay > 0f)
                {
                    await Awaitable.WaitForSecondsAsync(accumulatedDelay, token);
                }

                // Cancel 后别再 Invoke 了
                if (token.IsCancellationRequested || this == null)
                {
                    return;
                }

                entry.response?.Invoke();
            }
        }
        catch (System.Exception)
        {
            // await WaitForSecondsAsync 在 Cancel 时可能抛 OperationCanceledException；
            // 我们不直接 catch OCE 因为 Unity 某些编译配置下它不在可引用的程序集里。
            // 但不管是什么异常，只要 token 已经 Cancel，就静默返回——后续代码不会再执行。
            if (!token.IsCancellationRequested)
            {
                throw; // 不是取消导致的异常，重新抛出让开发者看到
            }
        }
    }

    private void CancelActiveChain()
    {
        if (m_activeCts != null)
        {
            m_activeCts.Cancel();
            m_activeCts.Dispose();
            m_activeCts = null;
        }
    }
}
