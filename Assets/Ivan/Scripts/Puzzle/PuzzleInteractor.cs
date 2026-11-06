using System.Collections.Generic;
using UnityEngine;

namespace Ivan.Puzzle
{
    // 可交互物的注册表。用注册表而不是每次按键都扫场景，
    // 避免对上百个 MonoBehaviour 做 FindObjectsByType。
    public static class PuzzleInteractables
    {
        private static readonly List<IPuzzleInteractable> s_all = new(8);

        public static IReadOnlyList<IPuzzleInteractable> All => s_all;
        public static int Count => s_all.Count;

        public static void Register(IPuzzleInteractable item)
        {
            if (item == null || s_all.Contains(item)) return;
            s_all.Add(item);
        }

        public static void Unregister(IPuzzleInteractable item)
        {
            if (item == null) return;
            s_all.Remove(item);
        }
    }

    // 挂在玩家身上：按 E 时选半径内最近的可交互物执行。
    // 返回 true 表示交互已消费，玩家控制器就不会再走「喂食/补种」分支。
    [DisallowMultipleComponent]
    public class PuzzleInteractor : MonoBehaviour
    {
        [Tooltip("交互半径（米）")]
        [SerializeField] private float m_range = 2f;

        public float Range => m_range;

        // 选最近的一个执行，其余保持原状；一次按键只消费一个交互
        public bool TryInteract()
        {
            IPuzzleInteractable best = null;
            float bestDistance = m_range;
            Vector3 origin = transform.position;

            var all = PuzzleInteractables.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var candidate = all[i];
                // 接口引用无法走 Unity 的销毁判定，统一按 Object 比较
                if (candidate is Object obj && obj == null) continue;
                if (!candidate.CanInteract) continue;

                Transform anchor = candidate.InteractAnchor;
                if (anchor == null) continue;

                Vector3 delta = anchor.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            if (best == null) return false;

            best.Interact(transform);
            return true;
        }
    }
}
