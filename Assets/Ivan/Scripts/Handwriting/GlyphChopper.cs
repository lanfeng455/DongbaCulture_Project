using System.Collections.Generic;
using UnityEngine;

// Handwriting-triggered chopping: writing the glyph (default 斧) fells a few
// random living trees in the scene. Damage flows through AttackableEntity.
// ApplyHit — the project's single damage entry — so hit flash, loot drops
// (sticks), delayed destroy and fertility un-registration all reuse the
// existing combat pipeline with zero new per-tree logic.
//
// Setup: place next to the HandwritingPanel UI object, teach the glyph in the
// panel's record mode, tune the chop count range.
public class GlyphChopper : MonoBehaviour
{
    [Tooltip("触发砍伐的字名（与录入模板时的字名一致），复合字形如「斧砍树」作为一个整体模板录入")]
    [SerializeField] private string m_glyphName = "斧砍树";

    [Tooltip("每次砍伐数量下限")]
    [SerializeField] private int m_minChops = 2;

    [Tooltip("每次砍伐数量上限")]
    [SerializeField] private int m_maxChops = 4;

    [Tooltip("单次伤害（≥树的血量即一击砍倒；默认 999 = 必倒）")]
    [SerializeField] private int m_damage = 999;

    private HandwritingPanel m_panel;

    private void OnEnable()
    {
        m_panel = FindFirstObjectByType<HandwritingPanel>();
        if (m_panel != null)
        {
            m_panel.GlyphRecognized += HandleGlyphRecognized;
        }
        else
        {
            Debug.LogWarning("[GlyphChopper] No HandwritingPanel in scene — chopping stays offline.", this);
        }
    }

    private void OnDisable()
    {
        if (m_panel != null)
        {
            m_panel.GlyphRecognized -= HandleGlyphRecognized;
        }
    }

    private void HandleGlyphRecognized(string glyphName, float confidence)
    {
        if (string.IsNullOrEmpty(m_glyphName) || m_glyphName.Trim() != glyphName)
        {
            return;
        }

        ChopRandomTrees();
    }

    private void ChopRandomTrees()
    {
        // Both hand-placed and spawner-grown trees count: anything alive in
        // the scene is fair game for the axe
        TreeEntity[] trees = FindObjectsByType<TreeEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        List<TreeEntity> alive = new List<TreeEntity>(trees.Length);
        foreach (TreeEntity tree in trees)
        {
            if (tree.IsAlive)
            {
                alive.Add(tree);
            }
        }

        if (alive.Count == 0)
        {
            Debug.Log("[GlyphChopper] 场景里没有可砍的树。");
            return;
        }

        // Fisher-Yates shuffle, then take the first N — uniform random pick
        int chopCount = Mathf.Min(Random.Range(m_minChops, m_maxChops + 1), alive.Count);
        for (int i = alive.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (alive[i], alive[j]) = (alive[j], alive[i]);
        }

        for (int i = 0; i < chopCount; i++)
        {
            // Hit point at the tree root: enough for the current flash feedback
            alive[i].ApplyHit(m_damage, alive[i].transform.position);
        }

        Debug.Log($"[GlyphChopper] 「{m_glyphName}」砍倒了 {chopCount} 棵树。");
    }
}
