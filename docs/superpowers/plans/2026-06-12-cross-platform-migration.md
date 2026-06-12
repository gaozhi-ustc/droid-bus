# DroidBus 跨平台迁移 Implementation Plan

**Goal:** 把 Windows 专用的 DroidBus 群控台迁移为跨平台(win-x64 / linux-x64 / osx-x64 / osx-arm64)单代码库;UI 换 Avalonia;scrcpy 窗口嵌入抽象为平台实现;二进制按 `tools/<rid>/` 打包。**本期落地:Ubuntu(`DISPLAY=:1`,X11)跑通**;Windows 保持可用;macOS 仅占位。

**关联设计:** `docs/superpowers/specs/2026-06-12-cross-platform-migration-design.md`

**Tech Stack:** C# / .NET 8;Avalonia UI 11;SkiaSharp(缩略图解码);xUnit + FluentAssertions;复用 adb/scrcpy 2.x。

**约定:**
- 解决方案根:`/home/gaozhi/git_projects/droid-bus`(Linux 开发);源码 `src/`,测试 `tests/`,二进制 `tools/<rid>/`。
- 所有 adb/scrcpy 调用经 `IProcessRunner` 抽象,便于单测 fake。
- 标 **【真机验证】** 的步骤需已授权设备 + `DISPLAY=:1`,无法单测。
- 平台二进制不进 git;`tools/<rid>/` 仅留 `README.md` + `.gitignore`,本地放置。

**前置(用户在本机以 `!` 前缀或自行执行,需 sudo 口令):**
```bash
# .NET 8 SDK
sudo apt update && sudo apt install -y dotnet-sdk-8.0      # 或用 Microsoft 源/脚本
# adb + scrcpy(linux-x64)
sudo apt install -y adb scrcpy
# 验证
dotnet --version    # 期望 8.x
adb version ; scrcpy --version
```

---

## 现状基线(开始前)

- `DroidBus.Core`(net8.0)纯逻辑,跨平台就绪度高;耦合点:`BinaryLocator`(Win 路径/`.exe`)、`ProcessRunnerTests`(`cmd.exe`)。
- `DroidBus.App`(net8.0-windows WinForms):`Program.cs`/`MainForm.cs`(478 行)/`Grid/*`/`Controls/*`/`Input/BroadcastOverlay.cs`/`Mirror/{ScrcpyHost,ThumbnailPoller}.cs`/`Interop/NativeMethods.cs`/`MirrorController.cs`。
- 全部 23 个 Core 测试应在迁移后保持通过。

---

## Task 0:目录结构重整 + 基线可构建

**Files:** 新增 `Directory.Build.props`、`tools/<rid>/README.md`、`scripts/{windows,linux}/`;移动 `*.ps1`→`scripts/windows/`,`inventory.*`→`assets/`;更新 `.gitignore`。

- [ ] **Step 1:建目录骨架**
  ```bash
  mkdir -p tools/win-x64 tools/linux-x64 tools/osx-x64 tools/osx-arm64 \
           scripts/windows scripts/linux assets
  git mv Check-UnlockWindow.ps1 Get-SimNumbers.ps1 _shot.ps1 scripts/windows/
  git mv inventory.csv inventory.json assets/
  ```
- [ ] **Step 2:每个 `tools/<rid>/` 放 `README.md`**(说明放哪些二进制 + 版本)与根 `.gitignore` 追加忽略二进制(保留 README)。
- [ ] **Step 3:`Directory.Build.props`** 设共享属性(`LangVersion`、`Nullable`、`ImplicitUsings`、默认 `RuntimeIdentifiers`)。
- [ ] **Step 4:`dotnet build`** 确认现状仍能在 Windows 解析(Linux 上 App 因 `net8.0-windows` 暂不构建,符合预期;Core+Tests 可构建)。
- [ ] **Step 5:Commit** `chore: restructure repo for cross-platform layout`

---

## Task 1:Core — `BinaryLocator` 按 RID 解析

**Files:** `src/DroidBus.Core/BinaryLocator.cs`;Test `tests/.../BinaryLocatorTests.cs`(扩充)。

- [ ] **Step 1:写失败测试**——给定 `tools/linux-x64/` 放 `adb`/`scrcpy`(无 `.exe`)能定位;`DROIDBUS_TOOLS` 覆盖;都缺时回退 PATH 命中(用临时 PATH 注入 fake `adb`)。保留原 Windows `.exe` 用例(按 `OperatingSystem` 条件)。
- [ ] **Step 2:确认失败。**
- [ ] **Step 3:实现**——
  - `static string Rid` 由 `RuntimeInformation.RuntimeIdentifier`/`OSArchitecture` 归一为四 RID 之一。
  - 工具文件名:`OperatingSystem.IsWindows()? "adb.exe":"adb"`,scrcpy 同理。
  - 解析:`DROIDBUS_TOOLS` → `Path.Combine(AppContext.BaseDirectory,"tools",Rid)` → PATH(`Environment.GetEnvironmentVariable("PATH")` 拆分查可执行)。
  - `scrcpy-server`:`SCRCPY_SERVER_PATH` → `tools/<rid>/scrcpy-server` → 常见 share 目录(`/usr/share/scrcpy/`、`/usr/local/share/scrcpy/`)。
  - apk(sndcpy/Adbkeyboard):打包模式 `tools/<rid>/`;缺失不致命(惰性 require)。
- [ ] **Step 4:确认通过(Linux 上跑)。**
- [ ] **Step 5:Commit** `feat(core): RID-aware BinaryLocator with PATH fallback`

---

## Task 2:Core — 测试去 Windows 依赖

**Files:** `tests/.../ProcessRunnerTests.cs`。

- [ ] **Step 1:** 把 `cmd.exe /c echo hello` 改为跨平台:Windows→`cmd /c echo hello`,否则→`/bin/sh -c "echo hello"`(用 `OperatingSystem.IsWindows()` 分支),断言 stdout=`hello`。
- [ ] **Step 2:`dotnet test`** 全绿(Linux)。
- [ ] **Step 3:Commit** `test(core): make ProcessRunner test cross-platform`

---

## Task 3:Avalonia App 脚手架(替换 WinForms 工程)

**Files:** 重建 `src/DroidBus.App/DroidBus.App.csproj`(`net8.0`,删 `UseWindowsForms`/`net8.0-windows`;加 Avalonia + SkiaSharp 包);`Program.cs`、`App.axaml(.cs)`、`MainWindow.axaml(.cs)` 占位。

- [ ] **Step 1:** 新工程引用 Avalonia 11、`Avalonia.Desktop`、`Avalonia.Themes.Fluent`、`SkiaSharp`。保留对 `DroidBus.Core` 引用。
- [ ] **Step 2:** 最小 `MainWindow` 显示 "DroidBus" 标题 + 空网格占位。
- [ ] **Step 3:【真机验证】** `DISPLAY=:1 dotnet run --project src/DroidBus.App` 出窗。
- [ ] **Step 4:Commit** `feat(app): bootstrap Avalonia cross-platform shell`

---

## Task 4:窗口嵌入抽象 `INativeWindowEmbedder`

**Files:** `src/DroidBus.App/Interop/INativeWindowEmbedder.cs`、`Windows/Win32Embedder.cs`、`Linux/X11Embedder.cs`、`MacOS/MacEmbedder.cs`、`EmbedderFactory.cs`。

- [ ] **Step 1:** 定义接口(`FindWindow(pid,title)`/`Embed(child,host)`/`MoveResize`/`Release`)。
- [ ] **Step 2:Win32Embedder** —— 迁移现有 `NativeMethods` 逻辑(EnumWindows+标题、SetParent、WS_CHILD、MoveWindow)。
- [ ] **Step 3:X11Embedder** —— P/Invoke `libX11`:`XOpenDisplay`、`XQueryTree` 递归 + 读 `_NET_WM_PID`(`XGetWindowProperty`)与 `_NET_WM_NAME`/`WM_NAME` 按 PID+title 匹配;`XReparentWindow`+`XMapWindow`+`XMoveResizeWindow`;`Release` 用 `XReparentWindow` 回 root 或销毁随进程。
- [ ] **Step 4:MacEmbedder** —— 抛 `PlatformNotSupportedException`(占位,触发缩略图降级)。
- [ ] **Step 5:EmbedderFactory** 按 `OperatingSystem` 选择。
- [ ] **Step 6:** Win32 部分逻辑可对 `FindWindow` 做有限单测(纯匹配函数抽离);X11/mac 走真机。
- [ ] **Step 7:Commit** `feat(app): platform native window embedder abstraction (win/x11/mac)`

---

## Task 5:`ScrcpyHost` 平台无关化 + `NativeControlHost` 承载

**Files:** `src/DroidBus.App/Mirror/ScrcpyHost.cs`(改用 `INativeWindowEmbedder` + Avalonia host 句柄);Avalonia `Views/DeviceTile`(含 `NativeControlHost`)。

- [ ] **Step 1:** `ScrcpyHost` 去掉 `NativeMethods` 直依赖,改注入 `INativeWindowEmbedder` + host 原生句柄(来自 `NativeControlHost`)。保留:拉起 scrcpy 进程、轮询找窗(15s)、崩溃事件、Resize(经 embedder)。
- [ ] **Step 2:** `DeviceTile`(Avalonia `UserControl`)内嵌 `NativeControlHost`,`Attached`→拿句柄启动 `ScrcpyHost`,`Detached`/`SizeChanged`→Resize/Release。选中高亮、序列号/电量标签用 Avalonia 覆盖层。
- [ ] **Step 3:【真机验证】** 单台 scrcpy 嵌入一个格子,随窗口缩放铺满。
- [ ] **Step 4:Commit** `feat(app): host scrcpy window via NativeControlHost + embedder`

---

## Task 6:画面墙网格 + 选中/放大(Avalonia)

**Files:** `Views/DeviceGrid`、`MainWindow`,接 `DeviceManager` 轮询。

- [ ] **Step 1:** 3×2(可配)网格;`DeviceManager.PollLoopAsync` 事件 marshal 到 UI 线程(`Dispatcher.UIThread.Post`)。
- [ ] **Step 2:** 单击多选/全选高亮;双击放大为主控台(切换布局,host 句柄重建后重 reparent)。
- [ ] **Step 3:【真机验证】** 多台并发投屏 + 选中 + 放大。
- [ ] **Step 4:Commit** `feat(app): device wall grid with select/maximize`

---

## Task 7:缩略图轮询迁 SkiaSharp + 降级路径

**Files:** `src/DroidBus.App/Mirror/ThumbnailPoller.cs`(去 `System.Drawing`)。

- [ ] **Step 1:** 用 SkiaSharp 解码 PNG(`SKBitmap`)缩放,转 Avalonia `Bitmap` 显示。
- [ ] **Step 2:** 嵌入失败/ macOS 占位时,格子走 `ThumbnailPoller` 显示静态预览(降级,不崩)。
- [ ] **Step 3:【真机验证】** 主控模式缩略条刷新正常。
- [ ] **Step 4:Commit** `feat(app): SkiaSharp thumbnails + embed-failure fallback`

---

## Task 8:右侧控制栏 + 批量操作(Avalonia)

**Files:** `Views/ControlPanel`、`Views/BatchOps`,接 `BatchExecutor`/`AdbDeviceController`/`ScriptRunner`/`ImeCommands`。

- [ ] **Step 1:** 单台开关(录屏/触摸/常亮/音频/输入)与批量(装 APK/开应用/跑脚本/截图)。Core 逻辑全复用。
- [ ] **Step 2:【真机验证】** 各开关与一次批量任务 + 失败汇总。
- [ ] **Step 3:Commit** `feat(app): control panel + batch ops on Avalonia`

---

## Task 9:同步输入广播 + 宿主键盘(Avalonia)

**Files:** `Input/BroadcastOverlay` → Avalonia 覆盖层 + 指针事件;键盘走 Avalonia `TextInput`/`KeyDown`。

- [ ] **Step 1:** 主控台指针事件经 `Letterbox.Fit` 反算坐标,`BroadcastPlan.Targets` 多播(沿用 Core)。
- [ ] **Step 2:** 宿主键盘:Avalonia `TextInput`(合成后字符,含中文)→ `KeyTranslator.FromChar`;`KeyDown` 特殊键→`FromVirtualKey`(注意 Avalonia `Key` 枚举≠Win32 VK,需新映射表)→ 路由(广播/聚焦)。IME 准备沿用 `ImeCommands`。
- [ ] **Step 3:【真机验证】** 广播点击/滑动一致;宿主键盘中文输入到聚焦/广播设备。
- [ ] **Step 4:Commit** `feat(app): sync input broadcast + host keyboard on Avalonia`

---

## Task 10:二进制打包到输出 + Windows 回归

**Files:** `DroidBus.App.csproj`(按 RID 拷贝 `tools/<rid>/`);CI/构建说明。

- [ ] **Step 1:** `<Content Include="..\..\tools\$(RuntimeIdentifier)\**" CopyToOutputDirectory>`(或 RID 条件项)。`BinaryLocator` 优先用输出目录 `tools/<rid>/`。
- [ ] **Step 2:【真机验证 / Windows】** `dotnet run -r win-x64` 在 Windows 端回归:嵌入、缩放、广播不回退。
- [ ] **Step 3:Commit** `build: bundle per-RID tools to output; verify Windows regression`

---

## Task 11:文档与收尾

- [ ] 更新根 `README`:四平台构建/运行、`tools/<rid>/` 放置、`DISPLAY=:1` 运行说明、降级行为。
- [ ] macOS 占位说明(接口已就位,待实现 `MacEmbedder` + osx 二进制)。
- [ ] Commit `docs: cross-platform build/run guide`

---

## 里程碑
- **M0**:目录重整 + Core 可移植化 + 测试全绿(Task 0–2)。
- **M1(Ubuntu 跑通核心)**:Avalonia 壳 + X11 嵌入单台(Task 3–5)。
- **M2**:画面墙 + 控制栏 + 广播 + 键盘(Task 6–9)。
- **M3**:打包 + Windows 回归 + 文档(Task 10–11)。
- **延后**:macOS `MacEmbedder` 实现 + osx 真机联调。
