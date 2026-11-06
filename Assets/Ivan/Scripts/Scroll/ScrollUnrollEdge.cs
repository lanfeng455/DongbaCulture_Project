using UnityEngine;
using UnityEngine.UI;

namespace Ivan.Scroll
{
    // 卷轴右缘的「纸卷边」：跟着画面揭示边界走。
    // 记录历史最大揭示 x（世界坐标），摄像机右缘揭示新区域时纸卷平滑铺开；
    // 往回走时边界不退，纸卷随镜头移出画面，回到旧区域不会重新铺。
    // 素材全部运行时程序生成（圆柱着色纸卷条 + 左侧柔影），想要更好的图
    // 就在 Inspector 里把 Sprite 拖进 Override 字段覆盖。
    [AddComponentMenu("Scroll/Scroll Unroll Edge")]
    public class ScrollUnrollEdge : MonoBehaviour
    {
        [Header("跟随")]
        [Tooltip("用于换算屏幕位置的相机；留空用 Camera.main")]
        [SerializeField] private Camera m_camera;

        [Header("外观")]
        [Tooltip("纸卷条宽度（参考分辨率下的像素，会按屏幕等比缩放）")]
        [SerializeField] private float m_rollWidthInPixels = 104f;
        [Tooltip("纸卷左侧柔影宽度（参考分辨率下的像素，会按屏幕等比缩放）")]
        [SerializeField] private float m_shadeWidthInPixels = 90f;
        [Tooltip("纸卷基色（程序生成素材用；和纸面同色最自然）")]
        [SerializeField] private Color m_rollColor = new Color(0.93f, 0.88f, 0.74f);
        [Tooltip("柔影最深处的透明度（贴着纸卷那一侧）")]
        [SerializeField] private float m_shadeStrength = 0.22f;

        [Header("自定义素材（可选）")]
        [Tooltip("拖入后替代程序生成的纸卷图")]
        [SerializeField] private Sprite m_rollSpriteOverride;
        [Tooltip("拖入后替代程序生成的柔影图")]
        [SerializeField] private Sprite m_shadeSpriteOverride;

        [Header("分辨率适配")]
        [Tooltip("像素尺寸的设计分辨率：下面的宽度都按它写，其它分辨率由 CanvasScaler 等比缩放。" +
                 "默认 3840x2160 是为了保持你当前 4K Game View 里的观感")]
        [SerializeField] private Vector2 m_referenceResolution = new Vector2(3840f, 2160f);

        private Vector3 m_frontierPoint;    // 历史最大揭示点（世界坐标，只进不退）
        private bool m_initialized;

        private Canvas m_canvas;
        private RectTransform m_strip;
        private GameObject m_canvasObject;

        private void Awake()
        {
            EnsureUi();
        }

        private void OnDestroy()
        {
            // 画布是独立根物体（不挂在本组件下，避免继承场景缩放），销毁时自己清理
            if (m_canvasObject != null)
            {
                Destroy(m_canvasObject);
            }
        }

        private void Update()
        {
            if (m_camera == null)
            {
                m_camera = Camera.main;
                if (m_camera == null)
                {
                    return;
                }
            }

            // 摄像机右缘中点投到地面（y=0）的点：此刻揭示到的位置。
            // 不额外做平滑——相机本身已经跟随玩家平滑移动，再加一层滞后会让纸卷
            // 永远追不上边缘，稳定地停在右缘左侧（走动时可差几十像素）
            Ray ray = m_camera.ViewportPointToRay(new Vector3(1f, 0.5f, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-5f)
            {
                return; // 正交俯视相机理论上不会，保险
            }
            float t = -ray.origin.y / ray.direction.y;
            Vector3 groundPoint = ray.origin + ray.direction * t;

            if (!m_initialized)
            {
                m_initialized = true;
                m_frontierPoint = groundPoint;
            }
            else if (groundPoint.x > m_frontierPoint.x)
            {
                m_frontierPoint = groundPoint;
            }

            // 用边界点本身换算屏幕坐标（不再假设 z），纸卷右缘精确贴上去
            Vector3 screen = m_camera.WorldToScreenPoint(m_frontierPoint);
            if (screen.z < 0f)
            {
                return; // 边界在相机身后，保持上一帧位置
            }

            Vector2 position = m_strip.anchoredPosition;
            // WorldToScreenPoint 给的是物理像素，画布单位 = 像素 / scaleFactor
            position.x = screen.x / Mathf.Max(1e-4f, m_canvas.scaleFactor);
            m_strip.anchoredPosition = position;
        }

        private void EnsureUi()
        {
            // 画布：Overlay；放成根物体而不是挂在组件下——场景物体常带缩放，
            // 父级缩放会连带把画布和位置一起缩小
            var canvasObject = new GameObject("ScrollUnrollCanvas", typeof(Canvas));
            m_canvasObject = canvasObject;
            m_canvas = canvasObject.GetComponent<Canvas>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_canvas.sortingOrder = 900; // 低于对话字幕（1000），卷边不会盖住台词

            // 分辨率适配：宽度按设计分辨率换算，其它分辨率等比缩放
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = m_referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 整条（柔影 + 纸卷）：pivot 在右缘，右缘对齐揭示边界
            var stripObject = new GameObject("RollStrip", typeof(RectTransform));
            m_strip = (RectTransform)stripObject.transform;
            m_strip.SetParent(m_canvas.transform, false);
            m_strip.anchorMin = new Vector2(0f, 0f);
            m_strip.anchorMax = new Vector2(0f, 1f);
            m_strip.pivot = new Vector2(1f, 0.5f);
            m_strip.anchoredPosition = Vector2.zero;
            m_strip.sizeDelta = new Vector2(m_shadeWidthInPixels + m_rollWidthInPixels, 0f);

            // 柔影（左侧）：纸卷投在纸面上的阴影，离卷越远越淡
            var shadeObject = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            var shadeRect = (RectTransform)shadeObject.transform;
            shadeRect.SetParent(m_strip, false);
            shadeRect.anchorMin = new Vector2(0f, 0f);
            shadeRect.anchorMax = new Vector2(0f, 1f);
            shadeRect.pivot = new Vector2(0f, 0.5f);
            shadeRect.anchoredPosition = Vector2.zero;
            shadeRect.sizeDelta = new Vector2(m_shadeWidthInPixels, 0f);
            var shadeImage = shadeObject.GetComponent<Image>();
            shadeImage.sprite = m_shadeSpriteOverride != null ? m_shadeSpriteOverride : CreateShadeSprite();
            shadeImage.raycastTarget = false;

            // 纸卷（右侧竖条）：圆柱着色，最右边压一条深色棱线表示卷的厚度
            var rollObject = new GameObject("Roll", typeof(RectTransform), typeof(Image));
            var rollRect = (RectTransform)rollObject.transform;
            rollRect.SetParent(m_strip, false);
            rollRect.anchorMin = new Vector2(1f, 0f);
            rollRect.anchorMax = new Vector2(1f, 1f);
            rollRect.pivot = new Vector2(1f, 0.5f);
            rollRect.anchoredPosition = Vector2.zero;
            rollRect.sizeDelta = new Vector2(m_rollWidthInPixels, 0f);
            var rollImage = rollObject.GetComponent<Image>();
            rollImage.sprite = m_rollSpriteOverride != null ? m_rollSpriteOverride : CreateRollSprite();
            rollImage.raycastTarget = false;
        }

        // 程序生成纸卷条：中间亮、两边暗的圆柱着色，竖直方向拉伸使用
        private Sprite CreateRollSprite()
        {
            const int width = 32;
            const int height = 8;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                float brightness = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI);
                if (t > 0.9f)
                {
                    brightness *= 0.72f; // 卷边棱线
                }
                Color pixel = m_rollColor * brightness;
                pixel.a = 1f;
                for (int y = 0; y < height; y++)
                {
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        // 程序生成柔影：贴着纸卷一侧最深，向左渐隐
        private Sprite CreateShadeSprite()
        {
            const int width = 64;
            const int height = 8;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                float alpha = Mathf.Pow(t, 1.6f) * m_shadeStrength;
                for (int y = 0; y < height; y++)
                {
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
