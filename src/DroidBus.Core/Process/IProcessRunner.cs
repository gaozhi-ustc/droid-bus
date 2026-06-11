namespace DroidBus.Core.Process;

public readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string exe,
        IReadOnlyList<string> args,
        CancellationToken ct = default);
}
