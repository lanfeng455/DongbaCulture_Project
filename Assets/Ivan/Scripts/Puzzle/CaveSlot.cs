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
        // 直到它真正走出触发区（OnTriggerExit）才恢复捕获资格
        private SnakeActor m_releasing;

        public SnakeSide RequiredSide => m_requiredSide;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var snake = other.GetComponentInParent<SnakeActor>();
            if (snake == null || snake.IsCaptured) return;
            if (snake.Side != m_requiredSide) return;
            if (snake == m_releasing) return;          // 出洞放行中，别把它吸回来

            m_releasing = null;
            snake.Capture(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var snake = other.GetComponentInParent<SnakeActor>();
            if (snake == null || snake != m_releasing) return;

            // 彻底离开触发区：放行结束，之后玩家再推进来照常捕获
            m_releasing = null;
        }

        // 由 SnakeActor.ExitCave 调用：这条蛇要出来了，放行直到它离开触发区
        public void BeginRelease(SnakeActor snake)
        {
            if (snake != null)
            {
                m_releasing = snake;
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
