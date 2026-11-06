using UnityEngine;

namespace Ivan.Puzzle
{
    // 纯几何的视线判定：XZ 平面上「点到线段」的距离与遮挡半径比较。
    // 不依赖物理、不依赖帧率，每帧调用也很便宜（遮挡物通常只有几个）。
    public static class SightLineSensor
    {
        // 两点之间是否被任一遮挡物挡住
        public static bool IsBlocked(Vector3 from, Vector3 to)
        {
            return TryFindBlocker(from, to) != null;
        }

        // 返回第一个挡住视线的遮挡物，没有则返回 null
        public static SightBlocker TryFindBlocker(Vector3 from, Vector3 to)
        {
            Vector3 a = new Vector3(from.x, 0f, from.z);
            Vector3 b = new Vector3(to.x, 0f, to.z);
            Vector3 ab = b - a;
            float abLengthSquared = ab.sqrMagnitude;

            var all = SightBlocker.All;
            for (int i = 0; i < all.Count; i++)
            {
                var blocker = all[i];
                if (blocker == null || !blocker.isActiveAndEnabled) continue;

                Vector3 center = blocker.Center;
                // 投影到线段上并夹到两端，退化情况（两点重合）直接取起点
                float t = abLengthSquared > 0.0001f
                    ? Mathf.Clamp01(Vector3.Dot(center - a, ab) / abLengthSquared)
                    : 0f;
                Vector3 closest = a + ab * t;

                float radius = blocker.Radius;
                if ((center - closest).sqrMagnitude <= radius * radius)
                {
                    return blocker;
                }
            }
            return null;
        }
    }
}
