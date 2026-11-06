using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// JSON-persisted template library for the handwriting recognizer.
//
// Storage layout: one flat JSON file holding every handwritten template.
// A "glyph" is identified by its name string and may own several templates
// (write the same glyph 3-5 times for stable recognition).
//
// Path policy: in the Editor the file lives under Assets so templates are
// versioned with the project and survive domain reloads; in a player build it
// lives in persistentDataPath (seeded later from StreamingAssets when needed).
public class HandwritingLibrary
{
    [Serializable]
    private class StrokeData
    {
        public List<Vector2> points = new List<Vector2>();
    }

    [Serializable]
    private class SampleData
    {
        public string glyphName = "";
        public List<StrokeData> strokes = new List<StrokeData>();
    }

    [Serializable]
    private class LibraryData
    {
        public List<SampleData> templates = new List<SampleData>();
    }

    private readonly int m_resamplePoints;
    private readonly List<SampleData> m_samples = new List<SampleData>();

    // Normalized clouds rebuilt from m_samples; kept in sync so recognition
    // never pays normalization cost for unchanged templates.
    private readonly List<PointCloudRecognizer.GlyphCloud> m_clouds =
        new List<PointCloudRecognizer.GlyphCloud>();

    private LibraryData m_serialized;

    public int SampleCount => m_samples.Count;

    public int GlyphCount
    {
        get
        {
            HashSet<string> names = new HashSet<string>();
            foreach (SampleData sample in m_samples)
            {
                names.Add(sample.glyphName);
            }

            return names.Count;
        }
    }

    public int ResamplePoints => m_resamplePoints;

    public HandwritingLibrary(int resamplePoints = PointCloudRecognizer.k_defaultResamplePoints)
    {
        m_resamplePoints = Mathf.Max(resamplePoints, 8);
    }

    public static string DefaultLibraryPath()
    {
#if UNITY_EDITOR
        return "Assets/Ivan/Handwriting/GlyphTemplates.json";
#else
        return Path.Combine(Application.persistentDataPath, "GlyphTemplates.json");
#endif
    }

    public void Load(string path)
    {
        m_samples.Clear();
        m_clouds.Clear();

        if (!File.Exists(path))
        {
            return; // first run / nothing recorded yet: start with an empty library
        }

        try
        {
            string json = File.ReadAllText(path);
            m_serialized = JsonUtility.FromJson<LibraryData>(json);

            if (m_serialized?.templates != null)
            {
                foreach (SampleData sample in m_serialized.templates)
                {
                    m_samples.Add(sample);
                    RebuildCloud(sample);
                }
            }

            Debug.Log($"[HandwritingLibrary] Loaded {m_samples.Count} templates " +
                $"({GlyphCount} glyphs) from {path}");
        }
        catch (Exception ex)
        {
            // Corrupt file must not brick the sandbox: start empty and warn
            Debug.LogWarning($"[HandwritingLibrary] Failed to load {path}: {ex.Message}");
        }
    }

    public void Save(string path)
    {
        m_serialized = new LibraryData { templates = m_samples };

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(m_serialized, prettyPrint: true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HandwritingLibrary] Failed to save {path}: {ex}");
        }
    }

    // Returns how many templates the named glyph currently owns (used by the
    // recording UI to hint "write a few more times").
    public int GetSampleCount(string glyphName)
    {
        int count = 0;
        foreach (SampleData sample in m_samples)
        {
            if (sample.glyphName == glyphName)
            {
                count++;
            }
        }

        return count;
    }

    public void AddSample(string glyphName, List<List<Vector2>> strokes)
    {
        if (string.IsNullOrWhiteSpace(glyphName) || strokes == null || strokes.Count == 0)
        {
            return;
        }

        SampleData sample = new SampleData { glyphName = glyphName };
        foreach (List<Vector2> stroke in strokes)
        {
            if (stroke == null || stroke.Count < 2)
            {
                continue; // drop dust strokes before persisting
            }

            sample.strokes.Add(new StrokeData { points = new List<Vector2>(stroke) });
        }

        if (sample.strokes.Count == 0)
        {
            return;
        }

        m_samples.Add(sample);
        RebuildCloud(sample);
    }

    // Removes every template of the named glyph (recording correction tool)
    public bool DeleteGlyph(string glyphName)
    {
        bool removed = m_samples.RemoveAll(s => s.glyphName == glyphName) > 0;
        if (removed)
        {
            RebuildAllClouds();
        }

        return removed;
    }

    public List<PointCloudRecognizer.RecognitionCandidate> Recognize(
        List<List<Vector2>> strokes, int maxResults)
    {
        Vector2[] candidate = PointCloudRecognizer.Normalize(strokes, m_resamplePoints);
        return PointCloudRecognizer.Recognize(candidate, m_clouds, maxResults);
    }

    private void RebuildCloud(SampleData sample)
    {
        List<List<Vector2>> strokes = new List<List<Vector2>>(sample.strokes.Count);
        foreach (StrokeData stroke in sample.strokes)
        {
            strokes.Add(stroke.points);
        }

        Vector2[] cloud = PointCloudRecognizer.Normalize(strokes, m_resamplePoints);
        if (cloud == null)
        {
            return; // degenerate sample (shouldn't happen — AddSample filters)
        }

        m_clouds.Add(new PointCloudRecognizer.GlyphCloud { name = sample.glyphName, points = cloud });
    }

    private void RebuildAllClouds()
    {
        m_clouds.Clear();
        foreach (SampleData sample in m_samples)
        {
            RebuildCloud(sample);
        }
    }
}
