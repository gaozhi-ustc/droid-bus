using DroidBus.Core;
using DroidBus.Core.Batch;
using DroidBus.Core.Control;
using DroidBus.Core.Process;
using DroidBus.Core.Script;
using DroidBus.Core.Time;

namespace DroidBus.App.Scripting;

/// 在多台设备上并行执行同一脚本,返回批量结果。
public sealed class ScriptLauncher
{
    private readonly BinaryLocator _bin;
    public ScriptLauncher(BinaryLocator bin) => _bin = bin;

    public async Task<BatchResult> RunFileAsync(
        string adbScriptPath, IReadOnlyList<string> serials, CancellationToken ct)
    {
        var commands = ScriptParser.ParseGbkFile(adbScriptPath);
        return await BatchExecutor.RunAsync(serials, async (serial, token) =>
        {
            var ctrl = new AdbDeviceController(new ProcessRunner(), _bin.Adb);
            var runner = new ScriptRunner(ctrl, new SystemClock());
            await runner.RunAsync(serial, commands, token);
        }, onProgress: _ => { }, ct: ct);
    }
}
