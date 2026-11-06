using System.Collections.Generic;
using UnityEngine;

// 融合配方库：按顺序查表，第一条命中的配方生效。
// 没命中任何配方 = NoMatch（手里的字原样弹回）。
[CreateAssetMenu(fileName = "FusionDatabase", menuName = "Dongba/Fusion Database", order = 1)]
public class DongbaFusionDatabase : ScriptableObject
{
    [SerializeField] private List<DongbaFusionRecipe> m_recipes = new List<DongbaFusionRecipe>();

    public bool TryFind(string heldGlyph, string targetGlyph, out DongbaFusionRecipe recipe)
    {
        recipe = null;

        if (string.IsNullOrEmpty(heldGlyph) || string.IsNullOrEmpty(targetGlyph))
        {
            return false;
        }

        foreach (DongbaFusionRecipe candidate in m_recipes)
        {
            if (candidate != null && candidate.Matches(heldGlyph, targetGlyph))
            {
                recipe = candidate;
                return true;
            }
        }

        return false;
    }

    // 让编辑器工具或运行时代码可以追加配方
    public void AddRecipe(DongbaFusionRecipe recipe)
    {
        if (recipe != null && !m_recipes.Contains(recipe))
        {
            m_recipes.Add(recipe);
        }
    }
}
