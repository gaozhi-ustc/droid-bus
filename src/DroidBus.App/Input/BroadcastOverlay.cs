using DroidBus.Core.Control;

namespace DroidBus.App.Input;

/// 贴在主控台画面之上的半透明捕获层。捕获一次按下→抬起,回调原始格子像素坐标;
/// 同时持有键盘焦点,把宿主机键盘(含 IME 合成的中文)捕获并向上抛 KeyAction。
public sealed class BroadcastOverlay : Form
{
    private Point _down;
    private bool _dragging;

    /// (downX,downY,upX,upY) —— 相对捕获层(= 主控台画面)的像素坐标。
    public event Action<int, int, int, int>? Gesture;

    /// 宿主键盘输入翻译后的设备动作(文本/特殊键)。
    public event Action<KeyAction>? KeyInput;

    public BroadcastOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.DeepSkyBlue;
        Opacity = 0.12; // 轻微着色,提示「广播中」
        KeyPreview = true;
    }

    /// 只贴合目标控件内的画面区域(去掉 scrcpy 居中后的黑边)。
    /// 蓝色提示层正好覆盖可操作的手机画面,用户一眼看清点哪、且每个点都落在有效区。
    public void CoverRegion(Control target, int offsetX, int offsetY, int width, int height)
    {
        Location = target.PointToScreen(new Point(offsetX, offsetY));
        Size = new Size(width, height);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // 抢到键盘焦点,IME 才会把合成字符交付到本层(否则会被嵌入的 scrcpy 子窗吃掉)。
        Activate();
        var ok = Focus();
        MainForm.DbgLog($"overlay OnShown Activate+Focus()={ok} CanFocus={CanFocus} ContainsFocus={ContainsFocus}");
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _down = e.Location; _dragging = true;
        Focus(); // 点击后重新确保键盘焦点在捕获层
        MainForm.DbgLog($"overlay MouseDown at {e.Location}");
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        MainForm.DbgLog($"overlay MouseUp at {e.Location} dragging={_dragging}");
        if (_dragging)
        {
            _dragging = false;
            Gesture?.Invoke(_down.X, _down.Y, e.X, e.Y);
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        MainForm.DbgLog($"overlay KeyDown vk={(int)e.KeyCode} ({e.KeyCode})");
        // 特殊/导航/编辑键 -> keyevent;普通字符键留给 OnKeyPress(等 IME 合成后)。
        if (KeyTranslator.FromVirtualKey((int)e.KeyCode) is { } action)
        {
            KeyInput?.Invoke(action);
            e.Handled = true;
            e.SuppressKeyPress = true; // 别再产生 KeyPress / 系统提示音
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        MainForm.DbgLog($"overlay KeyPress code={(int)e.KeyChar}");
        // 可打印字符(含 IME 合成的中文)-> 文本注入。
        if (KeyTranslator.FromChar(e.KeyChar) is { } action)
        {
            KeyInput?.Invoke(action);
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }
}
