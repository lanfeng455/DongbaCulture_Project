using UnityEngine;

// One Inspector-configurable audio rule for the environment system. A rule matches
// a snapshot when every checked dimension matches; unchecked dimensions act as
// wildcards. When several rules match, the most specific one (most checked
// dimensions) wins, so "Weather=Rain" can be overridden by "Weather=Rain +
// Season=Winter", and a rule with nothing checked serves as the default fallback.
//
// audioName semantics:
// - empty            : keep the current playback (not an error)
// - k_FadeOut signal : fade the channel out instead of playing
// - anything else    : Addressables address (assigned in the Addressables Groups window)
[System.Serializable]
public class EnvironmentAudioRule
{
    [Tooltip("勾选后该维度参与匹配，不勾选视为通配")]
    public bool matchWeather;
    public Weather weather = Weather.Clear;

    [Tooltip("勾选后该维度参与匹配，不勾选视为通配")]
    public bool matchTimeOfDay;
    public TimeOfDay timeOfDay = TimeOfDay.Day;

    [Tooltip("勾选后该维度参与匹配，不勾选视为通配")]
    public bool matchSeason;
    public Season season = Season.Spring;

    [Tooltip("音频名称，即 Addressables 资产地址。留空 = 保持当前播放；填 AudioManager.k_FadeOutBgmSignal / k_FadeOutBgsSignal = 淡出该通道")]
    public string audioName;
}
