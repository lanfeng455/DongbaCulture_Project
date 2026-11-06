using System.Collections.Generic;
using UnityEngine;

// Routes recognized handwriting glyphs into scene reactions.
//
// Built-in reactions drive the EnvironmentManager (weather / time of day /
// season); anything else hangs off each entry's onGlyphRecognized UnityEvent
// (TransformAnimator, DialogSystem, SignalSender, ...). Extending the sandbox
// means adding table rows in the Inspector — no code changes.
//
// The default table ships with the nine weather glyphs: 日 月 春 夏 秋 冬 雨 云 晴.
// Setup: place next to the HandwritingPanel UI object (auto-discovers both the
// panel and the EnvironmentManager).
public class GlyphReactionController : MonoBehaviour
{
    [Tooltip("手写面板（留空 = 自动查找场景中的 HandwritingPanel）")]
    [SerializeField] private HandwritingPanel m_panel;

    [Tooltip("字名 → 场景反应 映射表")]
    [SerializeField] private List<GlyphReactionEntry> m_entries = new List<GlyphReactionEntry>
    {
        new() { glyphName = "日", kind = GlyphReactionKind.SetTimeOfDay, timeOfDay = TimeOfDay.Day },
        new() { glyphName = "月", kind = GlyphReactionKind.SetTimeOfDay, timeOfDay = TimeOfDay.Night },
        new() { glyphName = "春", kind = GlyphReactionKind.SetSeason, season = Season.Spring },
        new() { glyphName = "夏", kind = GlyphReactionKind.SetSeason, season = Season.Summer },
        new() { glyphName = "秋", kind = GlyphReactionKind.SetSeason, season = Season.Autumn },
        new() { glyphName = "冬", kind = GlyphReactionKind.SetSeason, season = Season.Winter },
        new() { glyphName = "雨", kind = GlyphReactionKind.SetWeather, weather = Weather.Rain },
        new() { glyphName = "云", kind = GlyphReactionKind.SetWeather, weather = Weather.Cloudy },
        new() { glyphName = "晴", kind = GlyphReactionKind.SetWeather, weather = Weather.Clear }
    };

    private void OnEnable()
    {
        if (m_panel == null)
        {
            m_panel = FindFirstObjectByType<HandwritingPanel>();
        }

        if (m_panel == null)
        {
            Debug.LogWarning("[GlyphReactionController] No HandwritingPanel in scene — reactions stay offline.", this);
            return;
        }

        m_panel.GlyphRecognized += HandleGlyphRecognized;
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
        GlyphReactionEntry entry = FindEntry(glyphName);
        if (entry == null)
        {
            // Normal routing: glyphs owned by other listeners (e.g. VegetationSpawner
            // owns 树/草) simply have no environment entry here. Keeping a quiet
            // trace helps catch name mismatches without looking like an error.
            Debug.Log($"[GlyphReactionController] 「{glyphName}」不属于环境控制字，跳过（其他系统可能处理它）。");
            return;
        }

        ApplyEnvironment(entry);
        entry.onGlyphRecognized?.Invoke();
        Debug.Log($"[GlyphReactionController] 「{glyphName}」→ {entry.kind}（置信度 {confidence:F2}）");
    }

    private GlyphReactionEntry FindEntry(string glyphName)
    {
        foreach (GlyphReactionEntry entry in m_entries)
        {
            if (entry.Matches(glyphName))
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyEnvironment(GlyphReactionEntry entry)
    {
        if (entry.kind == GlyphReactionKind.None)
        {
            return; // event-only entry — nothing to push into the environment
        }

        EnvironmentManager environment = EnvironmentManager.Instance;
        if (environment == null)
        {
            Debug.LogWarning("[GlyphReactionController] No EnvironmentManager in scene — environment change skipped.", this);
            return;
        }

        switch (entry.kind)
        {
            case GlyphReactionKind.SetWeather:
                environment.SetWeather(entry.weather);
                break;

            case GlyphReactionKind.SetTimeOfDay:
                environment.SetTimeOfDay(entry.timeOfDay);
                break;

            case GlyphReactionKind.SetSeason:
                environment.SetSeason(entry.season);
                break;
        }
    }
}

// Reaction categories the controller knows how to apply natively. Everything
// else goes through the entry's onGlyphRecognized UnityEvent.
public enum GlyphReactionKind
{
    None,
    SetWeather,
    SetTimeOfDay,
    SetSeason
}
