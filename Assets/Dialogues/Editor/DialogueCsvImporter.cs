using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 对话 CSV → DialogueLibrary 一键同步工具。
//
// 主流程（推荐）：
//   1. 用 Excel 维护 Assets/Dialogues/IntroDialogue.xlsx，另存为同名 .csv
//   2. 在 Project 窗口选中该 CSV
//   3. 右键 → Dongba/同步为 DialogueLibrary（或按快捷键 Ctrl+Shift+D）
//      → 同目录下找到 IntroDialogue.asset 就覆盖它，找不到就新建一个
//
// 也可以一次同步整个文件夹：
//   Tools/Dongba/同步所有对话 CSV
//
// 反方向（导回 CSV 给策划改）仍保留：选中 Library 资产 → Dongba/导出为 CSV。
public static class DialogueCsvImporter
{
    private const string k_dialogueRoot = "Assets/Dialogues";

    private const string k_headerIndex = "Index";
    private const string k_headerSpeaker = "Speaker";
    private const string k_headerContent = "Content";
    private const string k_headerNote = "Note";

    // 允许中文表头，方便把模板交给不熟悉英文列名的人填写
    private static readonly string[] k_indexAliases = { "Index", "序号", "编号" };
    private static readonly string[] k_speakerAliases = { "Speaker", "说话人", "角色" };
    private static readonly string[] k_contentAliases = { "Content", "内容", "台词", "文本" };
    private static readonly string[] k_noteAliases = { "Note", "备注", "说明" };

    // ------------------------------------------------------------------
    // 主入口：选中 CSV → 一键同步
    // ------------------------------------------------------------------

    [MenuItem("Assets/Dongba/同步为 DialogueLibrary", false, 30)]
    private static void SyncSelectedCsv()
    {
        List<string> csvPaths = CollectSelectedCsvPaths();
        if (csvPaths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "没有可同步的 CSV",
                "请在 Project 窗口选中一个或多个 .csv 文件后再执行。",
                "OK");
            return;
        }

        int created = 0;
        int updated = 0;
        foreach (string csvPath in csvPaths)
        {
            DialogueLibrarySO library = SyncCsvToLibrary(csvPath, out bool wasCreated);
            if (library == null)
            {
                continue;
            }

            if (wasCreated)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueCsv] 同步完成：新建 {created} 个，更新 {updated} 个 DialogueLibrary。");
    }

    [MenuItem("Assets/Dongba/同步为 DialogueLibrary", true)]
    private static bool ValidateSyncSelectedCsv()
    {
        return CollectSelectedCsvPaths().Count > 0;
    }

    // ------------------------------------------------------------------
    // 批量：整个 Assets/Dialogues 目录
    // ------------------------------------------------------------------

    [MenuItem("Tools/Dongba/同步所有对话 CSV", false, 30)]
    private static void SyncAllCsvInDialogueFolder()
    {
        if (!Directory.Exists(k_dialogueRoot))
        {
            EditorUtility.DisplayDialog("找不到目录", $"目录不存在：{k_dialogueRoot}", "OK");
            return;
        }

        string[] csvGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { k_dialogueRoot });
        int created = 0;
        int updated = 0;

        foreach (string guid in csvGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DialogueLibrarySO library = SyncCsvToLibrary(path, out bool wasCreated);
            if (library == null)
            {
                continue;
            }

            if (wasCreated)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueCsv] 全目录同步完成：新建 {created} 个，更新 {updated} 个 DialogueLibrary。");
    }

    // ------------------------------------------------------------------
    // 核心：单个 CSV → 同名 Library（不存在则创建）
    // ------------------------------------------------------------------

    // 返回写入后的 Library；失败返回 null。
    // Library 固定放在 CSV 旁边，文件名与 CSV 同名。
    private static DialogueLibrarySO SyncCsvToLibrary(string csvAssetPath, out bool wasCreated)
    {
        wasCreated = false;

        List<DialogueLibrarySO.DialogueEntry> entries = ReadEntries(csvAssetPath);
        if (entries == null)
        {
            return null;
        }

        string libraryPath = Path.ChangeExtension(csvAssetPath, ".asset");
        DialogueLibrarySO library = AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>(libraryPath);

        if (library == null)
        {
            // 目录里有同名但类型不对的资产时不要硬覆盖，避免误删用户资产
            Object existing = AssetDatabase.LoadAssetAtPath<Object>(libraryPath);
            if (existing != null)
            {
                Debug.LogError(
                    $"[DialogueCsv] {libraryPath} 已存在但不是 DialogueLibrary，已跳过该 CSV。",
                    existing);
                return null;
            }

            library = ScriptableObject.CreateInstance<DialogueLibrarySO>();
            library.EditorReplaceEntries(entries);
            library.LinkedCsvPath = csvAssetPath;
            AssetDatabase.CreateAsset(library, libraryPath);
            wasCreated = true;
        }
        else
        {
            Undo.RecordObject(library, "同步 Dialogue CSV");
            library.EditorReplaceEntries(entries);
            library.LinkedCsvPath = csvAssetPath;
        }

        EditorUtility.SetDirty(library);
        return library;
    }

    // 读取并解析 CSV；失败返回 null
    private static List<DialogueLibrarySO.DialogueEntry> ReadEntries(string csvAssetPath)
    {
        string absolutePath = ToAbsolutePath(csvAssetPath);
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            Debug.LogError($"[DialogueCsv] 找不到文件：{csvAssetPath}");
            return null;
        }

        string csvText;
        try
        {
            csvText = File.ReadAllText(absolutePath, CsvSerializer.Utf8WithBom);
        }
        catch (IOException exception)
        {
            Debug.LogError($"[DialogueCsv] 读取失败 {csvAssetPath}：{exception.Message}");
            return null;
        }

        List<List<string>> rows = CsvSerializer.Deserialize(csvText);
        if (rows.Count == 0)
        {
            Debug.LogWarning($"[DialogueCsv] {csvAssetPath} 是空文件，已跳过。");
            return null;
        }

        List<string> header = rows[0];
        int contentCol = FindColumn(header, k_contentAliases);
        if (contentCol < 0)
        {
            Debug.LogError(
                $"[DialogueCsv] {csvAssetPath} 的表头缺少 Content/内容 列，" +
                $"当前表头为：{string.Join(",", header)}");
            return null;
        }

        int speakerCol = FindColumn(header, k_speakerAliases);
        int noteCol = FindColumn(header, k_noteAliases);

        List<DialogueLibrarySO.DialogueEntry> entries = new List<DialogueLibrarySO.DialogueEntry>();
        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];
            string speaker = speakerCol >= 0 ? GetCell(row, speakerCol) : string.Empty;
            string content = GetCell(row, contentCol);
            string note = noteCol >= 0 ? GetCell(row, noteCol) : string.Empty;

            // 说话人和内容都空的行视为空行，跳过
            if (string.IsNullOrEmpty(speaker) && string.IsNullOrEmpty(content))
            {
                continue;
            }

            entries.Add(new DialogueLibrarySO.DialogueEntry
            {
                speaker = speaker,
                content = content,
                note = note,
            });
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning($"[DialogueCsv] {csvAssetPath} 没有有效数据行，已跳过。");
            return null;
        }

        return entries;
    }

    // ------------------------------------------------------------------
    // 反向：Library → CSV
    // ------------------------------------------------------------------

    [MenuItem("Assets/Dongba/导出为 CSV", false, 31)]
    private static void ExportSelectedLibrary()
    {
        DialogueLibrarySO library = Selection.activeObject as DialogueLibrarySO;
        if (library == null)
        {
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(library);
        string csvPath = Path.ChangeExtension(assetPath, ".csv");

        WriteCsv(library, csvPath);
        AssetDatabase.ImportAsset(csvPath);

        Debug.Log($"[DialogueCsv] 已导出 {library.Count} 条对话到 {csvPath}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(csvPath));
    }

    [MenuItem("Assets/Dongba/导出为 CSV", true)]
    private static bool ValidateExportSelectedLibrary()
    {
        return Selection.activeObject is DialogueLibrarySO;
    }

    private static void WriteCsv(DialogueLibrarySO library, string csvAssetPath)
    {
        List<string> header = new List<string> { k_headerIndex, k_headerSpeaker, k_headerContent, k_headerNote };
        List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();

        for (int i = 0; i < library.Count; i++)
        {
            DialogueLibrarySO.DialogueEntry entry = library.GetEntry(i);
            rows.Add(new List<string>
            {
                i.ToString(),
                entry.speaker ?? string.Empty,
                entry.content ?? string.Empty,
                entry.note ?? string.Empty,
            });
        }

        string absolutePath = ToAbsolutePath(csvAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, CsvSerializer.Serialize(header, rows), CsvSerializer.Utf8WithBom);
    }

    // ------------------------------------------------------------------
    // 工具
    // ------------------------------------------------------------------

    private static List<string> CollectSelectedCsvPaths()
    {
        List<string> result = new List<string>();
        foreach (Object selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(path) &&
                path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(path);
            }
        }
        return result;
    }

    private static int FindColumn(List<string> header, string[] aliases)
    {
        for (int i = 0; i < header.Count; i++)
        {
            string cell = header[i]?.Trim();
            if (string.IsNullOrEmpty(cell))
            {
                continue;
            }

            foreach (string alias in aliases)
            {
                if (string.Equals(cell, alias, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string GetCell(List<string> row, int column)
    {
        if (column < 0 || column >= row.Count)
        {
            return string.Empty;
        }
        return row[column]?.Trim() ?? string.Empty;
    }

    // Assets/Dialogues/x.csv → 绝对路径
    private static string ToAbsolutePath(string projectRelativePath)
    {
        if (string.IsNullOrEmpty(projectRelativePath))
        {
            return null;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(
            projectRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
