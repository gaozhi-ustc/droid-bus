namespace DroidBus.Core.Script;

public abstract record ScriptCommand;

public sealed record TapCommand(int X, int Y) : ScriptCommand;
public sealed record LongPressCommand(int X, int Y) : ScriptCommand;
public sealed record SwipeCommand(int X1, int Y1, int X2, int Y2) : ScriptCommand;
public sealed record FastTapCommand(int X, int Y) : ScriptCommand;
public sealed record FastDoubleTapCommand(int X, int Y) : ScriptCommand;
public sealed record DelayCommand(TimeSpan Duration) : ScriptCommand;
public sealed record RandomDelayCommand : ScriptCommand;
public sealed record HomeCommand : ScriptCommand;
public sealed record BackCommand : ScriptCommand;
public sealed record ExecCommand(string ShellCommand) : ScriptCommand;
public sealed record InputTextCommand(string Text) : ScriptCommand;   // 走 IME(ADBKeyBoard)
public sealed record AdbTextCommand(string Text) : ScriptCommand;     // 走 input text
public sealed record LaunchAppCommand(string Package) : ScriptCommand;
