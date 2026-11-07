using UnityEngine;
using UnityEngine.InputSystem;

using Ivan.Puzzle;

namespace Ivan.Diorama
{
    // 组员 ObliqueForest 输入映射的独立复刻版（不依赖 ObliqueForest 组件）：
    //   WASD / 方向键  移动（八向，4 m/s）
    //   Space          跳跃（可越过河流，落在河里则弹回岸边）
    //   J / Enter      挥斧（前方 2m 内的树，3 斧砍倒）
    //   E              谜题交互优先；没有可交互目标时才是喂食（动物）/ 补种（树桩）
    // 相机在 LateUpdate 中跟随玩家，保持组员原版的 (0,17,-20) 正交偏移。
    public class DioramaPlayerController : MonoBehaviour
    {
        private const float k_jumpDuration = 0.85f;
        private const float k_attackDuration = 0.62f;
        private const float k_interactRange = 2f;

        [Tooltip("移动速度（米/秒），组员原版为 4")]
        [SerializeField] private float m_moveSpeed = 4f;

        [Header("活动边界")]
        [Tooltip("勾选后自动从地面（Stationary paper ground）的实际尺寸推导边界；取消勾选则用手填值")]
        [SerializeField] private bool m_deriveBoundsFromGround = true;
        [Tooltip("手填的活动范围半宽/半深（仅在未勾选自动推导时使用）")]
        [SerializeField] private float m_halfWidth = 46f;
        [SerializeField] private float m_halfDepth = 21f;
        [Tooltip("边界相对地面边缘的内缩距离（米），防止走出纸面边缘")]
        [SerializeField] private float m_boundPadding = 0.5f;
        [Tooltip("可行走区域（任意多边形，见 WalkableArea）。指定后优先生效，忽略上面的矩形边界")]
        [SerializeField] private WalkableArea m_walkableArea;

        [Header("河流")]
        [Tooltip("是否把河流视为不可行走。Painted River 组件摆放的河流会自动登记进判定；" +
                 "跳跃可以越过河面")]
        [SerializeField] private bool m_riverBlocksMovement = false;
        [Tooltip("是否同时启用组员原版的全局河带公式（x≈2 一条带）。卷轴场景没有那条河，" +
                 "保持关闭，否则会在卷轴中段形成一道看不见的墙")]
        [SerializeField] private bool m_useLegacyRiverCurve = false;

        [Header("碰撞")]
        [Tooltip("玩家胶囊碰撞体半径（米），控制离障碍物的最小距离")]
        [SerializeField] private float m_capsuleRadius = 0.22f;
        [Tooltip("玩家胶囊碰撞体高度（米）")]
        [SerializeField] private float m_capsuleHeight = 0.6f;

        [Header("引用")]
        [SerializeField] private Transform m_body;      // 玩家字形（Player glyph）
        [SerializeField] private Transform m_axe;       // 斧头（body 的子物体）
        [Tooltip("跟随相机。留空表示由外部（如 Cinemachine）接管，本脚本不再移动相机")]
        [SerializeField] private Camera m_camera;
        [Tooltip("相机是否跟随玩家的纵深（Z）？卷轴玩法只沿 X 卷动，关闭后 Z 锁定：" +
                 "取相机在场景里摆好的初始 Z（进 Play 前把相机移到想要的 Z 即可）")]
        [SerializeField] private bool m_cameraFollowsZ = false;
        [Tooltip("谜题交互（按 E 优先触发谜题交互，没有可交互目标才走喂食/补种）")]
        [SerializeField] private PuzzleInteractor m_puzzleInteractor;

        private SpriteRenderer m_bodyImage;
        private SpriteRenderer m_axeImage;
        private Vector3 m_lastDryPosition;
        private Vector3 m_heading = Vector3.forward;
        private int m_facing = 1;
        private int m_stageFacing = 0;      // 演出朝向锁定：0 = 未锁定，±1 = StageActor 锁定的朝向
        private bool m_stageMovementLocked; // 演出移动锁：Story Sequence Player 演出期间禁止玩家移动
        private float m_cameraBaseZ;        // Z 锁定时相机所在的 Z（取场景里相机的初始 Z）
        private bool m_cameraBaseZCaptured;

        [Header("转身")]
        [Tooltip("左右转向时「压扁→翻面→弹回」的时长（秒）；0 = 立即翻转")]
        [SerializeField] private float m_turnDuration = 0.14f;

        private int m_renderedFacing = 1;   // 实际渲染的朝向（转身动画压扁到 0 时才换面）
        private float m_turnAge = -1f;      // >= 0 表示正在转身
        private float m_bodyBaseScaleX = 1f; // body 的基准缩放，转身压扁在此基础上做

        // 演出锁定优先；未锁定时用移动朝向
        private int EffectiveFacing => m_stageFacing != 0 ? m_stageFacing : m_facing;
        private float m_clock;
        private float m_jumpAge = -1f;
        private float m_attackAge = -1f;
        private bool m_hitDone;
        private Vector3 m_boundCenter = Vector3.zero;   // 边界中心（默认原点，自动推导时跟随地面中心）
        private Rigidbody m_rigidbody;
        private Vector3 m_moveInput;                    // Update 缓存的移动输入，FixedUpdate 消费
        private Vector3 m_lastCheckedPos;               // 排障用：上一物理步位置
        private bool m_driftReported;                   // 排障用：漂移只报告一次
        private bool m_useAutoMoveInput;                // 演出导演接管输入时为 true
        private Vector2 m_autoMoveInput;                // 演出导演注入的移动输入（x=右, y=前）

        // 演出系统（ScrollPlayDirector）注入自动移动：激活期间替代键盘输入。
        // 传 Vector2.zero 表示原地停住（仍处于接管状态）
        public void SetAutoMoveInput(Vector2 input)
        {
            m_autoMoveInput = input;
            m_useAutoMoveInput = true;
        }

        // 交还控制权给键盘
        public void ClearAutoMoveInput()
        {
            m_useAutoMoveInput = false;
            m_autoMoveInput = Vector2.zero;
        }

        // 演出指令（@向左看/@向右看）锁定玩家朝向：
        // 本脚本的视觉刷新每帧按移动方向重写 flipX，直接翻渲染器会被下一帧抹掉，
        // 所以 StageActor 对玩家必须走这个锁定口。传 0 清除锁定恢复自由转向
        public void SetStageFacing(float directionX)
        {
            m_stageFacing = directionX == 0f ? 0 : (directionX > 0f ? 1 : -1);
        }

        // 演出移动锁：Story Sequence Player 演出期间禁止玩家移动。
        // 与导演自动输入不同，手动键盘模式下同样生效（谜题区演出用）
        public void SetStageMovementLock(bool locked)
        {
            m_stageMovementLocked = locked;
        }

        public Vector3 BoundCenter => m_boundCenter;
        public float HalfWidth => m_halfWidth;
        public float HalfDepth => m_halfDepth;
        public float MoveSpeed => m_moveSpeed;

        private void Awake()
        {
            if (m_body != null)
            {
                m_bodyImage = m_body.GetComponent<SpriteRenderer>();
                m_bodyBaseScaleX = m_body.localScale.x;
            }
            if (m_axe != null)
            {
                m_axeImage = m_axe.GetComponent<SpriteRenderer>();
                m_axe.gameObject.SetActive(false);
            }
            m_lastDryPosition = transform.position;
            m_lastCheckedPos = transform.position;

            if (m_puzzleInteractor == null)
            {
                m_puzzleInteractor = GetComponent<PuzzleInteractor>();
            }

            // 物理移动基础：动态刚体 + 胶囊碰撞体。
            // 只做平面移动——锁旋转与竖直位移，跳跃是视觉弧线（作用在 body 字形上）
            m_rigidbody = GetComponent<Rigidbody>();
            if (m_rigidbody == null)
            {
                m_rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation |
                                      RigidbodyConstraints.FreezePositionY;
            m_rigidbody.useGravity = false;
            m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
            }
            capsule.radius = m_capsuleRadius;
            capsule.height = m_capsuleHeight;
            capsule.center = new Vector3(0f, m_capsuleHeight * 0.5f, 0f);

            if (m_deriveBoundsFromGround)
            {
                DeriveBoundsFromGround();
            }
        }

        // 从地面 Renderer 的世界包围盒推导可活动范围。
        // 用户扩展地图（放大/移动 ground）后，边界自动跟着变，无需改代码。
        [ContextMenu("从地面重新计算活动边界")]
        public void DeriveBoundsFromGround()
        {
            var ground = GameObject.Find("Stationary paper ground");
            if (ground == null)
            {
                Debug.LogWarning("[DioramaPlayerController] 场景里找不到 Stationary paper ground，" +
                                 "沿用手填边界。", this);
                return;
            }

            var renderer = ground.GetComponent<Renderer>();
            if (renderer == null) return;

            var bounds = renderer.bounds;
            // 地面中心不一定是原点，按地面实际中心对称
            m_boundCenter = new Vector3(bounds.center.x, 0f, bounds.center.z);
            m_halfWidth = Mathf.Max(0.1f, bounds.extents.x - m_boundPadding);
            m_halfDepth = Mathf.Max(0.1f, bounds.extents.z - m_boundPadding);

            Debug.Log($"[DioramaPlayerController] 活动边界已更新：" +
                      $"中心 {m_boundCenter}，半宽 {m_halfWidth:F1} m，半深 {m_halfDepth:F1} m。", this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_clock += dt;

            var kb = Keyboard.current;
            if (kb == null && !m_useAutoMoveInput) return;

            // —— 移动输入：演出导演的自动输入优先；否则键盘（组员 ObliqueForest.cs Line 129 同款映射）——
            Vector3 move = Vector3.zero;
            if (m_useAutoMoveInput)
            {
                move.x = m_autoMoveInput.x;
                move.z = m_autoMoveInput.y;
            }
            else if (kb != null)
            {
                move.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                       - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                move.z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                       - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
            }
            move = Vector3.ClampMagnitude(move, 1f);

            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame && m_jumpAge < 0)
                {
                    m_jumpAge = 0f;
                    // 跳跃期间关闭所有水体碰撞，让玩家能跨过河面（落地时恢复）
                    DioramaRiver.SetWaterEnabled(false);
                }
                if ((kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && m_attackAge < 0)
                {
                    m_attackAge = 0f;
                    m_hitDone = false;
                }
                if (kb.eKey.wasPressedThisFrame)
                {
                    // 谜题交互优先；没有可交互目标时保持原有的喂食/补种行为
                    if (m_puzzleInteractor == null || !m_puzzleInteractor.TryInteract())
                    {
                        Care();
                    }
                }
            }

            // —— 演出移动锁：序列演出期间键盘与自动输入一并挡掉 ——
            if (m_stageMovementLocked)
            {
                move = Vector3.zero;
            }

            // —— 移动只缓存输入与朝向，实际位移在 FixedUpdate 由刚体完成 ——
            m_moveInput = move;
            if (move.sqrMagnitude > 0f && m_attackAge < 0f)
            {
                m_heading = move;
                if (move.x != 0f)
                {
                    // 真人键盘接管移动时解除演出朝向锁定（导演的自动输入不算）
                    if (!m_useAutoMoveInput && m_stageFacing != 0)
                    {
                        m_stageFacing = 0;
                    }
                    m_facing = move.x > 0f ? 1 : -1;
                }
            }

            if (!InBlockingRiver(transform.position))
            {
                m_lastDryPosition = transform.position;
            }

            // —— 跳跃（组员 Line 133：hop 弧线 + 落河弹回）——
            float hop = 0f;
            if (m_jumpAge >= 0f)
            {
                m_jumpAge += dt;
                hop = Mathf.Sin(Mathf.Clamp01(m_jumpAge / k_jumpDuration) * Mathf.PI) * 1.45f;
                if (m_jumpAge >= k_jumpDuration)
                {
                    m_jumpAge = -1f;
                    DioramaRiver.SetWaterEnabled(true);
                    if (InBlockingRiver(transform.position))
                    {
                        // 落河弹回：刚体与变换要同步写，否则物理下一步会把玩家拉回去
                        m_rigidbody.position = m_lastDryPosition;
                        transform.position = m_lastDryPosition;
                    }
                }
            }

            // —— 身体浮动与朝向（组员 Line 135 简化）——
            if (m_body != null)
            {
                m_body.localPosition = new Vector3(0f, hop + 0.015f * Mathf.Sin(m_clock * 3.4f), 0f);
                if (m_bodyImage != null)
                {
                    // 演出朝向锁定（@向左看/@向右看）优先于移动朝向；转向走压扁翻面过渡
                    UpdateTurnVisual();
                }
            }

            // —— 层级（组员 Line 142：每帧按脚部 z 坐标重算，axe 比身体高 1）——
            if (m_bodyImage != null)
            {
                m_bodyImage.sortingOrder = DioramaRiver.SortingOrderFor(transform.position);
                if (m_axeImage != null)
                {
                    m_axeImage.sortingOrder = m_bodyImage.sortingOrder + 1;
                }
            }

            // —— 挥斧（组员 Line 137：前摇 0.16s、挥下 0.3s、0.25s 判定、0.62s 收斧）——
            if (m_attackAge >= 0f)
            {
                m_attackAge += dt;
                if (m_axe != null)
                {
                    m_axe.gameObject.SetActive(true);
                    float swing = m_attackAge < 0.16f
                        ? Mathf.Lerp(20f, 85f, Mathf.SmoothStep(0f, 1f, m_attackAge / 0.16f))
                        : Mathf.Lerp(85f, -110f, Mathf.SmoothStep(0f, 1f, (m_attackAge - 0.16f) / 0.3f));
                    m_axe.localRotation = Quaternion.Euler(0f, 0f, EffectiveFacing * swing);
                    var axeScale = m_axe.localScale;
                    axeScale.x = Mathf.Abs(axeScale.x) * EffectiveFacing;
                    m_axe.localScale = axeScale;
                }
                if (m_attackAge > 0.25f && !m_hitDone)
                {
                    m_hitDone = true;
                    StrikeTrees();
                }
                if (m_attackAge > k_attackDuration)
                {
                    m_attackAge = -1f;
                    if (m_axe != null)
                    {
                        m_axe.gameObject.SetActive(false);
                    }
                }
            }
        }

        // 速度驱动移动：树、门、河面等阻挡全部交给物理碰撞，天然支持贴墙滑动；
        // 以后加"解锁的门"只需开关门自己的 Collider，这里不用改
        private void FixedUpdate()
        {
            if (m_rigidbody == null) return;

            Vector3 velocity = m_moveInput * m_moveSpeed;

            // 可行走区域/矩形边界属于逻辑限制，物理引擎不知道，这里做第二道闸：
            // 下一步越界则尝试拆轴滑动（贴边不卡住），两轴都不行才停住
            Vector3 step = velocity * Time.fixedDeltaTime;
            if (step.sqrMagnitude > 0f && !IsFree(m_rigidbody.position + step))
            {
                Vector3 stepX = new Vector3(step.x, 0f, 0f);
                Vector3 stepZ = new Vector3(0f, 0f, step.z);
                if (IsFree(m_rigidbody.position + stepX))
                {
                    step = stepX;
                }
                else if (IsFree(m_rigidbody.position + stepZ))
                {
                    step = stepZ;
                }
                else
                {
                    step = Vector3.zero;
                }
                velocity = step / Time.fixedDeltaTime;
            }

            m_rigidbody.linearVelocity = velocity;

            // 排障：无输入却被推动 = 多半和某个碰撞体持续重叠，物理引擎在每步把人往外挤。
            // 速度是每步直接赋值的，代码自身不会产生惯性，所以只检测物理位移
            Vector3 drift = m_rigidbody.position - m_lastCheckedPos;
            m_lastCheckedPos = m_rigidbody.position;
            if (!m_driftReported && m_moveInput.sqrMagnitude < 0.001f && drift.magnitude > 0.02f)
            {
                m_driftReported = true;
                ReportPersistentOverlap();
            }
        }

        // 无输入漂移时调用：把和玩家胶囊重叠的碰撞体全部列出来，方便定位肇事者
        private void ReportPersistentOverlap()
        {
            Vector3 bottom = transform.position + new Vector3(0f, m_capsuleRadius + 0.01f, 0f);
            Vector3 top = transform.position + new Vector3(0f, m_capsuleHeight - m_capsuleRadius, 0f);
            var hits = Physics.OverlapCapsule(bottom, top, m_capsuleRadius);
            int reported = 0;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                reported++;
                Debug.LogWarning($"[Diorama] 玩家与「{GetPath(hit.transform)}」的碰撞体持续重叠，" +
                                 "物理引擎正在把玩家往外推。请检查该物体的碰撞体尺寸与位置。", hit);
            }
            if (reported == 0)
            {
                Debug.LogWarning("[Diorama] 玩家无输入却在漂移，但没检测到碰撞体重叠——" +
                                 "请检查是否有其他脚本在移动玩家。", this);
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + " / " + path;
            }
            return path;
        }

        private void LateUpdate()
        {
            // 相机跟随：保持组员原版的正交偏移 (0,17,-20)
            if (m_camera == null) return;

            // 首次拿到相机时记下它在场景里的 Z，作为 Z 锁定值（编辑态想调构图就移动相机）
            if (!m_cameraBaseZCaptured)
            {
                m_cameraBaseZ = m_camera.transform.position.z;
                m_cameraBaseZCaptured = true;
            }

            Vector3 target = transform.position + new Vector3(0f, 17f, -20f);
            if (!m_cameraFollowsZ)
            {
                target.z = m_cameraBaseZ;   // 只沿 X 跟随，纵深锁死
            }
            m_camera.transform.position = Vector3.Lerp(m_camera.transform.position, target, Time.deltaTime * 4f);
        }

        // 河流判定与层级公式统一在 DioramaRiver 静态类（玩家/动物共用）。
        // 组件河流（PaintedRiver）始终参与；组员的全局公式按开关启用
        private bool InRiver(Vector3 p)
        {
            if (DioramaRiver.InPaintedRiver(p)) return true;
            return m_useLegacyRiverCurve && DioramaRiver.InLegacyCurve(p);
        }

        // 左右转身过渡：压扁到 0 的瞬间换面，再弹回原宽。
        // 扁平纸片小人直接 flipX 会很生硬；键盘移动和演出 @向左看/@向右看 共用这条路径
        private void UpdateTurnVisual()
        {
            int targetFacing = EffectiveFacing;
            float half = m_turnDuration * 0.5f;

            // 朝向变了且没在转身：开始一段转身（时长为 0 则直接翻）
            if (targetFacing != m_renderedFacing && m_turnAge < 0f)
            {
                if (m_turnDuration <= 0f)
                {
                    m_renderedFacing = targetFacing;
                }
                else
                {
                    m_turnAge = 0f;
                }
            }

            float scaleX = 1f;
            if (m_turnAge >= 0f)
            {
                // 前半程方向又变回来了：取消本次转身
                if (targetFacing == m_renderedFacing && m_turnAge < half)
                {
                    m_turnAge = -1f;
                }
                else
                {
                    m_turnAge += Time.deltaTime;
                    if (m_turnAge < half)
                    {
                        scaleX = Mathf.Lerp(1f, 0f, m_turnAge / half);
                    }
                    else
                    {
                        // 中点翻面：读取当下的目标朝向，转身途中反悔也能自然衔接
                        m_renderedFacing = targetFacing;
                        if (m_turnAge < m_turnDuration)
                        {
                            scaleX = Mathf.Lerp(0f, 1f, (m_turnAge - half) / half);
                        }
                        else
                        {
                            m_turnAge = -1f;
                        }
                    }
                }
            }

            Vector3 scale = m_body.localScale;
            scale.x = m_bodyBaseScaleX * scaleX;
            m_body.localScale = scale;
            m_bodyImage.flipX = m_renderedFacing < 0;
        }

        // 河带只在开启 m_riverBlocksMovement 时视为不可走。
        // 卷轴场景没有真的河，但 InRiver 是全局公式，不关掉会在 x≈2 处形成一道看不见的墙
        private bool InBlockingRiver(Vector3 p)
        {
            return m_riverBlocksMovement && InRiver(p);
        }

        private bool IsFree(Vector3 p)
        {
            // 指定了多边形可行走区域则以它为准；否则退回按地面推导的矩形边界
            if (m_walkableArea != null)
            {
                if (!m_walkableArea.Contains(p)) return false;
            }
            else
            {
                float dx = Mathf.Abs(p.x - m_boundCenter.x);
                float dz = Mathf.Abs(p.z - m_boundCenter.z);
                if (dx > m_halfWidth || dz > m_halfDepth) return false;
            }
            if (m_jumpAge < 0f && InBlockingRiver(p)) return false; // 河里不能走，除非正在跳
            return true;
        }

        // 挥斧命中：前方 2m 内、朝向夹角内的所有活树（组员 StrikeTrees 的简化）
        private void StrikeTrees()
        {
            var trees = FindObjectsByType<DioramaTree>(FindObjectsSortMode.None);
            foreach (var tree in trees)
            {
                if (tree.IsStump) continue;

                Vector3 d = tree.transform.position - transform.position;
                if (d.magnitude < k_interactRange && Vector3.Dot(d.normalized, m_heading) > 0.15f)
                {
                    tree.ApplyHit(m_heading);
                }
            }
        }

        // E 交互：优先喂食 2m 内最近的动物，否则补种 2m 内的树桩
        private void Care()
        {
            var animals = FindObjectsByType<DioramaAnimal>(FindObjectsSortMode.None);
            DioramaAnimal bestAnimal = null;
            float bestDist = k_interactRange;
            foreach (var animal in animals)
            {
                float d = Vector3.Distance(animal.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestAnimal = animal;
                }
            }
            if (bestAnimal != null)
            {
                bestAnimal.Feed();
                return;
            }

            var trees = FindObjectsByType<DioramaTree>(FindObjectsSortMode.None);
            foreach (var tree in trees)
            {
                if (!tree.IsStump) continue;
                if (Vector3.Distance(tree.transform.position, transform.position) < k_interactRange)
                {
                    tree.Replant();
                    return;
                }
            }
        }
    }
}
