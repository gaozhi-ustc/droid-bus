using DroidBus.Core.Mirror;

namespace DroidBus.App.Controls;

/// 右侧控制栏:上半批量区(后续任务填充),下半单台开关。
public sealed class ControlPanelView : Panel
{
    public FlowLayoutPanel BatchArea { get; } = new();
    public BatchOpsView BatchOps { get; } = new();
    private readonly FlowLayoutPanel _single = new();
    private readonly CheckBox _record = new() { Text = "录屏" };
    private readonly CheckBox _screenOff = new() { Text = "息屏投屏" };
    private readonly CheckBox _stayAwake = new() { Text = "常亮不黑屏" };
    private readonly CheckBox _showTouches = new() { Text = "显示触摸" };
    private readonly CheckBox _broadcast = new() { Text = "同步输入广播", ForeColor = Color.Gold, AutoSize = true };
    private readonly Label _title = new()
    {
        Text = "未选中设备", Dock = DockStyle.Top, Height = 24, ForeColor = Color.Gainsboro
    };

    /// 单台投屏开关变化(录屏/息屏/常亮),由 MainForm 订阅以重投。
    public event Action? OptionsChanged;
    /// 显示触摸切换(true=开),走 adb 不重投。
    public event Action<bool>? ShowTouchesToggled;
    public event Action? AudioRequested;
    public event Action? TypeTextRequested;
    public event Action<bool>? BroadcastToggled;
    /// 导航键:参数为 Android keycode(返回 4 / 主页 3 / 最近 187)。
    public event Action<int>? NavRequested;

    public ControlPanelView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(38, 41, 50);
        ForeColor = Color.Gainsboro;
        Padding = new Padding(8);

        BatchArea.Dock = DockStyle.Top;
        BatchArea.Height = 380;
        BatchArea.FlowDirection = FlowDirection.TopDown;
        BatchArea.WrapContents = false;
        BatchArea.AutoScroll = true;
        BatchArea.Controls.Add(BatchOps);

        _single.Dock = DockStyle.Fill;
        _single.FlowDirection = FlowDirection.TopDown;
        _single.WrapContents = false;
        foreach (var cb in new[] { _record, _screenOff, _stayAwake, _showTouches })
        {
            cb.ForeColor = Color.Gainsboro;
            cb.AutoSize = true;
        }
        _record.CheckedChanged += (_, _) => OptionsChanged?.Invoke();
        _screenOff.CheckedChanged += (_, _) => OptionsChanged?.Invoke();
        _stayAwake.CheckedChanged += (_, _) => OptionsChanged?.Invoke();
        _showTouches.CheckedChanged += (_, _) => ShowTouchesToggled?.Invoke(_showTouches.Checked);
        _broadcast.CheckedChanged += (_, _) => BroadcastToggled?.Invoke(_broadcast.Checked);

        var nav = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
        nav.Controls.Add(MakeNavButton("返回", 4));
        nav.Controls.Add(MakeNavButton("主页", 3));
        nav.Controls.Add(MakeNavButton("最近", 187));

        _single.Controls.Add(_title);
        _single.Controls.Add(nav);
        _single.Controls.Add(_record);
        _single.Controls.Add(_screenOff);
        _single.Controls.Add(_stayAwake);
        _single.Controls.Add(_showTouches);
        _single.Controls.Add(_broadcast);

        var audioBtn = new Button { Text = "转发音频", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        var typeBtn = new Button { Text = "输入文字", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        audioBtn.Click += (_, _) => AudioRequested?.Invoke();
        typeBtn.Click += (_, _) => TypeTextRequested?.Invoke();
        _single.Controls.Add(audioBtn);
        _single.Controls.Add(typeBtn);

        Controls.Add(_single);     // Fill
        Controls.Add(BatchArea);   // Top
    }

    private Button MakeNavButton(string text, int keycode)
    {
        var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        b.Click += (_, _) => NavRequested?.Invoke(keycode);
        return b;
    }

    public void ShowSelected(string? title)
    {
        _title.Text = title is null ? "未选中设备" : $"选中:{title}";
        _single.Enabled = title is not null;
    }

    /// 把当前开关写入 options(供重投使用)。
    public MirrorOptions Apply(MirrorOptions baseOptions) => baseOptions with
    {
        Record = _record.Checked,
        TurnScreenOff = _screenOff.Checked,
        StayAwake = _stayAwake.Checked,
    };
}
