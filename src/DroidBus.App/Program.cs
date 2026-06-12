using Avalonia;

namespace DroidBus.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DebugLog.Write($"FATAL UnhandledException: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            DebugLog.Write($"UnobservedTaskException: {e.Exception}");
        DebugLog.Write("=== DroidBus.App start ===");
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"FATAL Main: {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
