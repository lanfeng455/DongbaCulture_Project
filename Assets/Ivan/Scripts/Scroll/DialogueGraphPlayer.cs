using UnityEngine;

using DongbaPrototype;

namespace Ivan.Scroll
{
    // 通用对话图播放器：谜题侧共用（StorySequencePlayer 的 Graph 步骤、Beat4Puzzle 的拾取旁白）。
    // 节点语义与森林侧一致：
    //   对话节点 → DialogueDisplay 双语字幕（打完字等回车推进）
    //   等待节点 → 停顿指定秒数
    //   事件节点 → 按 Action 分发到谜题逻辑
    //   结束节点 → 收尾隐藏字幕
    // 条件节点第一版不支持：遇到时按「是」分支继续并警告。
    public static class DialogueGraphPlayer
    {
        // skipDialogue = true 时跳过对话与等待但照常触发事件（供剧情跳过用，与 Event 步行为一致）
        public static async Awaitable PlayAsync(ForestDialogueGraph graph, string startNodeId, bool skipDialogue = false)
        {
            if (graph == null || string.IsNullOrEmpty(startNodeId))
            {
                Debug.LogWarning("[DialogueGraphPlayer] 没有图资产或起点节点，本次播放跳过。");
                return;
            }

            // 图有硬伤就拒绝播放，别让半截剧情卡住调用方
            var errors = graph.Validate();
            if (errors.Count > 0)
            {
                Debug.LogWarning($"[DialogueGraphPlayer] 图「{graph.name}」校验未通过，本次播放跳过：{string.Join(" / ", errors)}");
                return;
            }

            var display = DialogueDisplay.Instance;
            ForestDialogueGraph.Node node = graph.Find(startNodeId);
            int budget = 128;   // 坏连线防御：不允许无限推进

            while (node != null && budget-- > 0)
            {
                switch (node.kind)
                {
                    case ForestDialogueGraph.Kind.Dialogue:
                    {
                        if (skipDialogue || display == null) break;

                        display.PlayBilingualLine(node.speaker, node.en, node.zh, manualContinue: true);
                        await display.WaitUntilPlaybackStopsAsync();
                        break;
                    }

                    case ForestDialogueGraph.Kind.Wait:
                    {
                        if (skipDialogue || node.seconds <= 0f) break;

                        await Awaitable.WaitForSecondsAsync(node.seconds);
                        break;
                    }

                    case ForestDialogueGraph.Kind.Event:
                    {
                        DispatchAction(node, graph);
                        break;
                    }

                    case ForestDialogueGraph.Kind.Branch:
                    {
                        Debug.LogWarning($"[DialogueGraphPlayer] 节点「{node.title}」是条件分支，" +
                                         "谜题图第一版不支持，按「是」分支继续。", graph);
                        break;
                    }
                }

                if (node.kind == ForestDialogueGraph.Kind.End) break;
                node = graph.Find(node.next);
            }

            display?.HideDialogue();
        }

        // 事件节点分发；森林专属事件出现在谜题图里属于配置错误，警告即可
        private static void DispatchAction(ForestDialogueGraph.Node node, ForestDialogueGraph graph)
        {
            switch (node.action)
            {
                case ForestDialogueGraph.Action.DeliverGrain:
                    UnityEngine.Object.FindFirstObjectByType<Ivan.Puzzle.Beat4Puzzle>()?.DeliverGrain();
                    break;

                default:
                    Debug.LogWarning($"[DialogueGraphPlayer] 节点「{node.title}」的事件 {node.action} 不是谜题事件，已忽略。", graph);
                    break;
            }
        }
    }
}
