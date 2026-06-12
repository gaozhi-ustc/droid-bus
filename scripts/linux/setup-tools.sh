#!/usr/bin/env bash
#
# DroidBus — Linux (linux-x64) 工具链一键准备脚本
#
# 作用:把 scrcpy(2.x/3.x,含 scrcpy-server)、配套 adb、sndcpy.apk、Adbkeyboard.apk
#       下载并放进 tools/linux-x64/,供 BinaryLocator 解析(DROIDBUS_TOOLS → tools/<rid>/ → PATH)。
#
# 说明:
#   - 需要 root 的部分(apt 装 adb / udev 规则)默认【不】自动执行,只打印命令;
#     传 --with-apt 才会用 sudo 实际安装(会提示输入密码)。
#   - 其余下载/解压/放置全部免 sudo。
#   - 幂等:已存在的文件默认跳过,传 --force 重新下载。
#
# 用法:
#   scripts/linux/setup-tools.sh                # 仅免 sudo 部分(下载 scrcpy/apk 到 tools/linux-x64)
#   scripts/linux/setup-tools.sh --with-apt     # 顺带用 sudo apt 装 adb/fastboot/udev 规则
#   SCRCPY_VERSION=3.3.4 scripts/linux/setup-tools.sh --force
#
set -euo pipefail

SCRCPY_VERSION="${SCRCPY_VERSION:-3.3.4}"
SNDCPY_VERSION="${SNDCPY_VERSION:-1.1}"

WITH_APT=0
FORCE=0
for arg in "$@"; do
  case "$arg" in
    --with-apt) WITH_APT=1 ;;
    --force)    FORCE=1 ;;
    -h|--help)  grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "未知参数: $arg(--help 查看用法)" >&2; exit 2 ;;
  esac
done

# 仓库根 = 本脚本的 ../..
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_DIR="$REPO_ROOT/tools/linux-x64"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

log()  { printf '\033[1;36m[setup]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n'  "$*"; }
die()  { printf '\033[1;31m[fail]\033[0m %s\n'  "$*" >&2; exit 1; }

# ---- 0. 前置检查 -----------------------------------------------------------
ARCH="$(uname -m)"
[ "$ARCH" = "x86_64" ] || die "本脚本针对 linux-x64(x86_64),当前架构:$ARCH。其它架构请改 RID 与下载源。"
command -v curl >/dev/null || die "需要 curl。"
command -v tar  >/dev/null || die "需要 tar。"
command -v unzip >/dev/null || warn "未找到 unzip;sndcpy.apk 的解压会失败(apt install unzip)。"
mkdir -p "$TOOLS_DIR"

# ---- 1. adb / fastboot / udev(需 root)-----------------------------------
APT_CMD='sudo apt update && sudo apt install -y adb fastboot android-sdk-platform-tools-common
sudo usermod -aG plugdev "$USER"
sudo udevadm control --reload-rules && sudo udevadm trigger'

if command -v adb >/dev/null; then
  log "系统已有 adb:$(adb version | head -1)"
else
  if [ "$WITH_APT" = 1 ]; then
    log "用 apt 安装 adb / fastboot / udev 规则(需要 sudo 密码)…"
    sudo apt update && sudo apt install -y adb fastboot android-sdk-platform-tools-common
    sudo usermod -aG plugdev "$USER" || true
    sudo udevadm control --reload-rules && sudo udevadm trigger || true
    warn "已把你加入 plugdev 组:需【重新登录】或拔插设备后生效。"
  else
    warn "系统未装 adb。tools/linux-x64/ 里会放一个 scrcpy 自带的 adb 作兜底,"
    warn "但建议另外用 apt 装系统 adb + udev 规则(否则可能 'no permissions')。手动执行:"
    printf '\n%s\n\n' "$APT_CMD"
  fi
fi

# ---- 2. 下载工具(免 sudo)-------------------------------------------------
# $1=目标文件名(tools 下) $2=URL $3=校验用的 file(1) 关键字(可空)
download_to_tools() {
  local name="$1" url="$2"
  local dest="$TOOLS_DIR/$name"
  if [ -e "$dest" ] && [ "$FORCE" = 0 ]; then
    log "跳过已存在:$name(--force 可覆盖)"
    return 0
  fi
  log "下载 $name …"
  curl -fSL --retry 3 --max-time 300 -o "$dest" "$url" \
    || die "下载失败:$url"
}

# 2a. scrcpy 预编译包(含 scrcpy / scrcpy-server / adb)
SCRCPY_PKG="scrcpy-linux-x86_64-v${SCRCPY_VERSION}"
SCRCPY_URL="https://github.com/Genymobile/scrcpy/releases/download/v${SCRCPY_VERSION}/${SCRCPY_PKG}.tar.gz"
if { [ ! -x "$TOOLS_DIR/scrcpy" ] || [ ! -e "$TOOLS_DIR/scrcpy-server" ]; } || [ "$FORCE" = 1 ]; then
  log "下载 scrcpy ${SCRCPY_VERSION} 预编译包 …"
  curl -fSL --retry 3 --max-time 300 -o "$TMP_DIR/scrcpy.tgz" "$SCRCPY_URL" \
    || die "下载失败:$SCRCPY_URL(检查版本号 SCRCPY_VERSION 是否存在)"
  tar xzf "$TMP_DIR/scrcpy.tgz" -C "$TMP_DIR"
  cp "$TMP_DIR/$SCRCPY_PKG/scrcpy"        "$TOOLS_DIR/scrcpy"
  cp "$TMP_DIR/$SCRCPY_PKG/scrcpy-server" "$TOOLS_DIR/scrcpy-server"
  # 包内自带的 adb 与 server 严格配套,放进来作兜底
  [ -f "$TMP_DIR/$SCRCPY_PKG/adb" ] && cp "$TMP_DIR/$SCRCPY_PKG/adb" "$TOOLS_DIR/adb" && chmod +x "$TOOLS_DIR/adb"
  chmod +x "$TOOLS_DIR/scrcpy"
else
  log "跳过 scrcpy / scrcpy-server(已存在)"
fi

# 2b. sndcpy.apk(打包在 zip 内)
if [ ! -e "$TOOLS_DIR/sndcpy.apk" ] || [ "$FORCE" = 1 ]; then
  log "下载 sndcpy ${SNDCPY_VERSION} …"
  curl -fSL --retry 3 --max-time 120 -o "$TMP_DIR/sndcpy.zip" \
    "https://github.com/rom1v/sndcpy/releases/download/v${SNDCPY_VERSION}/sndcpy-v${SNDCPY_VERSION}.zip" \
    || die "下载 sndcpy 失败"
  unzip -o "$TMP_DIR/sndcpy.zip" -d "$TMP_DIR/sndcpy" >/dev/null
  find "$TMP_DIR/sndcpy" -iname 'sndcpy.apk' -exec cp {} "$TOOLS_DIR/sndcpy.apk" \;
else
  log "跳过 sndcpy.apk(已存在)"
fi

# 2c. Adbkeyboard.apk(中文/Unicode 输入;文件名按 BinaryLocator 期望大小写)
download_to_tools "Adbkeyboard.apk" \
  "https://github.com/senzhk/ADBKeyBoard/raw/master/ADBKeyboard.apk"

# ---- 3. 校验 ---------------------------------------------------------------
log "校验 scrcpy 运行与依赖 …"
if missing="$(ldd "$TOOLS_DIR/scrcpy" 2>&1 | grep -i 'not found' || true)"; [ -n "$missing" ]; then
  warn "scrcpy 有未满足的动态库依赖:"
  printf '%s\n' "$missing"
  warn "通常需要:sudo apt install -y ffmpeg libsdl2-2.0-0 libusb-1.0-0"
else
  "$TOOLS_DIR/scrcpy" --version 2>/dev/null | head -1 || warn "scrcpy --version 执行异常"
fi

echo
log "完成。tools/linux-x64/ 现有:"
ls -la "$TOOLS_DIR" | grep -vE '^total|README'
cat <<EOF

下一步:
  1) 接入并授权设备(复用 Windows 授权:把 Windows ~/.android/adbkey(+.pub) 拷到本机 ~/.android/,chmod 600 adbkey)
  2) adb kill-server && adb start-server && adb devices -l   # 期望 device(已授权)
  3) 运行 App(就绪后):DROIDBUS_TOOLS=$TOOLS_DIR DISPLAY=:1 dotnet run --project src/DroidBus.App
EOF
