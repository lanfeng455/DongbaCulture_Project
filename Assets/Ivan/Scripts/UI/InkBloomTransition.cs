using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title → 森林的墨晕转场入口：挂在 Title 里任意常驻物体上（推荐直接挂“开始”按钮），
// 按钮 onClick 调 BeginTransition() 即可。实际流程由运行时生成的 InkBloomOverlay 完成：
// 墨点晕开盖屏 → 盖屏状态下加载森林 → 退墨显形。
public class InkBloomTransition : MonoBehaviour
{
    [Tooltip("目标场景名，必须加入 Build Settings")]
    [SerializeField] private string m_targetSceneName = "ObliqueForest";

    [Tooltip("墨点晕开盖满全屏的耗时（秒）")]
    [SerializeField] private float m_coverDuration = .7f;

    [Tooltip("森林加载完成后墨退去的耗时（秒）")]
    [SerializeField] private float m_revealDuration = .9f;

    [Tooltip("墨色，默认东巴墨的深棕黑")]
    [SerializeField] private Color m_inkColor = new Color(.13f, .10f, .08f, 1f);

    [Tooltip("墨未到时纸面的湿晕色")]
    [SerializeField] private Color m_haloColor = new Color(.42f, .35f, .27f, .55f);

    // 按钮回调：从屏幕中心晕开。
    public void BeginTransition()
    {
        BeginTransitionFromViewportPoint(new Vector2(.5f, .5f));
    }

    // 从指定屏幕像素位置晕开，可传点击点让墨从按钮处渗出。
    public void BeginTransitionFromScreenPoint(Vector2 screenPoint)
    {
        BeginTransitionFromViewportPoint(new Vector2(screenPoint.x / Screen.width, screenPoint.y / Screen.height));
    }

    void BeginTransitionFromViewportPoint(Vector2 viewportPoint)
    {
        var shader = Resources.Load<Shader>("InkBloomTransition");
        if (shader == null)
        {
            // shader 缺失时不阻断流程，退化为直接切场景。
            Debug.LogError("[InkBloomTransition] 找不到 Resources/InkBloomTransition，改为直接切场景", this);
            SceneManager.LoadScene(m_targetSceneName);
            return;
        }

        var go = new GameObject("Ink bloom overlay", typeof(Canvas));
        var overlay = go.AddComponent<InkBloomOverlay>();
        overlay.Play(m_targetSceneName, viewportPoint, new Material(shader), m_coverDuration, m_revealDuration, m_inkColor, m_haloColor);
    }
}

// 运行时覆盖层：全屏 RawImage + 墨晕材质，负责“晕开 → 加载 → 退墨”完整时序。
// 必须独立于 Title 场景存活（DontDestroyOnLoad），否则 Title 卸载时会中断流程。
// 仅通过 AddComponent 动态创建，不需要文件名与类名对应。
[RequireComponent(typeof(Canvas))]
public class InkBloomOverlay : MonoBehaviour
{
    static readonly int k_ProgressId = Shader.PropertyToID("_Progress");
    static readonly int k_OriginId = Shader.PropertyToID("_Origin");
    static readonly int k_InkColorId = Shader.PropertyToID("_InkColor");
    static readonly int k_HaloColorId = Shader.PropertyToID("_HaloColor");

    Material m_inkMaterial;
    float m_coverDuration;
    float m_revealDuration;
    string m_targetSceneName;

    public void Play(string targetSceneName, Vector2 originViewport, Material inkMaterial, float coverDuration, float revealDuration, Color inkColor, Color haloColor)
    {
        m_targetSceneName = targetSceneName;
        m_inkMaterial = inkMaterial;
        m_coverDuration = coverDuration;
        m_revealDuration = revealDuration;

        // 覆盖层盖在一切 UI 之上（ReelPortalHost 的 Canvas 是 31000，这里略高于它）。
        DontDestroyOnLoad(gameObject);
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var image = new GameObject("Ink", typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(transform, false);
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.texture = Texture2D.whiteTexture; // 材质不采样纹理，仅给 RawImage 一个有效默认贴图
        image.raycastTarget = true;             // 转场期间吃掉点击，防止误触按钮
        image.material = m_inkMaterial;

        m_inkMaterial.SetFloat(k_ProgressId, 0f);
        m_inkMaterial.SetVector(k_OriginId, originViewport);
        m_inkMaterial.SetColor(k_InkColorId, inkColor);
        m_inkMaterial.SetColor(k_HaloColorId, haloColor);

        _ = RunInkBloomAsync(destroyCancellationToken);
    }

    async Awaitable RunInkBloomAsync(CancellationToken token)
    {
        if (!Application.CanStreamedLevelBeLoaded(m_targetSceneName))
        {
            Debug.LogError($"[InkBloomOverlay] 场景未加入 Build Settings：{m_targetSceneName}");
            Destroy(gameObject);
            return;
        }

        try
        {
            // 阶段一：墨晕开，盖满全屏。
            await AnimateProgressAsync(0f, 1f, m_coverDuration, token);

            // 阶段二：盖屏状态下加载森林，玩家看不到任何硬切。
            var load = SceneManager.LoadSceneAsync(m_targetSceneName);
            while (load != null && !load.isDone)
            {
                await Awaitable.EndOfFrameAsync(token);
            }

            // 阶段三：给森林首帧程序化生成留一点时间，再退墨显形。
            await Awaitable.WaitForSecondsAsync(.15f, token);
            await AnimateProgressAsync(1f, 0f, m_revealDuration, token);
        }
        catch (OperationCanceledException)
        {
            return; // 覆盖层被销毁（如退出游戏），正常中断
        }

        Destroy(gameObject);
    }

    async Awaitable AnimateProgressAsync(float from, float to, float duration, CancellationToken token)
    {
        float age = 0;
        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(.0001f, duration));
            t = t * t * (3 - 2 * t); // smoothstep：墨的渗开先快后缓
            m_inkMaterial.SetFloat(k_ProgressId, Mathf.Lerp(from, to, t));
            await Awaitable.EndOfFrameAsync(token);
        }
        m_inkMaterial.SetFloat(k_ProgressId, to);
    }

    void OnDestroy()
    {
        if (m_inkMaterial != null) Destroy(m_inkMaterial);
    }
}
