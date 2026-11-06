using UnityEngine;

// 物品类型枚举（保留给 ToolEquip 等旧系统，Axe 目前不在 Hotbar 里）
public enum ItemId
{
    Axe,
    Stick
}

// Hotbar 物品栏：固定 5 格，每格独立存一个 DroppedItem 实例。
// 选中哪格 → 那格的物品显示在玩家手持槽位（HeldSlot）。
// 同类物品不叠加，每格要么一个实例要么 null。
//
// 保留 ToolSlot（ItemId?）作为旧系统兼容：Axe 等工具还没放进 Hotbar。
public class PlayerInventory : MonoBehaviour
{
    [Header("Hotbar")]
    [Tooltip("Hotbar 格数（默认 5，类似 Minecraft）")]
    [SerializeField] private int m_slotCount = 5;

    [Header("旧系统兼容（Axe 等工具不在 Hotbar 里）")]
    [Tooltip("玩家初始持有的工具（目前只支持 Axe，左键挥斧）")]
    [SerializeField] private ItemId? m_toolSlot = ItemId.Axe;

    private DroppedItem[] m_slots;
    private int m_selectedIndex;

    // 选中格变化、拾取/丢弃都会广播，让 UI 和手持视觉同步
    public event System.Action InventoryChanged;

    public int SlotCount => m_slots != null ? m_slots.Length : m_slotCount;
    public int SelectedIndex => m_selectedIndex;

    // 旧系统兼容：工具槽（Axe 等）；Hotbar 选中格为空时可以用来挥斧
    public ItemId? ToolSlot => m_toolSlot;

    // 当前选中格的物品（= 手持物品）；空手时返回 null
    public DroppedItem SelectedItem =>
        m_slots != null && m_selectedIndex >= 0 && m_selectedIndex < m_slots.Length
            ? m_slots[m_selectedIndex]
            : null;

    public DroppedItem this[int index]
    {
        get
        {
            if (m_slots == null || index < 0 || index >= m_slots.Length) return null;
            return m_slots[index];
        }
    }

    private void Awake()
    {
        m_slots = new DroppedItem[m_slotCount];
    }

    // 把掉落物放进 Hotbar 的第一个空格；满了返回 false（物品留在地上）
    public bool Add(DroppedItem item)
    {
        if (item == null || item.IsCollected) return false;

        for (int i = 0; i < m_slots.Length; i++)
        {
            if (m_slots[i] == null)
            {
                m_slots[i] = item;

                // 如果这是第一个物品（之前全空），自动选中它
                if (SelectedItem == null)
                {
                    m_selectedIndex = i;
                }

                InventoryChanged?.Invoke();
                return true;
            }
        }

        return false; // Hotbar 满了
    }

    // 选中第 index 格：切走的物品自动回到 Hotbar 原位，新选中的去 HeldSlot
    public void SelectSlot(int index)
    {
        if (m_slots == null) return;
        if (index < 0 || index >= m_slots.Length) return;
        if (index == m_selectedIndex) return;

        m_selectedIndex = index;
        InventoryChanged?.Invoke();
    }

    // 把某个具体实例从 Hotbar 移除（放置成功时用）；返回 true=找到了并移除
    public bool Remove(DroppedItem item)
    {
        if (item == null || m_slots == null) return false;

        for (int i = 0; i < m_slots.Length; i++)
        {
            if (m_slots[i] == item)
            {
                m_slots[i] = null;

                // 如果移除的是当前选中格，跳到第一个有物品的格或 0
                if (i == m_selectedIndex)
                {
                    m_selectedIndex = FindFirstOccupiedOrZero();
                }

                InventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    // 强制刷新 UI（融合改了 sprite 后调用，让 HotbarUI 重绘图标）
    public void RefreshUI()
    {
        InventoryChanged?.Invoke();
    }

    // 清空选中格 → 空手（物品从 Hotbar 移除，丢回地上原位）
    public void ClearSelected()
    {
        if (SelectedItem == null) return;

        m_slots[m_selectedIndex] = null;
        m_selectedIndex = FindFirstOccupiedOrZero();
        InventoryChanged?.Invoke();
    }

    // 丢掉选中格物品（从 Hotbar 移除，丢回地上）；返回被丢掉的实例
    public DroppedItem DropSelected()
    {
        if (SelectedItem == null) return null;

        DroppedItem dropped = m_slots[m_selectedIndex];
        m_slots[m_selectedIndex] = null;
        m_selectedIndex = FindFirstOccupiedOrZero();
        InventoryChanged?.Invoke();
        return dropped;
    }

    private int FindFirstOccupiedOrZero()
    {
        for (int i = 0; i < m_slots.Length; i++)
        {
            if (m_slots[i] != null) return i;
        }
        return 0; // 全空，回 0
    }
}
