using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// Owns the player's hands: shows the equipped tool, plays the swing on left click,
// and delivers damage to whatever IHittable the swing reaches. Mouse routing also lives
// here so a click can only mean one thing: bubble -> backpack UI, player -> toggle
// backpack, anything else -> swing the tool.
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMov))]
[RequireComponent(typeof(BackpackUI))]
public class ToolEquip : MonoBehaviour
{
    [Header("Tool visuals")]
    [Tooltip("Prefab shown in the hand while the axe is equipped")]
    [SerializeField] private GameObject m_axePrefab;

    [Tooltip("Prefab shown in the hand while the stick is equipped")]
    [SerializeField] private GameObject m_stickPrefab;

    [Header("Swing")]
    [Tooltip("Damage dealt to one target per swing")]
    [SerializeField] private int m_damagePerHit = 1;

    [Tooltip("How far ahead of the player the swing can reach, in meters")]
    [SerializeField] private float m_hitRangeInMeters = 1.1f;

    [Tooltip("Radius of the swing hit check, in meters")]
    [SerializeField] private float m_hitRadiusInMeters = 0.7f;

    // The axe sweeps from upright to 75° toward the facing side, inside the requested 45°-90° arc
    private const float k_swingAngleDegrees = 75f;
    private const float k_swingOutInSeconds = 0.16f;
    private const float k_swingBackInSeconds = 0.14f;
    // Wide enough that the axe floats clearly beside the body instead of overlapping it
    private const float k_handSideOffsetInMeters = 0.6f;
    private const float k_handHeightInMeters = 0.85f;

    private PlayerInventory m_inventory;
    private PlayerMov m_playerMov;
    private BackpackUI m_backpackUI;
    private Transform m_toolAnchor;
    private Transform m_cameraTransform;
    private GameObject m_currentToolInstance;
    private SpriteRenderer m_currentBladeRenderer;
    private Tween m_swingTween;
    private readonly Collider[] m_hitBuffer = new Collider[8];

    private void Awake()
    {
        m_inventory = GetComponent<PlayerInventory>();
        m_playerMov = GetComponent<PlayerMov>();
        m_backpackUI = GetComponent<BackpackUI>();
        if (m_backpackUI != null)
        {
            // 旧头部气泡背包 UI 暂时禁用，HotbarUI 替代了它
            m_backpackUI.enabled = false;
        }

        // The anchor only marks the hand; LateUpdate keeps it on the facing side in screen space
        Transform anchor = transform.Find("ToolAnchor");
        if (anchor == null)
        {
            anchor = new GameObject("ToolAnchor").transform;
            anchor.SetParent(transform, false);
        }
        m_toolAnchor = anchor;
    }

    private void OnEnable()
    {
        m_inventory.InventoryChanged += HandleInventoryChanged;
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            m_cameraTransform = Camera.main.transform;
        }
        RefreshToolVisual();
    }

    private void OnDisable()
    {
        m_inventory.InventoryChanged -= HandleInventoryChanged;
        m_swingTween?.Kill();
    }

    private void Update()
    {
        RouteMouseClick();
    }

    private void LateUpdate()
    {
        if (m_cameraTransform == null)
        {
            return;
        }

        // Keep the hand on the facing side, at chest height, in screen space
        m_toolAnchor.position = transform.position
            + m_cameraTransform.right * (m_playerMov.FacingSign * k_handSideOffsetInMeters)
            + Vector3.up * k_handHeightInMeters;

        // Hotbar 模式：选中的 DroppedItem 需要每帧 FaceCamera 并保持在 toolAnchor 位置
        if (m_inventory != null && m_inventory.SelectedItem != null)
        {
            DroppedItem hotbarItem = m_inventory.SelectedItem;
            hotbarItem.transform.position = m_toolAnchor.position; // 跟住 toolAnchor
            hotbarItem.FaceCamera();                                 // 旋转 + depth push
            // flipX 由 DroppedItem 自己的 SpriteRenderer 处理，这里不干预
            return;
        }

        if (m_currentBladeRenderer != null)
        {
            // Mirror the glyph around its grip so the axe turns together with the facing
            m_currentBladeRenderer.flipX = m_playerMov.FacingSign < 0;
        }
    }

    private void RouteMouseClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || m_cameraTransform == null)
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Collide))
        {
            BackpackBubble bubble = hit.collider.GetComponentInParent<BackpackBubble>();
            if (bubble != null && m_backpackUI != null)
            {
                m_backpackUI.HandleBubbleClick(bubble.Kind);
                return;
            }

            if (hit.collider.GetComponentInParent<PlayerMov>() == m_playerMov && m_backpackUI != null)
            {
                m_backpackUI.Toggle();
                return;
            }
        }

        TrySwing();
    }

    // Swings the equipped tool if it can swing; public so a future input action can trigger it too
    public void TrySwing()
    {
        if (m_inventory.ToolSlot != ItemId.Axe || m_currentToolInstance == null)
        {
            return;
        }
        if (m_swingTween != null && m_swingTween.IsActive())
        {
            return;
        }

        // The blade rotates around its own origin; the sprite's bottom pivot makes that the grip
        Transform blade = m_currentToolInstance.transform.Find("Blade");
        if (blade == null)
        {
            return;
        }

        float direction = m_playerMov.FacingSign;

        m_swingTween = DOTween.Sequence()
            .Append(blade.DOLocalRotate(new Vector3(0f, 0f, -k_swingAngleDegrees * direction), k_swingOutInSeconds)
                .SetEase(Ease.OutCubic))
            .Append(blade.DOLocalRotate(Vector3.zero, k_swingBackInSeconds)
                .SetEase(Ease.InOutSine));

        // Land the hit right as the swing reaches its lowest point
        DOVirtual.DelayedCall(k_swingOutInSeconds, ApplySwingHit);
    }

    private void ApplySwingHit()
    {
        if (this == null || !isActiveAndEnabled || m_cameraTransform == null)
        {
            return;
        }

        // Check slightly ahead of the player, biased toward the facing side
        Vector3 forward = m_cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 center = transform.position + Vector3.up * k_handHeightInMeters
            + m_cameraTransform.right * (m_playerMov.FacingSign * m_hitRangeInMeters * 0.7f)
            + forward * (m_hitRangeInMeters * 0.5f);

        int hitCount = Physics.OverlapSphereNonAlloc(center, m_hitRadiusInMeters, m_hitBuffer,
            ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            IHittable hittable = m_hitBuffer[i].GetComponentInParent<IHittable>();
            if (hittable == null || !hittable.IsAlive)
            {
                continue;
            }

            // One clean hit per swing, never a multi-target drain
            hittable.ApplyHit(m_damagePerHit, m_hitBuffer[i].ClosestPoint(center));
            break;
        }
    }

    private void HandleInventoryChanged()
    {
        RefreshToolVisual();
    }

    private void RefreshToolVisual()
    {
        // 把上一个 Hotbar 物品从 toolAnchor 卸下来（SetParent(null, true) 保持世界坐标）
        // Hotbar 持有引用，不能 Destroy，只解除父子让它留在场景原位（但 Collider 已禁用）
        if (m_currentToolInstance != null)
        {
            m_currentToolInstance.transform.SetParent(null, true);
        }
        m_currentToolInstance = null;
        m_currentBladeRenderer = null;

        // Hotbar 模式：选中格有物品就挂上 toolAnchor，LateUpdate 每帧管位置和旋转
        if (m_inventory != null && m_inventory.SelectedItem != null)
        {
            DroppedItem item = m_inventory.SelectedItem;
            item.gameObject.SetActive(true);
            // KeepWorldTransform=true 保留它在地上的贴地位置（还没到 LateUpdate 接管）
            item.transform.SetParent(m_toolAnchor, true);
            m_currentToolInstance = item.gameObject;
            m_currentBladeRenderer = item.GlyphRenderer;
            return;
        }

        // Hotbar 空手时：fallback 旧的 Axe/Stick prefab 逻辑
        GameObject prefab = null;
        if (m_inventory != null && m_inventory.ToolSlot == ItemId.Axe)
        {
            prefab = m_axePrefab;
        }
        else if (m_inventory != null && m_inventory.ToolSlot == ItemId.Stick)
        {
            prefab = m_stickPrefab;
        }

        if (prefab != null)
        {
            m_currentToolInstance = Instantiate(prefab, m_toolAnchor);
            m_currentToolInstance.transform.localPosition = Vector3.zero;
            StickPickup pickup = m_currentToolInstance.GetComponent<StickPickup>();
            if (pickup != null) pickup.enabled = false;
            Transform blade = m_currentToolInstance.transform.Find("Blade");
            if (blade != null) m_currentBladeRenderer = blade.GetComponent<SpriteRenderer>();
        }
    }
}
