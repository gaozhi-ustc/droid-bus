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

    /// 画面区域在顶层窗口坐标系中的【物理像素】矩形(scrcpy 子窗口应填充此区域)。
    /// 嵌入的原生窗口用物理像素定位,故需乘 RenderScaling。返回空矩形表示尚不可定位。
    public PixelRect ScreenRectInWindowPx
    {
        get
        {
            if (this.VisualRoot is not Visual root) return default;
            var pos = ScreenArea.TranslatePoint(new Point(0, 0), root);
            if (pos is null) return default;
            var scale = (this.VisualRoot as TopLevel)?.RenderScaling ?? 1.0;
            return new PixelRect(
                (int)(pos.Value.X * scale),
                (int)(pos.Value.Y * scale),
                (int)(ScreenArea.Bounds.Width * scale),
                (int)(ScreenArea.Bounds.Height * scale));
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
