using System.Collections.Generic;
using UnityEngine;

namespace Ivan.Puzzle
{
    // 视线遮挡物：登记到一个静态表里，供 SightLineSensor 做「连线是否被挡住」判定。
    // 默认不挂：只有需要参与视线谜题的东西才挂（可推动的石头），
    // 否则整片树林都会去挡线，谜题就没法玩了。
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Sight Blocker")]
    public class SightBlocker : MonoBehaviour
    {
        private static readonly List<SightBlocker> s_all = new(16);

        public static IReadOnlyList<SightBlocker> All => s_all;

        [Tooltip("遮挡半径手动覆盖（米），0 = 自动按精灵宽度推导")]
        [SerializeField] private float m_radiusOverride = 0f;

        [Tooltip("自动推导时取渲染半宽的比例。0.85 约等于「视觉上这块石头占的宽度」")]
        [SerializeField] private float m_footprintScale = 0.85f;

        [Tooltip("自动推导半径上限（米），防止异常大的精灵吃掉整条视线")]
        [SerializeField] private float m_maxRadius = 5f;

        private float m_cachedRadius = -1f;

        // 视线判定只在 XZ 平面进行，Y 一律归零
        public Vector3 Center
        {
            get
            {
                Vector3 p = transform.position;
                return new Vector3(p.x, 0f, p.z);
            }
        }

        public float Radius
        {
            get
            {
                if (m_cachedRadius < 0f)
                {
                    m_cachedRadius = ComputeRadius();
                }
                return m_cachedRadius;
            }
        }

        private void OnEnable()
        {
            m_cachedRadius = -1f;
            if (!s_all.Contains(this))
            {
                s_all.Add(this);
            }
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        // 石头体积被改动后调用，让半径重新推导
        public void RefreshRadius()
        {
            m_cachedRadius = -1f;
        }

        private float ComputeRadius()
        {
            if (m_radiusOverride > 0f)
            {
                return m_radiusOverride;
            }

            // 视线遮挡看的是「画面上这块石头有多宽」，所以按精灵渲染宽度推导，
            // 而不是物理占位半径（后者通常比视觉小得多）
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 0.4f;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            return Mathf.Clamp(bounds.extents.x * m_footprintScale, 0.1f, m_maxRadius);
        }
    }
}
