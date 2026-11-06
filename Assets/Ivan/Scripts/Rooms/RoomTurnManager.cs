using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 田字转台换景：所有房间 PrefabInstance 都直接放在场景里（作为 DioramaRoot 的子物体），
// 转场时只做 SetActive + 旋转——不加载/卸载任何资产，房间状态（解谜进度、拾取/放置物品）
// 在切场之间完整保留。
//
// 层级约定（很重要）：
//   DioramaRoot（会绕 Y 轴旋转）：原点 = 田字中心；下面直接挂所有房间 PrefabInstance
//     Room_A / Room_B / Room_C / Room_D（都是 DioramaRoot 的直接子物体，localScale = 1）
//       架子、Slot、DongbaGlyph 等场景内容
//   Environment（不转）：相机、平行光、天空、雨雪、EnvironmentManager、UI —— 散在场景根也行
//   玩家：必须留在 DioramaRoot 之外。转台转完玩家原地不动，正好落在新房间门口，
//         再往里走一小步。若把玩家挂到 DioramaRoot 下，他会跟着旧房间被转走。
//
// 房间布局：4 个房间绕田心分布，每转 90° 一个。每个房间都已经摆好在自己的槽位上。
// 转场流程：门 → 下沉玩家 → 旋转 DioramaRoot → 旧房间失活 + 新房间激活 → 上升玩家
public class RoomTurnManager : MonoBehaviour
{
    private static RoomTurnManager s_instance;

    [Header("场景结构")]
    [Tooltip("会旋转的根物体；原点必须放在田字中心")]
    [SerializeField] private Transform m_dioramaRoot;

    [Tooltip("所有房间 PrefabInstance（DioramaRoot 的直接子物体）。顺序就是它们在场景里的顺序，" +
             "第一个必须是当前台口的房间（初始激活的那个）")]
    [SerializeField] private List<Transform> m_rooms = new List<Transform>();

    [Tooltip("玩家；留空时自动查找。必须与 DioramaRoot 同级，不要挂在它下面")]
    [SerializeField] private PlayerEntity m_player;

    [Header("转台")]
    [Tooltip("旋转 90° 的时长（秒）")]
    [SerializeField] private float m_turnDurationInSeconds = 0.75f;

    [Tooltip("旋转缓动")]
    [SerializeField] private Ease m_turnEase = Ease.InOutCubic;

    [Header("落位")]
    [Tooltip("转完后玩家从门口往房间中心方向自动走的距离（米）；默认 0 = 原地落位只复位朝向")]
    [SerializeField] private float m_entryWalkDistanceInMeters = 0f;

    [Tooltip("自动走这一小段的时长（秒）")]
    [SerializeField] private float m_entryWalkDurationInSeconds = 0.45f;

    [Header("转场过场")]
    [Tooltip("玩家下沉到画面外的深度（米，Y 轴向下）")]
    [SerializeField] private float m_sinkDepthInMeters = 3f;

    [Tooltip("玩家下沉/冒出的时长（秒）")]
    [SerializeField] private float m_sinkDurationInSeconds = 0.2f;

    private int m_currentRoomIndex;
    private float m_currentYawInDegrees;
    private bool m_isTurning;

    public static RoomTurnManager Instance => s_instance;

    public PlayerEntity Player => m_player;

    public bool IsTurning => m_isTurning;

    public bool CanAcceptTurn => !m_isTurning && m_player != null && !m_player.IsDead;

    // 留给音效/震屏等外部系统挂接
    public event Action RoomTurnStarted;
    public event Action RoomTurnCompleted;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning($"[{GetType().Name}] 场景里已经有转台管理器，这个会被移除。", this);
            Destroy(this);
            return;
        }

        s_instance = this;

        if (m_dioramaRoot == null)
        {
            Debug.LogError($"[{GetType().Name}] 未指定 DioramaRoot，转台无法工作。", this);
            return;
        }

        if (m_rooms.Count == 0)
        {
            Debug.LogError($"[{GetType().Name}] 房间列表为空，请把所有 Room PrefabInstance 拖进来。", this);
            return;
        }

        if (m_player == null)
        {
            m_player = FindFirstObjectByType<PlayerEntity>();
        }

        if (m_player != null && m_player.transform.IsChildOf(m_dioramaRoot))
        {
            Debug.LogWarning($"[{GetType().Name}] 玩家挂在 DioramaRoot 下面了：转台会把玩家一起转走，请把玩家移到 DioramaRoot 之外。", this);
        }

        // 确保只有第一个房间激活，其余都隐藏——它们已经在场景里了
        for (int i = 0; i < m_rooms.Count; i++)
        {
            if (m_rooms[i] != null)
            {
                m_rooms[i].gameObject.SetActive(i == 0);
            }
        }

        m_currentRoomIndex = 0;
        m_currentYawInDegrees = m_dioramaRoot.eulerAngles.y;

        // 给玩家注入初始房间的可活动边界
        InjectBoundsZone(m_rooms[0]);
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    #region 公开接口

    // 玩家走进门时调用：激活目标房间并转动转台。
    // 旋转角度由 RoomTurnManager 根据目标房间在 DioramaRoot 下的方位自动算出——
    // 不需要在 DoorTurnTrigger 里手填 turnAngleInDegrees 了。
    // exitPointName：目标房间里玩家从哪个点出来（留空则保留玩家当前 XZ）
    public void RequestTurn(Transform targetRoom, float turnAngleInDegrees, string exitPointName = null)
    {
        if (!CanAcceptTurn || targetRoom == null)
        {
            return;
        }

        // 根据目标房间的 local position 自动算出正确的旋转角度，
        // 忽略手填的 turnAngleInDegrees——它很容易配错方向
        float autoAngle = ComputeTurnAngle(targetRoom);
        _ = TurnAsync(targetRoom, autoAngle, exitPointName);
    }

    // 根据目标房间在 DioramaRoot 下的 local position 算出
    // 让它转到台口（相机正前方，默认 +Z 方向）需要的增量旋转角度。
    // 算法：Atan2(localX, localZ) 得到目标房间当前的方位角，
    // 再减去 DioramaRoot 的当前 yaw，就是增量。
    private float ComputeTurnAngle(Transform targetRoom)
    {
        if (m_dioramaRoot == null)
        {
            return 0f;
        }

        // 目标房间相对于 DioramaRoot 的方位角（XZ 平面）
        Vector3 localPos = targetRoom.localPosition;
        float bearingDegrees = Mathf.Atan2(localPos.x, localPos.z) * Mathf.Rad2Deg;

        // 让目标房间从"方位角 bearing"转到"台口（0°）"，
        // DioramaRoot 需要转 -bearing 度
        float desiredYaw = -bearingDegrees;

        // 增量 = 目标 yaw - 当前 yaw，归一化到 (-180, 180] 走最短路径
        float delta = desiredYaw - m_currentYawInDegrees;
        while (delta > 180f)
        {
            delta -= 360f;
        }

        while (delta <= -180f)
        {
            delta += 360f;
        }

        return delta;
    }

    // 玩家靠近门时调用：目标房间已经在场景里了，不需要预加载——
    // 这个方法留空，保留接口兼容性，门那边可以选择不调
    public void RequestPreload(Transform targetRoom, float turnAngleInDegrees)
    {
        // 所有房间常驻，不需要预加载
    }

    // 当前台口的房间 Transform
    public Transform CurrentRoom => m_rooms.Count > 0 ? m_rooms[m_currentRoomIndex] : null;

    // 根据 Transform 查找房间索引
    public int FindRoomIndex(Transform room)
    {
        return m_rooms.IndexOf(room);
    }

    #endregion

    private async Awaitable TurnAsync(Transform nextRoom, float turnAngleInDegrees, string exitPointName)
    {
        m_isTurning = true;

        if (m_player != null)
        {
            m_player.BeginRoomTurn();
        }

        // 确认目标房间在列表里
        int nextIndex = m_rooms.IndexOf(nextRoom);
        if (nextIndex < 0)
        {
            Debug.LogError($"[{GetType().Name}] 目标房间 {nextRoom.name} 不在 m_rooms 列表里。", this);
            m_isTurning = false;

            if (m_player != null)
            {
                m_player.CancelRoomTurn();
            }

            return;
        }

        // 目标房间就是当前房间？不用转
        if (nextIndex == m_currentRoomIndex)
        {
            m_isTurning = false;

            if (m_player != null)
            {
                m_player.CancelRoomTurn();
            }

            return;
        }

        float sinkY = m_player.transform.position.y - m_sinkDepthInMeters;

        // 1) 先把玩家沉到地下：此时只有旧房间在画面里，没有重叠，也不存在玩家相对场景横向漂移
        Tween sinkTween = m_player.transform.DOMoveY(sinkY, m_sinkDurationInSeconds)
            .SetEase(Ease.InSine);
        await sinkTween.AsyncWaitForCompletion();

        if (this == null)
        {
            return;
        }

        // 2) 玩家已经藏好，激活目标房间并开始转台
        nextRoom.gameObject.SetActive(true);
        OnRoomTurnStarted();

        Tween turnTween = m_dioramaRoot
            .DORotate(new Vector3(0f, turnAngleInDegrees, 0f), m_turnDurationInSeconds, RotateMode.WorldAxisAdd)
            .SetEase(m_turnEase);

        await turnTween.AsyncWaitForCompletion();

        if (this == null)
        {
            return;
        }

        m_currentYawInDegrees = NormalizeAngle(m_dioramaRoot.eulerAngles.y);

        // 3) 旧房间退场：失活即可，它还在场景里，状态完整保留
        Transform currentRoom = m_rooms[m_currentRoomIndex];
        if (currentRoom != null && currentRoom != nextRoom)
        {
            currentRoom.gameObject.SetActive(false);
        }

        m_currentRoomIndex = nextIndex;

        // 给玩家注入新房间的可活动边界——此时新房间已在台口、坐标正确
        InjectBoundsZone(nextRoom);

        // 4) 转盘停稳后再读出口点的世界坐标：此时目标房间已经在台口，坐标才是对的
        Vector3 entryPosition = ResolveEntryPosition(nextRoom, exitPointName);

        // 5) 把玩家放到出口点的地下位置，然后冒出来
        if (m_player != null)
        {
            entryPosition.y = sinkY;
            m_player.transform.position = entryPosition;

            float surfaceY = entryPosition.y + m_sinkDepthInMeters;
            Tween riseTween = m_player.transform.DOMoveY(surfaceY, m_sinkDurationInSeconds)
                .SetEase(Ease.OutSine);
            await riseTween.AsyncWaitForCompletion();

            if (this == null)
            {
                return;
            }
        }

        WalkPlayerIntoRoom(nextRoom);

        if (m_entryWalkDurationInSeconds > 0f && m_entryWalkDistanceInMeters > 0f)
        {
            await Awaitable.WaitForSecondsAsync(m_entryWalkDurationInSeconds);
        }

        if (this == null)
        {
            return;
        }

        m_isTurning = false;
        OnRoomTurnCompleted();
    }

    // 从目标房间里找到出口点；找不到就保留玩家当前的 XZ，只把 Y 拉回地面
    private Vector3 ResolveEntryPosition(Transform room, string exitPointName)
    {
        if (m_player == null)
        {
            return Vector3.zero;
        }

        if (!string.IsNullOrEmpty(exitPointName))
        {
            Transform exit = room.Find(exitPointName);
            if (exit != null)
            {
                return exit.position;
            }
        }

        return m_player.transform.position;
    }

    private void WalkPlayerIntoRoom(Transform room)
    {
        if (m_player == null)
        {
            return;
        }

        // 玩家不在 DioramaRoot 下，转台转完他原地不动，正好落在新房间门口。
        // 这里始终调用 CompleteRoomTurn：即使不走位（距离为 0），也要让它复位朝向并回到 Normal，
        // 否则状态机会一直卡在 RoomTurnState。
        Vector3 target = m_player.transform.position;

        if (m_entryWalkDistanceInMeters > 0f)
        {
            // 朝房间中心走一小步。注意 room.position 是房间 pivot 的世界坐标，
            // 未必在房间正中心，所以这个值默认是 0——需要时再手动调大
            Vector3 inward = room.position - m_player.transform.position;
            inward.y = 0f;

            if (inward.sqrMagnitude > 0.0001f)
            {
                target += inward.normalized * m_entryWalkDistanceInMeters;
            }
        }

        m_player.CompleteRoomTurn(target, m_entryWalkDurationInSeconds);
    }

    // 给玩家注入指定房间的可活动边界。
    // 约定：每个房间里有一个叫 "MoveBounds" 的子物体，上面挂着这个房间的边界 Collider 组。
    private void InjectBoundsZone(Transform room)
    {
        if (m_player == null || room == null)
        {
            return;
        }

        Transform boundsTransform = room.Find("MoveBounds");
        if (boundsTransform == null)
        {
            // 房间里没放 MoveBounds 子物体——玩家继续用上一个边界或回退到方形 AABB
            return;
        }

        m_player.SetBoundsZone(boundsTransform.gameObject);
    }

    private void OnRoomTurnStarted()
    {
        RoomTurnStarted?.Invoke();
    }

    private void OnRoomTurnCompleted()
    {
        RoomTurnCompleted?.Invoke();
    }

    // 把角度归一化到 (-180, 180]，避免 DORotate 累加后出现 360+ 的大值
    private static float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle <= -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
