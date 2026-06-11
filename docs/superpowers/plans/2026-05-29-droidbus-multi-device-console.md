# DroidBus 多设备群控台 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把单设备的「安卓投屏」重做成一个 Windows 多设备群控台:同屏监看 6 块 Note9 的实时画面、单台放大精控、批量群控,并开放原 app 所有锁定功能。

**Architecture:** 方案 1 —— 宿主程序(C#/.NET 8 WinForms)用 Win32 `SetParent` 把每台设备的 scrcpy 窗口嵌入网格格子,复用 scrcpy 2.0 的渲染与控制;控制层做同步输入广播、批量任务、脚本引擎。纯逻辑(解析器、命令构造、批量编排)放在 `DroidBus.Core` 类库做 TDD;UI 与原生互操作放在 `DroidBus.App`,用真机做集成验证。

**Tech Stack:** C# / .NET 8;WinForms(`net8.0-windows`);xUnit + FluentAssertions;复用已安装的 `scrcpy.exe`(2.0)/`adb.exe`/`sndcpy.apk`/`Adbkeyboard.apk`(位于 `C:\Program Files (x86)\Androidscreen\Resources\`)。

**约定:**
- 解决方案根:`C:\Users\gaozhi\droid-bus`,源码在 `src/`,测试在 `tests/`。
- 所有 adb/scrcpy 调用通过 `IProcessRunner` 抽象,便于单元测试用 fake。
- 标有 **【真机验证】** 的步骤无法单元测试,用 6 块已授权的 Note9 真机手动验证。
- git 身份用一次性参数:`git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit ...`(不改全局配置)。

**目标设备(已授权):** `2620e8b738037ece 267063a5431c7ece 2771ac69ac1c7ece 28b3e9657a3f7ece 29299ad508047ece 525659584b443498`

---

## 文件结构

```
droid-bus/
├─ DroidBus.sln
├─ src/
│  ├─ DroidBus.Core/                 # 纯逻辑类库 (net8.0)，无 WinForms 依赖
│  │  ├─ DroidBus.Core.csproj
│  │  ├─ Models/Device.cs            # Device record + DeviceState 枚举
│  │  ├─ Process/IProcessRunner.cs   # 进程抽象 + ProcessResult
│  │  ├─ Process/ProcessRunner.cs    # 真实实现
│  │  ├─ BinaryLocator.cs            # 定位 adb/scrcpy/apk 路径
│  │  ├─ Adb/AdbClient.cs            # adb 封装 + 解析
│  │  ├─ Adb/AdbCommands.cs          # 静态命令参数构造器
│  │  ├─ Devices/DeviceManager.cs    # 发现 + 轮询
│  │  ├─ Mirror/MirrorOptions.cs     # 投屏启动选项
│  │  ├─ Mirror/ScrcpyArgsBuilder.cs # MirrorOptions -> scrcpy 参数
│  │  ├─ Batch/BatchExecutor.cs      # 并行批量执行 + 结果汇总
│  │  ├─ Control/IDeviceController.cs# tap/swipe/text/key 抽象
│  │  ├─ Control/AdbDeviceController.cs
│  │  ├─ Control/SyncInputTranslator.cs # 指针事件 -> adb 命令
│  │  ├─ Script/ScriptCommand.cs     # DSL 命令模型
│  │  ├─ Script/ScriptParser.cs      # .adb GBK DSL 解析
│  │  ├─ Script/ScriptRunner.cs      # 在一台设备上执行命令序列
│  │  └─ Time/IClock.cs              # 可注入时钟(延时)
│  └─ DroidBus.App/                  # WinForms (net8.0-windows)
│     ├─ DroidBus.App.csproj
│     ├─ Program.cs
│     ├─ MainForm.cs                 # 顶部工具条 + 网格 + 右控制栏
│     ├─ Grid/DeviceGridControl.cs   # 3×2 网格容器
│     ├─ Grid/DeviceTile.cs          # 单格(承载 scrcpy 窗口 + 选中高亮)
│     ├─ Mirror/ScrcpyHost.cs        # 拉起 scrcpy + SetParent 嵌入 + resize
│     ├─ Interop/NativeMethods.cs    # P/Invoke
│     └─ ControlPanel/ControlPanel.cs# 右侧批量+单台控制
└─ tests/
   └─ DroidBus.Core.Tests/
      ├─ DroidBus.Core.Tests.csproj
      └─ ...(每个被测单元一个文件)
```

---

## Task 0: 解决方案脚手架

**Files:**
- Create: `DroidBus.sln`, `src/DroidBus.Core/DroidBus.Core.csproj`, `src/DroidBus.App/DroidBus.App.csproj`, `tests/DroidBus.Core.Tests/DroidBus.Core.Tests.csproj`

- [ ] **Step 1: 确认 .NET 8 SDK 可用**

Run: `dotnet --version`
Expected: 输出 `8.x.x`(若无,先安装 .NET 8 SDK)。

- [ ] **Step 2: 创建解决方案与三个项目**

Run:
```bash
cd /c/Users/gaozhi/droid-bus
dotnet new sln -n DroidBus
dotnet new classlib -n DroidBus.Core -o src/DroidBus.Core -f net8.0
dotnet new winforms -n DroidBus.App -o src/DroidBus.App -f net8.0-windows
dotnet new xunit -n DroidBus.Core.Tests -o tests/DroidBus.Core.Tests -f net8.0
dotnet sln add src/DroidBus.Core src/DroidBus.App tests/DroidBus.Core.Tests
dotnet add src/DroidBus.App reference src/DroidBus.Core
dotnet add tests/DroidBus.Core.Tests reference src/DroidBus.Core
dotnet add tests/DroidBus.Core.Tests package FluentAssertions
```
删除模板生成的 `src/DroidBus.Core/Class1.cs`。

- [ ] **Step 3: 构建验证**

Run: `dotnet build`
Expected: `Build succeeded`,0 error。

- [ ] **Step 4: 写一个最小通过测试(脚手架自检)**

Create `tests/DroidBus.Core.Tests/SanityTests.cs`:
```csharp
namespace DroidBus.Core.Tests;

public class SanityTests
{
    [Fact]
    public void Sln_builds_and_tests_run()
    {
        true.Should().BeTrue();
    }
}
```
在文件顶部加 `using FluentAssertions;`。

- [ ] **Step 5: 运行测试**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "chore: scaffold DroidBus solution (Core/App/Tests)"
```

---

## Task 1: Device 模型

**Files:**
- Create: `src/DroidBus.Core/Models/Device.cs`
- Test: `tests/DroidBus.Core.Tests/DeviceTests.cs`

- [ ] **Step 1: 写失败测试**

Create `tests/DroidBus.Core.Tests/DeviceTests.cs`:
```csharp
using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class DeviceTests
{
    [Fact]
    public void Online_device_is_controllable()
    {
        var d = new Device("29299ad508047ece", DeviceState.Online) { Model = "SM-N960U1", BatteryPercent = 80 };
        d.IsControllable.Should().BeTrue();
        d.Serial.Should().Be("29299ad508047ece");
    }

    [Theory]
    [InlineData(DeviceState.Offline)]
    [InlineData(DeviceState.Unauthorized)]
    public void Non_online_device_is_not_controllable(DeviceState state)
    {
        new Device("x", state).IsControllable.Should().BeFalse();
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test --filter DeviceTests`
Expected: FAIL,编译错误 `Device` / `DeviceState` 不存在。

- [ ] **Step 3: 实现模型**

Create `src/DroidBus.Core/Models/Device.cs`:
```csharp
namespace DroidBus.Core.Models;

public enum DeviceState { Online, Offline, Unauthorized }

public sealed class Device
{
    public Device(string serial, DeviceState state)
    {
        Serial = serial;
        State = state;
    }

    public string Serial { get; }
    public DeviceState State { get; set; }
    public string? Model { get; set; }
    public int BatteryPercent { get; set; } = -1;

    public bool IsControllable => State == DeviceState.Online;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test --filter DeviceTests`
Expected: PASS(3 个)。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add Device model"
```

---

## Task 2: 进程抽象 IProcessRunner

**Files:**
- Create: `src/DroidBus.Core/Process/IProcessRunner.cs`, `src/DroidBus.Core/Process/ProcessRunner.cs`
- Test: `tests/DroidBus.Core.Tests/ProcessRunnerTests.cs`

- [ ] **Step 1: 写失败测试(用真实进程跑 cmd)**

Create `tests/DroidBus.Core.Tests/ProcessRunnerTests.cs`:
```csharp
using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task Runs_process_and_captures_stdout()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync("cmd.exe", new[] { "/c", "echo", "hello" });
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Be("hello");
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter ProcessRunnerTests`
Expected: FAIL,`ProcessRunner` 不存在。

- [ ] **Step 3: 实现接口与真实实现**

Create `src/DroidBus.Core/Process/IProcessRunner.cs`:
```csharp
namespace DroidBus.Core.Process;

public readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string exe,
        IReadOnlyList<string> args,
        CancellationToken ct = default);
}
```

Create `src/DroidBus.Core/Process/ProcessRunner.cs`:
```csharp
using System.Diagnostics;
using System.Text;

namespace DroidBus.Core.Process;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string exe, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new System.Diagnostics.Process { StartInfo = psi };
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) outBuf.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) errBuf.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        return new ProcessResult(p.ExitCode, outBuf.ToString(), errBuf.ToString());
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter ProcessRunnerTests`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add IProcessRunner abstraction + real impl"
```

---

## Task 3: BinaryLocator

定位 `adb.exe / scrcpy.exe / scrcpy-server / sndcpy.apk / Adbkeyboard.apk`。优先级:环境变量 `DROIDBUS_TOOLS` 覆盖 → 默认 `C:\Program Files (x86)\Androidscreen\Resources`。

**Files:**
- Create: `src/DroidBus.Core/BinaryLocator.cs`
- Test: `tests/DroidBus.Core.Tests/BinaryLocatorTests.cs`

- [ ] **Step 1: 写失败测试(用临时目录造假二进制)**

Create `tests/DroidBus.Core.Tests/BinaryLocatorTests.cs`:
```csharp
using DroidBus.Core;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BinaryLocatorTests
{
    [Fact]
    public void Locates_tools_in_given_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in new[] { "adb.exe", "scrcpy.exe", "scrcpy-server", "sndcpy.apk", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(dir, f), "x");

        var locator = BinaryLocator.FromDirectory(dir);

        locator.Adb.Should().Be(Path.Combine(dir, "adb.exe"));
        locator.Scrcpy.Should().Be(Path.Combine(dir, "scrcpy.exe"));
        locator.SndcpyApk.Should().Be(Path.Combine(dir, "sndcpy.apk"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Throws_when_adb_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var act = () => BinaryLocator.FromDirectory(dir);
        act.Should().Throw<FileNotFoundException>().WithMessage("*adb.exe*");
        Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter BinaryLocatorTests`
Expected: FAIL,`BinaryLocator` 不存在。

- [ ] **Step 3: 实现**

Create `src/DroidBus.Core/BinaryLocator.cs`:
```csharp
namespace DroidBus.Core;

public sealed class BinaryLocator
{
    public const string DefaultDir = @"C:\Program Files (x86)\Androidscreen\Resources";

    private BinaryLocator(string dir)
    {
        Adb = Require(dir, "adb.exe");
        Scrcpy = Require(dir, "scrcpy.exe");
        ScrcpyServer = Require(dir, "scrcpy-server");
        SndcpyApk = Require(dir, "sndcpy.apk");
        AdbKeyboardApk = Require(dir, "Adbkeyboard.apk");
        Dir = dir;
    }

    public string Dir { get; }
    public string Adb { get; }
    public string Scrcpy { get; }
    public string ScrcpyServer { get; }
    public string SndcpyApk { get; }
    public string AdbKeyboardApk { get; }

    public static BinaryLocator FromDirectory(string dir) => new(dir);

    public static BinaryLocator Discover()
    {
        var env = Environment.GetEnvironmentVariable("DROIDBUS_TOOLS");
        return new(string.IsNullOrWhiteSpace(env) ? DefaultDir : env);
    }

    private static string Require(string dir, string file)
    {
        var p = Path.Combine(dir, file);
        if (!File.Exists(p))
            throw new FileNotFoundException($"找不到必需的二进制 {file}(目录:{dir})", p);
        return p;
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter BinaryLocatorTests`
Expected: PASS(2 个)。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add BinaryLocator"
```

---

## Task 4: AdbClient — 解析 `adb devices -l`

**Files:**
- Create: `src/DroidBus.Core/Adb/AdbClient.cs`
- Test: `tests/DroidBus.Core.Tests/AdbClientParseTests.cs`

- [ ] **Step 1: 写失败测试(纯解析,不起进程)**

Create `tests/DroidBus.Core.Tests/AdbClientParseTests.cs`:
```csharp
using DroidBus.Core.Adb;
using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbClientParseTests
{
    [Fact]
    public void Parses_device_lines_with_state_and_model()
    {
        const string output = """
            List of devices attached
            29299ad508047ece       device product:crownqlteue model:SM-N960U1 device:crownqlteue transport_id:5
            525659584b443498       unauthorized
            2620e8b738037ece       offline

            """;

        var devices = AdbClient.ParseDevices(output);

        devices.Should().HaveCount(3);
        devices[0].Serial.Should().Be("29299ad508047ece");
        devices[0].State.Should().Be(DeviceState.Online);
        devices[0].Model.Should().Be("SM-N960U1");
        devices[1].State.Should().Be(DeviceState.Unauthorized);
        devices[2].State.Should().Be(DeviceState.Offline);
    }

    [Fact]
    public void Ignores_header_and_blank_lines()
    {
        AdbClient.ParseDevices("List of devices attached\n\n").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter AdbClientParseTests`
Expected: FAIL,`AdbClient` 不存在。

- [ ] **Step 3: 实现 AdbClient(解析 + 实例方法)**

Create `src/DroidBus.Core/Adb/AdbClient.cs`:
```csharp
using System.Text.RegularExpressions;
using DroidBus.Core.Models;
using DroidBus.Core.Process;

namespace DroidBus.Core.Adb;

public sealed class AdbClient
{
    private readonly IProcessRunner _runner;
    private readonly string _adb;

    public AdbClient(IProcessRunner runner, string adbPath)
    {
        _runner = runner;
        _adb = adbPath;
    }

    public static IReadOnlyList<Device> ParseDevices(string adbDevicesOutput)
    {
        var list = new List<Device>();
        foreach (var raw in adbDevicesOutput.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var serial = parts[0];
            var rest = parts[1].Trim();
            var state = rest.StartsWith("device")
                ? DeviceState.Online
                : rest.StartsWith("unauthorized") ? DeviceState.Unauthorized : DeviceState.Offline;

            var device = new Device(serial, state);
            var m = Regex.Match(rest, @"model:(\S+)");
            if (m.Success) device.Model = m.Groups[1].Value;
            list.Add(device);
        }
        return list;
    }

    public async Task<IReadOnlyList<Device>> ListDevicesAsync(CancellationToken ct = default)
    {
        var r = await _runner.RunAsync(_adb, new[] { "devices", "-l" }, ct);
        return ParseDevices(r.StdOut);
    }

    public async Task<int> GetBatteryAsync(string serial, CancellationToken ct = default)
    {
        var r = await _runner.RunAsync(_adb,
            new[] { "-s", serial, "shell", "dumpsys", "battery" }, ct);
        var m = Regex.Match(r.StdOut, @"level:\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter AdbClientParseTests`
Expected: PASS(2 个)。

- [ ] **Step 5: 【真机验证】列出真实设备**

写一个临时控制台片段或在 App 早期接线后,确认 `ListDevicesAsync` 返回 6 台、`GetBatteryAsync` 返回合理电量。可在 App Task 中验证;此处仅记录预期:6 台全部 `Online`。

- [ ] **Step 6: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): AdbClient device listing + battery"
```

---

## Task 5: AdbCommands — 命令参数构造器

集中构造所有 adb 子命令的参数数组(可单测),避免散落字符串拼接(防注入、易测)。

**Files:**
- Create: `src/DroidBus.Core/Adb/AdbCommands.cs`
- Test: `tests/DroidBus.Core.Tests/AdbCommandsTests.cs`

- [ ] **Step 1: 写失败测试**

Create `tests/DroidBus.Core.Tests/AdbCommandsTests.cs`:
```csharp
using DroidBus.Core.Adb;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbCommandsTests
{
    [Fact]
    public void InstallApk_builds_args()
    {
        AdbCommands.InstallApk("S1", @"C:\a.apk")
            .Should().Equal("-s", "S1", "install", "-r", @"C:\a.apk");
    }

    [Fact]
    public void Uninstall_builds_args()
    {
        AdbCommands.Uninstall("S1", "com.x")
            .Should().Equal("-s", "S1", "uninstall", "com.x");
    }

    [Fact]
    public void Push_and_Pull_build_args()
    {
        AdbCommands.Push("S1", @"C:\f", "/sdcard/f").Should().Equal("-s", "S1", "push", @"C:\f", "/sdcard/f");
        AdbCommands.Pull("S1", "/sdcard/f", @"C:\f").Should().Equal("-s", "S1", "pull", "/sdcard/f", @"C:\f");
    }

    [Fact]
    public void StartApp_uses_monkey()
    {
        AdbCommands.StartApp("S1", "com.tencent.mobileqq")
            .Should().Equal("-s", "S1", "shell", "monkey", "-p", "com.tencent.mobileqq",
                            "-c", "android.intent.category.LAUNCHER", "1");
    }

    [Fact]
    public void Input_helpers_build_args()
    {
        AdbCommands.Tap("S1", 191, 832).Should().Equal("-s", "S1", "shell", "input", "tap", "191", "832");
        AdbCommands.Swipe("S1", 1, 2, 3, 4, 300).Should().Equal("-s", "S1", "shell", "input", "swipe", "1", "2", "3", "4", "300");
        AdbCommands.KeyEvent("S1", 3).Should().Equal("-s", "S1", "shell", "input", "keyevent", "3");
        AdbCommands.Text("S1", "abc").Should().Equal("-s", "S1", "shell", "input", "text", "abc");
        AdbCommands.SetShowTouches("S1", true).Should().Equal("-s", "S1", "shell", "settings", "put", "system", "show_touches", "1");
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter AdbCommandsTests`
Expected: FAIL,`AdbCommands` 不存在。

- [ ] **Step 3: 实现**

Create `src/DroidBus.Core/Adb/AdbCommands.cs`:
```csharp
using System.Globalization;

namespace DroidBus.Core.Adb;

/// 每个方法返回传给 adb.exe 的参数数组(不含 adb 本身)。
public static class AdbCommands
{
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

    public static string[] InstallApk(string s, string apkPath) => new[] { "-s", s, "install", "-r", apkPath };
    public static string[] Uninstall(string s, string pkg) => new[] { "-s", s, "uninstall", pkg };
    public static string[] Push(string s, string local, string remote) => new[] { "-s", s, "push", local, remote };
    public static string[] Pull(string s, string remote, string local) => new[] { "-s", s, "pull", remote, local };

    public static string[] StartApp(string s, string pkg) => new[]
    {
        "-s", s, "shell", "monkey", "-p", pkg, "-c", "android.intent.category.LAUNCHER", "1"
    };

    public static string[] Tap(string s, int x, int y) => new[] { "-s", s, "shell", "input", "tap", I(x), I(y) };
    public static string[] Swipe(string s, int x1, int y1, int x2, int y2, int ms) =>
        new[] { "-s", s, "shell", "input", "swipe", I(x1), I(y1), I(x2), I(y2), I(ms) };
    public static string[] KeyEvent(string s, int keycode) => new[] { "-s", s, "shell", "input", "keyevent", I(keycode) };
    public static string[] Text(string s, string text) => new[] { "-s", s, "shell", "input", "text", text };
    public static string[] SetShowTouches(string s, bool on) =>
        new[] { "-s", s, "shell", "settings", "put", "system", "show_touches", on ? "1" : "0" };
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter AdbCommandsTests`
Expected: PASS(5 个)。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add AdbCommands arg builders"
```

---

## Task 6: ScrcpyArgsBuilder — 投屏参数构造

把 `MirrorOptions` 转成 scrcpy 2.0 参数。**关键事实(已实测 scrcpy 2.0):** `--show-touches` 是原生 flag;音频需 Android 11+,故对 A10 板子默认 `--no-audio`;码率用 `--video-bit-rate`;无边框窗口用 `--window-borderless` 便于嵌入;用 `--window-title` 便于按标题/PID 找窗口。

**Files:**
- Create: `src/DroidBus.Core/Mirror/MirrorOptions.cs`, `src/DroidBus.Core/Mirror/ScrcpyArgsBuilder.cs`
- Test: `tests/DroidBus.Core.Tests/ScrcpyArgsBuilderTests.cs`

- [ ] **Step 1: 写失败测试**

Create `tests/DroidBus.Core.Tests/ScrcpyArgsBuilderTests.cs`:
```csharp
using DroidBus.Core.Mirror;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScrcpyArgsBuilderTests
{
    [Fact]
    public void Default_options_produce_base_args()
    {
        var args = ScrcpyArgsBuilder.Build("S1", new MirrorOptions());
        args.Should().Contain(new[] { "-s", "S1" });
        args.Should().Contain("--window-borderless");
        args.Should().Contain("--no-audio");                 // A10 默认关音频
        args.Should().Contain("--window-title");
        args.Should().Contain("DroidBus:S1");
        args.Should().Contain("--video-bit-rate");
        args.Should().Contain("4M");
        args.Should().Contain("--max-size");
        args.Should().Contain("1080");
    }

    [Fact]
    public void Toggles_add_flags()
    {
        var o = new MirrorOptions
        {
            TurnScreenOff = true, StayAwake = true, ShowTouches = true,
            LockOrientation = 0, BitRateMbps = 8, MaxSize = 1280,
            Record = true, RecordDir = @"C:\rec"
        };
        var args = ScrcpyArgsBuilder.Build("S1", o);
        args.Should().Contain("--turn-screen-off");
        args.Should().Contain("--stay-awake");
        args.Should().Contain("--show-touches");
        args.Should().Contain("--lock-video-orientation");
        args.Should().Contain("0");
        args.Should().Contain("8M");
        args.Should().Contain("1280");
        // record 路径形如 C:\rec\S1-<timestamp>.mp4
        args.Should().Contain(a => a.StartsWith(@"C:\rec\S1-") && a.EndsWith(".mp4"));
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter ScrcpyArgsBuilderTests`
Expected: FAIL,类型不存在。

- [ ] **Step 3: 实现 MirrorOptions + Builder**

Create `src/DroidBus.Core/Mirror/MirrorOptions.cs`:
```csharp
namespace DroidBus.Core.Mirror;

public sealed class MirrorOptions
{
    public int BitRateMbps { get; set; } = 4;
    public int MaxSize { get; set; } = 1080;
    public bool TurnScreenOff { get; set; }
    public bool StayAwake { get; set; }
    public bool ShowTouches { get; set; }
    public int? LockOrientation { get; set; }   // 0/1/2/3 = 旋转锁定;null=不锁
    public bool Record { get; set; }
    public string RecordDir { get; set; } = "";
    public bool NoAudio { get; set; } = true;    // A10 默认关音频(走 sndcpy)
}
```

Create `src/DroidBus.Core/Mirror/ScrcpyArgsBuilder.cs`:
```csharp
using System.Globalization;

namespace DroidBus.Core.Mirror;

public static class ScrcpyArgsBuilder
{
    public static IReadOnlyList<string> Build(string serial, MirrorOptions o, DateTime? now = null)
    {
        var a = new List<string>
        {
            "-s", serial,
            "--window-borderless",
            "--window-title", $"DroidBus:{serial}",
            "--video-bit-rate", $"{o.BitRateMbps}M",
            "--max-size", o.MaxSize.ToString(CultureInfo.InvariantCulture),
        };
        if (o.NoAudio) a.Add("--no-audio");
        if (o.TurnScreenOff) a.Add("--turn-screen-off");
        if (o.StayAwake) a.Add("--stay-awake");
        if (o.ShowTouches) a.Add("--show-touches");
        if (o.LockOrientation is int rot)
        {
            a.Add("--lock-video-orientation");
            a.Add(rot.ToString(CultureInfo.InvariantCulture));
        }
        if (o.Record)
        {
            var ts = (now ?? DateTime.Now).ToString("yyyyMMdd-HHmmss");
            a.Add("--record");
            a.Add(Path.Combine(o.RecordDir, $"{serial}-{ts}.mp4"));
        }
        return a;
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter ScrcpyArgsBuilderTests`
Expected: PASS(2 个)。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add MirrorOptions + ScrcpyArgsBuilder"
```

---

## Task 7: DeviceManager — 发现 + 轮询 + 变更事件

维护设备字典,周期性 `ListDevicesAsync` 刷新,并在某台状态/电量变化时触发事件。轮询循环用 `IProcessRunner` 间接可测;此处单测「合并逻辑」(把新一次列表并入旧状态、产出变更)。

**Files:**
- Create: `src/DroidBus.Core/Devices/DeviceManager.cs`
- Test: `tests/DroidBus.Core.Tests/DeviceManagerTests.cs`

- [ ] **Step 1: 写失败测试(只测纯合并 Merge)**

Create `tests/DroidBus.Core.Tests/DeviceManagerTests.cs`:
```csharp
using DroidBus.Core.Devices;
using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class DeviceManagerTests
{
    [Fact]
    public void Merge_adds_new_marks_missing_offline_and_reports_changes()
    {
        var current = new List<Device>
        {
            new("A", DeviceState.Online),
            new("B", DeviceState.Online),
        };
        var scan = new List<Device>
        {
            new("A", DeviceState.Online),     // 不变
            new("C", DeviceState.Online),     // 新增
            // B 消失 -> 应标记为 Offline
        };

        var changes = DeviceManager.Merge(current, scan);

        current.Select(d => d.Serial).Should().BeEquivalentTo(new[] { "A", "B", "C" });
        current.Single(d => d.Serial == "B").State.Should().Be(DeviceState.Offline);
        changes.Select(c => c.Serial).Should().BeEquivalentTo(new[] { "B", "C" });
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter DeviceManagerTests`
Expected: FAIL,`DeviceManager` 不存在。

- [ ] **Step 3: 实现 DeviceManager**

Create `src/DroidBus.Core/Devices/DeviceManager.cs`:
```csharp
using DroidBus.Core.Adb;
using DroidBus.Core.Models;

namespace DroidBus.Core.Devices;

public sealed class DeviceManager
{
    private readonly AdbClient _adb;
    private readonly List<Device> _devices = new();

    public DeviceManager(AdbClient adb) => _adb = adb;

    public IReadOnlyList<Device> Devices => _devices;

    /// 某台设备状态发生变化时触发(新增 / 上线 / 掉线 / 掉授权)。
    public event Action<Device>? DeviceChanged;

    /// 把一次扫描结果并入 current,返回发生变化的设备。静态以便纯单测。
    public static IReadOnlyList<Device> Merge(List<Device> current, IReadOnlyList<Device> scan)
    {
        var changed = new List<Device>();
        var scanSerials = scan.Select(s => s.Serial).ToHashSet();

        foreach (var s in scan)
        {
            var existing = current.FirstOrDefault(d => d.Serial == s.Serial);
            if (existing is null)
            {
                current.Add(s);
                changed.Add(s);
            }
            else if (existing.State != s.State)
            {
                existing.State = s.State;
                if (s.Model != null) existing.Model = s.Model;
                changed.Add(existing);
            }
        }
        foreach (var d in current.Where(d => !scanSerials.Contains(d.Serial) && d.State != DeviceState.Offline))
        {
            d.State = DeviceState.Offline;
            changed.Add(d);
        }
        return changed;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var scan = await _adb.ListDevicesAsync(ct);
        foreach (var c in Merge(_devices, scan))
            DeviceChanged?.Invoke(c);
    }

    /// 后台轮询;由 App 在 UI 线程外调用,事件回调需 marshal 回 UI。
    public async Task PollLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RefreshAsync(ct); } catch { /* 单次失败忽略,下轮重试 */ }
            try { await Task.Delay(interval, ct); } catch (TaskCanceledException) { break; }
        }
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter DeviceManagerTests`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add DeviceManager with merge + poll loop"
```

---

## Task 8: BatchExecutor — 并行批量执行 + 失败汇总

对一组设备并行执行同一个异步动作,**单台失败不影响其余**,汇总成功/失败,回报进度。

**Files:**
- Create: `src/DroidBus.Core/Batch/BatchExecutor.cs`
- Test: `tests/DroidBus.Core.Tests/BatchExecutorTests.cs`

- [ ] **Step 1: 写失败测试(用 fake 动作,故意让一台抛异常)**

Create `tests/DroidBus.Core.Tests/BatchExecutorTests.cs`:
```csharp
using DroidBus.Core.Batch;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BatchExecutorTests
{
    [Fact]
    public async Task Runs_all_and_aggregates_failures()
    {
        var serials = new[] { "A", "B", "C" };
        var progress = new List<string>();

        var result = await BatchExecutor.RunAsync(
            serials,
            action: async (serial, ct) =>
            {
                await Task.Yield();
                if (serial == "B") throw new InvalidOperationException("boom");
            },
            onProgress: s => { lock (progress) progress.Add(s); });

        result.Succeeded.Should().BeEquivalentTo(new[] { "A", "C" });
        result.Failed.Keys.Should().BeEquivalentTo(new[] { "B" });
        result.Failed["B"].Should().Contain("boom");
        progress.Should().BeEquivalentTo(serials);   // 每台都回报进度一次
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter BatchExecutorTests`
Expected: FAIL,`BatchExecutor` 不存在。

- [ ] **Step 3: 实现**

Create `src/DroidBus.Core/Batch/BatchExecutor.cs`:
```csharp
using System.Collections.Concurrent;

namespace DroidBus.Core.Batch;

public sealed record BatchResult(
    IReadOnlyList<string> Succeeded,
    IReadOnlyDictionary<string, string> Failed);

public static class BatchExecutor
{
    public static async Task<BatchResult> RunAsync(
        IEnumerable<string> serials,
        Func<string, CancellationToken, Task> action,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var ok = new ConcurrentBag<string>();
        var fail = new ConcurrentDictionary<string, string>();

        var tasks = serials.Select(async serial =>
        {
            try
            {
                await action(serial, ct);
                ok.Add(serial);
            }
            catch (Exception ex)
            {
                fail[serial] = ex.Message;
            }
            finally
            {
                onProgress?.Invoke(serial);
            }
        });

        await Task.WhenAll(tasks);
        return new BatchResult(ok.ToArray(), fail);
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter BatchExecutorTests`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add BatchExecutor with failure aggregation"
```

---

## Task 9: IDeviceController + AdbDeviceController

设备控制原语(tap/swipe/key/text/launch/exec)。脚本引擎与同步广播都依赖这层;用接口让脚本逻辑可单测。

**Files:**
- Create: `src/DroidBus.Core/Control/IDeviceController.cs`, `src/DroidBus.Core/Control/AdbDeviceController.cs`
- Test: `tests/DroidBus.Core.Tests/AdbDeviceControllerTests.cs`

- [ ] **Step 1: 写失败测试(用 fake IProcessRunner 断言 adb 调用)**

Create `tests/DroidBus.Core.Tests/AdbDeviceControllerTests.cs`:
```csharp
using DroidBus.Core.Control;
using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbDeviceControllerTests
{
    private sealed class RecordingRunner : IProcessRunner
    {
        public List<string[]> Calls { get; } = new();
        public Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> args, CancellationToken ct = default)
        {
            Calls.Add(args.ToArray());
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    [Fact]
    public async Task Tap_invokes_adb_input_tap()
    {
        var runner = new RecordingRunner();
        var c = new AdbDeviceController(runner, "adb.exe");
        await c.TapAsync("S1", 10, 20);
        runner.Calls.Single().Should().Equal("-s", "S1", "shell", "input", "tap", "10", "20");
    }

    [Fact]
    public async Task Exec_runs_raw_shell()
    {
        var runner = new RecordingRunner();
        var c = new AdbDeviceController(runner, "adb.exe");
        await c.ExecAsync("S1", "input keyevent 24");
        runner.Calls.Single().Should().Equal("-s", "S1", "shell", "input", "keyevent", "24");
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter AdbDeviceControllerTests`
Expected: FAIL,类型不存在。

- [ ] **Step 3: 实现接口 + adb 实现**

Create `src/DroidBus.Core/Control/IDeviceController.cs`:
```csharp
namespace DroidBus.Core.Control;

public interface IDeviceController
{
    Task TapAsync(string serial, int x, int y, CancellationToken ct = default);
    Task SwipeAsync(string serial, int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct = default);
    Task KeyEventAsync(string serial, int keycode, CancellationToken ct = default);
    Task TextAsync(string serial, string text, CancellationToken ct = default);
    Task LaunchAppAsync(string serial, string pkg, CancellationToken ct = default);
    /// 执行原始 shell 命令(去掉前缀 "adb shell"/"adb" 后的部分)。
    Task ExecAsync(string serial, string shellCommand, CancellationToken ct = default);
}
```

Create `src/DroidBus.Core/Control/AdbDeviceController.cs`:
```csharp
using DroidBus.Core.Adb;
using DroidBus.Core.Process;

namespace DroidBus.Core.Control;

public sealed class AdbDeviceController : IDeviceController
{
    private readonly IProcessRunner _runner;
    private readonly string _adb;

    public AdbDeviceController(IProcessRunner runner, string adbPath)
    {
        _runner = runner;
        _adb = adbPath;
    }

    public Task TapAsync(string s, int x, int y, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Tap(s, x, y), ct);

    public Task SwipeAsync(string s, int x1, int y1, int x2, int y2, int ms, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Swipe(s, x1, y1, x2, y2, ms), ct);

    public Task KeyEventAsync(string s, int keycode, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.KeyEvent(s, keycode), ct);

    public Task TextAsync(string s, string text, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Text(s, text), ct);

    public Task LaunchAppAsync(string s, string pkg, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.StartApp(s, pkg), ct);

    public Task ExecAsync(string s, string shellCommand, CancellationToken ct = default)
    {
        // 把 "input keyevent 24" 拆成 args 跟在 "-s S shell" 后面;
        // 同时容忍用户写了 "adb shell xxx" / "shell xxx" 前缀。
        var cmd = shellCommand.Trim();
        if (cmd.StartsWith("adb ", StringComparison.OrdinalIgnoreCase)) cmd = cmd[4..].Trim();
        if (cmd.StartsWith("shell ", StringComparison.OrdinalIgnoreCase)) cmd = cmd[6..].Trim();
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var args = new List<string> { "-s", s, "shell" };
        args.AddRange(parts);
        return _runner.RunAsync(_adb, args, ct);
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter AdbDeviceControllerTests`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add IDeviceController + AdbDeviceController"
```

---

## Task 10: IClock(可注入时钟)

脚本里的「延时/随机延时」需要可控时钟,否则单测会真的 sleep。

**Files:**
- Create: `src/DroidBus.Core/Time/IClock.cs`
- Test: 由 Task 12(ScriptRunner)覆盖。

- [ ] **Step 1: 实现接口 + 真实实现 + 测试用 fake**

Create `src/DroidBus.Core/Time/IClock.cs`:
```csharp
namespace DroidBus.Core.Time;

public interface IClock
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
    int RandomMilliseconds(int minMs, int maxMs);
}

public sealed class SystemClock : IClock
{
    private readonly Random _rng = new();
    public Task DelayAsync(TimeSpan d, CancellationToken ct = default) => Task.Delay(d, ct);
    public int RandomMilliseconds(int minMs, int maxMs) => _rng.Next(minMs, maxMs);
}
```

- [ ] **Step 2: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 3: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add IClock abstraction"
```

---

## Task 11: ScriptCommand + ScriptParser(`.adb` 中文 DSL)

兼容原 app 的 GBK 脚本。命令以 `;` 或换行分隔;关键字与参数之间**无空格**(如 `点击191 832`)。需先匹配更长的关键字(`快速双击` 先于 `快速点击` 先于 `点击`;`ADB文本` 单独)。

**Files:**
- Create: `src/DroidBus.Core/Script/ScriptCommand.cs`, `src/DroidBus.Core/Script/ScriptParser.cs`
- Modify: `src/DroidBus.Core/DroidBus.Core.csproj`(加 `System.Text.Encoding.CodePages` 包)
- Test: `tests/DroidBus.Core.Tests/ScriptParserTests.cs`

- [ ] **Step 1: 加 GBK 编码包**

Run:
```bash
dotnet add src/DroidBus.Core package System.Text.Encoding.CodePages
```

- [ ] **Step 2: 写失败测试(覆盖每种命令 + GBK 解码)**

Create `tests/DroidBus.Core.Tests/ScriptParserTests.cs`:
```csharp
using System.Text;
using DroidBus.Core.Script;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScriptParserTests
{
    [Fact]
    public void Parses_all_command_types()
    {
        const string script =
            "点击191 832;延时2S;长按163 1416;滑动852 1292 45 1008;" +
            "返回桌面;返回上层;快速点击657 1175;快速双击657 1175;随机延时;" +
            "执行命令input keyevent 24;输入文本hello;ADB文本world;启动应用com.tencent.mobileqq;";

        var cmds = ScriptParser.Parse(script);

        cmds.Should().HaveCount(13);
        cmds[0].Should().BeEquivalentTo(new TapCommand(191, 832));
        cmds[1].Should().BeEquivalentTo(new DelayCommand(TimeSpan.FromSeconds(2)));
        cmds[2].Should().BeEquivalentTo(new LongPressCommand(163, 1416));
        cmds[3].Should().BeEquivalentTo(new SwipeCommand(852, 1292, 45, 1008));
        cmds[4].Should().BeOfType<HomeCommand>();
        cmds[5].Should().BeOfType<BackCommand>();
        cmds[6].Should().BeEquivalentTo(new FastTapCommand(657, 1175));
        cmds[7].Should().BeEquivalentTo(new FastDoubleTapCommand(657, 1175));
        cmds[8].Should().BeOfType<RandomDelayCommand>();
        cmds[9].Should().BeEquivalentTo(new ExecCommand("input keyevent 24"));
        cmds[10].Should().BeEquivalentTo(new InputTextCommand("hello"));
        cmds[11].Should().BeEquivalentTo(new AdbTextCommand("world"));
        cmds[12].Should().BeEquivalentTo(new LaunchAppCommand("com.tencent.mobileqq"));
    }

    [Fact]
    public void Skips_blank_segments_and_supports_newlines()
    {
        ScriptParser.Parse("点击1 2;\n\n返回桌面\n").Should().HaveCount(2);
    }

    [Fact]
    public void Reads_gbk_encoded_file()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GB2312");
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, gbk.GetBytes("启动应用com.x;返回桌面;"));

        var cmds = ScriptParser.ParseGbkFile(path);

        cmds.Should().HaveCount(2);
        cmds[0].Should().BeEquivalentTo(new LaunchAppCommand("com.x"));
        File.Delete(path);
    }
}
```

- [ ] **Step 3: 运行确认失败**

Run: `dotnet test --filter ScriptParserTests`
Expected: FAIL,类型不存在。

- [ ] **Step 4: 实现命令模型**

Create `src/DroidBus.Core/Script/ScriptCommand.cs`:
```csharp
namespace DroidBus.Core.Script;

public abstract record ScriptCommand;

public sealed record TapCommand(int X, int Y) : ScriptCommand;
public sealed record LongPressCommand(int X, int Y) : ScriptCommand;
public sealed record SwipeCommand(int X1, int Y1, int X2, int Y2) : ScriptCommand;
public sealed record FastTapCommand(int X, int Y) : ScriptCommand;
public sealed record FastDoubleTapCommand(int X, int Y) : ScriptCommand;
public sealed record DelayCommand(TimeSpan Duration) : ScriptCommand;
public sealed record RandomDelayCommand : ScriptCommand;
public sealed record HomeCommand : ScriptCommand;
public sealed record BackCommand : ScriptCommand;
public sealed record ExecCommand(string ShellCommand) : ScriptCommand;
public sealed record InputTextCommand(string Text) : ScriptCommand;   // 走 IME(ADBKeyBoard)
public sealed record AdbTextCommand(string Text) : ScriptCommand;     // 走 input text
public sealed record LaunchAppCommand(string Package) : ScriptCommand;
```

- [ ] **Step 5: 实现解析器**

Create `src/DroidBus.Core/Script/ScriptParser.cs`:
```csharp
using System.Text;

namespace DroidBus.Core.Script;

public static class ScriptParser
{
    public static IReadOnlyList<ScriptCommand> ParseGbkFile(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding("GB2312").GetString(File.ReadAllBytes(path));
        return Parse(text);
    }

    public static IReadOnlyList<ScriptCommand> Parse(string script)
    {
        var result = new List<ScriptCommand>();
        var segments = script.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var line = seg.Trim();
            if (line.Length == 0) continue;
            var cmd = ParseLine(line);
            if (cmd != null) result.Add(cmd);
        }
        return result;
    }

    private static ScriptCommand? ParseLine(string line)
    {
        // 无参命令(整行匹配)
        if (line == "返回桌面") return new HomeCommand();
        if (line == "返回上层") return new BackCommand();
        if (line == "随机延时") return new RandomDelayCommand();

        // 带参命令:按"更长关键字优先"顺序匹配前缀
        if (TryArg(line, "快速双击", out var a) && Ints(a, 2, out var d)) return new FastDoubleTapCommand(d[0], d[1]);
        if (TryArg(line, "快速点击", out a) && Ints(a, 2, out d)) return new FastTapCommand(d[0], d[1]);
        if (TryArg(line, "长按", out a) && Ints(a, 2, out d)) return new LongPressCommand(d[0], d[1]);
        if (TryArg(line, "点击", out a) && Ints(a, 2, out d)) return new TapCommand(d[0], d[1]);
        if (TryArg(line, "滑动", out a) && Ints(a, 4, out d)) return new SwipeCommand(d[0], d[1], d[2], d[3]);
        if (TryArg(line, "延时", out a)) return new DelayCommand(ParseDuration(a));
        if (TryArg(line, "执行命令", out a)) return new ExecCommand(a.Trim());
        if (TryArg(line, "输入文本", out a)) return new InputTextCommand(a);
        if (TryArg(line, "ADB文本", out a)) return new AdbTextCommand(a);
        if (TryArg(line, "启动应用", out a)) return new LaunchAppCommand(a.Trim());

        return null; // 未知命令忽略(向后兼容)
    }

    private static bool TryArg(string line, string keyword, out string arg)
    {
        if (line.StartsWith(keyword, StringComparison.Ordinal))
        {
            arg = line[keyword.Length..];
            return true;
        }
        arg = "";
        return false;
    }

    private static bool Ints(string s, int count, out int[] values)
    {
        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        values = new int[count];
        if (parts.Length < count) return false;
        for (var i = 0; i < count; i++)
            if (!int.TryParse(parts[i], out values[i])) return false;
        return true;
    }

    private static TimeSpan ParseDuration(string s)
    {
        s = s.Trim().ToUpperInvariant();
        if (s.EndsWith("MS") && int.TryParse(s[..^2], out var ms)) return TimeSpan.FromMilliseconds(ms);
        if (s.EndsWith("S") && int.TryParse(s[..^1], out var sec)) return TimeSpan.FromSeconds(sec);
        return int.TryParse(s, out var n) ? TimeSpan.FromSeconds(n) : TimeSpan.Zero;
    }
}
```

- [ ] **Step 6: 运行确认通过**

Run: `dotnet test --filter ScriptParserTests`
Expected: PASS(3 个)。

- [ ] **Step 7: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add ScriptCommand model + GBK .adb DSL parser"
```

---

## Task 12: ScriptRunner — 在一台设备上执行命令序列

把命令翻成 `IDeviceController` 调用;`延时`/`随机延时` 走 `IClock`。`输入文本`(IME)在本任务先按 `TextAsync` 处理,待 Task 19 接入 ADBKeyBoard 后改为 IME 广播。

**Files:**
- Create: `src/DroidBus.Core/Script/ScriptRunner.cs`
- Test: `tests/DroidBus.Core.Tests/ScriptRunnerTests.cs`

- [ ] **Step 1: 写失败测试(fake controller + fake clock,断言调用序列)**

Create `tests/DroidBus.Core.Tests/ScriptRunnerTests.cs`:
```csharp
using DroidBus.Core.Control;
using DroidBus.Core.Script;
using DroidBus.Core.Time;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScriptRunnerTests
{
    private sealed class FakeController : IDeviceController
    {
        public List<string> Log { get; } = new();
        public Task TapAsync(string s, int x, int y, CancellationToken ct = default) { Log.Add($"tap {x} {y}"); return Task.CompletedTask; }
        public Task SwipeAsync(string s, int x1, int y1, int x2, int y2, int ms, CancellationToken ct = default) { Log.Add($"swipe {x1} {y1} {x2} {y2} {ms}"); return Task.CompletedTask; }
        public Task KeyEventAsync(string s, int k, CancellationToken ct = default) { Log.Add($"key {k}"); return Task.CompletedTask; }
        public Task TextAsync(string s, string t, CancellationToken ct = default) { Log.Add($"text {t}"); return Task.CompletedTask; }
        public Task LaunchAppAsync(string s, string p, CancellationToken ct = default) { Log.Add($"launch {p}"); return Task.CompletedTask; }
        public Task ExecAsync(string s, string c, CancellationToken ct = default) { Log.Add($"exec {c}"); return Task.CompletedTask; }
    }

    private sealed class FakeClock : IClock
    {
        public List<TimeSpan> Delays { get; } = new();
        public Task DelayAsync(TimeSpan d, CancellationToken ct = default) { Delays.Add(d); return Task.CompletedTask; }
        public int RandomMilliseconds(int min, int max) => min;
    }

    [Fact]
    public async Task Translates_commands_to_controller_calls()
    {
        var ctrl = new FakeController();
        var clock = new FakeClock();
        var runner = new ScriptRunner(ctrl, clock);

        var cmds = new ScriptCommand[]
        {
            new TapCommand(10, 20),
            new SwipeCommand(1, 2, 3, 4),
            new HomeCommand(),
            new BackCommand(),
            new DelayCommand(TimeSpan.FromSeconds(2)),
            new RandomDelayCommand(),
            new LaunchAppCommand("com.x"),
            new ExecCommand("input keyevent 24"),
            new AdbTextCommand("hi"),
        };

        await runner.RunAsync("S1", cmds);

        ctrl.Log.Should().Equal(
            "tap 10 20",
            "swipe 1 2 3 4 200",   // 普通滑动固定时长 200ms(见实现 SwipeMs)
            "key 3",
            "key 4",
            "launch com.x",
            "exec input keyevent 24",
            "text hi");
        clock.Delays[0].Should().Be(TimeSpan.FromSeconds(2));
        clock.Delays.Should().HaveCount(2); // 显式 2s + 一次随机延时
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter ScriptRunnerTests`
Expected: FAIL,`ScriptRunner` 不存在。

- [ ] **Step 3: 实现**

Create `src/DroidBus.Core/Script/ScriptRunner.cs`:
```csharp
using DroidBus.Core.Control;
using DroidBus.Core.Time;

namespace DroidBus.Core.Script;

public sealed class ScriptRunner
{
    private const int Home = 3, Back = 4;
    private const int SwipeMs = 200, LongPressMs = 800;
    private const int RandomMin = 300, RandomMax = 1200;

    private readonly IDeviceController _ctrl;
    private readonly IClock _clock;

    public ScriptRunner(IDeviceController ctrl, IClock clock)
    {
        _ctrl = ctrl;
        _clock = clock;
    }

    public async Task RunAsync(string serial, IReadOnlyList<ScriptCommand> commands, CancellationToken ct = default)
    {
        foreach (var cmd in commands)
        {
            ct.ThrowIfCancellationRequested();
            switch (cmd)
            {
                case TapCommand t: await _ctrl.TapAsync(serial, t.X, t.Y, ct); break;
                case FastTapCommand t: await _ctrl.TapAsync(serial, t.X, t.Y, ct); break;
                case FastDoubleTapCommand t:
                    await _ctrl.TapAsync(serial, t.X, t.Y, ct);
                    await _ctrl.TapAsync(serial, t.X, t.Y, ct);
                    break;
                case LongPressCommand l: await _ctrl.SwipeAsync(serial, l.X, l.Y, l.X, l.Y, LongPressMs, ct); break;
                case SwipeCommand s: await _ctrl.SwipeAsync(serial, s.X1, s.Y1, s.X2, s.Y2, SwipeMs, ct); break;
                case HomeCommand: await _ctrl.KeyEventAsync(serial, Home, ct); break;
                case BackCommand: await _ctrl.KeyEventAsync(serial, Back, ct); break;
                case DelayCommand d: await _clock.DelayAsync(d.Duration, ct); break;
                case RandomDelayCommand:
                    await _clock.DelayAsync(TimeSpan.FromMilliseconds(_clock.RandomMilliseconds(RandomMin, RandomMax)), ct);
                    break;
                case LaunchAppCommand a: await _ctrl.LaunchAppAsync(serial, a.Package, ct); break;
                case ExecCommand e: await _ctrl.ExecAsync(serial, e.ShellCommand, ct); break;
                case AdbTextCommand x: await _ctrl.TextAsync(serial, x.Text, ct); break;
                case InputTextCommand x: await _ctrl.TextAsync(serial, x.Text, ct); break; // Task 19 改为 IME
            }
        }
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter ScriptRunnerTests`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add ScriptRunner"
```

---

## Task 13: SyncInputTranslator — 手势→设备坐标命令(同步广播核心)

主控台格子里的一次手势(按下点、抬起点,均为格子像素坐标)按比例映射到设备分辨率,产出 `TapCommand` 或 `SwipeCommand`。广播时对每台选中设备用同一组设备坐标(各台分辨率一致,Note9 均 1440×2960)。纯函数,易测。

**Files:**
- Create: `src/DroidBus.Core/Control/SyncInputTranslator.cs`
- Test: `tests/DroidBus.Core.Tests/SyncInputTranslatorTests.cs`

- [ ] **Step 1: 写失败测试**

Create `tests/DroidBus.Core.Tests/SyncInputTranslatorTests.cs`:
```csharp
using DroidBus.Core.Control;
using DroidBus.Core.Script;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class SyncInputTranslatorTests
{
    // 格子 360×740,设备 1440×2960 -> 比例 ×4
    [Fact]
    public void Click_at_tile_center_maps_to_device_tap()
    {
        var cmd = SyncInputTranslator.Translate(
            downX: 180, downY: 370, upX: 180, upY: 370,
            tileW: 360, tileH: 740, devW: 1440, devH: 2960);

        cmd.Should().BeEquivalentTo(new TapCommand(720, 1480));
    }

    [Fact]
    public void Drag_maps_to_device_swipe()
    {
        var cmd = SyncInputTranslator.Translate(
            downX: 10, downY: 10, upX: 100, upY: 200,
            tileW: 360, tileH: 740, devW: 1440, devH: 2960);

        cmd.Should().BeEquivalentTo(new SwipeCommand(40, 40, 400, 800));
    }

    [Fact]
    public void Tiny_movement_below_threshold_is_a_tap()
    {
        var cmd = SyncInputTranslator.Translate(50, 50, 53, 52, 360, 740, 1440, 2960);
        cmd.Should().BeOfType<TapCommand>();
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter SyncInputTranslatorTests`
Expected: FAIL,类型不存在。

- [ ] **Step 3: 实现**

Create `src/DroidBus.Core/Control/SyncInputTranslator.cs`:
```csharp
using DroidBus.Core.Script;

namespace DroidBus.Core.Control;

public static class SyncInputTranslator
{
    private const int TapThresholdPx = 5; // 移动小于该值视为点击

    public static ScriptCommand Translate(
        int downX, int downY, int upX, int upY,
        int tileW, int tileH, int devW, int devH)
    {
        int MapX(int v) => (int)Math.Round((double)v / tileW * devW);
        int MapY(int v) => (int)Math.Round((double)v / tileH * devH);

        var dx = Math.Abs(upX - downX);
        var dy = Math.Abs(upY - downY);
        if (dx <= TapThresholdPx && dy <= TapThresholdPx)
            return new TapCommand(MapX(downX), MapY(downY));

        return new SwipeCommand(MapX(downX), MapY(downY), MapX(upX), MapY(upY));
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter SyncInputTranslatorTests`
Expected: PASS(3 个)。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(core): add SyncInputTranslator"
```

---

# 第二部分:App / WinForms(原生互操作 + UI,真机验证)

> 以下任务大量涉及 Win32 窗口嵌入与真实设备,**无法有意义地单元测试**。每个任务给出完整可编译代码 + 明确的 **【真机验证】** 步骤。先决条件:6 块 Note9 已授权并在线。

---

## Task 14: NativeMethods + ScrcpyHost(拉起 scrcpy 并 SetParent 嵌入)

为单台设备:拉起 `scrcpy.exe`(无边框、窗口标题 `DroidBus:<serial>`),轮询找到该进程的顶层窗口,`SetParent` 到一个 `Panel`,改成子窗口样式并填满,进程退出时触发 `Crashed`。

**Files:**
- Create: `src/DroidBus.App/Interop/NativeMethods.cs`, `src/DroidBus.App/Mirror/ScrcpyHost.cs`

- [ ] **Step 1: P/Invoke 声明**

Create `src/DroidBus.App/Interop/NativeMethods.cs`:
```csharp
using System.Runtime.InteropServices;

namespace DroidBus.App.Interop;

internal static class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const long WS_CHILD = 0x40000000L;
    public const long WS_POPUP = 0x80000000L;
    public const long WS_CAPTION = 0x00C00000L;
    public const long WS_THICKFRAME = 0x00040000L;
    public const long WS_VISIBLE = 0x10000000L;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll", SetLastError = true)] public static extern long GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll", SetLastError = true)] public static extern long SetWindowLongPtr(IntPtr h, int idx, long val);

    /// 找到属于指定 PID 的第一个可见顶层窗口。
    public static IntPtr FindMainWindowForPid(uint pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var wpid);
            if (wpid == pid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
```

- [ ] **Step 2: ScrcpyHost 实现**

Create `src/DroidBus.App/Mirror/ScrcpyHost.cs`:
```csharp
using System.Diagnostics;
using DroidBus.App.Interop;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App.Mirror;

public sealed class ScrcpyHost : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly Control _parent;     // 承载的 Panel
    private Process? _proc;
    private IntPtr _child = IntPtr.Zero;

    public ScrcpyHost(BinaryLocator bin, Control parent)
    {
        _bin = bin;
        _parent = parent;
    }

    public string? Serial { get; private set; }
    /// scrcpy 进程意外退出(崩溃/设备掉线)时触发,参数为退出码。
    public event Action<int>? Crashed;

    public async Task StartAsync(Device device, MirrorOptions options)
    {
        Serial = device.Serial;
        var args = ScrcpyArgsBuilder.Build(device.Serial, options);

        var psi = new ProcessStartInfo(_bin.Scrcpy)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = _bin.Dir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _proc.Exited += (_, _) =>
        {
            var code = _proc?.ExitCode ?? -1;
            if (code != 0) Crashed?.Invoke(code);
        };
        _proc.Start();

        // 轮询等待 scrcpy 的窗口出现(最多 ~8 秒)
        for (var i = 0; i < 80 && _child == IntPtr.Zero; i++)
        {
            await Task.Delay(100);
            _proc.Refresh();
            if (_proc.HasExited) { Crashed?.Invoke(_proc.ExitCode); return; }
            _child = NativeMethods.FindMainWindowForPid((uint)_proc.Id);
        }
        if (_child == IntPtr.Zero)
            throw new InvalidOperationException($"未能定位 {device.Serial} 的 scrcpy 窗口");

        Embed();
    }

    private void Embed()
    {
        if (_child == IntPtr.Zero) return;
        // 改成无边框子窗口
        var style = NativeMethods.GetWindowLongPtr(_child, NativeMethods.GWL_STYLE);
        style &= ~NativeMethods.WS_POPUP;
        style &= ~NativeMethods.WS_CAPTION;
        style &= ~NativeMethods.WS_THICKFRAME;
        style |= NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE;
        NativeMethods.SetWindowLongPtr(_child, NativeMethods.GWL_STYLE, style);

        NativeMethods.SetParent(_child, _parent.Handle);
        Resize();
    }

    public void Resize()
    {
        if (_child == IntPtr.Zero) return;
        NativeMethods.MoveWindow(_child, 0, 0, _parent.ClientSize.Width, _parent.ClientSize.Height, true);
    }

    public void Stop()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        _proc?.Dispose();
        _proc = null;
        _child = IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 3: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 4: 【真机验证】单台嵌入冒烟**

临时在 `Program.cs` 里(或一个临时按钮)对一台真机调用:`new ScrcpyHost(BinaryLocator.Discover(), aPanel).StartAsync(device, new MirrorOptions())`。
预期:scrcpy 画面无边框地嵌入到窗体的 Panel 中、随窗口缩放;拔掉该设备(或 `adb disconnect`)时触发 `Crashed`。验证后移除临时代码。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): add ScrcpyHost window embedding via SetParent"
```

---

## Task 15: DeviceTile + DeviceGridControl(3×2 画面墙)

`DeviceTile`:顶部信息条(序列号/型号/电量/状态)+ 投屏内容 Panel(scrcpy 嵌入此处)+ 选中高亮边框;单击选中、双击放大事件。`DeviceGridControl`:3 列 × 2 行 `TableLayoutPanel`,容纳最多 6 个 tile。

**Files:**
- Create: `src/DroidBus.App/Grid/DeviceTile.cs`, `src/DroidBus.App/Grid/DeviceGridControl.cs`

- [ ] **Step 1: DeviceTile**

Create `src/DroidBus.App/Grid/DeviceTile.cs`:
```csharp
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
```

- [ ] **Step 2: DeviceGridControl**

Create `src/DroidBus.App/Grid/DeviceGridControl.cs`:
```csharp
namespace DroidBus.App.Grid;

public sealed class DeviceGridControl : TableLayoutPanel
{
    public IReadOnlyList<DeviceTile> Tiles { get; }

    public DeviceGridControl(int columns = 3, int rows = 2)
    {
        Dock = DockStyle.Fill;
        ColumnCount = columns;
        RowCount = rows;
        BackColor = Color.FromArgb(18, 20, 25);
        for (var c = 0; c < columns; c++)
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (var r = 0; r < rows; r++)
            RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

        var tiles = new List<DeviceTile>();
        for (var i = 0; i < columns * rows; i++)
        {
            var tile = new DeviceTile { Dock = DockStyle.Fill, Margin = new Padding(3) };
            tiles.Add(tile);
            Controls.Add(tile, i % columns, i / columns);
        }
        Tiles = tiles;
    }
}
```

- [ ] **Step 3: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 4: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): add DeviceTile + DeviceGridControl"
```

---

## Task 16: MainForm 外壳 + 设备轮询接线

布局 A 外壳:顶部工具条(全部投屏/刷新/连接无线/全局码率·分辨率)+ 中间网格 + 右侧控制栏占位。启动后跑 `DeviceManager.PollLoopAsync`,把状态变化 marshal 回 UI 更新 tile。

**Files:**
- Modify: `src/DroidBus.App/Program.cs`
- Create: `src/DroidBus.App/MainForm.cs`

- [ ] **Step 1: MainForm**

Create `src/DroidBus.App/MainForm.cs`:
```csharp
using DroidBus.App.Grid;
using DroidBus.Core;
using DroidBus.Core.Adb;
using DroidBus.Core.Devices;
using DroidBus.Core.Models;
using DroidBus.Core.Process;

namespace DroidBus.App;

public sealed class MainForm : Form
{
    private readonly BinaryLocator _bin;
    private readonly DeviceManager _devices;
    private readonly DeviceGridControl _grid = new();
    private readonly FlowLayoutPanel _toolbar = new();
    private readonly Panel _controlPanel = new();
    private CancellationTokenSource? _pollCts;

    public DeviceGridControl Grid => _grid;
    public BinaryLocator Bin => _bin;

    public MainForm()
    {
        _bin = BinaryLocator.Discover();
        var adb = new AdbClient(new ProcessRunner(), _bin.Adb);
        _devices = new DeviceManager(adb);

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

        _controlPanel.Dock = DockStyle.Right;
        _controlPanel.Width = 240;
        _controlPanel.BackColor = Color.FromArgb(38, 41, 50);

        Controls.Add(_grid);          // Fill
        Controls.Add(_controlPanel);  // Right
        Controls.Add(_toolbar);       // Top

        _devices.DeviceChanged += OnDeviceChanged;
        Load += async (_, _) => await StartPollingAsync();
        FormClosing += (_, _) => _pollCts?.Cancel();
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

    private void RebindTiles()
    {
        var online = _devices.Devices.OrderBy(d => d.Serial).ToList();
        for (var i = 0; i < _grid.Tiles.Count; i++)
            _grid.Tiles[i].Bind(i < online.Count ? online[i] : null);
    }

    private void OnDeviceChanged(Device d)
    {
        if (IsHandleCreated)
            BeginInvoke(() =>
            {
                RebindTiles();
                foreach (var t in _grid.Tiles) t.UpdateHeader();
            });
    }

    // 选中模型与投屏由 Task 17 接线
    internal DeviceTile? SelectedTile { get; set; }
    private async void OnMirrorAll(object? sender, EventArgs e) { await Task.CompletedTask; }
}
```

- [ ] **Step 2: Program.cs 启动 MainForm**

Replace `src/DroidBus.App/Program.cs` 内容为:
```csharp
namespace DroidBus.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
```
删除模板生成的 `Form1.cs` / `Form1.Designer.cs`(若存在)。

- [ ] **Step 3: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 4: 【真机验证】设备出现在网格**

Run: `dotnet run --project src/DroidBus.App`
预期:窗口打开,3×2 网格中 6 个 tile 显示 6 台真机的型号/在线/电量;拔插某台时该 tile 在 3 秒内变「离线/在线」。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): MainForm shell + device polling"
```

---

## Task 17: 全部投屏 + 选中模型 + 双击放大

为每个有设备的 tile 拉起一个 `ScrcpyHost` 并嵌入其 `Surface`。实现选中模型:单击单选(高亮)、Ctrl+单击多选、双击放大为「主控台」(其余 tile 暂停渲染由 §下文最佳努力,先做铺满放大)。维护 `_hosts` 字典,窗体关闭/重投时清理。

**Files:**
- Modify: `src/DroidBus.App/MainForm.cs`
- Create: `src/DroidBus.App/MirrorController.cs`

- [ ] **Step 1: MirrorController(管理多路 ScrcpyHost)**

Create `src/DroidBus.App/MirrorController.cs`:
```csharp
using DroidBus.App.Grid;
using DroidBus.App.Mirror;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App;

/// 管理每台设备的 ScrcpyHost 生命周期(投屏/停止/崩溃重启)。
public sealed class MirrorController : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly Dictionary<string, ScrcpyHost> _hosts = new();

    public MirrorController(BinaryLocator bin) => _bin = bin;

    public bool IsMirroring(string serial) => _hosts.ContainsKey(serial);

    /// 对一个 tile 启动投屏;若已在投屏则忽略。崩溃时自动重启(Task 23 增强)。
    public async Task StartAsync(DeviceTile tile, MirrorOptions options)
    {
        if (tile.Device is not { } dev || !dev.IsControllable) return;
        if (_hosts.ContainsKey(dev.Serial)) return;

        var host = new ScrcpyHost(_bin, tile.Surface);
        _hosts[dev.Serial] = host;
        await host.StartAsync(dev, options);
    }

    /// 重新投屏一台(用于切换单台开关后重启 scrcpy)。
    public async Task RestartAsync(DeviceTile tile, MirrorOptions options)
    {
        Stop(tile.Device?.Serial);
        await StartAsync(tile, options);
    }

    public void Stop(string? serial)
    {
        if (serial is null) return;
        if (_hosts.Remove(serial, out var host)) host.Dispose();
    }

    public void ResizeAll()
    {
        foreach (var h in _hosts.Values) h.Resize();
    }

    public ScrcpyHost? Get(string serial) => _hosts.TryGetValue(serial, out var h) ? h : null;

    public void Dispose()
    {
        foreach (var h in _hosts.Values) h.Dispose();
        _hosts.Clear();
    }
}
```

- [ ] **Step 2: MainForm 接线选中 + 投屏**

Modify `src/DroidBus.App/MainForm.cs` —— 增加字段、构造里给 tile 挂事件、实现选中/投屏/放大。在类内添加:
```csharp
    private readonly MirrorController _mirror;
    private MirrorOptions _globalOptions = new();
    private DeviceTile? _focusedTile; // 放大中的 tile,null 表示网格模式

    // 当前选中的全部 tile(用于群控)
    public IReadOnlyList<DeviceTile> SelectedTiles =>
        _grid.Tiles.Where(t => t.Selected && t.Device is not null).ToList();
```
在构造函数 `_devices = new DeviceManager(adb);` 之后添加:
```csharp
        _mirror = new MirrorController(_bin);
```
在构造函数 `Controls.Add(_toolbar);` 之后、`_devices.DeviceChanged += ...` 之前添加 tile 事件挂接:
```csharp
        foreach (var tile in _grid.Tiles)
        {
            tile.TileClicked += OnTileClicked;
            tile.TileDoubleClicked += OnTileDoubleClicked;
        }
        _grid.Resize += (_, _) => _mirror.ResizeAll();
```
然后把 `OnMirrorAll` 替换为实际实现并新增选中/放大处理:
```csharp
    private async void OnMirrorAll(object? sender, EventArgs e)
    {
        foreach (var tile in _grid.Tiles)
            if (tile.Device is { IsControllable: true })
                await _mirror.StartAsync(tile, _globalOptions);
    }

    private void OnTileClicked(DeviceTile tile)
    {
        var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
        if (!ctrl)
            foreach (var t in _grid.Tiles) t.Selected = false;
        tile.Selected = !tile.Selected || !ctrl;
        SelectedTile = tile;
    }

    private void OnTileDoubleClicked(DeviceTile tile)
    {
        if (_focusedTile is null) FocusTile(tile);
        else RestoreGrid();
    }

    private void FocusTile(DeviceTile focus)
    {
        _focusedTile = focus;
        foreach (var t in _grid.Tiles)
            t.Visible = ReferenceEquals(t, focus);
        _mirror.ResizeAll();
    }

    private void RestoreGrid()
    {
        _focusedTile = null;
        foreach (var t in _grid.Tiles) t.Visible = true;
        _mirror.ResizeAll();
    }
```
在 `FormClosing` 处理中追加清理:
```csharp
        FormClosing += (_, _) => { _pollCts?.Cancel(); _mirror.Dispose(); };
```
(替换原先只 `_pollCts?.Cancel()` 的那一行。)

- [ ] **Step 3: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 4: 【真机验证】6 路并发 + 选中 + 放大**

Run: `dotnet run --project src/DroidBus.App`,点「全部投屏」。
预期:6 个 tile 同时出现实时画面;单击某 tile 橙色高亮、Ctrl+单击可多选;双击一台铺满放大、再次双击还原网格;拖动窗口大小时画面跟随缩放。

- [ ] **Step 5: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): mirror-all, selection model, double-click focus"
```

---

## Task 18: 右侧控制栏 + 单台开关(录屏/息屏/常亮/显示触摸)

右侧 `ControlPanelView`:上半「批量操作」区(Task 20/22 填充),下半「当前选中设备开关」。开关切换后改 `MirrorOptions` 并 `RestartAsync` 该 tile 使新参数生效;显示触摸走 adb(不需重投)。

**Files:**
- Create: `src/DroidBus.App/Controls/ControlPanelView.cs`
- Modify: `src/DroidBus.App/MainForm.cs`
- Modify: `src/DroidBus.Core/Mirror/MirrorOptions.cs`(若非 record,改为 record)

- [ ] **Step 1: 确认 MirrorOptions 是 record(支持 `with`)**

打开 `src/DroidBus.Core/Mirror/MirrorOptions.cs`,确认声明为 `public record MirrorOptions`,各属性为 `{ get; init; }`(Task 6 已如此)。Task 18 依赖 `with` 表达式,若当时写成 `class` 则改为 `record`。

- [ ] **Step 2: ControlPanelView**

Create `src/DroidBus.App/Controls/ControlPanelView.cs`:
```csharp
using DroidBus.Core.Mirror;

namespace DroidBus.App.Controls;

/// 右侧控制栏:上半批量区(后续任务填充),下半单台开关。
public sealed class ControlPanelView : Panel
{
    public FlowLayoutPanel BatchArea { get; } = new();
    private readonly FlowLayoutPanel _single = new();
    private readonly CheckBox _record = new() { Text = "录屏" };
    private readonly CheckBox _screenOff = new() { Text = "息屏投屏" };
    private readonly CheckBox _stayAwake = new() { Text = "常亮不黑屏" };
    private readonly CheckBox _showTouches = new() { Text = "显示触摸" };
    private readonly Label _title = new()
    {
        Text = "未选中设备", Dock = DockStyle.Top, Height = 24, ForeColor = Color.Gainsboro
    };

    /// 单台投屏开关变化(录屏/息屏/常亮),由 MainForm 订阅以重投。
    public event Action? OptionsChanged;
    /// 显示触摸切换(true=开),走 adb 不重投。
    public event Action<bool>? ShowTouchesToggled;

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

        _single.Controls.Add(_title);
        _single.Controls.Add(_record);
        _single.Controls.Add(_screenOff);
        _single.Controls.Add(_stayAwake);
        _single.Controls.Add(_showTouches);

        Controls.Add(_single);     // Fill
        Controls.Add(BatchArea);   // Top
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
```

- [ ] **Step 3: MainForm 挂上 ControlPanelView 并接线**

Modify `src/DroidBus.App/MainForm.cs`:
1) 把字段 `private readonly Panel _controlPanel = new();` 改为
```csharp
    private readonly DroidBus.App.Controls.ControlPanelView _controlPanel = new();
```
2) 构造里把 `_controlPanel.BackColor = ...;` 那行删掉(控件自带配色),仅保留:
```csharp
        _controlPanel.Dock = DockStyle.Right;
        _controlPanel.Width = 260;
```
3) 在挂接 tile 事件那段之后,添加:
```csharp
        _controlPanel.OptionsChanged += OnSingleOptionsChanged;
        _controlPanel.ShowTouchesToggled += OnShowTouchesToggled;
```
4) 新增处理方法(放在类内):
```csharp
    private async void OnSingleOptionsChanged()
    {
        if (SelectedTile is not { Device: { IsControllable: true } } tile) return;
        var opts = _controlPanel.Apply(_globalOptions);
        await _mirror.RestartAsync(tile, opts);
    }

    private async void OnShowTouchesToggled(bool on)
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var adb = new DroidBus.Core.Adb.AdbClient(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(adb, _bin.Adb);
        await ctrl.ExecAsync(dev.Serial, $"shell settings put system show_touches {(on ? 1 : 0)}", default);
    }
```
5) 在 `OnTileClicked` 末尾追加更新右栏标题:
```csharp
        _controlPanel.ShowSelected(tile.Device?.Model ?? tile.Device?.Serial);
```

- [ ] **Step 4: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 5: 【真机验证】单台开关生效**

Run: `dotnet run --project src/DroidBus.App`,投屏后选中一台,逐个切换开关。
预期:勾「显示触摸」立刻在该机出现触点圈;勾「录屏/息屏/常亮」后该 tile 自动重投且生效(录屏文件落在 `RecordDir`;息屏时设备屏幕关闭但 PC 仍有画面)。

- [ ] **Step 6: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): right control panel + per-device toggles"
```

---

## Task 19: 音频转发(sndcpy)+ 中文输入(ADBKeyBoard)

Android 10 不支持 scrcpy 原生音频,改用 `sndcpy.apk` + `adb forward` 走 PC 播放;中文/Unicode 输入用 `ADBKeyBoard.apk`(广播 IME),纯 ASCII 仍可走 `input text`。本任务做两件可单测的 Core 能力 + App 接线。

**Files:**
- Create: `src/DroidBus.Core/Audio/SndcpyCommands.cs`
- Create: `src/DroidBus.Core/Input/ImeCommands.cs`
- Test: `tests/DroidBus.Core.Tests/SndcpyCommandsTests.cs`, `tests/DroidBus.Core.Tests/ImeCommandsTests.cs`
- Modify: `src/DroidBus.Core/Script/ScriptRunner.cs`(`InputTextCommand` 改走 IME 广播)
- Modify: `src/DroidBus.App/Controls/ControlPanelView.cs`(加「转发音频」「输入文字」按钮)
- Modify: `src/DroidBus.App/MainForm.cs`

- [ ] **Step 1: 写失败测试(sndcpy 与 IME 参数构造)**

Create `tests/DroidBus.Core.Tests/SndcpyCommandsTests.cs`:
```csharp
using DroidBus.Core.Audio;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class SndcpyCommandsTests
{
    [Fact]
    public void Install_args_target_serial()
    {
        SndcpyCommands.Install("S1", @"C:\r\sndcpy.apk")
            .Should().Equal("-s", "S1", "install", "-r", @"C:\r\sndcpy.apk");
    }

    [Fact]
    public void Forward_sets_localabstract_socket()
    {
        SndcpyCommands.Forward("S1", 28200)
            .Should().Equal("-s", "S1", "forward", "tcp:28200", "localabstract:sndcpy");
    }

    [Fact]
    public void StartService_uses_am_start_foreground_service()
    {
        SndcpyCommands.StartService("S1")
            .Should().Equal("-s", "S1", "shell", "am", "start-foreground-service",
                "com.rom1v.sndcpy/.RecordService");
    }
}
```

Create `tests/DroidBus.Core.Tests/ImeCommandsTests.cs`:
```csharp
using DroidBus.Core.Input;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ImeCommandsTests
{
    [Fact]
    public void Enable_and_set_make_adbkeyboard_default()
    {
        ImeCommands.Enable("S1").Should().Equal(
            "-s", "S1", "shell", "ime", "enable", "com.android.adbkeyboard/.AdbIME");
        ImeCommands.Set("S1").Should().Equal(
            "-s", "S1", "shell", "ime", "set", "com.android.adbkeyboard/.AdbIME");
    }

    [Fact]
    public void TypeUnicode_broadcasts_msg_extra()
    {
        ImeCommands.TypeUnicode("S1", "你好")
            .Should().Equal("-s", "S1", "shell", "am", "broadcast", "-a",
                "ADB_INPUT_TEXT", "--es", "msg", "你好");
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter "SndcpyCommandsTests|ImeCommandsTests"`
Expected: FAIL,类型不存在。

- [ ] **Step 3: 实现 SndcpyCommands**

Create `src/DroidBus.Core/Audio/SndcpyCommands.cs`:
```csharp
namespace DroidBus.Core.Audio;

/// 构造 sndcpy 音频转发所需的 adb 参数(Android 10 用)。
public static class SndcpyCommands
{
    public static IReadOnlyList<string> Install(string serial, string apkPath) =>
        new[] { "-s", serial, "install", "-r", apkPath };

    public static IReadOnlyList<string> Forward(string serial, int port) =>
        new[] { "-s", serial, "forward", $"tcp:{port}", "localabstract:sndcpy" };

    public static IReadOnlyList<string> StartService(string serial) =>
        new[] { "-s", serial, "shell", "am", "start-foreground-service",
            "com.rom1v.sndcpy/.RecordService" };
}
```

- [ ] **Step 4: 实现 ImeCommands**

Create `src/DroidBus.Core/Input/ImeCommands.cs`:
```csharp
namespace DroidBus.Core.Input;

/// ADBKeyBoard IME 命令:启用/设为默认/广播 Unicode 文本。
public static class ImeCommands
{
    private const string Component = "com.android.adbkeyboard/.AdbIME";

    public static IReadOnlyList<string> Enable(string serial) =>
        new[] { "-s", serial, "shell", "ime", "enable", Component };

    public static IReadOnlyList<string> Set(string serial) =>
        new[] { "-s", serial, "shell", "ime", "set", Component };

    public static IReadOnlyList<string> TypeUnicode(string serial, string text) =>
        new[] { "-s", serial, "shell", "am", "broadcast", "-a", "ADB_INPUT_TEXT", "--es", "msg", text };
}
```

- [ ] **Step 5: 运行确认通过**

Run: `dotnet test --filter "SndcpyCommandsTests|ImeCommandsTests"`
Expected: PASS(5 个)。

- [ ] **Step 6: ScriptRunner 的 InputTextCommand 改走 IME**

`InputTextCommand`(「输入文本」)应走 ADBKeyBoard 而非 `input text`,以支持中文。在 `IDeviceController` 增加方法并在 `AdbDeviceController` 实现:
```csharp
    // IDeviceController 接口新增:
    Task TypeUnicodeAsync(string serial, string text, CancellationToken ct);
```
```csharp
    // AdbDeviceController 实现:
    public Task TypeUnicodeAsync(string serial, string text, CancellationToken ct) =>
        Run(DroidBus.Core.Input.ImeCommands.TypeUnicode(serial, text), ct);
```
(`Run` 为 AdbDeviceController 内已有的「调 _adb + 参数」私有助手;若名称不同,沿用现有那个。)然后在 `ScriptRunner.RunAsync` 的 `switch` 中,把 `InputTextCommand` 分支由 `TextAsync` 改为:
```csharp
            case InputTextCommand t:
                await _ctrl.TypeUnicodeAsync(serial, t.Text, ct);
                break;
```
`AdbTextCommand`(「ADB文本」)分支保持调用 `TextAsync`(`input text`,纯 ASCII)。

- [ ] **Step 7: 更新 ScriptRunner 测试中的 InputText 断言**

打开 `tests/DroidBus.Core.Tests/ScriptRunnerTests.cs`,把验证「输入文本」的用例由期望 `input text ...` 改为期望调用 `TypeUnicodeAsync`(对 fake controller 记录的调用断言)。示例:
```csharp
    [Fact]
    public async Task InputText_uses_ime_broadcast()
    {
        var ctrl = new FakeController();
        var runner = new ScriptRunner(ctrl, new FakeClock());
        await runner.RunAsync("S1", new ScriptCommand[] { new InputTextCommand("你好") }, default);
        ctrl.Unicode.Should().ContainSingle().Which.Should().Be(("S1", "你好"));
    }
```
(在 `FakeController` 里加 `public List<(string,string)> Unicode { get; } = new();` 并在 `TypeUnicodeAsync` 记录。)

- [ ] **Step 8: App 接线「转发音频」「输入文字」**

在 `ControlPanelView` 的 `_single` 区追加两个按钮并暴露事件:
```csharp
    public event Action? AudioRequested;
    public event Action? TypeTextRequested;
```
构造里:
```csharp
        var audioBtn = new Button { Text = "转发音频", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        var typeBtn = new Button { Text = "输入文字", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        audioBtn.Click += (_, _) => AudioRequested?.Invoke();
        typeBtn.Click += (_, _) => TypeTextRequested?.Invoke();
        _single.Controls.Add(audioBtn);
        _single.Controls.Add(typeBtn);
```
在 `MainForm` 订阅:
```csharp
        _controlPanel.AudioRequested += OnAudioRequested;
        _controlPanel.TypeTextRequested += OnTypeTextRequested;
```
```csharp
    private async void OnAudioRequested()
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var runner = new DroidBus.Core.Process.ProcessRunner();
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.Install(dev.Serial, _bin.SndcpyApk), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.Forward(dev.Serial, 28200), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Audio.SndcpyCommands.StartService(dev.Serial), default);
        // sndcpy 的 PC 端播放器随原 app 资源附带;若无则提示用户手动起 sndcpy.bat。
        MessageBox.Show("已在设备侧启动音频服务。若 PC 无声,请运行 Resources 下的 sndcpy 播放端。");
    }

    private async void OnTypeTextRequested()
    {
        if (SelectedTile?.Device is not { IsControllable: true } dev) return;
        var text = Microsoft.VisualBasic.Interaction.InputBox("输入要发送到设备的文本", "输入文字", "");
        if (string.IsNullOrEmpty(text)) return;
        var adb = new DroidBus.Core.Adb.AdbClient(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(adb, _bin.Adb);
        await ctrl.TypeUnicodeAsync(dev.Serial, text, default);
    }
```
（`Microsoft.VisualBasic` 在 .NET 8 需在 `DroidBus.App.csproj` 设 `<UseWindowsForms>true</UseWindowsForms>` 时自带;若编译报缺失,加 `<Reference>` 或改用一个自建的简单输入对话框。）

- [ ] **Step 9: 构建 + 单测**

Run: `dotnet build` -> succeeded。
Run: `dotnet test` -> 全绿(含改写后的 ScriptRunner 用例)。

- [ ] **Step 10: 【真机验证】音频与中文输入**

Run app,选中一台:点「转发音频」→ PC 出声(在设备上播放音乐验证);点「输入文字」输入「你好世界」→ 设备当前输入框出现中文。首次需在设备上把默认输入法切到 ADBKeyBoard(本任务的 `ime enable/set` 已尝试自动切换;若系统弹确认则手动允许一次)。

- [ ] **Step 11: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat: sndcpy audio forward + ADBKeyBoard unicode input"
```

---

## Task 20: 批量操作(装/卸 APK、推/拉文件、批量启动应用)

把 `BatchExecutor` 接到右栏批量区:对**所有选中设备**并行装/卸 APK、推/拉文件、按包名启动应用;每台进度回显、结束汇总失败列表。Core 端先补一个把 `BatchResult` 汇总成文案的纯函数(可单测)。

**Files:**
- Create: `src/DroidBus.Core/Batch/BatchReport.cs`
- Test: `tests/DroidBus.Core.Tests/BatchReportTests.cs`
- Create: `src/DroidBus.App/Controls/BatchOpsView.cs`
- Modify: `src/DroidBus.App/Controls/ControlPanelView.cs`(把 BatchOpsView 放进 `BatchArea`)
- Modify: `src/DroidBus.App/MainForm.cs`(执行批量并回显)

- [ ] **Step 1: 写失败测试(BatchReport 汇总)**

Create `tests/DroidBus.Core.Tests/BatchReportTests.cs`:
```csharp
using DroidBus.Core.Batch;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BatchReportTests
{
    [Fact]
    public void All_success_reports_count()
    {
        var r = new BatchResult(
            Succeeded: new List<string> { "S1", "S2" },
            Failed: new Dictionary<string, string>());
        BatchReport.Summarize(r).Should().Be("成功 2 台,失败 0 台。");
    }

    [Fact]
    public void Failures_listed_with_reason()
    {
        var r = new BatchResult(
            Succeeded: new List<string> { "S1" },
            Failed: new Dictionary<string, string> { ["S2"] = "install failed" });
        BatchReport.Summarize(r).Should().Be(
            "成功 1 台,失败 1 台。\n失败设备:\n  S2: install failed");
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter BatchReportTests`
Expected: FAIL,`BatchReport` 不存在。

- [ ] **Step 3: 实现 BatchReport**

Create `src/DroidBus.Core/Batch/BatchReport.cs`:
```csharp
using System.Text;

namespace DroidBus.Core.Batch;

public static class BatchReport
{
    public static string Summarize(BatchResult r)
    {
        var sb = new StringBuilder();
        sb.Append($"成功 {r.Succeeded.Count} 台,失败 {r.Failed.Count} 台。");
        if (r.Failed.Count > 0)
        {
            sb.Append("\n失败设备:");
            foreach (var kv in r.Failed)
                sb.Append($"\n  {kv.Key}: {kv.Value}");
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter BatchReportTests`
Expected: PASS(2 个)。

- [ ] **Step 5: BatchOpsView(批量按钮组)**

Create `src/DroidBus.App/Controls/BatchOpsView.cs`:
```csharp
namespace DroidBus.App.Controls;

/// 批量操作按钮组。事件由 MainForm 处理(它持有选中设备与执行器)。
public sealed class BatchOpsView : FlowLayoutPanel
{
    public event Action? InstallApk;
    public event Action? UninstallApk;
    public event Action? PushFile;
    public event Action? PullFile;
    public event Action? LaunchApp;

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
    }

    private void Add(string text, Action onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Width = 220, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        b.Click += (_, _) => onClick();
        Controls.Add(b);
    }
}
```

- [ ] **Step 6: 把 BatchOpsView 放进 ControlPanelView**

Modify `src/DroidBus.App/Controls/ControlPanelView.cs`:增加字段并暴露,放入 `BatchArea`:
```csharp
    public BatchOpsView BatchOps { get; } = new();
```
构造里(配置好 `BatchArea` 之后)添加:
```csharp
        BatchArea.Controls.Add(BatchOps);
```

- [ ] **Step 7: MainForm 执行批量并回显**

Modify `src/DroidBus.App/MainForm.cs`:构造里订阅:
```csharp
        var ops = _controlPanel.BatchOps;
        ops.InstallApk  += OnBatchInstallApk;
        ops.UninstallApk += OnBatchUninstallApk;
        ops.PushFile    += OnBatchPushFile;
        ops.PullFile    += OnBatchPullFile;
        ops.LaunchApp   += OnBatchLaunchApp;
```
新增辅助与处理方法:
```csharp
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
```

- [ ] **Step 8: 构建 + 单测**

Run: `dotnet build` -> succeeded。
Run: `dotnet test` -> 全绿。

- [ ] **Step 9: 【真机验证】批量四操作**

选中 ≥2 台后:装一个测试 APK → 各机出现该应用;按包名启动 → 各机前台打开;推一个文件到 `/sdcard/` → 在设备上确认存在;拉回 → PC 出现按序列号命名的副本。故意让一台离线,确认其余成功、汇总框列出失败那台。

- [ ] **Step 10: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat: batch install/uninstall/push/pull/launch via BatchExecutor"
```

---

## Task 21: 同步输入广播(主控台手势 → 多机)

放大某台为主控台后,开「同步输入广播」:在主控台画面上的点击/滑动,经 `SyncInputTranslator` 换算成设备坐标,**并行下发到所有选中设备**(含主控台本身,走 adb,避免依赖嵌入窗口的鼠标拦截)。实现手段:在主控台 `Surface` 之上盖一个**置顶半透明捕获层**(独立无边框 `Form`,贴合 Surface 屏幕坐标),从根上规避嵌入式子窗口抢走鼠标的 airspace 问题。

> 设备分辨率:目标 6 台 Note9 均为 1440×2960(见 spec)。用常量;后续若接异构设备再查 `wm size`。

**Files:**
- Create: `src/DroidBus.App/Input/BroadcastOverlay.cs`
- Modify: `src/DroidBus.App/Controls/ControlPanelView.cs`(加「同步输入广播」开关)
- Modify: `src/DroidBus.App/MainForm.cs`

- [ ] **Step 1: BroadcastOverlay 捕获层**

Create `src/DroidBus.App/Input/BroadcastOverlay.cs`:
```csharp
namespace DroidBus.App.Input;

/// 贴在主控台画面之上的半透明捕获层。捕获一次按下→抬起,回调原始格子像素坐标。
public sealed class BroadcastOverlay : Form
{
    private Point _down;
    private bool _dragging;

    /// (downX,downY,upX,upY) —— 相对捕获层(= 主控台画面)的像素坐标。
    public event Action<int, int, int, int>? Gesture;

    public BroadcastOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.DeepSkyBlue;
        Opacity = 0.12; // 轻微着色,提示「广播中」
    }

    /// 贴合目标控件的屏幕矩形。
    public void CoverControl(Control target)
    {
        var topLeft = target.PointToScreen(Point.Empty);
        Location = topLeft;
        Size = target.ClientSize;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _down = e.Location; _dragging = true;
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            Gesture?.Invoke(_down.X, _down.Y, e.X, e.Y);
        }
        base.OnMouseUp(e);
    }
}
```

- [ ] **Step 2: ControlPanelView 加广播开关**

Modify `src/DroidBus.App/Controls/ControlPanelView.cs`:在 `_single` 区(或 BatchArea)添加:
```csharp
    private readonly CheckBox _broadcast = new() { Text = "同步输入广播", ForeColor = Color.Gold, AutoSize = true };
    public event Action<bool>? BroadcastToggled;
```
构造里:
```csharp
        _broadcast.CheckedChanged += (_, _) => BroadcastToggled?.Invoke(_broadcast.Checked);
        _single.Controls.Add(_broadcast);
```

- [ ] **Step 3: MainForm 接线广播**

Modify `src/DroidBus.App/MainForm.cs`:增加字段:
```csharp
    private DroidBus.App.Input.BroadcastOverlay? _overlay;
    private const int DeviceW = 1440;   // Note9
    private const int DeviceH = 2960;
```
构造里订阅:
```csharp
        _controlPanel.BroadcastToggled += OnBroadcastToggled;
```
实现:
```csharp
    private void OnBroadcastToggled(bool on)
    {
        if (on)
        {
            if (_focusedTile?.Device is not { IsControllable: true })
            {
                MessageBox.Show("请先双击放大一台作为主控台");
                return;
            }
            _overlay = new DroidBus.App.Input.BroadcastOverlay();
            _overlay.Gesture += OnBroadcastGesture;
            _overlay.CoverControl(_focusedTile.Surface);
            _overlay.Show(this);
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
        if (_focusedTile?.Surface is not { } surf) return;
        var cmd = DroidBus.Core.Control.SyncInputTranslator.Translate(
            dx, dy, ux, uy, surf.ClientSize.Width, surf.ClientSize.Height, DeviceW, DeviceH);

        var serials = SelectedTiles.Count > 0
            ? SelectedTiles.Select(t => t.Device!.Serial).ToList()
            : new List<string> { _focusedTile.Device!.Serial };

        var adb = new DroidBus.Core.Adb.AdbClient(new DroidBus.Core.Process.ProcessRunner(), _bin.Adb);
        var ctrl = new DroidBus.Core.Control.AdbDeviceController(adb, _bin.Adb);

        await DroidBus.Core.Batch.BatchExecutor.RunAsync(serials, async (s, ct) =>
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
    }
```
窗体大小/位置变化时让 overlay 跟随(可选,简单起见在 `Move`/`Resize`/`FocusTile`/`RestoreGrid` 后,若 `_overlay != null` 调 `_overlay.CoverControl(_focusedTile.Surface)`)。在 `FormClosing` 里追加 `_overlay?.Dispose();`。

> 注:`SwipeAsync` 形参取 `(serial,x1,y1,x2,y2,durationMs,ct)`。若 Task 9 的 `AdbDeviceController.SwipeAsync` 未含 `durationMs` 形参,则在此调用其现有签名(去掉 200),并由 `AdbCommands.Swipe` 决定时长。保持与 Task 5/9 的实际签名一致。

- [ ] **Step 4: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 5: 【真机验证】一拖多**

放大一台为主控台,勾「同步输入广播」并多选若干设备。在主控台画面上点击/滑动:
预期:所有选中设备同步出现同一位置的点击/同方向滑动(各机分辨率一致,落点一致)。关掉开关后捕获层消失,主控台恢复独占操作。

- [ ] **Step 6: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat(app): sync input broadcast via capture overlay"
```

---

## Task 22: 脚本引擎 UI(加载 `.adb` → 多机并行执行)

把已有的 `ScriptParser.ParseGbkFile` + `ScriptRunner` 接到 UI:选个 `.adb` 文件,在**所有选中设备**上并行跑,每台一个 `ScriptRunner`,结束汇总失败设备。Core 端无需新逻辑(Task 11/12 已就绪),本任务以 App 接线为主,外加一个把「在多机上跑脚本」封装起来的协调器(便于复用)。

**Files:**
- Create: `src/DroidBus.App/Scripting/ScriptLauncher.cs`
- Modify: `src/DroidBus.App/Controls/BatchOpsView.cs`(加「跑脚本」按钮)
- Modify: `src/DroidBus.App/MainForm.cs`

- [ ] **Step 1: ScriptLauncher(多机并行跑脚本)**

Create `src/DroidBus.App/Scripting/ScriptLauncher.cs`:
```csharp
using DroidBus.Core;
using DroidBus.Core.Adb;
using DroidBus.Core.Batch;
using DroidBus.Core.Control;
using DroidBus.Core.Process;
using DroidBus.Core.Script;
using DroidBus.Core.Time;

namespace DroidBus.App.Scripting;

/// 在多台设备上并行执行同一脚本,返回批量结果。
public sealed class ScriptLauncher
{
    private readonly BinaryLocator _bin;
    public ScriptLauncher(BinaryLocator bin) => _bin = bin;

    public async Task<BatchResult> RunFileAsync(
        string adbScriptPath, IReadOnlyList<string> serials, CancellationToken ct)
    {
        var commands = ScriptParser.ParseGbkFile(adbScriptPath);
        return await BatchExecutor.RunAsync(serials, async (serial, token) =>
        {
            var adb = new AdbClient(new ProcessRunner(), _bin.Adb);
            var ctrl = new AdbDeviceController(adb, _bin.Adb);
            var runner = new ScriptRunner(ctrl, new SystemClock());
            await runner.RunAsync(serial, commands, token);
        }, onProgress: _ => { }, ct: ct);
    }
}
```

- [ ] **Step 2: BatchOpsView 加「跑脚本」**

Modify `src/DroidBus.App/Controls/BatchOpsView.cs`:加事件与按钮:
```csharp
    public event Action? RunScript;
```
在构造里 `Add(...)` 列表末尾添加:
```csharp
        Add("跑脚本(.adb)", () => RunScript?.Invoke());
```

- [ ] **Step 3: MainForm 接线跑脚本**

Modify `src/DroidBus.App/MainForm.cs`:在订阅 BatchOps 事件那段添加:
```csharp
        ops.RunScript += OnRunScript;
```
新增字段与处理:
```csharp
    private readonly DroidBus.App.Scripting.ScriptLauncher _scripts;
```
(在构造里 `_mirror = new MirrorController(_bin);` 之后初始化 `_scripts = new DroidBus.App.Scripting.ScriptLauncher(_bin);`。)
```csharp
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
```

- [ ] **Step 4: 构建**

Run: `dotnet build`
Expected: Build succeeded。

- [ ] **Step 5: 【真机验证】多机跑原 `.adb` 脚本**

准备一个原 GBK 编码的 `.adb` 脚本(例如含「点击」「滑动」「延时」「返回桌面」「启动应用」),选中多台后点「跑脚本」。
预期:各机按脚本动作并行执行,中文命令被正确解析(GB2312 解码无乱码),结束弹出成功/失败汇总。故意放一条非法行,确认报错信息可读且不影响其余设备(失败隔离)。

- [ ] **Step 6: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat: .adb script engine UI with multi-device parallel run"
```

---

## Task 23: 韧性(scrcpy 崩溃自重启 / 掉线掉授权 / adb server 恢复)

三类容错:① scrcpy 进程意外退出 → 自动重投该格(带退避与重试上限);② 轮询发现设备掉线/掉授权 → tile 置灰提示、停掉其投屏;③ adb server 异常 → `kill-server` + `start-server` 后重新枚举握手。先补可单测的 `AdbCommands.KillServer/StartServer`,再做 App 接线。

**Files:**
- Modify: `src/DroidBus.Core/Adb/AdbCommands.cs`(加 KillServer/StartServer)
- Test: `tests/DroidBus.Core.Tests/AdbCommandsTests.cs`(追加用例)
- Modify: `src/DroidBus.App/MirrorController.cs`(崩溃自重启)
- Modify: `src/DroidBus.App/MainForm.cs`(掉线处理 + adb 恢复按钮)

- [ ] **Step 1: 追加失败测试(server 命令)**

在 `tests/DroidBus.Core.Tests/AdbCommandsTests.cs` 追加:
```csharp
    [Fact]
    public void KillServer_and_StartServer_args()
    {
        AdbCommands.KillServer().Should().Equal("kill-server");
        AdbCommands.StartServer().Should().Equal("start-server");
    }
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test --filter AdbCommandsTests`
Expected: FAIL,方法不存在。

- [ ] **Step 3: 实现 KillServer/StartServer**

在 `src/DroidBus.Core/Adb/AdbCommands.cs` 添加:
```csharp
    public static IReadOnlyList<string> KillServer() => new[] { "kill-server" };
    public static IReadOnlyList<string> StartServer() => new[] { "start-server" };
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test --filter AdbCommandsTests`
Expected: PASS。

- [ ] **Step 5: MirrorController 崩溃自重启**

Modify `src/DroidBus.App/MirrorController.cs`:为每台记录重试次数,`Crashed` 时退避重投(上限 3 次)。把 `StartAsync` 改为接管崩溃事件:
```csharp
    private readonly Dictionary<string, int> _retries = new();
    private const int MaxRetries = 3;

    // 由 MainForm 注入:把「重投某 tile」的动作回调进来(因为重投需要 tile + options)。
    public Func<string, Task>? RestartRequested { get; set; }
```
在 `StartAsync` 内、`await host.StartAsync(...)` 之前挂接:
```csharp
        host.Crashed += async code =>
        {
            if (!_hosts.ContainsKey(dev.Serial)) return; // 已被主动 Stop
            var n = _retries.GetValueOrDefault(dev.Serial);
            if (n >= MaxRetries) return;
            _retries[dev.Serial] = n + 1;
            _hosts.Remove(dev.Serial, out var dead); dead?.Dispose();
            await Task.Delay(500 * (n + 1));
            if (RestartRequested is not null) await RestartRequested(dev.Serial);
        };
```
`StartAsync` 成功后清零重试:在方法末尾(`await host.StartAsync` 之后)加 `_retries[dev.Serial] = 0;`。`Stop(serial)` 内追加 `_retries.Remove(serial);`。

- [ ] **Step 6: MainForm 注入重投回调 + 掉线处理 + adb 恢复**

Modify `src/DroidBus.App/MainForm.cs`:
1) 构造里 `_mirror = new MirrorController(_bin);` 之后:
```csharp
        _mirror.RestartRequested = async serial =>
        {
            var tile = _grid.Tiles.FirstOrDefault(t => t.Device?.Serial == serial);
            if (tile is not null && IsHandleCreated)
                await (Task)BeginInvoke(async () => await _mirror.StartAsync(tile, _globalOptions));
        };
```
2) `OnDeviceChanged` 内,对掉线/掉授权设备停掉投屏:
```csharp
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
```
(tile 置灰由 `DeviceTile.UpdateHeader` 已实现:非在线时头部变红、文案为「离线/未授权」。)
3) 顶部工具条加「修复 ADB」按钮(kill/start-server 后刷新):
```csharp
        AddToolbarButton("修复 ADB", async (_, _) => await RecoverAdbAsync());
```
```csharp
    private async Task RecoverAdbAsync()
    {
        var runner = new DroidBus.Core.Process.ProcessRunner();
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.KillServer(), default);
        await runner.RunAsync(_bin.Adb, DroidBus.Core.Adb.AdbCommands.StartServer(), default);
        await _devices.RefreshAsync();
        RebindTiles();
        MessageBox.Show("已重启 adb server 并重新枚举设备。", "修复 ADB");
    }
```

- [ ] **Step 7: 构建 + 单测**

Run: `dotnet build` -> succeeded。
Run: `dotnet test` -> 全绿。

- [ ] **Step 8: 【真机验证】三类容错**

- 崩溃自重启:投屏后 `taskkill` 掉某台的 `scrcpy.exe`(或拔线再插)→ 该格 3 秒内自动恢复画面(最多重试 3 次)。
- 掉线/掉授权:拔掉一台 → 其 tile 头部变红「离线」、画面停止;在设备上撤销 USB 调试授权 → 显示「未授权」。
- adb 恢复:`adb kill-server` 后点「修复 ADB」→ 设备重新出现在网格。

- [ ] **Step 9: Commit**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" add -A
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit -m "feat: resilience — scrcpy auto-restart, offline handling, adb recovery"
```

---

## Task 24: 6 真机整体集成验收

不写新代码,按里程碑逐项在 6 块已授权 Note9 上跑通。任何一项不过 → 回到对应 Task 修复后重跑。

- [ ] **Step 1: M1 地基**
  - `dotnet run --project src/DroidBus.App`:3×2 网格出现 6 台真机型号/在线/电量;拔插 3 秒内更新。

- [ ] **Step 2: M2 多路 + 单台开关**
  - 「全部投屏」:6 路画面同时流畅;单击高亮、Ctrl 多选、双击放大/还原、缩放跟随。
  - 选中一台逐项切:录屏(文件落 RecordDir)、息屏投屏、常亮、显示触摸、转发音频(PC 出声)、输入文字(中文进设备)。

- [ ] **Step 3: M3 群控**
  - 同步输入广播:主控台点击/滑动 → 选中各机一致动作。
  - 批量:装/卸 APK、推/拉文件、按包名启动,≥2 台并行;故意 1 台离线 → 其余成功 + 失败汇总。

- [ ] **Step 4: M4 脚本**
  - 加载原 GBK `.adb` 脚本多机并行执行,中文无乱码,失败隔离 + 汇总。

- [ ] **Step 5: 韧性**
  - 杀 scrcpy 自动恢复;掉线/掉授权置灰;「修复 ADB」重新枚举。

- [ ] **Step 6: 最终提交(打 tag)**

```bash
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" tag v1.0
git -c user.name="gaozhi" -c user.email="gaozhi@xinshu.ai" commit --allow-empty -m "chore: v1.0 — 6-device integration verified"
```

---

## 自查(Self-Review)

写完计划后对照 spec 复核的结果:

### 1. Spec 覆盖
- §6 灰掉功能映射:录屏 → Task 6/18;息屏 → Task 6/18;常亮 → Task 6/18;显示触摸 → Task 5/18;剪贴板 → scrcpy 默认双向(Task 6,无需额外代码);中文输入 → Task 19;音频 → Task 19。**全覆盖**。
- §7 群控四能力:同步输入广播 → Task 13/21;批量启动应用 → Task 5/20;批量装卸 APK / 推拉文件 → Task 5/20;批量跑脚本 → Task 11/12/22。**全覆盖**。
- §8 脚本 DSL 全部命令 → Task 11(解析)+ Task 12(执行)+ Task 19(输入文本改 IME)。**全覆盖**。
- §5 布局 A(顶部工具条 / 画面墙 / 右侧控制栏)→ Task 15/16/17/18。
- §9 错误处理(掉线置灰 / scrcpy 重启 / 批量失败汇总 / adb 恢复)→ Task 8/20/23。
- §2 不在范围:第二桌面**未出现在任何任务**(正确)。

### 2. 占位符扫描
无 TBD/TODO/“稍后实现”;每个代码步骤均给出完整可编译代码,真机步骤均有明确预期。✔

### 3. 类型/签名一致性
- `ScrcpyHost(BinaryLocator, Control)` / `StartAsync(Device, MirrorOptions)` / `Crashed: Action<int>` / `Resize()` / `Stop()` / `Dispose()` —— Task 14 定义,Task 17/23 一致使用。
- `MirrorController.StartAsync/RestartAsync/Stop/ResizeAll/Get` + `RestartRequested` —— Task 17 定义,Task 23 扩展(新增字段不破坏既有签名)。
- `DeviceTile.Surface/Selected/Bind/UpdateHeader/TileClicked/TileDoubleClicked` —— Task 15 定义,Task 17/18/21 一致使用。
- `MirrorOptions` 为 record(`with` 依赖)—— Task 6 定义,Task 18 Step 1 显式校验。
- `BatchExecutor.RunAsync(serials, Func<string,CancellationToken,Task>, onProgress, ct) → BatchResult(Succeeded, Failed)` —— Task 8 定义,Task 20/21/22 一致使用;`BatchReport.Summarize` Task 20 新增。
- `AdbDeviceController`:`TapAsync/SwipeAsync/KeyEventAsync/TextAsync/LaunchAppAsync/ExecAsync` Task 9 定义;`TypeUnicodeAsync` Task 19 新增并同步更新 `IDeviceController` 与 ScriptRunner 测试。**Task 21 Step 3 已注明 `SwipeAsync` 形参须与 Task 9 实际签名对齐**。
- `SyncInputTranslator.Translate(...)` 返回 `ScriptCommand`(Tap/Swipe)—— Task 13 定义,Task 21 按类型分支使用。
- `ScriptParser.ParseGbkFile` / `ScriptRunner(IDeviceController, IClock).RunAsync` —— Task 11/12 定义,Task 22 经 `ScriptLauncher` 使用。

### 4. 已修正项
- Task 19 改 `InputTextCommand` 走 IME 后,**同步要求更新 Task 12 的 ScriptRunner 测试**(Task 19 Step 7),避免遗留与新行为矛盾的旧断言。

---

> **执行说明**:本计划用 TDD 推进 Core(Task 0–13,真单测),App/互操作层(Task 14–24)因涉及 Win32 嵌入与真机,给出完整可编译代码 + 【真机验证】手动步骤。所有 git 提交用一次性身份标志 `-c user.name=... -c user.email=...`,不改全局配置。
