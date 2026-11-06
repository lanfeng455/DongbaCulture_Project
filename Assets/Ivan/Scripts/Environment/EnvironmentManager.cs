using System;
using System.Collections.Generic;
using UnityEngine;

// Central environment state hub. All weather/time/season changes go through this
// manager and are broadcast on a single channel — entities subscribe once and
// receive the full snapshot instead of tracking separate systems.
//
// Setup: place one EnvironmentManager in the scene; all EnvironmentPanel and
// responsive entities auto-discover it through Instance.
public class EnvironmentManager : MonoBehaviour
{
    private static EnvironmentManager s_instance;

    // Global season read by ground shaders (grass blends its per-material season
    // colors through this); enum order Spring..Winter matches the shader's 0-3 range
    private static readonly int k_SeasonBlendId = Shader.PropertyToID("_SeasonBlend");

    [Header("Current State")]
    [SerializeField] private Weather m_weather = Weather.Clear;
    [SerializeField] private TimeOfDay m_timeOfDay = TimeOfDay.Day;
    [SerializeField] private Season m_season = Season.Spring;

    [Header("Audio Rules")]
    [Tooltip("BGM 规则表：环境变化时按最具体匹配的规则切换 BGM")]
    [SerializeField] private List<EnvironmentAudioRule> m_bgmRules = new List<EnvironmentAudioRule>();

    [Tooltip("BGS 规则表：环境变化时按最具体匹配的规则切换 BGS")]
    [SerializeField] private List<EnvironmentAudioRule> m_bgsRules = new List<EnvironmentAudioRule>();

    [Tooltip("环境音频切换的淡入淡出时长（秒，0 = 立即切换）")]
    [SerializeField] private float m_audioFadeDuration = 1.5f;

    // The single channel every responsive entity listens to
    public event Action<EnvironmentSnapshot> EnvironmentChanged;

    public static EnvironmentManager Instance => s_instance;

    public EnvironmentSnapshot CurrentSnapshot => new EnvironmentSnapshot
    {
        Weather = m_weather,
        TimeOfDay = m_timeOfDay,
        Season = m_season
    };

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning("[EnvironmentManager] Duplicate instance destroyed.", this);
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        ApplySeasonToGlobal();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    private void Start()
    {
        // Initial audio sync after all Awakes ran, so a scene that starts in rain
        // already hears it (later changes go through Broadcast)
        ApplyAudio(CurrentSnapshot);
    }

    public void SetWeather(Weather weather)
    {
        Debug.Log($"修改天气为 {weather}");
        if (m_weather == weather)
        {
            return;
        }

        m_weather = weather;
        Broadcast();
    }

    public void SetTimeOfDay(TimeOfDay timeOfDay)
    {
        if (m_timeOfDay == timeOfDay)
        {
            return;
        }

        m_timeOfDay = timeOfDay;
        Broadcast();
    }

    public void SetSeason(Season season)
    {
        if (m_season == season)
        {
            return;
        }

        m_season = season;
        ApplySeasonToGlobal();
        Broadcast();
    }

    // Repaints every shader keyed to the global season (e.g. the grass floor)
    private void ApplySeasonToGlobal()
    {
        Shader.SetGlobalFloat(k_SeasonBlendId, (float)m_season);
    }

    private void Broadcast()
    {
        EnvironmentChanged?.Invoke(CurrentSnapshot);
        ApplyAudio(CurrentSnapshot);
    }

    // Resolves the BGM/BGS rule tables against the snapshot and drives the
    // AudioManager. No matching rule or empty audio name keeps the current
    // playback — unconfigured audio is never treated as an error.
    private void ApplyAudio(EnvironmentSnapshot snapshot)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return; // no AudioManager in scene: audio features simply stay off
        }

        ApplyChannelAudio(m_bgmRules, snapshot, isBgm: true);
        ApplyChannelAudio(m_bgsRules, snapshot, isBgm: false);
    }

    private void ApplyChannelAudio(List<EnvironmentAudioRule> rules, EnvironmentSnapshot snapshot, bool isBgm)
    {
        EnvironmentAudioRule rule = ResolveAudioRule(rules, snapshot);
        if (rule == null || string.IsNullOrEmpty(rule.audioName))
        {
            return; // unconfigured: keep playing whatever is on
        }

        AudioManager audio = AudioManager.Instance;

        if (isBgm)
        {
            if (rule.audioName == AudioManager.k_FadeOutBgmSignal)
            {
                audio.StopBgm(m_audioFadeDuration);
            }
            else
            {
                audio.PlayBgm(rule.audioName, m_audioFadeDuration);
            }
        }
        else
        {
            if (rule.audioName == AudioManager.k_FadeOutBgsSignal)
            {
                audio.StopBgs(m_audioFadeDuration);
            }
            else
            {
                audio.PlayBgs(rule.audioName, m_audioFadeDuration);
            }
        }
    }

    // Most specific match wins: a rule matches when all its checked dimensions
    // equal the snapshot, and specificity is the number of checked dimensions.
    // A rule with nothing checked matches everything — the default fallback.
    private EnvironmentAudioRule ResolveAudioRule(List<EnvironmentAudioRule> rules, EnvironmentSnapshot snapshot)
    {
        if (rules == null)
        {
            return null;
        }

        EnvironmentAudioRule best = null;
        int bestScore = -1;

        foreach (EnvironmentAudioRule rule in rules)
        {
            int score = 0;

            if (rule.matchWeather)
            {
                if (rule.weather != snapshot.Weather)
                {
                    continue;
                }

                score++;
            }

            if (rule.matchTimeOfDay)
            {
                if (rule.timeOfDay != snapshot.TimeOfDay)
                {
                    continue;
                }

                score++;
            }

            if (rule.matchSeason)
            {
                if (rule.season != snapshot.Season)
                {
                    continue;
                }

                score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = rule;
            }
        }

        return best;
    }
}
