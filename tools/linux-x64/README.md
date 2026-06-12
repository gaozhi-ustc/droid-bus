# tools/linux-x64/

放置 Ubuntu/Linux x64 二进制(或留空,依赖系统 `PATH`):

- `adb`            — `apt install adb` 或 platform-tools
- `scrcpy`         — `apt install scrcpy`(2.x)
- `scrcpy-server`  — 随 scrcpy 提供,通常在 `/usr/share/scrcpy/scrcpy-server`
- `sndcpy.apk`     — 音频转发(Android 10 需要)
- `Adbkeyboard.apk`— 中文/Unicode 输入

一键准备:`scripts/linux/setup-tools.sh`(免 sudo 部分自动下载到此目录)。

当前放置版本(2026-06-12):scrcpy `3.3.4`(Genymobile 官方 Linux x86_64 预编译,含 scrcpy-server 与配套 adb),adb `1.0.41`,sndcpy `v1.1`,ADBKeyBoard(master)。
