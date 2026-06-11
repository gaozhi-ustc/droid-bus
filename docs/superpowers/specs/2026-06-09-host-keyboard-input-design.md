# DroidBus 宿主机键盘输入到手机 — 设计文档

- **日期**: 2026-06-09
- **状态**: 设计已确认,实现中
- **分支**: feat/droidbus-console

## 1. 背景与目标

当前群控台不支持用宿主机键盘输入到手机:既没有键盘广播,连被放大/聚焦的那台("第一个有焦点的手机")也不响应宿主键盘。本特性补上:**在宿主机敲键盘 → 实时输入到手机**,支持中文,贴合现有的触摸广播模型。

## 2. 已确认的决策

- **目标(发往哪里)**:与触摸广播一致 —— 广播开 → 所有在线可控设备;未广播但有主控聚焦 → 聚焦那台。复用 `BroadcastPlan.Targets`。
- **字符集**:中文 / Unicode,走 ADBKeyboard(`ADB_INPUT_TEXT` 广播);特殊键走 `input keyevent`。
- **激活方式**:跟广播/聚焦状态,无额外开关。

## 3. 关键设计点:捕获机制(纠正)

最初设想用低级键盘钩子 `WH_KEYBOARD_LL`,但它在 **IME 合成之前**拿到的是原始虚拟键码(拼音按键),拿不到合成后的汉字 —— **与中文需求冲突**。

因此捕获走**可获焦的捕获层(顶层 Form)**:它持有键盘焦点,IME 把汉字合成进它,`KeyPress`(`WM_CHAR`)交付合成后的字符(含中文),`KeyDown` 交付特殊键。它是顶层窗口、保持在最上并主动保持焦点,因此即便嵌入的 scrcpy 子窗会抢 Win32 焦点,我们仍能稳定捕获 —— 正好治"聚焦手机不响应键盘"。

## 4. 架构

### Core(纯逻辑,TDD)
- `KeyAction`:`TypeTextAction(string Text)` | `KeyEventAction(int AndroidKeyCode)`。
- `KeyTranslator`:
  - `FromChar(char)` → 可打印字符(含 IME 合成的中文)→ `TypeTextAction`;控制字符(`\r \b \t`)→ `null`(避免与特殊键重复触发)。
  - `FromVirtualKey(int vk)` → 特殊/导航/编辑键 → `KeyEventAction`;普通字符键 → `null`。vk 取值 = WinForms `Keys` 枚举值(= Win32 VK),故 Core 不依赖 WinForms、可单测。
  - v1 特殊键表:Enter→66, Backspace→67, Tab→61, Esc→111, ←↑→↓→21/19/22/20, Del→112, Home/End→122/123, PgUp/PgDn→92/93。

### App(WinForms + 互操作,真机验证)
- 捕获层(扩展 `BroadcastOverlay` 或同款可获焦 Form):`KeyPreview=true`,处理 `KeyPress`(→`FromChar`)与 `KeyDown`(→`FromVirtualKey`);激活时 `Focus()`/`Activate()` 并保持焦点。产出 `KeyInput` 事件。
- `MainForm.RouteKey(KeyAction)`:目标 = 广播态→`BroadcastPlan.Targets`,否则聚焦 serial;经 `BatchExecutor` fire-and-forget 调 `AdbDeviceController.TypeUnicodeAsync`(文本)/`KeyEventAsync`(特殊键),与现有导航键路由一致。
- IME 准备:键盘捕获激活时,对目标设备一次性、幂等地 `install Adbkeyboard.apk`(缺失时)→ `ime enable` → `ime set`(`ImeCommands` 已存在,此前未被调用)。特殊键不依赖 IME;仅文本路径需要。

## 5. 数据流

```
宿主键盘 → 捕获层(持焦,IME 合成)
  KeyPress(合成字符,含中文) → KeyTranslator.FromChar → TypeTextAction
  KeyDown(特殊键 vk)        → KeyTranslator.FromVirtualKey → KeyEventAction
        → MainForm.RouteKey → 目标设备集合(广播/聚焦)
            TypeTextAction  → AdbDeviceController.TypeUnicodeAsync(ADBKeyboard 广播)
            KeyEventAction  → AdbDeviceController.KeyEventAsync(input keyevent)
```

## 6. 错误处理
- 单台失败不影响其余(沿用 `BatchExecutor` 汇总)。
- IME 安装/设置失败 → 该台文本输入无效,但特殊键仍可用;记录不阻塞。

## 7. 测试策略
- Core `KeyTranslator`:xUnit + FluentAssertions 全覆盖(可打印/控制字符/中文/各特殊键映射)。
- App 捕获层 + 路由 + IME 设置:真机验证(WinForms 焦点、IME 合成、6 台广播)。

## 8. 范围外(v1)
- 每键延迟优化 / 合批(人手打字速率下 adb 路径可接受)。
- 退出时恢复设备原输入法。
- IME 合成预览高亮。
- 后续可把路由 sink 从 adb 换成 MaaTouch `t`/`k` 或 scrcpy 控制 socket(更低延迟),接口预留。
