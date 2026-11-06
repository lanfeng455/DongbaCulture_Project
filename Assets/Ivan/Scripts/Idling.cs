using DG.Tweening;
using UnityEngine;


public class Idling : MonoBehaviour
{
    [Tooltip("Peak height gain relative to the resting scale (0.05 = +5%)")]
    [SerializeField] private float m_idleAmount = 0.05f;

    [Tooltip("Duration of one full idle cycle in seconds")]
    [SerializeField] private float m_cycleDurationInSeconds = 2.6f;

    private Vector3 m_baseLocalPosition;
    private Tween m_idleTween;

    private void Awake()
    {
        // 用局部坐标缓存基准位置：房间可能被实例化到任意位置/朝向（转台换景），
        // 世界坐标会在 Awake 时还是错的，局部坐标才能保证 idle 始终围绕预制体里摆好的位置摆动
        m_baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        StartIdle();
    }

    private void OnDisable()
    {
        // Never leave a tween running on a disabled or destroyed object
        m_idleTween?.Kill();
        m_idleTween = null;
    }

    private void StartIdle()
    {
        transform.localPosition = m_baseLocalPosition;
        // 计算y轴水平面上45度方向的单位向量
        Vector3 dir45 = new Vector3(-Mathf.Sin(45f * Mathf.Deg2Rad), 0f, -Mathf.Cos(45f * Mathf.Deg2Rad)).normalized;
        // 目标位置为基础位置沿45度方向偏移idleAmount的距离（局部空间）
        Vector3 targetPos = m_baseLocalPosition + dir45 * m_idleAmount;
        // 以半周期时长移动到目标位置，使用Yoyo循环实现无限来回运动
        m_idleTween = transform.DOLocalMove(targetPos, m_cycleDurationInSeconds / 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
}
