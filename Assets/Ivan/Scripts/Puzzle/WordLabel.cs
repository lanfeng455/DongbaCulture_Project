using System.Collections.Generic;
using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // 头顶文字：两个 SpriteRenderer 各自一个 GameObject，用 SetActive 切换「可见 / 不可见」。
    // 不做文字渲染系统，用户已经有素材，拖进来即可。
    [AddComponentMenu("Puzzle/Word Label")]
    public class WordLabel : MonoBehaviour
    {
        [Tooltip("「可见」文字的物体（整块 SetActive）")]
        [SerializeField] private GameObject m_visibleWord;

        [Tooltip("「不可见」文字的物体（整块 SetActive）")]
        [SerializeField] private GameObject m_invisibleWord;

        [Tooltip("上下浮动幅度（米），0 表示不动")]
        [SerializeField] private float m_floatAmplitude = 0.04f;

        [Tooltip("浮动速度（弧度/秒）")]
        [SerializeField] private float m_floatSpeed = 2f;

        [Tooltip("层级偏移，保证文字盖在角色精灵之上")]
        [SerializeField] private int m_sortingOffset = 20;

        private readonly List<SpriteRenderer> m_renderers = new(4);
        private Transform m_cameraTransform;
        private Vector3 m_baseLocalPosition;
        private float m_clock;
        private bool m_isShown;

        private void Awake()
        {
            m_baseLocalPosition = transform.localPosition;

            if (Camera.main != null)
            {
                m_cameraTransform = Camera.main.transform;
            }

            CollectRenderers(m_visibleWord);
            CollectRenderers(m_invisibleWord);

            Hide();
        }

        private void LateUpdate()
        {
            if (!m_isShown) return;

            m_clock += Time.deltaTime;

            if (m_cameraTransform != null)
            {
                transform.rotation = m_cameraTransform.rotation;
            }

            if (m_floatAmplitude > 0f)
            {
                float bob = Mathf.Sin(m_clock * m_floatSpeed) * m_floatAmplitude;
                transform.localPosition = m_baseLocalPosition + new Vector3(0f, bob, 0f);
            }

            int order = DioramaRiver.SortingOrderFor(transform.position) + m_sortingOffset;
            foreach (var renderer in m_renderers)
            {
                if (renderer != null)
                {
                    renderer.sortingOrder = order;
                }
            }
        }

        public void ShowVisible()
        {
            Apply(true);
        }

        public void ShowInvisible()
        {
            Apply(false);
        }

        public void Hide()
        {
            if (m_visibleWord != null) m_visibleWord.SetActive(false);
            if (m_invisibleWord != null) m_invisibleWord.SetActive(false);
            m_isShown = false;
        }

        private void Apply(bool showVisible)
        {
            m_isShown = m_visibleWord != null || m_invisibleWord != null;
            if (m_visibleWord != null) m_visibleWord.SetActive(showVisible);
            if (m_invisibleWord != null) m_invisibleWord.SetActive(!showVisible);
        }

        private void CollectRenderers(GameObject root)
        {
            if (root == null) return;
            m_renderers.AddRange(root.GetComponentsInChildren<SpriteRenderer>(true));
        }
    }
}
