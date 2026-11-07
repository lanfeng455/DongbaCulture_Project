using System.Collections.Generic;
using UnityEngine;

namespace Ivan.Diorama
{
    // 组员河流公式复刻（ObliqueForest.cs Line 125），玩家与动物共用。
    public static class DioramaRiver
    {
        public static float RiverX(float z)
        {
            return 2f + Mathf.Sin(z * 0.18f) * 2.05f;
        }

        // 组员原版的全局河带公式（x≈2 一条带）。只有老场景需要；卷轴场景保持关闭
        public static bool InLegacyCurve(Vector3 p)
        {
            return Mathf.Abs(p.x - RiverX(p.z)) < 1.05f;
        }

        // 组件摆放的河流（PaintedRiver）注册表：按组件变换做点判定，
        // 玩家（禁行/跳跃）与场景生成器（避让）无需知道河流摆在哪里
        private static readonly List<PaintedRiver> s_paintedRivers = new();

        public static void RegisterPainted(PaintedRiver river)
        {
            if (river != null && !s_paintedRivers.Contains(river))
            {
                s_paintedRivers.Add(river);
            }
        }

        public static void UnregisterPainted(PaintedRiver river)
        {
            s_paintedRivers.Remove(river);
        }

        public static bool InPaintedRiver(Vector3 p)
        {
            // 场景卸载后可能残留空引用，顺手清理
            s_paintedRivers.RemoveAll(r => r == null);
            for (int i = 0; i < s_paintedRivers.Count; i++)
            {
                if (s_paintedRivers[i].ContainsPoint(p)) return true;
            }
            return false;
        }

        // 总判定：全局公式（老场景）或任意组件河流。生成器避让等"两者都要"的场合用这个
        public static bool InRiver(Vector3 p)
        {
            return InLegacyCurve(p) || InPaintedRiver(p);
        }

        // 组员层级公式（ObliqueForest.cs Line 140/142）：
        // z 越小（越靠画面下方/前方）sortingOrder 越高，实现俯视角前后遮挡
        public static int SortingOrderFor(Vector3 p)
        {
            return 100 - Mathf.RoundToInt(p.z * 10f);
        }

        // 水体碰撞体注册表（由 DioramaWater 组件自动注册/注销）。
        // 玩家跳跃瞬间统一禁用所有水体碰撞，落地再恢复，实现"跳过河"。
        private static readonly List<Collider> s_waterColliders = new List<Collider>();

        public static void RegisterWater(Collider water)
        {
            if (water != null && !s_waterColliders.Contains(water))
            {
                s_waterColliders.Add(water);
            }
        }

        public static void UnregisterWater(Collider water)
        {
            s_waterColliders.Remove(water);
        }

        public static void SetWaterEnabled(bool enabled)
        {
            // 场景卸载后可能残留空引用，顺手清理
            s_waterColliders.RemoveAll(c => c == null);
            foreach (var c in s_waterColliders)
            {
                c.enabled = enabled;
            }
        }
    }
}

