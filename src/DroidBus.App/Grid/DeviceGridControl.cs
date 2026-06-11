namespace DroidBus.App.Grid;

/// 设备画面墙。两种布局:
///  - 网格模式:columns×rows 平铺;
///  - 大主控模式:主控占左侧大块,其余设备在右侧竖排缩略条(广播时可一边操作主控一边看其余跟动)。
public sealed class DeviceGridControl : Panel
{
    private const int Gap = 4;
    private readonly int _cols;
    private readonly int _rows;
    private DeviceTile? _master; // null = 网格模式

    public IReadOnlyList<DeviceTile> Tiles { get; }

    public DeviceGridControl(int columns = 3, int rows = 2)
    {
        _cols = columns;
        _rows = rows;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(18, 20, 25);

        var tiles = new List<DeviceTile>();
        for (var i = 0; i < columns * rows; i++)
            tiles.Add(new DeviceTile());
        Tiles = tiles;

        // 先赋值 Tiles 再加进 Controls —— Controls.Add 会触发一次 OnLayout,
        // 那时若 Tiles 还是 null 会 NRE。SuspendLayout 顺带省掉每加一个就重排一次。
        SuspendLayout();
        foreach (var tile in tiles) Controls.Add(tile);
        ResumeLayout(true);
    }

    public bool IsFocused => _master is not null;

    /// 网格模式:平铺全部 tile。
    public void ShowGrid()
    {
        _master = null;
        PerformLayout();
    }

    /// 大主控模式:master 占左侧大块,其余在右侧竖排缩略条。
    public void ShowMaster(DeviceTile master)
    {
        _master = master;
        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Tiles is null) return; // 构造期 base.ctor 可能先触发一次布局
        var w = ClientSize.Width;
        var h = ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        if (_master is null)
        {
            var cw = (w - Gap * (_cols + 1)) / _cols;
            var ch = (h - Gap * (_rows + 1)) / _rows;
            for (var i = 0; i < Tiles.Count; i++)
            {
                var c = i % _cols;
                var r = i / _cols;
                Tiles[i].Visible = true;
                Tiles[i].Bounds = new Rectangle(Gap + c * (cw + Gap), Gap + r * (ch + Gap), cw, ch);
            }
            return;
        }

        var others = Tiles.Where(t => !ReferenceEquals(t, _master)).ToList();
        var stripW = Math.Clamp(w / 5, 120, 280);
        var masterW = w - stripW - Gap * 3;

        _master.Visible = true;
        _master.Bounds = new Rectangle(Gap, Gap, masterW, h - Gap * 2);

        var n = Math.Max(others.Count, 1);
        var sh = (h - Gap * (n + 1)) / n;
        for (var i = 0; i < others.Count; i++)
        {
            others[i].Visible = true;
            others[i].Bounds = new Rectangle(masterW + Gap * 2, Gap + i * (sh + Gap), stripW, sh);
        }
    }
}
