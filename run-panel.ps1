#!/usr/bin/env pwsh
# KTU-DE-YAS Panel Başlatma Script'i
# Kullanım: pwsh run-panel.ps1

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     KTU-DE-YAS Panel Başlatılıyor...                      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Proje dizinini ayarla
$projectPath = "$PSScriptRoot/KtuDeYasPortal.Panel"

# Dizin kontrolü
if (-not (Test-Path $projectPath)) {
    Write-Host "❌ HATA: Proje dizini bulunamadı: $projectPath" -ForegroundColor Red
    exit 1
}

# Panel, kullanıcı oturumu yerine kurulum servis hesabıyla Auth API'den JWT
# alır. Bu değerler merkez .env dosyasından okunur; repoya sır olarak yazılmaz.
$backendRoot = Resolve-Path "$PSScriptRoot/../ktu-de-yas" -ErrorAction SilentlyContinue
if (-not $backendRoot) {
    Write-Host "❌ HATA: Backend dizini bulunamadı: $PSScriptRoot/../ktu-de-yas" -ForegroundColor Red
    exit 1
}

. "$($backendRoot.Path)/ps1-local-dev-env.ps1"
$localEnvironment = Get-DeYasLocalDevEnvironment -EnvironmentFilePath "$($backendRoot.Path)/infra/compose/center-deployment/app/.env"
foreach ($entry in $localEnvironment.GetEnumerator()) {
    Set-Item -LiteralPath "Env:$($entry.Key)" -Value ([string]$entry.Value)
}
$env:ASPNETCORE_URLS = "http://localhost:5056"
$env:Services__TimeseriesApi = "http://localhost:5000"
$env:Services__AuthApi = "http://localhost:5100"
$env:Services__EdgeApi = "http://localhost:5080"
$env:Services__VideoApi = "http://localhost:5010"
$env:Services__AlertConfigApi = "http://localhost:5020"

# Panel'a geç
Set-Location $projectPath

Write-Host "📂 Dizin: $projectPath" -ForegroundColor Yellow
Write-Host "🔧 .NET SDK: $(dotnet --version)" -ForegroundColor Yellow
Write-Host "🔐 Panel servis hesabı ve bağlantı ayarları yüklendi" -ForegroundColor Yellow
Write-Host ""

# Bağımlılıkları geri yükle
Write-Host "📦 NuGet paketleri geri yükleniyor..." -ForegroundColor Green
dotnet restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Paket geri yükleme başarısız!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Paketler başarıyla yüklendi" -ForegroundColor Green
Write-Host ""

# Build kontrolü
Write-Host "🔨 Proje derleniyor..." -ForegroundColor Green
dotnet build --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Derleme başarısız!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Derleme başarılı" -ForegroundColor Green
Write-Host ""

# Panel'i başlat
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║     🚀 Panel başlatılıyor...                               ║" -ForegroundColor Magenta
Write-Host "║     📍 URL: http://localhost:5056                          ║" -ForegroundColor Magenta
Write-Host "║     ⏹️  Durdurmak için: Ctrl+C                             ║" -ForegroundColor Magenta
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

dotnet run --no-build
