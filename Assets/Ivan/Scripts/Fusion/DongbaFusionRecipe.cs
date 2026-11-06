using UnityEngine;
using UnityEngine.Events;

// 融合结果类型
public enum FusionOutcome
{
    // 不匹配：手里的字原样飞回
    NoMatch,

    // 文本改写：手里的字变成新字后飞回（例：石 + 白 → 白石）
    RewriteHeld,

    // 文本改写：场景里的目标字变成新字，手里的字飞回
    RewriteTarget,

    // 其他（待定）：交给配方上的 UnityEvent 自己处理，手里的字照常飞回
    Custom
}

// 一条融合配方：手中字 + 场景字 → 结果。
// 作为资产配置，加新组合不需要改代码。
[CreateAssetMenu(fileName = "FusionRecipe", menuName = "Dongba/Fusion Recipe", order = 0)]
public class DongbaFusionRecipe : ScriptableObject
{
    [Header("输入")]
    [Tooltip("玩家手中那个字（如 石）")]
    [SerializeField] private string m_heldGlyph;

    [Tooltip("场景中被瞄准的那个字（如 白）")]
    [SerializeField] private string m_targetGlyph;

    [Header("结果")]
    [SerializeField] private FusionOutcome m_outcome = FusionOutcome.RewriteHeld;

    [Tooltip("改写后的文字（如 白石）；NoMatch / Custom 时可留空")]
    [SerializeField] private string m_resultGlyph;

    [Tooltip("改写后的贴图；留空表示只改文字语义、不动图")]
    [SerializeField] private Sprite m_resultSprite;

    [Tooltip("仅 Custom 分支触发，用于播放特效、发信道信号等")]
    [SerializeField] private UnityEvent m_onFused;

    public string HeldGlyph => m_heldGlyph;
    public string TargetGlyph => m_targetGlyph;
    public FusionOutcome Outcome => m_outcome;
    public string ResultGlyph => m_resultGlyph;
    public Sprite ResultSprite => m_resultSprite;

    // 双向匹配：配置顺序写反了也能命中
    public bool Matches(string heldGlyph, string targetGlyph)
    {
        bool forward = heldGlyph == m_heldGlyph && targetGlyph == m_targetGlyph;
        bool backward = heldGlyph == m_targetGlyph && targetGlyph == m_heldGlyph;
        return forward || backward;
    }

    public void InvokeFused()
    {
        m_onFused?.Invoke();
    }
}
