param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,

    [string]$PackageName = "com.PresentFutures.UnityXRAIFlorence",
    [string]$ActivityName = "com.unity3d.player.UnityPlayerActivity",

    [string]$ScreenshotPath = "quest_screenshot.png",
    [switch]$SkipInstall
)

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing required command: $name"
    }
}

Require-Command adb

$deviceCount = (adb devices | Select-String "\tdevice$").Count
if ($deviceCount -lt 1) {
    throw "No adb devices detected. Connect Quest and enable USB debugging."
}

if (-not $SkipInstall) {
    if (-not (Test-Path $ApkPath)) {
        throw "APK not found: $ApkPath"
    }
    Write-Host "Installing APK..."
    adb install -r $ApkPath
}

Write-Host "Launching app..."
adb shell am start -n "$PackageName/$ActivityName"
Start-Sleep -Seconds 8

Write-Host "Capturing screenshot..."
adb exec-out screencap -p > $ScreenshotPath
Write-Host "Saved screenshot to $ScreenshotPath"
