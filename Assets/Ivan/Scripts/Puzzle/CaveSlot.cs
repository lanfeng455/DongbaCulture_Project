using UnityEngine;

namespace Ivan.Puzzle
{
    // 洞穴槽位：Triggers 里只接受指定阵营的蛇。
    // 白蛇→北洞、黑蛇→南洞不靠代码方向判断，靠场景里摆在北/南即可。
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Cave Slot")]
    public class CaveSlot : MonoBehaviour
    {
        [Tooltip("本洞只接收哪一条蛇")]
        [SerializeField] private SnakeSide m_requiredSide = SnakeSide.White;

        // 正在出洞放行的蛇：它的碰撞体重开时还在触发区内，
        // 不能再把它吸回来（否则出洞→回捕→出洞无限抽搐），
        // 直到它真正走出触发区（OnTriggerExit）才恢复捕获资格。
        // 加一个超时兜底：DOMove 弹出距离小于 Trigger 半径时，蛇可能永远走不出 Trigger，
        // 超时后强制清理 releasing，让玩家可以再次推它进洞
        private SnakeActor m_releasing;
        private Collider m_trigger;
        private float m_releaseTimeout;   // 超时倒计时，> 0 时每帧递减

        private const float k_releaseTimeoutSeconds = 3f;   // 3 秒后强制恢复捕获资格

        public SnakeSide RequiredSide => m_requiredSide;

        private void Awake() { m_trigger = GetComponent<Collider>(); }

        private void FixedUpdate()
        {
            if (m_releasing == null || m_releasing.State == SnakeState.Exiting) return;
            var obstacle = m_releasing.GetComponent<Ivan.Diorama.DioramaObstacle>();
            var body = obstacle == null ? null : obstacle.Collider;
            // Exit animation keeps the snake collider disabled until it is outside.
            // In that case Unity never sends OnTriggerExit; clear the release guard
            // geometrically or this cave will permanently reject the returning snake.
            if (body != null && body.enabled && !m_trigger.bounds.Intersects(body.bounds))
                m_releasing = null;
        }

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }

        private void Update()
        {
            if (m_releaseTimeout > 0f)
            {
                m_releaseTimeout -= Time.deltaTime;
                if (m_releaseTimeout <= 0f)
                {
                    m_releasing = null;   // 超时：强制恢复
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var snake = other.GetComponentInParent<SnakeActor>();
            if (snake == null || snake.IsCaptured) return;
            if (snake.Side != m_requiredSide) return;
            if (snake == m_releasing) return;          // 出洞放行中，别把它吸回来

            m_releasing = null;
            m_releaseTimeout = 0f;
            snake.Capture(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var snake = other.GetComponentInParent<SnakeActor>();
            if (snake == null || snake != m_releasing) return;

            // 彻底离开触发区：放行结束，之后玩家再推进来照常捕获
            m_releasing = null;
            m_releaseTimeout = 0f;
        }

        // 由 SnakeActor.ExitCave 调用：这条蛇要出来了，放行直到它离开触发区或超时
        public void BeginRelease(SnakeActor snake)
        {
            if (snake != null)
            {
                m_releasing = snake;
                m_releaseTimeout = k_releaseTimeoutSeconds;
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = m_requiredSide == SnakeSide.White
                ? new Color(0.95f, 0.95f, 1f, 0.35f)
                : new Color(0.2f, 0.2f, 0.25f, 0.35f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
