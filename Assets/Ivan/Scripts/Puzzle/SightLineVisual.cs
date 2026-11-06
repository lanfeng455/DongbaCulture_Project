using UnityEngine;
using UnityEngine.Rendering;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // 视线可视化：一条很细、很淡的 LineRenderer，通/挡两种颜色。
    // 宽度与不透明度都在 Inspector 上直接调，默认值刻意压得很低，避免抢画面。
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("Puzzle/Sight Line Visual")]
    public class SightLineVisual : MonoBehaviour
    {
        [Tooltip("线的起点（一般挂一条蛇）")]
        [SerializeField] private Transform m_from;

        [Tooltip("线的终点（一般挂另一条蛇）")]
        [SerializeField] private Transform m_to;

        [Tooltip("线宽（米）。太粗会盖住画面，0.02~0.05 之间比较合适")]
        [SerializeField] private float m_width = 0.03f;

        [Tooltip("不透明度 0~1，默认 0.18，几乎只留一层若隐若现的暗示")]
        [Range(0f, 1f)]
        [SerializeField] private float m_opacity = 0.18f;

        [Tooltip("通畅时的颜色")]
        [SerializeField] private Color m_clearColor = new(0.38f, 0.86f, 0.96f, 1f);

        [Tooltip("被挡住时的颜色")]
        [SerializeField] private Color m_blockedColor = new(0.95f, 0.42f, 0.36f, 1f);

        [Tooltip("线离地高度（米），避免穿进角色身体")]
        [SerializeField] private float m_height = 0.12f;

        [Tooltip("初始是否显示（谜题里一般先隐藏，分开后才出现）")]
        [SerializeField] private bool m_visibleOnStart = false;

        private LineRenderer m_line;
        private bool m_isBlocked;
        private bool m_isVisible;

        private void Awake()
        {
            m_line = GetComponent<LineRenderer>();
            m_line.useWorldSpace = true;
            m_line.positionCount = 2;
            m_line.textureMode = LineTextureMode.Stretch;
            m_line.alignment = LineAlignment.View;
            m_line.numCapVertices = 2;
            m_line.shadowCastingMode = ShadowCastingMode.Off;
            m_line.receiveShadows = false;

            if (m_line.sharedMaterial == null)
            {
                m_line.sharedMaterial = CreateLineMaterial();
            }

            ApplyStyle();
            SetVisible(m_visibleOnStart);
        }

        private void LateUpdate()
        {
            if (!m_isVisible) return;
            ApplyPositions();
        }

        private void OnValidate()
        {
            if (m_line == null)
            {
                m_line = GetComponent<LineRenderer>();
            }
            ApplyStyle();
        }

        public void SetVisible(bool visible)
        {
            m_isVisible = visible;
            if (m_line == null) return;
            m_line.enabled = visible;
            if (visible)
            {
                ApplyPositions();
            }
        }

        // 通畅/被挡只改颜色，线本身一直在，玩家能直观看出「视线被打断了」
        public void SetBlocked(bool blocked)
        {
            if (m_isBlocked == blocked) return;
            m_isBlocked = blocked;
            ApplyStyle();
        }

        public void SetOpacity(float opacity)
        {
            m_opacity = Mathf.Clamp01(opacity);
            ApplyStyle();
        }

        public void SetWidth(float width)
        {
            m_width = Mathf.Max(0.001f, width);
            ApplyStyle();
        }

        private void ApplyPositions()
        {
            if (m_line == null) return;
            if (m_from == null || m_to == null)
            {
                m_line.enabled = false;
                return;
            }

            Vector3 a = m_from.position;
            Vector3 b = m_to.position;
            a.y = m_height;
            b.y = m_height;

            m_line.SetPosition(0, a);
            m_line.SetPosition(1, b);
            // 层级跟着两端中点走，避免线盖在近处角色脸上
            m_line.sortingOrder = DioramaRiver.SortingOrderFor((a + b) * 0.5f);
        }

        private void ApplyStyle()
        {
            if (m_line == null) return;

            m_line.startWidth = m_width;
            m_line.endWidth = m_width;

            Color color = m_isBlocked ? m_blockedColor : m_clearColor;
            color.a = Mathf.Clamp01(m_opacity);
            m_line.startColor = color;
            m_line.endColor = color;
        }

        // 材质：用 Sprite 默认着色器（支持顶点色/透明），线的颜色由 startColor/endColor 控制
        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                return null;
            }
            return new Material(shader);
        }
    }
}
