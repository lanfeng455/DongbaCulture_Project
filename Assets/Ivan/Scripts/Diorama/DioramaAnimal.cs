using DG.Tweening;
using UnityEngine;

namespace Ivan.Diorama
{
    // 组员原型动物的轻量运行时替身：
    //   漫游（组员 AnimalEcology 漫游态的简化：随机选点、走过去、歇一会儿）
    //   喂食（E）：原地跳一下 + 微微长大
    //   层级（组员 Line 140）：每帧按 z 坐标重算 sortingOrder
    public class DioramaAnimal : MonoBehaviour
    {
        private const float k_maxGrowScale = 1.25f;
        private const float k_roamRadius = 3f;      // 以出生点为中心的漫游半径
        private const float k_arriveDistance = 0.2f;

        [Tooltip("漫游移动速度（米/秒）")]
        [SerializeField] private float m_moveSpeed = 1.2f;
        [Tooltip("是否按地面实际尺寸自动推导活动边界")]
        [SerializeField] private bool m_deriveBoundsFromGround = true;
        [Tooltip("活动范围半宽/半深（仅在未勾选自动推导时使用）")]
        [SerializeField] private float m_halfWidth = 46f;
        [SerializeField] private float m_halfDepth = 21f;

        private Transform m_art;                    // 字形（带 SpriteRenderer 的 Billboard 子物体）
        private SpriteRenderer m_renderer;
        private Vector3 m_baseScale;
        private float m_baseLocalY;
        private int m_feedCount;
        private Vector3 m_boundCenter = Vector3.zero;

        private Vector3 m_home;                     // 出生点，漫游围绕它
        private Vector3 m_goal;
        private float m_waitUntil;
        private float m_hopPhase;                   // 走路小跳动的相位

        private void Awake()
        {
            m_renderer = GetComponentInChildren<SpriteRenderer>();
            if (m_renderer != null)
            {
                m_art = m_renderer.transform;
                m_baseScale = m_art.localScale;
                m_baseLocalY = m_art.localPosition.y;
            }

            if (m_deriveBoundsFromGround)
            {
                var ground = GameObject.Find("Stationary paper ground");
                if (ground != null)
                {
                    var groundRenderer = ground.GetComponent<Renderer>();
                    if (groundRenderer != null)
                    {
                        var bounds = groundRenderer.bounds;
                        m_boundCenter = new Vector3(bounds.center.x, 0f, bounds.center.z);
                        m_halfWidth = Mathf.Max(0.1f, bounds.extents.x - 0.5f);
                        m_halfDepth = Mathf.Max(0.1f, bounds.extents.z - 0.5f);
                    }
                }
            }

            m_home = transform.position;
            m_goal = m_home;
            m_waitUntil = Time.time + Random.Range(0.5f, 3f);   // 错开各动物起步时间
            m_hopPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (Time.time >= m_waitUntil)
            {
                Vector3 toGoal = m_goal - transform.position;
                toGoal.y = 0f;

                if (toGoal.magnitude < k_arriveDistance)
                {
                    // 到达：歇 1.5~4 秒，选下一个点
                    m_waitUntil = Time.time + Random.Range(1.5f, 4f);
                    m_goal = PickNewGoal();
                }
                else
                {
                    // 朝目标走
                    Vector3 dir = toGoal.normalized;
                    Vector3 next = transform.position + dir * (m_moveSpeed * dt);
                    if (IsFree(next))
                    {
                        transform.position = next;
                    }
                    else
                    {
                        // 撞界/撞河：立刻换目标
                        m_goal = PickNewGoal();
                    }

                    // 字形朝向移动方向
                    if (m_renderer != null && dir.x != 0f)
                    {
                        m_renderer.flipX = dir.x < 0f;
                    }

                    // 走路小跳动（组员动物 hop 的简化）
                    if (m_art != null)
                    {
                        m_hopPhase += dt * 8f;
                        var lp = m_art.localPosition;
                        lp.y = m_baseLocalY + Mathf.Abs(Mathf.Sin(m_hopPhase)) * 0.08f;
                        m_art.localPosition = lp;
                    }
                }
            }

            // 组员层级公式：动物在动，每帧按 z 重算（Line 140）
            if (m_renderer != null)
            {
                m_renderer.sortingOrder = DioramaRiver.SortingOrderFor(transform.position);
            }
        }

        // 在出生点周围随机选一个能站的点（避河、不越界）
        private Vector3 PickNewGoal()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 circle = Random.insideUnitCircle * k_roamRadius;
                var candidate = m_home + new Vector3(circle.x, 0f, circle.y);
                if (IsFree(candidate))
                {
                    return candidate;
                }
            }
            return m_home;
        }

        private bool IsFree(Vector3 p)
        {
            float dx = Mathf.Abs(p.x - m_boundCenter.x);
            float dz = Mathf.Abs(p.z - m_boundCenter.z);
            if (dx > m_halfWidth || dz > m_halfDepth) return false;
            if (DioramaRiver.InRiver(p)) return false;   // 动物不下河
            return true;
        }

        // E 喂食：原地跳一下 + 微微长大（组员 Care 的简化复刻）
        public void Feed()
        {
            if (m_art == null) return;

            m_feedCount++;
            m_art.DOKill();

            // 跳跃：上去 0.22s，落回 0.22s
            m_art.DOLocalMoveY(m_baseLocalY + 0.4f, 0.22f).SetEase(Ease.OutQuad)
                .OnComplete(() => m_art.DOLocalMoveY(m_baseLocalY, 0.22f).SetEase(Ease.InQuad));

            // 每次喂食长大 5%，封顶 1.25 倍
            float grow = Mathf.Min(1f + m_feedCount * 0.05f, k_maxGrowScale);
            m_art.DOScale(m_baseScale * grow, 0.3f).SetEase(Ease.OutBack);
        }
    }
}
