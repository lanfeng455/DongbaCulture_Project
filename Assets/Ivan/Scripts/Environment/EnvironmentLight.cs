using UnityEngine;

// Bridges the URP directional light to the environment channel. Not a sticker
// entity, so it doesn't inherit SceneEntity — it just listens and adjusts light
// color and intensity per time of day and weather.
[RequireComponent(typeof(Light))]
public class EnvironmentLight : MonoBehaviour
{
    [Header("Day")]
    [SerializeField] private Color m_dayColor = Color.white;
    [SerializeField] private float m_dayIntensity = 1.2f;

    [Header("Night")]
    [SerializeField] private Color m_nightColor = new Color(0.35f, 0.42f, 0.65f);
    [SerializeField] private float m_nightIntensity = 0.3f;

    [Header("Weather Dimming")]
    [SerializeField] private float m_cloudyDimFactor = 0.85f;
    [SerializeField] private float m_rainDimFactor = 0.6f;

    private Light m_light;

    private void Awake()
    {
        m_light = GetComponent<Light>();
    }

    private void Start()
    {
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
        bool isNight = snapshot.TimeOfDay == TimeOfDay.Night;
        m_light.color = isNight ? m_nightColor : m_dayColor;

        float intensity = isNight ? m_nightIntensity : m_dayIntensity;
        switch (snapshot.Weather)
        {
            case Weather.Cloudy:
                intensity *= m_cloudyDimFactor;
                break;
            case Weather.Rain:
                intensity *= m_rainDimFactor;
                break;
        }

        m_light.intensity = intensity;
    }
}
