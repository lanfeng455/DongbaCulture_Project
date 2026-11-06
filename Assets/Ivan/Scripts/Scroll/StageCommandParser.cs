using System.Globalization;
using UnityEngine;

namespace Ivan.Scroll
{
    public enum StageCommandType
    {
        FaceRight,
        FaceLeft,
        Wait,
        Move,
        Stop,
        MoveTo,
        Hide,
    }

    public readonly struct StageCommand
    {
        public readonly StageCommandType Type;
        public readonly float Value;

        public StageCommand(StageCommandType type, float value)
        {
            Type = type;
            Value = value;
        }
    }

    // 对话 Content 列的演出指令范式（以 @ 开头即指令行，不进字幕）：
    //   @向右看 / @向左看       转身
    //   @等待1秒 / @等待1.5秒   停顿（作用于对话流本身，字幕隐藏）
    //   @以2为速度移动          沿当前朝向持续移动，直到 @停止 / @隐藏
    //   @停止                   停止移动，精灵保持可见
    //   @移动到12.5             以当前移速走到目标 X（父物体局部坐标，即 Inspector
    //                           里 Transform 显示的 Position.x），走到才继续下一行
    //                           （速度取此前 @以N为速度移动 设的值；没设过则警告跳过）
    //   @隐藏                   停止移动并隐藏精灵
    // 除 @等待 外都通过 Speaker 名找到场景里挂 Stage Actor 的物体执行。
    public static class StageCommandParser
    {
        public static bool TryParse(string content, out StageCommand command)
        {
            command = default;
            if (string.IsNullOrWhiteSpace(content)) return false;

            string text = content.Trim();
            if (!text.StartsWith("@")) return false;

            text = text.TrimStart('@').Trim();   // 兼容「@ 隐藏」这类带空格写法

            if (text.StartsWith("向右看"))
            {
                command = new StageCommand(StageCommandType.FaceRight, 1f);
                return true;
            }
            if (text.StartsWith("向左看"))
            {
                command = new StageCommand(StageCommandType.FaceLeft, -1f);
                return true;
            }
            if (text.StartsWith("隐藏"))
            {
                command = new StageCommand(StageCommandType.Hide, 0f);
                return true;
            }
            if (text.StartsWith("停止"))
            {
                command = new StageCommand(StageCommandType.Stop, 0f);
                return true;
            }
            if (text.StartsWith("移动到"))
            {
                // 缺数字时 Value 为 NaN，由执行端警告并跳过
                command = new StageCommand(StageCommandType.MoveTo, ExtractNumber(text, float.NaN));
                return true;
            }
            if (text.StartsWith("等待"))
            {
                command = new StageCommand(StageCommandType.Wait, ExtractNumber(text, 1f));
                return true;
            }
            if (text.StartsWith("以") && text.Contains("速度移动"))
            {
                command = new StageCommand(StageCommandType.Move, ExtractNumber(text, 1f));
                return true;
            }

            Debug.LogWarning($"[StageCommandParser] 无法识别的演出指令「{content}」，该行已跳过（不进字幕）。");
            return true;   // 未知指令也按指令行吞掉，避免把 @xxx 打到字幕上
        }

        // 抽取文本里第一个数字（支持小数与前导负号，如 -5.31），失败用默认值
        private static float ExtractNumber(string text, float fallback)
        {
            int start = -1;
            for (int i = 0; i < text.Length; i++)
            {
                bool isDigitOrDot = char.IsDigit(text[i]) || text[i] == '.';

                // 负号仅在「还没开始读数」且紧跟数字时才算数字的一部分（@移动到-5.31）
                bool isLeadingSign = text[i] == '-' && start < 0 &&
                                     i + 1 < text.Length && char.IsDigit(text[i + 1]);

                if ((isDigitOrDot || isLeadingSign) && start < 0)
                {
                    start = i;
                }
                else if (!isDigitOrDot && start >= 0)
                {
                    return Parse(text, start, i, fallback);
                }
            }
            return start >= 0 ? Parse(text, start, text.Length, fallback) : fallback;
        }

        private static float Parse(string text, int start, int end, float fallback)
        {
            string number = text.Substring(start, end - start);
            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;
        }
    }
}
