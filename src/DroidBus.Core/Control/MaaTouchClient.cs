using DroidBus.Core.Process;

namespace DroidBus.Core.Control;

/// 一台设备上的 MaaTouch 低层客户端:在一条 <see cref="IStreamingProcess"/> 上
/// 读启动横幅拿 <see cref="MaaTouchInfo"/>,并把 d/m/u/c 命令写进 stdin。
/// 坐标按设备触摸分辨率传入(换算在 <see cref="MaaTouchSession"/> 里做)。
public sealed class MaaTouchClient : IAsyncDisposable
{
    private readonly IStreamingProcess _proc;

    public MaaTouchClient(IStreamingProcess proc) => _proc = proc;

    public MaaTouchInfo Info { get; private set; }

    /// 读启动横幅直到拿到 `^` 行(关键的 max_x/max_y/max_pressure),解析为 Info。
    public async Task<MaaTouchInfo> InitializeAsync(CancellationToken ct = default)
    {
        var lines = new List<string>();
        for (var i = 0; i < 16; i++)
        {
            var line = await _proc.ReadLineAsync(ct);
            if (line is null) break;
            lines.Add(line);
            if (line.TrimStart().StartsWith('^')) break; // 拿到分辨率即可,$ pid 可选
        }
        Info = MaaTouchProtocol.ParseHandshake(lines);
        return Info;
    }

    public ValueTask DownAsync(int id, int x, int y, int pressure, CancellationToken ct = default)
        => _proc.WriteLineAsync(MaaTouchProtocol.Down(id, x, y, pressure), ct);

    public ValueTask MoveAsync(int id, int x, int y, int pressure, CancellationToken ct = default)
        => _proc.WriteLineAsync(MaaTouchProtocol.Move(id, x, y, pressure), ct);

    public ValueTask UpAsync(int id, CancellationToken ct = default)
        => _proc.WriteLineAsync(MaaTouchProtocol.Up(id), ct);

    public ValueTask CommitAsync(CancellationToken ct = default)
        => _proc.WriteLineAsync(MaaTouchProtocol.Commit(), ct);

    public ValueTask ResetAsync(CancellationToken ct = default)
        => _proc.WriteLineAsync(MaaTouchProtocol.Reset(), ct);

    public ValueTask DisposeAsync() => _proc.DisposeAsync();
}
