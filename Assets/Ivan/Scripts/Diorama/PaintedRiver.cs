using System.Collections.Generic;
using UnityEngine;

namespace Ivan.Diorama
{
    // 水墨河流（运行时部分）：只负责三件事——
    //   1. 阻挡判定（ContainsPoint，经 DioramaRiver 注册表供玩家/动物/生成器使用）
    //   2. 水纹流动动画（摆动烘焙出来的 WaterGlyph 子物体）
    //   3. 编辑器 Gizmos（河心线/岸线）
    // 视觉（水带贴图 + 水纹精灵）由 PaintedRiverBaker 在编辑器里烘焙成本场景真实物体，
    // 运行时不生成、不加载、不拼装。参数改动后需在烘焙窗口重新烘焙。
    [DisallowMultipleComponent]
    [AddComponentMenu("Diorama/Painted River")]
    public class PaintedRiver : MonoBehaviour
    {
        // 烘焙产物的子物体命名（烘焙窗口与运行时动画按这两个名字识别，勿改）
        public const string RibbonChildName = "Ribbon";
        public const string GlyphChildPrefix = "WaterGlyph";

        [Header("河道走向")]
        [Tooltip("世界朝向角：0 = 沿世界 +X 横穿画面，90 = 沿 +Z 纵向（组员原版方向）。" +
                 "想略微倾斜就填 ±几度。烘焙时按此值重设物体旋转")]
        [SerializeField] private float m_flowYawDegrees = 0f;
        [Tooltip("河道长度（米，沿流向）")]
        [SerializeField] private float m_length = 24f;
        [Tooltip("水面半宽（米）：河心到水边")]
        [SerializeField] private float m_bankHalfWidth = 1.35f;

        [Header("蜿蜒")]
        [Tooltip("河心正弦摆幅（米），0 = 笔直")]
        [SerializeField] private float m_meanderAmplitude = 0.85f;
        [Tooltip("河心正弦波长（米）")]
        [SerializeField] private float m_meanderWavelength = 9f;

        [Header("外观（烘焙参数）")]
        [Tooltip("水面基色（组员原版 .39/.59/.64）")]
        [SerializeField] private Color m_waterColor = new Color(0.39f, 0.59f, 0.64f, 1f);
        [Tooltip("岸边深墨色（水陆交界的湿边）")]
        [SerializeField] private Color m_deepColor = new Color(0.30f, 0.50f, 0.56f, 1f);
        [Tooltip("岸上棕色水渍色（组员原版 .49/.37/.20）")]
        [SerializeField] private Color m_bankStainColor = new Color(0.49f, 0.37f, 0.20f, 1f);
        [Tooltip("水面不透明度：河心处的峰值 alpha")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float m_waterOpacity = 0.45f;
        [Tooltip("水边羽化宽度（米）：水带向岸线的渐隐距离")]
        [SerializeField] private float m_edgeFeather = 0.55f;
        [Tooltip("河两端淡出长度（米），避免硬切断头")]
        [SerializeField] private float m_endFade = 1.4f;

        [Header("水纹（烘焙参数）")]
        [Tooltip("水纹 glyph 沿河的间距（米），组员原版 0.25")]
        [SerializeField] private float m_glyphSpacing = 0.25f;
        [Tooltip("水纹 tint（组员原版 .48/.66/.73/.86）")]
        [SerializeField] private Color m_glyphTint = new Color(0.48f, 0.66f, 0.73f, 0.86f);

        [Header("层级与流动")]
        [Tooltip("水带基础层级（组员原版 -90，地面 -100）；水纹自动 +10")]
        [SerializeField] private int m_baseSortingOrder = -90;
        [Tooltip("播放时水纹是否横向轻摆（流动错觉）；烘焙出的水纹静止也没问题")]
        [SerializeField] private bool m_animateGlyphs = true;
        [Tooltip("水纹横摆周期（秒），组员原版 2.2")]
        [SerializeField] private float m_flowPeriod = 2.2f;
        [Tooltip("水纹横摆幅度（米），组员原版 0.06")]
        [SerializeField] private float m_flowBob = 0.06f;

        [Header("阻挡与随机")]
        [Tooltip("不可行走的半宽（米）：略小于水面半宽，玩家能踩到岸边。跳跃可越过")]
        [SerializeField] private float m_blockingHalfWidth = 1.05f;
        [Tooltip("随机种子：岸线形状与水纹散布都由它决定（烘焙时使用）")]
        [SerializeField] private int m_seed = 148;

        private readonly List<Transform> m_glyphTransforms = new();
        private readonly List<Vector3> m_glyphOrigins = new();
        private float m_clock;
        private bool m_glyphsCached;

        private void OnEnable()
        {
            DioramaRiver.RegisterPainted(this);
        }

        private void OnDisable()
        {
            DioramaRiver.UnregisterPainted(this);
        }

        private void Update()
        {
            if (!Application.isPlaying || !m_animateGlyphs) return;

            CacheGlyphs();
            if (m_glyphTransforms.Count == 0) return;

            // 组员原版的流动错觉：每个水纹按相位差横向轻摆（跨流向方向）
            m_clock += Time.deltaTime;
            float w = Mathf.PI * 2f / Mathf.Max(0.01f, m_flowPeriod);
            for (int i = 0; i < m_glyphTransforms.Count; i++)
            {
                if (m_glyphTransforms[i] == null) continue;
                Vector3 p = m_glyphOrigins[i];
                p.y += m_flowBob * Mathf.Sin(m_clock * w - i * 0.35f);
                m_glyphTransforms[i].localPosition = p;
            }
        }

        // 收集烘焙出来的水纹子物体（只在第一次播放帧做一次）
        private void CacheGlyphs()
        {
            if (m_glyphsCached) return;
            m_glyphsCached = true;
            m_glyphTransforms.Clear();
            m_glyphOrigins.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(GlyphChildPrefix)) continue;
                m_glyphTransforms.Add(child);
                m_glyphOrigins.Add(child.localPosition);
            }
        }

        // 世界点是否落在河道内（用于禁行/跳跃判定，由 DioramaRiver 注册表统一调用）
        public bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float margin = m_blockingHalfWidth + 0.5f;
            if (local.x < -m_length * 0.5f - margin || local.x > m_length * 0.5f + margin) return false;
            return Mathf.Abs(local.y - CenterlineLocalY(local.x)) < m_blockingHalfWidth;
        }

        // 河心线在本地空间的横向位置：x 沿流向，y 跨流向（父物体平贴后即世界 x/z）。
        // 烘焙窗口用同一公式画贴图，两边必须保持一致
        public float CenterlineLocalY(float localX)
        {
            float wavelength = Mathf.Max(0.01f, m_meanderWavelength);
            return Mathf.Sin(localX * Mathf.PI * 2f / wavelength + m_seed * 0.17f) * m_meanderAmplitude;
        }

        // 选中时画河心线与两条岸线，方便对位
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.62f, 0.85f, 0.9f);
            const int steps = 48;
            Vector3 prevCenter = Vector3.zero;
            Vector3 prevBankA = Vector3.zero;
            Vector3 prevBankB = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float x = -m_length * 0.5f + m_length * i / steps;
                float y = CenterlineLocalY(x);
                Vector3 center = transform.TransformPoint(new Vector3(x, y, 0f));
                // transform.up = 本地跨流向轴（父物体平贴后即世界水平方向）
                Vector3 across = transform.up * m_bankHalfWidth;
                Vector3 bankA = center + across;
                Vector3 bankB = center - across;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevCenter, center);
                    Gizmos.DrawLine(prevBankA, bankA);
                    Gizmos.DrawLine(prevBankB, bankB);
                }
                prevCenter = center;
                prevBankA = bankA;
                prevBankB = bankB;
            }
        }
    }
}
