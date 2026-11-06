using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Scroll
{
    // 卷轴演出导演：自动 / 手动两种观看模式。
    //   Auto：接管玩家移动输入，一路向右走到 End X（边走边说，对话由绊线自己触发）。
    //   Manual：什么都不注入，玩家自己走，绊线照常触发。
    // 到达终点后自动交还控制权（谜题部分始终手动玩）。
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Scroll Play Director")]
    public class ScrollPlayDirector : MonoBehaviour
    {
        public enum PlayMode
        {
            Manual,     // 玩家自己走
            Auto,       // 导演带着走
        }

        [Tooltip("观看模式。Auto = 自动向右走到终点；Manual = 玩家自由操作")]
        [SerializeField] private PlayMode m_mode = PlayMode.Auto;

        [Tooltip("自动行走的终点 X（一般放在谜题区左边缘），到达后交还控制权")]
        [SerializeField] private float m_endX = 40f;

        [Tooltip("自动行走速度（米/秒），0 = 沿用玩家自身移速")]
        [SerializeField] private float m_autoWalkSpeed = 0f;

        private DioramaPlayerController m_player;
        private bool m_finished;
        private bool m_holdForDialogue;     // 停走型绊线要求：站定等当前对话播完
        private bool m_holdAwaitingStart;   // hold 第一阶段：对话还没真正开始（IsPlaying 尚未变 true）
        private float m_holdStartTimeout;   // 对话迟迟不启动的兜底计时，防止把玩家永久锁死

        private const float k_holdStartTimeoutSeconds = 1f;

        public PlayMode Mode => m_mode;
        public bool HasFinished => m_finished;

        // 停走型绊线（StoryTripwire.PauseWalking）触发时调用：
        // 角色原地站定，DialogueDisplay 播完再恢复行走。
        // 内部分两阶段：先等对话真正开始（IsPlaying 变 true），再等它结束；
        // 这样既不会被 PlayRange 调用帧的空窗骗过，也不会因对话启动失败而永久锁死玩家
        public void HoldForDialogue()
        {
            m_holdForDialogue = true;
            m_holdAwaitingStart = true;
            m_holdStartTimeout = k_holdStartTimeoutSeconds;
        }

        // 外部（UI 按钮、过场脚本）可随时切换模式
        public void SetMode(PlayMode mode)
        {
            m_mode = mode;
            if (mode == PlayMode.Manual && m_player != null)
            {
                m_player.ClearAutoMoveInput();
            }
        }

        private void Start()
        {
            m_player = FindFirstObjectByType<DioramaPlayerController>();
            if (m_player == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 场景里没有 DioramaPlayerController，自动行走不可用。", this);
            }
        }

        private void Update()
        {
            if (m_mode != PlayMode.Auto || m_finished || m_player == null) return;

            if (m_player.transform.position.x >= m_endX)
            {
                // 抵达终点：交还控制权，之后的手动操作（谜题）不受影响
                m_finished = true;
                m_player.ClearAutoMoveInput();
                return;
            }

            // 停走型对话：站定（保持接管、输入置零），对话播完自动恢复。
            // 两阶段判定 + 超时兜底，避免与 DialogueDisplay 的帧级时序竞态
            if (m_holdForDialogue)
            {
                m_player.SetAutoMoveInput(Vector2.zero);

                var display = DialogueDisplay.Instance;
                if (display == null)
                {
                    m_holdForDialogue = false;
                }
                else if (m_holdAwaitingStart)
                {
                    // 阶段一：等对话真正开始（容忍 PlayRange 调用帧的空窗）
                    if (display.IsPlaying)
                    {
                        m_holdAwaitingStart = false;
                    }
                    else
                    {
                        m_holdStartTimeout -= Time.deltaTime;
                        if (m_holdStartTimeout <= 0f)
                        {
                            // 对话没启动（空段/配置错误/被吞）：放弃 hold，别把玩家锁死
                            m_holdForDialogue = false;
                        }
                    }
                }
                else if (!display.IsPlaying)
                {
                    // 阶段二：对话已开始过，现在结束 → 恢复行走
                    m_holdForDialogue = false;
                }

                if (m_holdForDialogue)
                {
                    return;
                }
            }

            // 每帧持续注入：速度差异通过按玩家实际移速折算输入幅度实现，0 表示全速
            if (m_autoWalkSpeed > 0f && m_player.MoveSpeed > 0f)
            {
                float ratio = Mathf.Clamp01(m_autoWalkSpeed / m_player.MoveSpeed);
                m_player.SetAutoMoveInput(Vector2.right * ratio);
            }
            else
            {
                m_player.SetAutoMoveInput(Vector2.right);
            }
        }

        private void OnDisable()
        {
            if (m_player != null)
            {
                m_player.ClearAutoMoveInput();
            }
        }
    }
}
