using System;
using UnityEngine;
using UnityEngine.Events;

// One row of the handwriting reaction table: which glyph name triggers which
// environment change (and optional extra events). Kept as a plain serialized
// class so the controller stays config-driven — new glyphs and new reactions
// are Inspector work, not code work.
[Serializable]
public class GlyphReactionEntry
{
    [Tooltip("识别出的字名，必须与录入模板时填写的字名完全一致（如：雨）")]
    public string glyphName;

    [Tooltip("反应类型：改天气 / 改时段 / 改季节 / 仅触发事件")]
    public GlyphReactionKind kind = GlyphReactionKind.None;

    [Tooltip("Kind = 改天气 时生效")]
    public Weather weather = Weather.Clear;

    [Tooltip("Kind = 改时段 时生效")]
    public TimeOfDay timeOfDay = TimeOfDay.Day;

    [Tooltip("Kind = 改季节 时生效（注意：冬 + 雨 天气 = 下雪）")]
    public Season season = Season.Spring;

    [Tooltip("识别成功后额外触发的事件（动画、音效、DialogSystem、SignalSender 等任意扩展反应）")]
    public UnityEvent onGlyphRecognized;

    public bool Matches(string name)
    {
        return !string.IsNullOrEmpty(glyphName) &&
            string.Equals(glyphName.Trim(), name, StringComparison.Ordinal);
    }
}
