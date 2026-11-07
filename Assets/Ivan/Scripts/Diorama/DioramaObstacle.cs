using System;
using UnityEngine;

namespace Ivan.Diorama
{
    // 通用静态障碍物（2D 卷轴场景版）：挂到任何带 SpriteRenderer 的物体上
    // （石头、木牌、栅栏、门……），Awake 时自动补一个物理碰撞体挡住玩家。
    //
    // 本场景移动是纯平面（XZ）的，真正起阻挡作用的只有"俯视占位"：
    //   - Capsule：圆形占位（石头、树、木桩……）
    //   - Box：矩形占位（栅栏、墙、门……）
    //
    // 锚点说明：卷轴精灵是朝相机倾斜的广告牌，且轴心多在画面中心，
    // 所以不能拿 transform 当立足点。统一从渲染包围盒推导：
    // 底边中点 (center.x, min.y, min.z) 即"贴地处"，与轴心位置无关。
    // 碰撞体放在该处一个无旋转的子物体上，避免随精灵倾斜而歪掉。
    //
    // 动态障碍（可解锁的门等）：拿到本组件调 SetEnabled(false) 即放行，玩家代码不用改。
    // 树等需要改碰撞尺寸的：SetCapsuleSize() 临时改、ResetSize() 恢复。
    [DisallowMultipleComponent]
    [AddComponentMenu("Diorama/Diorama Obstacle")]
    public class DioramaObstacle : MonoBehaviour
    {
        private const string k_anchorName = "Obstacle Footprint";

        public enum ColliderShape
        {
            Capsule,    // 圆形占位：石头、树、桩
            Box,        // 矩形占位：栅栏、墙、门
        }

        public event System.Action OnBeingPushed;
        public event System.Action OnFinishPushed;
        
        private void OnCollisionEnter(Collision collision)
        {
            OnBeingPushed?.Invoke();
        }

        private void OnCollisionExit(Collision collision)
        {
            OnFinishPushed?.Invoke();
        }

        [Tooltip("碰撞体形状：Capsule 圆形占位（石头等），Box 矩形占位（栅栏、门等）")]
        [SerializeField] private ColliderShape m_shape = ColliderShape.Capsule;

        [Tooltip("占位宽度相对精灵渲染宽度的比例。视觉图通常比实际可占地面大，默认 0.35")]
        [SerializeField] private float m_footprintScale = 0.35f;

        [Tooltip("在自动推导的占位基础上再整体缩放；调大则更难靠近")]
        [SerializeField] private float m_sizeScale = 1f;

        [Tooltip("立足点推导结果的额外偏移（世界方向）。默认 (0,0,0)，特殊精灵再微调")]
        [SerializeField] private Vector3 m_centerOffset = Vector3.zero;

        [Tooltip("占位半径手动覆盖（米），0 = 自动推导。推导不理想时直接指定")]
        [SerializeField] private float m_radiusOverride = 0f;

        [Tooltip("自动推导半径的上限（米）。超过说明精灵四边形远大于可见图案，" +
                 "会截断并警告；之后请移除本组件（装饰物）或用 Radius Override 指定")]
        [SerializeField] private float m_maxAutoRadius = 5f;

        private Collider m_collider;
        private float m_baseRadius;     // 自动推导时的原始尺寸，供临时改尺寸后恢复
        private float m_baseHeight;
        private Vector3 m_baseCenter;

        public Collider Collider => m_collider;

        private void Awake()
        {
            EnsureCollider();
        }

        // 幂等：已有碰撞体则直接使用，否则按 m_shape 创建并记录原始尺寸
        public void EnsureCollider()
        {
            // includeInactive：物体刚开始激活时子层级状态可能还没同步，避免重复建碰撞体
            m_collider = GetComponentInChildren<Collider>(true);
            if (m_collider != null)
            {
                CacheBaseSize();
                return;
            }

            m_collider = m_shape == ColliderShape.Box
                ? BuildBoxCollider()
                : BuildCapsuleCollider();
            CacheBaseSize();
        }

        // 开关阻挡（门锁、倒下的树……），不销毁碰撞体以便随时恢复
        public void SetEnabled(bool enabled)
        {
            if (m_collider != null)
            {
                m_collider.enabled = enabled;
            }
        }

        // 临时改胶囊尺寸（如树桩变矮）；ResetSize() 恢复
        public void SetCapsuleSize(float radius, float height, Vector3 center)
        {
            if (m_collider is CapsuleCollider capsule)
            {
                capsule.radius = radius;
                capsule.height = height;
                capsule.center = center;
            }
        }

        public void ResetSize()
        {
            if (m_collider is CapsuleCollider capsule)
            {
                capsule.radius = m_baseRadius;
                capsule.height = m_baseHeight;
                capsule.center = m_baseCenter;
            }
        }

        // 占位半径：取精灵渲染半宽的一定比例（绕 X 轴倾斜不影响宽度，可靠）
        private float ComputeFootprintRadius()
        {
            if (m_radiusOverride > 0f)
            {
                return m_radiusOverride;
            }
            Bounds b = ComputeWorldBounds();
            float radius = Mathf.Max(0.1f, b.extents.x * m_footprintScale * m_sizeScale);
            if (radius > m_maxAutoRadius)
            {
                // 墨迹字形素材的四边形可能比可见图案大几个数量级，绝不能照单全收
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"[DioramaObstacle] 「{GetPath(transform)}」推导出异常大的占位半径 " +
                                     $"{radius:F2} m，已截断为 {m_maxAutoRadius} m。该精灵四边形远大于可见墨迹；" +
                                     "装饰物请移除本组件，要挡路请用 Radius Override 指定半径。", transform);
                }
                radius = m_maxAutoRadius;
            }
            return radius;
        }

        // 精灵的"立足点"：包围盒底边中点。广告牌绕 X 轴倾斜时底边即贴地那条边，
        // 与轴心在画面中心还是底部无关，两种精灵都适用
        private Vector3 ComputeFootprintAnchor()
        {
            Bounds b = ComputeWorldBounds();
            return new Vector3(b.center.x, b.min.y, b.min.z) + m_centerOffset;
        }

        private Bounds ComputeWorldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // 没有渲染器时给 1m 默认占位，避免零尺寸碰撞体
                return new Bounds(transform.position, Vector3.one);
            }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + " / " + path;
            }
            return path;
        }

        // 碰撞体专用锚点子物体：位于立足点、无旋转，避免胶囊随精灵倾斜而歪掉
        private Transform GetOrCreateAnchor()
        {
            var existing = transform.Find(k_anchorName);
            if (existing != null)
            {
                return existing;
            }
            var anchor = new GameObject(k_anchorName).transform;
            anchor.SetParent(transform, false);
            anchor.position = ComputeFootprintAnchor();
            anchor.rotation = Quaternion.identity;
            return anchor;
        }

        private Collider BuildCapsuleCollider()
        {
            Transform anchor = GetOrCreateAnchor();
            var capsule = anchor.gameObject.AddComponent<CapsuleCollider>();
            // 世界单位半径换算成局部（锚点继承父级缩放）
            float scale = Mathf.Max(0.001f, anchor.lossyScale.x);
            capsule.radius = ComputeFootprintRadius() / scale;
            capsule.height = 1f;
            capsule.center = new Vector3(0f, 0.5f, 0f);
            return capsule;
        }

        private Collider BuildBoxCollider()
        {
            Transform anchor = GetOrCreateAnchor();
            var box = anchor.gameObject.AddComponent<BoxCollider>();
            float width = ComputeFootprintRadius() * 2f;
            // 竖立精灵的 z 尺寸被倾斜污染（含高度分量），深度改为按宽度推导
            float depth = Mathf.Max(0.3f, width * m_footprintScale);
            float scale = Mathf.Max(0.001f, anchor.lossyScale.x);
            box.size = new Vector3(width / scale, 1f, depth / scale);
            box.center = new Vector3(0f, 0.5f, 0f);
            return box;
        }

        private void CacheBaseSize()
        {
            if (m_collider is CapsuleCollider capsule)
            {
                m_baseRadius = capsule.radius;
                m_baseHeight = capsule.height;
                m_baseCenter = capsule.center;
            }
        }

        // 橙色线框 = 俯视占位。Edit Mode 画预测值，Play 后画实际碰撞体，
        // 用来核对碰撞体和画面是否对齐
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);

            if (m_collider is CapsuleCollider capsule)
            {
                float scale = Mathf.Max(0.001f, capsule.transform.lossyScale.x);
                DrawFootprintCircle(capsule.transform.TransformPoint(capsule.center),
                                    capsule.radius * scale);
                return;
            }
            if (m_collider is BoxCollider box)
            {
                Vector3 s = box.transform.lossyScale;
                DrawFootprintRect(box.transform.TransformPoint(box.center),
                                  box.size.x * s.x, box.size.z * s.z);
                return;
            }

            // 还没生成碰撞体（Edit Mode）：按当前参数画预测占位
            Vector3 anchor = ComputeFootprintAnchor();
            if (m_shape == ColliderShape.Box)
            {
                float width = ComputeFootprintRadius() * 2f;
                DrawFootprintRect(anchor, width, Mathf.Max(0.3f, width * m_footprintScale));
            }
            else
            {
                DrawFootprintCircle(anchor, ComputeFootprintRadius());
            }
        }

        private static void DrawFootprintCircle(Vector3 center, float radius)
        {
            const int k_segments = 32;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= k_segments; i++)
            {
                float angle = i * Mathf.PI * 2f / k_segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static void DrawFootprintRect(Vector3 center, float width, float depth)
        {
            Vector3 half = new Vector3(width * 0.5f, 0f, depth * 0.5f);
            Vector3 a = center + new Vector3(-half.x, 0f, -half.z);
            Vector3 b = center + new Vector3(half.x, 0f, -half.z);
            Vector3 c = center + new Vector3(half.x, 0f, half.z);
            Vector3 d = center + new Vector3(-half.x, 0f, half.z);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
