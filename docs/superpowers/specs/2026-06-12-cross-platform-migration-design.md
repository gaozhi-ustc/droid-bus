# DroidBus 跨平台迁移(Windows / Ubuntu / macOS)— 设计文档

- **日期**: 2026-06-12
- **状态**: 设计待评审
- **目标平台 RID**: `win-x64`、`linux-x64`、`osx-x64`(Intel)、`osx-arm64`(Apple Silicon)
- **本期落地目标**: Ubuntu（`DISPLAY=:1`，X11）跑通投屏与群控;Windows 保持可用;macOS 仅搭好目录与接口占位,延后实现。

> 说明:本仓库 `docs/superpowers/` 是沿用原 Windows 机器上 superpowers 工作流的文档约定(spec → plan);当前 Ubuntu 环境并未安装 superpowers 插件,本文档与配套 plan 仅遵循同一约定撰写,便于延续。

## 1. 背景与现状

现有 DroidBus 是 Windows 专用的多设备群控台(详见 `2026-05-29-droidbus-multi-device-console-design.md`)。代码分两层:

- **`DroidBus.Core`**(`net8.0`):纯逻辑——设备发现/轮询、adb 命令构造、scrcpy 参数、批量执行、`.adb` 中文脚本引擎、MaaTouch、广播规划。**已基本可移植**,无 UI 依赖。
- **`DroidBus.App`**(`net8.0-windows`,WinForms):UI 与原生互操作。**强 Windows 绑定**。

### 阻碍跨平台的三处硬绑定(均在 App 层)
1. **WinForms**:`net8.0-windows` + `UseWindowsForms`,在 Linux/macOS 上 .NET 8 无法运行。
2. **Win32 P/Invoke(`user32.dll`)**:`Interop/NativeMethods.cs` 用 `EnumWindows`/`SetParent`/`MoveWindow`/`SetWindowLongPtr` 把 scrcpy 的 SDL 顶层窗口**重定父**嵌入网格格子(`Mirror/ScrcpyHost.cs`)。这是整个"画面墙"架构的核心机制,且是 Windows 独有。
3. **`System.Drawing` / GDI+**:`Mirror/ThumbnailPoller.cs` 用 `Bitmap`/`Graphics` 解码缩放 PNG 缩略图。`System.Drawing.Common` 自 .NET 7 起仅支持 Windows。

### Core 层的轻度耦合(易修)
- `BinaryLocator.cs`:硬编码 `C:\Program Files (x86)\Androidscreen\Resources` 与 `adb.exe`/`scrcpy.exe` 文件名。
- `tests/.../ProcessRunnerTests.cs`:用 `cmd.exe /c echo` 验证,Linux 无 `cmd.exe`。
- 其余(`ScreencapClient` 用 `exec-out`、`ScriptParser` 用 GBK via `System.Text.Encoding.CodePages`、`ScrcpyArgsBuilder` 用 `Path.Combine`)**均跨平台可用**。

## 2. 目标与非目标

### 目标
- 一套 .NET 代码库,在 win-x64 / linux-x64 / osx-x64 / osx-arm64 四个 RID 下可构建。
- UI 换成跨平台底座(**Avalonia UI**,见 §4),保留"画面墙 + 单台放大 + 右侧控制栏 + 同步广播"的交互。
- scrcpy 窗口嵌入抽象为 `INativeWindowEmbedder`,Windows/Linux 各自实现,macOS 占位。
- 二进制(adb/scrcpy/scrcpy-server/apk)按 `tools/<rid>/` 随仓打包,`BinaryLocator` 按运行时 RID 解析,缺失回退 PATH。
- 目录结构重整,使平台差异显式、可扩展(见 §6)。

### 非目标(本期)
- macOS 的实际窗口嵌入与真机联调(只搭结构与接口)。
- 第二桌面/虚拟副屏(Android 14+,与原项目一致不做)。
- Wayland 原生嵌入(本期假定 X11;Wayland 下走 XWayland 兼容,见 §5 风险)。
- 重写 Core 业务逻辑(只做可移植化微调)。

## 3. 平台能力矩阵

| 能力 | Windows (win-x64) | Ubuntu (linux-x64) | macOS (osx-x64/arm64) |
|---|---|---|---|
| .NET 8 运行时 | ✅ | ✅ | ✅ |
| UI 底座 Avalonia | ✅ | ✅(X11) | ✅(本期不验) |
| 进程编排(adb/scrcpy) | ✅ | ✅ | ✅ |
| scrcpy 窗口重定父嵌入 | `SetParent`(user32) | `XReparentWindow`(libX11) | NSView 嵌入(**占位**) |
| 缩略图 PNG 解码 | 换 SkiaSharp | 换 SkiaSharp | 换 SkiaSharp |
| 键盘捕获/IME | Avalonia `TextInput` | Avalonia `TextInput` | Avalonia `TextInput` |
| 二进制来源 | `tools/win-x64/` | `tools/linux-x64/` 或 PATH | `tools/osx-*/`(占位) |

## 4. UI 底座决策:Avalonia UI

**选 Avalonia 而非 GTK#/MAUI/Web**:
- 跨 Win/Linux(X11)/macOS(x64+arm64),与 .NET 8 一等公民集成,XAML + MVVM。
- 提供 **`NativeControlHost`**:在 Avalonia 视觉树里挖一块由原生窗口句柄承载的区域,正是嵌入 scrcpy 窗口所需;且能在其上叠加 Avalonia 覆盖层(选中高亮、标签、广播遮罩),规避原 WPF airspace 顾虑。
- `INativeControlHostDestroyableControlHandle` 在各平台给出原生句柄(Win32 HWND / X11 XID / macOS NSView),与我们的 reparent 抽象对接干净。

**代价**:App 层 UI 需从 WinForms 重写为 Avalonia(MainForm/grid/tile/控制栏/广播覆盖)。Core 不动,业务接线基本照搬。

## 5. 关键机制:scrcpy 窗口嵌入抽象

原 `ScrcpyHost.Embed()`(Win32 专用)抽象为接口,App 按平台选实现:

```
INativeWindowEmbedder
  IntPtr? FindWindow(int pid, string title)   // 按 PID+标题定位 scrcpy 顶层窗
  void    Embed(IntPtr child, IntPtr hostHandle) // 重定父为 host 子窗、去边框
  void    MoveResize(IntPtr child, int x, int y, int w, int h)
  void    Release(IntPtr child)
```

- **Win32Embedder**(win-x64):沿用现有 `NativeMethods` 逻辑(`EnumWindows`+标题匹配、`SetParent`、改 `WS_CHILD`、`MoveWindow`)。
- **X11Embedder**(linux-x64):P/Invoke `libX11`。
  - 找窗:遍历 `XQueryTree` 子窗 + 读 `_NET_WM_PID`(EWMH)与 `WM_NAME`/`_NET_WM_NAME` 匹配 scrcpy 进程与 `--window-title`。
  - 嵌入:`XReparentWindow(display, child, hostXid, 0, 0)` + `XMapWindow`;必要时清窗管装饰(scrcpy 已支持 `--window-borderless`)。
  - 缩放:`XMoveResizeWindow`。
  - `host` 句柄来自 Avalonia `NativeControlHost` 的 X11 窗口 XID。
- **MacEmbedder**(osx-*):**占位**——抛 `PlatformNotSupportedException` 并记录,UI 降级为"仅缩略图轮询"(`ScreencapClient` 已跨平台),保证 App 在 macOS 能启动、不崩。

`EmbedderFactory.Create()` 用 `OperatingSystem.IsWindows()/IsLinux()/IsMacOS()` 选择实现。

**DPI 说明**:原 Windows 端把宿主设为 `DpiUnaware` 让嵌入画面随面板缩放。Avalonia 用逻辑像素 + scaling;X11 无 per-monitor DPI 虚拟化问题,reparent 后 `XMoveResizeWindow` 直接生效。Windows 端在 Avalonia 下需用物理像素换算并显式禁用 DPI 虚拟化(实现期验证)。

## 6. 目录结构重整(本设计的硬性要求)

```
droid-bus/
├─ DroidBus.sln
├─ Directory.Build.props              # 共享属性 + RID 默认值
├─ global.json                        # 钉 .NET 8 SDK(可选)
├─ src/
│  ├─ DroidBus.Core/                  # net8.0 共享纯逻辑(RID 感知的 BinaryLocator)
│  └─ DroidBus.App/                   # net8.0 Avalonia 跨平台 UI(单工程)
│     ├─ App.axaml / MainWindow.axaml # Avalonia 视图
│     ├─ Views/  ViewModels/          # 画面墙 / 控制栏 / 广播
│     ├─ Mirror/ScrcpyHost.cs         # 用 INativeWindowEmbedder(平台无关)
│     └─ Interop/
│        ├─ INativeWindowEmbedder.cs
│        ├─ Windows/Win32Embedder.cs  # user32(仅 win 编译/运行)
│        ├─ Linux/X11Embedder.cs      # libX11(仅 linux 运行)
│        └─ MacOS/MacEmbedder.cs      # 占位
├─ tools/                             # 随仓打包的平台二进制(git-lfs/external,见下)
│  ├─ win-x64/    adb.exe scrcpy.exe scrcpy-server *.apk *.dll
│  ├─ linux-x64/  adb scrcpy scrcpy-server *.apk
│  ├─ osx-x64/    (占位 README:放 adb/scrcpy)
│  └─ osx-arm64/  (占位 README)
├─ scripts/
│  ├─ windows/    *.ps1               # 原 Check-UnlockWindow / Get-SimNumbers / _shot
│  └─ linux/      *.sh                # 后续等价脚本(占位)
├─ tests/
│  └─ DroidBus.Core.Tests/            # net8.0,去 cmd.exe 依赖
├─ assets/        inventory.csv inventory.json
└─ docs/superpowers/{specs,plans}/
```

**二进制管理**:`tools/<rid>/` 体积较大,默认 `.gitignore` 排除真正的二进制(保留每目录一个 `README.md` 说明放什么 + 版本),通过安装脚本/手工放置填充;`DroidBus.App.csproj` 用 `<None ... CopyToOutputDirectory>` 只拷贝当前 RID 子目录到 `bin/<rid>/tools/`。避免把上百 MB 二进制塞进 git。

## 7. Core 可移植化要点

- `BinaryLocator`:
  - 新增 `RuntimeRid`(由 `RuntimeInformation` 推断 win-x64/linux-x64/osx-x64/osx-arm64)。
  - 工具名按平台:Windows `adb.exe`/`scrcpy.exe`,其余 `adb`/`scrcpy`。
  - 解析顺序:`DROIDBUS_TOOLS` 环境变量 → `tools/<rid>/`(相对 AppContext.BaseDirectory) → PATH 查找(`adb`/`scrcpy` 在 `$PATH`)。
  - `scrcpy-server`:Linux 下 apt/brew 装的 scrcpy 自带,路径用 `SCRCPY_SERVER_PATH` 或包内 share 目录;打包模式放 `tools/<rid>/`。
- `ProcessRunnerTests`:改用跨平台命令——Windows 用 `cmd /c echo`,非 Windows 用 `/bin/sh -c 'echo hello'`(或直接测 `dotnet --version` 这类存在性);保持 stdout 断言。
- 缩略图解码迁出 `System.Drawing`:`ThumbnailPoller` 属 App 层,改用 **SkiaSharp**(跨平台)解码缩放 PNG → Avalonia `Bitmap`。

## 8. 错误处理与降级
- 嵌入失败(找不到窗/reparent 失败)→ 该格降级为 `ScreencapClient` 缩略图轮询 + 提示,不崩溃。
- macOS 上 `INativeWindowEmbedder` 占位实现直接走缩略图降级路径(天然验证降级链)。
- 工具缺失(`BinaryLocator` 抛 `FileNotFoundException`)→ 启动时友好提示安装命令(`apt install adb scrcpy` / 放置 `tools/<rid>/`)。
- adb server 异常、设备掉线/掉授权、scrcpy 崩溃自动重启:沿用 Core/`MirrorController` 既有逻辑(平台无关)。

## 9. 风险
- **X11 reparent 时序**:scrcpy 用 SDL 创建窗口,窗口出现到可 reparent 有竞态;需轮询 `_NET_WM_PID`+标题(类比现有 Windows 端 15s 轮询)。Wayland 会话下 scrcpy 若用原生 Wayland 后端则无 XID,需强制 SDL 走 X11(`SDL_VIDEODRIVER=x11`)或要求 XWayland。
- **Avalonia NativeControlHost 句柄稳定性**:格子显隐/网格↔放大切换时句柄重建,需在 `Attached/Detached` 钩子里重新 reparent。
- **Windows 回归**:从 WinForms 改 Avalonia 后,原 DPI-unaware 嵌入需在 Avalonia 重新验证不回退。
- **二进制版本漂移**:各平台 scrcpy 版本需与 scrcpy-server 匹配;`tools/<rid>/README` 记录版本。

## 10. 验收(本期 Ubuntu)
- `dotnet build` 在 linux-x64 成功;`dotnet test` 全绿(Core)。
- `DISPLAY=:1 dotnet run --project src/DroidBus.App`:启动出窗,至少 1 台已授权设备 scrcpy 画面嵌入网格格子并随面板缩放;单台放大、选中、同步广播点击可用。
- 工具缺失/设备掉线走降级提示而非崩溃。
