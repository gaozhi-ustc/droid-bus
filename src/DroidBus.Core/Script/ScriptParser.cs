using System.Text;

namespace DroidBus.Core.Script;

public static class ScriptParser
{
    public static IReadOnlyList<ScriptCommand> ParseGbkFile(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding("GB2312").GetString(File.ReadAllBytes(path));
        return Parse(text);
    }

    public static IReadOnlyList<ScriptCommand> Parse(string script)
    {
        var result = new List<ScriptCommand>();
        var segments = script.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var line = seg.Trim();
            if (line.Length == 0) continue;
            var cmd = ParseLine(line);
            if (cmd != null) result.Add(cmd);
        }
        return result;
    }

    private static ScriptCommand? ParseLine(string line)
    {
        // 无参命令(整行匹配)
        if (line == "返回桌面") return new HomeCommand();
        if (line == "返回上层") return new BackCommand();
        if (line == "随机延时") return new RandomDelayCommand();

        // 带参命令:按"更长关键字优先"顺序匹配前缀
        if (TryArg(line, "快速双击", out var a) && Ints(a, 2, out var d)) return new FastDoubleTapCommand(d[0], d[1]);
        if (TryArg(line, "快速点击", out a) && Ints(a, 2, out d)) return new FastTapCommand(d[0], d[1]);
        if (TryArg(line, "长按", out a) && Ints(a, 2, out d)) return new LongPressCommand(d[0], d[1]);
        if (TryArg(line, "点击", out a) && Ints(a, 2, out d)) return new TapCommand(d[0], d[1]);
        if (TryArg(line, "滑动", out a) && Ints(a, 4, out d)) return new SwipeCommand(d[0], d[1], d[2], d[3]);
        if (TryArg(line, "延时", out a)) return new DelayCommand(ParseDuration(a));
        if (TryArg(line, "执行命令", out a)) return new ExecCommand(a.Trim());
        if (TryArg(line, "输入文本", out a)) return new InputTextCommand(a);
        if (TryArg(line, "ADB文本", out a)) return new AdbTextCommand(a);
        if (TryArg(line, "启动应用", out a)) return new LaunchAppCommand(a.Trim());

        return null; // 未知命令忽略(向后兼容)
    }

    private static bool TryArg(string line, string keyword, out string arg)
    {
        if (line.StartsWith(keyword, StringComparison.Ordinal))
        {
            arg = line[keyword.Length..];
            return true;
        }
        arg = "";
        return false;
    }

    private static bool Ints(string s, int count, out int[] values)
    {
        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        values = new int[count];
        if (parts.Length < count) return false;
        for (var i = 0; i < count; i++)
            if (!int.TryParse(parts[i], out values[i])) return false;
        return true;
    }

    private static TimeSpan ParseDuration(string s)
    {
        s = s.Trim().ToUpperInvariant();
        if (s.EndsWith("MS") && int.TryParse(s[..^2], out var ms)) return TimeSpan.FromMilliseconds(ms);
        if (s.EndsWith("S") && int.TryParse(s[..^1], out var sec)) return TimeSpan.FromSeconds(sec);
        return int.TryParse(s, out var n) ? TimeSpan.FromSeconds(n) : TimeSpan.Zero;
    }
}
