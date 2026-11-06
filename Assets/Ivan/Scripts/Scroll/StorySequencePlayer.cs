using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Ivan.Diorama;

namespace Ivan.Scroll
{
    // 剧情节拍序列器：按顺序执行「对话 → 事件 → 等待 → 对话 …」的多步演出。
    // 与 Story Tripwire 的区别：绊线只能触发一段对话；本组件能在对话之间
    // 夹游戏事件（如让纠缠蛇对 Split）和停顿，适合谜题区的关键节拍。
    //
    // 触发方式（Inspector 选）：
    //   Cross X       玩家向右越过本物体的 X 坐标（与绊线相同）
    //   Enter Trigger 玩家进入本物体的触发器（接近传感器；
    //                 物体上没有 Collider 时自动创建一个 BoxCollider，自己调大小）
    //
    // 步骤类型（从上往下依次执行）：
    //   Dialogue  播对话库 from~to 句（等播完才进下一步）
    //   Wait      停顿指定秒数
    //   Event     调用 UnityEvent 里绑定的方法（把目标物体拖进来选方法即可）
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Story Sequence Player")]
    public class StorySequencePlayer : MonoBehaviour
    {
        public enum TriggerKind
        {
            CrossX,       // 玩家 X 越过本物体 X
            EnterTrigger, // 玩家进入本物体触发器
        }

        public enum StepKind
        {
            Dialogue,
            Wait,
            Event,
        }

        [System.Serializable]
        public class StoryStep
        {
            [Tooltip("这一步做什么")]
            public StepKind m_kind = StepKind.Dialogue;

            [Tooltip("对话起始条目索引（含）")]
            public int m_fromIndex = 0;

            [Tooltip("对话结束条目索引（含）")]
            public int m_toIndex = 0;

            [Tooltip("Wait 步的停顿秒数")]
            public float m_seconds = 1f;

            [Tooltip("Event 步要调用的方法：把目标物体拖进来选方法（如 EntangledSnakePair.Split）")]
            public UnityEvent m_onEvent = new();
        }

        [Header("触发")]
        [Tooltip("Cross X = 玩家向右越过本物体 X；Enter Trigger = 玩家进入本物体触发器")]
        [SerializeField] private TriggerKind m_trigger = TriggerKind.EnterTrigger;

        [Header("行走")]
        [Tooltip("演出期间锁住玩家移动（谜题区手动模式下同样生效），序列结束后自动恢复")]
        [SerializeField] private bool m_lockPlayerMovement = true;

        [Tooltip("停走型（仅卷轴自动模式生效）：对话步期间请导演站定，播完恢复")]
        [SerializeField] private bool m_pauseWalking = false;

        [Header("步骤（从上往下依次执行）")]
        [SerializeField] private List<StoryStep> m_steps = new();

        private DioramaPlayerController m_player;
        private ScrollPlayDirector m_director;
        private bool m_hasFired;

        private void Awake()
        {
            if (m_trigger != TriggerKind.EnterTrigger) return;

            // 接近触发需要碰撞体；用户没摆就自动给一个能用的 Box，再自己调大小
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2.5f, 2f, 4f);
                box.center = new Vector3(0f, 1f, 0f);
            }
            else if (!collider.isTrigger)
            {
                // 已有碰撞体但不是触发器：会变成一堵物理墙把玩家挡在绊线前，
                // 表现为"快碰到就停下、播完也不动"——强制改成触发器
                collider.isTrigger = true;
            }
        }

        private void Start()
        {
            m_player = FindFirstObjectByType<DioramaPlayerController>();
            m_director = FindFirstObjectByType<ScrollPlayDirector>();
        }

        private void Update()
        {
            if (m_trigger != TriggerKind.CrossX || m_hasFired || m_player == null) return;

            if (m_player.transform.position.x >= transform.position.x)
            {
                Fire();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_trigger != TriggerKind.EnterTrigger || m_hasFired) return;

            // 只认玩家（玩家可能挂在父级层级下，向上找）
            if (other.GetComponentInParent<DioramaPlayerController>() == null) return;

            Fire();
        }

        // 编辑器预演 / 外部强制触发
        public void Fire()
        {
            if (m_hasFired) return;
            m_hasFired = true;

            _ = RunSequenceAsync();
        }

        private async Awaitable RunSequenceAsync()
        {
            // 演出期间锁玩家移动（可选）；用 try/finally 保证序列提前结束也恢复
            if (m_lockPlayerMovement && m_player != null)
            {
                m_player.SetStageMovementLock(true);
            }

            try
            {
                foreach (StoryStep step in m_steps)
                {
                    // 组件被销毁/禁用就中断，防止场景切换后继续跑
                    if (this == null || !isActiveAndEnabled) return;

                    switch (step.m_kind)
                    {
                        case StepKind.Dialogue:
                        {
                            var display = DialogueDisplay.Instance;
                            if (display == null) continue;   // 场景没对话组件：跳过对话步，别卡死序列

                            display.PlayRange(step.m_fromIndex, step.m_toIndex);
                            if (m_pauseWalking && m_director != null)
                            {
                                m_director.HoldForDialogue();
                            }
                            await display.WaitUntilPlaybackStopsAsync();
                            break;
                        }

                        case StepKind.Wait:
                        {
                            if (step.m_seconds > 0f)
                            {
                                await Awaitable.WaitForSecondsAsync(step.m_seconds);
                            }
                            break;
                        }

                        case StepKind.Event:
                        {
                            step.m_onEvent?.Invoke();
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (m_lockPlayerMovement && m_player != null)
                {
                    m_player.SetStageMovementLock(false);
                }
            }
        }

        // 金色 = 触发范围/触发线；触发后变灰
        private void OnDrawGizmos()
        {
            Gizmos.color = m_hasFired
                ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                : new Color(1f, 0.8f, 0.2f, 0.8f);

            if (m_trigger == TriggerKind.CrossX)
            {
                Vector3 basePos = transform.position;
                Gizmos.DrawLine(basePos + new Vector3(0f, 0f, -30f), basePos + new Vector3(0f, 0f, 30f));
            }
            else
            {
                var collider = GetComponent<Collider>();
                if (collider != null)
                {
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + new Vector3(0f, 2.5f, 0f),
                $"Sequence [{m_steps.Count} steps]{(m_hasFired ? " (done)" : "")}");
#endif
        }
    }
}
