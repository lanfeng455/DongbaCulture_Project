using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Turns recognized handwriting into clustered vegetation: subscribes to the
// HandwritingPanel and, per glyph rule, scatters plant clusters inside a spawn
// area using a parent-point / child-point cluster process (random cluster
// centers + 2D gaussian offsets), enforces minimum spacing between spawned
// plants, raycasts each candidate down onto the ground, then optionally rolls
// a "bonus flower" for the whole batch based on the best fertility among the
// landing spots (batch-level roll matches the design "whenever something is
// created near fertile ground, a flower may pop up").
//
// All generated instances are parented under a single lazily-created
// "GeneratedVegetation" container beneath this component so the Hierarchy
// stays clean and chopping/scene cleanup keeps working through normal
// Destroy calls.
//
// Setup: place next to the HandwritingPanel UI object, drag plant prefabs
// (低树/中树/高树/草/花) into the default rules, assign a spawn area center.
public class VegetationSpawner : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("生成区中心（场景里放一个空物体即可）")]
    [SerializeField] private Transform m_spawnAreaCenter;

    [Tooltip("生成区半径（米），簇心与散点都被钳制在圆内")]
    [SerializeField] private float m_spawnAreaRadius = 12f;

    [Header("Fertility")]
    [Tooltip("环境值场（留空 = 自动查找）")]
    [SerializeField] private FertilityField m_fertilityField;

    [Tooltip("附花概率系数：p = 本批最高环境值 × 该系数（可大于 1 提高出花率）")]
    [SerializeField] private float m_flowerChanceFactor = 1f;

    [Tooltip("花在植物旁的偏移半径（米）")]
    [SerializeField] private float m_flowerOffsetRadius = 0.6f;

    [Header("Rules")]
    [Tooltip("字名 → 植物 聚类生成规则表")]
    [SerializeField] private List<GlyphVegetationRule> m_rules = new List<GlyphVegetationRule>
    {
        new() { glyphName = "树", minClusters = 1, maxClusters = 1, plantsPerCluster = 3, clusterSigma = 2.2f, minSpacing = 1.4f },
        new() { glyphName = "草", minClusters = 2, maxClusters = 3, plantsPerCluster = 6, clusterSigma = 1.4f, minSpacing = 0.8f, flowerPrefab = null }
    };

    [Header("Presentation")]
    [Tooltip("长出动画时长（秒，0 = 无动画）")]
    [SerializeField] private float m_growDuration = 0.45f;

    [Tooltip("从多高处向下探测地面")]
    [SerializeField] private float m_groundProbeHeight = 10f;

    [Tooltip("生成时打印结果与附花 roll（开发期可见反馈，发布可关）")]
    [SerializeField] private bool m_logSpawns = true;

    private const string k_containerName = "GeneratedVegetation";

    private readonly List<Transform> m_spawnedPlants = new List<Transform>();
    private HandwritingPanel m_panel;
    private Transform m_container;

    // One spawned plant with the fertility observed at its landing spot
    private struct SpawnedPlant
    {
        public Transform plant;
        public Vector3 position;
        public float fertility;
    }

    private void OnEnable()
    {
        if (m_fertilityField == null)
        {
            m_fertilityField = FindFirstObjectByType<FertilityField>();
        }

        m_panel = FindFirstObjectByType<HandwritingPanel>();
        if (m_panel != null)
        {
            m_panel.GlyphRecognized += HandleGlyphRecognized;
        }
        else
        {
            Debug.LogWarning("[VegetationSpawner] No HandwritingPanel in scene — spawning stays offline.", this);
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
        GlyphVegetationRule rule = FindRule(glyphName);
        if (rule == null)
        {
            return; // not a vegetation glyph: environment reactions handle it
        }

        if (rule.prefabs == null || rule.prefabs.Length == 0)
        {
            Debug.LogWarning($"[VegetationSpawner] 「{glyphName}」规则没有配置预制体，请在 Inspector 拖入。", this);
            return;
        }

        List<SpawnedPlant> batch = new List<SpawnedPlant>();
        SpawnClusters(rule, batch);

        if (m_logSpawns)
        {
            Debug.Log($"[VegetationSpawner] 「{glyphName}」长出 {batch.Count} 株植物。" +
                (batch.Count > 0 ? $"环境值峰值 {GetBestFertility(batch):F2}。" : ""));
        }

        RollBatchFlower(rule, batch);
    }

    private GlyphVegetationRule FindRule(string glyphName)
    {
        foreach (GlyphVegetationRule rule in m_rules)
        {
            if (!string.IsNullOrEmpty(rule.glyphName) && rule.glyphName.Trim() == glyphName)
            {
                return rule;
            }
        }

        return null;
    }

    // --- Cluster process ----------------------------------------------------

    private void SpawnClusters(GlyphVegetationRule rule, List<SpawnedPlant> batch)
    {
        int clusterCount = Random.Range(rule.minClusters, rule.maxClusters + 1);

        for (int c = 0; c < clusterCount; c++)
        {
            Vector3 clusterCenter = RandomPointInArea();
            for (int i = 0; i < rule.plantsPerCluster; i++)
            {
                // 2D gaussian offset around the cluster center (parent-point /
                // child-point cluster process): most children land within 2σ
                Vector2 offset = Gaussian2D() * rule.clusterSigma;
                Vector3 candidate = clusterCenter + new Vector3(offset.x, 0f, offset.y);

                if (!IsInsideArea(candidate) || !HasEnoughSpacing(candidate, rule.minSpacing))
                {
                    continue;
                }

                TrySpawnPlant(rule, candidate, batch);
            }
        }
    }

    private void TrySpawnPlant(GlyphVegetationRule rule, Vector3 position, List<SpawnedPlant> batch)
    {
        if (!TryResolveGroundY(position, out float groundY))
        {
            return; // no ground under this candidate — drop it
        }

        Vector3 spawnPosition = new Vector3(position.x, groundY, position.z);
        GameObject prefab = rule.prefabs[Random.Range(0, rule.prefabs.Length)];
        GameObject plant = Instantiate(prefab, spawnPosition, Quaternion.identity, Container);

        // Strip Unity's "(Clone)" suffix so the hierarchy reads like design intent
        plant.name = prefab.name;
        m_spawnedPlants.Add(plant.transform);

        // Sample fertility BEFORE registering the new plant: the plant itself
        // shouldn't inflate the flower odds of its own spawn
        float fertility = m_fertilityField != null ? m_fertilityField.Sample(spawnPosition) : 0f;

        if (rule.countsTowardFertility && m_fertilityField != null)
        {
            m_fertilityField.Register(spawnPosition);
            FertilityRegistration.Attach(plant, m_fertilityField);
        }

        GrowIn(plant.transform);
        batch.Add(new SpawnedPlant { plant = plant.transform, position = spawnPosition, fertility = fertility });
    }

    // Batch-level flower roll: one roll per recognized glyph using the best
    // fertility among this batch's landing spots. Fewer, more legible rolls
    // than per-plant odds ("this stroke of writing either blooms or not").
    private void RollBatchFlower(GlyphVegetationRule rule, List<SpawnedPlant> batch)
    {
        if (rule.flowerPrefab == null || batch.Count == 0)
        {
            return;
        }

        float bestFertility = GetBestFertility(batch);
        if (bestFertility <= 0f)
        {
            if (m_logSpawns)
            {
                Debug.Log("[VegetationSpawner] 附花未触发：本批落点环境值为 0（空地第一次种树不长花，种密后再写就会开）。");
            }

            return;
        }

        float chance = Mathf.Clamp01(bestFertility * m_flowerChanceFactor);
        bool bloomed = Random.value <= chance;

        if (m_logSpawns)
        {
            Debug.Log($"[VegetationSpawner] 附花 roll：p={chance:F2}（峰值 {bestFertility:F2} × 系数 {m_flowerChanceFactor:F1}）→ {(bloomed ? "命中 🌸" : "未命中")}");
        }

        if (bloomed)
        {
            SpawnFlower(rule.flowerPrefab, GetMostFertileSpot(batch));
        }
    }

    private static float GetBestFertility(List<SpawnedPlant> batch)
    {
        float best = 0f;
        foreach (SpawnedPlant plant in batch)
        {
            best = Mathf.Max(best, plant.fertility);
        }

        return best;
    }

    private static Vector3 GetMostFertileSpot(List<SpawnedPlant> batch)
    {
        SpawnedPlant best = batch[0];
        foreach (SpawnedPlant plant in batch)
        {
            if (plant.fertility > best.fertility)
            {
                best = plant;
            }
        }

        return best.position;
    }

    private void SpawnFlower(GameObject flowerPrefab, Vector3 nearPosition)
    {
        Vector2 offset = Random.insideUnitCircle * m_flowerOffsetRadius;
        Vector3 flowerPosition = nearPosition + new Vector3(offset.x, 0f, offset.y);

        if (!TryResolveGroundY(flowerPosition, out float groundY))
        {
            return;
        }

        flowerPosition.y = groundY;
        GameObject flower = Instantiate(flowerPrefab, flowerPosition, Quaternion.identity, Container);
        flower.name = flowerPrefab.name;
        GrowIn(flower.transform);

        // Flowers never register into the fertility field (design decision:
        // prevents the flower → fertility → more-flower snowball)
    }

    // Lazy singleton container under this component — created once, reused for
    // every later spawn so the scene never accumulates duplicate roots
    private Transform Container
    {
        get
        {
            if (m_container == null)
            {
                GameObject go = new GameObject(k_containerName);
                go.transform.SetParent(transform, false);
                m_container = go.transform;
            }

            return m_container;
        }
    }

    // Pop-from-ground reveal: scale 0 → original with a slight overshoot.
    // Reads the prefab's own localScale so user-authored sizes are preserved.
    private void GrowIn(Transform plant)
    {
        if (m_growDuration <= 0f)
        {
            return;
        }

        Vector3 targetScale = plant.localScale;
        plant.localScale = Vector3.zero;
        plant.DOScale(targetScale, m_growDuration).SetEase(Ease.OutBack);
    }

    // --- Geometry helpers ---------------------------------------------------

    // Uniform point inside the spawn disc (sqrt for area-uniform distribution)
    private Vector3 RandomPointInArea()
    {
        Vector3 center = m_spawnAreaCenter != null ? m_spawnAreaCenter.position : transform.position;
        Vector2 disc = Random.insideUnitCircle * m_spawnAreaRadius;
        return center + new Vector3(disc.x, 0f, disc.y);
    }

    private bool IsInsideArea(Vector3 position)
    {
        Vector3 center = m_spawnAreaCenter != null ? m_spawnAreaCenter.position : transform.position;
        Vector2 delta = new Vector2(position.x - center.x, position.z - center.z);
        return delta.sqrMagnitude <= m_spawnAreaRadius * m_spawnAreaRadius;
    }

    // Prunes destroyed plants while checking distance, so the list self-cleans
    private bool HasEnoughSpacing(Vector3 position, float minSpacing)
    {
        float squaredSpacing = minSpacing * minSpacing;

        for (int i = m_spawnedPlants.Count - 1; i >= 0; i--)
        {
            Transform plant = m_spawnedPlants[i];
            if (plant == null)
            {
                m_spawnedPlants.RemoveAt(i);
                continue;
            }

            Vector3 delta = plant.position - position;
            delta.y = 0f; // spacing is a ground-plane concern only
            if (delta.sqrMagnitude < squaredSpacing)
            {
                return false;
            }
        }

        return true;
    }

    // Probe from above like AttackableEntity.ResolveGroundY: the candidate
    // plant doesn't exist yet, so any hit is valid ground
    private bool TryResolveGroundY(Vector3 position, out float groundY)
    {
        Vector3 origin = position + Vector3.up * m_groundProbeHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, m_groundProbeHeight * 2f))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        Debug.LogWarning($"[VegetationSpawner] No ground found under ({position.x:F1}, {position.z:F1}) — candidate dropped.", this);
        return false;
    }

    // Box-Muller: two independent standard gaussian components in one call
    private static Vector2 Gaussian2D()
    {
        float u = Mathf.Max(UnityEngine.Random.value, 1e-6f);
        float v = UnityEngine.Random.value;
        float radius = Mathf.Sqrt(-2f * Mathf.Log(u));
        float angle = 2f * Mathf.PI * v;
        return new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
    }
}
