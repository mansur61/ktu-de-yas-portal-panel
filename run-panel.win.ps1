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

# Dev ortami: sadece dotnet run servisleri (timeseries, edge-layer, panel)
# Docker container'lari (Node-RED, Kafka, EMQX vb.) DOKUNULMAZ.

$logDir   = "$root\logs\ps1"
$logStamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host ""
Write-Host "=== KTU DeYas Panel (Windows / Dev) ===" -ForegroundColor Cyan
Write-Host "  Port zaten dolu ise servis SKIP edilir (kill yapilmaz)." -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── Build ─────────────────────────────────────────────────────────────────────
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

# ── Port kontrol helper ───────────────────────────────────────────────────────
function Test-PortListening {
    param([int]$Port)
    try {
        $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
        return ($listeners | Where-Object { $_.Port -eq $Port }).Count -gt 0
    } catch { return $false }
}

# ── Değişiklik algılama ────────────────────────────────────────────────────────
function Test-HasChanges {
    param([string]$ProjectPath)

    $projDir = Split-Path $ProjectPath -Parent
    $binDir  = Join-Path $projDir "bin\Release\net8.0"
    if (-not (Test-Path $binDir)) { return $false }

    $dllFiles = Get-ChildItem -Path $binDir -Filter "*.dll" -ErrorAction SilentlyContinue
    if (-not $dllFiles) { return $false }
    $binaryTime = ($dllFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime

    $srcFiles = Get-ChildItem -Path $projDir -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

    if (-not $srcFiles) { return $false }
    $latestSrc = ($srcFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime

    return $latestSrc -gt $binaryTime
}

# ── Servis baslatici ──────────────────────────────────────────────────────────
$script:launchedProcesses = @()

function Start-Service {
    param(
        [string]$Name,
        [int]   $Port,
        [string]$Project
    )

    $portInUse = Test-PortListening -Port $Port

    if ($portInUse) {
        $hasChanges = Test-HasChanges -ProjectPath $Project
        if ($hasChanges) {
            Write-Host "  [DEGISIKLIK] $Name — kaynak binary'den yeni, yeniden baslatiliyor..." -ForegroundColor Magenta

            # GÜVENLI KILL: sadece 'dotnet.exe' process'i olan PID'leri öldür
            try {
                $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
                foreach ($c in $conn) {
                    $ownerProc = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
                    if ($ownerProc -and $ownerProc.Name -match 'dotnet') {
                        Write-Host "    dotnet PID $($c.OwningProcess) durduruluyor (port $Port)..." -ForegroundColor DarkGray
                        Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
                    } else {
                        Write-Host "    SKIP: PID $($c.OwningProcess) ('$($ownerProc.Name)') — dotnet degil, dokunulmuyor." -ForegroundColor DarkYellow
                    }
                }
            } catch {}
            Start-Sleep -Seconds 2
        } else {
            Write-Host "  [SKIP] $Name — port $Port kullanimda, degisiklik yok." -ForegroundColor Yellow
            return
        }
    }

    $proc = Start-Process -FilePath "cmd.exe" `
        -ArgumentList "/c set DOTNET_ENVIRONMENT=Development && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --project `"$Project`" -c Release --no-build" `
        -WindowStyle Normal `
        -PassThru

    $script:launchedProcesses += $proc
    Write-Host "  [OK] $Name baslatildi (port $Port, PID $($proc.Id))." -ForegroundColor Green
}

# ── Servisleri sirayla baslt ──────────────────────────────────────────────────
Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

# 1. timeseries-service
Start-Service -Name "timeseries-service" -Port 5000 `
    -Project "$($backendRoot.Path)\src\timeseries-service\TimeseriesService.csproj"

# Port 5000 hazir olana kadar bekle
$tsReady = $false
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/api/structures" -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($null -ne $r -and $r.StatusCode -lt 500) { $tsReady = $true; break }
    } catch {}
    Write-Host "  Bekleniyor... ($($i*2+2)s)" -ForegroundColor DarkGray
}
if ($tsReady) { Write-Host "  timeseries-service hazir." -ForegroundColor Green }
else { Write-Host "  UYARI: 30s icinde yanit alinamadi, devam ediliyor." -ForegroundColor Yellow }

# 2. edge-layer
Start-Service -Name "edge-layer" -Port 5080 `
    -Project "$($backendRoot.Path)\src\edge-layer\EdgeLayer.csproj"
Start-Sleep -Seconds 2

# 3. panel
Start-Service -Name "panel" -Port 5056 -Project $panelCsproj
Start-Sleep -Seconds 1

# ── Bilgi ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== PANEL ACTIVE ===" -ForegroundColor Green
Write-Host "  Admin Panel    : http://localhost:5056" -ForegroundColor White
Write-Host "  timeseries-api : http://localhost:5000" -ForegroundColor White
Write-Host "  Edge API       : http://localhost:5080" -ForegroundColor White
Write-Host ""
Write-Host "  Node-RED Merkez: http://localhost:1880  (Docker)" -ForegroundColor DarkCyan
Write-Host "  Node-RED Baraj : http://localhost:1881/ui (Docker)" -ForegroundColor DarkCyan
Write-Host "  Node-RED Kopru : http://localhost:1882/ui (Docker)" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  Log: $logDir" -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    Read-Host "Cikis icin Enter (bu script'in actiği pencereler kapanacak, Docker etkilenmez)"

    foreach ($proc in $script:launchedProcesses) {
        try {
            if (-not $proc.HasExited) {
                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            }
        } catch {}
    }

    Write-Host "Bu script'in actigi pencereler kapatildi. Docker calismaya devam ediyor." -ForegroundColor Green
}
