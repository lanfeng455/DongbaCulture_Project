using UnityEngine;

// 挂在每个门洞上：玩家走进门 → 让转台把门另一侧的房间转到镜头前。
//
// 判定方式说明：玩家身上没有碰撞体/刚体（这是现有预制体的结构），
// 所以这里不用物理 Trigger 回调，而是每帧用 Collider.ClosestPoint 判断玩家位置是否在圈内。
// 两个 Collider 只是「范围定义」，勾不勾 Is Trigger 都可以，建议勾上避免物理阻挡。
//
// 所有房间都常驻场景，转场只做 SetActive + 旋转，不需要预加载。
[DisallowMultipleComponent]
public class DoorTurnTrigger : MonoBehaviour
{
    [Header("目标房间")]
    [Tooltip("目标房间 PrefabInstance（DioramaRoot 的直接子物体）")]
    [SerializeField] private Transform m_targetRoom;

    [Tooltip("（已废弃）旋转角度现在由 RoomTurnManager 根据目标房间位置自动算出，这个字段不再使用")]
    [SerializeField] private float m_turnAngleInDegrees = 90f;

    [Tooltip("玩家进入这个门后，在目标房间里从哪个出口点出来（目标房间里同名空物体的名字）")]
    [SerializeField] private string m_exitPointName = "PlayerExit";

    [Header("触发范围")]
    [Tooltip("提交范围（小圈）：玩家进圈就转场。留空时用本物体上的 Collider")]
    [SerializeField] private Collider m_commitTrigger;

    private RoomTurnManager m_manager;
    private bool m_wasInsideCommit;
    private bool m_hasInitialized;

    private void Awake()
    {
        m_manager = RoomTurnManager.Instance;

        if (m_commitTrigger == null)
        {
            m_commitTrigger = GetComponent<Collider>();
        }

        if (m_commitTrigger == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 未指定提交范围，也没有本物体上的 Collider；这个门不会工作。", this);
        }
    }

    private void Update()
    {
        // 场景里的门可能比 RoomTurnManager 更早 Awake，这里补一次查找
        if (m_manager == null)
        {
            m_manager = RoomTurnManager.Instance;
        }

        if (m_manager == null || m_manager.Player == null || m_commitTrigger == null || m_targetRoom == null)
        {
            return;
        }

        Vector3 playerPosition = m_manager.Player.transform.position;
        bool insideCommit = IsInside(m_commitTrigger, playerPosition);

        // 房间被激活时玩家可能已经站在门里（刚转场落地就是这样）：
        // 第一帧只记录状态，不触发，避免原地来回转
        if (!m_hasInitialized)
        {
            m_hasInitialized = true;
            m_wasInsideCommit = insideCommit;
            return;
        }

        if (insideCommit && !m_wasInsideCommit)
        {
            m_manager.RequestTurn(m_targetRoom, m_turnAngleInDegrees, m_exitPointName);
        }

        m_wasInsideCommit = insideCommit;
    }

    private static bool IsInside(Collider volume, Vector3 point)
    {
        if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy)
        {
            return false;
        }

        // 点在碰撞体内部时 ClosestPoint 返回自身；这样对旋转过的门也准确
        return (volume.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }
}
