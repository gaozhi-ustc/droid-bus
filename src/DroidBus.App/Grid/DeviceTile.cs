using DroidBus.Core.Models;

namespace DroidBus.App.Grid;

public sealed class DeviceTile : Panel
{
    private readonly Label _header = new();
    public Panel Surface { get; } = new();  // scrcpy 嵌入这里
    public Device? Device { get; private set; }

    public event Action<DeviceTile>? TileClicked;
    public event Action<DeviceTile>? TileDoubleClicked;

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; UpdateBorder(); }
    }

    public DeviceTile()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(2);
        BackColor = Color.FromArgb(30, 33, 40);

        _header.Dock = DockStyle.Top;
        _header.Height = 22;
        _header.ForeColor = Color.Gainsboro;
        _header.TextAlign = ContentAlignment.MiddleLeft;
        _header.Text = "(空)";

        Surface.Dock = DockStyle.Fill;
        Surface.BackColor = Color.Black;

        Controls.Add(Surface);
        Controls.Add(_header);

        foreach (var c in new Control[] { this, _header, Surface })
        {
            c.Click += (_, _) => TileClicked?.Invoke(this);
            c.DoubleClick += (_, _) => TileDoubleClicked?.Invoke(this);
        }
        UpdateBorder();
    }

    public void Bind(Device? device)
    {
        Device = device;
        UpdateHeader();
    }

    /// 用一帧抓图填充承载面板(缩略条模式:scrcpy 小窗口不重绘,改贴静态图)。
    /// 接管旧图的释放,避免高频刷新泄漏 GDI 位图。
    public void ShowThumbnail(Image img)
    {
        var old = Surface.BackgroundImage;
        Surface.BackgroundImageLayout = ImageLayout.Zoom;
        Surface.BackgroundImage = img;
        if (!ReferenceEquals(old, img)) old?.Dispose();
    }

    public void ClearThumbnail()
    {
        var old = Surface.BackgroundImage;
        Surface.BackgroundImage = null;
        old?.Dispose();
    }

    public void UpdateHeader()
    {
        if (Device is null) { _header.Text = "(空)"; return; }
        var bat = Device.BatteryPercent >= 0 ? $"{Device.BatteryPercent}%" : "?";
        var state = Device.State switch
        {
            DeviceState.Online => "在线",
            DeviceState.Unauthorized => "未授权",
            _ => "离线"
        };
        _header.Text = $"{Device.Model ?? Device.Serial}  [{state}] {bat}";
        _header.ForeColor = Device.State == DeviceState.Online ? Color.Gainsboro : Color.IndianRed;
    }

    private void UpdateBorder()
    {
        Padding = new Padding(_selected ? 3 : 2);
        BackColor = _selected ? Color.FromArgb(224, 108, 43) : Color.FromArgb(30, 33, 40);
    }
}
