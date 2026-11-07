using UnityEngine;

namespace Ivan.Scroll
{
    // 区域幕布（水墨揭开）：用一块「空白纸」盖住世界里还没解锁的矩形区域，
    // Reveal() 后纸面往右收缩、左缘带湿墨前缘，像水墨从左往右晕开。
    // 世界空间 SpriteRenderer 实现（平贴在纸面上），不受分辨率/相机影响。
    // 用法：把本物体摆到区域中心，调好 Size；谜题导演的 On Completed 事件绑定 Reveal()。
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Ink Reveal Curtain")]
    public class InkRevealCurtain : MonoBehaviour
    {
        // 贴图按世界尺度生成（而不是「一张小图拉满整块区域」），否则纸纹会被拉伸到糊成纯色
        private const float k_pixelsPerMeter = 40f;
        private const int k_maxTextureEdge = 2048;
        private const float k_frontFadeStart = 0.82f;   // 揭开进度的最后一段让前缘淡出
        private const float k_heightOffset = 0.05f;     // 抬高一点避免和地面 z-fighting

        [Header("覆盖区域")]
        [Tooltip("区域尺寸（米），x = 宽（世界 +x），y = 深（世界 +z）。中心是本物体位置")]
        [SerializeField] private Vector2 m_size = new Vector2(20f, 15f);

        [Header("外观")]
        [Tooltip("纸面颜色：用取色器吸地面（Stationary paper ground）的颜色即可无缝")]
        [SerializeField] private Color m_paperColor = new Color(0.93f, 0.89f, 0.78f, 1f);
        [Tooltip("纸面不透明度。略低于 1 时地形会极淡地透出，读作「被纸洗淡」，不像贴了一块色块")]
        [Range(0.5f, 1f)]
        [SerializeField] private float m_opacity = 0.97f;
        [Tooltip("边缘羽化宽度（米）：四边由实体过渡到透明，消除「硬切矩形」的观感")]
        [SerializeField] private float m_edgeFeather = 1.1f;
        [Tooltip("墨迹前缘宽度（米），越大晕染带越宽")]
        [SerializeField] private float m_edgeWidth = 1.6f;
        [Tooltip("墨迹前缘向左探出的距离（米），避免主体左缘与墨锋之间露出缝")]
        [SerializeField] private float m_inkBleed = 0.25f;
        [Tooltip("渲染层级：要盖过地形（一般 100 上下）和玩家")]
        [SerializeField] private int m_sortingOrder = 1000;
        [Tooltip("纸面主体贴图覆盖：留空则程序生成纸纹")]
        [SerializeField] private Sprite m_bodySpriteOverride;
        [Tooltip("墨迹前缘贴图覆盖：留空则程序生成（需自带朝右淡出的不规则 alpha）")]
        [SerializeField] private Sprite m_frontSpriteOverride;

        [Header("揭开")]
        [Tooltip("从 Reveal() 到完全揭开的时长（秒）")]
        [SerializeField] private float m_revealDuration = 2.5f;

        private SpriteRenderer m_body;          // 纸面主体（锚在区域右缘，收缩模拟纸退）
        private SpriteRenderer m_front;         // 不规则湿墨前缘
        private Texture2D m_bodyTexture;
        private Texture2D m_frontTexture;
        private Vector2 m_bodySize;             // 主体精灵未缩放尺寸（世界单位）
        private Vector2 m_bodyPivotUnit;        // 主体精灵 pivot 的 0~1 归一化
        private Vector2 m_frontPivotUnit;
        private float m_rightEdgeX;             // 区域右缘（世界 x）
        private float m_centerZ;
        private float m_revealAge = -1f;        // < 0 = 未在揭开
        private float m_seed;                   // 纸纹/墨迹噪声种子（按位置取，实例间不重样）

        private void Awake()
        {
            m_seed = transform.position.x * 0.73f + transform.position.z * 1.31f;
            BuildVisuals();
            CoverInstant();
        }

        private void OnDestroy()
        {
            // 幕布主体/前缘是独立根物体（防场景缩放牵连），销毁时自己清理
            if (m_body != null) Destroy(m_body.gameObject);
            if (m_front != null) Destroy(m_front.gameObject);
            if (m_bodyTexture != null) Destroy(m_bodyTexture);
            if (m_frontTexture != null) Destroy(m_frontTexture);
        }

        private void Update()
        {
            if (m_revealAge < 0f) return;

            m_revealAge += Time.deltaTime;
            float t = Mathf.Clamp01(m_revealAge / m_revealDuration);
            ApplyLayout(t);

            if (t < 1f) return;

            m_revealAge = -1f;
            m_body.gameObject.SetActive(false);
            m_front.gameObject.SetActive(false);
        }

        // 开始揭开。供 UnityEvent（谜题完成）/ 代码调用
        [ContextMenu("揭开（播放动画）")]
        public void Reveal()
        {
            if (m_revealAge >= 0f) return;
            if (m_body == null || m_front == null) return;
            if (!m_body.gameObject.activeSelf) return; // 已经揭过了
            m_revealAge = 0f;
        }

        // 立即盖回（测试/重置用）
        [ContextMenu("重新盖上（测试）")]
        public void CoverInstant()
        {
            if (m_body == null || m_front == null) return;

            m_revealAge = -1f;
            m_body.gameObject.SetActive(true);
            m_front.gameObject.SetActive(true);
            ApplyLayout(0f);
        }

        // 按揭开进度重新摆放纸面与墨锋。progress: 0 = 完全盖住，1 = 完全揭开
        private void ApplyLayout(float progress)
        {
            // smoothstep：起步/收尾放慢，像墨慢慢洇开
            float eased = progress * progress * (3f - 2f * progress);
            float width = m_size.x * (1f - eased);
            float leftEdge = m_rightEdgeX - width;

            // 主体往右缘收缩（pivot 在右缘，缩放即改变覆盖宽度）
            Vector3 bodyScale = m_body.transform.localScale;
            bodyScale.x = width / Mathf.Max(0.001f, m_bodySize.x);
            m_body.transform.localScale = bodyScale;

            Vector3 bodyPos = m_body.transform.position;
            bodyPos.x = m_rightEdgeX - (1f - m_bodyPivotUnit.x) * width;
            m_body.transform.position = bodyPos;

            // 墨锋贴着主体左缘走，并向左探出一点，盖住主体羽化留下的淡边
            Vector3 frontPos = m_front.transform.position;
            frontPos.x = leftEdge - m_inkBleed + m_frontPivotUnit.x * m_edgeWidth;
            m_front.transform.position = frontPos;

            // 收尾把前缘淡出，避免最后一帧留一条深色细线
            Color frontColor = m_front.color;
            frontColor.a = 1f - Mathf.InverseLerp(k_frontFadeStart, 1f, progress);
            m_front.color = frontColor;
        }

        private void BuildVisuals()
        {
            // 平贴在纸面上（x-z 平面），与地面一致；精灵的 texture v 轴映到世界 z
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
            Vector3 center = transform.position;
            m_rightEdgeX = center.x + m_size.x * 0.5f;
            m_centerZ = center.z;
            float y = center.y + k_heightOffset;

            Sprite bodySprite = m_bodySpriteOverride != null ? m_bodySpriteOverride : BuildBodySprite();
            m_body = CreateRenderer("InkCurtainBody", bodySprite, flat, y, m_sortingOrder);
            m_bodySize = bodySprite.bounds.size;
            m_bodyPivotUnit = new Vector2(
                bodySprite.pivot.x / bodySprite.rect.width,
                bodySprite.pivot.y / bodySprite.rect.height);

            // 墨锋排在主体之上，压住主体左缘的淡边
            Sprite frontSprite = m_frontSpriteOverride != null ? m_frontSpriteOverride : BuildFrontSprite();
            m_front = CreateRenderer("InkCurtainFront", frontSprite, flat, y, m_sortingOrder + 1);
            m_frontPivotUnit = new Vector2(
                frontSprite.pivot.x / frontSprite.rect.width,
                frontSprite.pivot.y / frontSprite.rect.height);

            Vector2 frontSize = frontSprite.bounds.size;
            m_front.transform.localScale = new Vector3(
                m_edgeWidth / Mathf.Max(0.001f, frontSize.x),
                m_size.y / Mathf.Max(0.001f, frontSize.y),
                1f);
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Quaternion rotation, float y, int sortingOrder)
        {
            // 放成根物体而不是挂在组件下：场景物体常带缩放，父级缩放会连带改变覆盖范围
            var rendererObject = new GameObject(objectName);
            var renderer = rendererObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.rotation = rotation;
            renderer.transform.position = new Vector3(transform.position.x, y, m_centerZ);
            return renderer;
        }

        // 主体纸面：按世界尺度生成纸纹，四边羽化，pivot 在右缘（揭开时往右收缩）
        private Sprite BuildBodySprite()
        {
            int texW = Mathf.Clamp(Mathf.RoundToInt(m_size.x * k_pixelsPerMeter), 16, k_maxTextureEdge);
            int texH = Mathf.Clamp(Mathf.RoundToInt(m_size.y * k_pixelsPerMeter), 16, k_maxTextureEdge);
            // 让精灵的世界宽度正好等于 m_size.x：纹理像素与世界米数一一对应，不会被二次拉伸
            float pixelsPerUnit = texW / Mathf.Max(0.01f, m_size.x);

            m_bodyTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            var pixels = new Color32[texW * texH];
            float feather = Mathf.Max(0.05f, m_edgeFeather);

            for (int y = 0; y < texH; y++)
            {
                float wz = (float)y / (texH - 1) * m_size.y;
                for (int x = 0; x < texW; x++)
                {
                    float wx = (float)x / (texW - 1) * m_size.x;

                    // 按米数算离最近边的距离，再用两层噪声把边界揉皱，避免整齐的矩形轮廓
                    float edgeDistance = Mathf.Min(
                        Mathf.Min(wx, m_size.x - wx),
                        Mathf.Min(wz, m_size.y - wz));
                    float wrinkle =
                        (Mathf.PerlinNoise(wx * 0.55f + m_seed * 1.7f, wz * 0.55f + m_seed) - 0.5f) * 1.4f
                        + (Mathf.PerlinNoise(wx * 2.1f + m_seed * 2f, wz * 2.1f + m_seed * 3f) - 0.5f) * 0.5f;

                    float alpha = Mathf.Clamp01((edgeDistance + wrinkle) / feather);
                    alpha = alpha * alpha * (3f - 2f * alpha);   // smoothstep 让过渡更柔
                    alpha *= m_opacity;

                    pixels[y * texW + x] = PaperPixel(wx, wz, alpha);
                }
            }

            m_bodyTexture.SetPixels32(pixels);
            m_bodyTexture.Apply(false, false);
            m_bodyTexture.wrapMode = TextureWrapMode.Clamp;
            m_bodyTexture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(m_bodyTexture,
                new Rect(0f, 0f, texW, texH),
                new Vector2(1f, 0.5f), pixelsPerUnit);
        }

        // 墨迹前缘：每行的墨锋位置由两层 Perlin 叠加（低频大起伏 + 高频碎边），
        // 墨锋内是深墨、向外迅速洇回纸色并淡出，pivot 在左缘（定位即揭示边界）
        private Sprite BuildFrontSprite()
        {
            int texW = Mathf.Clamp(Mathf.RoundToInt(m_edgeWidth * k_pixelsPerMeter), 16, k_maxTextureEdge);
            int texH = Mathf.Clamp(Mathf.RoundToInt(m_size.y * k_pixelsPerMeter), 16, k_maxTextureEdge);
            float pixelsPerUnit = texW / Mathf.Max(0.01f, m_edgeWidth);

            m_frontTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            var pixels = new Color32[texW * texH];
            float verticalFeather = Mathf.Max(0.05f, m_edgeFeather);

            for (int y = 0; y < texH; y++)
            {
                float wz = (float)y / (texH - 1) * m_size.y;

                // 这一行的墨锋能探到多远（占整条带宽的比例），各行动态不同 → 前缘参差
                float reach = 0.26f
                    + 0.34f * Mathf.PerlinNoise(wz * 0.55f + m_seed * 2f, m_seed * 5f)
                    + 0.20f * Mathf.PerlinNoise(wz * 3.1f + m_seed * 7f, m_seed * 9f);

                // 上下两端同样羽化，避免前缘带出现横向硬边
                float verticalAlpha = Mathf.Clamp01(
                    Mathf.Min(wz, m_size.y - wz) / verticalFeather);

                for (int x = 0; x < texW; x++)
                {
                    float wx = (float)x / (texW - 1) * m_edgeWidth;
                    float u = (float)x / (texW - 1);

                    // 从墨锋中心向右淡出
                    float alpha = Mathf.Clamp01((reach - u) / 0.28f);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    alpha *= 0.92f * verticalAlpha;

                    // 越靠左越接近深墨，往外洇回纸色（湿边只有薄薄一层，不会变成一道黑杠）
                    float wet = Mathf.Clamp01(1f - u / 0.42f);
                    Color paper = PaperPixel(wx, wz, 1f);
                    Color ink = new Color(paper.r * 0.66f, paper.g * 0.64f, paper.b * 0.60f, 1f);
                    Color color = Color.Lerp(paper, ink, wet * wet);
                    color.a = alpha;

                    pixels[y * texW + x] = color;
                }
            }

            m_frontTexture.SetPixels32(pixels);
            m_frontTexture.Apply(false, false);
            m_frontTexture.wrapMode = TextureWrapMode.Clamp;
            m_frontTexture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(m_frontTexture,
                new Rect(0f, 0f, texW, texH),
                new Vector2(0f, 0.5f), pixelsPerUnit);
        }

        // 纸面像素（参数是世界米数）：纤维 + 颗粒 + 水渍 + 稀疏墨点，叠出纸的层次
        private Color PaperPixel(float wx, float wz, float alpha)
        {
            // 纤维：一个方向低频、另一个方向高频 → 拉长的纤维丝
            float fiberA = Mathf.PerlinNoise(wx * 0.7f + m_seed * 2f, wz * 5.5f + m_seed);
            float fiberB = Mathf.PerlinNoise(wx * 5.5f + m_seed * 5f, wz * 0.7f + m_seed * 1.3f);

            // 中频颗粒
            float grain = Mathf.PerlinNoise(wx * 9f + m_seed * 7f, wz * 9f + m_seed * 4f);

            float lum = 1f
                + (fiberA - 0.5f) * 0.030f
                + (fiberB - 0.5f) * 0.030f
                + (grain - 0.5f) * 0.040f;

            // 低频水渍：淡淡的深色晕斑，让大面积不再是死平一块
            float stain = Mathf.PerlinNoise(wx * 0.075f + m_seed * 3f, wz * 0.06f + m_seed * 8f);
            lum -= Mathf.Clamp01((stain - 0.42f) / 0.5f) * 0.045f;

            // 稀疏墨点（频率控制在约 7cm 尺度，避免变成噪点）
            float speck = Mathf.PerlinNoise(wx * 14f + m_seed * 9f, wz * 14f + m_seed * 11f);
            lum -= Mathf.Clamp01((speck - 0.70f) / 0.3f) * 0.10f;

            return new Color(m_paperColor.r * lum, m_paperColor.g * lum, m_paperColor.b * lum, alpha);
        }

        private void OnDrawGizmos()
        {
            Vector3 c = transform.position;
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.9f);
            Gizmos.DrawWireCube(c, new Vector3(m_size.x, 0.2f, m_size.y));

            // 揭开起点（区域左缘）画一条竖线，方便对齐
            Gizmos.DrawLine(
                c + new Vector3(-m_size.x * 0.5f, 0f, -m_size.y * 0.5f),
                c + new Vector3(-m_size.x * 0.5f, 0f, m_size.y * 0.5f));
        }
    }
}
