using UnityEngine;

namespace Ivan.Puzzle
{
    // 谜题可交互物契约：玩家按 E 时由 PuzzleInteractor 统一调度。
    // 新谜题只要实现本接口并在 OnEnable/OnDisable 里注册即可接入，不需要改玩家代码。
    public interface IPuzzleInteractable
    {
        // 当前是否可交互（条件未满足时返回 false，交互键会落回玩家原有的行为）
        bool CanInteract { get; }

        // 距离判定用的锚点（一般返回自身 transform）
        Transform InteractAnchor { get; }

        // 真正执行交互
        void Interact(Transform interactor);
    }
}
