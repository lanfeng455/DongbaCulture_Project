using UnityEngine;

namespace Ivan.Diorama
{
    // 水体标记：把自身碰撞体登记进 DioramaRiver 注册表，
    // 玩家跳跃瞬间由 DioramaRiver.SetWaterEnabled(false) 统一禁用，落地恢复。
    // 以后新增水体（池塘、湖面……）只要挂这一个组件即可被"跳河"逻辑识别。
    [RequireComponent(typeof(Collider))]
    public class DioramaWater : MonoBehaviour
    {
        private Collider m_collider;

        private void OnEnable()
        {
            if (m_collider == null)
            {
                m_collider = GetComponent<Collider>();
            }
            DioramaRiver.RegisterWater(m_collider);
        }

        private void OnDisable()
        {
            DioramaRiver.UnregisterWater(m_collider);
        }
    }
}
