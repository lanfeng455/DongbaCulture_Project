using UnityEngine;
using UnityEngine.UIElements;

// Runtime control panel for the environment system. Binds to a UIDocument and
// pushes user input into EnvironmentManager; highlights the active button in
// each group whenever the environment changes.
[RequireComponent(typeof(UIDocument))]
public class EnvironmentPanel : MonoBehaviour
{
    private const string k_activeClass = "environment-panel__button--active";

    private EnvironmentManager m_manager;
    private VisualElement m_root;

    private void OnEnable()
    {
        m_root = GetComponent<UIDocument>().rootVisualElement;
    }

    private void Start()
    {
        // Instance is assigned in EnvironmentManager.Awake; resolving it in OnEnable
        // would race with that assignment since cross-object Awake/OnEnable order is
        // undefined. Start runs after every Awake, so the manager is guaranteed here.
        m_manager = EnvironmentManager.Instance;
        if (m_manager == null)
        {
            Debug.LogWarning("[EnvironmentPanel] No EnvironmentManager found in scene.", this);
            return;
        }

        BindButton("weather-clear", () => m_manager.SetWeather(Weather.Clear));
        BindButton("weather-cloudy", () => m_manager.SetWeather(Weather.Cloudy));
        BindButton("weather-rain", () => m_manager.SetWeather(Weather.Rain));

        BindButton("time-day", () => m_manager.SetTimeOfDay(TimeOfDay.Day));
        BindButton("time-night", () => m_manager.SetTimeOfDay(TimeOfDay.Night));

        BindButton("season-spring", () => m_manager.SetSeason(Season.Spring));
        BindButton("season-summer", () => m_manager.SetSeason(Season.Summer));
        BindButton("season-autumn", () => m_manager.SetSeason(Season.Autumn));
        BindButton("season-winter", () => m_manager.SetSeason(Season.Winter));

        m_manager.EnvironmentChanged += HandleEnvironmentChanged;
        HandleEnvironmentChanged(m_manager.CurrentSnapshot);
    }

    private void OnDisable()
    {
        if (m_manager != null)
        {
            m_manager.EnvironmentChanged -= HandleEnvironmentChanged;
        }
    }

    private void BindButton(string buttonName, System.Action onClick)
    {
        Button button = m_root.Q<Button>(buttonName);
        if (button != null)
        {
            button.clicked += onClick;
        }
    }

    private void HandleEnvironmentChanged(EnvironmentSnapshot snapshot)
    {
        SetActiveButton("weather-clear", snapshot.Weather == Weather.Clear);
        SetActiveButton("weather-cloudy", snapshot.Weather == Weather.Cloudy);
        SetActiveButton("weather-rain", snapshot.Weather == Weather.Rain);

        SetActiveButton("time-day", snapshot.TimeOfDay == TimeOfDay.Day);
        SetActiveButton("time-night", snapshot.TimeOfDay == TimeOfDay.Night);

        SetActiveButton("season-spring", snapshot.Season == Season.Spring);
        SetActiveButton("season-summer", snapshot.Season == Season.Summer);
        SetActiveButton("season-autumn", snapshot.Season == Season.Autumn);
        SetActiveButton("season-winter", snapshot.Season == Season.Winter);
    }

    private void SetActiveButton(string buttonName, bool isActive)
    {
        Button button = m_root.Q<Button>(buttonName);
        button?.EnableInClassList(k_activeClass, isActive);
    }
}
