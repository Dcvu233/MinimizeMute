$ErrorActionPreference = "Stop"

$appName = "MinimizeMute"
$sourceDir = $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$appName"
$exePath = Join-Path $installDir "$appName.exe"

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $installDir -Recurse -Force

New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenuDir "$appName.lnk"))
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $exePath
$shortcut.Save()

$uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "Uninstall $appName.lnk"))
$uninstallShortcut.TargetPath = "powershell.exe"
$uninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
$uninstallShortcut.WorkingDirectory = $installDir
$uninstallShortcut.Save()

Start-Process -FilePath $exePath
