<#
.SYNOPSIS
  Read each Samsung board's bootloader string (ro.bootloader) and security patch
  level, and decide whether it is still inside the afaneh92 token-unlock window.
  Read-only (adb getprop). No writes, no risk.

.DESCRIPTION
  Rule (per afaneh92/USACanadaSamsungBootloaderUnlock README):
    - unlock works only for bootloader binary V1-7;
    - security patch <= 2021-02   -> window OPEN, likely unlockable;
    - 2021-03 .. 2023-03          -> token bypassed at boot, likely NOT unlockable;
    - >= 2023-04                  -> permanently patched by Samsung, NOT unlockable.
  Note: Android 10 does NOT imply an early patch -- judge by the patch DATE.
  This script does not unlock or flash anything; it only reads the current state.

.EXAMPLE
  .\Check-UnlockWindow.ps1
#>

[CmdletBinding()]
param(
    [string]$Adb = $(
        if ($env:DROIDBUS_TOOLS) { Join-Path $env:DROIDBUS_TOOLS 'adb.exe' }
        elseif (Test-Path 'C:\Program Files (x86)\Androidscreen\Resources\adb.exe') {
            'C:\Program Files (x86)\Androidscreen\Resources\adb.exe'
        } else { 'adb' }
    )
)

function Get-Prop {
    param([string]$Serial, [string]$Name)
    (& $Adb -s $Serial shell getprop $Name 2>$null | Out-String).Trim()
}

# 1) Enumerate online devices (state = device only)
$serials = & $Adb devices |
    Select-Object -Skip 1 |
    Where-Object { $_ -match '\sdevice$' } |
    ForEach-Object { ($_ -split '\s+')[0] }

if (-not $serials) {
    Write-Warning 'No online devices. Check the USB hub / adb authorization, or run `adb devices`.'
    return
}

# 2) Read values + decide per board
$rows = foreach ($s in $serials) {
    $model = Get-Prop $s 'ro.product.model'
    $bl    = Get-Prop $s 'ro.bootloader'
    $patch = Get-Prop $s 'ro.build.version.security_patch'
    $rel   = Get-Prop $s 'ro.build.version.release'

    # Bootloader binary version digit: the digit after the 3-letter region code
    # (e.g. N960U1UEU5DUI1 -> 5)
    $blVer = if ($bl -match '[A-Z]{3}(\d)') { [int]$Matches[1] } else { $null }

    # Verdict driven primarily by patch date
    $verdict = 'UNKNOWN (no patch)'
    if ($patch -match '^\d{4}-\d{2}-\d{2}$') {
        $d = [datetime]$patch
        if     ($d -lt [datetime]'2021-03-01') { $verdict = 'OPEN (unlockable)' }
        elseif ($d -lt [datetime]'2023-04-01') { $verdict = 'BYPASSED (likely no)' }
        else                                   { $verdict = 'PATCHED (no)' }
    }
    # Bootloader binary > 7 is out regardless
    if ($null -ne $blVer -and $blVer -gt 7) { $verdict = "BL V$blVer>7 (no)" }

    [pscustomobject]@{
        Serial     = $s
        Model      = $model
        Android    = $rel
        Bootloader = $bl
        BL_Ver     = $blVer
        Patch      = $patch
        Verdict    = $verdict
    }
}

$rows | Format-Table -AutoSize

# 3) Summary
$open = @($rows | Where-Object { $_.Verdict -like 'OPEN*' }).Count
Write-Host ''
Write-Host ("Window OPEN (unlockable): {0} / {1} boards." -f $open, $rows.Count) -ForegroundColor Cyan
Write-Host 'WARNING: do NOT OTA/upgrade any board you intend to unlock -- one update closes the window permanently and cannot be downgraded.' -ForegroundColor Yellow
