using System;
using UnityEngine;

// 挂在玩家“撞击检测”子物体上。该子物体需要 Collider（Is Trigger）+ 刚体，否则收不到触发器
// 回调（玩家靠 transform 移动，没有刚体就唤不起触发器事件）。
//
// 冲撞期间由 PlayerEntity 显式激活，命中第一个可受击目标后立刻失效——保证一次冲撞只打中一个
// 目标，撞到就弹回（弹回逻辑在 PlayerEntity.ChargeAttackState）。
public class ChargeHitbox : MonoBehaviour
{
    [Tooltip("单次冲撞造成的伤害")]
    [SerializeField] private int m_damage = 1;

    // 命中时抛出（目标 + 命中点），玩家据此立即弹回
    public event Action<IHittable, Vector3> HitLanded;

    private Collider m_collider;
    private bool m_isArmed;
    private bool m_hasHitThisCharge;

    private void Awake()
    {
        m_collider = GetComponent<Collider>();
        if (m_collider == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 缺少 Collider；撞击检测不会生效。", this);
        }

        // 平时失效：走路蹭过的东西不该掉血
        Disarm();
    }

    // 激活检测（冲撞开始时由玩家调用）
    public void Arm()
    {
        m_hasHitThisCharge = false;
        m_isArmed = true;

        if (m_collider != null)
        {
            m_collider.enabled = true;
        }
    }

    // 失效（冲撞结束或已命中时由玩家调用）
    public void Disarm()
    {
        m_isArmed = false;

        if (m_collider != null)
        {
            m_collider.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!m_isArmed || m_hasHitThisCharge)
        {
            return;
        }

        // 打不到自己：同一个根节点下的子物体（描边、手持物等）一律忽略
        if (other.transform.root == transform.root)
        {
            return;
        }

        IHittable target = other.GetComponentInParent<IHittable>();
        if (target == null)
        {
            return;
        }

        m_hasHitThisCharge = true;
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        target.ApplyHit(m_damage, hitPoint);
        HitLanded?.Invoke(target, hitPoint);
    }
}
