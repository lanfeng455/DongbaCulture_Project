using System;
using UnityEngine;

// One row of the vegetation spawn table: which glyph name spawns which set of
// prefabs and with what cluster shape. Kept as plain serialized config so the
// spawner stays data-driven — "草""花""石头" or future glyphs are Inspector
// rows, not code.
[Serializable]
public class GlyphVegetationRule
{
    [Tooltip("识别出的字名（如：树 / 草），与录入模板时的字名一致")]
    public string glyphName;

    [Tooltip("候选预制体（随机挑一个），如低/中/高树三种")]
    public GameObject[] prefabs;

    [Header("Cluster Shape 聚簇形状")]
    [Tooltip("簇数下限")]
    public int minClusters = 1;

    [Tooltip("簇数上限")]
    public int maxClusters = 2;

    [Tooltip("每簇生成数量")]
    public int plantsPerCluster = 5;

    [Tooltip("高斯 σ（米）：树群离散度，视觉簇半径约为 2 倍 σ")]
    public float clusterSigma = 2.2f;

    [Tooltip("与已生成植物的最小间距（米），重叠候选会被丢弃")]
    public float minSpacing = 1.4f;

    [Header("Fertility 环境值联动")]
    [Tooltip("是否计入局部环境值（花通常不计入）")]
    public bool countsTowardFertility = true;

    [Tooltip("附花预制体（留空 = 该字不附带花生效）")]
    public GameObject flowerPrefab;
}
