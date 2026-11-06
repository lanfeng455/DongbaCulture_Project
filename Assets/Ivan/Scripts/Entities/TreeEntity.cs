using UnityEngine;
using DG.Tweening;

// Dynamic + immovable entity: one state per season, each repainting the sticker color.
// Subscribes to the EnvironmentManager channel so weather and time-of-day tint the
// seasonal base color without duplicating state logic.
// 继承 AttackableEntity：被玩家撞击/武器砍到会扣血，血量归零时掉落木棒并销毁。
[ExecuteAlways]
[RequireComponent(typeof(EntityStateMachine))]
public class TreeEntity : AttackableEntity
{
    [Header("Season Colors")]
    [SerializeField] private Color m_springColor = new Color(0.45f, 0.65f, 0.3f);
    [SerializeField] private Color m_summerColor = new Color(0.2f, 0.5f, 0.25f);
    [SerializeField] private Color m_autumnColor = new Color(0.8f, 0.55f, 0.2f);
    [SerializeField] private Color m_winterColor = new Color(0.75f, 0.78f, 0.8f);

    [Header("Environment Tints")]
    [Tooltip("Applied over the season color at night")]
    [SerializeField] private Color m_nightTint = new Color(0.3f, 0.35f, 0.5f);

    [Tooltip("Applied over the season color during rain (non-winter)")]
    [SerializeField] private Color m_rainTint = new Color(0.65f, 0.68f, 0.72f);

    [Tooltip("Extra whitening when winter rain turns to snow")]
    [SerializeField] private Color m_snowTint = new Color(0.9f, 0.92f, 0.95f);

    [Tooltip("Season applied on spawn (editor preview only; runtime follows EnvironmentManager)")]
    [SerializeField] private Season m_startingSeason = Season.Spring;

    [Header("显示条件")]
    [Tooltip("需要满足的季节条件列表（空列表则始终显示）")]
    [SerializeField] private Season[] m_requiredSeasons = new Season[0];

    private SeasonState m_spring;
    private SeasonState m_summer;
    private SeasonState m_autumn;
    private SeasonState m_winter;
    private EntityStateMachine m_states;

    private Season m_currentSeason;
    private EnvironmentSnapshot m_lastSnapshot;

    protected override void Awake()
    {
        base.Awake();

        m_states = GetComponent<EntityStateMachine>();

        m_spring = new SeasonState(this, Season.Spring);
        m_summer = new SeasonState(this, Season.Summer);
        m_autumn = new SeasonState(this, Season.Autumn);
        m_winter = new SeasonState(this, Season.Winter);

        m_states.Register(m_spring);
        m_states.Register(m_summer);
        m_states.Register(m_autumn);
        m_states.Register(m_winter);
        m_states.Switch(StateFor(m_startingSeason));
    }

    protected override void Start()
    {
        base.Start();

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
        m_lastSnapshot = snapshot;
        SetSeason(snapshot.Season);
        ApplyVisibility(snapshot);
        UpdateColor();
    }

    // Public API for external season control (still routed through the state machine)
    public void SetSeason(Season season)
    {
        m_currentSeason = season;
        m_states.Switch(StateFor(season));
    }

    // Combines the seasonal base color with weather and time-of-day tints
    private void UpdateColor()
    {
        Color baseColor = ColorFor(m_currentSeason);
        Color finalColor = baseColor;

        if (m_lastSnapshot.TimeOfDay == TimeOfDay.Night)
        {
            finalColor = Color.Lerp(baseColor, m_nightTint, 0.65f);
        }
        else if (m_lastSnapshot.IsSnowing)
        {
            finalColor = Color.Lerp(baseColor, m_snowTint, 0.5f);
        }
        else if (m_lastSnapshot.Weather == Weather.Rain)
        {
            finalColor = Color.Lerp(baseColor, m_rainTint, 0.35f);
        }

        SetStickerColor(finalColor);
    }

    override protected void PlayHitFeedback(Vector3 hitPoint)
    {
        base.PlayHitFeedback(hitPoint);
        GlobalBehavior.Instance.DoShakeCamera(0.3f, 0.1f);
        transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.1f);
    }

    // Optional season filter: empty list means always visible, otherwise the entity
    // only shows up during the seasons listed (e.g. a blossom tree for spring only).
    private void ApplyVisibility(EnvironmentSnapshot snapshot)
    {
        bool visible = true;

        if (m_requiredSeasons != null && m_requiredSeasons.Length > 0)
        {
            bool seasonMatched = false;
            foreach (Season season in m_requiredSeasons)
            {
                if (season == snapshot.Season)
                {
                    seasonMatched = true;
                    break;
                }
            }

            if (!seasonMatched)
            {
                visible = false;
            }
        }

        gameObject.SetActive(visible);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Editor preview only: state machine hasn't initialized, so apply the raw season color
        if (!Application.isPlaying)
        {
            SetStickerColor(ColorFor(m_startingSeason));
        }
    }
#endif

    private SeasonState StateFor(Season season)
    {
        switch (season)
        {
            case Season.Summer:
                return m_summer;
            case Season.Autumn:
                return m_autumn;
            case Season.Winter:
                return m_winter;
            default:
                return m_spring;
        }
    }

    private Color ColorFor(Season season)
    {
        switch (season)
        {
            case Season.Summer:
                return m_summerColor;
            case Season.Autumn:
                return m_autumnColor;
            case Season.Winter:
                return m_winterColor;
            default:
                return m_springColor;
        }
    }

    // One state per season; Enter triggers a recolor so future per-season behavior
    // (fruit, falling leaves) stays local to the state class
    private class SeasonState : IEntityState
    {
        private readonly TreeEntity m_owner;
        private readonly Season m_season;

        public SeasonState(TreeEntity owner, Season season)
        {
            m_owner = owner;
            m_season = season;
        }

        public void Enter()
        {
            m_owner.UpdateColor();
        }

        public void Exit()
        {
        }

        public void Tick(float deltaTime)
        {
        }
    }
}
