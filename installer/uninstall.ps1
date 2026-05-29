$ErrorActionPreference = "SilentlyContinue"

$appName = "MinimizeMute"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$appName"

Get-Process -Name $appName | Stop-Process -Force
Remove-Item -LiteralPath $startMenuDir -Recurse -Force

$cleanupScript = Join-Path $env:TEMP "$appName-cleanup.ps1"
@"
Start-Sleep -Seconds 1
Remove-Item -LiteralPath '$installDir' -Recurse -Force
Remove-Item -LiteralPath '$cleanupScript' -Force
"@ | Set-Content -Path $cleanupScript -Encoding UTF8

Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$cleanupScript`"" -WindowStyle Hidden
