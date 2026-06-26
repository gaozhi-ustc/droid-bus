# Xiaomi Root Handoff

Date: 2026-06-26

## Device

- Model: `M2104K10AC`
- Codename: `chopin`
- Android: `11`
- MIUI: `V12.5.8.0.RKPCNXM`
- Build fingerprint: `Redmi/chopin/chopin:11/RP1A.200720.011/V12.5.8.0.RKPCNXM:user/release-keys`
- Active slot during preparation: `_a`
- Bootloader lock state: `ro.boot.flash.locked=0` (already unlocked)
- Verified boot state observed in system: `orange`
- Existing root before work: none (`su` not found, `magisk` not found)

## What Was Done

1. Confirmed `adb` connectivity and collected device properties from the phone in Android system mode.
2. Confirmed the exact stock ROM/boot version and matched it to a local stock `boot.img`.
3. Found local Magisk payload resources and generated a patched boot image offline on-device in `/data/local/tmp`.
4. Pulled the patched image back to this machine and verified it is a valid Android boot image.
5. Did not flash anything yet.

## Important Paths

- Patched boot image to flash:
  `/tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/chopin/magisk_patched_chopin_V12.5.8.0_RKPCNXM_boot_a.img`
- Stock boot image for rollback:
  `/tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/chopin/boot.img`
- Stock ROM image directory used for matching:
  `/tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/chopin/chopin_images_V12.5.8.0.RKPCNXM_20210823.0000.00_11.0_cn/images`
- Local Magisk payload used for patching:
  `/tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/magisk-official/payload`
- Temporary working dir created on phone:
  `/data/local/tmp/magisk-root-20260626`

## Hashes

- Stock `boot.img` sha256:
  `cd7de20a4b417e9fea1e0337a6da1df5e02dde5814dc2c78ee2f7c5ecd31ff19`
- Patched `magisk_patched_chopin_V12.5.8.0_RKPCNXM_boot_a.img` sha256:
  `b4c7729313614eba575558572a87e5fc3a9a14f28a1a98a88f16758e753632ee`

## How The Patched Image Was Generated

- Used Magisk `30.7` payload files from the local `magisk-official/payload` directory.
- Because the available `magiskboot` binary was `aarch64`, patching was done on the phone instead of on the Linux host.
- Files pushed to the phone included:
  `boot_patch.sh`, `util_functions.sh`, `busybox`, `magiskinit`, `magisk`, `magiskboot`, `init-ld`, `stub.apk`, plus the matched stock `boot.img`.
- Patch command that succeeded on the phone:

```sh
adb -s xoaiaypr49nn99vs shell 'cd /data/local/tmp/magisk-root-20260626/payload && chmod 755 * && ./busybox sh ./boot_patch.sh ../boot.img'
```

- Resulting image on phone:
  `/data/local/tmp/magisk-root-20260626/payload/new-boot.img`

## Current Blocker

- `adb` mode worked.
- `fastboot` mode did not work on this computer.
- On every attempt, the phone entered `FASTBOOT` on screen, but the Linux host failed USB enumeration before `fastboot devices` could see it.
- `lsusb` showed no Xiaomi device in `FASTBOOT`.
- Kernel logs repeatedly showed:
  - `device descriptor read/64, error -71`
  - `Device not responding to setup address`
  - `unable to enumerate USB device`
- The problem persisted across at least two physical USB ports.
- Therefore no flash was attempted from this machine.

## Next Step On Another Computer

1. Keep the phone in `FASTBOOT`.
2. Copy or access the patched image path above.
3. Verify the device is visible:

```sh
fastboot devices
```

4. Flash the patched image to the prepared active slot:

```sh
fastboot flash boot_a /tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/chopin/magisk_patched_chopin_V12.5.8.0_RKPCNXM_boot_a.img
fastboot reboot
```

5. After booting Android, install/open Magisk app if needed and verify root:

```sh
adb shell su -c id
```

## Rollback

If the patched boot does not boot correctly, flash stock boot back to the same slot:

```sh
fastboot flash boot_a /tmp/claude-1000/-home-gaozhi-git-projects-droid-bus/af7deee7-aa65-4329-afcc-d9ed2d5e6b3c/scratchpad/chopin/boot.img
fastboot reboot
```
