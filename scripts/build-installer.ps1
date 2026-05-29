param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $repoRoot "dist"
$publishDir = Join-Path $distDir "publish"
$stagingDir = Join-Path $distDir "installer-staging"
$setupPath = Join-Path $distDir "MinimizeMuteSetup.exe"
$archivePath = Join-Path $distDir "MinimizeMuteSetup.7z"
$configPath = Join-Path $distDir "sfx-config.txt"

& (Join-Path $PSScriptRoot "build-release.ps1") -Configuration $Configuration -Runtime $Runtime

if (Test-Path $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $stagingDir -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot "installer\install.cmd") -Destination $stagingDir -Force
Copy-Item -Path (Join-Path $repoRoot "installer\install.ps1") -Destination $stagingDir -Force
Copy-Item -Path (Join-Path $repoRoot "installer\uninstall.ps1") -Destination $stagingDir -Force

Remove-Item -LiteralPath $setupPath, $archivePath, $configPath -Force -ErrorAction SilentlyContinue

$sevenZip = Get-Command 7z.exe -ErrorAction SilentlyContinue
if (-not $sevenZip) {
    $sevenZip = Get-Command 7za.exe -ErrorAction SilentlyContinue
}

$sevenZipPath = if ($sevenZip) { $sevenZip.Source } else { "" }
if (-not $sevenZipPath) {
    foreach ($candidate in @(
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe"
    )) {
        if (Test-Path $candidate) {
            $sevenZipPath = $candidate
            break
        }
    }
}

if (-not $sevenZipPath) {
    throw "7-Zip was not found. Install 7-Zip and make sure 7z.exe is available in PATH."
}

$sfxModule = Join-Path (Split-Path $sevenZipPath) "7z.sfx"
if (-not (Test-Path $sfxModule)) {
    throw "7z.sfx was not found next to $sevenZipPath. Install the full 7-Zip package."
}

& $sevenZipPath a -t7z -mx=9 $archivePath (Join-Path $stagingDir "*") | Out-Host
if (-not (Test-Path $archivePath)) {
    throw "Archive was not created: $archivePath"
}

@"
;!@Install@!UTF-8!
Title="MinimizeMute Setup"
BeginPrompt="Install MinimizeMute?"
RunProgram="install.cmd"
;!@InstallEnd@!
"@ | Set-Content -Path $configPath -Encoding UTF8

$output = [System.IO.File]::Open($setupPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
try {
    foreach ($part in @($sfxModule, $configPath, $archivePath)) {
        $input = [System.IO.File]::OpenRead($part)
        try {
            $input.CopyTo($output)
        }
        finally {
            $input.Dispose()
        }
    }
}
finally {
    $output.Dispose()
}

Remove-Item -LiteralPath $archivePath, $configPath -Force -ErrorAction SilentlyContinue

Write-Host "Installer created at $setupPath"
