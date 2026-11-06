using System.Collections.Generic;
using UnityEngine;

// Local fertility field over the ground plane: a sparse spatial grid where
// every registered plant contributes to its cell, and sampling any point
// blends the 3x3 neighbourhood with a small gaussian-like kernel (center
// weighs 4, edges 2, corners 1 — sum 16). Result is normalized to [0, 1]
// against m_maxPlantsPerCell so gameplay thresholds stay meaningful.
//
// Why a grid instead of summing distances to every plant: O(1) sampling no
// matter how many plants exist, trivially debuggable with Gizmos, and future
// systems (animals, water) can register/unregister into the same field.
public class FertilityField : MonoBehaviour
{
    [Tooltip("格子边长（米），越小越局部、格子越多")]
    [SerializeField] private float m_cellSize = 2f;

    [Tooltip("每格植物数达到该值时，中心采样环境值 = 1.0（值越小花越容易出现）")]
    [SerializeField] private int m_maxPlantsPerCell = 2;

    [Tooltip("Scene 视图画出非零格子与环境值（调试用）")]
    [SerializeField] private bool m_drawGizmos = false;

    // Sparse storage: only cells with plants exist
    private readonly Dictionary<Vector2Int, int> m_cells = new Dictionary<Vector2Int, int>();

    public void Register(Vector3 position)
    {
        Vector2Int cell = CellOf(position);
        m_cells.TryGetValue(cell, out int count);
        m_cells[cell] = count + 1;
    }

    // Clamped at zero: un-registering more than registered is a no-op
    public void Unregister(Vector3 position)
    {
        Vector2Int cell = CellOf(position);
        if (!m_cells.TryGetValue(cell, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            m_cells.Remove(cell);
        }
        else
        {
            m_cells[cell] = count - 1;
        }
    }

    // Blended neighbourhood sample in [0, 1]
    public float Sample(Vector3 position)
    {
        Vector2Int center = CellOf(position);

        // Kernel: 1 2 1 / 2 4 2 / 1 2 1 — centre dominant, smooth falloff
        int[] rowWeights = { 1, 2, 1 };
        float weighted = 0f;
        const int k_kernelSum = 16;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!m_cells.TryGetValue(new Vector2Int(center.x + dx, center.y + dz), out int count))
                {
                    continue;
                }

                weighted += count * rowWeights[dx + 1] * rowWeights[dz + 1];
            }
        }

        return Mathf.Clamp01(weighted / (k_kernelSum * m_maxPlantsPerCell));
    }

    private Vector2Int CellOf(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / m_cellSize),
            Mathf.FloorToInt(position.z / m_cellSize));
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!m_drawGizmos || Application.isPlaying && m_cells.Count == 0)
        {
            return;
        }

        Gizmos.color = new Color(0.06f, 0.86f, 0.47f, 0.35f);
        float y = transform.position.y + 0.05f;

        foreach (KeyValuePair<Vector2Int, int> cell in m_cells)
        {
            Vector3 center = new Vector3(
                (cell.Key.x + 0.5f) * m_cellSize, y, (cell.Key.y + 0.5f) * m_cellSize);
            Vector3 size = new Vector3(m_cellSize, 0f, m_cellSize);

            // Alpha scales with how full the cell is for an at-a-glance heatmap
            Gizmos.color = new Color(0.06f, 0.86f, 0.47f, 0.15f + 0.55f * Mathf.Clamp01(cell.Value / (float)m_maxPlantsPerCell));
            Gizmos.DrawCube(center, size);
        }
    }
#endif
}
