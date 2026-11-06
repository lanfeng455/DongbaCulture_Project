using UnityEditor;
using UnityEngine;

namespace Ivan.Diorama
{
    // WalkableArea 的 Scene 视图拖点编辑器：
    // 选中挂有 WalkableArea 的物体后，每个顶点出现一个移动手柄，拖动即改边界（支持 Undo）。
    [CustomEditor(typeof(WalkableArea))]
    public class WalkableAreaEditor : Editor
    {
        private const float k_handleHeight = 0.1f;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var area = (WalkableArea)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("在每段中点插入顶点（加密边界）"))
            {
                Densify(area);
            }
            if (GUILayout.Button("从地面重置为矩形"))
            {
                ResetToGroundRect(area);
            }
        }

        private void OnSceneGUI()
        {
            var area = (WalkableArea)target;
            int n = area.PointCount;
            if (n < 2) return;

            // 闭合折线，选中时看得更清楚
            Handles.color = new Color(0.2f, 0.9f, 0.4f);
            var loop = new Vector3[n + 1];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = area.GetPoint(i);
                loop[i] = new Vector3(p.x, k_handleHeight, p.y);
            }
            loop[n] = loop[0];
            Handles.DrawAAPolyLine(3f, loop);

            for (int i = 0; i < n; i++)
            {
                Vector2 p = area.GetPoint(i);
                Vector3 world = new Vector3(p.x, k_handleHeight, p.y);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (!EditorGUI.EndChangeCheck()) continue;

                Undo.RecordObject(area, "Move Walkable Area Point");
                // 只允许在地面（XZ 平面）内拖动，高度固定
                area.SetPoint(i, new Vector2(moved.x, moved.z));
                EditorUtility.SetDirty(area);
            }
        }

        // 相邻两点间插入中点，方便把粗轮廓逐步修成不规则形状
        private static void Densify(WalkableArea area)
        {
            int n = area.PointCount;
            if (n < 2) return;

            Undo.RecordObject(area, "Densify Walkable Area");
            var points = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = area.GetPoint(i);
                Vector2 b = area.GetPoint((i + 1) % n);
                points[i * 2] = a;
                points[i * 2 + 1] = (a + b) * 0.5f;
            }
            area.SetPoints(points);
            EditorUtility.SetDirty(area);
        }

        private static void ResetToGroundRect(WalkableArea area)
        {
            var ground = GameObject.Find("Stationary paper ground");
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                Debug.LogWarning("[WalkableArea] 场景里找不到 Stationary paper ground，无法重置。", area);
                return;
            }

            Undo.RecordObject(area, "Reset Walkable Area");
            Bounds b = renderer.bounds;
            area.SetPoints(new[]
            {
                new Vector2(b.min.x, b.min.z),
                new Vector2(b.max.x, b.min.z),
                new Vector2(b.max.x, b.max.z),
                new Vector2(b.min.x, b.max.z),
            });
            EditorUtility.SetDirty(area);
        }
    }
}
