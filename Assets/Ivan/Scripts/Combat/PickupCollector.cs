using System;
using UnityEngine;

// 挂在玩家“拾取范围”子物体上。该子物体需要 Collider（Is Trigger）+ 刚体。
//
// 用 OnTriggerStay 反复报告范围内的掉落物，由玩家决定收不收：这样即使站着不动，
// 手上的东西腾空之后也能自动捡起来（OnTriggerEnter 只在进入那一瞬间报告一次）。
public class PickupCollector : MonoBehaviour
{
    // 范围内的掉落物（每个物理步都可能报一次，调用方自行过滤已收下的）
    public event Action<DroppedItem> ItemInRange;

    private Collider m_collider;

    private void Awake()
    {
        m_collider = GetComponent<Collider>();
        if (m_collider == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 缺少 Collider；拾取检测不会生效。", this);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        DroppedItem item = other.GetComponentInParent<DroppedItem>();
        if (item == null || item.IsCollected)
        {
            return;
        }

        ItemInRange?.Invoke(item);
    }
}
