using System;
using System.Collections.Generic;
using UnityEngine;

// $P Point-Cloud Recognizer (Vatavu, Anthony & Wobbrock, 2012) adapted for
// multi-stroke glyph handwriting (Dongba glyphs are multi-stroke pictographs).
//
// Why a point-cloud matcher instead of a neural net: "learning" a new glyph is
// just storing a few handwritten templates — instant, no training pipeline,
// no external runtime. Recognition returns a normalized score in [0, 1] that
// works directly as a confidence value for gameplay thresholds.
//
// Pipeline per sample: Resample -> Scale to unit square (aspect preserving)
// -> Translate to centroid. Recognition = weighted greedy nearest-neighbour
// matching between the two normalized point clouds (rotation-sensitive on
// purpose: glyph orientation carries meaning).
public static class PointCloudRecognizer
{
    public const int k_defaultResamplePoints = 32;

    // A single glyph template / candidate already normalized to origin cloud.
    // Kept as plain arrays: recognition runs tight loops over these.
    public struct GlyphCloud
    {
        public string name;
        public Vector2[] points;
    }

    public struct RecognitionCandidate
    {
        public string name;
        public float score;
    }

    // One point of ink with the stroke it belongs to. Resampling must know
    // stroke boundaries so it never interpolates across two separate strokes.
    private struct InkPoint
    {
        public Vector2 pos;
        public int strokeId;
    }

    // Converts raw strokes (one point list per stroke) into a normalized
    // point cloud ready for matching. Returns null when there is not enough
    // ink to form a glyph (guards against accidental taps / dust points).
    public static Vector2[] Normalize(List<List<Vector2>> strokes, int resamplePoints)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return null;
        }

        // Flatten with stroke ids, dropping degenerate strokes (< 2 points)
        List<InkPoint> flat = new List<InkPoint>(64);
        int strokeId = 0;
        bool hasInk = false;

        foreach (List<Vector2> stroke in strokes)
        {
            if (stroke == null || stroke.Count < 2)
            {
                strokeId++;
                continue;
            }

            hasInk = true;
            foreach (Vector2 p in stroke)
            {
                flat.Add(new InkPoint { pos = p, strokeId = strokeId });
            }

            strokeId++;
        }

        if (!hasInk)
        {
            return null;
        }

        InkPoint[] resampled = Resample(flat, resamplePoints);
        ScaleToUnitSquare(resampled);
        TranslateToOrigin(resampled);
        return ToPositions(resampled);
    }

    // Matches the candidate against every template cloud, keeps the best score
    // per glyph name (one glyph may own several templates), returns the top-k
    // candidates sorted by descending score.
    public static List<RecognitionCandidate> Recognize(
        Vector2[] candidate, List<GlyphCloud> templates, int maxResults)
    {
        List<RecognitionCandidate> results = new List<RecognitionCandidate>();

        if (candidate == null || candidate.Length == 0 || templates == null || templates.Count == 0)
        {
            return results;
        }

        foreach (GlyphCloud template in templates)
        {
            if (template.points == null || template.points.Length != candidate.Length)
            {
                continue; // clouds must share resample size to compare 1:1
            }

            float distance = GreedyCloudMatch(candidate, template.points);

            // $P score: map match distance to [0, 1]; 1 = identical clouds
            float score = 1f - distance / (0.5f * Mathf.Sqrt(2f * candidate.Length * candidate.Length));

            results.Add(new RecognitionCandidate { name = template.name, score = score });
        }

        // Best score per glyph name wins (multi-template glyphs)
        results.Sort((a, b) => b.score.CompareTo(a.score));
        List<RecognitionCandidate> deduped = new List<RecognitionCandidate>(results.Count);
        foreach (RecognitionCandidate c in results)
        {
            if (deduped.Exists(d => d.name == c.name))
            {
                continue;
            }

            deduped.Add(c);
            if (deduped.Count >= maxResults)
            {
                break;
            }
        }

        return deduped;
    }

    // --- $P internals -------------------------------------------------------

    // Equal-distance resampling along the ink path. Skips distance accounting
    // across stroke boundaries (no interpolation between separate strokes),
    // matching the reference $P implementation.
    private static InkPoint[] Resample(List<InkPoint> source, int n)
    {
        float interval = PathLength(source) / (n - 1);
        if (interval <= Mathf.Epsilon)
        {
            // Zero-length ink (e.g. repeated identical points): fill with copies
            InkPoint[] flatFill = new InkPoint[n];
            for (int i = 0; i < n; i++)
            {
                flatFill[i] = source[0];
            }

            return flatFill;
        }

        List<InkPoint> src = new List<InkPoint>(source);
        List<InkPoint> dst = new List<InkPoint>(n) { src[0] };
        float accumulated = 0f;

        for (int i = 1; i < src.Count; i++)
        {
            if (src[i].strokeId != src[i - 1].strokeId)
            {
                continue; // stroke switch: not connected, no distance to cover
            }

            float d = Vector2.Distance(src[i - 1].pos, src[i].pos);
            if (accumulated + d >= interval)
            {
                float t = (interval - accumulated) / d;
                Vector2 inserted = src[i - 1].pos + t * (src[i].pos - src[i - 1].pos);
                InkPoint newPoint = new InkPoint { pos = inserted, strokeId = src[i - 1].strokeId };

                dst.Add(newPoint);
                src.Insert(i, newPoint);
                accumulated = 0f;
            }
            else
            {
                accumulated += d;
            }
        }

        // Floating point drift can leave the cloud a few points short — pad
        // with the final point so every cloud has exactly n samples.
        while (dst.Count < n)
        {
            dst.Add(src[src.Count - 1]);
        }

        InkPoint[] result = new InkPoint[n];
        dst.CopyTo(0, result, 0, n);
        return result;
    }

    private static float PathLength(List<InkPoint> points)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].strokeId != points[i - 1].strokeId)
            {
                continue;
            }

            length += Vector2.Distance(points[i - 1].pos, points[i].pos);
        }

        return length;
    }

    // Aspect-preserving scale into the unit square: divides both axes by the
    // bounding box's larger side, so glyph proportions survive normalization.
    private static void ScaleToUnitSquare(InkPoint[] points)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (InkPoint p in points)
        {
            minX = Mathf.Min(minX, p.pos.x);
            minY = Mathf.Min(minY, p.pos.y);
            maxX = Mathf.Max(maxX, p.pos.x);
            maxY = Mathf.Max(maxY, p.pos.y);
        }

        float size = Mathf.Max(maxX - minX, maxY - minY);
        size = Mathf.Max(size, 1e-6f); // guard: single-point ink

        for (int i = 0; i < points.Length; i++)
        {
            points[i].pos = new Vector2(
                (points[i].pos.x - minX) / size,
                (points[i].pos.y - minY) / size);
        }
    }

    private static void TranslateToOrigin(InkPoint[] points)
    {
        Vector2 centroid = Vector2.zero;
        foreach (InkPoint p in points)
        {
            centroid += p.pos;
        }

        centroid /= points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            points[i].pos -= centroid;
        }
    }

    // Greedy matching with multiple start offsets: $P can't assume the first
    // point of two clouds corresponds to the same pen-down location, so it
    // tries every `step`-th point as a start and keeps the cheapest pairing.
    private static float GreedyCloudMatch(Vector2[] points1, Vector2[] points2)
    {
        int n = points1.Length;
        int step = Mathf.FloorToInt(Mathf.Pow(n, 1f - 0.5f)); // $P: e = 0.5
        step = Mathf.Max(step, 1);

        float min = float.MaxValue;
        for (int i = 0; i < n; i += step)
        {
            min = Mathf.Min(min, CloudDistance(points1, points2, i));
            min = Mathf.Min(min, CloudDistance(points2, points1, i));
        }

        return min;
    }

    // Greedy nearest-neighbour pairing starting at index `start`. Points near
    // the start weigh more — the weighting models "the pen path likely begins
    // around here" and makes the metric stable against re-parameterization.
    private static float CloudDistance(Vector2[] points, Vector2[] template, int start)
    {
        int n = points.Length;
        bool[] matched = new bool[n];
        float sum = 0f;
        int i = start;

        do
        {
            int index = -1;
            float minDistance = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                if (matched[j])
                {
                    continue;
                }

                float d = Vector2.Distance(points[i], template[j]);
                if (d < minDistance)
                {
                    minDistance = d;
                    index = j;
                }
            }

            matched[index] = true;
            float weight = 1f - ((i - start + n) % n) / (float)n;
            sum += weight * minDistance;
            i = (i + 1) % n;
        }
        while (i != start);

        return sum;
    }

    private static Vector2[] ToPositions(InkPoint[] points)
    {
        Vector2[] positions = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            positions[i] = points[i].pos;
        }

        return positions;
    }
}
