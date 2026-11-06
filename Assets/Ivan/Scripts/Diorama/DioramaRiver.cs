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

        public static bool InRiver(Vector3 p)
        {
            return Mathf.Abs(p.x - RiverX(p.z)) < 1.05f;
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

