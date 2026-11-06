using System.Collections.Generic;
using UnityEngine;

using Ivan.Diorama;

namespace Ivan.Scroll
{
    // 舞台演员：挂在参与对话演出的 NPC 上，Actor Name 对应对话表的 Speaker 列。
    // 对话里的 @指令（向右看 / 移动 / 隐藏……）按名字找到这里执行。
    // 同名只认最新注册的一个。
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Stage Actor")]
    public class StageActor : MonoBehaviour
    {
        [Tooltip("演员名，必须与对话表 Speaker 列一致（不分大小写）")]
        [SerializeField] private string m_actorName = "Fox";

        [Tooltip("精灵渲染器；留空取自身或子物体里的第一个")]
        [SerializeField] private SpriteRenderer m_renderer;

        [Tooltip("素材默认朝向：原画朝右不勾、朝左勾上（影响翻转逻辑）")]
        [SerializeField] private bool m_defaultFacesLeft = false;

        private float m_moveSpeed;
        private float m_cruiseSpeed;      // 巡航速度：最近一次 @以N为速度移动 的设定，跨段/停止后仍保留
        private bool m_hasMoveTarget;    // @移动到 定点移动进行中
        private float m_moveTargetX;     // 目标为父物体局部坐标 X（Inspector 里 Transform 显示的值）
        private bool m_hasFacingLock;    // 演出转向后锁定，LateUpdate 每帧维持
        private bool m_lockedFaceLeft;

        public string ActorName => m_actorName;

        // 定点移动是否已完成（没有进行中的目标即视为已到达）
        public bool HasReachedMoveTarget => !m_hasMoveTarget;

        private void Awake()
        {
            if (m_renderer == null)
            {
                m_renderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            StageActorRegistry.Register(this);
        }

        private void OnDisable()
        {
            StageActorRegistry.Unregister(this);
        }

        private void Update()
        {
            if (m_hasMoveTarget)
            {
                // 定点移动：朝目标局部 X 走，到达即停（装饰性行走，不参与物理碰撞）
                Vector3 local = transform.localPosition;
                float newX = Mathf.MoveTowards(local.x, m_moveTargetX, m_moveSpeed * Time.deltaTime);
                transform.localPosition = new Vector3(newX, local.y, local.z);

                if (Mathf.Approximately(newX, m_moveTargetX))
                {
                    m_hasMoveTarget = false;
                    m_moveSpeed = 0f;
                }
            }
            else if (m_moveSpeed > 0f)
            {
                // 持续移动：沿当前朝向直线走（装饰性行走，不参与物理碰撞）
                float direction = IsFacingLeft ? -1f : 1f;
                transform.position += new Vector3(direction, 0f, 0f) * (m_moveSpeed * Time.deltaTime);
            }
        }

        // 演出转向是持续状态：每帧维持翻转，抵抗 Animator / 其他组件对 flipX 的覆盖
        private void LateUpdate()
        {
            if (m_hasFacingLock && m_renderer != null)
            {
                m_renderer.flipX = m_lockedFaceLeft;
            }
        }

        private bool IsFacingLeft => m_renderer != null && m_renderer.flipX != m_defaultFacesLeft;

        // directionX 正 = 朝右（世界 +X），负 = 朝左
        public void Face(float directionX)
        {
            // 玩家控制器的视觉刷新每帧按移动方向重写 flipX，
            // 直接翻渲染器下一帧就会被抹掉，必须走它的演出朝向锁定口。
            // 注意 StageActor 常挂在身体子物体上、控制器在父物体上，只查自身会漏，
            // 一旦漏了就会走下面的翻转锁，LateUpdate 每帧把 flipX 按回去（转向动画播完弹回）
            var player = GetComponent<DioramaPlayerController>();
            if (player == null)
            {
                player = GetComponentInParent<DioramaPlayerController>();
            }
            if (player != null)
            {
                // 解除可能残留的翻转锁，避免和控制器抢 flipX
                m_hasFacingLock = false;
                player.SetStageFacing(directionX);
                return;
            }

            m_hasFacingLock = true;
            m_lockedFaceLeft = (directionX < 0f) != m_defaultFacesLeft;
            if (m_renderer != null)
            {
                m_renderer.flipX = m_lockedFaceLeft;
            }
        }

        public void StartMove(float speed)
        {
            m_moveSpeed = Mathf.Max(0f, speed);
            m_cruiseSpeed = m_moveSpeed;   // 记住巡航速度，之后 @移动到 不用再赋一遍
        }

        // @停止：停止移动（同时打断进行中的 @移动到），精灵保持可见
        public void StopMove()
        {
            m_moveSpeed = 0f;
            m_hasMoveTarget = false;
        }

        // @移动到x：走到目标 X 才继续下一行。目标为父物体局部坐标 X
        // （即 Inspector 里 Transform 显示的 Position.x，支持负数）。
        // 速度取当前移速；当前已停（到达过/被叫停）则回退到巡航速度——
        // @以N为速度移动 全局只需设一次，之后所有 @移动到 沿用
        public void MoveTo(float targetX)
        {
            if (m_moveSpeed <= 0f && m_cruiseSpeed > 0f)
            {
                m_moveSpeed = m_cruiseSpeed;
            }

            if (m_moveSpeed <= 0f)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] 「{m_actorName}」从未设过移速，请先在任意一段对话里写一次 @以N为速度移动。该指令已跳过。",
                    this);
                return;
            }

            m_moveTargetX = targetX;
            m_hasMoveTarget = true;

            // 朝目标转身（目标在右边朝右，在左边朝左；按局部坐标判断）
            Face(targetX >= transform.localPosition.x ? 1f : -1f);
        }

        public void Hide()
        {
            StopMove();
            if (m_renderer != null)
            {
                m_renderer.enabled = false;
            }
        }

        // 预留：@显示 复出用（当前范式未包含，加指令时一行接上即可）
        public void Show()
        {
            if (m_renderer != null)
            {
                m_renderer.enabled = true;
            }
        }
    }

    // Speaker 名 → StageActor 注册表：对话指令按名字找人，找不到就警告并跳过
    public static class StageActorRegistry
    {
        private static readonly Dictionary<string, StageActor> s_byName = new(8);

        public static void Register(StageActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.ActorName)) return;
            s_byName[actor.ActorName.Trim().ToLowerInvariant()] = actor;
        }

        public static void Unregister(StageActor actor)
        {
            if (actor == null) return;
            s_byName.Remove(actor.ActorName.Trim().ToLowerInvariant());
        }

        public static StageActor Find(string speaker)
        {
            if (!string.IsNullOrWhiteSpace(speaker) &&
                s_byName.TryGetValue(speaker.Trim().ToLowerInvariant(), out StageActor actor) &&
                actor != null)
            {
                return actor;
            }

            Debug.LogWarning($"[StageActorRegistry] 场景里没有名为「{speaker}」的 Stage Actor，该指令被跳过。");
            return null;
        }
    }
}
