using System.Text;

namespace DroidBus.Core.Control;

/// 把一行 shell 命令按引号分词:空白处断开,'...' 与 "..." 内的空白保留为同一参数,配对引号被剥离。
public static class ShellArgs
{
    public static IReadOnlyList<string> Split(string command)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inToken = false;
        var quote = '\0'; // 当前所在引号字符;'\0' 表示不在引号内

        foreach (var ch in command)
        {
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';   // 闭合引号
                else sb.Append(ch);
            }
            else if (ch is '\'' or '"')
            {
                quote = ch;
                inToken = true;                  // 即便引号内为空也算一个参数
            }
            else if (ch is ' ' or '\t')
            {
                if (inToken) { tokens.Add(sb.ToString()); sb.Clear(); inToken = false; }
            }
            else
            {
                sb.Append(ch);
                inToken = true;
            }
        }
        if (inToken) tokens.Add(sb.ToString());
        return tokens;
    }
}
