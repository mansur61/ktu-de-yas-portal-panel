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

# ── Log klasörü: her çalıştırmada sil → yeniden oluştur ─────────────────────
$logDir   = "$root/logs/ps1"
$logStamp = Get-Date -Format "yyyyMMdd-HHmmss"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# PID dosyası: başlatılan her dotnet process'inin PID'i buraya yazılır
$pidFile = "$logDir/pids-$logStamp.txt"

# Açılan bash script dosyaları listesi (temizlik için)
$script:TempBashFiles = [System.Collections.Generic.List[string]]::new()

function Write-Log {
    param([string]$Msg)
    $ts = Get-Date -Format "yyyy-MM-ddTHH:mm:ss"
    "[$ts] $Msg" | Add-Content -Path "$logDir/run-panel-$logStamp.log" -Encoding utf8
}

Write-Log "run-panel.mac.ps1 started | NoBuild=$NoBuild AutoConfirm=$AutoConfirm"

Write-Host ""
Write-Host "=== KTU DeYas Panel (macOS / Dev) ===" -ForegroundColor Cyan
Write-Host "  Log: $logDir" -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    $c = Read-Host "Devam? (y/N)"
    if ($c -ne "y" -and $c -ne "Y") { exit 0 }
}

# ── Build ─────────────────────────────────────────────────────────────────────
if (-not $NoBuild) {
    Write-Host "Build ediliyor..." -ForegroundColor Cyan
    $buildProjects = @(
        @{ Name = "timeseries-service"; Path = "$($backendRoot.Path)/src/timeseries-service/TimeseriesService.csproj" },
        @{ Name = "edge-layer";         Path = "$($backendRoot.Path)/src/edge-layer/EdgeLayer.csproj" },
        @{ Name = "panel";              Path = $panelCsproj }
    )
    foreach ($proj in $buildProjects) {
        Write-Host "  build: $($proj.Name)..." -ForegroundColor Gray
        $buildLog = "$logDir/build-$($proj.Name)-$logStamp.log"
        dotnet build $proj.Path -c Release --nologo -v q 2>&1 | Out-File -FilePath $buildLog -Encoding utf8
        if ($LASTEXITCODE -ne 0) {
            Write-Host "HATA: $($proj.Name) build basarisiz!" -ForegroundColor Red
            Get-Content $buildLog | Select-Object -Last 20 | Write-Host
            Write-Log "build FAILED: $($proj.Name)"
            Read-Host "Devam icin Enter"
            exit 1
        }
        Write-Host "  [OK] $($proj.Name)" -ForegroundColor Green
        Write-Log "build ok: $($proj.Name)"
    }
    Write-Host "Build tamamlandi." -ForegroundColor Green
    Write-Host ""
}

# ── Port kontrol ──────────────────────────────────────────────────────────────
function Test-PortListening([int]$Port) {
    $r = & lsof -ti ":$Port" 2>$null
    return ($null -ne $r -and "$r".Trim() -ne "")
}

# ── Servis başlatıcı ──────────────────────────────────────────────────────────
# Her servis için:
#   1. /tmp/ktu-deyas-<name>-<guid>.sh bash scripti oluşturur
#   2. Script dotnet run çalıştırır, çıktıyı log dosyasına yazar
#   3. Script kendi PID'ini $pidFile'a kaydeder (kill için)
#   4. macOS Terminal'de bu script'i açar
# ─────────────────────────────────────────────────────────────────────────────
function Start-Service {
    param(
        [string] $Name,
        [int]    $Port,
        [string] $ProjectPath,
        [string] $ExtraEnv = ""          # ek env satırları, bash syntax'ında
    )

    if (Test-PortListening $Port) {
        Write-Host "  [SKIP] $Name — port $Port zaten kullanimda." -ForegroundColor Yellow
        Write-Log "SKIP $Name — port $Port in use"
        return
    }

    $safeName    = $Name -replace '[^a-zA-Z0-9_-]', '-'
    $serviceLog  = "$logDir/$safeName-$logStamp.log"
    $uid         = [guid]::NewGuid().ToString('N').Substring(0,8)
    $bashFile    = "/tmp/ktu-deyas-$safeName-$uid.sh"
    $osaFile     = "/tmp/ktu-deyas-close-$uid.scpt"
    $script:TempBashFiles.Add($bashFile)
    $script:TempBashFiles.Add($osaFile)

    # AppleScript dosyasını ayrı yaz — bash içinden çağrılır
    @'
tell application "Terminal"
    close front window saving no
end tell
'@ | Set-Content -Path $osaFile -Encoding ASCII

    # bash script: PID'i kaydet, dotnet çalıştır, log yaz, Enter → pencere kapat
    $bashContent = @"
#!/bin/bash
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
$ExtraEnv

SERVICE_LOG=$serviceLog
PID_FILE=$pidFile
OSA_FILE=$osaFile

printf '=== $Name ===\n' | tee -a "\$SERVICE_LOG"
printf 'Log: %s\n\n' "\$SERVICE_LOG" | tee -a "\$SERVICE_LOG"

dotnet run --project '$ProjectPath' -c Release --no-build 2>&1 | tee -a "\$SERVICE_LOG" &
DOTNET_PID=\$!
printf '%s\n' "\$DOTNET_PID" >> "\$PID_FILE"
printf '  PID=%s kaydedildi.\n' "\$DOTNET_PID" | tee -a "\$SERVICE_LOG"

wait \$DOTNET_PID
EXIT_CODE=\$?

printf '\n' | tee -a "\$SERVICE_LOG"
if [ "\$EXIT_CODE" -eq 0 ]; then
    printf '=== $Name tamamlandi ===\n' | tee -a "\$SERVICE_LOG"
else
    printf '=== $Name durdu (exit=%s) ===\n' "\$EXIT_CODE" | tee -a "\$SERVICE_LOG"
fi

printf '\nEnter basilinca bu pencere kapanacak...\n'
read -r _REPLY

osascript "\$OSA_FILE" 2>/dev/null
exit \$EXIT_CODE
"@

    Set-Content -Path $bashFile -Value $bashContent -Encoding ASCII
    & chmod +x $bashFile
    & open -a Terminal $bashFile

    Write-Host "  [OK] $Name baslatildi (port $Port)" -ForegroundColor Green
    Write-Log "started $Name | port=$Port | log=$serviceLog | bash=$bashFile"
}

# ── Servisleri sırayla başlat ─────────────────────────────────────────────────
Write-Host "Servisler baslatiliyor..." -ForegroundColor Cyan

# 1. timeseries-service
Start-Service -Name "timeseries-service" `
    -Port 5000 `
    -ProjectPath "$($backendRoot.Path)/src/timeseries-service/TimeseriesService.csproj"

# Port 5000 hazır olana kadar bekle
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
else          { Write-Host "  UYARI: 30s icinde yanit alinamadi, devam ediliyor." -ForegroundColor Yellow }

# 2. edge-layer
Start-Service -Name "edge-layer" `
    -Port 5080 `
    -ProjectPath "$($backendRoot.Path)/src/edge-layer/EdgeLayer.csproj"
Start-Sleep -Seconds 2

# 3. panel
Start-Service -Name "panel" `
    -Port 5056 `
    -ProjectPath $panelCsproj `
    -ExtraEnv @"
export ASPNETCORE_URLS='http://localhost:5056'
export Services__TimeseriesApi='http://localhost:5000'
export Services__EdgeApi='http://localhost:5080'
"@
Start-Sleep -Seconds 1

# ── Bilgi ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== PANEL ACTIVE ===" -ForegroundColor Green
Write-Host "  Admin Panel    : http://localhost:5056/structures" -ForegroundColor White
Write-Host "  timeseries-api : http://localhost:5000" -ForegroundColor White
Write-Host "  Edge API       : http://localhost:5080" -ForegroundColor White
Write-Host ""
Write-Host "  Node-RED Merkez: http://localhost:1880  (Docker)" -ForegroundColor DarkCyan
Write-Host "  Node-RED Baraj : http://localhost:1881/ui (Docker)" -ForegroundColor DarkCyan
Write-Host "  Node-RED Kopru : http://localhost:1882/ui (Docker)" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  Log klasoru    : $logDir" -ForegroundColor DarkGray
Write-Host "  PID dosyasi    : $pidFile" -ForegroundColor DarkGray
Write-Host ""

if (-not $AutoConfirm) {
    Read-Host "DURDURMAK icin Enter'a basin (Docker etkilenmez)"

    Write-Host ""
    Write-Host "Servisler durduruluyor..." -ForegroundColor Yellow
    Write-Log "shutdown initiated"

    # 1. PID dosyasındaki dotnet process'lerini kill et
    if (Test-Path $pidFile) {
        $pids = Get-Content $pidFile -Encoding utf8 | Where-Object { $_ -match '^\d+$' }
        foreach ($p in $pids) {
            try {
                $procName = & ps -p $p -o comm= 2>$null
                if ($procName) {
                    Write-Host "  kill PID=$p ($procName)" -ForegroundColor DarkGray
                    & kill -9 $p 2>$null
                    Write-Log "killed PID=$p ($procName)"
                }
            } catch {}
        }
    }

    # 2. Güvenlik ağı: hâlâ çalışan proje-adı dotnet process'leri
    foreach ($pattern in @("timeseries-service", "EdgeLayer", "KtuDeYasPortal")) {
        $procs = & pgrep -f $pattern 2>$null
        foreach ($p in ($procs -split '\n' | Where-Object { $_ -match '^\d+$' })) {
            Write-Host "  kill (pattern=$pattern) PID=$p" -ForegroundColor DarkGray
            & kill -9 $p 2>$null
        }
    }

    # 3. Temp bash script dosyalarını temizle
    foreach ($f in $script:TempBashFiles) {
        Remove-Item $f -Force -ErrorAction SilentlyContinue
    }

    # 4. Terminal pencerelerini kapat
    try {
        & osascript -e 'tell application "Terminal" to close every window asking no-save' 2>$null
    } catch {}

    Write-Log "shutdown complete"
    Write-Host "Durduruldu. Docker calismaya devam ediyor." -ForegroundColor Green
}
