using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// Bendable stream ribbon: builds a triangle-strip mesh through child control points
// so the water can drop out of the spring hole, follow the slope, and dive under the
// pond surface. Add/move the child points to shape the path; the mesh rebuilds live
// in the editor. UV.y carries arc length (the shader scrolls along it), UV2.x carries
// the normalized 0..1 position along the whole path (the shader's end-foam bands).
// Keep this transform's scale at 1 — width lives here, not on the transform.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StreamRibbon : MonoBehaviour
{
    [Tooltip("Control points along the flow, top to bottom. Usually the child transforms of this object")]
    [SerializeField] private Transform[] m_points;

    [Tooltip("Ribbon width in meters")]
    [SerializeField] private float m_width = 0.5f;

    [Tooltip("Mesh subdivisions between neighbouring points; higher follows tight bends more smoothly")]
    [SerializeField] private int m_subdivisions = 6;

    [Header("Flow Reveal")]
    [Tooltip("Play the source-to-pond reveal animation automatically each time the ribbon is enabled")]
    [SerializeField] private bool m_autoFillOnEnable = true;

    [Tooltip("Seconds for the full path to fill from the spring hole down to the pond")]
    [SerializeField] private float m_fillDuration = 2.2f;

    [Tooltip("Width of the soft reveal band (normalized path units). Larger = fuzzier head")]
    [SerializeField] private float m_fillBand = 0.08f;

    private Mesh m_mesh;
    private readonly List<Vector3> m_lastPointPositions = new List<Vector3>();
    private float m_lastWidth = -1f;
    private int m_lastSubdivisions = -1;

    private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_propertyBlock;
    private CancellationTokenSource m_revealCancellation;

    private static readonly int k_flowFillId = Shader.PropertyToID("_FlowFill");
    private static readonly int k_flowBandId = Shader.PropertyToID("_FlowBand");

    private void OnEnable()
    {
        Rebuild();
        if (m_autoFillOnEnable && Application.isPlaying)
        {
            // Hide immediately so the first frame shows nothing, then reveal
            ApplyFillImmediate(0.0f);
            RunRevealAsync();
        }
        else
        {
            // Editor or manual mode: show the full ribbon so placement is easy
            ApplyFillImmediate(1.0f);
        }
    }

    private void OnDisable()
    {
        // Cancel any in-flight reveal so re-enabling restarts cleanly
        if (m_revealCancellation != null)
        {
            m_revealCancellation.Cancel();
            m_revealCancellation.Dispose();
            m_revealCancellation = null;
        }
    }

    private void OnValidate()
    {
        // Inspector edits (width, subdivisions, reassigned points) rebuild immediately
        Rebuild();
    }

    private void Update()
    {
        // Live feedback while dragging points in the Scene view; no-op at runtime
#if UNITY_EDITOR
        if (!Application.isPlaying && IsDirty())
        {
            Rebuild();
        }
#endif
    }

    private bool IsDirty()
    {
        if (m_points == null)
        {
            return false;
        }
        if (m_width != m_lastWidth || m_subdivisions != m_lastSubdivisions
            || m_lastPointPositions.Count != m_points.Length)
        {
            return true;
        }

        for (int i = 0; i < m_points.Length; i++)
        {
            if (m_points[i] == null)
            {
                return true;
            }
            if (m_points[i].localPosition != m_lastPointPositions[i])
            {
                return true;
            }
        }
        return false;
    }

    public void Rebuild()
    {
        m_lastWidth = m_width;
        m_lastSubdivisions = m_subdivisions;
        m_lastPointPositions.Clear();
        if (m_points != null)
        {
            foreach (Transform point in m_points)
            {
                m_lastPointPositions.Add(point != null ? point.localPosition : Vector3.zero);
            }
        }

        if (m_points == null || m_points.Length < 2)
        {
            SetMesh(null);
            return;
        }

        m_subdivisions = Mathf.Max(1, m_subdivisions);

        // Sample the Catmull-Rom spline through all control points
        var samples = new List<Vector3>();
        for (int i = 0; i < m_points.Length - 1; i++)
        {
            Vector3 p0 = LocalPos(Mathf.Max(0, i - 1));
            Vector3 p1 = LocalPos(i);
            Vector3 p2 = LocalPos(i + 1);
            Vector3 p3 = LocalPos(Mathf.Min(m_points.Length - 1, i + 2));

            for (int step = 0; step < m_subdivisions; step++)
            {
                float t = step / (float)m_subdivisions;
                samples.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        samples.Add(LocalPos(m_points.Length - 1));

        // Accumulate arc length first so UVs and the 0..1 fraction stay truthful
        var arc = new float[samples.Count];
        for (int i = 1; i < samples.Count; i++)
        {
            arc[i] = arc[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
        }
        float totalLength = Mathf.Max(arc[samples.Count - 1], 1e-4f);

        int count = samples.Count;
        var verts = new Vector3[count * 2];
        var normals = new Vector3[count * 2];
        var uv = new Vector2[count * 2];
        var uv2 = new Vector2[count * 2];
        var triangles = new int[(count - 1) * 6];

        Vector3 side = Vector3.right;
        for (int i = 0; i < count; i++)
        {
            Vector3 prev = samples[Mathf.Max(0, i - 1)];
            Vector3 next = samples[Mathf.Min(count - 1, i + 1)];
            Vector3 tangent = next - prev;

            // Horizontal side vector; when the ribbon runs vertical the cross product
            // degenerates, so keep the last good side to face through the drop
            if (tangent.sqrMagnitude > 1e-8f)
            {
                Vector3 candidate = Vector3.Cross(Vector3.up, tangent.normalized);
                if (candidate.sqrMagnitude > 1e-4f)
                {
                    side = candidate.normalized;
                }
            }
            Vector3 normal = Vector3.Cross(tangent.normalized, side).normalized;

            Vector3 half = side * (m_width * 0.5f);
            int vi = i * 2;
            verts[vi] = samples[i] - half;
            verts[vi + 1] = samples[i] + half;
            normals[vi] = normal;
            normals[vi + 1] = normal;
            uv[vi] = new Vector2(0f, arc[i]);
            uv[vi + 1] = new Vector2(1f, arc[i]);
            float frac = arc[i] / totalLength;
            uv2[vi] = new Vector2(frac, 0f);
            uv2[vi + 1] = new Vector2(frac, 0f);
        }

        for (int i = 0; i < count - 1; i++)
        {
            int vi = i * 2;
            int ti = i * 6;
            triangles[ti] = vi;
            triangles[ti + 1] = vi + 3;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        if (m_mesh == null)
        {
            m_mesh = new Mesh { name = "StreamRibbon" };
        }
        m_mesh.Clear();
        m_mesh.vertices = verts;
        m_mesh.normals = normals;
        m_mesh.uv = uv;
        m_mesh.uv2 = uv2;
        m_mesh.triangles = triangles;
        m_mesh.RecalculateBounds();
        SetMesh(m_mesh);
    }

    private Vector3 LocalPos(int index)
    {
        return m_points[index] != null ? m_points[index].localPosition : Vector3.zero;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void SetMesh(Mesh mesh)
    {
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void EnsureBlock()
    {
        if (m_renderer == null)
        {
            m_renderer = GetComponent<MeshRenderer>();
        }
        if (m_propertyBlock == null)
        {
            m_propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ApplyFillImmediate(float fill)
    {
        EnsureBlock();
        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetFloat(k_flowFillId, fill);
        m_propertyBlock.SetFloat(k_flowBandId, m_fillBand);
        m_renderer.SetPropertyBlock(m_propertyBlock);
    }

    // Runs the reveal from source (fill 0) to pond (fill 1). Uses Unity 6's
    // Awaitable + CancellationToken so it cooperates with scene loads and object
    // disablements — OnDisable cancels and re-enabling restarts cleanly.
    private async void RunRevealAsync()
    {
        m_revealCancellation = new CancellationTokenSource();
        CancellationToken token = m_revealCancellation.Token;

        try
        {
            // Start fully hidden; OnEnable already set fill=1, so override to 0 first
            ApplyFillImmediate(0.0f);

            float duration = Mathf.Max(m_fillDuration, 0.01f);
            float t = 0f;
            while (t < duration)
            {
                if (token.IsCancellationRequested || !this || !isActiveAndEnabled)
                {
                    return;
                }

                // Ease-out cubic so the water picks up speed then glides into place
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                ApplyFillImmediate(eased);

                await Awaitable.NextFrameAsync(token);
                t += Time.deltaTime;
            }

            ApplyFillImmediate(1.0f);
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // 正常取消：OnDisable cancel token 时 await 会抛这个，静默吞掉
        }
    }
}
