using UnityEngine;

namespace Ivan.Diorama
{
    // 可行走区域：一串世界坐标顶点围成的任意多边形，用来替代矩形边界贴合不规则地形。
    // 顶点用扁平 Vector2 存储（x = 世界 X，y = 世界 Z），选中本物体即可在 Scene 视图拖点编辑。
    //
    // 为什么不用 PolygonCollider2D：Unity 的 2D 物理锁定在世界 XY 平面，
    // 而本场景地面是 XZ 平面，硬用需要坐标换算且场景里无法直观描边，不如直接做点在多边形内判定。
    public class WalkableArea : MonoBehaviour
    {
        [Tooltip("多边形顶点（x = 世界 X，y = 世界 Z），顺/逆时针均可；少于 3 个点视为不限制移动")]
        [SerializeField] private Vector2[] m_points = new Vector2[0];

        public int PointCount => m_points.Length;

        public Vector2 GetPoint(int index)
        {
            return m_points[index];
        }

        public void SetPoint(int index, Vector2 value)
        {
            m_points[index] = value;
        }

        public void SetPoints(Vector2[] points)
        {
            m_points = points;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return Contains(new Vector2(worldPosition.x, worldPosition.z));
        }

        // 射线法：从测试点向 +x 方向发射线，与奇数条边相交即在多边形内
        public bool Contains(Vector2 p)
        {
            int n = m_points.Length;
            if (n < 3) return true; // 没画完多边形时不限制，避免把玩家卡死

            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = m_points[i];
                Vector2 b = m_points[j];

                // straddles 为 false 时必不发生除法，水平边天然被跳过
                bool straddles = (a.y > p.y) != (b.y > p.y);
                if (straddles && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private void OnDrawGizmos()
        {
            int n = m_points.Length;
            if (n < 2) return;

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = m_points[i];
                Vector2 b = m_points[(i + 1) % n];
                Gizmos.DrawLine(new Vector3(a.x, 0.1f, a.y), new Vector3(b.x, 0.1f, b.y));
                Gizmos.DrawSphere(new Vector3(a.x, 0.1f, a.y), 0.15f);
            }
        }
    }
}
