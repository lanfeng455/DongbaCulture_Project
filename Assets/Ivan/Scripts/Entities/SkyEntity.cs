using UnityEngine;

// Static + immovable entity: the sky (天) backdrop sticker. Tints per time of day
// and weather so the whole scene reads dawn-blue at night and gray in rain.
[ExecuteAlways]
public class SkyEntity : SceneEntity
{
    [Header("Time & Weather Tints")]
    [SerializeField] private Color m_dayColor = Color.white;
    [SerializeField] private Color m_nightColor = new Color(0.22f, 0.26f, 0.45f);
    [SerializeField] private Color m_cloudyTint = new Color(0.8f, 0.8f, 0.82f);
    [SerializeField] private Color m_rainTint = new Color(0.55f, 0.57f, 0.65f);

    [Header("显示条件")]
    [Tooltip("是否仅在夜间显示")]
    [SerializeField] private bool m_nightOnly = false;
    [Tooltip("需要满足的天气条件列表（空列表则始终显示）")]
    [SerializeField] private Weather[] m_requiredWeathers = new Weather[0];

    private void ApplyVisibility(EnvironmentSnapshot snapshot)
    {
        bool visible = true;

        if (m_nightOnly && snapshot.TimeOfDay != TimeOfDay.Night)
        {
            visible = false;
        }

        if (m_requiredWeathers != null && m_requiredWeathers.Length > 0)
        {
            bool weatherMatched = false;
            foreach (Weather w in m_requiredWeathers)
            {
                if (w == snapshot.Weather)
                {
                    weatherMatched = true;
                    break;
                }
            }

            if (!weatherMatched)
            {
                visible = false;
            }
        }

        gameObject.SetActive(visible);
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
        Color color = snapshot.TimeOfDay == TimeOfDay.Night ? m_nightColor : m_dayColor;

        switch (snapshot.Weather)
        {
            case Weather.Cloudy:
                color = Color.Lerp(color, m_cloudyTint, 0.4f);
                break;
            case Weather.Rain:
                color = Color.Lerp(color, m_rainTint, 0.5f);
                break;
        }

        ApplyVisibility(snapshot);
        SetStickerColor(color);
    }
}
