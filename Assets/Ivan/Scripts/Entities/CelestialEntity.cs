using UnityEngine;

// Static + immovable entity: the sun or moon hanging from the sky on its stick.
// Visible only during its chosen time of day, tinted by the current weather.
// The same component serves both sun and moon — pick Appears At in the Inspector.
[ExecuteAlways]
public class CelestialEntity : SceneEntity
{
    [Tooltip("Sun appears by day, moon by night")]
    [SerializeField] private TimeOfDay m_appearsAt = TimeOfDay.Day;

    [Header("Weather Tints")]
    [SerializeField] private Color m_clearColor = Color.white;
    [SerializeField] private Color m_cloudyColor = new Color(0.75f, 0.76f, 0.8f);
    [SerializeField] private Color m_rainColor = new Color(0.5f, 0.52f, 0.6f);

    // The sun and moon hang from the sky above the scene; a billboarded celestial
    // projecting a shadow onto the ground would read wrong, so they opt out of
    // the glyph shadow default.
    protected CelestialEntity()
    {
        m_castShadows = false;
    }

    protected override void Start()
    {
        base.Start();

        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentChanged += HandleEnvironmentChanged;
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
        SetStickerVisible(snapshot.TimeOfDay == m_appearsAt);

        Color color = snapshot.Weather switch
        {
            Weather.Cloudy => m_cloudyColor,
            Weather.Rain => m_rainColor,
            _ => m_clearColor
        };

        SetStickerColor(color);
    }
}
