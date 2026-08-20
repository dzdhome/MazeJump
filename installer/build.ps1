# Build script: publish Release (win-x64, self-contained) then package with Inno Setup.
# Usage: powershell -ExecutionPolicy Bypass -File .\installer\build.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "==> Step 1/2: dotnet publish (win-x64, self-contained)" -ForegroundColor Cyan
dotnet publish MazeJump.csproj -c Release -r win-x64 --self-contained true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$root\publish\win-x64"

$iscc = "C:\Users\PC5\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $iscc)) {
    throw "ISCC.exe not found. Install Inno Setup 6 first (winget install JRSoftware.InnoSetup)."
}

Write-Host "==> Step 2/2: compiling installer with Inno Setup" -ForegroundColor Cyan
& $iscc "$root\installer\setup.iss"

Write-Host "Done! Installer created at installer\output\MazeJump_Setup_win64.exe" -ForegroundColor Green
