using UnityEngine;

// Head-mounted backpack UI（已废弃，由 HotbarUI 替代）。
// 保留文件只是为了让 RequireComponent(typeof(BackpackUI)) 不丢引用。
// ToolEquip.Awake 会把它 enabled = false，运行时不做事。
[DefaultExecutionOrder(1000)]
public class BackpackUI : MonoBehaviour
{
    [Header("Sprites（已废弃，HotbarUI 不读这些）")]
    [SerializeField] private Sprite m_bubbleSprite;
    [SerializeField] private Sprite m_axeSprite;
    [SerializeField] private Sprite m_stickSprite;

    // 以下全是空方法，ToolEquip 加了 null 保护，不会真的调进来
    public void Toggle() { }
    public void HandleBubbleClick(BubbleKind kind) { }
}

public enum BubbleKind
{
    Big,
    Left,
    Right
}

// Marker on a clickable backpack bubble; BackpackUI owns all the behavior.
public class BackpackBubble : MonoBehaviour
{
    public BubbleKind Kind { get; private set; }

    public void Setup(BubbleKind kind, SpriteRenderer itemRenderer, TextMesh countLabel)
    {
        Kind = kind;
    }

    public void SetItem(Sprite sprite, Color tint, int count) { }
}
