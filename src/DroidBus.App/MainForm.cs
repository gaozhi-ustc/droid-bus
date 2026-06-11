using DroidBus.App.Grid;
using DroidBus.Core;
using DroidBus.Core.Adb;
using DroidBus.Core.Devices;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;
using DroidBus.Core.Process;

namespace DroidBus.App;

public sealed class MainForm : Form
{
    private readonly BinaryLocator _bin;
    private readonly DeviceManager _devices;
    private readonly DeviceGridControl _grid = new();
    private readonly FlowLayoutPanel _toolbar = new();
    private readonly DroidBus.App.Controls.ControlPanelView _controlPanel = new();
    private CancellationTokenSource? _pollCts;
    private readonly MirrorController _mirror;
    private readonly DroidBus.App.Scripting.ScriptLauncher _scripts;
    private MirrorOptions _globalOptions = new();
    private DeviceTile? _focusedTile; // 放大中的 tile,null 表示网格模式
    private DroidBus.App.Input.BroadcastOverlay? _overlay;
    private readonly HashSet<string> _imeReady = new(); // 已设好 ADBKeyboard 的设备(每会话一次)
    private readonly HashSet<string> _preFocusMirrored = new(); // 进入放大前在投屏的设备(退回网格只恢复这些)
    private readonly DroidBus.App.Mirror.ThumbnailPoller _thumbs;
    private const int DeviceW = 1440;   // Note9
    private const int DeviceH = 2960;

    // 当前选中的全部 tile(用于群控)
    public IReadOnlyList<DeviceTile> SelectedTiles =>
        _grid.Tiles.Where(t => t.Selected && t.Device is not null).ToList();

    public DeviceGridControl Grid => _grid;
    public BinaryLocator Bin => _bin;

    public MainForm()
    {
        _bin = BinaryLocator.Discover();
        var adb = new AdbClient(new ProcessRunner(), _bin.Adb);
        _devices = new DeviceManager(adb);
        _mirror = new MirrorController(_bin, DeviceW, DeviceH);
        _mirror.RestartRequested = serial =>
        {
            if (!IsHandleCreated) return Task.CompletedTask;
            var tile = _grid.Tiles.FirstOrDefault(t => t.Device?.Serial == serial);
            if (tile is not null)
                BeginInvoke(new Action(async () => await _mirror.StartAsync(tile, _globalOptions)));
            return Task.CompletedTask;
        };
        _scripts = new DroidBus.App.Scripting.ScriptLauncher(_bin);
        _thumbs = new DroidBus.App.Mirror.ThumbnailPoller(_bin.Adb, TimeSpan.FromMilliseconds(150));

        Text = "DroidBus 群控台";
        Width = 1400; Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 20, 25);

        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = 40;
        _toolbar.BackColor = Color.FromArgb(47, 54, 64);
        _toolbar.Padding = new Padding(6, 6, 6, 6);
        AddToolbarButton("全部投屏", OnMirrorAll);
        AddToolbarButton("刷新设备", async (_, _) => await _devices.RefreshAsync());
        AddToolbarButton("修复 ADB", async (_, _) => await RecoverAdbAsync());

        _controlPanel.Dock = DockStyle.Right;
        _controlPanel.Width = 260;

        Controls.Add(_grid);          // Fill
        Controls.Add(_controlPanel);  // Right
        Controls.Add(_toolbar);       // Top

        foreach (var tile in _grid.Tiles)
        {
            tile.TileClicked += OnTileClicked;
            tile.TileDoubleClicked += OnTileDoubleClicked;
        }
        _grid.Resize += (_, _) => _mirror.ResizeAll();

        _controlPanel.OptionsChanged += OnSingleOptionsChanged;
        _controlPanel.ShowTouchesToggled += OnShowTouchesToggled;
        _controlPanel.AudioRequested += OnAudioRequested;
        _controlPanel.TypeTextRequested += OnTypeTextRequested;
        _controlPanel.BroadcastToggled += OnBroadcastToggled;
        _controlPanel.NavRequested += OnNavRequested;

        var ops = _controlPanel.BatchOps;
        ops.InstallApk  += OnBatchInstallApk;
        ops.UninstallApk += OnBatchUninstallApk;
        ops.PushFile    += OnBatchPushFile;
        ops.PullFile    += OnBatchPullFile;
        ops.LaunchApp   += OnBatchLaunchApp;
        ops.RunScript   += OnRunScript;

        _devices.DeviceChanged += OnDeviceChanged;
        Load += async (_, _) => await StartPollingAsync();
        FormClosing += (_, _) => { _pollCts?.Cancel(); _thumbs.Dispose(); _mirror.Dispose(); _overlay?.Dispose(); };
    }

    private void AddToolbarButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        b.Click += onClick;
        _toolbar.Controls.Add(b);
    }

    private async Task StartPollingAsync()
    {
        await _devices.RefreshAsync();
        RebindTiles();
        _pollCts = new CancellationTokenSource();
        _ = _devices.PollLoopAsync(TimeSpan.FromSeconds(3), _pollCts.Token);
    }

    private async Task RecoverAdbAsync()
    {
        var runner = new DroidBus.Core.Process.ProcessRunner();
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.KillServer(), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.StartServer(), default);
        await _devices.RefreshAsync();
        RebindTiles();
        MessageBox.Show("已重启 adb server 并重新枚举设备。", "修复 ADB");
    }

    private void RebindTiles()
    {
        var online = _devices.Devices.OrderBy(d => d.Serial).ToList();
        for (var i = 0; i < _grid.Tiles.Count; i++)
            _grid.Tiles[i].Bind(i < online.Count ? online[i] : null);
    }

    private void OnDeviceChanged(Device d)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (!d.IsControllable) _mirror.Stop(d.Serial); // 掉线/掉授权:停投屏
            RebindTiles();
            foreach (var t in _grid.Tiles) t.UpdateHeader();
        });
    }

    // 选中模型与投屏由 Task 17 接线
    internal DeviceTile? SelectedTile { get; set; }

    private async void OnMirrorAll(object? sender, EventArgs e)
    {
        foreach (var tile in _grid.Tiles)
            if (tile.Device is { IsControllable: true })
                try { await _mirror.StartAsync(tile, _globalOptions); }
                catch { /* 单台失败不影响其余;掉线/窗口未出现等 */ }
    }

    private async void OnTileClicked(DeviceTile tile)
    {
        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        if (!ctrl)
            foreach (var t in _grid.Tiles) t.Selected = false;
        tile.Selected = !tile.Selected || !ctrl;
        SelectedTile = tile;
        _controlPanel.ShowSelected(tile.Device?.Model ?? tile.Device?.Serial);

        // 点击即按需投屏:只 load 这一台(已在投屏则不动),不必走「全部投屏」。
        if (tile.Device is { IsControllable: true } dev && !_mirror.IsMirroring(dev.Serial))
        {
            try { await _mirror.StartAsync(tile, _globalOptions); }
            catch { /* 单台失败忽略,不影响选中 */ }
        }
    }

    private void OnTileDoubleClicked(DeviceTile tile)
    {
        if (_focusedTile is null) FocusTile(tile);
        else RestoreGrid();
    }

    private void FocusTile(DeviceTile focus)
    {
        _focusedTile = focus;
        // 缩略条设备:停掉 scrcpy(缩到极小尺寸只会黑屏),改用低频抓图缩略图。
        // 主控仍走 scrcpy 实时大图。
        var strip = _grid.Tiles
            .Where(t => !ReferenceEquals(t, focus) && t.Device is { IsControllable: true })
            .ToList();
        // 记住进入放大前哪些在实时投屏:退回网格时只恢复这些,不再把 6 台全 load 一遍。
        _preFocusMirrored.Clear();
        foreach (var t in strip)
            if (_mirror.IsMirroring(t.Device!.Serial)) _preFocusMirrored.Add(t.Device!.Serial);
        foreach (var t in strip) _mirror.Stop(t.Device!.Serial);
        _grid.ShowMaster(focus);
        _mirror.ResizeAll();
        _thumbs.Start(strip);
        CoverOverlay();
    }

    /// 让广播捕获层只覆盖主控画面区域(扣掉黑边),而不是整块面板。
    private void CoverOverlay()
    {
        if (_overlay is null || _focusedTile?.Surface is not { } surf) return;
        var box = DroidBus.Core.Control.Letterbox.Fit(
            surf.ClientSize.Width, surf.ClientSize.Height, DeviceW, DeviceH);
        if (box.Width <= 0 || box.Height <= 0) return;
        _overlay.CoverRegion(surf, box.OffsetX, box.OffsetY, box.Width, box.Height);
    }

    private void RestoreGrid()
    {
        _focusedTile = null;
        _thumbs.Stop();
        _grid.ShowGrid();
        foreach (var t in _grid.Tiles) t.ClearThumbnail();
        _mirror.ResizeAll();
        RestoreStripMirrors(); // 只恢复进入放大前在投屏的设备,不再全部 load
        _overlay?.Close(); _overlay?.Dispose(); _overlay = null;
    }

    private async void RestoreStripMirrors()
    {
        // 只恢复进入放大前就在投屏的那些;从没 load 的保持空白(按需点击再 load)。
        foreach (var tile in _grid.Tiles)
            if (tile.Device is { IsControllable: true } dev
                && _preFocusMirrored.Contains(dev.Serial)
                && !_mirror.IsMirroring(dev.Serial))
                try { await _mirror.StartAsync(tile, _globalOptions); }
                catch { /* 单台失败不影响其余 */ }
    }

    private async void OnSingleOptionsChanged()
    {
        if (SelectedTile is not { Device: { IsControllable: true } } tile) return;
        var opts = _controlPanel.Apply(_globalOptions);
        await _mirror.RestartAsync(tile, opts);
    }

    private async void OnShowTouchesToggled(bool on)
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        await ctrl.ExecAsync(dev.Serial, $"shell settings put system show_touches {(on ? 1 : 0)}", default);
    }

    private async void OnAudioRequested()
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var runner = new DroidBus.Core.Process.ProcessRunner();
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.Install(dev.Serial, _bin.SndcpyApk), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.Forward(dev.Serial, 28200), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.StartService(dev.Serial), default);
        MessageBox.Show("已在设备侧启动音频服务。若 PC 无声,请运行 Resources 下的 sndcpy 播放端。");
    }

    private async void OnTypeTextRequested()
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var text = Microsoft.VisualBasic.Interaction.InputBox("输入要发送到设备的文本", "输入文字", "");
        if (string.IsNullOrEmpty(text)) return;
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        await ctrl.TypeUnicodeAsync(dev.Serial, text, default);
    }

    private static readonly object _logLock = new();
    internal static void DbgLog(string msg)
    {
        try { lock (_logLock) System.IO.File.AppendAllText(@"C:\Users\gaozhi\droid-bus\_broadcast.log",
            $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}"); } catch { }
    }

    private void OnBroadcastToggled(bool on)
    {
        DbgLog($"=== KBD-INSTR BUILD === OnBroadcastToggled on={on} focused={_focusedTile?.Device?.Serial ?? "<null>"}");
        if (on)
        {
            // 未放大主控时自动放大一台(选中的优先,否则首台可控),避免「先开广播没反应」的顺序坑。
            if (_focusedTile?.Device is not { IsControllable: true })
            {
                var pick = SelectedTile?.Device is { IsControllable: true } ? SelectedTile
                         : _grid.Tiles.FirstOrDefault(t => t.Device is { IsControllable: true });
                if (pick is null) { MessageBox.Show("没有在线可控设备"); return; }
                FocusTile(pick);
            }
            _overlay = new DroidBus.App.Input.BroadcastOverlay();
            _overlay.Gesture += OnBroadcastGesture;
            _overlay.KeyInput += OnBroadcastKey;
            CoverOverlay();
            _overlay.Show(this);
            _ = EnsureAdbKeyboardAsync(BroadcastTargets()); // 准备 ADBKeyboard 以支持中文键盘输入
        }
        else
        {
            _overlay?.Close();
            _overlay?.Dispose();
            _overlay = null;
        }
    }

    private async void OnBroadcastGesture(int dx, int dy, int ux, int uy)
    {
        DbgLog($"Gesture down=({dx},{dy}) up=({ux},{uy})");
        if (_focusedTile?.Surface is not { } surf) { DbgLog("  abort: no focused surface"); return; }

        // 捕获层只覆盖画面区域(黑边已扣掉),坐标直接相对画面,按真实画面尺寸映射到设备。
        var box = DroidBus.Core.Control.Letterbox.Fit(
            surf.ClientSize.Width, surf.ClientSize.Height, DeviceW, DeviceH);
        if (box.Width <= 0 || box.Height <= 0) { DbgLog("  abort: empty box"); return; }

        var cmd = DroidBus.Core.Control.SyncInputTranslator.Translate(
            dx, dy, ux, uy, box.Width, box.Height, DeviceW, DeviceH);
        DbgLog($"  box=({box.Width}x{box.Height}) cmd={cmd}");

        // 广播给所有在线设备(含主控自己 —— 捕获层挡住了主控的 scrcpy 输入,得走 adb)。
        // 不能用 SelectedTiles:双击放大那一下会先触发 Click 把多选打散成单选,只剩主控。
        var serials = BroadcastTargets();
        DbgLog($"  targets[{serials.Count}]: {string.Join(",", serials)}");

        var ctrl = new DroidBus.Core.Control.AdbDeviceController(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);

        var result = await DroidBus.Core.Batch.BatchExecutor.RunAsync(serials, async (s, ct) =>
        {
            switch (cmd)
            {
                case DroidBus.Core.Script.TapCommand t:
                    await ctrl.TapAsync(s, t.X, t.Y, ct);
                    break;
                case DroidBus.Core.Script.SwipeCommand sw:
                    await ctrl.SwipeAsync(s, sw.X1, sw.Y1, sw.X2, sw.Y2, 200, ct);
                    break;
            }
        }, onProgress: _ => { }, ct: default);
        DbgLog($"  result ok[{result.Succeeded.Count}]={string.Join(",", result.Succeeded)} " +
               $"fail[{result.Failed.Count}]={string.Join("; ", result.Failed.Select(kv => kv.Key + ":" + kv.Value))}");
    }

    /// 广播目标:所有在线可控设备(与触摸广播一致)。
    private IReadOnlyList<string> BroadcastTargets() =>
        DroidBus.Core.Control.BroadcastPlan.Targets(
            _grid.Tiles.Where(t => t.Device is not null)
                       .Select(t => (t.Device!.Serial, t.Device!.IsControllable)));

    /// 广播态宿主键盘输入:文本走 ADBKeyboard(Unicode),特殊键走 keyevent,发给所有在线设备。
    private async void OnBroadcastKey(DroidBus.Core.Control.KeyAction action)
    {
        var serials = BroadcastTargets();
        DbgLog($"OnBroadcastKey action={action} targets[{serials.Count}]");
        if (serials.Count == 0) return;
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(new ProcessRunner(), _bin.Adb);
        var result = await DroidBus.Core.Batch.BatchExecutor.RunAsync(serials, async (s, ct) =>
        {
            switch (action)
            {
                case DroidBus.Core.Control.TypeTextAction t:
                    await ctrl.TypeUnicodeAsync(s, t.Text, ct);
                    break;
                case DroidBus.Core.Control.KeyEventAction k:
                    await ctrl.KeyEventAsync(s, k.AndroidKeyCode, ct);
                    break;
            }
        }, onProgress: _ => { }, ct: default);
        DbgLog($"  key result ok[{result.Succeeded.Count}] fail[{result.Failed.Count}]={string.Join(";", result.Failed.Select(kv => kv.Key + ":" + kv.Value))}");
    }

    /// 一次性、幂等地在目标设备上装并设 ADBKeyboard 为当前输入法(支持中文键盘输入)。
    /// 每个 serial 每会话只做一次;特殊键不依赖它,仅文本路径需要。
    private async Task EnsureAdbKeyboardAsync(IReadOnlyList<string> serials)
    {
        List<string> todo;
        lock (_imeReady) todo = serials.Where(s => !_imeReady.Contains(s)).ToList();
        if (todo.Count == 0) return;

        var runner = new ProcessRunner();
        var result = await DroidBus.Core.Batch.BatchExecutor.RunAsync(todo, async (s, ct) =>
        {
            await runner.RunAsync(_bin.Adb, AdbCommands.InstallApk(s, _bin.AdbKeyboardApk), ct);
            await runner.RunAsync(_bin.Adb, DroidBus.Core.Input.ImeCommands.Enable(s), ct);
            await runner.RunAsync(_bin.Adb, DroidBus.Core.Input.ImeCommands.Set(s), ct);
            lock (_imeReady) _imeReady.Add(s);
        }, onProgress: _ => { }, ct: default);
        DbgLog($"EnsureAdbKeyboard todo[{todo.Count}] ok[{result.Succeeded.Count}] fail[{result.Failed.Count}]={string.Join(";", result.Failed.Select(kv => kv.Key + ":" + kv.Value))}");
    }

    private async void OnNavRequested(int keycode)
    {
        // 广播态下导航键也发给所有在线设备(和触摸广播一致);否则只发给选中设备。
        var serials = _overlay is not null ? BroadcastTargets() : SelectedSerials();
        if (serials.Count == 0) return;

        var ctrl = new DroidBus.Core.Control.AdbDeviceController(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        await DroidBus.Core.Batch.BatchExecutor.RunAsync(
            serials, (s, ct) => ctrl.KeyEventAsync(s, keycode, ct), onProgress: _ => { }, ct: default);
    }

    private IReadOnlyList<string> SelectedSerials() =>
        SelectedTiles.Select(t => t.Device!.Serial).ToList();

    private DroidBus.Core.Process.ProcessRunner Runner() => new();

    private async Task RunBatchAsync(string title,
        Func<string, CancellationToken, Task> perDevice)
    {
        var serials = SelectedSerials();
        if (serials.Count == 0) { MessageBox.Show("未选中任何设备"); return; }
        var result = await DroidBus.Core.Batch.BatchExecutor.RunAsync(
            serials, perDevice, onProgress: _ => { }, ct: default);
        MessageBox.Show(DroidBus.Core.Batch.BatchReport.Summarize(result), title);
    }

    private static string? PickFile()
    {
        using var d = new OpenFileDialog();
        return d.ShowDialog() == DialogResult.OK ? d.FileName : null;
    }

    private async void OnBatchInstallApk()
    {
        var apk = PickFile(); if (apk is null) return;
        await RunBatchAsync("批量装 APK", (s, ct) =>
            Runner().RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.InstallApk(s, apk), ct));
    }

    private async void OnBatchUninstallApk()
    {
        var pkg = Microsoft.VisualBasic.Interaction.InputBox("要卸载的包名", "批量卸 APK", "");
        if (string.IsNullOrWhiteSpace(pkg)) return;
        await RunBatchAsync("批量卸 APK", (s, ct) =>
            Runner().RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.Uninstall(s, pkg), ct));
    }

    private async void OnBatchPushFile()
    {
        var local = PickFile(); if (local is null) return;
        var remote = Microsoft.VisualBasic.Interaction.InputBox("设备目标路径", "批量推文件", "/sdcard/");
        if (string.IsNullOrWhiteSpace(remote)) return;
        await RunBatchAsync("批量推文件", (s, ct) =>
            Runner().RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.Push(s, local, remote), ct));
    }

    private async void OnBatchPullFile()
    {
        var remote = Microsoft.VisualBasic.Interaction.InputBox("设备文件路径", "批量拉文件", "/sdcard/");
        if (string.IsNullOrWhiteSpace(remote)) return;
        using var fb = new FolderBrowserDialog();
        if (fb.ShowDialog() != DialogResult.OK) return;
        await RunBatchAsync("批量拉文件", (s, ct) =>
        {
            var dest = System.IO.Path.Combine(fb.SelectedPath, $"{s}_{System.IO.Path.GetFileName(remote)}");
            return Runner().RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.Pull(s, remote, dest), ct);
        });
    }

    private async void OnBatchLaunchApp()
    {
        var pkg = Microsoft.VisualBasic.Interaction.InputBox("要启动的包名", "批量启动应用", "");
        if (string.IsNullOrWhiteSpace(pkg)) return;
        await RunBatchAsync("批量启动应用", (s, ct) =>
            Runner().RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.StartApp(s, pkg), ct));
    }

    private async void OnRunScript()
    {
        var serials = SelectedSerials();
        if (serials.Count == 0) { MessageBox.Show("未选中任何设备"); return; }

        using var d = new OpenFileDialog { Filter = "ADB 脚本 (*.adb)|*.adb|所有文件 (*.*)|*.*" };
        if (d.ShowDialog() != DialogResult.OK) return;

        DroidBus.Core.Batch.BatchResult result;
        try
        {
            result = await _scripts.RunFileAsync(d.FileName, serials, default);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"脚本解析/执行出错:{ex.Message}", "跑脚本");
            return;
        }
        MessageBox.Show(DroidBus.Core.Batch.BatchReport.Summarize(result), "跑脚本");
    }
}
