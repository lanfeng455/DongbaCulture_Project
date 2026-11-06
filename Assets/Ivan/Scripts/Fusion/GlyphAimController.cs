using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// 瞄准融合系统：
//   按住右键 → 进入瞄准：场景里所有可组合文字/放置槽被圆环包围，光标指着哪个就锁定哪个
//   松开右键 → 把手中的道具发射向锁定的目标，圈扩大，按目标类型分派：
//     DongbaGlyph   → 按配方融合（组词提示、改写结果），然后飞回手中
//     GlyphItemSlot → 放置判定：接受则堆叠在槽位上，不接受则原样飞回手中
//   光标没指着任何目标时松开 → 取消，道具留在手里
//
// 文字融合结果三种（见 FusionOutcome）：
//   不匹配      → 手里的字原样飞回
//   文本改写    → 手里的字（或目标字）变成新字，然后飞回
//   其他（待定）→ 触发配方上的 UnityEvent，手里的字照常飞回
//
// 刻意不挂到 PlayerEntity 的状态机上：瞄准期间玩家仍可移动，
// 也不会打断冲撞等已有状态。
[RequireComponent(typeof(PlayerEntity))]
public class GlyphAimController : MonoBehaviour
{
    [Header("融合数据")]
    [Tooltip("融合配方库；留空时任何组合都按不匹配处理")]
    [SerializeField] private DongbaFusionDatabase m_database;

    [Header("瞄准")]
    [Tooltip("能瞄准的最大距离（米）；超出后不锁定任何目标")]
    [SerializeField] private float m_maxAimDistanceInMeters = 15f;

    [Tooltip("光标命中判定的像素半径下限；远处的目标圈投影很小，靠它保证仍然点得到")]
    [SerializeField] private float m_minHitRadiusInPixels = 26f;

    [Tooltip("命中半径相对目标圈投影半径的倍率，1.15 ≈ 圈边缘再外扩 15%")]
    [SerializeField] private float m_hitRadiusMultiplier = 1.15f;

    [Tooltip("箭头起点相对玩家脚底的高度（米）")]
    [SerializeField] private float m_arrowStartHeightInMeters = 0.9f;

    [Header("发射与融合节奏（秒）")]
    [Tooltip("手中的字飞向目标的时长")]
    [SerializeField] private float m_flyOutDurationInSeconds = 0.35f;

    [Tooltip("结果飞回手中的时长")]
    [SerializeField] private float m_returnDurationInSeconds = 0.45f;

    [Tooltip("融合过程提示（逐字展示组词过程）；留空时自动在自身添加")]
    [SerializeField] private GlyphFusionHintDisplay m_hint;

    [Header("融合视觉")]
    [Tooltip("融合时目标的圈扩大到的半径（米）")]
    [SerializeField] private float m_expandedRingRadiusInMeters = 1.6f;

    [Tooltip("圈扩大的时长（秒）")]
    [SerializeField] private float m_expandDurationInSeconds = 0.3f;

    [Tooltip("落点从目标字往玩家方向回退的距离（米）。留出这个空隙，手里的字才不会盖在目标字上糊成一团")]
    [SerializeField] private float m_landingOffsetFromTargetInMeters = 0.55f;

    private PlayerEntity m_player;
    private GlyphAimVisuals m_visuals;
    private Camera m_aimCamera;

    private bool m_isAiming;
    private bool m_isFusing;
    private IGlyphAimTarget m_currentTarget;

    public bool IsAiming => m_isAiming;
    public bool IsFusing => m_isFusing;

    private void Awake()
    {
        m_player = GetComponent<PlayerEntity>();

        m_visuals = GetComponent<GlyphAimVisuals>();
        if (m_visuals == null)
        {
            m_visuals = gameObject.AddComponent<GlyphAimVisuals>();
        }

        m_hint = GetComponent<GlyphFusionHintDisplay>();
        if (m_hint == null)
        {
            m_hint = gameObject.AddComponent<GlyphFusionHintDisplay>();
        }
    }

    private void OnDisable()
    {
        // 飞行中的物品由 RunFusionAsync 的 finally 挂回槽位；
        // 这里只需要停掉瞄准视觉，避免圆环留在场景里
        ExitAim();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (m_player.IsDead || m_isFusing)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            TryEnterAim();
        }

        if (!m_isAiming)
        {
            return;
        }

        UpdateAimTarget();

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            TryFire();
        }
    }

    // 手里必须有"带文字身份"的道具才能进入瞄准
    private void TryEnterAim()
    {
        DroppedItem heldItem = m_player.HeldItem;
        if (heldItem == null || string.IsNullOrEmpty(heldItem.GlyphWord))
        {
            return;
        }

        if (DongbaGlyphRegistry.Targets.Count == 0)
        {
            return;
        }

        m_isAiming = true;
        m_currentTarget = null;

        m_visuals.ShowRings(DongbaGlyphRegistry.Targets);
        m_visuals.HighlightTarget(null);
        m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
    }

    private void UpdateAimTarget()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Camera aimCamera = ResolveAimCamera();
        if (aimCamera == null)
        {
            return;
        }

        // 光标指着谁就锁谁；一个都没指着 → 不锁定（松开右键即取消）
        IGlyphAimTarget target = DongbaGlyphRegistry.FindUnderCursor(
            aimCamera,
            mouse.position.ReadValue(),
            m_player.transform.position,
            m_maxAimDistanceInMeters,
            m_minHitRadiusInPixels,
            m_hitRadiusMultiplier);

        if (target != m_currentTarget)
        {
            m_currentTarget = target;
            m_visuals.HighlightTarget(target);
        }

        if (target == null)
        {
            m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
            return;
        }

        Vector3 arrowStart = m_player.transform.position + Vector3.up * m_arrowStartHeightInMeters;
        m_visuals.SetArrow(arrowStart, target.WorldPosition, true);
    }

    private Camera ResolveAimCamera()
    {
        if (m_aimCamera == null)
        {
            m_aimCamera = Camera.main;
        }

        return m_aimCamera;
    }

    private void TryFire()
    {
        IGlyphAimTarget target = m_currentTarget;
        m_isAiming = false;
        m_currentTarget = null;

        if (target == null)
        {
            ExitAim();
            return;
        }

        // 按目标类型分派：放置槽走放置流程，文字走融合流程。
        // 先转成具体类型，后续的 null 判断才能用 Unity 重载过的 == 识别伪 null
        if (target is GlyphItemSlot slot)
        {
            _ = RunPlacementAsync(slot);
            return;
        }

        if (target is DongbaGlyph glyph)
        {
            _ = RunFusionAsync(glyph);
            return;
        }

        ExitAim();
    }

    // 放置流程：发射 → 圈扩大 → 槽位判定 → 接受（堆叠留场）/ 不接受（原样飞回手中）。
    // 刻意不做组词提示——那是文字融合专属的表现
    private async Awaitable RunPlacementAsync(GlyphItemSlot slot)
    {
        DroppedItem item = m_player.HeldItem;
        if (item == null || slot == null)
        {
            ExitAim();
            return;
        }

        m_isFusing = true;

        // 物品离手后槽位是空的，要先挡住拾取，否则中途会捡起别的东西
        m_player.IsPickupBlocked = true;

        try
        {
            // 与融合一致的视觉节奏：只留目标的圈并扩大
            m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
            m_visuals.HideAllExcept(slot);
            m_visuals.ExpandRing(slot, m_expandedRingRadiusInMeters, m_expandDurationInSeconds);

            item.DetachFromSlot();

            // 飞行途中就正对镜头：落位时朝向不会再跳，
            // 也保证落点计算里的 pivot 偏移与实际朝向一致
            if (slot.FaceCameraEnabled)
            {
                item.FaceCamera();
            }

            await item.transform
                .DOMove(slot.GetNextStackWorldPosition(item), m_flyOutDurationInSeconds)
                .SetEase(Ease.InCubic)
                .AsyncWaitForCompletion();

            if (this == null || item == null || slot == null)
            {
                return;
            }

            if (slot.TryPlaceItem(item))
            {
                // 接受：道具 SetParent 到 Room 下（有父节点），手空了；
                // 从 Hotbar 移除这个引用（触发 InventoryChanged → UI 刷新 → HeldSlot 卸物）
                PlayerInventory inventory = m_player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.Remove(item);
                }
                return;
            }

            // 不接受：道具原样飞回手中
            await ReturnItemToHandAsync(item);

            if (this == null || item == null)
            {
                return;
            }

            item.AttachToSlot(m_player.HeldSlot);
        }
        catch (System.Exception exception)
        {
            // 飞行途中物品或槽位被销毁时，DOTween / Awaitable 会抛异常。
            // 这种情况不需要刷错误，只有对象还活着才是真的逻辑问题
            if (this != null)
            {
                Debug.LogWarning($"[{GetType().Name}] 放置流程中断：{exception.Message}", this);
            }
        }
        finally
        {
            m_isFusing = false;

            if (m_player != null)
            {
                m_player.IsPickupBlocked = false;

                // 放置成功（挂 Room 下）与退回完成（挂 HeldSlot 下）都有父节点；
                // 只有中途被打断、物品还悬在半空（无父节点）时才兜底挂回手中
                if (item != null && item.transform.parent == null)
                {
                    item.AttachToSlot(m_player.HeldSlot);
                }
            }

            if (m_visuals != null)
            {
                m_visuals.HideRings();
                m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
            }
        }
    }

    // 发射 → 圈扩大 → 出结果 → 飞回手中
    private async Awaitable RunFusionAsync(DongbaGlyph target)
    {
        DroppedItem item = m_player.HeldItem;
        if (item == null || target == null)
        {
            ExitAim();
            return;
        }

        m_isFusing = true;

        // 物品离手后槽位是空的，要先挡住拾取，否则中途会捡起别的东西
        m_player.IsPickupBlocked = true;

        try
        {
            await RunFusionSequenceAsync(item, target);
        }
        catch (System.Exception exception)
        {
            // 飞行途中物品或玩家被销毁时，DOTween / Awaitable 会抛异常。
            // 这种情况不需要刷错误，只有对象还活着才是真的逻辑问题。
            if (this != null)
            {
                Debug.LogWarning($"[{GetType().Name}] 融合流程中断：{exception.Message}", this);
            }
        }
        finally
        {
            // 无论正常结束还是中途被打断（物体销毁、组件禁用），都要复位状态
            m_isFusing = false;

            if (m_player != null)
            {
                m_player.IsPickupBlocked = false;

                // 融合改了道具 sprite → 强制 HotbarUI 刷新
                PlayerInventory inventory = m_player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.RefreshUI();
                }

                // 中途退出时物品可能还悬在目标位置，必须挂回槽位，
                // 否则它会永远停在场景中央（也拿不回来）
                if (item != null && item.transform.parent != m_player.HeldSlot)
                {
                    item.AttachToSlot(m_player.HeldSlot);
                }
            }

            if (m_visuals != null)
            {
                m_visuals.HideRings();
                m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
            }

            if (m_hint != null)
            {
                m_hint.Hide();
            }
        }
    }

    private async Awaitable RunFusionSequenceAsync(DroppedItem item, DongbaGlyph target)
    {
        // 只保留目标的圈，并让它扩大，做出"把字吸进去"的观感
        m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
        m_visuals.HideAllExcept(target);
        m_visuals.ExpandRing(target, m_expandedRingRadiusInMeters, m_expandDurationInSeconds);

        item.DetachFromSlot();

        await item.transform
            .DOMove(ResolveLandingPosition(target, item), m_flyOutDurationInSeconds)
            .SetEase(Ease.InCubic)
            .AsyncWaitForCompletion();

        if (this == null || item == null || target == null)
        {
            return;
        }

        // 先把结果查出来（还不改字），用来驱动提示文字；
        // 真正改写放在提示展示结果那一刻，两边的节奏就对齐了
        string targetWord = target.GlyphWord;
        string heldWord = item.GlyphWord;
        DongbaFusionRecipe recipe = ResolveRecipe(heldWord, targetWord);
        string resultWord = ResolveResultWord(recipe);

        await m_hint.PlayAsync(
            target.WorldPosition,
            targetWord,
            heldWord,
            resultWord,
            () => ApplyFusionResult(item, target, recipe));

        if (this == null || item == null)
        {
            return;
        }

        await ReturnItemToHandAsync(item);

        if (this == null || item == null)
        {
            return;
        }

        // 状态复位统一交给 RunFusionAsync 的 finally
        item.AttachToSlot(m_player.HeldSlot);
    }

    // 道具飞回手中（融合结束 / 放置被退回共用）
    private async Awaitable ReturnItemToHandAsync(DroppedItem item)
    {
        Vector3 handPosition = m_player.HeldSlot != null
            ? m_player.HeldSlot.position
            : m_player.transform.position + Vector3.up * m_arrowStartHeightInMeters;

        await item.transform
            .DOMove(handPosition, m_returnDurationInSeconds)
            .SetEase(Ease.OutCubic)
            .AsyncWaitForCompletion();
    }

    // 落点：从目标字往玩家方向回退，让两个字并排不重叠
    private Vector3 ResolveLandingPosition(DongbaGlyph target, DroppedItem item)
    {
        Vector3 targetPosition = target.WorldPosition;
        Vector3 playerPosition = m_player.transform.position;

        Vector3 toPlayer = playerPosition - targetPosition;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            // 玩家几乎站在目标正上方时，用玩家朝向兜底
            toPlayer = -m_player.transform.forward;
            toPlayer.y = 0f;
        }

        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return targetPosition;
        }

        Vector3 direction = toPlayer.normalized;
        float distanceToTarget = toPlayer.magnitude;

        // 两个字各自水平半径之和，再留 15% 间隙——这样不管字形多大都不会叠住
        float glyphHalfExtent = ResolveRendererHorizontalExtent(target.GlyphRenderer);
        float itemHalfExtent = ResolveRendererHorizontalExtent(item.GlyphRenderer);
        float separation = (glyphHalfExtent + itemHalfExtent) * 1.15f;

        // 参数是下限：想退得更远就直接调大，不再被任何上限截断
        float offset = Mathf.Max(m_landingOffsetFromTargetInMeters, separation);

        // 唯一的硬约束：别退到玩家身后
        offset = Mathf.Min(offset, distanceToTarget * 0.85f);

        return targetPosition + direction * offset;
    }

    // 取渲染器在世界空间里的水平半径。
    // 三个轴取最大值：字形平躺时看 x/z，billboard 朝相机时 y 也参与，都能覆盖到
    private static float ResolveRendererHorizontalExtent(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return 0f;
        }

        Bounds bounds = renderer.bounds;
        return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
    }

    // 查表但不下任何判断，单独拆出来是为了让提示文字和实际改写共用同一份结果
    private DongbaFusionRecipe ResolveRecipe(string heldWord, string targetWord)
    {
        if (m_database == null)
        {
            return null;
        }

        m_database.TryFind(heldWord, targetWord, out DongbaFusionRecipe recipe);
        return recipe;
    }

    // 提示里该展示的结果文字；不匹配 / 自定义结果时返回 null（提示会表现为"合不成"）
    private static string ResolveResultWord(DongbaFusionRecipe recipe)
    {
        if (recipe == null)
        {
            return null;
        }

        switch (recipe.Outcome)
        {
            case FusionOutcome.RewriteHeld:
            case FusionOutcome.RewriteTarget:
                return recipe.ResultGlyph;

            default:
                return null;
        }
    }

    private void ApplyFusionResult(DroppedItem item, DongbaGlyph target, DongbaFusionRecipe recipe)
    {
        if (recipe == null)
        {
            // 不匹配：什么都不改，原样飞回
            return;
        }

        switch (recipe.Outcome)
        {
            case FusionOutcome.RewriteHeld:
                if (!string.IsNullOrEmpty(recipe.ResultGlyph))
                {
                    item.SetGlyphWord(recipe.ResultGlyph, recipe.ResultSprite);
                }
                break;

            case FusionOutcome.RewriteTarget:
                if (!string.IsNullOrEmpty(recipe.ResultGlyph))
                {
                    target.ApplyGlyphWord(recipe.ResultGlyph, recipe.ResultSprite);
                }
                break;

            case FusionOutcome.Custom:
                recipe.InvokeFused();
                break;

            case FusionOutcome.NoMatch:
            default:
                break;
        }
    }

    private void ExitAim()
    {
        m_isAiming = false;
        m_currentTarget = null;

        if (m_visuals != null)
        {
            m_visuals.HideRings();
            m_visuals.SetArrow(Vector3.zero, Vector3.zero, false);
        }
    }
}
