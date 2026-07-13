param(
    [switch] $NoBuild,
    [switch] $AutoConfirm
)

$ErrorActionPreference = "Stop"

$root        = $PSScriptRoot
$panelCsproj = "$root/KtuDeYasPortal.Panel/KtuDeYasPortal.Panel.csproj"
$backendRoot = Resolve-Path "$root/../ktu-de-yas" -ErrorAction SilentlyContinue
# Build tamamlandıysa çalışan dotnet servisleri eski binary'yi kullanıyor olabilir.
# Bu durumda kaynak/binary zamanı eşit olacağından yalnız mtime kontrolü yeterli değildir.
$ForceRestartServices = -not $NoBuild

if (-not $backendRoot) {
    Write-Host "Backend repo bulunamadi: $root/../ktu-de-yas" -ForegroundColor Red
    exit 1
}

# Dev ortami: sadece dotnet run servisleri (timeseries, edge-layer, panel)
# Docker container'lari (Node-RED, Kafka, EMQX vb.) DOKUNULMAZ.

$logDir   = "$root/logs/ps1"
$logStamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host ""
Write-Host "=== KTU DeYas Panel (macOS / Dev) ===" -ForegroundColor Cyan
Write-Host "  Port zaten dolu ise servis SKIP edilir (kill yapilmaz)." -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── Build ────────────────────────────────────────────────────────────────────
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

# ── Port kontrol helper ───────────────────────────────────────────────────────
function Test-PortListening {
    param([int]$Port)
    $result = & lsof -ti ":$Port" 2>$null
    return ($null -ne $result -and "$result".Trim() -ne "")
}

# ── Değişiklik algılama ────────────────────────────────────────────────────────
# Proje klasöründeki .cs dosyalarından en yenisinin mtime'ını
# bin/Release/net8.0/ altındaki DLL'in mtime'ıyla karşılaştırır.
# Kaynak daha yeniyse → değişiklik var (build sonrası değiştirilmiş).
function Test-HasChanges {
    param([string]$ProjectPath)

    $projDir = Split-Path $ProjectPath -Parent

    # Binary: bin/Release/net8.0/*.dll
    $binDir = Join-Path $projDir "bin/Release/net8.0"
    if (-not (Test-Path $binDir)) { return $false } # hiç build yoksa — değişiklik yok sayılır

    $dllFiles = Get-ChildItem -Path $binDir -Filter "*.dll" -ErrorAction SilentlyContinue
    if (-not $dllFiles) { return $false }
    $binaryTime = ($dllFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime

    # Kaynak: proje klasöründeki .cs dosyaları (obj/ hariç)
    $srcFiles = Get-ChildItem -Path $projDir -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notlike "*/obj/*" -and $_.FullName -notlike "*/bin/*" }

    if (-not $srcFiles) { return $false }
    $latestSrc = ($srcFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime

    # Kaynak binary'den daha yeniyse değişiklik var
    return $latestSrc -gt $binaryTime
}

# ── Servis baslatici ──────────────────────────────────────────────────────────
function Start-Service {
    param(
        [string]$Name,
        [int]   $Port,
        [string]$Project
    )

    $portInUse = Test-PortListening -Port $Port

    if ($portInUse) {
        $hasChanges = Test-HasChanges -ProjectPath $Project
        if ($ForceRestartServices -or $hasChanges) {
            Write-Host "  [DEGISIKLIK] $Name — kaynak binary'den yeni, yeniden baslatiliyor..." -ForegroundColor Magenta

            # GÜVENLI KILL: sadece 'dotnet' process'i olan PID'leri öldür
            # Docker container PID'leri ve başka servisler korunur
            $portPids = & lsof -ti ":$Port" 2>$null
            foreach ($procPid in ($portPids -split '\n' | Where-Object { $_ -match '^\d+$' })) {
                if (-not $procPid) { continue }

                # PID'in yalnızca bu geliştirme servislerinden biri olduğunu doğrula.
                # macOS'ta dotnet run, uygulamayı doğrudan binary olarak gösterebilir.
                $procName = & ps -p $procPid -o comm= 2>$null
                if ($procName -match 'dotnet|TimeseriesService|EdgeLayer|KtuDeYasPortal\.Panel') {
                    Write-Host "    uygulama PID $procPid durduruluyor (port $Port)..." -ForegroundColor DarkGray
                    & kill -9 $procPid 2>$null
                } else {
                    Write-Host "    SKIP: PID $procPid process '$procName' — dotnet degil, dokunulmuyor." -ForegroundColor DarkYellow
                }
            }
            Start-Sleep -Seconds 2
        } else {
            Write-Host "  [SKIP] $Name — port $Port kullanimda, degisiklik yok." -ForegroundColor Yellow
            return
        }
    }

    $escapedProject = $Project -replace '"', '\"'
    $bashFile = "/tmp/ktu-deyas-$Name-$(New-Guid).sh"

    $bashContent = @"
#!/bin/bash
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
echo "=== $Name ==="
dotnet run --project "$escapedProject" -c Release --no-build
echo "=== $Name durdu ==="
"@

    Set-Content -Path $bashFile -Value $bashContent -Encoding ASCII -NoNewline
    & chmod +x $bashFile
    & open -a Terminal $bashFile
    Write-Host "  [OK] $Name baslatildi (port $Port)." -ForegroundColor Green
}

# ── Servisleri sirayla baslt ──────────────────────────────────────────────────
Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

# 1. timeseries-service
Start-Service -Name "timeseries-service" -Port 5000 `
    -Project "$($backendRoot.Path)/src/timeseries-service/TimeseriesService.csproj"

# Port 5000 hazir olana kadar bekle (panel 500 almasin)
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
    -Project "$($backendRoot.Path)/src/edge-layer/EdgeLayer.csproj"
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
    Read-Host "Cikis icin Enter (Terminal pencereleri kapanacak, Docker etkilenmez)"
    & osascript -e 'tell application "Terminal" to close every window asking no-save' 2>$null
    Write-Host "Terminal pencereleri kapatildi." -ForegroundColor Green
}
