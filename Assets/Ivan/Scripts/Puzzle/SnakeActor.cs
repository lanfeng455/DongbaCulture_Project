using DG.Tweening;
using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    public enum SnakeSide
    {
        White,
        Black,
    }

    public enum SnakeState
    {
        Hidden,     // 还没分开
        Free,       // 刚分开，还没开始判视线：可自由推动（给玩家推开白蛇的余地）
        Staring,    // 可见：追着对方靠近（能推，松手会贴回来）
        Blinded,    // 不可见：不追，可被玩家推动
        Captured,   // 已入洞等待。视线恢复时会自己爬出来——洞不是锁
        Exiting,    // 正从洞里窜出来（弹出动画中，物理挂起）
    }

    // 一条蛇。状态由 Puzzle1Director 每帧根据视线动态切换，这里只管表现和位移。
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Snake Actor")]
    public class SnakeActor : MonoBehaviour
    {
        private const float k_chestHeight = 0.5f;

        [Tooltip("这条蛇是哪一条（决定它能进哪个洞穴）")]
        [SerializeField] private SnakeSide m_side = SnakeSide.White;

        [Tooltip("蛇的精灵（留空则取子物体里的第一个 SpriteRenderer）")]
        [SerializeField] private SpriteRenderer m_image;

        [Tooltip("头顶的「可见 / 不可见」文字")]
        [SerializeField] private WordLabel m_wordLabel;

        [Tooltip("视线发射点。挂组件时自动生成，请拖到蛇眼位置（只取 XZ，Y 不参与判定）")]
        [SerializeField] private Transform m_sightAnchor;

        [Tooltip("对峙时两条蛇保持的最小距离（米）")]
        [SerializeField] private float m_standoffDistance = 2.2f;

        [Tooltip("对峙时相互靠近的速度（米/秒）")]
        [SerializeField] private float m_approachSpeed = 1.1f;

        [Tooltip("被推动时的阻力，越大越沉、越不会滑远")]
        [SerializeField] private float m_pushDamping = 8f;

        [Tooltip("被推动时的质量，越大玩家越难推")]
        [SerializeField] private float m_pushMass = 1f;

        [Tooltip("入洞滑行的时长（秒）")]
        [SerializeField] private float m_captureDuration = 0.45f;

        [Tooltip("从洞里窜出来时弹出的距离（米），需大于洞的触发半径，防止出去瞬间又入洞")]
        [SerializeField] private float m_exitPopDistance = 1.8f;

        [Tooltip("从洞里窜出来的时长（秒）")]
        [SerializeField] private float m_exitDuration = 0.25f;

        private Rigidbody m_body;
        private Collider m_collider;
        private CaveSlot m_cave;
        private SnakeActor m_opponent;
        private SnakeState m_state = SnakeState.Hidden;

        public SnakeSide Side => m_side;
        public SnakeState State => m_state;
        public bool IsCaptured => m_state == SnakeState.Captured;

        // 视线端点：优先用眼部锚点，没配过就用蛇根（判定时 Y 会被归零）
        public Vector3 SightPosition => (m_sightAnchor != null ? m_sightAnchor : transform).position;

        // 挂组件的瞬间（编辑器）就把刚体配好：关重力、锁 Y 与旋转。
        // 否则默认重力开着，一进 Play 蛇就掉进虚空
        private void Reset()
        {
            m_body = PrepareBody(gameObject);
            EnsureSightAnchor();
        }

        private void Awake()
        {
            if (m_image == null)
            {
                m_image = GetComponentInChildren<SpriteRenderer>();
            }

            m_body = PrepareBody(gameObject);

            // 未被导演接手前（Hidden）：锁成 kinematic，既不受物理影响也不会被玩家推飞。
            // 同时预设质量与阻尼，保证之后 EnterFree 打开 dynamic 时一推就是「沉稳」手感
            m_body.isKinematic = true;
            m_body.mass = m_pushMass;
            m_body.linearDamping = m_pushDamping;

            var obstacle = GetComponent<DioramaObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<DioramaObstacle>();
            }
            obstacle.EnsureCollider();
            m_collider = obstacle.Collider;

            if (m_wordLabel != null)
            {
                m_wordLabel.Hide();
            }

            EnsureSightAnchor();
        }

        // 眼部锚点：一个空子物体，编辑器里拖到蛇眼位置即可
        private void EnsureSightAnchor()
        {
            if (m_sightAnchor != null) return;

            var anchor = new GameObject("Sight Anchor").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = new Vector3(0f, 0.45f, 0f);
            m_sightAnchor = anchor;
        }

        // 本场景移动是纯平面（XZ）的：无重力、锁竖直位移与旋转，其余交给物理
        private static Rigidbody PrepareBody(GameObject host)
        {
            var body = host.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = host.AddComponent<Rigidbody>();
            }
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation |
                               RigidbodyConstraints.FreezePositionY;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            return body;
        }

        private void Update()
        {
            if (m_state != SnakeState.Staring) return;
            ApproachOpponent();
        }

        private void LateUpdate()
        {
            // 每条蛇按自己的 z 坐标重算层级，和场景里其他精灵一致
            if (m_image != null)
            {
                m_image.sortingOrder = DioramaRiver.SortingOrderFor(transform.position);
            }
        }

        public void BindOpponent(SnakeActor opponent)
        {
            m_opponent = opponent;
        }

        // 分开瞬间的淡入：从透明渐变到不透明，避免凭空闪现
        public void PlayAppear(float duration)
        {
            if (m_image == null) return;

            Color color = m_image.color;
            color.a = 0f;
            m_image.color = color;
            FadeImage(1f, duration);
        }


        // Tween only a scalar: precompiled DOTween Color plugins can reference
        // Unity Color operators unavailable in another Unity 6 runtime.
        private Tween m_imageFade;
        private Tween FadeImage(float alpha, float duration)
        {
            m_imageFade?.Kill();
            m_imageFade = DOTween.To(() => m_image.color.a, value =>
            {
                Color tint = m_image.color;
                tint.a = value;
                m_image.color = tint;
            }, alpha, duration).SetTarget(m_image);
            return m_imageFade;
        }

        // 刚分开：可自由推动，头顶先不出文字
        public void EnterFree()
        {
            if (m_state == SnakeState.Captured || m_state == SnakeState.Free) return;

            m_state = SnakeState.Free;
            m_body.isKinematic = false;
            m_body.mass = m_pushMass;
            m_body.linearDamping = m_pushDamping;
            SetColliderEnabled(true);

            if (m_wordLabel != null)
            {
                m_wordLabel.Hide();
            }
        }

        // 视线通畅：可见，锁死对峙并追着靠近（推不动）。
        // 必须用石头挡线把它变成「不可见」才能推动——这是谜题的核心闸门，
        // 也堵死「顶着对峙硬推」的取巧路径
        public void EnterStaring()
        {
            if (m_state == SnakeState.Captured || m_state == SnakeState.Exiting ||
                m_state == SnakeState.Staring) return;

            m_state = SnakeState.Staring;
            if (m_side == SnakeSide.White)
            {
                AudioManager.Instance.PlaySe("Assets/Ivan/Audio/SE/SnakeHiss");
            }
            m_body.linearVelocity = Vector3.zero;
            m_body.isKinematic = true;
            SetColliderEnabled(true);

            if (m_wordLabel != null)
            {
                m_wordLabel.ShowVisible();
            }
        }

        // 视线被挡住：不可见，停在原地但可以被玩家推走
        public void EnterBlinded()
        {
            if (m_state == SnakeState.Captured || m_state == SnakeState.Exiting ||
                m_state == SnakeState.Blinded) return;

            m_state = SnakeState.Blinded;
            if (m_side == SnakeSide.White)
            {
                AudioManager.Instance.PlaySe("Assets/Ivan/Audio/SE/buttonHover");
            }
            m_body.isKinematic = false;
            m_body.mass = m_pushMass;
            m_body.linearDamping = m_pushDamping;
            SetColliderEnabled(true);

            if (m_wordLabel != null)
            {
                m_wordLabel.ShowInvisible();
            }
        }

        // 被推进洞穴：滑到洞里停住等同伴。只有它「看不见」对方时才安分待着——
        // 视线一恢复，导演会调 ExitCave 让它窜出来，洞不是锁
        public void Capture(CaveSlot cave)
        {
            if (m_state == SnakeState.Captured || m_state == SnakeState.Exiting) return;

            m_state = SnakeState.Captured;
            m_cave = cave;
            m_body.linearVelocity = Vector3.zero;
            m_body.isKinematic = true;
            SetColliderEnabled(false);

            if (m_wordLabel != null)
            {
                m_wordLabel.Hide();
            }

            Vector3 target = cave.transform.position;
            target.y = transform.position.y;
            transform.DOKill();
            transform.DOMove(target, m_captureDuration).SetEase(Ease.InQuad);
        }

        // 从洞里窜出来（视线恢复时由导演调用）：朝对方方向弹出洞口，
        // 弹出距离大于洞的触发半径，避免碰撞体重开的瞬间又把自己送回洞里
        public void ExitCave(SnakeActor target)
        {
            if (m_state != SnakeState.Captured) return;

            m_state = SnakeState.Exiting;
            m_body.linearVelocity = Vector3.zero;

            // 通知洞放行：碰撞体重开时可能仍在触发区内，别再把它吸回来
            if (m_cave != null)
            {
                m_cave.BeginRelease(this);
                m_cave = null;
            }

            if (m_wordLabel != null)
            {
                m_wordLabel.ShowVisible();
            }

            Vector3 direction = Vector3.forward;
            if (target != null)
            {
                direction = target.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    direction.Normalize();
                }
            }

            Vector3 exit = transform.position + direction * m_exitPopDistance;
            exit.y = transform.position.y;
            transform.DOMove(exit, m_exitDuration).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (this == null) return;

                m_body.isKinematic = false;
                m_body.mass = m_pushMass;
                m_body.linearDamping = m_pushDamping;
                SetColliderEnabled(true);
                // 先回到可推的 Free，导演下一帧会按当前视线接管成可见/不可见
                m_state = SnakeState.Free;
            });
        }

        // 两只都入洞后由导演调用：一起淡出退场
        public void Depart()
        {
            if (m_image != null)
            {
                FadeImage(0f, m_captureDuration)
                    .OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                DOVirtual.DelayedCall(m_captureDuration, () => gameObject.SetActive(false));
            }
        }

        private void SetColliderEnabled(bool enabled)
        {
            if (m_collider != null)
            {
                m_collider.enabled = enabled;
            }
        }

        // 追赶：Staring 是 kinematic（推不动），用 MovePosition 位移，
        // 贴到对峙距离停住。同伴已入洞时照样追过去（想去会合）
        private void ApproachOpponent()
        {
            if (m_opponent == null) return;

            Vector3 delta = m_opponent.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= m_standoffDistance) return;

            Vector3 direction = delta / distance;
            float step = Mathf.Min(
                m_approachSpeed * Time.deltaTime,
                distance - m_standoffDistance);

            m_body.MovePosition(m_body.position + direction * step);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = m_side == SnakeSide.White
                ? new Color(0.9f, 0.9f, 1f, 0.6f)
                : new Color(0.15f, 0.15f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * k_chestHeight, m_standoffDistance * 0.5f);
        }
    }
}
