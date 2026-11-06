using DG.Tweening;
using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // 纠缠状态的两条蛇：两个精灵交叉叠在一起，玩家触碰（或按 E）后分开成两条可行动的蛇。
    // 时间轴上它只负责「分开」这一件事，分开后自己退场。
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Entangled Snake Pair")]
    public class EntangledSnakePair : MonoBehaviour, IPuzzleInteractable
    {
        [Tooltip("纠缠外观：白蛇那一半")]
        [SerializeField] private SpriteRenderer m_whiteAppearance;

        [Tooltip("纠缠外观：黑蛇那一半")]
        [SerializeField] private SpriteRenderer m_blackAppearance;

        [Tooltip("两条蛇互相交叉的角度（度），值越大越像缠绕")]
        [SerializeField] private float m_twistAngle = 12f;

        [Tooltip("两半各自偏离中心的距离（米）")]
        [SerializeField] private float m_separation = 0.14f;

        [Tooltip("扭动幅度（度），0 = 静止")]
        [SerializeField] private float m_wiggleAngle = 6f;

        [Tooltip("扭动速度（弧度/秒）")]
        [SerializeField] private float m_wiggleSpeed = 1.6f;

        [Tooltip("玩家靠近到这个半径就自动分开（米）")]
        [SerializeField] private float m_touchRadius = 1.2f;

        [Tooltip("等待演出信号：勾上后靠近/按 E 都不分开，等对话演出（Story Sequence Player 的 Event 步）调用 Split() 才拆")]
        [SerializeField] private bool m_waitForStoryCue = false;

        [Tooltip("分开后要出现的白蛇")]
        [SerializeField] private SnakeActor m_whiteSnake;

        [Tooltip("分开后要出现的黑蛇")]
        [SerializeField] private SnakeActor m_blackSnake;

        [SerializeField] private Puzzle1Director m_director;

        [Tooltip("分开的过程时长（秒）")]
        [SerializeField] private float m_splitDuration = 0.35f;

        private Transform m_player;
        private float m_clock;
        private bool m_hasSplit;

        public bool CanInteract => !m_waitForStoryCue && !m_hasSplit && m_whiteSnake != null && m_blackSnake != null;
        public Transform InteractAnchor => transform;

        private void OnEnable()
        {
            PuzzleInteractables.Register(this);
        }

        private void OnDisable()
        {
            PuzzleInteractables.Unregister(this);
        }

        private void Start()
        {
            // 纠缠期间两条单独的蛇不该在场：这里强制关掉，不依赖编辑器里手动取消激活
            if (m_whiteSnake != null)
            {
                m_whiteSnake.gameObject.SetActive(false);
            }
            if (m_blackSnake != null)
            {
                m_blackSnake.gameObject.SetActive(false);
            }

            // 玩家优先按 PuzzleInteractor 找；没挂时兜底找玩家控制器，
            // 保证「靠近自动分开」不依赖玩家那边先把组件配齐
            var interactor = FindFirstObjectByType<PuzzleInteractor>();
            if (interactor != null)
            {
                m_player = interactor.transform;
            }
            else
            {
                var player = FindFirstObjectByType<DioramaPlayerController>();
                if (player != null)
                {
                    m_player = player.transform;
                }
            }

            // 层级一次性设定（纠缠体不会移动）；扭动动画由 Update 每帧驱动
            int order = DioramaRiver.SortingOrderFor(transform.position);
            if (m_whiteAppearance != null) m_whiteAppearance.sortingOrder = order;
            if (m_blackAppearance != null) m_blackAppearance.sortingOrder = order;

            EnsureTouchCollider();
        }

        private void Update()
        {
            if (!m_hasSplit)
            {
                // 纠缠期间持续扭动，看起来像两条蛇在较劲
                m_clock += Time.deltaTime;
                ApplyEntangledLook();
            }

            if (m_hasSplit || m_player == null) return;

            // 演出接管模式：靠近不自动拆，Split() 由 Story Sequence Player 的 Event 步调用
            if (m_waitForStoryCue) return;

            Vector3 delta = m_player.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= m_touchRadius * m_touchRadius)
            {
                Split();
            }
        }

        public void Interact(Transform interactor)
        {
            // 演出接管期间按 E 也不拆（对话没播完，蛇不该散）
            if (m_waitForStoryCue) return;

            Split();
        }

        // 两半偏移 + 交叉旋转 + 反相位正弦扭动（一张一合），像在缠绕蠕动
        private void ApplyEntangledLook()
        {
            float wiggle = Mathf.Sin(m_clock * m_wiggleSpeed) * m_wiggleAngle;

            if (m_whiteAppearance != null)
            {
                m_whiteAppearance.transform.localPosition = new Vector3(-m_separation, 0f, 0f);
                m_whiteAppearance.transform.localRotation =
                    Quaternion.Euler(0f, 0f, m_twistAngle + wiggle);
            }
            if (m_blackAppearance != null)
            {
                m_blackAppearance.transform.localPosition = new Vector3(m_separation, 0f, 0f);
                m_blackAppearance.transform.localRotation =
                    Quaternion.Euler(0f, 0f, -m_twistAngle - wiggle);
            }
        }

        // 分开两条蛇。public 供对话演出（UnityEvent / Story Sequence Player）调用
        public void Split()
        {
            if (m_hasSplit) return;
            m_hasSplit = true;

            // 纠缠外观淡出，两条蛇从各自摆好的位置淡入
            FadeOut(m_whiteAppearance);
            FadeOut(m_blackAppearance);

            Appear(m_whiteSnake);
            Appear(m_blackSnake);

            if (m_director != null)
            {
                m_director.OnSnakePairSplit();
            }

            DOVirtual.DelayedCall(m_splitDuration, () => gameObject.SetActive(false));
        }

        private void Appear(SnakeActor snake)
        {
            if (snake == null) return;
            snake.gameObject.SetActive(true);
            snake.PlayAppear(m_splitDuration);
        }

        // 给纠缠体一个实体占位：玩家撞得到（静态碰撞体推不动），
        // 不然会整个穿模过去，「触碰」的观感就没了
        private void EnsureTouchCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.55f;
            capsule.height = 1.1f;
            capsule.center = new Vector3(0f, 0.55f, 0f);
        }

        private static void FadeOut(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            renderer.DOFade(0f, 0.25f);
        }
    }
}
