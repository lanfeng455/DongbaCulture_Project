using System;
using UnityEngine;
using UnityEngine.Events;

namespace Ivan.Puzzle
{
    // 谜题导演基类：只负责「阶段」和「完成」两件事。
    // 具体谜题（Puzzle1 及以后）继承它，把判定逻辑写在自己的 Update 里。
    public abstract class PuzzleDirectorBase : MonoBehaviour
    {
        [Tooltip("谜题完成时触发：在 Inspector 里挂后续动作（揭开蒙版 / 播演出 / 开门…）")]
        [SerializeField] private UnityEvent m_onCompleted = new();

        private int m_stage;

        // 阶段变化广播（UI、音效、后续谜题可订阅）
        public event Action<int> StageChanged;
        // 谜题完成广播
        public event Action PuzzleCompleted;

        public int Stage => m_stage;
        public bool IsCompleted { get; private set; }

        protected void SetStage(int stage)
        {
            if (IsCompleted || m_stage == stage) return;
            m_stage = stage;
            StageChanged?.Invoke(stage);
        }

        protected void CompletePuzzle()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            PuzzleCompleted?.Invoke();
            m_onCompleted?.Invoke();
        }
    }
}
