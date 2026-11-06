using System.Collections.Generic;
using UnityEngine;

// 可瞄准目标的全局注册表（可组合文字 + 道具放置槽）。
// 静态类天然单例：目标自己进出，瞄准系统只读，不需要任何场景引用。
public static class DongbaGlyphRegistry
{
    private static readonly List<IGlyphAimTarget> s_targets = new List<IGlyphAimTarget>();

    public static IReadOnlyList<IGlyphAimTarget> Targets => s_targets;

    public static void Register(IGlyphAimTarget target)
    {
        if (target == null || s_targets.Contains(target))
        {
            return;
        }

        s_targets.Add(target);
    }

    public static void Unregister(IGlyphAimTarget target)
    {
        if (target == null)
        {
            return;
        }

        s_targets.Remove(target);
    }

    // 找光标（屏幕坐标）下的瞄准目标：
    // 目标圈在屏幕上的投影范围内就算命中，命中多个时取"相对自己的圈最居中"的那个。
    // 像素命中半径带下限，保证远处的目标（圈投影很小）也点得到。
    // 超出 maxDistanceInMeters 的目标直接跳过，在相机背后的也跳过。
    public static IGlyphAimTarget FindUnderCursor(
        Camera camera,
        Vector2 screenPosition,
        Vector3 playerPosition,
        float maxDistanceInMeters,
        float minHitRadiusInPixels,
        float hitRadiusMultiplier)
    {
        if (camera == null)
        {
            return null;
        }

        IGlyphAimTarget best = null;
        float bestScore = float.PositiveInfinity;
        float maxSqrDistance = maxDistanceInMeters * maxDistanceInMeters;

        for (int i = s_targets.Count - 1; i >= 0; i--)
        {
            IGlyphAimTarget target = s_targets[i];

            // 物体可能在切换场景时被销毁，顺手清掉失效引用
            if (!IsTargetAlive(target))
            {
                s_targets.RemoveAt(i);
                continue;
            }

            if (!target.IsAimable)
            {
                continue;
            }

            Vector3 worldPosition = target.WorldPosition;

            if ((worldPosition - playerPosition).sqrMagnitude > maxSqrDistance)
            {
                continue;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);

            // z <= 0 → 目标在相机背后，别让它被"点在"屏幕上
            if (screenPoint.z <= 0f)
            {
                continue;
            }

            Vector2 targetScreenPosition = new Vector2(screenPoint.x, screenPoint.y);
            float pixelDistance = Vector2.Distance(screenPosition, targetScreenPosition);

            // 把圈的半径沿相机右方向投影到屏幕，得到随远近自动缩放的像素半径
            float ringRadiusInMeters = Mathf.Max(0.1f, target.RingRadiusInMeters);
            Vector3 screenEdge = camera.WorldToScreenPoint(
                worldPosition + camera.transform.right * ringRadiusInMeters);
            float screenRadius = Vector2.Distance(
                targetScreenPosition, new Vector2(screenEdge.x, screenEdge.y));

            float hitRadiusInPixels = Mathf.Max(
                screenRadius * hitRadiusMultiplier, minHitRadiusInPixels);

            if (pixelDistance > hitRadiusInPixels)
            {
                continue;
            }

            // 归一化距离：越小说明光标越贴近该目标中心，不受目标远近影响
            float score = pixelDistance / hitRadiusInPixels;
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }

        return best;
    }

    // 判断目标是否仍然有效：真 null 与已被销毁（Unity 伪 null）都算无效。
    // 接口引用走不到 Unity 重载过的 ==，必须先转回 Object 再比较。
    public static bool IsTargetAlive(IGlyphAimTarget target)
    {
        if (target == null)
        {
            return false;
        }

        return target is Object unityObject && unityObject != null;
    }

    // 关闭 Domain Reload 时静态列表会残留上一个会话的对象，进入 Play 时清一次
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        s_targets.Clear();
    }
}
