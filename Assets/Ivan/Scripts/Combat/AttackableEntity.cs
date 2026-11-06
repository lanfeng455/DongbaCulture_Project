using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 可受击实体的父类：树、敌人等都继承它。负责血量、受击反馈与死亡掉落。
// 和其他实体一样，木棍、字形、位置、大小全部由预制体控制，本组件不生成任何子物体。
// 一切伤害都从 ApplyHit 进来（玩家冲撞、之后的武器），所以新增可受击对象无需改动攻击方。
public class AttackableEntity : SceneEntity, IHittable
{
    // 一条掉落表：掉落物预制体 + 数量 + 散落半径
    [System.Serializable]
    public struct DropEntry
    {
        [Tooltip("掉落物预制体（挂 DroppedItem 的那个）")]
        public GameObject prefab;

        [Tooltip("掉落数量")]
        public int count;

        [Tooltip("以死亡位置为中心的散落半径（米）")]
        public float scatterRadius;
    }

    [Header("生命值")]
    [Tooltip("最大血量；被扣到 0 即死亡")]
    [SerializeField] private int m_maxHealth = 3;

    [Header("死亡掉落")]
    [Tooltip("死亡时掉落的物品表（留空则不掉落）")]
    [SerializeField] private List<DropEntry> m_drops = new List<DropEntry>();

    [Header("死亡")]
    [Tooltip("死亡延迟（秒）：让受击白闪播完再消失；设为 0 表示立即销毁")]
    [SerializeField] private float m_deathDelayInSeconds = 0.18f;

    private int m_health;
    private bool m_isDying;

    public bool IsAlive => m_health > 0 && !m_isDying;
    public int Health => m_health;
    public int MaxHealth => m_maxHealth;

    protected override void Awake()
    {
        base.Awake();
        m_health = m_maxHealth;
    }

    // IHittable：伤害的唯一入口
    public void ApplyHit(int damage, Vector3 hitPoint)
    {
        if (!IsAlive || damage <= 0)
        {
            return;
        }

        m_health = Mathf.Max(0, m_health - damage);
        PlayHitFeedback(hitPoint);

        if (m_health <= 0)
        {
            Die();
        }
    }

    // 受击反馈：默认白闪。屏幕震动等额外反馈加在这里（子类 override 时记得调用 base）
    protected virtual void PlayHitFeedback(Vector3 hitPoint)
    {
        PlayHitFlash();
    }

    // 死亡：等受击白闪播完再掉落并销毁自身。若立即 Destroy，致命一击的白闪会被一起吞掉。
    // 子类可 override 做倒塌动画（那时要自行处理销毁时机）。
    protected virtual void Die()
    {
        if (m_isDying)
        {
            return;
        }

        m_isDying = true;

        if (m_deathDelayInSeconds <= 0f)
        {
            DropLoot();
            Destroy(gameObject);
            return;
        }

        DOVirtual.DelayedCall(m_deathDelayInSeconds, () =>
        {
            if (this == null)
            {
                return; // 等待期间已被销毁
            }

            DropLoot();
            Destroy(gameObject);
        });
    }

    // 按掉落表在死亡位置周围散落物品：均匀分角 + 抖动，避免叠在同一个点上。
    // 生成前会往下 Raycast 找地面贴地——这样不管掉落 Prefab 有没有 Rigidbody，
    // 都不会卡在 transform.position 的半空里（树的根节点 Y 通常 ≠ 地面 Y）
    private void DropLoot()
    {
        if (m_drops == null || m_drops.Count == 0)
        {
            return;
        }

        foreach (DropEntry entry in m_drops)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 掉落表里有未指定预制体的条目，已跳过。", this);
                continue;
            }

            int count = Mathf.Max(0, entry.count);
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1
                    ? i * (Mathf.PI * 2f / count) + Random.value * 0.6f
                    : Random.value * Mathf.PI * 2f;
                float radius = entry.scatterRadius * Random.Range(0.35f, 1f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                Vector3 spawnPos = transform.position + offset;
                spawnPos.y = ResolveGroundY(spawnPos);
                GameObject dropped = Instantiate(entry.prefab, spawnPos, Quaternion.identity);
                AttachToCurrentRoom(dropped);
            }
        }
    }

    // 从 (x, 10m, z) 往下找地面 Y。
    // 跳过自己身上的 Collider（RaycastAll 排序后跳过第一个命中自己的），
    // 找到第一个非 trigger 的地面 Collider 就用它的 hit point。
    private float ResolveGroundY(Vector2 xzPos)
    {
        Vector3 rayOrigin = new Vector3(xzPos.x, transform.position.y + 10f, xzPos.y);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);

        // 按距离从近到远排序
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            // 跳过自己身上的 Collider
            if (hits[i].collider.transform.IsChildOf(transform))
            {
                continue;
            }

            return hits[i].point.y;
        }

        // 没找到任何地面——fallback 到 y=0
        return 0f;
    }

    // 把掉落物挂到当前激活的 Room 下：这样 Room SetActive(false) 时掉落物自动隐藏，
    // 切场回来又原样在原位。Room 不存在就留在场景根，世界坐标不变。
    private static void AttachToCurrentRoom(GameObject dropped)
    {
        RoomTurnManager manager = FindFirstObjectByType<RoomTurnManager>();
        if (manager == null || manager.CurrentRoom == null)
        {
            return;
        }

        dropped.transform.SetParent(manager.CurrentRoom, true);
    }
}
