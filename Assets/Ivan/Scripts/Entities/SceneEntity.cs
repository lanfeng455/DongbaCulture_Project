using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

// Base of every diorama entity. Static + immovable entities are exactly this component;
// dynamics add an EntityStateMachine, movable entities add an IMovable implementation
// (composition instead of a 2x2 inheritance matrix).
//
// This component handles the paper-cut outline behind the user-provided glyph and the
// glyph shadow toggle. Stick placement, glyph position, and scale are entirely user-controlled.
[ExecuteAlways]
public class SceneEntity : MonoBehaviour
{
    [Header("Glyph")]
    [Tooltip("The Dongba glyph sprite renderer placed in the prefab; the outline is generated behind it")]
    [SerializeField] private SpriteRenderer m_glyphRenderer;

    [Tooltip("Paper-cut outline color drawn behind the glyph")]
    [SerializeField] private Color m_outlineColor = new Color(0.93f, 0.89f, 0.8f);

    [Tooltip("Outline copy scale relative to the glyph")]
    [SerializeField] private float m_outlineScale = 1.12f;

    [Tooltip("If true the glyph (and its outline) always face the main camera; the stick keeps its own orientation")]
    [SerializeField] private bool m_billboard = true;

    [Tooltip("If true the glyph casts a directional-light shadow; requires the M_PaperSprite material on the glyph renderer")]
    [SerializeField] protected bool m_castShadows = true;

    [Header("受击反馈")]
    [Tooltip("受击白闪的持续时间（秒）")]
    [SerializeField] private float m_hitFlashDurationInSeconds = 0.18f;

    private static readonly int s_flashAmountId = Shader.PropertyToID("_FlashAmount");

    private SpriteRenderer m_outlineRenderer;
    private Material m_glyphMaterialInstance;
    private Tween m_hitFlashTween;
    private float m_hitFlashAmount;

    // 子类（玩家等）做镜头相对移动/朝向时也需要主镜头，故对子类开放；
    // 注意不要在子类里再声明同名字段，Unity 会报“同名字段被重复序列化”
    protected Transform m_cameraTransform;

    protected virtual void Awake()
    {
        EnsureOutline();
    }

    protected virtual void Start()
    {
        if (Camera.main != null)
        {
            m_cameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !m_billboard || m_cameraTransform == null || m_glyphRenderer == null)
        {
            return;
        }

        m_glyphRenderer.transform.rotation = m_cameraTransform.rotation * Quaternion.Euler(0f, 0f, BillboardRollDegrees);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                EnsureOutline();
            }
        };
    }
#endif

    // Repaint API used by dynamic states (tree seasons) and future recolor effects
    public void SetStickerColor(Color color)
    {
        if (m_glyphRenderer != null)
        {
            m_glyphRenderer.color = color;
        }
    }

    // Visibility toggle for entities that only appear under certain conditions
    // (sun by day, moon by night). The outline must be toggled too — disabling the
    // glyph renderer alone would leave the outline floating behind.
    public void SetStickerVisible(bool visible)
    {
        if (m_glyphRenderer != null)
        {
            m_glyphRenderer.enabled = visible;
        }

        if (m_outlineRenderer != null)
        {
            m_outlineRenderer.enabled = visible;
        }
    }

    public void SetGlyph(Sprite sprite)
    {
        if (m_glyphRenderer != null)
        {
            m_glyphRenderer.sprite = sprite;
        }

        RefreshOutline();
    }

    // 受击白闪：把字形短暂推向白色再回落，靠 PaperSprite.shader 的 _FlashAmount 驱动。
    // 与季节变色（SpriteRenderer.color）互不干扰；受击方（AttackableEntity）调用它。
    public void PlayHitFlash()
    {
        PlayHitFlash(m_hitFlashDurationInSeconds);
    }

    public void PlayHitFlash(float durationInSeconds)
    {
        if (!Application.isPlaying || m_glyphRenderer == null)
        {
            return;
        }

        m_hitFlashTween?.Kill();

        float riseTime = durationInSeconds * 0.25f;
        float fallTime = durationInSeconds * 0.75f;

        // 先快速冲白、再缓慢回落，读起来像“亮一下”
        Sequence sequence = DOTween.Sequence();
        sequence.Append(DOTween.To(() => m_hitFlashAmount, ApplyHitFlashAmount, 1f, riseTime));
        sequence.Append(DOTween.To(() => m_hitFlashAmount, ApplyHitFlashAmount, 0f, fallTime));
        sequence.OnComplete(() =>
        {
            if (this == null)
            {
                return;
            }

            ApplyHitFlashAmount(0f);
        });

        m_hitFlashTween = sequence;
    }

    // 写材质实例而不是共享材质：所有实体共用 M_PaperSprite，写共享材质会让全场景一起闪
    private void ApplyHitFlashAmount(float amount)
    {
        m_hitFlashAmount = amount;

        if (m_glyphRenderer == null)
        {
            return;
        }

        if (m_glyphMaterialInstance == null)
        {
            m_glyphMaterialInstance = m_glyphRenderer.material;
        }

        if (m_glyphMaterialInstance != null)
        {
            m_glyphMaterialInstance.SetFloat(s_flashAmountId, amount);
        }
    }

    private void OnDisable()
    {
        // 被禁用/销毁时把白闪收干净，别让材质停在白色；也顺手停掉无人接管的补间
        m_hitFlashTween?.Kill();
        m_hitFlashTween = null;
        m_hitFlashAmount = 0f;

        if (Application.isPlaying && m_glyphMaterialInstance != null)
        {
            m_glyphMaterialInstance.SetFloat(s_flashAmountId, 0f);
        }
    }

    // 屏幕空间内的自转角（度），叠加在 Billboard 朝向之上：动态实体（如玩家）用它做外观
    // 旋转动画，字形在旋转过程中依然正对镜头。子类在 LateUpdate 之前设置即可。
    protected float BillboardRollDegrees { get; set; }

    // 本实体修饰的字形渲染器；子类用于朝向翻转等操作
    protected SpriteRenderer GlyphRenderer => m_glyphRenderer;

    private void EnsureOutline()
    {
        if (m_glyphRenderer == null)
        {
            return;
        }

        // Reuse the outline by name; never destroy and recreate so it never accumulates
        Transform existing = m_glyphRenderer.transform.Find("Outline");
        if (existing != null)
        {
            m_outlineRenderer = existing.GetComponent<SpriteRenderer>();
        }
        else
        {
            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(m_glyphRenderer.transform, false);
            m_outlineRenderer = outline.AddComponent<SpriteRenderer>();
        }

        ApplyShadowCasting();
        RefreshOutline();
    }

    private void RefreshOutline()
    {
        if (m_glyphRenderer == null || m_outlineRenderer == null)
        {
            return;
        }

        Sprite glyph = m_glyphRenderer.sprite;
        m_outlineRenderer.sprite = glyph;
        m_outlineRenderer.color = m_outlineColor;
        // Stay in the glyph's sorting layer (e.g. Sky vs Default) so ground sprites
        // never cover a sky entity's paper border; the order just dips one below
        m_outlineRenderer.sortingLayerID = m_glyphRenderer.sortingLayerID;
        m_outlineRenderer.sortingOrder = m_glyphRenderer.sortingOrder - 1;
        m_outlineRenderer.transform.localScale = Vector3.one * m_outlineScale;

        // The glyph pivot is usually at the bottom; scaling the outline around that pivot
        // would make the stroke thicker at the top and missing at the bottom. Shift it down
        // so it expands evenly around the glyph's vertical center.
        if (glyph != null)
        {
            float glyphHeight = glyph.bounds.size.y;
            m_outlineRenderer.transform.localPosition = new Vector3(0f, -glyphHeight * (m_outlineScale - 1f) * 0.5f, 0f);
        }
        else
        {
            m_outlineRenderer.transform.localPosition = Vector3.zero;
        }
    }

    // Paper glyphs cast real directional-light shadows when their material has a
    // ShadowCaster pass (M_PaperSprite). The outline is a flat paper border behind
    // the glyph, so it never casts — the shadow keeps the glyph silhouette only.
    private void ApplyShadowCasting()
    {
        if (m_glyphRenderer != null)
        {
            m_glyphRenderer.shadowCastingMode = m_castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
        }

        if (m_outlineRenderer != null)
        {
            m_outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
