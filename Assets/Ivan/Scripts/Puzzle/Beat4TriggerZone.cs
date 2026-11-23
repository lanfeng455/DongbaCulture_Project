using System;
using UnityEngine;

namespace Ivan.Puzzle
{
    // 接触触发区（照 Beat2 CaveSlot 的路子）：
    //   只把"通过身份白名单"的那一次接触上报给 Beat4Puzzle；
    //   上报后用 releasing 标记 + 超时挡住重复触发——物体被推开又碰回来不会再来一遍。
    //   （真正的重复保护还有一层：Beat4Puzzle 自己用 m_isSplit / Half.Delivered 兜底）
    //
    // 挂载：由 Beat4Puzzle 在运行时挂到板栗 / 巨兽身上，不需要手动挂。
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Beat4 Trigger Zone")]
    public class Beat4TriggerZone : MonoBehaviour
    {
        // 返回 true 表示这次接触被接受（身份对上了，可以锁）；false 表示不关我事
        public event Func<Collider, bool> Entered;

        private const float k_releaseTimeoutSeconds = 3f;

        private Collider m_releasing;
        private float m_releaseTimeout;

        private void Update()
        {
            if (m_releaseTimeout <= 0f) return;

            m_releaseTimeout -= Time.deltaTime;
            if (m_releaseTimeout <= 0f)
            {
                m_releasing = null;   // 超时：强制放行，别把触发区永久锁死
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_releasing != null && other == m_releasing) return;
            if (Entered == null || !Entered(other)) return;

            m_releasing = other;
            m_releaseTimeout = k_releaseTimeoutSeconds;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != m_releasing) return;

            m_releasing = null;
            m_releaseTimeout = 0f;
        }
    }
}
