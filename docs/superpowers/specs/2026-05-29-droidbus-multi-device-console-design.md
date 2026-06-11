# DroidBus 群控台 — 设计文档

- **日期**: 2026-05-29
- **状态**: 已通过设计评审,待 spec 评审
- **工作目录**: `C:\Users\gaozhi\droid-bus`

## 1. 背景与目标

现有的「安卓投屏」(`C:\Program Files (x86)\Androidscreen\安卓投屏.exe`)是一个 .NET WinForms 程序,内核为开源 **scrcpy 2.x**(捆绑 `scrcpy.exe` + `scrcpy-server` + `adb.exe` + FFmpeg 6.0 + SDL2)。它的两个限制:

1. **一次只能管理 1 台设备**。
2. **大量功能被灰掉/锁定**(录屏、音频转发、文字输入、剪贴板同步、显示触摸、画面常亮、群控、脚本自动化等),疑似付费解锁。

**目标**:重做一个 Windows 多设备群控台,实现:

- 同屏监看多块设备的实时画面(画面墙)。
- 单台放大精细操控。
- 批量群控(同步输入广播、批量启动应用、批量装卸 APK / 推拉文件、批量跑脚本)。
- **原 app 所有灰掉/锁定的功能全部开放**。

目标硬件:用户的手机农场,**6 块 Galaxy Note9 主板(SM-N960U1,Android 10,sdm845)**,已全部完成 ADB 授权,经 USB Hub 机柜接入本 PC。设备序列号:

```
2620e8b738037ece  267063a5431c7ece  2771ac69ac1c7ece
28b3e9657a3f7ece  29299ad508047ece (512GB/8GB)  525659584b443498 (VZW)
```

## 2. 范围

### 在范围内
- 多设备实时投屏画面墙(布局 A)+ 单台放大主控台。
- 设备发现、状态轮询(在线/型号/电量)、掉线与掉授权检测重连。
- 原 app 灰掉功能的全开放实现(见 §6)。
- 群控四能力(见 §7)。
- 兼容原 `.adb` 中文脚本 DSL 的脚本引擎,支持多机并行执行。

### 不在范围内
- **第二桌面 / 虚拟副屏**:scrcpy 的「新建虚拟显示」需 Android 14+,目标板子是 Android 10,**明确不做**。
- 改 ROM / 解锁 Bootloader / root(板子 BL 锁死,与本项目无关)。
- 跨平台(仅 Windows)。

## 3. 技术栈

- **语言/框架**: C# / .NET 8 + **WinForms**。
- **为何选 WinForms 而非 WPF**: 方案 1(见 §4)需要把外部原生窗口(scrcpy 的 SDL 窗口)用 Win32 `SetParent` 嵌入到网格格子中。WinForms 以 `Panel.Handle` 作父句柄最直接,**规避 WPF 的 airspace 问题**(WPF 控件无法叠加在被托管的 HWND 之上,会让选中高亮、标签等覆盖层无法实现)。UI 用现代主题美化。
- **二进制复用**: 直接调用已安装于 `C:\Program Files (x86)\Androidscreen\Resources\` 的:
  - `scrcpy.exe`、`scrcpy-server`(渲染 + 控制协议)
  - `adb.exe`、FFmpeg(avcodec/avformat/avutil-60/58)、`SDL2.dll`
  - `sndcpy.apk`(音频转发,Android 10 必需)
  - `Adbkeyboard.apk`(中文/Unicode 输入)
  - 不重新打包;首启时定位这些路径,缺失时提示。

## 4. 架构(方案 1:编排 + 嵌入 scrcpy)

分 4 层:

```
UI 层(画面墙网格 / 右侧控制栏 / 顶部工具条)
        ▼
控制层 ControlBus(同步输入广播器 / 批量任务执行器 / 脚本引擎)
        ▼
设备层 DeviceManager(设备发现+状态轮询 / 投屏宿主 ScrcpyHost)
        ▼
复用二进制(scrcpy.exe / scrcpy-server / adb.exe / sndcpy.apk / ADBKeyBoard.apk)
```

### 组件职责
- **DeviceManager**: `adb devices` 发现设备;轮询型号/电量/在线;掉线、掉授权检测;重连(含 `adb kill-server`/`start-server` 恢复路径)。
- **ScrcpyHost**(每台一个实例): 以 `scrcpy.exe -s <serial> --window-borderless ...` 拉起进程,等待并抓取其顶层 HWND,`SetParent` 到目标 Panel,随格子大小 `MoveWindow` 缩放;进程崩溃自动重启。
- **ControlBus**: 见 §7。
- **UI**: 见 §5。

## 5. 界面布局(布局 A)

- **顶部工具条**: 全部投屏 / 刷新设备 / 连接无线设备 / 全局码率·分辨率。
- **主区**: 3×2 实时画面墙;每格嵌入一台 scrcpy 画面;单击选中(支持多选/全选,选中高亮),双击放大为主控台。
- **右侧控制栏**:
  - 上半:**批量操作面板** —— 同步输入广播开关、批量装 APK、批量开应用、跑脚本、批量截图/录屏。
  - 下半:**当前选中设备的单台开关** —— 录屏 / 显示触摸 / 转发音频 / 输入文字 / 常亮 等。

## 6. 「灰掉功能」实现映射(全部开放)

| 原灰掉功能 | 实现方式 |
|---|---|
| 启动时录屏 | scrcpy `--record=<file>.mp4` |
| 息屏启动 | scrcpy `--turn-screen-off` |
| 画面常亮不黑屏 | scrcpy `--stay-awake`(配合已设的 `stay_on_while_plugged_in`) |
| 显示触摸 | `adb shell settings put system show_touches 1`(关:置 0) |
| 同步剪贴板 | scrcpy 默认双向同步(PC↔设备) |
| 键盘输入文字(中文) | ADBKeyBoard.apk(Unicode/中文);纯 ASCII 走 scrcpy 注入 |
| 转发音频 | **sndcpy**(因 Android 10,scrcpy 原生音频需 A11+) |
| 群控 / 批量 / 脚本 | 见 §7 |

### Android 10 约束(已确认)
- scrcpy 原生音频转发需 Android 11+ → 音频走 **sndcpy**。
- scrcpy 新建虚拟显示(第二桌面)需 Android 14+ → **不做**(见 §2)。
- 其余功能在 Android 10 上全部可用。

## 7. 群控设计(四能力,全做)

1. **同步输入广播**: 在主控台(放大的那台)上的点击/滑动/打字事件,翻译为 scrcpy-server 控制协议消息,**多播**到所有选中设备的控制 socket;`adb shell input tap/swipe/text/keyevent` 作为兜底通道。UI 提供「广播开/关」与参与设备勾选。
2. **批量启动应用 / 跳转界面**: `adb -s <serial> shell am start ...` / monkey 按包名启动,并行下发。
3. **批量装卸 APK / 推拉文件**: `adb install/uninstall`、`adb push/pull`;并行执行 + 进度 + 结果回显。
4. **批量跑脚本**: 见 §8。

## 8. 脚本引擎(兼容原 `.adb` 中文 DSL)

原脚本为 GBK 编码、分号分隔的中文命令行。新引擎需解析并支持以下命令(并向后兼容已有脚本),且能在所有选中设备上**并行执行**:

| 命令 | 语义 | 落地 |
|---|---|---|
| `点击X Y` | 点击坐标 | `input tap X Y` |
| `长按X Y` | 长按 | `input swipe X Y X Y <时长>` |
| `滑动X1 Y1 X2 Y2` | 滑动 | `input swipe X1 Y1 X2 Y2` |
| `快速点击X Y` / `快速双击X Y` | 快/双击 | 连续 tap |
| `延时NS` / `随机延时` | 等待 | sleep / 随机区间 |
| `返回桌面` / `返回上层` | HOME / BACK | `keyevent KEYCODE_HOME` / `BACK` |
| `执行命令<cmd>` | 原始命令 | 直接执行(如 `adb shell input keyevent 24`) |
| `输入文本<t>` | IME 输入 | ADBKeyBoard 广播 |
| `ADB文本<t>` | adb 文本 | `input text <t>` |
| `启动应用<pkg>` | 按包名启动 | `am start` / monkey |

## 9. 错误处理

- 设备掉线/掉授权 → 对应格子置灰 + 提示,提供重连。
- scrcpy 进程崩溃 → 自动重启该格的 ScrcpyHost。
- 批量任务单台失败 → 不中断其余,结束后汇总失败设备列表。
- adb server 异常 → `adb kill-server` + `start-server` 恢复后重新握手(沿用项目已知的掉授权/重枚举处理经验)。

## 10. 测试策略

用 6 块真机(已授权)分阶段在真实设备上验证,不用 mock:

- 单台投屏 + 嵌入正确 → 6 路并发投屏稳定 → 逐项功能开关 → 群控广播一致性 → 批量任务与脚本并行正确性与失败汇总。

## 11. 里程碑

- **M1 地基**: 设备发现 + 单台 scrcpy 嵌入 + 网格布局。
- **M2 多路**: 6 路并发投屏 + 选中/放大 + 单台功能开关(录屏/触摸/常亮/音频/输入)。
- **M3 群控**: 同步输入广播 + 批量装 APK / 推文件 / 开应用。
- **M4 脚本**: `.adb` DSL 引擎 + 多机并行执行 + 错误汇总 + 打磨。
