using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Transform 动画组件：用 DOTween 驱动任意组合的平移 / 旋转 / 缩放，
// 方便挂在机关、道具、架子上做小动画。
//
// 设计要点：
//   - 每个"动画"是一条 TransformAnimEntry，三个通道（位置/旋转/缩放）各自独立可选
//   - 目标值可选世界空间或本地空间；位置可以是绝对目标也可以是相对偏移
//   - 时长、缓动、延迟、循环次数每通道单独配
//   - 支持链式：前一个 entry 完成才播下一个
//   - 提供 Play() / Stop() / PlayOnEnable，SignalReceiver 的 UnityEvent 直接 Play() 即可
//
// 典型用法：门开、架子升起、道具归位时弹一下、解谜完成后机关动画等
[DisallowMultipleComponent]
public class TransformAnimator : MonoBehaviour
{
    // 一条动画：位置/旋转/缩放各一条子通道，互不干扰、独立配时。
    // 任意通道 CanPlay = false 时自动跳过，灵活组合出 8 种动画
    [System.Serializable]
    public struct TransformAnimEntry
    {
        [Header("位置")]
        public bool usePosition;

        [Tooltip("目标是绝对坐标还是从当前位置加偏移")]
        public bool positionIsOffset;

        public Vector3 positionValue;
        public float positionDuration;
        public Ease positionEase;
        public float positionDelay;

        [Header("旋转")]
        public bool useRotation;

        [Tooltip("目标是绝对角度还是从当前加偏移")]
        public bool rotationIsOffset;

        public Vector3 rotationEuler;
        public float rotationDuration;
        public Ease rotationEase;
        public float rotationDelay;

        [Header("缩放")]
        public bool useScale;

        [Tooltip("目标是绝对缩放还是从当前加偏移")]
        public bool scaleIsOffset;

        public Vector3 scaleValue;
        public float scaleDuration;
        public Ease scaleEase;
        public float scaleDelay;

        [Header("空间")]
        [Tooltip("位置和旋转用世界空间还是本地空间；缩放永远是本地")]
        public bool useWorldSpace;

        [Header("循环")]
        [Tooltip("0 = 不循环；-1 = 无限循环；正数 = 循环次数")]
        public int loops;

        [Tooltip("循环模式：Restart = 每次从头来；Yoyo = 来回弹")]
        public LoopType loopType;
    }

    [Header("动画序列")]
    [Tooltip("按顺序播放，前一个 entry 的所有通道都完成后才播下一个")]
    [SerializeField] private List<TransformAnimEntry> m_entries = new List<TransformAnimEntry>();

    [Header("触发")]
    [Tooltip("组件启用时自动播一遍")]
    [SerializeField] private bool m_playOnEnable;

    [Header("目标")]
    [Tooltip("要动画的 Transform；留空则用本物体")]
    [SerializeField] private Transform m_target;

    private readonly List<Tween> m_activeTweens = new List<Tween>();

    // 正在播放？至少有一条活跃 Tween
    public bool IsPlaying => m_activeTweens.Count > 0;

    public Transform Target => m_target != null ? m_target : transform;

    private void OnEnable()
    {
        if (m_playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    // 播整个序列，停止当前正在播的
    public void Play()
    {
        Stop();

        Transform target = Target;
        if (target == null || m_entries == null || m_entries.Count == 0)
        {
            return;
        }

        float sequenceStart = 0f;

        for (int i = 0; i < m_entries.Count; i++)
        {
            TransformAnimEntry entry = m_entries[i];
            float entryEnd = BuildEntryTweens(target, entry, sequenceStart);

            // 下一个 entry 从当前 entry 所有通道里最晚的那个开始
            sequenceStart += entryEnd;
        }
    }

    // 只播某一个 entry（索引超出就不播）
    public void PlayEntry(int index)
    {
        Stop();

        Transform target = Target;
        if (target == null || m_entries == null || index < 0 || index >= m_entries.Count)
        {
            return;
        }

        BuildEntryTweens(target, m_entries[index], 0f);
    }

    // 停止所有活跃的 Tween，Transform 停在当前状态（不回原位）
    public void Stop()
    {
        for (int i = 0; i < m_activeTweens.Count; i++)
        {
            if (m_activeTweens[i] != null && m_activeTweens[i].IsActive())
            {
                m_activeTweens[i].Kill();
            }
        }

        m_activeTweens.Clear();
    }

    // 硬重置：把 Transform 写回初始值（由第一个 entry 的起点定义），停掉所有 Tween
    public void ResetToInitial()
    {
        Stop();

        Transform target = Target;
        if (target == null || m_entries == null || m_entries.Count == 0)
        {
            return;
        }

        // 回退逻辑：对 offset 模式的通道做减法
        for (int i = m_entries.Count - 1; i >= 0; i--)
        {
            TransformAnimEntry entry = m_entries[i];
            if (entry.usePosition && entry.positionIsOffset)
            {
                target.position -= entry.positionValue;
            }

            if (entry.useRotation && entry.rotationIsOffset)
            {
                target.eulerAngles -= entry.rotationEuler;
            }

            if (entry.useScale && entry.scaleIsOffset)
            {
                target.localScale -= entry.scaleValue;
            }
        }
    }

    // 构建一条 entry 的所有通道 Tween，返回所有通道里最晚开始的时间
    private float BuildEntryTweens(Transform target, TransformAnimEntry entry, float sequenceStart)
    {
        float entryEnd = 0f;

        if (entry.usePosition)
        {
            Vector3 from = entry.useWorldSpace ? target.position : target.localPosition;
            Vector3 to = entry.positionIsOffset ? from + entry.positionValue : entry.positionValue;

            // 本地空间目标需要换算（如果用了 world space 选项但目标是本地坐标）
            Tweener tween;
            if (entry.useWorldSpace)
            {
                tween = target.DOMove(to, Mathf.Max(0.001f, entry.positionDuration));
            }
            else
            {
                tween = target.DOLocalMove(to, Mathf.Max(0.001f, entry.positionDuration));
            }

            tween.SetEase(entry.positionEase)
                 .SetDelay(sequenceStart + entry.positionDelay)
                 .SetLoops(
                     entry.loops,
                     entry.loops != 0 && entry.loopType == LoopType.Yoyo
                         ? LoopType.Yoyo
                         : LoopType.Restart);

            m_activeTweens.Add(tween);
            entryEnd = Mathf.Max(entryEnd, entry.positionDelay + entry.positionDuration);
        }

        if (entry.useRotation)
        {
            Vector3 to = entry.rotationEuler;
            if (entry.rotationIsOffset)
            {
                Quaternion from = entry.useWorldSpace ? target.rotation : target.localRotation;
                to = (from * Quaternion.Euler(entry.rotationEuler)).eulerAngles;
            }

            Tweener tween;
            if (entry.useWorldSpace)
            {
                tween = target.DORotate(to, Mathf.Max(0.001f, entry.rotationDuration), RotateMode.FastBeyond360);
            }
            else
            {
                tween = target.DOLocalRotate(to, Mathf.Max(0.001f, entry.rotationDuration), RotateMode.FastBeyond360);
            }

            tween.SetEase(entry.rotationEase)
                 .SetDelay(sequenceStart + entry.rotationDelay)
                 .SetLoops(
                     entry.loops,
                     entry.loops != 0 && entry.loopType == LoopType.Yoyo
                         ? LoopType.Yoyo
                         : LoopType.Restart);

            m_activeTweens.Add(tween);
            entryEnd = Mathf.Max(entryEnd, entry.rotationDelay + entry.rotationDuration);
        }

        if (entry.useScale)
        {
            Vector3 from = target.localScale;
            Vector3 to = entry.scaleIsOffset ? from + entry.scaleValue : entry.scaleValue;

            Tweener tween = target.DOScale(to, Mathf.Max(0.001f, entry.scaleDuration));

            tween.SetEase(entry.scaleEase)
                 .SetDelay(sequenceStart + entry.scaleDelay)
                 .SetLoops(
                     entry.loops,
                     entry.loops != 0 && entry.loopType == LoopType.Yoyo
                         ? LoopType.Yoyo
                         : LoopType.Restart);

            m_activeTweens.Add(tween);
            entryEnd = Mathf.Max(entryEnd, entry.scaleDelay + entry.scaleDuration);
        }

        return entryEnd;
    }
}
