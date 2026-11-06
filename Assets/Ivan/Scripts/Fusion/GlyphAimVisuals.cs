using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 瞄准模式的视觉层：给每个可瞄准目标（可组合文字 / 道具放置槽）套一个面向相机的圆环，
// 并用 LineRenderer 从玩家画一条箭头到当前选中的目标。
//
// 圆环贴图在运行时程序化生成（无需美术资源），圆形始终正对相机，
// 所以斜俯视下看起来仍是正圆，和设计稿一致。
public class GlyphAimVisuals : MonoBehaviour
{
    [Header("圆环")]
    [Tooltip("普通候选文字的圈颜色")]
    [SerializeField] private Color m_ringColor = new Color(0.85f, 0.2f, 0.2f, 0.9f);

    [Tooltip("当前锁定目标的圈颜色（更亮）")]
    [SerializeField] private Color m_selectedRingColor = new Color(1f, 0.4f, 0.25f, 1f);

    [Tooltip("圈半径相对字形设定半径的倍率，1 = 默认")]
    [SerializeField] private float m_ringRadiusScale = 1f;

    [Tooltip("选中目标的圈额外放大倍数")]
    [SerializeField] private float m_selectedScaleMultiplier = 1.12f;

    [Header("箭头")]
    [Tooltip("箭头颜色")]
    [SerializeField] private Color m_arrowColor = new Color(0.9f, 0.25f, 0.18f, 1f);

    [Tooltip("箭杆宽度（米）")]
    [SerializeField] private float m_arrowWidthInMeters = 0.06f;

    [Tooltip("箭头头部两翼长度（米）")]
    [SerializeField] private float m_arrowHeadLengthInMeters = 0.35f;

    [Tooltip("箭头离地高度（米），避免贴地被地面挡住")]
    [SerializeField] private float m_arrowHeightInMeters = 0.9f;

    [Tooltip("排序层级，越大越靠前")]
    [SerializeField] private int m_sortingOrder = 500;

    private const float k_ringTextureSize = 128f;
    private const float k_ringThicknessPixels = 6f;

    private sealed class RingEntry
    {
        public SpriteRenderer Renderer;
        public float Radius;
        public bool IsAnimating;
        public Tween ScaleTween;
    }

    private static Sprite s_ringSprite;
    private static Material s_lineMaterial;

    private readonly Dictionary<IGlyphAimTarget, RingEntry> m_rings = new Dictionary<IGlyphAimTarget, RingEntry>();
    private readonly List<IGlyphAimTarget> m_staleKeys = new List<IGlyphAimTarget>();
    private readonly List<IGlyphAimTarget> m_survivingKeys = new List<IGlyphAimTarget>();
    private readonly List<IGlyphAimTarget> m_allKeys = new List<IGlyphAimTarget>();
    private readonly List<KeyValuePair<IGlyphAimTarget, RingEntry>> m_keptPairs =
        new List<KeyValuePair<IGlyphAimTarget, RingEntry>>();

    private Camera m_camera;
    private LineRenderer m_shaftLine;
    private LineRenderer m_headLine;

    private void Awake()
    {
        EnsureResources();
        CreateArrowLines();
    }

    private void LateUpdate()
    {
        // 圆环和箭头都跟随相机朝向：斜俯视下也保持正对屏幕
        if (m_camera == null)
        {
            m_camera = Camera.main;
        }

        Quaternion facing = m_camera != null ? m_camera.transform.rotation : Quaternion.identity;

        m_staleKeys.Clear();
        m_survivingKeys.Clear();

        foreach (KeyValuePair<IGlyphAimTarget, RingEntry> pair in m_rings)
        {
            IGlyphAimTarget target = pair.Key;
            RingEntry entry = pair.Value;

            // 目标可能在瞄准期间被销毁/隐藏/集齐
            if (!DongbaGlyphRegistry.IsTargetAlive(target) || !target.IsAimable)
            {
                m_staleKeys.Add(target);
                DestroyRingEntry(entry);
                continue;
            }

            Vector3 position = target.WorldPosition;
            entry.Renderer.transform.position = position;
            entry.Renderer.transform.rotation = facing;
            m_survivingKeys.Add(target);

            // 扩圈动画期间不让每帧同步覆盖 DOTween 改的缩放
            if (entry.IsAnimating)
            {
                continue;
            }

            float diameter = entry.Radius * 2f;
            entry.Renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        // 重建字典而不是按已销毁对象删键，避免 Unity 的伪 null 让 Remove 失效
        if (m_staleKeys.Count > 0)
        {
            RebuildDictionary(m_survivingKeys);
        }
    }

    // 进入瞄准：为每个候选目标建圈
    public void ShowRings(IReadOnlyList<IGlyphAimTarget> targets)
    {
        HideRings();

        if (targets == null)
        {
            return;
        }

        foreach (IGlyphAimTarget target in targets)
        {
            if (!DongbaGlyphRegistry.IsTargetAlive(target) || !target.IsAimable)
            {
                continue;
            }

            CreateRing(target);
        }
    }

    // 切换锁定目标：高亮它的圈，其余回到普通色
    public void HighlightTarget(IGlyphAimTarget target)
    {
        foreach (KeyValuePair<IGlyphAimTarget, RingEntry> pair in m_rings)
        {
            bool isTarget = pair.Key == target;
            pair.Value.Renderer.color = isTarget ? m_selectedRingColor : m_ringColor;
            pair.Value.Renderer.sortingOrder = isTarget ? m_sortingOrder + 1 : m_sortingOrder;

            if (!pair.Value.IsAnimating)
            {
                float multiplier = isTarget ? m_selectedScaleMultiplier : 1f;
                float diameter = pair.Value.Radius * 2f * multiplier;
                pair.Value.Renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
            }
        }
    }

    // 发射瞬间：目标的圈扩大，做出"吸收"的观感
    public void ExpandRing(IGlyphAimTarget target, float targetRadiusInMeters, float durationInSeconds)
    {
        if (!DongbaGlyphRegistry.IsTargetAlive(target) || !m_rings.TryGetValue(target, out RingEntry entry))
        {
            return;
        }

        entry.IsAnimating = true;
        entry.ScaleTween?.Kill();

        float diameter = targetRadiusInMeters * 2f;
        entry.ScaleTween = entry.Renderer.transform
            .DOScale(new Vector3(diameter, diameter, 1f), durationInSeconds)
            .SetEase(Ease.OutCubic);
    }

    // 只保留一个圈（发射时把其它候选圈收掉）
    public void HideAllExcept(IGlyphAimTarget keep)
    {
        m_survivingKeys.Clear();

        foreach (KeyValuePair<IGlyphAimTarget, RingEntry> pair in m_rings)
        {
            if (pair.Key == keep)
            {
                m_survivingKeys.Add(pair.Key);
                continue;
            }

            DestroyRingEntry(pair.Value);
        }

        RebuildDictionary(m_survivingKeys);
    }

    public void HideRings()
    {
        foreach (RingEntry entry in m_rings.Values)
        {
            DestroyRingEntry(entry);
        }

        m_rings.Clear();
    }

    // 箭头：visible = false 时隐藏
    public void SetArrow(Vector3 fromWorldPosition, Vector3 toWorldPosition, bool visible)
    {
        if (m_shaftLine == null || m_headLine == null)
        {
            return;
        }

        if (!visible)
        {
            m_shaftLine.enabled = false;
            m_headLine.enabled = false;
            return;
        }

        Vector3 from = fromWorldPosition + Vector3.up * m_arrowHeightInMeters;
        Vector3 to = toWorldPosition + Vector3.up * m_arrowHeightInMeters;

        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f)
        {
            m_shaftLine.enabled = false;
            m_headLine.enabled = false;
            return;
        }

        m_shaftLine.enabled = true;
        m_headLine.enabled = true;

        m_shaftLine.positionCount = 2;
        m_shaftLine.SetPosition(0, from);
        m_shaftLine.SetPosition(1, to);

        // 头部在相机平面内张开两翼，箭头方向始终可读
        Vector3 direction = delta.normalized;
        Vector3 planeNormal = m_camera != null ? m_camera.transform.forward : Vector3.forward;
        Vector3 wingAxis = Vector3.Cross(direction, planeNormal);

        if (wingAxis.sqrMagnitude < 0.0001f)
        {
            wingAxis = Vector3.Cross(direction, Vector3.up);
        }

        wingAxis.Normalize();

        Vector3 headBase = to - direction * m_arrowHeadLengthInMeters;
        m_headLine.positionCount = 3;
        m_headLine.SetPosition(0, headBase + wingAxis * (m_arrowHeadLengthInMeters * 0.55f));
        m_headLine.SetPosition(1, to);
        m_headLine.SetPosition(2, headBase - wingAxis * (m_arrowHeadLengthInMeters * 0.55f));
    }

    private void CreateRing(IGlyphAimTarget target)
    {
        GameObject ringObject = new GameObject($"AimRing_{target.DisplayName}");
        ringObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
        renderer.sprite = s_ringSprite;
        renderer.color = m_ringColor;
        renderer.sortingOrder = m_sortingOrder;

        float radius = Mathf.Max(0.1f, target.RingRadiusInMeters * m_ringRadiusScale);
        float diameter = radius * 2f;
        ringObject.transform.localScale = new Vector3(diameter, diameter, 1f);

        m_rings[target] = new RingEntry
        {
            Renderer = renderer,
            Radius = radius,
            IsAnimating = false
        };
    }

    private void DestroyRingEntry(RingEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.ScaleTween?.Kill();

        if (entry.Renderer != null)
        {
            Destroy(entry.Renderer.gameObject);
        }
    }

    // 保留指定键对应的圆环，其余从字典里剔除。
    // 用"重建"而不是逐个 Remove，是因为被销毁的 Unity 对象作为键时 Remove 行为不可靠。
    private void RebuildDictionary(List<IGlyphAimTarget> keepKeys)
    {
        m_allKeys.Clear();
        m_allKeys.AddRange(m_rings.Keys);

        m_keptPairs.Clear();
        foreach (IGlyphAimTarget key in m_allKeys)
        {
            if (key == null || !keepKeys.Contains(key))
            {
                continue;
            }

            if (m_rings.TryGetValue(key, out RingEntry entry))
            {
                m_keptPairs.Add(new KeyValuePair<IGlyphAimTarget, RingEntry>(key, entry));
            }
        }

        m_rings.Clear();
        foreach (KeyValuePair<IGlyphAimTarget, RingEntry> pair in m_keptPairs)
        {
            m_rings[pair.Key] = pair.Value;
        }
    }

    private void CreateArrowLines()
    {
        m_shaftLine = CreateLine("AimArrowShaft");
        m_headLine = CreateLine("AimArrowHead");
        m_shaftLine.enabled = false;
        m_headLine.enabled = false;
    }

    private LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = m_arrowWidthInMeters;
        line.endWidth = m_arrowWidthInMeters;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.material = s_lineMaterial;
        line.startColor = m_arrowColor;
        line.endColor = m_arrowColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = m_sortingOrder + 2;

        return line;
    }

    private static void EnsureResources()
    {
        if (s_ringSprite == null)
        {
            s_ringSprite = BuildRingSprite();
        }

        if (s_lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                s_lineMaterial = new Material(shader);
            }
            else
            {
                Debug.LogWarning($"[{nameof(GlyphAimVisuals)}] 找不到可用 Shader，箭头可能不显示。");
            }
        }
    }

    // 程序化生成一个环形贴图：中心透明，外圈一道实线
    private static Sprite BuildRingSprite()
    {
        int size = Mathf.RoundToInt(k_ringTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        float outerRadius = half - 1f;
        float innerRadius = outerRadius - k_ringThicknessPixels;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // 环内外各留 1 像素做抗锯齿过渡
                float alpha = Mathf.Clamp01(Mathf.Min(distance - innerRadius, outerRadius - distance));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // pixelsPerUnit = size 让贴图在世界里正好 1x1 米，缩放即直径
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
