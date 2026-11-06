using UnityEngine;

// Static + movable entity: a single-state floating sticker that drifts at constant
// velocity and wraps around the play area so the sky never runs out of clouds.
// Subscribes to the EnvironmentManager channel to tint for weather and night.
[ExecuteAlways]
[RequireComponent(typeof(DriftMover))]
public class CloudEntity : SceneEntity
{
    [Header("Wrap Area (world XZ)")]
    [Tooltip("When the cloud fully leaves this area it re-enters from the opposite side")]
    [SerializeField] private Vector2 m_wrapMin = new Vector2(-14f, -14f);

    [SerializeField] private Vector2 m_wrapMax = new Vector2(14f, 14f);

    [Header("Weather Colors")]
    [SerializeField] private Color m_clearColor = Color.white;
    [SerializeField] private Color m_cloudyColor = new Color(0.78f, 0.8f, 0.84f);
    [SerializeField] private Color m_rainColor = new Color(0.42f, 0.45f, 0.55f);
    [SerializeField] private Color m_nightTint = new Color(0.28f, 0.3f, 0.42f);

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
        Color color = snapshot.Weather switch
        {
            Weather.Cloudy => m_cloudyColor,
            Weather.Rain => m_rainColor,
            _ => m_clearColor
        };

        if (snapshot.TimeOfDay == TimeOfDay.Night)
        {
            color = Color.Lerp(color, m_nightTint, 0.6f);
        }

        SetStickerColor(color);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = WrapValue(position.x, m_wrapMin.x, m_wrapMax.x);
        position.z = WrapValue(position.z, m_wrapMin.y, m_wrapMax.y);
        transform.position = position;
    }

    private static float WrapValue(float value, float min, float max)
    {
        // Wrap only after leaving the area completely, so the swap is never visible on screen
        if (value < min)
        {
            return max;
        }

        if (value > max)
        {
            return min;
        }

        return value;
    }
}
