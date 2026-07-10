param(
    [switch] $NoBuild,
    [switch] $AutoConfirm
)

$ErrorActionPreference = "Stop"

$root        = $PSScriptRoot
$panelCsproj = "$root\KtuDeYasPortal.Panel\KtuDeYasPortal.Panel.csproj"
$backendRoot = Resolve-Path "$root\..\ktu-de-yas" -ErrorAction SilentlyContinue

if (-not $backendRoot) {
    Write-Host "Backend repo bulunamadi: $root\..\ktu-de-yas" -ForegroundColor Red
    exit 1
}

# Log dizini
$logDir   = "$root\logs\ps1"
$logStamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host ""
Write-Host "=== KTU DeYas Admin Panel (Windows) ===" -ForegroundColor Cyan
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── ADIM 1: Çalışan tüm servisleri öldür ─────────────────────────────────────
# Sadece bu script'in portları: 5000, 5056, 5080
# Docker container'larına (nodered, kafka, emqx vb.) DOKUNULMAZ
Write-Host "Onceki .NET servisler kapatiliyor..." -ForegroundColor Yellow

foreach ($port in @(5000, 5056, 5080)) {
    try {
        $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        if ($conn) {
            foreach ($c in $conn) {
                Write-Host "  Port $port — PID $($c.OwningProcess) kill ediliyor..." -ForegroundColor DarkGray
                Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {}
}

Start-Sleep -Seconds 2
Write-Host "  Temizlendi." -ForegroundColor DarkGray
Write-Host ""

# ── ADIM 2: Build ─────────────────────────────────────────────────────────────
if (-not $NoBuild) {
    Write-Host "Build ediliyor..." -ForegroundColor Cyan

    $projects = @(
        @{ Name = "timeseries-service"; Path = "$($backendRoot.Path)\src\timeseries-service\TimeseriesService.csproj" },
        @{ Name = "edge-layer";         Path = "$($backendRoot.Path)\src\edge-layer\EdgeLayer.csproj" },
        @{ Name = "panel";              Path = $panelCsproj }
    )

    foreach ($proj in $projects) {
        Write-Host "  build: $($proj.Name)..." -ForegroundColor Gray
        $logFile = "$logDir\build-$($proj.Name)-$logStamp.log"
        dotnet build $proj.Path -c Release --nologo -v q 2>&1 | Out-File -FilePath $logFile -Encoding utf8
        if ($LASTEXITCODE -ne 0) {
            Write-Host "HATA: $($proj.Name) build basarisiz! Log: $logFile" -ForegroundColor Red
            Get-Content $logFile | Select-Object -Last 20 | Write-Host
            Read-Host "Devam icin Enter"
            exit 1
        }
        Write-Host "  [OK] $($proj.Name)" -ForegroundColor Green
    }

    Write-Host "Build tamamlandi." -ForegroundColor Green
    Write-Host ""
}

# ── ADIM 3: Servisleri başlat ─────────────────────────────────────────────────
function Start-ServiceWindow {
    param(
        [string]$Name,
        [string]$Project,
        [string]$LogFile
    )

    $escapedProject = $Project -replace '"', '\"'
    $cmd = "DOTNET_ENVIRONMENT=Development dotnet run --project `"$escapedProject`" -c Release --no-build"
    
    $psiArgs = "/c set DOTNET_ENVIRONMENT=Development && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --project `"$escapedProject`" -c Release --no-build"
    
    $proc = Start-Process -FilePath "cmd.exe" `
        -ArgumentList $psiArgs `
        -WindowStyle Normal `
        -PassThru
    
    return $proc
}

Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

$p1 = Start-ServiceWindow `
    -Name    "timeseries-service" `
    -Project "$($backendRoot.Path)\src\timeseries-service\TimeseriesService.csproj" `
    -LogFile "$logDir\run-panel.win-$logStamp-timeseries-service.log"
Write-Host "  [OK] timeseries-service baslatildi (PID: $($p1.Id))." -ForegroundColor Green
Start-Sleep -Seconds 4

$p2 = Start-ServiceWindow `
    -Name    "edge-layer" `
    -Project "$($backendRoot.Path)\src\edge-layer\EdgeLayer.csproj" `
    -LogFile "$logDir\run-panel.win-$logStamp-edge-layer.log"
Write-Host "  [OK] edge-layer baslatildi (PID: $($p2.Id))." -ForegroundColor Green
Start-Sleep -Seconds 4

$p3 = Start-ServiceWindow `
    -Name    "panel" `
    -Project $panelCsproj `
    -LogFile "$logDir\run-panel.win-$logStamp-panel.log"
Write-Host "  [OK] panel baslatildi (PID: $($p3.Id))." -ForegroundColor Green
Start-Sleep -Seconds 3

# ── ADIM 4: Bilgi ekranı ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== PANEL ACTIVE ===" -ForegroundColor Green
Write-Host "  Admin Panel    : http://localhost:5056" -ForegroundColor White
Write-Host "  Edge API       : http://localhost:5080/api/simulation/start/{structureId}" -ForegroundColor White
Write-Host "  Sensor API     : http://localhost:5000/api/sensors" -ForegroundColor White
Write-Host "  Structures API : http://localhost:5000/api/structures" -ForegroundColor White
Write-Host ""
Write-Host "  Node-RED (Merkez)  : http://localhost:1880     (tüm yapılar)" -ForegroundColor DarkCyan
Write-Host "  Node-RED (Baraj)   : http://localhost:1881/ui  (baraj monitoring)" -ForegroundColor DarkCyan
Write-Host "  Node-RED (Kopru)   : http://localhost:1882/ui  (kopru monitoring)" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "Log dizini: $logDir" -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    Read-Host "Cikis icin Enter (.NET servisleri durdurulacak, Docker etkilenmez)"

    # ── ADIM 5: Temizlik — sadece bu script'in portları ──────────────────────
    # Docker container'larına (nodered, kafka, emqx vb.) DOKUNULMAZ
    Write-Host "Servisler kapatiliyor..." -ForegroundColor Yellow

    foreach ($proc in @($p1, $p2, $p3)) {
        try {
            if ($proc -and -not $proc.HasExited) {
                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            }
        } catch {}
    }

    foreach ($port in @(5000, 5056, 5080)) {
        try {
            $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
            if ($conn) {
                foreach ($c in $conn) {
                    Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {}
    }

    Write-Host "Servisler durduruldu. Docker container'lari hala calisiyor." -ForegroundColor Green
}
