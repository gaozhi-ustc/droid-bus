using DroidBus.Core.Control;
using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class MaaTouchSessionTests
{
    /// 假的流式进程:预置可读行(握手),记录写入行(命令),不起真进程。
    private sealed class FakeStreamingProcess : IStreamingProcess
    {
        private readonly Queue<string?> _read;
        public List<string> Written { get; } = new();
        public bool IsRunning { get; private set; } = true;

        public FakeStreamingProcess(params string?[] readable) => _read = new Queue<string?>(readable);

        public ValueTask WriteLineAsync(string line, CancellationToken ct = default)
        {
            Written.Add(line);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> ReadLineAsync(CancellationToken ct = default)
            => ValueTask.FromResult(_read.Count > 0 ? _read.Dequeue() : null);

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Client_initializes_from_handshake_banner()
    {
        var proc = new FakeStreamingProcess("v 1", "^ 10 4096 4096 2048", "$ 999");
        var client = new MaaTouchClient(proc);

        var info = await client.InitializeAsync();

        info.MaxX.Should().Be(4096);
        info.MaxY.Should().Be(4096);
        info.MaxPressure.Should().Be(2048);
    }

    [Fact]
    public async Task Session_emits_down_then_commit_in_device_coords()
    {
        var proc = new FakeStreamingProcess();
        var session = new MaaTouchSession(new MaaTouchClient(proc),
            new MaaTouchInfo(10, 4096, 4096, 2048, "1", 1));

        await session.EmitAsync(new PointerSample(0, PointerAction.Down, 0.5, 0.5, 1.0f, 0));

        proc.Written.Should().Equal("d 0 2048 2048 2048", "c");
    }

    [Fact]
    public async Task Session_emits_move_and_up_with_commit_each()
    {
        var proc = new FakeStreamingProcess();
        var session = new MaaTouchSession(new MaaTouchClient(proc),
            new MaaTouchInfo(10, 1000, 2000, 100, "1", 1));

        await session.EmitAsync(new PointerSample(0, PointerAction.Move, 0.25, 0.5, 1.0f, 8));
        await session.EmitAsync(new PointerSample(0, PointerAction.Up, 0.25, 0.5, 0f, 16));

        proc.Written.Should().Equal("m 0 250 1000 100", "c", "u 0", "c");
    }

    [Fact]
    public async Task Session_routes_multiple_pointer_ids_for_multitouch()
    {
        var proc = new FakeStreamingProcess();
        var session = new MaaTouchSession(new MaaTouchClient(proc),
            new MaaTouchInfo(10, 100, 100, 1, "1", 1));

        await session.EmitAsync(new PointerSample(1, PointerAction.Down, 0.0, 0.0, 1.0f, 0));

        proc.Written.Should().Equal("d 1 0 0 1", "c");
    }
}
