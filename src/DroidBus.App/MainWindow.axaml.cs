using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DroidBus.App.Interop;
using DroidBus.App.Mirror;
using DroidBus.App.Views;
using DroidBus.Core;
using DroidBus.Core.Adb;
using DroidBus.Core.Devices;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;
using DroidBus.Core.Process;

namespace DroidBus.App;

public partial class MainWindow : Window
{
    private BinaryLocator _bin = null!;
    private DeviceManager _devices = null!;
    private MirrorController _mirror = null!;
    private INativeWindowEmbedder _embedder = null!;
    private CancellationTokenSource? _pollCts;
    private MirrorOptions _globalOptions = new();
    private DeviceTile? _focusedTile;              // 放大中的 tile
    private readonly List<DeviceTile> _tiles = new(6);
    private const int DeviceW = 1440;
    private const int DeviceH = 2960;

    public MainWindow()
    {
        InitializeComponent();

        // 填充 3×2 网格(6 格)
        for (var i = 0; i < 6; i++)
        {
            var tile = new DeviceTile();
            tile.TileClicked += OnTileClicked;
            tile.TileDoubleClicked += OnTileDoubleClicked;
            _tiles.Add(tile);
            TileGrid.Children.Add(tile);
        }

        Loaded += OnLoaded;
        Closing += (_, _) => Cleanup();
        // 窗口尺寸/布局变化 → 重新定位所有嵌入的 scrcpy 窗口
        TileGrid.LayoutUpdated += (_, _) => _mirror?.ResizeAll();
    }

    // ---- 初始化 --------------------------------------------------
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        StatusText.Text = "正在初始化…";
        try
        {
            _bin = BinaryLocator.Discover();
            _embedder = EmbedderFactory.Create();
            _devices = new DeviceManager(new AdbClient(new ProcessRunner(), _bin.Adb));
            _mirror = new MirrorController(_bin, _embedder, DeviceW, DeviceH);
            _mirror.RestartRequested = serial =>
            {
                var tile = _tiles.FirstOrDefault(t => t.Device?.Serial == serial);
                if (tile is not null)
                    Dispatcher.UIThread.Post(async () => await _mirror.StartAsync(tile, _globalOptions));
                return Task.CompletedTask;
            };

            _devices.DeviceChanged += OnDeviceChanged;
            await _devices.RefreshAsync();
            RebindTiles();

            _pollCts = new CancellationTokenSource();
            _ = _devices.PollLoopAsync(TimeSpan.FromSeconds(3), _pollCts.Token);

            TrySetWindowHandle();
            StatusText.Text = "就绪";

            // 开发自检:DROIDBUS_AUTOMIRROR=1 时自动投屏首台;=all 时全部投屏。
            var auto = Environment.GetEnvironmentVariable("DROIDBUS_AUTOMIRROR");
            if (!string.IsNullOrEmpty(auto))
            {
                var targets = auto == "all"
                    ? _tiles.Where(t => t.Device is { IsControllable: true })
                    : _tiles.Where(t => t.Device is { IsControllable: true }).Take(1);
                foreach (var t in targets)
                    try { await _mirror.StartAsync(t, _globalOptions); }
                    catch (Exception ex) { StatusText.Text = $"自动投屏失败:{ex.Message}"; }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"InitializeAsync 失败: {ex}");
            StatusText.Text = $"初始化失败:{ex.Message}";
        }
    }

    private void Cleanup()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _mirror.Dispose();
        if (_embedder is IDisposable d) d.Dispose();
    }

    // ---- 设备变化 → 更新 tile -----------------------------------
    private void OnDeviceChanged(Device d)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!d.IsControllable) _mirror.Stop(d.Serial);
            RebindTiles();
        });
    }

    private void RebindTiles()
    {
        var online = _devices.Devices.OrderBy(x => x.Serial).ToList();
        for (var i = 0; i < _tiles.Count; i++)
            _tiles[i].Bind(i < online.Count ? online[i] : null);
    }

    // ---- tile 点击 / 双击 ---------------------------------------
    private async void OnTileClicked(DeviceTile tile)
    {
        // TODO: Ctrl+click 多选(后续 Task)
        foreach (var t in _tiles) t.Opacity = ReferenceEquals(t, tile) ? 1.0 : 0.7;
        SelectedInfo.Text = tile.Device?.Model ?? tile.Device?.Serial ?? "--";

        // 按需投屏:点击即启动 scrcpy(已投屏则跳过)
        if (tile.Device is { IsControllable: true } dev && !_mirror.IsMirroring(dev.Serial))
        {
            try { await _mirror.StartAsync(tile, _globalOptions); }
            catch (Exception ex) { StatusText.Text = $"投屏失败:{ex.Message}"; }
        }
    }

    private void OnTileDoubleClicked(DeviceTile tile)
    {
        if (_focusedTile is null)
            FocusTile(tile);
        else
            RestoreGrid();
    }

    private void FocusTile(DeviceTile focus)
    {
        _focusedTile = focus;
        // 将其他 tile 缩成缩略条(后续 Task 实现完整布局切换)
        foreach (var t in _tiles.Where(t => !ReferenceEquals(t, focus)))
            t.IsVisible = false;
        _mirror.ResizeAll();
    }

    private void RestoreGrid()
    {
        _focusedTile = null;
        foreach (var t in _tiles) t.IsVisible = true;
        _mirror.ResizeAll();
    }

    // ---- 获得顶层窗口句柄 → 传给 MirrorController ---------------
    /// 当平台原生窗口就绪时调用(通常在 Loaded 之后,由外部触发或在此轮询)。
    /// 后续可通过 TopLevel.GetTopLevel(this)?.TryGetPlatformHandle() 获取句柄。
    private void TrySetWindowHandle()
    {
        if (_mirror.WindowHandle != IntPtr.Zero) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.TryGetPlatformHandle() is { } handle)
            _mirror.WindowHandle = handle.Handle;
    }

    // ---- 工具栏按钮 ----------------------------------------------
    private async void OnMirrorAll(object? sender, RoutedEventArgs e)
    {
        foreach (var tile in _tiles)
            if (tile.Device is { IsControllable: true })
                try { await _mirror.StartAsync(tile, _globalOptions); }
                catch { /* 单台失败不影响其余 */ }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在刷新…";
        try { await _devices.RefreshAsync(); } catch { }
        RebindTiles();
        StatusText.Text = "就绪";
    }

    private async void OnRecoverAdb(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在重启 adb server…";
        try
        {
            var runner = new ProcessRunner();
            await runner.RunAsync(_bin.Adb, AdbCommands.KillServer(), default);
            await runner.RunAsync(_bin.Adb, AdbCommands.StartServer(), default);
            await _devices.RefreshAsync();
            RebindTiles();
            StatusText.Text = "ADB 已恢复";
        }
        catch (Exception ex) { StatusText.Text = $"恢复失败:{ex.Message}"; }
    }
}
