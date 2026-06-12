using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DroidBus.Core.Models;

namespace DroidBus.App.Views;

public partial class DeviceTile : UserControl
{
    private Device? _device;

    public DeviceTile()
    {
        InitializeComponent();
    }

    public Device? Device => _device;

    /// 点击/双击事件。
    public event Action<DeviceTile>? TileClicked;
    public event Action<DeviceTile>? TileDoubleClicked;

    /// 画面区域(供 X11 reparent 定位用)。
    public Control ScreenSurface => ScreenArea;

    /// 画面区域在窗口坐标系中的像素区域(scrcpy 应填充此区域)。
    public Rect ScreenRectInWindow
    {
        get
        {
            var topLevel = this.VisualRoot as Visual;
            if (topLevel is null) return new Rect(0, 0, 0, 0);
            var pos = ScreenArea.TranslatePoint(new Point(0, 0), topLevel);
            if (pos is null) return new Rect(0, 0, 0, 0);
            return new Rect(pos.Value.X, pos.Value.Y, ScreenArea.Bounds.Width, ScreenArea.Bounds.Height);
        }
    }

    /// 绑定设备信息到标签。
    public void Bind(Device? device)
    {
        _device = device;
        SerialLabel.Text = device?.Serial ?? "--";
        BatteryLabel.Text = device is { BatteryPercent: >= 0 } ? $"🔋{device.BatteryPercent}%" : "";
        var color = device?.IsControllable == true
            ? Color.FromRgb(0x4c, 0xaf, 0x50)
            : Color.FromRgb(0xe5, 0x39, 0x35);
        StatusDot.Background = new SolidColorBrush(color);
    }

    /// 在缩略图模式下显示静态预览(降级路径)。
    public void ShowThumbnail(Avalonia.Media.Imaging.Bitmap bmp) { /* TODO: Task 5d */ }
    public void ClearThumbnail() { /* TODO: Task 5d */ }

    // ---- 输入事件 ---------------------------------------
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        TileClicked?.Invoke(this);
        if (e.ClickCount >= 2)
            TileDoubleClicked?.Invoke(this);
    }
}
