# tools/ — 平台二进制(按 RID 分目录)

DroidBus 运行时按当前 RID 在 `tools/<rid>/` 下查找 `adb` / `scrcpy` / `scrcpy-server` 及所需 apk。
`BinaryLocator` 解析顺序:环境变量 `DROIDBUS_TOOLS` → `tools/<rid>/`(相对程序输出目录)→ 系统 `PATH`。

支持的 RID 子目录:

| RID | 平台 | 二进制 |
|---|---|---|
| `win-x64` | Windows x64 | `adb.exe` `scrcpy.exe` `scrcpy-server` + FFmpeg/SDL2 dll + `sndcpy.apk` `Adbkeyboard.apk` |
| `linux-x64` | Ubuntu/Linux x64 | `adb` `scrcpy` `scrcpy-server` + `sndcpy.apk` `Adbkeyboard.apk` |
| `osx-x64` | macOS Intel | `adb` `scrcpy` `scrcpy-server` + apk(**占位,待实现**) |
| `osx-arm64` | macOS Apple Silicon | 同上(**占位,待实现**) |

## 二进制不进 git

实际可执行文件/库被 `.gitignore` 排除(体积大、平台相关)。每个子目录保留 `README.md` 记录**应放什么 + 版本**。

- **Ubuntu**:`sudo apt install adb scrcpy` 后,可直接依赖系统 PATH(无需填充 `tools/linux-x64/`);
  或把 `$(which adb)` / `$(which scrcpy)` / `/usr/share/scrcpy/scrcpy-server` 拷进来固定版本。
- **Windows**:从原 `C:\Program Files (x86)\Androidscreen\Resources\` 拷贝整套到 `tools/win-x64/`。
- **macOS**:`brew install android-platform-tools scrcpy` 或手工放置(本期不验证)。
