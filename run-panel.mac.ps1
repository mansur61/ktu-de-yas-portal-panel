param(
    [switch] $NoBuild,
    [switch] $AutoConfirm
)

$ErrorActionPreference = "Stop"

$root        = $PSScriptRoot
$panelCsproj = "$root/KtuDeYasPortal.Panel/KtuDeYasPortal.Panel.csproj"
$backendRoot = Resolve-Path "$root/../ktu-de-yas" -ErrorAction SilentlyContinue

if (-not $backendRoot) {
    Write-Host "Backend repo bulunamadi: $root/../ktu-de-yas" -ForegroundColor Red
    exit 1
}

# Log dizini
$logDir   = "$root/logs/ps1"
$logStamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host ""
Write-Host "=== KTU DeYas Admin Panel (macOS) ===" -ForegroundColor Cyan
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── ADIM 1: Çalışan tüm servisleri öldür ─────────────────────────────────────
# Sadece bu script'in kullandığı portlar: 5000, 5056, 5080
# Docker container'larına (nodered, kafka, emqx vb.) DOKUNULMAZ
Write-Host "Onceki .NET servisler kapatiliyor..." -ForegroundColor Yellow

foreach ($port in @(5000, 5056, 5080)) {
    $portPids = & lsof -ti :$port 2>$null
    foreach ($procPid in $portPids) {
        if ($procPid) {
            Write-Host "  Port $port — PID $procPid kill ediliyor..." -ForegroundColor DarkGray
            & kill -9 $procPid 2>$null
        }
    }
}

Start-Sleep -Seconds 2
Write-Host "  Temizlendi." -ForegroundColor DarkGray
Write-Host ""

# ── ADIM 2: Build ─────────────────────────────────────────────────────────────
if (-not $NoBuild) {
    Write-Host "Build ediliyor..." -ForegroundColor Cyan

    $projects = @(
        @{ Name = "timeseries-service"; Path = "$($backendRoot.Path)/src/timeseries-service/TimeseriesService.csproj" },
        @{ Name = "edge-layer";         Path = "$($backendRoot.Path)/src/edge-layer/EdgeLayer.csproj" },
        @{ Name = "panel";              Path = $panelCsproj }
    )

    foreach ($proj in $projects) {
        Write-Host "  build: $($proj.Name)..." -ForegroundColor Gray
        $logFile = "$logDir/build-$($proj.Name)-$logStamp.log"
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
# macOS'ta her servis için bash script oluşturup ayrı Terminal penceresinde çalıştır.
# $env:DOTNET_ENVIRONMENT PowerShell syntax'ı bash'ta çalışmaz — bash export kullan.

function Start-Service {
    param(
        [string]$Name,
        [string]$Project,
        [string]$LogFile
    )

    $escapedProject = $Project -replace '"', '\"'
    $escapedLog     = $LogFile -replace '"', '\"'
    $bashFile       = "/tmp/ktu-deyas-$Name-$(New-Guid).sh"

    $bashContent = @"
#!/bin/bash
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
echo "=== $Name baslatiliyor ==="
echo "Log: $escapedLog"
echo ""
dotnet run --project "$escapedProject" -c Release --no-build 2>&1 | tee "$escapedLog"
echo ""
echo "=== $Name durdu. ==="
"@

    Set-Content -Path $bashFile -Value $bashContent -Encoding ASCII -NoNewline
    & chmod +x $bashFile
    & open -a Terminal $bashFile
}

Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

# timeseries-service (port 5000)
Start-Service `
    -Name    "timeseries-service" `
    -Project "$($backendRoot.Path)/src/timeseries-service/TimeseriesService.csproj" `
    -LogFile "$logDir/run-panel.mac-$logStamp-timeseries-service.log"
Write-Host "  [OK] timeseries-service baslatildi." -ForegroundColor Green
Start-Sleep -Seconds 4

# edge-layer (port 5080)
Start-Service `
    -Name    "edge-layer" `
    -Project "$($backendRoot.Path)/src/edge-layer/EdgeLayer.csproj" `
    -LogFile "$logDir/run-panel.mac-$logStamp-edge-layer.log"
Write-Host "  [OK] edge-layer baslatildi." -ForegroundColor Green
Start-Sleep -Seconds 4

# panel (port 5056)
Start-Service `
    -Name    "panel" `
    -Project $panelCsproj `
    -LogFile "$logDir/run-panel.mac-$logStamp-panel.log"
Write-Host "  [OK] panel baslatildi." -ForegroundColor Green
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

    # ── ADIM 5: Temizlik — sadece bu script'in başlattığı portları kapat ──────
    # Docker container'larına DOKUNULMAZ (nodered-baraj, kafka, emqx vs.)
    Write-Host "Servisler kapatiliyor..." -ForegroundColor Yellow

    foreach ($port in @(5000, 5056, 5080)) {
        $portPids = & lsof -ti :$port 2>$null
        foreach ($procPid in $portPids) {
            if ($procPid) {
                Write-Host "  Port $port — PID $procPid durduruluyor..." -ForegroundColor DarkGray
                & kill -9 $procPid 2>$null
            }
        }
    }

    # Terminal pencerelerini kapat (sadece bu script'in açtıkları)
    & osascript -e 'tell application "Terminal" to close every window asking no-save' 2>$null

    Write-Host "Servisler durduruldu. Docker container'lari hala calisiyor." -ForegroundColor Green
}
