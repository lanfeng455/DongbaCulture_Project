using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ivan.Diorama
{
    // 批量落地工具：把选中物体按"渲染包围盒底边"吸附到地面，只动 Y，X/Z 保持不变。
    //
    // 选中规则（适配"容器父物体 + 若干悬浮子物体"的层级）：
    //   - 自身带渲染器的物体：连同子物体渲染器作为一个整体吸附；
    //   - 自身没有渲染器的容器：不下场，递归让每个子物体独立吸附。
    // 卷轴精灵的轴心常在画面中心（广告牌），手动把 y 改成 0 会让图案悬空或沉地，
    // 还得回头调 z。本工具按"可见底边贴地"对齐，一次处理任意多个选中物体，支持 Undo。
    public static class SnapToGroundTool
    {
        [MenuItem("Tools/Dongba/Snap Selection To Ground")]
        public static void SnapSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[SnapToGround] 请先在 Hierarchy 选中要落地的物体。");
                return;
            }

            float groundY = FindGroundY();
            var selected = new HashSet<GameObject>(selection);
            int moved = 0;

            foreach (var go in selection)
            {
                // 祖先已选中时跳过，避免被递归处理两次
                if (HasSelectedAncestor(go.transform, selected)) continue;
                SnapRecursive(go.transform, groundY, ref moved);
            }

            Debug.Log($"[SnapToGround] 已把 {moved} 个物体的底边吸附到 y = {groundY:F3}。" +
                      (moved == 0 ? "（选中物体及其子物体都不含渲染器，或已在地面高度）" : ""));
        }

        // 容器递归向下；遇到自身带渲染器的物体就作为一个整体吸附
        private static void SnapRecursive(Transform t, float groundY, ref int moved)
        {
            if (t.GetComponent<Renderer>() != null)
            {
                if (SnapSingle(t, groundY))
                {
                    moved++;
                }
                return;
            }
            foreach (Transform child in t)
            {
                SnapRecursive(child, groundY, ref moved);
            }
        }

        private static bool SnapSingle(Transform t, float groundY)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            float delta = groundY - b.min.y;
            if (Mathf.Abs(delta) < 0.0001f) return false;

            Undo.RecordObject(t, "Snap To Ground");
            Vector3 pos = t.position;
            pos.y += delta;
            t.position = pos;
            return true;
        }

        // 地面高度：优先取纸面地面的上表面，找不到则用 y = 0
        private static float FindGroundY()
        {
            var ground = GameObject.Find("Stationary paper ground");
            if (ground != null)
            {
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.bounds.max.y;
                }
            }
            return 0f;
        }

        private static bool HasSelectedAncestor(Transform t, HashSet<GameObject> selected)
        {
            Transform p = t.parent;
            while (p != null)
            {
                if (selected.Contains(p.gameObject)) return true;
                p = p.parent;
            }
            return false;
        }
    }
}
