<#
.SYNOPSIS
  Dump per-device SIM info for all connected phones: operator / phone number / ICCID / IMSI.
  Read-only via adb. Re-run after swapping SIM cards.

.DESCRIPTION
  Uses iphonesubinfo service-call transaction codes verified on these Galaxy Note9 /
  Android 10 devices:
    code 19 = getLine1Number   (phone number; empty if the carrier didn't write it to the SIM)
    code 12 = getIccSerialNumber (ICCID, the card serial)
    code  7 = getSubscriberId   (IMSI)
  These codes can differ on other Android versions / vendors.

  Number empty but ICCID present => SIM inserted but number not stored on card
  (common for CN carriers); use the ICCID with carrier support to look up the number.

.EXAMPLE
  .\Get-SimNumbers.ps1
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

# Call iphonesubinfo and decode the returned Parcel (UTF-16) into its string value.
function Get-SubInfo {
    param([string]$Serial, [int]$Code)
    $out = & $Adb -s $Serial shell service call iphonesubinfo $Code s16 com.android.shell 2>$null
    $ascii = ''
    foreach ($line in $out) { if ($line -match "'(.*)'") { $ascii += $Matches[1] } }
    ($ascii -replace "[^0-9A-Za-z+]", '')
}

# Enumerate online devices (state = device)
$serials = & $Adb devices |
    Select-Object -Skip 1 |
    Where-Object { $_ -match '\sdevice$' } |
    ForEach-Object { ($_ -split '\s+')[0] }

if (-not $serials) {
    Write-Warning 'No online devices. Check the USB hub / adb authorization, or run `adb devices`.'
    return
}

$rows = foreach ($s in $serials) {
    $simState = (& $Adb -s $s shell getprop gsm.sim.state 2>$null).Trim()
    $op       = (& $Adb -s $s shell getprop gsm.sim.operator.alpha 2>$null).Trim()

    # Only read subscriber info when a card is actually present; otherwise the modem
    # can return stale cached values (e.g. IMSI of a just-removed card).
    if ($simState -eq 'LOADED' -or $simState -eq 'READY') {
        $number = Get-SubInfo $s 19
        $iccid  = Get-SubInfo $s 12
        $imsi   = Get-SubInfo $s 7
    } else {
        $number = ''; $iccid = ''; $imsi = ''
    }

    if (-not $simState) { $simState = 'ABSENT' }
    if (-not $op)       { $op = '-' }
    if ($number) { $number = ($number -replace '^\+?86', '') } else { $number = '-' }
    if (-not $iccid)    { $iccid = '-' }
    if (-not $imsi)     { $imsi = '-' }

    [pscustomobject]@{
        Serial   = $s
        SimState = $simState
        Operator = $op
        Number   = $number
        ICCID    = $iccid
        IMSI     = $imsi
    }
}

$rows | Format-Table -AutoSize
