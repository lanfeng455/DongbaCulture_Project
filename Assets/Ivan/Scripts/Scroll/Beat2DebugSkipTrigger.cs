using UnityEngine;

namespace Ivan.Scroll
{
    // 最简 F6 触发器：挂在 Beat_2 场景任意物体（比如 ScrollPlayDirector 根节点）。
    // 文件名必须和类名一致，这样 Unity 才能在 Add Component 菜单里搜到。
    [AddComponentMenu("Scroll/Beat2 Debug Skip Trigger")]
    public class Beat2DebugSkipTrigger : MonoBehaviour
    {
        [Tooltip("哪个键触发跳过。默认 F6（和组员 Forest 那边 F6 跳剧情的快捷键对齐）")]
        [SerializeField] private KeyCode m_skipKey = KeyCode.F6;

        private void Update()
        {
            if (DongbaPrototype.DongbaInterface.GameplayBlocked || DongbaPrototype.DialogueAdvanceInput.GameplayInputBlocked) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (m_skipKey == KeyCode.F6 && kb.f6Key.wasPressedThisFrame)
            {
                if (Beat2DebugSkip.IsSkipping) Beat2DebugSkip.Stop();
                else Beat2DebugSkip.Start();
            }
        }
    }
}
