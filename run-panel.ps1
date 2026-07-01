param(
    [switch] $NoBuild,
    [switch] $AutoConfirm
)

<#
.SYNOPSIS
  Platform-aware wrapper that delegates to the appropriate script
  - run-panel.win.ps1 on Windows
  - run-panel.mac.ps1 on macOS/Linux

.PARAMETER NoBuild
  Skip the build step

.PARAMETER AutoConfirm
  Skip the initial confirmation prompt and run automatically
  Without this, the script will ask: "Devam? (y/N)"

.EXAMPLE
  # With confirmation prompt
  ./run-panel.ps1
  
  # Automatic mode
  ./run-panel.ps1 -AutoConfirm
  
  # Build and run
  ./run-panel.ps1 -AutoConfirm -NoBuild
#>

$scriptDir = $PSScriptRoot

if ($PSVersionTable.Platform -like "*Win*" -or $PSVersionTable.PSVersion.Major -lt 6) {
    # Windows PowerShell or Windows PS Core
    $targetScript = Join-Path $scriptDir "run-panel.win.ps1"
} else {
    # macOS / Linux
    $targetScript = Join-Path $scriptDir "run-panel.mac.ps1"
}

if (-not (Test-Path $targetScript)) {
    Write-Host "Hata: $targetScript bulunamadi" -ForegroundColor Red
    exit 1
}

Write-Host "Platform: $(if ($PSVersionTable.Platform) { $PSVersionTable.Platform } else { 'Windows' })" -ForegroundColor DarkGray
Write-Host "Çalıştırılıyor: $(Split-Path $targetScript -Leaf)" -ForegroundColor DarkGray
Write-Host ""

# Forward all arguments to the appropriate script
& $targetScript -NoBuild:$NoBuild -AutoConfirm:$AutoConfirm
exit $LASTEXITCODE
