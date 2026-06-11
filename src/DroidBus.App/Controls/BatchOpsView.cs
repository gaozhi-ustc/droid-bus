namespace DroidBus.App.Controls;

/// 批量操作按钮组。事件由 MainForm 处理(它持有选中设备与执行器)。
public sealed class BatchOpsView : FlowLayoutPanel
{
    public event Action? InstallApk;
    public event Action? UninstallApk;
    public event Action? PushFile;
    public event Action? PullFile;
    public event Action? LaunchApp;
    public event Action? RunScript;

    public BatchOpsView()
    {
        Dock = DockStyle.Fill;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;

        Add("批量装 APK", () => InstallApk?.Invoke());
        Add("批量卸 APK", () => UninstallApk?.Invoke());
        Add("批量推文件", () => PushFile?.Invoke());
        Add("批量拉文件", () => PullFile?.Invoke());
        Add("批量启动应用", () => LaunchApp?.Invoke());
        Add("跑脚本(.adb)", () => RunScript?.Invoke());
    }

    private void Add(string text, Action onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Width = 220, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        b.Click += (_, _) => onClick();
        Controls.Add(b);
    }
}
