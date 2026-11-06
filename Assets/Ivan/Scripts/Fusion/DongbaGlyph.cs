using UnityEngine;

// 场景里的"可组合文字"：挂在任意带字形 SpriteRenderer 的物体上，
// 声明它当前代表哪个字、是否可以参与融合。
//
// 自己负责在 OnEnable/OnDisable 时进出全局注册表，瞄准系统因此不需要
// 在场景里找对象，直接遍历注册表即可（房间切换、物体显隐都自动跟上）。
[DisallowMultipleComponent]
public class DongbaGlyph : MonoBehaviour, IGlyphAimTarget
{
    [Header("文字")]
    [Tooltip("当前文字（如 白、木）；融合改写后这里也会同步更新")]
    [SerializeField] private string m_glyphWord = "白";

    [Tooltip("取消勾选后，瞄准时不会被选中，也不显示圈")]
    [SerializeField] private bool m_isCombinable = true;

    [Header("显示")]
    [Tooltip("字形渲染器；留空时自动在子物体里找")]
    [SerializeField] private SpriteRenderer m_glyphRenderer;

    [Tooltip("瞄准时这个字周围的圈半径（米），按目标字形大小调")]
    [SerializeField] private float m_ringRadiusInMeters = 0.9f;

    // 融合时允许把结果写回场景文字（RewriteTarget 分支用）
    public string GlyphWord => m_glyphWord;

    // 瞄准系统走这个统一入口；对文字而言，可组合即可瞄准
    public bool IsAimable => m_isCombinable && isActiveAndEnabled;

    public string DisplayName => m_glyphWord;
    public float RingRadiusInMeters => m_ringRadiusInMeters;
    public SpriteRenderer GlyphRenderer => m_glyphRenderer;
    public Vector3 WorldPosition => transform.position;

    private void Awake()
    {
        ResolveRenderer();
    }

    private void OnEnable()
    {
        DongbaGlyphRegistry.Register(this);
    }

    private void OnDisable()
    {
        DongbaGlyphRegistry.Unregister(this);
    }

    // 融合结果改写场景文字时调用
    public void ApplyGlyphWord(string word, Sprite sprite)
    {
        m_glyphWord = word;

        if (sprite != null && m_glyphRenderer != null)
        {
            m_glyphRenderer.sprite = sprite;
        }
    }

    private void ResolveRenderer()
    {
        if (m_glyphRenderer == null)
        {
            m_glyphRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Reset()
    {
        ResolveRenderer();
    }
}
