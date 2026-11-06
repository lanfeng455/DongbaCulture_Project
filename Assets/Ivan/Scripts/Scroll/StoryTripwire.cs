using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Scroll
{
    // 剧情绊线：角色沿卷轴向右走，越过本物体的 X 坐标即触发一段对话。
    // 触发线就是本物体所在的那条竖线（Gizmo 可见），摆哪儿哪儿触发。
    // 每条绊线一次性；想要多段对话就多摆几条，或把区间拉宽。
    [DisallowMultipleComponent]
    [AddComponentMenu("Scroll/Story Tripwire")]
    public class StoryTripwire : MonoBehaviour
    {
        [Tooltip("对话起始条目索引（含）")]
        [SerializeField] private int m_fromIndex = 0;

        [Tooltip("对话结束条目索引（含）")]
        [SerializeField] private int m_toIndex = 0;

        [Tooltip("停下说完再走（仅自动模式生效）：勾选后角色在此站定，本段对话播完才继续；不勾 = 边走边说")]
        [SerializeField] private bool m_pauseWalking = false;

        private DioramaPlayerController m_player;
        private ScrollPlayDirector m_director;
        private bool m_hasFired;

        public int FromIndex => m_fromIndex;
        public int ToIndex => m_toIndex;
        public bool PauseWalking => m_pauseWalking;
        public bool HasFired => m_hasFired;

        private void Start()
        {
            m_player = FindFirstObjectByType<DioramaPlayerController>();
            m_director = FindFirstObjectByType<ScrollPlayDirector>();
            if (m_player == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 场景里没有 DioramaPlayerController，绊线不会触发。", this);
            }
        }

        private void Update()
        {
            if (m_hasFired || m_player == null) return;

            // 出生点若已在绊线右侧（重玩/中途进入），不补触发旧剧情
            if (m_player.transform.position.x >= transform.position.x)
            {
                Fire();
            }
        }

        // 编辑器 Builder 估算卷长、或外部强制重播时使用
        public void Fire()
        {
            if (m_hasFired) return;
            m_hasFired = true;

            var display = DialogueDisplay.Instance;
            if (display == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 场景里没有 DialogueDisplay，对话被跳过。", this);
                return;
            }

            display.PlayRange(m_fromIndex, m_toIndex);

            // 停走型：请导演原地站定，本段对话播完再继续（仅自动模式有意义）
            if (m_pauseWalking && m_director != null)
            {
                m_director.HoldForDialogue();
            }
        }

        // 青色竖线 = 触发线；触发后变灰
        private void OnDrawGizmos()
        {
            Gizmos.color = m_hasFired ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : new Color(0.2f, 0.9f, 0.9f, 0.8f);
            Vector3 basePos = transform.position;
            Gizmos.DrawLine(basePos + new Vector3(0f, 0f, -30f), basePos + new Vector3(0f, 0f, 30f));

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                basePos + new Vector3(0f, 2.5f, 0f),
                $"Tripwire [{m_fromIndex}-{m_toIndex}]");
#endif
        }
    }
}
