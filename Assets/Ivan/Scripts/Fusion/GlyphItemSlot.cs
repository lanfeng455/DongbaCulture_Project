using System.Collections.Generic;
using UnityEngine;

// 道具放置槽：没有文字、没有字形的空物体（"没有文字的 Glyph"）。
// 与 DongbaGlyph 共用同一套瞄准模式，但发射后走的是放置判定，
// 不做融合、也不弹组词提示。
//
// 流程：
//   手中道具发射到槽位 → 按 DroppedItem.GlyphWord 判断是否接受
//     接受   → 道具留在槽位上按布局堆叠；所有 ingredient 都集齐后调用完成 SignalSender
//     不接受 → 道具原样飞回手中；可选调用退回 SignalSender（可接 DialogSystem 做提示）
//
// Ingredient 列表定义"需要什么、要多少"。每个条目：
//   Glyph Word = 该种字的 DroppedItem.GlyphWord 值
//   Count      = 需要放置多少个
//
// 堆叠布局（Stack Layout）：
//   Vertical  —— 一字往上叠
//   Mountain  —— 山字形：最底层最宽，每往上一层少一个，自动居中
//
// 网格尺寸按道具精灵的实际大小自动算，间距参数只是"额外留白"。
// 道具的 pivot 未必在精灵中心（项目里的石头就是左下角 pivot），
// 因此定位一律以渲染器包围盒的视觉中心为准，而不是 transform 原点。
//
// 已放置的道具挂在**所在 Room** 的层级下（DioramaRoot 的直接子物体），
// 这样 Room 被 SetActive(false) 时道具自动跟着隐藏，切场回来再显示时状态完整保留。
// Room 本身 localScale = (1,1,1)，所以道具挂上去不会被非均匀缩放扭曲。
[DisallowMultipleComponent]
public class GlyphItemSlot : MonoBehaviour, IGlyphAimTarget
{
    [System.Serializable]
    public struct Ingredient
    {
        [Tooltip("要放置的道具文字（匹配 DroppedItem.GlyphWord）")]
        public string glyphWord;

        [Tooltip("这种字需要放置多少个才算集齐")]
        [Min(1)] public int count;
    }

    public enum StackLayout
    {
        Vertical,   // 一字往上叠
        Mountain    // 山字形：底层宽、往上收窄、每层居中
    }

    // 一个已放置道具的记录。
    // OffsetRight/Up 沿槽位本地 right/up 轴，单位米。
    // PlacementIndex = 放置时的序号（0-based），用于重算布局时准确判定它属于哪一层——
    // 不能靠 OffsetUpInMeters 反推，因为 cell 尺寸变化后每层的 up 值也会跟着变。
    // 道具挂在 Room 下，Room 可能被 DioramaRoot 带着旋转，但这不影响：
    // 我们每帧用世界空间写道具 position/rotation，它会自动跟着 Room 的变换。
    private sealed class PlacedRecord
    {
        public DroppedItem Item;
        public float OffsetRightInMeters;
        public float OffsetUpInMeters;
        public int PlacementIndex;
    }

    [Header("配方（需要什么、要多少）")]
    [Tooltip("每种字及其需要的数量")]
    [SerializeField] private List<Ingredient> m_ingredients = new List<Ingredient>
    {
        new Ingredient { glyphWord = "石", count = 3 },
        new Ingredient { glyphWord = "白石", count = 1 },
    };

    [Header("堆叠布局")]
    [SerializeField] private StackLayout m_stackLayout = StackLayout.Mountain;

    [Tooltip("最底层的道具个数（仅山字形使用）；上层每层比下层少一个，最少 1 个")]
    [Min(1)] [SerializeField] private int m_mountainBottomRowCount = 3;

    [Tooltip("同一层内相邻道具之间额外留的横向间隙（米）；0 = 刚好贴边")]
    [SerializeField] private float m_columnGapInMeters = 0.06f;

    [Tooltip("上下两层道具之间额外留的纵向间隙（米）")]
    [SerializeField] private float m_rowGapInMeters = 0.04f;

    [Tooltip("第一层道具底部相对槽位原点的高度（米）；要让道具落在架子台面上就调高一些")]
    [SerializeField] private float m_firstRowBottomHeightInMeters = 0.35f;

    [Header("朝向")]
    [Tooltip("放置上去的道具是否始终正对镜头；关闭后保持放置那一刻的世界朝向")]
    [SerializeField] private bool m_faceCamera = true;

    [Tooltip("沿相机方向额外前推多少米；跨过薄门/薄架子但不影响厚墙遮挡")]
    [SerializeField] private float m_depthPushInMeters = 0.05f;

    [Header("瞄准显示")]
    [Tooltip("瞄准时槽位周围的圈半径（米）")]
    [SerializeField] private float m_ringRadiusInMeters = 0.9f;

    [Tooltip("取消勾选后，瞄准时不会被选中")]
    [SerializeField] private bool m_isAimable = true;

    [Header("完成信号")]
    [Tooltip("所有 ingredient 都集齐时调用它的 Send()；SignalSender 的触发模式请设为 Manual")]
    [SerializeField] private SignalSender m_allPlacedSender;

    [Header("退回信号（可选）")]
    [Tooltip("道具被退回时调用它的 Send()（可接 DialogSystem 做提示）；留空则不发")]
    [SerializeField] private SignalSender m_rejectedSender;

    private readonly List<PlacedRecord> m_placedItems = new List<PlacedRecord>();
    private readonly Dictionary<string, int> m_placedByWord = new Dictionary<string, int>();

    private Camera m_camera;

    public string DisplayName => "放置槽";

    // 已集齐的槽位不再参与瞄准，圈也会随之消失
    public bool IsAimable => m_isAimable && isActiveAndEnabled && !IsComplete;

    public float RingRadiusInMeters => m_ringRadiusInMeters;
    public Vector3 WorldPosition => transform.position;

    // 飞行途中是否也要正对镜头（由放置流程决定，落位时才不会突然转向）
    public bool FaceCameraEnabled => m_faceCamera;

    // 总共已经放上去的道具数量（所有 ingredient 合计）
    public int PlacedCount { get; private set; }

    // 配方要求的总道具数（布局用它决定每层几个）
    public int TotalRequiredCount
    {
        get
        {
            if (m_ingredients == null || m_ingredients.Count == 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < m_ingredients.Count; i++)
            {
                total += Mathf.Max(1, m_ingredients[i].count);
            }

            return total;
        }
    }

    // 所有 ingredient 都集齐了吗？
    public bool IsComplete
    {
        get
        {
            if (m_ingredients == null || m_ingredients.Count == 0)
            {
                // 没配配方 = 永远不算完成（开发者应配好 ingredient 再用）
                return false;
            }

            for (int i = 0; i < m_ingredients.Count; i++)
            {
                Ingredient ingredient = m_ingredients[i];
                if (!m_placedByWord.TryGetValue(ingredient.glyphWord, out int placed))
                {
                    return false;
                }

                if (placed < ingredient.count)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private void OnEnable()
    {
        DongbaGlyphRegistry.Register(this);
    }

    private void OnDisable()
    {
        DongbaGlyphRegistry.Unregister(this);
        // 道具在 Room 层级下，Room 失活时 Unity 自动隐藏子物体，
        // 道具的记录保留，Room 再激活时原位显示
    }

    private void OnDestroy()
    {
        // 槽位被销毁时道具也不再有归属，一起清掉避免残留
        DestroyPlacedItems();
    }

    private void LateUpdate()
    {
        if (m_placedItems.Count == 0)
        {
            return;
        }

        Quaternion facing = m_faceCamera ? ResolveCameraFacing() : Quaternion.identity;

        for (int i = m_placedItems.Count - 1; i >= 0; i--)
        {
            PlacedRecord record = m_placedItems[i];

            // 道具可能在切场景时被销毁，顺手清掉失效引用
            if (record.Item == null)
            {
                m_placedItems.RemoveAt(i);
                continue;
            }

            ApplyPlacement(record, facing);
        }
    }

    // 这个道具是否被接受（真正的放置入口是 TryPlaceItem）。
    // ingredient 列表里有这种字、且它还没放够 → 接受；否则退回
    public bool Accepts(DroppedItem item)
    {
        if (item == null || IsComplete)
        {
            return false;
        }

        return FindPendingIngredient(item.GlyphWord) >= 0;
    }

    // 尝试放置：接受则把道具留在槽位上并返回 true；
    // 不接受则发退回信号并返回 false，道具由调用方负责飞回手中。
    // 完成/退回信号都在这里发，调用方只管飞行。
    public bool TryPlaceItem(DroppedItem item)
    {
        if (!Accepts(item))
        {
            if (m_rejectedSender != null)
            {
                m_rejectedSender.Send();
            }

            return false;
        }

        Vector2 offset = ResolveStackOffset(PlacedCount, MeasureItemSize(item));
        PlacedRecord record = new PlacedRecord
        {
            Item = item,
            OffsetRightInMeters = offset.x,
            OffsetUpInMeters = offset.y,
            PlacementIndex = PlacedCount, // 0-based 放置序号
        };

        m_placedItems.Add(record);
        PlacedCount++;

        // 道具原本在世界空间（飞行途中），SetParent 到 Room 根但保持世界变换不变。
        // Room 是 DioramaRoot 的直接子物体，localScale = (1,1,1)，
        // 所以挂上去不会有缩放扭曲，Room 失活时道具也会自动隐藏
        Transform roomRoot = ResolveRoomRoot();
        Vector3 worldPos = item.transform.position;
        Quaternion worldRot = item.transform.rotation;
        Vector3 worldScale = item.transform.lossyScale;

        item.transform.SetParent(roomRoot, true);
        item.transform.position = worldPos;
        item.transform.rotation = worldRot;
        item.transform.localScale = worldScale;

        // 立刻摆到位，避免等下一帧 LateUpdate 之间闪一下
        ApplyPlacement(record, m_faceCamera ? ResolveCameraFacing() : Quaternion.identity);

        m_placedByWord.TryGetValue(item.GlyphWord, out int current);
        m_placedByWord[item.GlyphWord] = current + 1;

        if (IsComplete && m_allPlacedSender != null)
        {
            m_allPlacedSender.Send();
        }

        return true;
    }

    // 下一个道具的落点（世界坐标，已按 pivot 偏移补偿）。
    // 传入即将放置的道具：网格尺寸取自它自己的精灵，落点也算准它的视觉中心
    public Vector3 GetNextStackWorldPosition(DroppedItem incoming)
    {
        Vector2 offset = ResolveStackOffset(PlacedCount, MeasureItemSize(incoming));
        Vector3 centerTarget = ResolveCenterWorld(offset.x, offset.y);

        SpriteRenderer renderer = incoming != null ? incoming.GlyphRenderer : null;
        if (renderer == null)
        {
            return centerTarget;
        }

        // 道具根节点到精灵视觉中心的当前偏移；飞行前朝向已经对齐过，落位后不会再跳
        Vector3 visualOffset = ResolveVisualCenter(renderer) - incoming.transform.position;
        return centerTarget - visualOffset;
    }

    // 把一条记录摆到位：朝向和位置都写世界空间值，
    // 这样 Room（挂在 DioramaRoot 下）的旋转不会让道具位置/朝向出错
    private void ApplyPlacement(PlacedRecord record, Quaternion facing)
    {
        Transform itemTransform = record.Item.transform;

        if (m_faceCamera)
        {
            itemTransform.rotation = facing;
        }

        Vector3 centerTarget = ResolveCenterWorld(
            record.OffsetRightInMeters, record.OffsetUpInMeters);

        SpriteRenderer renderer = record.Item.GlyphRenderer;
        if (renderer == null)
        {
            itemTransform.position = centerTarget;
        }
        else
        {
            // pivot 可能不在精灵中心，把视觉中心扣到格点上
            Vector3 visualOffset = ResolveVisualCenter(renderer) - itemTransform.position;
            itemTransform.position = centerTarget - visualOffset;
        }

        // 沿相机方向前推一点点：配合 PaperSprite shader 的深度拉平，
        // 让道具跨过薄门/薄架子，同时不影响厚墙的正确遮挡
        if (m_depthPushInMeters > 0f)
        {
            Camera camera = m_camera != null ? m_camera : Camera.main;
            if (camera != null)
            {
                itemTransform.position += camera.transform.forward * m_depthPushInMeters;
            }
        }
    }

    // 沿槽位自身右/上轴、按世界米数换算出的目标点。
    // Room 本身无缩放（localScale = (1,1,1)），直接用 transform.right/up 就是准确方向
    private Vector3 ResolveCenterWorld(float rightInMeters, float upInMeters)
    {
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        return transform.position + right * rightInMeters + up * upInMeters;
    }

    // 槽位所在的 Room 根：往上爬到 DioramaRoot 的直接子物体。
    // 约定层级：Slot → 架子 → ... → RoomPrefab → DioramaRoot
    private Transform ResolveRoomRoot()
    {
        Transform current = transform;

        // 找到第一个父物体是 DioramaRoot 同级的——实际上就是往上爬到倒数第 2 层
        // （Slot 所在的 Room Prefab 根节点）
        while (current.parent != null && current.parent.parent != null)
        {
            current = current.parent;
        }

        // 兜底：没爬到 Room 根就挂回场景根，道具不会丢但 Room 切换时不会跟着隐藏
        if (current.parent == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 槽位没有挂在 Room 下，道具挂场景根了，" +
                             "Room 切换时不会自动隐藏", this);
        }

        return current;
    }

    // 精灵视觉中心的世界坐标。用 TransformPoint 而不是 renderer.bounds，
    // 因为 bounds 在本帧刚改过旋转时可能还是上一帧的缓存
    private static Vector3 ResolveVisualCenter(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return renderer != null ? renderer.transform.position : Vector3.zero;
        }

        return renderer.transform.TransformPoint(renderer.sprite.bounds.center);
    }

    // 道具在世界里的实际尺寸（取精灵本地尺寸 × 损耗缩放，与当前旋转无关）
    private static Vector2 MeasureItemSize(DroppedItem item)
    {
        SpriteRenderer renderer = item != null ? item.GlyphRenderer : null;
        if (renderer == null || renderer.sprite == null)
        {
            // 没有渲染器时的兜底格子大小
            return new Vector2(0.5f, 0.5f);
        }

        Vector3 size = renderer.sprite.bounds.size;
        Vector3 lossyScale = renderer.transform.lossyScale;

        return new Vector2(
            Mathf.Abs(size.x * lossyScale.x),
            Mathf.Abs(size.y * lossyScale.y));
    }

    private Quaternion ResolveCameraFacing()
    {
        if (m_camera == null)
        {
            m_camera = Camera.main;
        }

        return m_camera != null ? m_camera.transform.rotation : Quaternion.identity;
    }

    // ingredient 列表里该字还有缺口吗？返回 ingredient 索引；没缺口或没配对返回 -1
    private int FindPendingIngredient(string glyphWord)
    {
        for (int i = 0; i < m_ingredients.Count; i++)
        {
            if (m_ingredients[i].glyphWord != glyphWord)
            {
                continue;
            }

            int needed = m_ingredients[i].count;
            int placed = m_placedByWord.TryGetValue(glyphWord, out int current) ? current : 0;
            if (placed < needed)
            {
                return i;
            }
        }

        return -1;
    }

    // 第 index 个道具的视觉中心相对槽位原点的偏移（单位：世界米）
    private Vector2 ResolveStackOffset(int index, Vector2 incomingCellSize)
    {
        int total = Mathf.Max(1, TotalRequiredCount);
        ResolveRowAndColumn(index, total, out int row, out int column, out int itemsInRow);

        // 按层统一 cell 尺寸：同一层里所有已放置道具 + 即将放置的 incoming 的最大尺寸
        Vector2 rowCellSize = ResolveRowCellSize(row, total, incomingCellSize);

        // 格子 = 道具实际尺寸 + 额外留白
        float cellWidth = rowCellSize.x + m_columnGapInMeters;
        float cellHeight = rowCellSize.y + m_rowGapInMeters;

        // 每层按本层实际道具数居中：3 个 → -1/0/+1，2 个 → -0.5/+0.5，1 个 → 0
        float right = (column - (itemsInRow - 1) * 0.5f) * cellWidth;

        // 第一层的"底部"贴在设定高度上，所以中心再加半个格子高
        float up = m_firstRowBottomHeightInMeters + cellHeight * 0.5f + row * cellHeight;

        return new Vector2(right, up);
    }

    // 第 row 层统一用的 cell 尺寸：遍历所有已放置道具，用 PlacementIndex 算出它属于哪一层，
    // 取同一层的最大尺寸 + 即将放置的 incoming 尺寸。
    // PlacementIndex 是放置时的原始序号，不受 cell 尺寸变化影响——这才是准确的层归属依据。
    private Vector2 ResolveRowCellSize(int row, int total, Vector2 incomingSize)
    {
        float maxW = incomingSize.x;
        float maxH = incomingSize.y;

        for (int i = 0; i < m_placedItems.Count; i++)
        {
            PlacedRecord rec = m_placedItems[i];
            if (rec.Item == null) continue;

            // 直接用 PlacementIndex 算它属于哪一层
            ResolveRowAndColumn(rec.PlacementIndex, total, out int recRow, out _, out _);
            if (recRow == row)
            {
                Vector2 size = MeasureItemSize(rec.Item);
                maxW = Mathf.Max(maxW, size.x);
                maxH = Mathf.Max(maxH, size.y);
            }
        }

        // 兜底最小格尺寸，避免极端情况下道具完全重叠
        maxW = Mathf.Max(maxW, 0.3f);
        maxH = Mathf.Max(maxH, 0.3f);

        return new Vector2(maxW, maxH);
    }

    // 把放置序号换算成"第几层、层内第几个、这一层共几个"
    private void ResolveRowAndColumn(int index, int total, out int row, out int column, out int itemsInRow)
    {
        if (m_stackLayout == StackLayout.Vertical)
        {
            row = index;
            column = 0;
            itemsInRow = 1;
            return;
        }

        // 山字形：从底层开始，每层容量比下层少一个（最少 1）
        int capacity = Mathf.Max(1, m_mountainBottomRowCount);
        int start = 0;
        int currentRow = 0;

        while (start < total)
        {
            int actualInRow = Mathf.Min(capacity, total - start);

            if (index < start + actualInRow)
            {
                row = currentRow;
                column = index - start;
                itemsInRow = actualInRow;
                return;
            }

            start += actualInRow;
            currentRow++;
            capacity = Mathf.Max(1, capacity - 1);
        }

        // 兜底：索引超出配方总数时继续往上单列叠
        row = currentRow;
        column = 0;
        itemsInRow = 1;
    }

    private void DestroyPlacedItems()
    {
        foreach (PlacedRecord record in m_placedItems)
        {
            if (record.Item != null)
            {
                Destroy(record.Item.gameObject);
            }
        }

        m_placedItems.Clear();
        m_placedByWord.Clear();
        PlacedCount = 0;
    }
}
