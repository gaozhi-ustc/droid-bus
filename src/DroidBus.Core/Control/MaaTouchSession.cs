namespace DroidBus.Core.Control;

/// 一台从机的 MaaTouch 注入下游:实现 <see cref="IPointerSink"/>,
/// 把归一化 <see cref="PointerSample"/> 按握手 max_x/max_y/max_pressure 换算成设备坐标,
/// 翻译为 d/m/u 并每帧 commit。群控广播 = 对每台从机的 Session 各 Emit 同一条流。
public sealed class MaaTouchSession : IPointerSink, IAsyncDisposable
{
    private readonly MaaTouchClient _client;
    private readonly MaaTouchInfo _info;

    public MaaTouchSession(MaaTouchClient client, MaaTouchInfo info)
    {
        _client = client;
        _info = info;
    }

    public async ValueTask EmitAsync(PointerSample sample, CancellationToken ct = default)
    {
        var (x, y) = PointerStreamMapper.ToTarget(sample.NormX, sample.NormY, _info.MaxX, _info.MaxY);
        var pressure = MaaTouchProtocol.ToPressure(sample.Pressure, _info.MaxPressure);
        var id = (int)sample.PointerId;

        switch (sample.Action)
        {
            case PointerAction.Down:
                await _client.DownAsync(id, x, y, pressure, ct);
                break;
            case PointerAction.Move:
                await _client.MoveAsync(id, x, y, pressure, ct);
                break;
            case PointerAction.Up:
            case PointerAction.Cancel:
                await _client.UpAsync(id, ct);
                break;
        }
        await _client.CommitAsync(ct);
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
