using UnityEngine;

namespace Ivan.Scroll
{
    // PCG 保留区：圈出一块「卷轴生成器不许往里放东西」的地方。
    // 手动摆的河流、地标、演出道具都用它圈住；生成器采样时会避让所有保留区。
    // 区块大小直接用物体上的 Box Collider（不参与物理，仅作区域标记 + Gizmo）。
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Scroll Reserved Zone")]
    public class ScrollReservedZone : MonoBehaviour
    {
        [Tooltip("这块区域留给什么（备注，仅提示用，如「河流」「仪式祭坛」）")]
        [SerializeField] private string m_note = "Reserved";

        public string Note => m_note;

        // 世界空间包围盒（XZ 平面判定用）
        public Bounds WorldBounds => GetComponent<BoxCollider>().bounds;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.size = new Vector3(6f, 4f, 14f);
            box.center = new Vector3(0f, 2f, 0f);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + new Vector3(0f, 3f, 0f),
                string.IsNullOrEmpty(m_note) ? "Reserved" : m_note);
#endif
        }
    }
}
