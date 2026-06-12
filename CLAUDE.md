# CLAUDE.md — DroidBus 开发者指南

> 多设备安卓群控台:同屏监看多台设备、单台放大精控、批量群控(同步输入广播 / 批量装卸 APK / 推拉文件 / 批量跑脚本)、`.adb` 中文脚本引擎。内核复用开源 **scrcpy 2.x/3.x** + **adb**。
>
> **当前状态(2026-06):正在从 Windows 专用迁移为跨平台。** 主开发环境已切到 **Ubuntu(`DISPLAY=:1`,X11)**。`DroidBus.Core` 已可移植化并验证;`DroidBus.App` 的 UI 将从 WinForms 重写为 **Avalonia**(进行中)。

---

## 1. 快速上手(Ubuntu)

```bash
# 1) .NET 8 SDK(用户级,免 sudo)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"   # 建议写进 ~/.bashrc

# 2) 安卓工具链(scrcpy/adb/apk → tools/linux-x64/)
#    免 sudo 部分脚本自动做;adb/udev 的 sudo 部分见脚本提示或加 --with-apt
scripts/linux/setup-tools.sh            # 或:scripts/linux/setup-tools.sh --with-apt

# 3) 编译 + 测试 Core(App 暂为 net8.0-windows,Linux 上先不编 —— 见 §6)
dotnet build src/DroidBus.Core/DroidBus.Core.csproj
dotnet test  tests/DroidBus.Core.Tests/DroidBus.Core.Tests.csproj      # 期望 94/94 通过

# 4) 接入并授权设备(见 §7),然后(Avalonia App 就绪后):
DROIDBUS_TOOLS="$PWD/tools/linux-x64" DISPLAY=:1 dotnet run --project src/DroidBus.App
```

> **注意**:这台机器上 `dotnet`/`adb`/`scrcpy` 不在系统 PATH 默认位置 —— `dotnet` 在 `~/.dotnet`,`scrcpy` 在 `tools/linux-x64/`。每个新 shell 先 `export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH`。

---

## 2. 仓库结构

```
droid-bus/
├─ CLAUDE.md                       # 本文件
├─ Directory.Build.props           # 四 RID 共享 MSBuild 属性
├─ DroidBus.sln
├─ src/
│  ├─ DroidBus.Core/               # net8.0,纯逻辑,跨平台。无 UI 依赖,全 TDD。
│  └─ DroidBus.App/                # ⚠ 目前仍是 net8.0-windows WinForms;待重写为 Avalonia
├─ tests/DroidBus.Core.Tests/      # net8.0,xUnit + FluentAssertions(94 用例)
├─ tools/                          # 平台二进制,按 RID 分目录(不进 git,见 §5)
│  ├─ win-x64/  linux-x64/  osx-x64/  osx-arm64/
│  └─ README.md                    # 每个目录放什么 + 版本
├─ scripts/
│  ├─ linux/setup-tools.sh         # Linux 工具链一键准备
│  └─ windows/*.ps1                # 原 Windows 辅助脚本
├─ assets/                         # inventory.csv / inventory.json(设备清单)
└─ docs/superpowers/
   ├─ specs/                       # 设计文档(spec)
   └─ plans/                       # 实施计划(逐任务 TDD)
```

---

## 3. 架构

四层(详见 `docs/superpowers/specs/2026-05-29-droidbus-multi-device-console-design.md`):

```
UI(画面墙网格 / 右侧控制栏 / 顶部工具条)            ← DroidBus.App
   ▼
控制层(同步输入广播 / 批量任务执行 / 脚本引擎)       ← DroidBus.Core
   ▼
设备层(DeviceManager 发现+轮询 / ScrcpyHost 投屏宿主)
   ▼
复用二进制(scrcpy / scrcpy-server / adb / *.apk)    ← tools/<rid>/
```

### DroidBus.Core(跨平台,纯逻辑)
- `Models/Device`、`Devices/DeviceManager`(发现/轮询/掉线检测)
- `Adb/AdbClient`(解析 `adb devices -l`)、`Adb/AdbCommands`(参数构造器)、`Adb/ScreencapClient`(`exec-out screencap` 抓 PNG)
- `Mirror/ScrcpyArgsBuilder` + `MirrorOptions`(MirrorOptions → scrcpy 参数;**用 2.x 参数** `--video-bit-rate`/`--show-touches`/`--render-driver`)
- `Control/*`:`IDeviceController`/`AdbDeviceController`、`SyncInputTranslator`、`Letterbox`(坐标反算)、`MaaTouch*`、`BroadcastPlan`、`KeyTranslator`
- `Script/*`:`.adb` 中文 DSL(GBK 编码,经 `System.Text.Encoding.CodePages`)解析 + 执行
- `Batch/BatchExecutor`(并行 + 失败汇总)、`Process/IProcessRunner`(进程抽象,便于单测 fake)、`BinaryLocator`(见 §5)

### DroidBus.App(平台相关,迁移中)
- **原 Windows 实现(WinForms + Win32)**:用 `user32.dll` 的 `SetParent`/`MoveWindow` 把每台 scrcpy 的 SDL 顶层窗口**重定父**嵌入网格格子(`Mirror/ScrcpyHost.cs` + `Interop/NativeMethods.cs`);缩略图用 `System.Drawing`/GDI+。**这三处都是 Windows 独有**,是迁移的核心难点。
- **迁移目标(Avalonia)**:UI 换 Avalonia 11;用 `NativeControlHost` 承载原生句柄;嵌入抽象为 `INativeWindowEmbedder`,各平台实现:
  - Windows:`SetParent`(user32)
  - Linux:`XReparentWindow`(libX11)— 适配本机 `DISPLAY=:1`
  - macOS:占位(`PlatformNotSupportedException`),降级为 `ScreencapClient` 缩略图轮询
  - 缩略图解码迁 **SkiaSharp**(跨平台,替换 System.Drawing)
  - 详见 `docs/superpowers/specs/2026-06-12-cross-platform-migration-design.md`

---

## 4. 平台支持矩阵

| RID | 平台 | UI | scrcpy 窗口嵌入 | 状态 |
|---|---|---|---|---|
| `win-x64` | Windows | Avalonia(原 WinForms) | `SetParent` | 待 Avalonia 重写后回归 |
| `linux-x64` | Ubuntu/X11 | Avalonia | `XReparentWindow` | **本期落地目标** |
| `osx-x64` | macOS Intel | Avalonia | 占位 | 仅搭目录结构 |
| `osx-arm64` | macOS Apple Silicon | Avalonia | 占位 | 仅搭目录结构 |

---

## 5. 二进制与 `BinaryLocator`

平台二进制按 RID 放 `tools/<rid>/`(`adb`/`scrcpy`/`scrcpy-server`/`sndcpy.apk`/`Adbkeyboard.apk`)。**二进制不进 git**(`.gitignore` 排除,只留每目录 README);用 `scripts/linux/setup-tools.sh` 或手工放置。

`BinaryLocator.Discover()` 解析顺序:
1. 环境变量 `DROIDBUS_TOOLS`
2. `tools/<rid>/`(相对程序输出目录,向上探查若干层 → 兼容 `dotnet run`)
3. 系统 `PATH`(`adb`/`scrcpy`)
4. (仅 Windows)原「安卓投屏」`C:\Program Files (x86)\Androidscreen\Resources`

工具名自动按平台:Windows `adb.exe`,其余 `adb`。`scrcpy-server` 额外搜 `SCRCPY_SERVER_PATH` 与 `/usr/share/scrcpy/` 等。apk 缺失不抛异常(对应能力降级)。`BinaryLocator.Rid` 归一为四 RID 之一。

---

## 6. 构建 / 测试 / 运行

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"

# Core(跨平台)
dotnet build src/DroidBus.Core/DroidBus.Core.csproj
dotnet test  tests/DroidBus.Core.Tests/DroidBus.Core.Tests.csproj

# ⚠ 整解 `dotnet build DroidBus.sln` 在 Linux 上会失败 ——
#   DroidBus.App 仍是 net8.0-windows(WinForms),Linux 不支持。这是预期,待 Avalonia 重写后解除。
#   在那之前,Linux 上只构建 Core + Tests。
```

测试约定:`IProcessRunner` 可注入 fake;测试已去 Windows 依赖(无 `cmd.exe`/`powershell`/`C:\` 硬编码),跨平台运行。

---

## 7. 复用 Windows 上的 ADB 授权(关键)

USB 调试授权绑的是**主机的 ADB 密钥**(设备里存了主机公钥),不绑物理机器。把 Windows 的密钥拷到 Ubuntu,即可零点击复用已有授权:

```bash
# Windows: C:\Users\<user>\.android\adbkey 与 adbkey.pub  →  拷到 Ubuntu:
mkdir -p ~/.android && cp /path/to/adbkey /path/to/adbkey.pub ~/.android/
chmod 600 ~/.android/adbkey && chmod 644 ~/.android/adbkey.pub
adb kill-server && adb start-server && adb devices -l     # 期望 device(已授权)
```

排错:`no permissions` → udev 规则/`plugdev` 组未生效(重新登录或拔插);`unauthorized` → 密钥没拷对,或在设备屏上重新点「允许」。

---

## 8. 迁移进度(2026-06-12)

**已完成并验证(M0):**
- ✅ 跨平台目录重整(`tools/<rid>/`、`scripts/<os>/`、`assets/`、`Directory.Build.props`)
- ✅ `DroidBus.Core` 可移植化:RID 感知的 `BinaryLocator`(env → tools → PATH);测试去 Windows 依赖
- ✅ Ubuntu 上 `dotnet test` **94/94 全绿**;`.NET 8.0.422` SDK(`~/.dotnet`)
- ✅ Linux 工具链就位:scrcpy 3.3.4 + scrcpy-server + adb + sndcpy.apk + Adbkeyboard.apk(`tools/linux-x64/`),`BinaryLocator.Discover()` 实测全部解析

**进行中 / 待办(见 `docs/superpowers/plans/2026-06-12-cross-platform-migration.md`):**
- ⬜ `DroidBus.App` → Avalonia(`net8.0`,去 WinForms)
- ⬜ `INativeWindowEmbedder`:Win32 / X11 / macOS 占位;`ScrcpyHost` 平台无关化 + `NativeControlHost` 承载
- ⬜ 画面墙网格 / 控制栏 / 同步广播 / 宿主键盘 的 Avalonia 端口
- ⬜ 缩略图迁 SkiaSharp;二进制按 RID 拷到输出;Windows 回归;macOS 实现 `MacEmbedder`

**参考文档:**
- 设计:`docs/superpowers/specs/2026-06-12-cross-platform-migration-design.md`
- 计划:`docs/superpowers/plans/2026-06-12-cross-platform-migration.md`(逐任务 TDD 步骤)
- 原始设计/计划:`docs/superpowers/{specs,plans}/2026-05-29-*` 与 `2026-06-09-host-keyboard-input-design.md`

---

## 9. 约定与注意

- **TDD**:Core 改动先写失败测试(xUnit + FluentAssertions),所有 adb/scrcpy 调用走 `IProcessRunner` 便于断言。
- **scrcpy 必须 2.x/3.x**:Ubuntu apt 的 `scrcpy` 是 1.21,参数不兼容,**不要用**;用 `setup-tools.sh` 装的官方预编译。
- **目标设备**:6 块 Galaxy Note9(SM-N960U1,Android 10)。Android 10 约束:音频走 sndcpy(scrcpy 原生音频需 A11+);不做虚拟副屏(需 A14+)。
- **X11 vs Wayland**:嵌入走 X11(`XReparentWindow`)。Wayland 会话下需让 scrcpy 走 XWayland 或 `SDL_VIDEODRIVER=x11`。
- **环境变量**:运行 App 用 `DROIDBUS_TOOLS` 指向 `tools/linux-x64`、`DISPLAY=:1`。
