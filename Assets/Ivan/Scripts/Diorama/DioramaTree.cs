using DG.Tweening;
using UnityEngine;

namespace Ivan.Diorama
{
    // 组员原型树的轻量运行时替身。
    // 砍倒流程复刻组员（ObliqueForest.cs Line 138 + UpdateTreeLeaves Line 297-303）：
    //   整树以根为轴倒下 → 叶子 fade out 消失 → 树干压扁成为树桩（E 可补种恢复）。
    // 层级按组员公式 100 - z*10 在 Awake 时设置（树位置固定，设一次即可）。
    public class DioramaTree : MonoBehaviour
    {
        private const int k_maxHealth = 3;
        private const float k_stumpScaleY = 0.35f;

        [Tooltip("剩余可砍次数，归零后倒下成为树桩")]
        [SerializeField] private int m_health = k_maxHealth;

        private Transform m_art;                    // 树干字形（第一个 SpriteRenderer）
        private SpriteRenderer m_trunkRenderer;
        private SpriteRenderer[] m_leafRenderers;   // 其余字形（树冠各层）
        private float[] m_leafAlphas;               // 叶子原始 alpha，补种恢复用
        private Vector3 m_trunkBaseScale;
        private bool m_isStump;
        private DioramaObstacle m_obstacle;         // 通用障碍组件：物理移动的动态阻挡

        public bool IsStump => m_isStump;

        private void Awake()
        {
            var srs = GetComponentsInChildren<SpriteRenderer>();
            if (srs.Length == 0) return;

            m_trunkRenderer = srs[0];
            m_art = srs[0].transform;
            m_trunkBaseScale = m_art.localScale;

            m_leafRenderers = new SpriteRenderer[srs.Length - 1];
            m_leafAlphas = new float[srs.Length - 1];
            for (int i = 1; i < srs.Length; i++)
            {
                m_leafRenderers[i - 1] = srs[i];
                m_leafAlphas[i - 1] = srs[i].color.a;
            }

            // 组员层级公式：100 - z*10（Line 140），树静止只设一次
            int order = DioramaRiver.SortingOrderFor(transform.position);
            foreach (var sr in srs)
            {
                sr.sortingOrder = order;
            }

            // 通用障碍组件负责碰撞体；树只负责倒下/成桩/补种时切换它的状态
            m_obstacle = GetComponent<DioramaObstacle>();
            if (m_obstacle == null)
            {
                m_obstacle = gameObject.AddComponent<DioramaObstacle>();
            }
            m_obstacle.EnsureCollider();
        }

        // 挥斧命中：扣血并回弹，归零则倒下
        public void ApplyHit(Vector3 hitDirection)
        {
            if (m_isStump || m_art == null) return;

            m_health--;

            if (m_health <= 0)
            {
                Fall(hitDirection);
            }
            else
            {
                // 受击回弹（Punch 自动回到原值，不破坏 Billboard 角度）
                m_art.DOPunchScale(Vector3.one * 0.08f, 0.2f, 6, 0.5f);
            }
        }

        // 倒下：整树以根为轴侧倒，叶子同步 fade out（组员 cutFade 的简化）
        private void Fall(Vector3 hitDirection)
        {
            m_isStump = true;

            // 倒下过程不再挡路，否则玩家会被空气墙卡住
            if (m_obstacle != null)
            {
                m_obstacle.SetEnabled(false);
            }

            float side = hitDirection.x >= 0f ? 1f : -1f;
            transform.DOLocalRotate(new Vector3(0f, 0f, side * 78f), 0.55f)
                .SetEase(Ease.InQuad)
                .OnComplete(BecomeStump);

            foreach (var leaf in m_leafRenderers)
            {
                if (leaf != null)
                {
                    leaf.DOFade(0f, 0.5f);
                }
            }
        }

        // 倒地完成后变桩：恢复直立，树干压扁，叶子隐藏
        private void BecomeStump()
        {
            transform.localRotation = Quaternion.identity;

            if (m_art != null)
            {
                m_art.localScale = new Vector3(
                    m_trunkBaseScale.x,
                    m_trunkBaseScale.y * k_stumpScaleY,
                    m_trunkBaseScale.z);
            }

            foreach (var leaf in m_leafRenderers)
            {
                if (leaf != null)
                {
                    leaf.enabled = false;
                }
            }

            // 树桩保留一个矮小碰撞体：仍然挡路，但贴地
            if (m_obstacle != null)
            {
                m_obstacle.SetCapsuleSize(0.25f, 0.5f, new Vector3(0f, 0.25f, 0f));
                m_obstacle.SetEnabled(true);
            }
        }

        // E 补种：树桩恢复直立满血，叶子重新长出
        public void Replant()
        {
            if (!m_isStump || m_art == null) return;

            m_isStump = false;
            m_health = k_maxHealth;

            // 恢复整树碰撞体
            if (m_obstacle != null)
            {
                m_obstacle.ResetSize();
                m_obstacle.SetEnabled(true);
            }

            m_art.DOKill();
            m_art.localScale = m_trunkBaseScale;

            for (int i = 0; i < m_leafRenderers.Length; i++)
            {
                var leaf = m_leafRenderers[i];
                if (leaf == null) continue;

                leaf.enabled = true;
                var color = leaf.color;
                color.a = 0f;
                leaf.color = color;
                leaf.DOFade(m_leafAlphas[i], 0.4f);
            }
        }
    }
}
