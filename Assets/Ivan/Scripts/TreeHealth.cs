using DG.Tweening;
using UnityEngine;

// The chopping loop: a tree takes axe hits through IHittable, shakes, and after enough
// damage collapses into pickup sticks. StickPickup lives in the same file because it
// is the second half of the same loop (tree falls -> sticks on the ground -> backpack).
public class TreeHealth : MonoBehaviour, IHittable
{
    [Tooltip("Axe hits needed to fell the tree")]
    [SerializeField] private int m_maxHealth = 3;

    [Tooltip("Stick prefab dropped when the tree falls")]
    [SerializeField] private GameObject m_stickPrefab;

    [Tooltip("Number of sticks dropped around the stump")]
    [SerializeField] private int m_stickDropCount = 3;

    private int m_health;
    private Transform m_visual;
    private Breathing m_breathing;
    private Tween m_shakeTween;

    public bool IsAlive => m_health > 0;

    private void Awake()
    {
        m_health = m_maxHealth;
        m_visual = transform.Find("Visual");
        m_breathing = GetComponent<Breathing>();
    }

    private void OnDisable()
    {
        m_shakeTween?.Kill();
    }

    public void ApplyHit(int damage, Vector3 hitPoint)
    {
        if (!IsAlive)
        {
            return;
        }

        m_health -= damage;
        PlayHitFeedback();

        if (!IsAlive)
        {
            Fell();
        }
    }

    private void PlayHitFeedback()
    {
        if (m_visual == null)
        {
            return;
        }

        // Punch the sprite child; the root keeps breathing undisturbed
        m_shakeTween?.Kill();
        m_shakeTween = m_visual.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.5f);
    }

    private void Fell()
    {
        DropSticks();

        // Stop the idle breathing before the death shrink takes over the same property
        if (m_breathing != null)
        {
            m_breathing.enabled = false;
        }

        transform.DOScaleY(0.05f, 0.3f).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (this != null)
                {
                    Destroy(gameObject);
                }
            });
    }

    private void DropSticks()
    {
        if (m_stickPrefab == null)
        {
            Debug.LogWarning($"[{nameof(TreeHealth)}] No stick prefab assigned on {name}; nothing dropped.", this);
            return;
        }

        // Even ring plus jitter so drops never stack on top of each other
        for (int i = 0; i < m_stickDropCount; i++)
        {
            float angle = i * (Mathf.PI * 2f / m_stickDropCount) + Random.value * 0.8f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(0.5f, 1f);
            Vector3 spawnPos = transform.position + offset;
            spawnPos.y = ResolveGroundY(spawnPos);
            GameObject stick = Instantiate(m_stickPrefab, spawnPos, Quaternion.identity);
            AttachToCurrentRoom(stick);
        }
    }

    // 和 AttackableEntity 里的贴地逻辑相同：从树上方 10m 往下扫，
    // 跳过自身 Collider，用第一个命中的非 trigger Collider 的 Y
    private float ResolveGroundY(Vector2 xzPos)
    {
        Vector3 rayOrigin = new Vector3(xzPos.x, transform.position.y + 10f, xzPos.y);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            return hits[i].point.y;
        }
        return 0f;
    }

    // 把掉落物挂到当前激活的 Room 下，切场时自动隐藏/恢复
    private static void AttachToCurrentRoom(GameObject dropped)
    {
        RoomTurnManager manager = FindFirstObjectByType<RoomTurnManager>();
        if (manager == null || manager.CurrentRoom == null) return;
        dropped.transform.SetParent(manager.CurrentRoom, true);
    }
}

// Picked up into the player's backpack on touch. Both this and the player's
// CharacterController generate trigger callbacks, so no Rigidbody is needed.
public class StickPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // A stick equipped in the hand lives under the player and must never pick itself up.
        // Checked at runtime too (not just at equip time): the instance's Awake runs before
        // Instantiate applies the parent, and one missed guard would loop
        // pick-up -> destroy -> re-equip, minting infinite sticks.
        if (GetComponentInParent<PlayerInventory>() != null)
        {
            return;
        }

        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
        {
            return;
        }

        // 新 Hotbar 系统：找到同物体上的 DroppedItem → 放进 Hotbar → 禁用物理/自转
        DroppedItem dropped = GetComponent<DroppedItem>();
        if (dropped != null && inventory.Add(dropped))
        {
            dropped.CollectSilently();
        }
        else
        {
            // Hotbar 满了或没有 DroppedItem 组件，Destroy 兜底
            Destroy(gameObject);
        }
    }
}
