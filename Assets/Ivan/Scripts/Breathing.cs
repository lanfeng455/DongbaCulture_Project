using DG.Tweening;
using UnityEngine;

// Loops a subtle vertical squash-and-stretch around the object's resting scale,
// giving the glyphs a quiet "breathing" feel without affecting gameplay.
public class Breathing : MonoBehaviour
{
    [Tooltip("Peak height gain relative to the resting scale (0.05 = +5%)")]
    [SerializeField] private float m_breathAmount = 0.05f;

    [Tooltip("Duration of one full breath cycle in seconds")]
    [SerializeField] private float m_cycleDurationInSeconds = 2.6f;

    private Vector3 m_baseScale;
    private Tween m_breathTween;

    private void Awake()
    {
        // Cache the scale set by the scene or prefab so the breath always returns to it
        m_baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        StartBreathing();
    }

    private void OnDisable()
    {
        // Never leave a tween running on a disabled or destroyed object
        m_breathTween?.Kill();
        m_breathTween = null;
    }

    private void StartBreathing()
    {
        transform.localScale = m_baseScale;

        // Height grows while width dips slightly, mimicking an inhale
        Vector3 peakScale = new Vector3(m_baseScale.x * (1f - m_breathAmount * 0.4f),
            m_baseScale.y * (1f + m_breathAmount), m_baseScale.z);

        m_breathTween = transform.DOScale(peakScale, m_cycleDurationInSeconds * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        // Jump to a random point in the cycle so instances do not breathe in sync
        m_breathTween.Goto(Random.value * m_cycleDurationInSeconds, true);
    }
}
