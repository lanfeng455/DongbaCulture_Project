using UnityEngine;
using Ivan.Diorama;

namespace Ivan.Puzzle
{
    // Puzzle1 的判定中枢：
    //   纠缠 → 分开（可自由推动，玩家先把白蛇推开）→ 玩家退开后开始判视线
    //   → 视线通畅 / 被石头挡住，两种状态每帧动态切换（不锁存）→ 两条蛇各入其洞 = 通关
    // 「不可见」刻意不做锁存：石头一挪开视线恢复，蛇立刻变回可见，这样玩家才能反复试。
    [DisallowMultipleComponent]
    [AddComponentMenu("Puzzle/Puzzle 1 Director")]
    public class Puzzle1Director : PuzzleDirectorBase
    {
        // 注意：枚举名不能叫 Stage，会与基类的 Stage 属性冲突
        public enum PuzzleStage
        {
            Entangled = 0,
            Separated = 1,
            Blinded = 2,
            Complete = 3,
        }

        [SerializeField] private SnakeActor m_whiteSnake;
        [SerializeField] private SnakeActor m_blackSnake;
        [Tooltip("两条蛇之间的视线可视化，留空则只做判定不画线")]
        [SerializeField] private SightLineVisual m_sightLine;

        [Tooltip("分开后玩家退开这个距离，才开始判定视线（留出推开白蛇的余地）")]
        [SerializeField] private float m_startDistance = 2.5f;

        private Transform m_player;
        private bool m_hasSplit;
        private bool m_hasSeparated;

        private void Awake()
        {
            if (m_whiteSnake != null) m_whiteSnake.BindOpponent(m_blackSnake);
            if (m_blackSnake != null) m_blackSnake.BindOpponent(m_whiteSnake);

            var interactor = FindFirstObjectByType<PuzzleInteractor>();
            if (interactor != null)
            {
                m_player = interactor.transform;
            }
            else
            {
                // 玩家还没挂 PuzzleInteractor 时的兜底，判定照样能跑
                var player = FindFirstObjectByType<DioramaPlayerController>();
                if (player != null)
                {
                    m_player = player.transform;
                }
            }

            if (m_sightLine != null) m_sightLine.SetVisible(false);
        }

        private void Update()
        {
            if (IsCompleted) return;
            if (!m_hasSplit) return;                        // 还没分开，什么都不做
            if (m_whiteSnake == null || m_blackSnake == null) return;

            if (!m_hasSeparated && !ShouldStartPuzzle()) return;

            // 每帧动态重算：石头挪开就恢复可见，不做锁存。
            // 端点取蛇的 Sight Anchor（眼部锚点），没有就用蛇根
            bool blocked = SightLineSensor.IsBlocked(
                m_whiteSnake.SightPosition,
                m_blackSnake.SightPosition);

            // 视线通：洞关不住还想会合的蛇，待在洞里的会自己窜出来——
            // 这堵死了「先关一只、拿石头挡线再轻松送另一只」的取巧解法
            if (!blocked)
            {
                if (m_whiteSnake.IsCaptured) m_whiteSnake.ExitCave(m_blackSnake);
                if (m_blackSnake.IsCaptured) m_blackSnake.ExitCave(m_whiteSnake);
            }

            // 通关 = 两只都在洞里且连线被挡（互相看不见才安分待着）。
            // 只要还看得见，上一段就会把蛇放出来，到不了这里
            if (blocked && m_whiteSnake.IsCaptured && m_blackSnake.IsCaptured)
            {
                if (m_sightLine != null) m_sightLine.SetVisible(false);
                m_whiteSnake.Depart();
                m_blackSnake.Depart();
                SetStage((int)PuzzleStage.Complete);
                CompletePuzzle();
                return;
            }

            if (m_sightLine != null)
            {
                m_sightLine.SetVisible(true);
                m_sightLine.SetBlocked(blocked);
            }

            if (blocked)
            {
                m_whiteSnake.EnterBlinded();
                m_blackSnake.EnterBlinded();
                SetStage((int)PuzzleStage.Blinded);
            }
            else
            {
                m_whiteSnake.EnterStaring();
                m_blackSnake.EnterStaring();
                SetStage((int)PuzzleStage.Separated);
            }
        }

        // 由 EntangledSnakePair 在分开完成时调用
        public void OnSnakePairSplit()
        {
            m_hasSplit = true;

            // 分开瞬间先保持可推动，等玩家退开再进入「可见/不可见」判定
            if (m_whiteSnake != null) m_whiteSnake.EnterFree();
            if (m_blackSnake != null) m_blackSnake.EnterFree();
        }

        // 玩家离开两条蛇的中点一定距离后，谜题正式开始
        private bool ShouldStartPuzzle()
        {
            if (m_player == null)
            {
                StartPuzzle();
                return true;
            }

            Vector3 middle = (m_whiteSnake.transform.position + m_blackSnake.transform.position) * 0.5f;
            Vector3 delta = m_player.position - middle;
            delta.y = 0f;

            if (delta.magnitude < m_startDistance) return false;

            StartPuzzle();
            return true;
        }

        private void StartPuzzle()
        {
            m_hasSeparated = true;
            SetStage((int)PuzzleStage.Separated);

            if (m_sightLine != null)
            {
                m_sightLine.SetVisible(true);
            }
        }
    }
}
