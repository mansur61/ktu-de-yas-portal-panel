param(
    [switch] $NoBuild,
    [switch] $AutoConfirm
)

$ErrorActionPreference = "Stop"

$root         = $PSScriptRoot
$panelCsproj  = "$root\KtuDeYasPortal.Panel\KtuDeYasPortal.Panel.csproj"
$backendRoot  = Resolve-Path "$root\..\ktu-de-yas" -ErrorAction SilentlyContinue
$script:processes = @()

if (-not $backendRoot) {
    Write-Host "Backend repo bulunamadi: $root\..\ktu-de-yas" -ForegroundColor Red
    exit 1
}

# Use the shared log helper from the backend repo
. "$($backendRoot.Path)\ps1-log-helper.ps1"

Write-Host ""
Write-Host "=== KTU DeYas Admin Panel ===" -ForegroundColor Cyan
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── Build ─────────────────────────────────────────────────────────────────────
if (-not $NoBuild) {
    Write-Host "Build ediliyor..." -ForegroundColor Cyan
    Invoke-LoggedBuild -ProjectPath "$($backendRoot.Path)\src\timeseries-service\TimeseriesService.csproj" -Name "timeseries-service"
    Invoke-LoggedBuild -ProjectPath "$($backendRoot.Path)\src\edge-layer\EdgeLayer.csproj" -Name "edge-layer"
    Invoke-LoggedBuild -ProjectPath $panelCsproj -Name "panel"
    Write-Host "Build OK" -ForegroundColor Green
    Write-Host ""
}

# ── Servisleri başlat — zaten çalışıyorsa atla ───────────────────────────────
Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

# timeseries-service (port 5000) — panel'in veri kaynağı
if ((Test-PortInUse 5000) -or (Test-ProcessRunning "timeseries-service")) {
    Write-Host "  [SKIP] timeseries-service (port 5000 veya process zaten var)" -ForegroundColor Yellow
}
else {
    $cmd = "`$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project `"$($backendRoot.Path)\src\timeseries-service\TimeseriesService.csproj`" -c Release"
    $script:processes += Start-LoggedPowerShell -Name "timeseries-service" -Command $cmd
    Write-Host "  [OK] timeseries-service" -ForegroundColor Green
    Start-Sleep -Seconds 4
}

# edge-layer (port 5080) — panel'den structure simulation tetikleme hedefi
if ((Test-PortInUse 5080) -or (Test-ProcessRunning "edge-layer")) {
    Write-Host "  [SKIP] edge-layer (port 5080 veya process zaten var)" -ForegroundColor Yellow
}
else {
    $cmd = "`$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project `"$($backendRoot.Path)\src\edge-layer\EdgeLayer.csproj`" -c Release"
    $script:processes += Start-LoggedPowerShell -Name "edge-layer" -Command $cmd
    Write-Host "  [OK] edge-layer" -ForegroundColor Green
    Start-Sleep -Seconds 4
}

# Admin Panel (port 5056)
if ((Test-PortInUse 5056) -or (Test-ProcessRunning "KtuDeYasPortal.Panel")) {
    Write-Host "  [SKIP] panel (port 5056 veya process zaten var)" -ForegroundColor Yellow
}
else {
    $cmd = "`$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project `"$panelCsproj`" -c Release"
    $script:processes += Start-LoggedPowerShell -Name "panel" -Command $cmd
    Write-Host "  [OK] panel" -ForegroundColor Green
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "=== PANEL ACTIVE ===" -ForegroundColor Green
Write-Host "  Admin Panel    : http://localhost:5056" -ForegroundColor White
Write-Host "  Edge API       : http://localhost:5080/api/simulation/start/{structureId}" -ForegroundColor White
Write-Host "  Sensor API     : http://localhost:5000/api/sensors" -ForegroundColor White
Write-Host "  Structures API : http://localhost:5000/api/structures" -ForegroundColor White
Write-Host ""

if (-not $AutoConfirm) {
    Read-Host "Cikis icin Enter"
    Stop-ProcessList $script:processes
    Write-Host "Durduruldu." -ForegroundColor Green
}
