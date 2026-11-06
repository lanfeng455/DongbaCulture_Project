using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// 动态 + 可移动实体：玩家。与其他实体一样是“木棍 + 字形 SpriteRenderer”的纸片结构，
// 共用 SceneEntity 的描边、Billboard 与阴影逻辑；木棍、贴图、位置、大小全部由预制体控制，
// 本组件只负责核心逻辑。
//
// 状态机（对齐 TreeEntity 的写法）：
//   Normal           静置或移动，显示当前外观（索引 0 视为基本人形）
//   冲撞攻击          空手时左键触发：朝一个方向加速冲出，撞完弹回原位后回到 Normal
//   外观变化          字形转体一周，转到一半（180°）时切换外观，转完回到 Normal
//   Die              外观变化的一种，同样有转体，落地后锁死输入
//   转场等待          转台旋转期间锁住输入，转完自动朝新房间中心走一小步再回到 Normal
//
// 移动用方向键八向、镜头相对，并钳制在方形范围内；冲撞同样不会冲出边界。
[ExecuteAlways]
[RequireComponent(typeof(EntityStateMachine))]
public class PlayerEntity : SceneEntity
{
    // 一种外观。后续补充外观细节时往这个结构体里加字段即可（如音效、特效、碰撞体尺寸）
    [System.Serializable]
    public struct AppearanceVariant
    {
        [Tooltip("外观名称，仅用于在 Inspector 中辨识")]
        public string label;

        [Tooltip("该外观使用的字形贴图；留空表示沿用当前贴图")]
        public Sprite sprite;
    }

    [Header("移动")]
    [Tooltip("地面移动速度（米/秒）")]
    [SerializeField] private float m_moveSpeedInMetersPerSecond = 5f;

    [Header("移动边界（运行时由 RoomTurnManager 自动注入）")]
    [Tooltip("可活动区域的边界 GameObject；每个房间预制体里应该有一个叫 MoveBounds 的子物体挂着这个房间专属的边界 Collider 组。未指定时回退到下方的方形 AABB")]
    [SerializeField] private GameObject m_boundsZone;

    [Tooltip("可活动区域的最小角（x = 世界 X，y = 世界 Z）；Bounds Zone 未指定时回退使用")]
    [SerializeField] private Vector2 m_boundsMin = new Vector2(-8f, -8f);

    [Tooltip("可活动区域的最大角（x = 世界 X，y = 世界 Z）；Bounds Zone 未指定时回退使用")]
    [SerializeField] private Vector2 m_boundsMax = new Vector2(8f, 8f);

    private Collider[] m_boundsColliders;

    [Header("冲撞攻击")]
    [Tooltip("冲撞前进的距离（米）")]
    [SerializeField] private float m_chargeDistanceInMeters = 1.6f;

    [Tooltip("加速冲出的时长（秒）")]
    [SerializeField] private float m_chargeOutDurationInSeconds = 0.18f;

    [Tooltip("弹回原位的时长（秒）")]
    [SerializeField] private float m_chargeBackDurationInSeconds = 0.22f;

    [Header("外观变化")]
    [Tooltip("外观列表，可自由扩展；索引 0 视为基本人形")]
    [SerializeField] private List<AppearanceVariant> m_appearances = new List<AppearanceVariant>();

    [Tooltip("转体一周所用时长（秒）")]
    [SerializeField] private float m_spinDurationInSeconds = 0.6f;

    [Tooltip("死亡后切换到哪个外观（外观列表索引）")]
    [SerializeField] private int m_deathAppearanceIndex = 0;

    [Header("持有物槽位")]
    [Tooltip("手中持有物的挂点（玩家预制体里的子物体）；留空时按名字 HeldSlot 查找")]
    [SerializeField] private Transform m_heldSlot;

    [Header("撞击检测")]
    [Tooltip("玩家子物体上的撞击检测（该子物体需 Collider(Is Trigger) + 刚体）；留空时按名字 ChargeHitbox 查找")]
    [SerializeField] private ChargeHitbox m_chargeHitbox;

    [Header("拾取范围")]
    [Tooltip("玩家子物体上的拾取范围（该子物体需 Collider(Is Trigger) + 刚体）；留空时按名字 PickupRange 查找")]
    [SerializeField] private PickupCollector m_pickupCollector;

    private const string k_heldSlotName = "HeldSlot";
    private const string k_chargeHitboxName = "ChargeHitbox";
    private const string k_pickupRangeName = "PickupRange";

    private EntityStateMachine m_states;

    private NormalState m_normalState;
    private ChargeAttackState m_chargeState;
    private AppearanceChangeState m_appearanceState;
    private DieState m_dieState;
    private RoomTurnState m_roomTurnState;

    private Vector3 m_lastMoveDirection;
    private int m_currentAppearanceIndex;
    private int m_pendingAppearanceIndex;
    private bool m_isDead;

    // 供拾取/装备等后续系统查询
    public bool IsHandEmpty => m_heldSlot == null || m_heldSlot.childCount == 0;
    public Transform HeldSlot => m_heldSlot;
    public bool IsDead => m_isDead;
    public int CurrentAppearanceIndex => m_currentAppearanceIndex;

    // 融合发射期间物品离手飞行，槽位短暂为空；此时必须屏蔽拾取，
    // 否则玩家会被判定成空手，捡起别的东西，飞回来的物品就会和它叠在槽位里
    public bool IsPickupBlocked { get; set; }

    // 手上那件掉落物（瞄准/融合系统用）；空手或槽位里不是掉落物时返回 null
    public DroppedItem HeldItem
    {
        get
        {
            if (m_heldSlot == null || m_heldSlot.childCount == 0)
            {
                return null;
            }

            return m_heldSlot.GetChild(0).GetComponent<DroppedItem>();
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // 确保 Player 上有 PlayerInventory——新 Hotbar 系统需要它
        // RequireComponent 只在 AddComponent 时触发，场景里已有的 Player 不会自动加
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<PlayerInventory>();
            Debug.Log("[PlayerEntity] 自动添加了 PlayerInventory 组件", this);
        }

        // 同时确保有 HotbarUI——负责把 Hotbar 状态渲染到 UI Toolkit 上
        // 场景模板里没有预挂，这里自动补；RequireComponent(PlayerInventory) 保证依赖链正确
        if (GetComponent<HotbarUI>() == null)
        {
            gameObject.AddComponent<HotbarUI>();
            Debug.Log("[PlayerEntity] 自动添加了 HotbarUI 组件", this);
        }

        // 订阅 InventoryChanged：Hotbar 选中变化时，把选中的物品移到 HeldSlot
        inventory.InventoryChanged += HandleInventoryChanged;

        m_states = GetComponent<EntityStateMachine>();

        m_normalState = new NormalState(this);
        m_chargeState = new ChargeAttackState(this);
        m_appearanceState = new AppearanceChangeState(this);
        m_dieState = new DieState(this);
        m_roomTurnState = new RoomTurnState(this);

        m_states.Register(m_normalState);
        m_states.Register(m_chargeState);
        m_states.Register(m_appearanceState);
        m_states.Register(m_dieState);
        m_states.Register(m_roomTurnState);

        ResolveHeldSlot();
        ResolveAttackComponents();
        ResolveBoundsZone();

        if (m_chargeHitbox != null)
        {
            m_chargeHitbox.HitLanded += HandleChargeHitLanded;
        }

        if (m_pickupCollector != null)
        {
            m_pickupCollector.ItemInRange += HandleItemInRange;
            Debug.Log($"[PlayerEntity] 已订阅 PickupCollector.ItemInRange", this);
        }
        else
        {
            Debug.LogWarning($"[PlayerEntity] m_pickupCollector 为 null，无法订阅！", this);
        }

        // 初始进入 Normal；Normal 的 Enter 会应用当前外观，所以这里先把索引归零
        m_currentAppearanceIndex = 0;
        m_states.Switch(m_normalState);
    }

    protected override void Start()
    {
        base.Start();

        // 主镜头由 SceneEntity 缓存并共用（基类的 m_cameraTransform），这里只做缺失提示
        if (m_cameraTransform == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 未找到主镜头，移动将退化为世界坐标对齐。", this);
        }
    }

    private void OnDestroy()
    {
        if (m_chargeHitbox != null)
        {
            m_chargeHitbox.HitLanded -= HandleChargeHitLanded;
        }

        if (m_pickupCollector != null)
        {
            m_pickupCollector.ItemInRange -= HandleItemInRange;
        }

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.InventoryChanged -= HandleInventoryChanged;
        }
    }

    // Hotbar 变化：把选中的物品挂到 HeldSlot，其余格子里的物品全部隐藏
    //
    // 关键：必须遍历**所有**格子，而不只是选中格。DroppedItem 是 Hotbar 里的真实实例
    // （不是克隆品），进背包时只做了 CollectSilently（关碰撞 + 停自转），sprite 仍在原地渲染。
    // 若只处理选中格，新捡到的、落在别的格子里的物品就会永远留在地上 —— 看起来像"捡了还有残留"。
    private void HandleInventoryChanged()
    {
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null || m_heldSlot == null) return;

        // 1) 先把手上已有的全部卸下并隐藏（可能是 ClearSelected / 放置成功后被移除的实例）
        HideHeldSlotChildren();

        // 2) 不在选中格的物品一律隐藏
        DroppedItem selected = inventory.SelectedItem;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            DroppedItem item = inventory[i];
            if (item == null || item == selected)
            {
                continue;
            }

            item.gameObject.SetActive(false);
        }

        // 3) 选中的挂到手上
        if (selected != null)
        {
            selected.gameObject.SetActive(true);
            selected.transform.SetParent(m_heldSlot, true);
        }
    }

    // 把 HeldSlot 下挂着的物品全部摘下并隐藏；逆序遍历以避免移除子物体时索引错位
    private void HideHeldSlotChildren()
    {
        for (int i = m_heldSlot.childCount - 1; i >= 0; i--)
        {
            Transform child = m_heldSlot.GetChild(i);
            child.SetParent(null, true);
            child.gameObject.SetActive(false);
        }
    }

    // 每帧让手持物品跟随 HeldSlot 位置 + FaceCamera + depth push
    private void LateUpdate()
    {
        if (m_heldSlot == null || m_cameraTransform == null) return;
        if (m_heldSlot.childCount == 0) return;

        DroppedItem heldItem = m_heldSlot.GetChild(0).GetComponent<DroppedItem>();
        if (heldItem == null) return;

        heldItem.transform.position = m_heldSlot.position;
        heldItem.FaceCamera();
    }

    private void Update()
    {
        if (!Application.isPlaying || m_isDead)
        {
            return;
        }

        // 左键发起一次冲撞；手持物品时同样可以冲撞
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            StartCharge();
        }
    }

    #region 公开接口
    // 冲撞攻击：Normal 状态下随时可用（手中有持有物也不影响）
    public void StartCharge()
    {
        if (m_isDead || m_states.Current != m_normalState)
        {
            return;
        }

        m_states.Switch(m_chargeState);
    }

    // 外观变化：转体一周并切换到目标外观，结束后回到 Normal
    public void ChangeAppearance(int appearanceIndex)
    {
        if (m_isDead || m_states.Current != m_normalState)
        {
            return;
        }

        if (m_appearances == null || appearanceIndex < 0 || appearanceIndex >= m_appearances.Count)
        {
            return;
        }

        m_pendingAppearanceIndex = appearanceIndex;
        m_states.Switch(m_appearanceState);
    }

    // 死亡：转体并切换到死亡外观，之后不再接受任何输入
    public void Die()
    {
        if (m_isDead)
        {
            return;
        }

        m_states.Switch(m_dieState);
    }

    // 转台开始旋转：锁住输入，等房间转进台口
    public void BeginRoomTurn()
    {
        if (m_isDead)
        {
            return;
        }

        m_states.Switch(m_roomTurnState);
    }

    // 转台停稳：朝新房间中心走一小步，走完自动回到 Normal
    public void CompleteRoomTurn(Vector3 worldTargetPosition, float durationInSeconds)
    {
        m_roomTurnState.WalkIntoRoom(worldTargetPosition, durationInSeconds);
    }

    // 转场被取消（加载失败等）：立刻恢复操作
    public void CancelRoomTurn()
    {
        if (m_isDead)
        {
            return;
        }

        m_states.Switch(m_normalState);
    }
    #endregion

    private void ResolveHeldSlot()
    {
        if (m_heldSlot != null)
        {
            return;
        }

        Transform slot = transform.Find(k_heldSlotName);
        if (slot != null)
        {
            m_heldSlot = slot;
            return;
        }

        Debug.LogWarning($"[{GetType().Name}] 未找到持有物槽位子物体 '{k_heldSlotName}'；装备与拾取系统接入后需补上。", this);
    }

    private void ResolveAttackComponents()
    {
        if (m_chargeHitbox == null)
        {
            m_chargeHitbox = FindChildComponent<ChargeHitbox>(k_chargeHitboxName);
            if (m_chargeHitbox == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 未找到撞击检测 '{k_chargeHitboxName}'；冲撞不会对目标造成伤害。", this);
            }
        }

        if (m_pickupCollector != null)
        {
            return;
        }

        m_pickupCollector = FindChildComponent<PickupCollector>(k_pickupRangeName);
        if (m_pickupCollector == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 未找到拾取范围 '{k_pickupRangeName}'；无法拾取掉落物。", this);
        }
    }

    // 从 Bounds Zone GameObject 收集所有 Collider（自身 + 子物体）
    private void ResolveBoundsZone()
    {
        if (m_boundsZone == null)
        {
            m_boundsColliders = null;
            return;
        }

        m_boundsColliders = m_boundsZone.GetComponentsInChildren<Collider>();

        if (m_boundsColliders.Length == 0)
        {
            Debug.LogWarning($"[{GetType().Name}] Bounds Zone '{m_boundsZone.name}' 上没有 Collider；边界判定将回退到方形 AABB。", this);
            m_boundsColliders = null;
        }
    }

    // 由 RoomTurnManager 在转场时调用，运行时切换当前房间的可活动边界。
    // 约定：每个房间预制体里有一个叫 MoveBounds 的子物体，上面挂着这个房间的 Collider 组。
    public void SetBoundsZone(GameObject zone)
    {
        m_boundsZone = zone;
        ResolveBoundsZone();
    }

    // 运行时手动刷新边界 Collider 缓存——适用于场景里真的新增/删除了 Collider 的情况
    // （比如桥梁搭起来、巨石被推开等）。
    // 注意：只是禁用/启用 Collider（collider.enabled = false/true）不需要调这个，
    // IsPointInsideCollider 和 ClampToColliderBounds 已经每帧检查 enabled 状态。
    public void RefreshBoundsZone()
    {
        ResolveBoundsZone();
    }

    private bool HasBoundsColliders => m_boundsColliders != null && m_boundsColliders.Length > 0;

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            return null;
        }

        T component = child.GetComponent<T>();
        if (component == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 子物体 '{childName}' 上没有 {typeof(T).Name}。", this);
        }

        return component;
    }

    private void ArmChargeHitbox()
    {
        if (m_chargeHitbox != null)
        {
            m_chargeHitbox.Arm();
        }
    }

    private void DisarmChargeHitbox()
    {
        if (m_chargeHitbox != null)
        {
            m_chargeHitbox.Disarm();
        }
    }

    // 撞到第一个目标：立刻弹回，不再一路往前撞
    private void HandleChargeHitLanded(IHittable target, Vector3 hitPoint)
    {
        m_chargeState.HandleHitLanded();
    }

    // 拾取范围内出现掉落物：放进 Hotbar 的第一个空格；
    // Hotbar 满了就让物品留在地上（不收集、不禁用）
    private void HandleItemInRange(DroppedItem item)
    {
        if (m_isDead || IsPickupBlocked || item == null || item.IsCollected)
        {
            return;
        }

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("[PlayerEntity] Player 上没有 PlayerInventory 组件！请给 Player GameObject 加上 PlayerInventory 脚本", this);
            return;
        }

        if (inventory.Add(item))
        {
            // 进背包后立刻关碰撞、停自转；可见性由 HandleInventoryChanged 统一管理
            // （选中格 → 挂到手上；其他格 → 隐藏），避免物品残留在地上
            item.CollectSilently();
        }
    }
    // 冲撞攻击等直接用 ClampToBounds 做硬钳制，不走墙滑。
    private void TickMovement(float deltaTime)
    {
        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude < 0.0001f)
        {
            return; // 无输入 = 静置
        }

        Vector3 direction = BuildCameraRelativeDirection(input);
        // 归一化上限，保证斜向（八向）速度与正向一致
        direction = Vector3.ClampMagnitude(direction, 1f);

        m_lastMoveDirection = direction;

        Vector3 currentPos = transform.position;
        Vector3 fullMove = currentPos + direction * m_moveSpeedInMetersPerSecond * deltaTime;

        // 1. 完整移动（X + Z）
        if (IsWithinBounds(fullMove))
        {
            transform.position = fullMove;
            return;
        }

        // 2. X-only 移动（保持原 Z，贴墙水平滑）
        Vector3 xOnly = new Vector3(fullMove.x, fullMove.y, currentPos.z);
        if (IsWithinBounds(xOnly))
        {
            transform.position = xOnly;
            return;
        }

        // 3. Z-only 移动（保持原 X，贴墙垂直滑）
        Vector3 zOnly = new Vector3(currentPos.x, fullMove.y, fullMove.z);
        if (IsWithinBounds(zOnly))
        {
            transform.position = zOnly;
            return;
        }

        // 4. 完全阻挡：用 ClampToBounds 做硬钳制（让玩家贴到边界上，
        //    下一帧继续推就能触发墙滑）
        transform.position = ClampToBounds(fullMove);
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;

        // 左：A 键或方向键左
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        // 右：D 键或方向键右
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        // 下：S 键或方向键下
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        // 上：W 键或方向键上
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        return input;
    }

    private Vector3 BuildCameraRelativeDirection(Vector2 input)
    {
        if (m_cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        // 把镜头的前向/右向压到地面平面上，让“上”输入对应屏幕上方
        Vector3 forward = m_cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = m_cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        return forward * input.y + right * input.x;
    }

    // 判断点是否在边界内（可活动区域内）。
    // Collider 版：点在**任意一个** Collider 内就算在边界内（多 Collider 做 OR 组合）；
    // 方形版：检查是否在 [min, max] 矩形内。
    private bool IsWithinBounds(Vector3 position)
    {
        if (HasBoundsColliders)
        {
            foreach (Collider col in m_boundsColliders)
            {
                if (IsPointInsideCollider(col, position))
                {
                    return true;
                }
            }

            return false;
        }

        // Fallback: 方形边界
        return position.x >= m_boundsMin.x && position.x <= m_boundsMax.x
            && position.z >= m_boundsMin.y && position.z <= m_boundsMax.y;
    }

    // 判断点是否在单个 Collider 内部（或非常接近表面）。
    // 用 ClosestPoint 的特性：内部点返回该点本身，外部点返回表面上最近的点。
    // 把 Y 轴对齐到 Collider 中心再查询，避免 Collider 的高度范围干扰 XZ 平面判定。
    // 禁用或失活的 Collider 直接跳过——机关门打开 = 把 Collider.enabled 设为 false 即可，即时生效。
    private bool IsPointInsideCollider(Collider collider, Vector3 position)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
        {
            return false;
        }

        const float k_tolerance = 0.02f;
        float colliderCenterY = collider.bounds.center.y;
        Vector3 queryPos = new Vector3(position.x, colliderCenterY, position.z);
        Vector3 closest = collider.ClosestPoint(queryPos);
        return Vector3.Distance(queryPos, closest) <= k_tolerance;
    }

    // 边界钳制：优先使用多 Collider 组合的可活动区域，未指定时回退到方形 AABB。
    // 冲撞攻击等没有墙滑逻辑的场景用这个方法做最终保险。
    private Vector3 ClampToBounds(Vector3 position)
    {
        if (HasBoundsColliders)
        {
            return ClampToColliderBounds(position);
        }

        // Fallback: 方形边界
        position.x = Mathf.Clamp(position.x, m_boundsMin.x, m_boundsMax.x);
        position.z = Mathf.Clamp(position.z, m_boundsMin.y, m_boundsMax.y);
        return position;
    }

    // 多 Collider 边界钳制：如果点在边界内直接返回；
    // 如果在边界外，找到所有 Collider 中离点最近的那个 ClosestPoint 作为钳制结果。
    private Vector3 ClampToColliderBounds(Vector3 position)
    {
        // 已经在边界内，无需钳制
        if (IsWithinBounds(position))
        {
            return position;
        }

        // 在边界外——找到所有 Collider 的 ClosestPoint 中离 position 最近的那个
        Vector3 bestClamped = position;
        float bestDistance = float.MaxValue;

        foreach (Collider col in m_boundsColliders)
        {
            // 跳过禁用或失活的 Collider（机关门打开 = collider.enabled = false）
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            float colliderCenterY = col.bounds.center.y;
            Vector3 queryPos = new Vector3(position.x, colliderCenterY, position.z);
            Vector3 closest = col.ClosestPoint(queryPos);
            float dist = Vector3.Distance(queryPos, closest);

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestClamped = closest;
            }
        }

        // 恢复玩家自身的 Y，避免被 Collider 高度拉动
        bestClamped.y = position.y;
        return bestClamped;
    }

    // 冲撞方向：优先取最近一次移动方向，从未移动过则朝镜头的右方
    private Vector3 ResolveChargeDirection()
    {
        if (m_lastMoveDirection.sqrMagnitude > 0.0001f)
        {
            return m_lastMoveDirection.normalized;
        }

        if (m_cameraTransform != null)
        {
            Vector3 screenRight = m_cameraTransform.right;
            screenRight.y = 0f;
            screenRight.Normalize();
            return screenRight;
        }

        return Vector3.right;
    }

    // 应用某个外观：切换字形贴图（Outline 会随之刷新）。贴图留空则保持当前外观。
    private void ApplyAppearance(int appearanceIndex)
    {
        m_currentAppearanceIndex = appearanceIndex;

        if (m_appearances == null || appearanceIndex < 0 || appearanceIndex >= m_appearances.Count)
        {
            return;
        }

        Sprite sprite = m_appearances[appearanceIndex].sprite;
        if (sprite != null)
        {
            SetGlyph(sprite);
        }
    }

    // 转体一周 + 半程切换外观的通用动画，外观变化与死亡共用。
    // 通过 SceneEntity 的自转挂点驱动，转体期间字形依旧 Billboard 朝向镜头。
    private Sequence CreateSpinSequence(int appearanceIndex, TweenCallback onCompleted)
    {
        BillboardRollDegrees = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(
                () => BillboardRollDegrees,
                value => BillboardRollDegrees = value,
                360f, m_spinDurationInSeconds)
            .SetEase(Ease.InOutSine));

        // 转到一半（180°）时切换外观，视觉上像是“翻面”
        sequence.InsertCallback(m_spinDurationInSeconds * 0.5f, () => ApplyAppearance(appearanceIndex));

        sequence.OnComplete(() =>
        {
            if (this == null)
            {
                return;
            }

            BillboardRollDegrees = 0f; // 转完回正，贴图保持正立
            onCompleted?.Invoke();
        });

        return sequence;
    }

    // 供嵌套状态类转发调用（嵌套类访问外层私有成员是安全的）
    private void ResetBillboardRoll()
    {
        BillboardRollDegrees = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        // 在 Scene 里缓存 Collider 数组（非 PlayMode 也能工作）
        Collider[] colliders = null;
        if (m_boundsZone != null)
        {
            colliders = m_boundsZone.GetComponentsInChildren<Collider>();
        }

        if (colliders != null && colliders.Length > 0)
        {
            // 画每个 Collider 的包围盒，让设计者一眼看出哪些区域可行走
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.6f); // 半透明青色
            foreach (Collider col in colliders)
            {
                Bounds bounds = col.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
        else
        {
            // Fallback: 方形边界的可视化
            Vector3 center = new Vector3((m_boundsMin.x + m_boundsMax.x) * 0.5f, 0f, (m_boundsMin.y + m_boundsMax.y) * 0.5f);
            Vector3 size = new Vector3(m_boundsMax.x - m_boundsMin.x, 0.1f, m_boundsMax.y - m_boundsMin.y);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
        }
    }

    // Normal：静置或移动，显示当前外观
    private class NormalState : IEntityState
    {
        private readonly PlayerEntity m_owner;

        public NormalState(PlayerEntity owner)
        {
            m_owner = owner;
        }

        public void Enter()
        {
            m_owner.ApplyAppearance(m_owner.m_currentAppearanceIndex);
        }

        public void Exit()
        {
        }

        public void Tick(float deltaTime)
        {
            m_owner.TickMovement(deltaTime);
        }
    }

    // 冲撞攻击：激活撞击检测 → 加速冲出 → 撞到目标或冲到最远处后弹回原位 → 回到 Normal
    private class ChargeAttackState : IEntityState
    {
        private readonly PlayerEntity m_owner;
        private Tween m_outTween;
        private Tween m_backTween;
        private Vector3 m_startPosition;
        private Vector3 m_peakPosition;
        private bool m_isReturning;

        public ChargeAttackState(PlayerEntity owner)
        {
            m_owner = owner;
        }

        public void Enter()
        {
            m_startPosition = m_owner.transform.position;
            m_peakPosition = m_owner.ClampToBounds(
                m_startPosition + m_owner.ResolveChargeDirection() * m_owner.m_chargeDistanceInMeters);
            m_isReturning = false;

            m_owner.ArmChargeHitbox();

            // 加速冲出：InCubic 让速度由慢到快，读起来像“发力撞击”
            m_outTween = m_owner.transform.DOMove(m_peakPosition, m_owner.m_chargeOutDurationInSeconds)
                .SetEase(Ease.InCubic)
                .OnComplete(BeginReturn);
        }

        // 撞到第一个目标就弹回：撞倒一片的问题由此解决
        public void HandleHitLanded()
        {
            BeginReturn();
        }

        private void BeginReturn()
        {
            if (m_isReturning || m_owner == null)
            {
                return;
            }

            m_isReturning = true;
            m_owner.DisarmChargeHitbox(); // 一次冲撞只生效一次

            if (m_outTween != null && m_outTween.IsActive())
            {
                m_outTween.Kill(); // 停在撞上的位置，从这里往回弹
            }

            // 弹回原位：OutCubic 收力回位，形成“撞完弹回”的手感
            m_backTween = m_owner.transform.DOMove(m_startPosition, m_owner.m_chargeBackDurationInSeconds)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    if (m_owner == null)
                    {
                        return;
                    }

                    m_owner.m_states.Switch(m_owner.m_normalState);
                });
        }

        public void Exit()
        {
            m_owner.DisarmChargeHitbox();

            if (m_outTween != null && m_outTween.IsActive())
            {
                m_outTween.Kill();
            }

            if (m_backTween != null && m_backTween.IsActive())
            {
                m_backTween.Kill();
            }

            m_outTween = null;
            m_backTween = null;
        }

        public void Tick(float deltaTime)
        {
            // 冲撞由 Tween 驱动，期间不接受移动输入
        }
    }

    // 外观变化：转体一周，半程切换外观，转完回到 Normal
    private class AppearanceChangeState : IEntityState
    {
        private readonly PlayerEntity m_owner;
        private Sequence m_sequence;

        public AppearanceChangeState(PlayerEntity owner)
        {
            m_owner = owner;
        }

        public void Enter()
        {
            m_sequence = m_owner.CreateSpinSequence(m_owner.m_pendingAppearanceIndex,
                () => m_owner.m_states.Switch(m_owner.m_normalState));
        }

        public void Exit()
        {
            if (m_sequence != null && m_sequence.IsActive())
            {
                m_sequence.Kill();
            }

            m_sequence = null;
            m_owner.ResetBillboardRoll();
        }

        public void Tick(float deltaTime)
        {
        }
    }

    // Die：与外观变化同样的转体，结束时停在死亡外观且不再回到 Normal
    private class DieState : IEntityState
    {
        private readonly PlayerEntity m_owner;
        private Sequence m_sequence;

        public DieState(PlayerEntity owner)
        {
            m_owner = owner;
        }

        public void Enter()
        {
            m_owner.m_isDead = true;
            m_owner.DisarmChargeHitbox();
            m_sequence = m_owner.CreateSpinSequence(m_owner.m_deathAppearanceIndex, null);
        }

        public void Exit()
        {
            if (m_sequence != null && m_sequence.IsActive())
            {
                m_sequence.Kill();
            }

            m_sequence = null;
            m_owner.ResetBillboardRoll();
        }

        public void Tick(float deltaTime)
        {
            // 死亡后不接受输入
        }
    }

    // 转场等待：转台旋转期间不接受移动/攻击输入；
    // 停稳后朝房间中心走一小步（由 RoomTurnManager 给出目标点），走完回到 Normal
    private class RoomTurnState : IEntityState
    {
        private readonly PlayerEntity m_owner;
        private Tween m_walkTween;

        public RoomTurnState(PlayerEntity owner)
        {
            m_owner = owner;
        }

        public void Enter()
        {
        }

        public void Exit()
        {
            KillWalkTween();
        }

        public void Tick(float deltaTime)
        {
            // 转台期间玩家不动，输入由 RoomTurnManager 锁住
        }

        public void WalkIntoRoom(Vector3 worldTargetPosition, float durationInSeconds)
        {
            KillWalkTween();

            // 转场不碰朝向：玩家独立于转台，朝向只由移动输入决定

            Vector3 direction = worldTargetPosition - m_owner.transform.position;
            direction.y = 0f;

            if (durationInSeconds > 0f && direction.sqrMagnitude > 0.0001f)
            {
                m_walkTween = m_owner.transform.DOMove(worldTargetPosition, durationInSeconds)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        if (m_owner == null)
                        {
                            return;
                        }

                        m_owner.m_states.Switch(m_owner.m_normalState);
                    });
            }
            else
            {
                // 不走位就直接回 Normal
                m_owner.m_states.Switch(m_owner.m_normalState);
            }
        }

        private void KillWalkTween()
        {
            if (m_walkTween != null && m_walkTween.IsActive())
            {
                m_walkTween.Kill();
            }

            m_walkTween = null;
        }
    }
}
