using System.Collections.Generic;
using UnityEngine;

// 对话库：一个 ScriptableObject 资产存一整段对话。
//
// 日常维护流程（推荐，CSV 为准）：
//   1. 用 Excel 编辑 Assets/Dialogues/xxx.xlsx，另存为同名 xxx.csv
//   2. Project 窗口选中 xxx.csv → 右键 → Dongba/同步为 DialogueLibrary
//      → 同目录自动生成或更新 xxx.asset（没有就新建）
//   3. 场景里的 DialogueDisplay 拖这个 .asset，用 Unity Events 调 PlayRange 播放
//
// 也可以在 Inspector 里临时改几条，改完右键资产 → Dongba/导出为 CSV 回写。
[CreateAssetMenu(fileName = "DialogueLibrary", menuName = "Dongba/Dialogue Library", order = 0)]
public class DialogueLibrarySO : ScriptableObject
{
    // 单句对话：一个说话人 + 一句内容 + 可选备注（备注不进游戏，只给策划看）
    [System.Serializable]
    public class DialogueEntry
    {
        [Tooltip("说话人名，留空则只显示内容（旁白模式）")]
        public string speaker;

        [Tooltip("这一句话的内容")]
        [TextArea(1, 4)]
        public string content;

        [Tooltip("策划/翻译备注，不进游戏")]
        public string note;
    }

    [Tooltip("按顺序播放的对话句子列表")]
    [SerializeField] private List<DialogueEntry> m_entries = new List<DialogueEntry>();

    [Tooltip("上次导入/导出时关联的 CSV 路径（编辑器用，仅作提示）")]
    [SerializeField] private string m_linkedCsvPath;

    public IReadOnlyList<DialogueEntry> Entries => m_entries;

    public int Count => m_entries.Count;

    public string LinkedCsvPath
    {
        get => m_linkedCsvPath;
        set => m_linkedCsvPath = value;
    }

    public DialogueEntry GetEntry(int index)
    {
        if (index < 0 || index >= m_entries.Count)
        {
            return null;
        }
        return m_entries[index];
    }

#if UNITY_EDITOR
    // 编辑器专用：直接覆盖条目列表（CSV 导入用）
    public void EditorReplaceEntries(List<DialogueEntry> newEntries)
    {
        m_entries = newEntries ?? new List<DialogueEntry>();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
