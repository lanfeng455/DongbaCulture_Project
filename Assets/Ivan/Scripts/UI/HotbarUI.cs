using UnityEngine;
using UnityEngine.UIElements;

// Hotbar 物品栏 UI：屏幕左下角 5 格水平排列，显示每格物品的 sprite，
// 点击切换选中（= 手持）。空槽显示边框，选中格高亮。
[RequireComponent(typeof(PlayerInventory))]
public class HotbarUI : MonoBehaviour
{
    [Header("UI 文档（留空则代码动态构建）")]
    [SerializeField] private UIDocument m_uiDocument;

    [Header("格子尺寸（像素）")]
    [SerializeField] private float m_slotSize = 64f;
    [SerializeField] private float m_slotGap = 6f;
    [SerializeField] private float m_bottomOffset = 20f;
    [SerializeField] private float m_leftOffset = 20f;

    private PlayerInventory m_inventory;
    private VisualElement m_slotContainer;
    private readonly Image[] m_slotImages = new Image[5];

    private void Awake()
    {
        m_inventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        m_inventory.InventoryChanged += RefreshUI;
    }

    private void OnDisable()
    {
        m_inventory.InventoryChanged -= RefreshUI;
    }

    private void Start()
    {
        if (m_uiDocument == null)
        {
            m_uiDocument = FindFirstObjectByType<UIDocument>();
        }

        if (m_uiDocument == null)
        {
            Debug.LogError($"[{nameof(HotbarUI)}] 场景里没有 UIDocument，无法创建 Hotbar UI。", this);
            enabled = false;
            return;
        }

        BuildUI();
        RefreshUI();
    }

    // 用代码构建 Hotbar：一个容器 + 5 个格子 + 1 个 Image 叠在格子上显示物品
    private void BuildUI()
    {
        VisualElement root = m_uiDocument.rootVisualElement;

        // 容器：绝对定位在左下角
        m_slotContainer = new VisualElement
        {
            name = "hotbar",
            pickingMode = PickingMode.Ignore
        };
        m_slotContainer.style.position = Position.Absolute;
        m_slotContainer.style.left = m_leftOffset;
        m_slotContainer.style.bottom = m_bottomOffset;
        m_slotContainer.style.flexDirection = FlexDirection.Row;

        root.Add(m_slotContainer);

        // 构建 5 个格子
        for (int i = 0; i < 5; i++)
        {
            int index = i; // 闭包捕获
            VisualElement slot = new VisualElement
            {
                name = $"hotbar__slot--{i}",
                pickingMode = PickingMode.Position
            };
            slot.style.width = m_slotSize;
            slot.style.height = m_slotSize;
            slot.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            slot.style.borderTopLeftRadius = 4;
            slot.style.borderTopRightRadius = 4;
            slot.style.borderBottomLeftRadius = 4;
            slot.style.borderBottomRightRadius = 4;
            // 最后一个格子不需要右边距
            if (i < 4)
            {
                slot.style.marginRight = m_slotGap;
            }
            slot.style.borderLeftWidth = 2;
            slot.style.borderRightWidth = 2;
            slot.style.borderTopWidth = 2;
            slot.style.borderBottomWidth = 2;
            slot.style.borderLeftColor = new Color(1f, 1f, 1f, 0.3f);
            slot.style.borderRightColor = new Color(1f, 1f, 1f, 0.3f);
            slot.style.borderTopColor = new Color(1f, 1f, 1f, 0.3f);
            slot.style.borderBottomColor = new Color(1f, 1f, 1f, 0.3f);

            // Image：叠在格子内部居中显示物品 sprite
            Image icon = new Image
            {
                name = $"hotbar__icon--{i}",
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.style.flexGrow = 1;
            icon.style.flexShrink = 1;
            icon.style.marginTop = 4;
            icon.style.marginBottom = 4;
            icon.style.marginLeft = 4;
            icon.style.marginRight = 4;

            slot.Add(icon);
            m_slotContainer.Add(slot);
            m_slotImages[i] = icon;

            // 点击事件
            slot.RegisterCallback<ClickEvent>(evt => OnSlotClicked(index));
        }
    }

    private void OnSlotClicked(int index)
    {
        if (index < 0 || index >= m_inventory.SlotCount) return;

        if (m_inventory.SelectedIndex == index)
        {
            // 点击已选中格 → 清空（空手）
            m_inventory.ClearSelected();
        }
        else
        {
            m_inventory.SelectSlot(index);
        }
    }

    private void RefreshUI()
    {
        if (m_slotContainer == null) return;

        for (int i = 0; i < 5; i++)
        {
            DroppedItem item = m_inventory[i];

            // 更新 Image sprite（用 sprite.texture 取实际像素贴图）
            if (m_slotImages[i] != null)
            {
                Sprite sprite = item != null && item.GlyphRenderer != null
                    ? item.GlyphRenderer.sprite
                    : null;
                m_slotImages[i].image = sprite != null ? sprite.texture : null;
            }

            // 更新选中高亮
            VisualElement slot = m_slotContainer.ElementAt(i);
            if (slot != null)
            {
                bool isSelected = i == m_inventory.SelectedIndex;
                if (isSelected)
                {
                    slot.style.borderLeftColor = new Color(1f, 0.9f, 0.4f, 0.9f);
                    slot.style.borderRightColor = new Color(1f, 0.9f, 0.4f, 0.9f);
                    slot.style.borderTopColor = new Color(1f, 0.9f, 0.4f, 0.9f);
                    slot.style.borderBottomColor = new Color(1f, 0.9f, 0.4f, 0.9f);
                    slot.style.backgroundColor = new Color(0.3f, 0.25f, 0.1f, 0.55f);
                }
                else
                {
                    slot.style.borderLeftColor = new Color(1f, 1f, 1f, 0.3f);
                    slot.style.borderRightColor = new Color(1f, 1f, 1f, 0.3f);
                    slot.style.borderTopColor = new Color(1f, 1f, 1f, 0.3f);
                    slot.style.borderBottomColor = new Color(1f, 1f, 1f, 0.3f);
                    slot.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                }
            }
        }
    }
}
