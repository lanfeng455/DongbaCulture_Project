using UnityEngine;

// Scene-wide precipitation effect driven by the environment snapshot: winter rain
// reads as snow, other rain stays rain. Each particle is a complete Dongba glyph
// (rain = three strokes as one group, snow = three flakes as one group) — glyphs are
// never split apart, otherwise the character meaning is lost.
//
// Setup: build one prefab with two ParticleSystems (rain + snow), assign them below,
// and place it in the scene. All emission/shape/speed art config lives on the
// particle systems themselves — this component only switches and tints them.
public class PrecipitationController : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("雨粒子，贴图为完整的东巴文“雨”字（三笔一组，不可拆开）")]
    [SerializeField] private ParticleSystem m_rainFx;

    [Tooltip("雪粒子，贴图为完整的东巴文“雪”字（三片一组，不可拆开）")]
    [SerializeField] private ParticleSystem m_snowFx;

    [Header("Rain Tints")]
    [Tooltip("雨天白天的粒子颜色")]
    [SerializeField] private Color m_rainDayColor = new Color(0.6f, 0.65f, 0.75f);

    [Tooltip("雨夜更暗更蓝")]
    [SerializeField] private Color m_rainNightColor = new Color(0.35f, 0.4f, 0.55f);

    [Header("Snow Tints")]
    [Tooltip("雪天白天的粒子颜色")]
    [SerializeField] private Color m_snowDayColor = Color.white;

    [Tooltip("雪夜微蓝")]
    [SerializeField] private Color m_snowNightColor = new Color(0.8f, 0.85f, 0.95f);

    private void Start()
    {
        if (m_rainFx == null && m_snowFx == null)
        {
            Debug.LogWarning($"[{GetType().Name}] No particle systems assigned; precipitation will not play.", this);
        }

        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentChanged += HandleEnvironmentChanged;
            // Sync immediately so we don't miss the initial broadcast if Start ran late
            HandleEnvironmentChanged(EnvironmentManager.Instance.CurrentSnapshot);
        }
    }

    private void OnDestroy()
    {
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentChanged -= HandleEnvironmentChanged;
        }
    }

    private void HandleEnvironmentChanged(EnvironmentSnapshot snapshot)
    {
        bool isNight = snapshot.TimeOfDay == TimeOfDay.Night;

        // Snow takes priority: winter rain reads as snow via IsSnowing
        SetFxState(m_rainFx, snapshot.IsRaining, isNight ? m_rainNightColor : m_rainDayColor);
        SetFxState(m_snowFx, snapshot.IsSnowing, isNight ? m_snowNightColor : m_snowDayColor);
    }

    // Starts/stops emission and applies the tint. Stop() defaults to StopEmitting,
    // so live particles finish their lifetime naturally — weather changes fade out.
    private void SetFxState(ParticleSystem fx, bool active, Color tint)
    {
        if (fx == null)
        {
            return;
        }

        ParticleSystem.MainModule main = fx.main;
        main.startColor = new ParticleSystem.MinMaxGradient(tint);

        if (active && !fx.isPlaying)
        {
            fx.Play();
        }
        else if (!active && fx.isPlaying)
        {
            fx.Stop();
        }
    }
}
