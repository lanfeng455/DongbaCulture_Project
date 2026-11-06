using System.Collections.Generic;
using System.Text;

// CSV 序列化/反序列化：处理引号转义、逗号、换行、UTF-8 BOM。
// Excel 双击打开 UTF-8 with BOM 不会乱码；写出时也带 BOM，保证往返一致。
//
// 格式约定（四列）：
//   Index,Speaker,Content,Note
//   0,老者,年轻人，你终于来了。,开场白
//
// Speaker/Note 可为空；Content 必填；Index 只是给人看的，导入时按行序重建。
public static class CsvSerializer
{
    private static readonly UTF8Encoding s_utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    // 把二维字符串表写成 CSV 文本
    public static string Serialize(IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        StringBuilder builder = new StringBuilder();
        AppendRow(builder, header);
        foreach (IReadOnlyList<string> row in rows)
        {
            AppendRow(builder, row);
        }
        return builder.ToString();
    }

    // 把 CSV 文本解析回二维字符串表
    // 处理 RFC 4180 风格的引号转义："他说""你好""" → 他说"你好"
    public static List<List<string>> Deserialize(string csvText)
    {
        List<List<string>> result = new List<List<string>>();
        if (string.IsNullOrEmpty(csvText))
        {
            return result;
        }

        List<string> currentRow = new List<string>();
        StringBuilder currentCell = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];

            if (insideQuotes)
            {
                if (c == '"')
                {
                    // 双引号后跟双引号 = 转义的引号
                    if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentCell.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    insideQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;
                }
                else if (c == '\n' || c == '\r')
                {
                    // 处理 \r\n / \n / \r
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;

                    // 跳过完全空行（只在行末没内容时才算空行）
                    bool rowHasContent = false;
                    foreach (string cell in currentRow)
                    {
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            rowHasContent = true;
                            break;
                        }
                    }
                    if (rowHasContent)
                    {
                        result.Add(currentRow);
                    }
                    currentRow = new List<string>();
                }
                else
                {
                    currentCell.Append(c);
                }
            }
        }

        // 文件末尾没换行的最后一行
        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString());
            bool rowHasContent = false;
            foreach (string cell in currentRow)
            {
                if (!string.IsNullOrWhiteSpace(cell))
                {
                    rowHasContent = true;
                    break;
                }
            }
            if (rowHasContent)
            {
                result.Add(currentRow);
            }
        }

        return result;
    }

    public static UTF8Encoding Utf8WithBom => s_utf8WithBom;

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> row)
    {
        for (int i = 0; i < row.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }
            builder.Append(EscapeCell(row[i]));
        }
        builder.Append("\r\n");
    }

    private static string EscapeCell(string cell)
    {
        if (string.IsNullOrEmpty(cell))
        {
            return string.Empty;
        }

        bool needsQuotes =
            cell.Contains(",") ||
            cell.Contains("\"") ||
            cell.Contains("\n") ||
            cell.Contains("\r") ||
            cell.StartsWith(" ") ||
            cell.EndsWith(" ");

        if (!needsQuotes)
        {
            return cell;
        }

        return "\"" + cell.Replace("\"", "\"\"") + "\"";
    }
}
