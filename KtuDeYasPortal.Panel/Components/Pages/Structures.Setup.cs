using Microsoft.AspNetCore.Components;
using DeYas.Contracts.Authorization;
using System.Text.Json;
using KtuDeYasPortal.Panel.Application.Settings;
using KtuDeYasPortal.Panel.Domain.Entities;

namespace KtuDeYasPortal.Panel.Components.Pages;

public partial class Structures
{
    private bool _sourceUdp, _sourceMqtt, _sourceTcp, _packageBusy, _packageDownloaded;
    private string? _setupNotice;
    private sealed class FriendlyUdpSource
    {
        public string Name { get; set; } = "";
        public string SensorType { get; set; } = "Generic";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 1572;
    }
    private readonly Dictionary<string, JsonElement> _resumedTcpSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _removedWizardSensorIds = [];
    private List<FriendlyUdpSource> _friendlyUdpSources = [];

    private void UpdateSourceSelection()
    {
        SetEnv("DEYAS_BRIDGE_SOURCE_MODE", _sourceTcp && (_sourceUdp || _sourceMqtt) ? "all"
            : _sourceTcp ? "tcp" : _sourceUdp && _sourceMqtt ? "both" : _sourceUdp ? "udp" : _sourceMqtt ? "mqtt" : "both");
        if (_sourceUdp && _friendlyUdpSources.Count == 0) AddFriendlySource();
        // Unchecking a source disables its input; the dictionary retains other saved settings.
        if (!_sourceUdp) { SetEnv("DEYAS_BRIDGE_UDP_SOURCES", "[]"); SetEnv("DEYAS_BRIDGE_SENSOR_HOST", ""); }
        else SyncFriendlySources();
        if (!_sourceMqtt) { SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_HOST", ""); SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_TOPIC", ""); }
    }
    private void AddFriendlySource() { _friendlyUdpSources.Add(new()); SyncFriendlySources(); }
    private void RemoveFriendlySource(FriendlyUdpSource source) { if(ReadSensorBindings().Values.Any(b=>b.Kind=="udp"&&b.Host==source.Host&&b.Port==source.Port)){_saveError="Önce bu kaynağa bağlı sensörleri barajdan kaldırın.";return;} _friendlyUdpSources.Remove(source); SyncFriendlySources(); }
    private void SyncFriendlySources() => SetEnv("DEYAS_BRIDGE_UDP_SOURCES", JsonSerializer.Serialize(
        _friendlyUdpSources.Where(s => !string.IsNullOrWhiteSpace(s.Host)).Select(s => new { host = s.Host.Trim(), port = s.Port })));

    private void OpenWizardSensorEditor()
    {
        _registerSourceSensor=true; _bindingDeviceId=""; _bindingStreamKey=""; _editingMqttConnectionId=null;
        _sensorSourceKind = "";
        _selectedSourceKinds.Clear();
        _pendingSourceKinds.Clear();
        _sourceHost = _sourceTopic = _sourceMetric = "";
        _sourcePayloadMode = "scalar";
        _sourceUsername = _deploymentSecrets["DEYAS_BRIDGE_SOURCE_MQTT_USERNAME"];
        _sourcePassword = _deploymentSecrets["DEYAS_BRIDGE_SOURCE_MQTT_PASSWORD"];
        _editingCameraId = null;
        _isWizardSensorMode = true;
        _pendingPos = null;
        _addSensorTab = "tcp";
        ResetTcpSensorForm();
        _saveError = null;
        _showSensorPopup = true;
    }

    private async Task EditSavedSourcesAsync(Guid id)
    {
        _editingStructure = null;
        await ResumeSetupAsync(id);
    }

    private async Task DownloadSavedPackageAsync(Guid id)
    {
        if (_packageBusy) return;
        var access = await SetupAccess.GetAsync(BarajPermissions.Manage, PortalMenuPermissions.Structures);
        if (!access.IsAdmin && !access.StructureIds.Contains(id))
        {
            _simulationErrorMessage = "Saha ZIP’ini indirmek için bu barajın yönetim yetkisi gerekir.";
            return;
        }
        await ResumeSetupAsync(id);
        _showCreateWizard = false;
        if (_createdStructureId != id) return;
        _deploymentSecrets["DEYAS_JWT_SIGNING_KEY"] = access.IsAdmin ? Configuration["Jwt:SigningKey"] ?? "" : "";
        _packageBusy = true;
        try
        {
            if (!ValidatePackageInputs()) { _simulationErrorMessage = _saveError; return; }
            await DownloadDeploymentPackageCoreAsync();
            if (!string.IsNullOrEmpty(_saveError)) _simulationErrorMessage = _saveError;
        }
        catch (Exception ex) { _simulationErrorMessage = $"ZIP indirilemedi: {ex.Message}"; }
        finally { _packageBusy = false; }
    }

    private async Task ResumeSetupAsync(Guid id)
    {
        try
        {
            var structure = await StructureUC.GetByIdAsync(id);
            if (structure is null) { _simulationErrorMessage = "Baraj kaydı bulunamadı. Listeyi yenileyin."; return; }
            ShowCreateWizard();
            _createdStructureId = structure.Id;
            _newName = _setupName = structure.Name;
            _newType = structure.StructureType;
            _setupStructureType = structure.StructureType.ToString();
            _newDescription = structure.Description ?? "";
            _newProvince = structure.Province;
            _newDistrict = structure.District ?? "";
            _newDistricts = _districtMap.TryGetValue(_newProvince, out var districts) ? districts.ToList() : [];
            _newAddress = structure.Address ?? "";
            _newLat = structure.Latitude ?? 40.5;
            _newLng = structure.Longitude ?? 40.0;
            _newIsActive = structure.IsActive;
            _newNodeRedUrl = structure.NodeRedUrl ?? "";
            _newEdgeApiUrl = structure.EdgeApiUrl ?? "";
            _newEdgeConfigJson = structure.EdgeConfigJson ?? EdgeConfigTemplate;
            if (!string.IsNullOrWhiteSpace(structure.SiteEnvironmentJson))
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(StripDeploymentSecrets(structure.SiteEnvironmentJson));
                foreach (var entry in values ?? []) _siteEnvironment[entry.Key] = entry.Value;
            }
            _cameraSettings = CameraDeploymentSettings.FromJson(GetEnv(CameraDeploymentSettings.StorageKey));
            var mode = GetEnv("DEYAS_BRIDGE_SOURCE_MODE");
            _sourceMqtt = !string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_HOST")) && mode is "mqtt" or "both" or "all";
            _sourceTcp = mode is "tcp" or "all";
            using (var sources = JsonDocument.Parse(string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_UDP_SOURCES")) ? "[]" : GetEnv("DEYAS_BRIDGE_UDP_SOURCES")))
            {
                foreach (var source in sources.RootElement.EnumerateArray())
                    _friendlyUdpSources.Add(new() { Host = source.GetProperty("host").GetString() ?? "", Port = source.TryGetProperty("port", out var port) ? port.GetInt32() : 1572 });
            }
            _sourceUdp = _friendlyUdpSources.Count > 0 && mode is "udp" or "both" or "all";
            if (!_sourceUdp && !string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_SENSOR_HOST")))
            {
                _friendlyUdpSources.Add(new() { Host = GetEnv("DEYAS_BRIDGE_SENSOR_HOST"), Port = int.TryParse(GetEnv("DEYAS_BRIDGE_SENSOR_PORT"), out var port) ? port : 1572 });
                _sourceUdp = true;
            }
            var tcpJson = GetEnv("DEYAS_BRIDGE_TCP_SOURCES");
            if (!string.IsNullOrWhiteSpace(tcpJson)) {
                using var tcp = JsonDocument.Parse(tcpJson);
                foreach (var source in tcp.RootElement.EnumerateArray())
                    if (source.TryGetProperty("deviceId", out var device) && device.GetString() is string deviceId)
                        _resumedTcpSettings[deviceId] = source.Clone();
            }
            _newStructureSensors = structure.Sensors.Select(s => new Sensor
            {
                Id = s.SensorId, Name = s.Name, DeviceId = s.DeviceId, ProtocolType = s.ProtocolType,
                IpAddress = s.IpAddress, Port = s.Port, TimeoutMs = s.TimeoutMs, Unit = s.Unit,
                Metrics = s.Metrics, Topic = s.Topic, Description = s.Description,
                SensorType = Enum.TryParse<DeYas.Contracts.Sensors.SensorType>(s.SensorType, out var type) ? type : DeYas.Contracts.Sensors.SensorType.Generic,
                ImagePositionX = s.ImagePositionX, ImagePositionY = s.ImagePositionY
            }).ToList();
            _sourceTcp |= HasTcpSensor;
            _samePcDeployment = GetEnv("DEYAS_SETUP_SHARED_PC") == "true";
            _samePcProjectName = string.IsNullOrWhiteSpace(GetEnv("DEYAS_SETUP_PROJECT_NAME")) ? "saha-a" : GetEnv("DEYAS_SETUP_PROJECT_NAME");
            if (int.TryParse(GetEnv("EDGE_API_HOST_PORT"), out var edgePort)) _samePcEdgePort = edgePort;
            if (int.TryParse(GetEnv("NODERED_HOST_PORT"), out var nodeRedPort)) _samePcNodeRedPort = nodeRedPort;
            var sourceLabels = GetEnv("DEYAS_SETUP_UDP_LABELS");
            if (!string.IsNullOrWhiteSpace(sourceLabels)) {
                var labels = JsonSerializer.Deserialize<List<FriendlyUdpSource>>(sourceLabels) ?? [];
                foreach (var source in _friendlyUdpSources) {
                    var label = labels.FirstOrDefault(x => x.Host == source.Host && x.Port == source.Port);
                    if (label is not null) { source.Name = label.Name; source.SensorType = label.SensorType; }
                }
            }
            await RefreshDiscoveredSensorsAsync();
            _wizardStep = 4;
            _setupNotice = "Kayıtlı kurulum açıldı. Baraj kimliği ve sensör ilişkileri korunur. Şifreleri gerektiğinde paket adımında yeniden girin.";
        }
        catch (Exception ex) { _saveError = $"Kurulum açılamadı. Kayıtlarınız korunuyor. {ex.Message}"; _simulationErrorMessage = _saveError; }
    }


    private object BuildTcpSource(Sensor sensor)
    {
        if (_resumedTcpSettings.TryGetValue(sensor.DeviceId, out var source)) {
            var values = source.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
            values["host"] = sensor.IpAddress;
            values["port"] = sensor.Port;
            values["deviceId"] = sensor.DeviceId;
            return values;
        }
        return new { host = sensor.IpAddress, port = sensor.Port, deviceId = sensor.DeviceId,
            profile = string.IsNullOrWhiteSpace(sensor.ProtocolType) ? "GenericTcp" : sensor.ProtocolType,
            timeoutMs = sensor.TimeoutMs ?? 5000, pollingIntervalMs = sensor.PollingIntervalMs, encoding = _tcpEncoding };
    }

    private async Task SaveBasicInfoAndContinueAsync()
    {
        if (_isSaving) return;
        if (_createdStructureId == Guid.Empty) { await SaveStructureAndOpenSetupAsync(); return; }
        if (string.IsNullOrWhiteSpace(_newName) || string.IsNullOrWhiteSpace(_newProvince) || !_newType.HasValue) { _saveError = "Baraj adını ve ili doldurun."; return; }
        _isSaving = true; _saveError = null;
        try {
            var structure = await StructureUC.GetByIdAsync(_createdStructureId);
            if (structure is null) { _saveError = "Baraj kaydı bulunamadı."; return; }
            structure.Name = _setupName = _newName.Trim();
            structure.Description = _newDescription.Trim();
            structure.Province = _newProvince; structure.District = _newDistrict;
            structure.Address = _newAddress; structure.Latitude = _newLat; structure.Longitude = _newLng;
            structure.StructureType = _newType.Value; _setupStructureType = _newType.Value.ToString();
            structure.IsActive = _newIsActive; structure.NodeRedUrl = _newNodeRedUrl;
            await StructureUC.UpdateAsync(structure.Id, structure, structure.Sensors.Select(s => s.SensorId).ToList());
            OpenSetupStep();
        } catch (Exception ex) { _saveError = $"Baraj bilgileri kaydedilemedi. {ex.Message}"; }
        finally { _isSaving = false; }
    }

    private int _packageSection = 1;
    private bool ValidatePackageInputs()
    {
        if (_samePcDeployment && (!System.Text.RegularExpressions.Regex.IsMatch(_samePcProjectName, "^[a-z0-9][a-z0-9_-]*$")
            || _samePcEdgePort is < 1024 or > 65535 || _samePcNodeRedPort is < 1024 or > 65535 || _samePcEdgePort == _samePcNodeRedPort)) {
            _packageSection = 1; _saveError = "Kurulumun kısa adını küçük harf, sayı veya tireyle yazın. İki bağlantı numarası 1024–65535 arasında ve birbirinden farklı olmalı."; return false;
        }
        foreach (var key in new[] { "CENTRAL_AUTH_API_BASE_URL", "CENTRAL_TIMESERIES_API_BASE_URL" })
            if (!Uri.TryCreate(GetEnv(key), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) {
                _packageSection = 2; _saveError = "Merkez hizmet adresini http:// veya https:// ile başlayacak şekilde yazın."; return false;
            }
        var signingKey = _deploymentSecrets["DEYAS_JWT_SIGNING_KEY"];
        if (!string.IsNullOrWhiteSpace(signingKey) && signingKey.Length < 32) {
            _packageSection = 3; _saveError = "Merkez erişim anahtarı en az 32 karakter olmalı. Bilmiyorsanız boş bırakın; kurulum sorumlusu sahada tamamlasın."; return false;
        }
        return true;
    }
    private string _sensorSourceKind = "", _sourceHost = "", _sourceTopic = "", _sourceMetric = "";
    private int _sourcePort = 1572;
    private string _sourceUsername = "", _sourcePassword = "", _sourcePayloadMode = "scalar";
    private void SourceMeasurementChanged()
    {
        if (_sensorSourceKind != "mqtt") return;
        _sourcePayloadMode = _tcpSensorType is "Vibration" or "Accelerometer" ? "waveform" : "scalar";
        if (Enum.TryParse<DeYas.Contracts.Sensors.SensorType>(_tcpSensorType, out var type))
            _sourceMetric = DeYas.Contracts.Sensors.SensorMetricsConfiguration.GetMetrics(type).Split(',')[0];
    }
    private string? _editingCameraId;
    private CameraDeploymentSettings _setupCameraDraft = new();
    private bool HasSetupSources => SourceCount > 0 || _newStructureSensors.Count > 0;
    private int SourceCount => _newStructureSensors.Count(IsTcpSensor) + _friendlyUdpSources.Count + MqttConnections().Count + _cameraSettings.Cameras.Count;
    private bool CanSaveConfiguredSource => _sensorSourceKind == "camera" ? _setupCameraDraft.Cameras.Count == 1
        : !string.IsNullOrWhiteSpace(_tcpSensorName) && !string.IsNullOrWhiteSpace(_tcpSensorType)
            && !string.IsNullOrWhiteSpace(_sourceHost) && _sourcePort is > 0 and <= 65535
            && (_sensorSourceKind != "mqtt" || !string.IsNullOrWhiteSpace(_sourceTopic))
            && (!_registerSourceSensor || !HasManualSensorBinding || ValidSensorBinding);
    private static bool IsGeneratedSourceField(string key) => key is "SAHA_ENABLED_PIPELINES" or "DEYAS_BRIDGE_SOURCE_MODE" or "DEYAS_BRIDGE_UDP_SOURCES" or "DEYAS_BRIDGE_TCP_SOURCES" or "DEYAS_BRIDGE_SOURCE_MQTT_HOST" or "DEYAS_BRIDGE_SOURCE_MQTT_PORT" or "DEYAS_BRIDGE_SOURCE_MQTT_TOPIC";
    private static readonly (string Kind, string Label)[] SourceChoices = [
        ("udp", "UDP — Bilgisayarın portundan ölçüm al"),
        ("mqtt", "MQTT — Yayın sunucusundan ölçüm al"),
        ("camera", "Kamera — USB veya ağ kamerası ekle"),
        ("tcp", "TCP / Modbus — Cihaza bağlanarak ölçüm oku")];
    private readonly HashSet<string> _selectedSourceKinds = [];
    private readonly Queue<string> _pendingSourceKinds = new();
    private void ToggleSourceKind(string kind, ChangeEventArgs args)
    { if(args.Value is true) _selectedSourceKinds.Add(kind); else _selectedSourceKinds.Remove(kind); }
    private void StartSourceBatch()
    {
        _pendingSourceKinds.Clear();
        foreach(var choice in SourceChoices.Where(c=>_selectedSourceKinds.Contains(c.Kind))) _pendingSourceKinds.Enqueue(choice.Kind);
        AdvanceSourceBatch();
    }
    private void AdvanceSourceBatch()
    {
        if(_pendingSourceKinds.Count==0) { CloseSensorPopup(); return; }
        ResetTcpSensorForm();
        _bindingDeviceId=""; _bindingStreamKey="";
        _sourceHost=_sourceTopic=_sourceMetric="";
        _editingCameraId=null; _editingMqttConnectionId=null;
        _sensorSourceKind=_pendingSourceKinds.Dequeue();
        SourceKindChanged();
    }
    private string SourceSaveLabel => _pendingSourceKinds.Count>0 ? "Kaydet ve sonraki bağlantıya geç" : ShouldRegisterSourceSensor ? "Sensörü kaydet" : "Bağlantıyı kaydet";
    private string SourceDefault(string envKey,string configKey) =>
        !string.IsNullOrWhiteSpace(GetEnv(envKey)) ? GetEnv(envKey) : Configuration["FieldSourceDefaults:"+configKey] ?? "";
    private void SourceKindChanged()
    {
        _saveError = null;
        _sourcePort = _sensorSourceKind == "mqtt" ? 1883 : 1572;
        if (_sensorSourceKind == "mqtt") LoadMqttForm();
        if (_sensorSourceKind == "udp") {
            _sourceHost=SourceDefault("DEYAS_BRIDGE_SENSOR_HOST","UdpHost");
            _sourcePort=int.TryParse(SourceDefault("DEYAS_BRIDGE_SENSOR_PORT","UdpPort"),out var port)?port:1572;
            // Reuse this site's source computer before proposing installation defaults.
            var saved=_friendlyUdpSources.FirstOrDefault();
            if(saved is not null) { _sourceHost=saved.Host; _sourcePort=saved.Port; }
            _tcpSensorName="UDP ölçüm kaynağı";
            _tcpSensorType="Accelerometer";
        }
        if (_sensorSourceKind == "camera") {
            var index = 1;
            while (_cameraSettings.Cameras.Any(c => c.Id == $"kamera-{index}")) index++;
            _setupCameraDraft = new() { Cameras = [new() { Id = $"kamera-{index}", Mode = "remote", SourceType = "rtsp", CaptureMode = "scheduled" }] };
        }
    }
    private void LoadMqttForm()
    {
        _tcpSensorName = GetEnv("DEYAS_SETUP_MQTT_NAME");
        if (string.IsNullOrWhiteSpace(_tcpSensorName)) _tcpSensorName = "Mevcut ölçüm sunucusu";
        _tcpSensorType = string.IsNullOrWhiteSpace(GetEnv("DEYAS_SETUP_MQTT_TYPE")) ? "Generic" : GetEnv("DEYAS_SETUP_MQTT_TYPE");
        _sourceHost = SourceDefault("DEYAS_BRIDGE_SOURCE_MQTT_HOST", "MqttHost");
        _sourcePort = int.TryParse(SourceDefault("DEYAS_BRIDGE_SOURCE_MQTT_PORT", "MqttPort"), out var port) ? port : 1883;
        _sourceTopic = SourceDefault("DEYAS_BRIDGE_SOURCE_MQTT_TOPIC", "MqttTopic");
        _sourceMetric = GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_SCALAR_METRIC");
        _sourcePayloadMode = GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_PAYLOAD_MODE") == "waveform" ? "waveform" : "scalar";
    }
    private void EditMqttSource() { OpenConnectionEditor(); _sensorSourceKind = "mqtt"; LoadMqttForm(); }
    private void RemoveMqttSource() { _sourceMqtt = false; UpdateSourceSelection(); }
    private void EditSetupCamera(CameraDeploymentEntry camera)
    {
        OpenConnectionEditor(); _sensorSourceKind = "camera"; _editingCameraId = camera.Id;
        _setupCameraDraft = CameraDeploymentSettings.FromJson(new CameraDeploymentSettings { Cameras = [camera] }.ToJson());
        _setupCameraDraft.Cameras[0].SourceValue = camera.SourceValue;
    }
    private void RemoveSetupCamera(CameraDeploymentEntry camera) => _cameraSettings.Cameras.Remove(camera);
    private async Task SaveConfiguredSourceAsync()
    {
        if (!CanSaveConfiguredSource || _setupSaving) return;
        var oldSecrets = new Dictionary<string, string>(_deploymentSecrets);
        var oldEnvironment = new Dictionary<string, string?>(_siteEnvironment);
        var oldUdp = _friendlyUdpSources.ToList();
        var oldCameras = _cameraSettings.Cameras.ToList();
        var oldMqtt = _sourceMqtt; var oldUdpEnabled = _sourceUdp;
        try {
            if (_sensorSourceKind == "camera") {
                _setupCameraDraft.ToJson();
                var combined = new CameraDeploymentSettings { Cameras = _cameraSettings.Cameras.Where(c => c.Id != _editingCameraId).Concat(_setupCameraDraft.Cameras).ToList() };
                combined.ToJson();
                _cameraSettings.Cameras = combined.Cameras;
                if (!IsPipelineSelected("kamera")) TogglePipeline("kamera");
            } else {
                if (_sensorSourceKind == "udp") {
                    if (!_friendlyUdpSources.Any(s => s.Host == _sourceHost.Trim() && s.Port == _sourcePort)) _friendlyUdpSources.Add(new() { Name = _tcpSensorName.Trim(), SensorType = _tcpSensorType, Host = _sourceHost.Trim(), Port = _sourcePort });
                    _sourceUdp = true;
                    SetEnv("DEYAS_SETUP_UDP_LABELS", JsonSerializer.Serialize(_friendlyUdpSources));
                } else {
                    AddMqttConnection();
                    _sourceMqtt = true;
                    _deploymentSecrets["DEYAS_BRIDGE_SOURCE_MQTT_USERNAME"] = _sourceUsername;
                    _deploymentSecrets["DEYAS_BRIDGE_SOURCE_MQTT_PASSWORD"] = _sourcePassword;
                    SetEnv("DEYAS_SETUP_MQTT_NAME", _tcpSensorName.Trim()); SetEnv("DEYAS_SETUP_MQTT_TYPE", _tcpSensorType);
                    SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_HOST", _sourceHost.Trim()); SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_PORT", _sourcePort.ToString());
                    SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_PAYLOAD_MODE", _sourcePayloadMode);
                    SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_TOPIC", _sourceTopic.Trim()); SetEnv("DEYAS_BRIDGE_SOURCE_MQTT_SCALAR_METRIC", _sourceMetric.Trim());
                }
                var pipeline = _tcpSensorType == "Lidar" ? "lidar" : "ivmeolcer";
                if (!IsPipelineSelected(pipeline)) TogglePipeline(pipeline);
                UpdateSourceSelection();
            }
            if(ShouldRegisterSourceSensor) await RegisterSourceSensorAsync();
            await SaveFieldSetupAsync();
            if (!string.IsNullOrEmpty(_saveError)) throw new InvalidOperationException(_saveError);
            _setupNotice = ShouldRegisterSourceSensor ? "Sensör kaydı hazır. Veri gelişi izleme ekranında kontrol edilir; konumu şimdi veya daha sonra seçebilirsiniz." : "Veri bağlantısı kaydedildi. Bu bağlantı tek bir sensör değildir; sahadan gelen cihazlar/kanallar ayrı ayrı bulunup listelenecek. Daha sonra her sensörü görselde yerleştirebilirsiniz.";
            _pendingPos=null;
            AdvanceSourceBatch();
        } catch (Exception ex) {
            foreach (var secret in oldSecrets) _deploymentSecrets[secret.Key] = secret.Value;
            _siteEnvironment.Clear(); foreach (var value in oldEnvironment) _siteEnvironment[value.Key] = value.Value;
            _friendlyUdpSources = oldUdp; _cameraSettings.Cameras = oldCameras;
            _sourceMqtt = oldMqtt; _sourceUdp = oldUdpEnabled;
            _saveError = $"Kaynak kaydedilemedi. Bilgilerinizi kontrol edip yeniden deneyin. {ex.Message}";
        }
    }

    private string BuildFriendlyInstallGuide()
    {
        var command = _samePcDeployment
            ? $"docker compose -p {_samePcProjectName} -f docker-compose.structure.yml"
            : "docker compose";
        return $"""
# Önce bu dosyayı okuyun

Baraj: {_setupName}
Baraj kimliği: {_createdStructureId}

## 1. Dosyaları saha bilgisayarına götürün

ZIP'i yeni bir klasöre açın. Bu paket kurulum dosyasıdır; indirilmesi cihazların çalıştığı anlamına gelmez.
Mevcut kurulum güncelleniyorsa mevcut .env, kamera dosyası, veri klasörleri ve proje adını koruyun. Yeni paketle mevcut erişim bilgilerini değiştirmeyin.

## 2. Kurulum sorumlusu bağlantıyı kontrol etsin

Docker'ın kurulu ve çalışır olması gerekir. .env dosyasında DEYAS_JWT_SIGNING_KEY merkezle aynı olmalı; boşsa kurulum sorumlusu doldurmalıdır.
Merkez ve cihaz adresleri bu bilgisayardan erişilebilir olmalıdır. Bu dosya parola içermez; .env erişim bilgileri içerir, paketi yalnız kurulum sorumlusuyla paylaşın.

## 3. Kurulumu başlatın

Paket klasöründe terminal açın. Yeni kurulumda:

```sh
{(_samePcDeployment ? "docker compose -f docker-compose.shared-services.yml up -d\n" : "")}{command} config --quiet
{command} pull
{command} up -d
{command} ps
```

Ortak bilgisayar kullanılıyorsa ortak hizmetleri yalnız ilk kurulumda başlatın. Diğer baraj için farklı proje adı ve kontrol/editör portları gerekir. Ayrıntı AYNI_PC_KURULUM.md içindedir.

## 4. Veri geldiğini doğrulayın

Panelde Bağlantı ve kurulum kontrolü ekranını açın. Barajı seçin; cihaz bağlantısını ve örnek veriyi kontrol edin. Ardından baraj kartındaki Veri toplamayı başlat ve Merkeze ilet işlemlerini kullanın.
Portalda Ölçümler ve grafikler / Kameralar ve kayıtlar ekranlarında gerçek ölçüm ve çekim zamanını kontrol edin. Veri yoksa barajın uygun durumda olduğu varsayılmaz.

Kamera varsa KAMERA_KURULUM.txt dosyasını da okuyun. Kamerası başka bilgisayara bağlıysa o bilgisayarda yayıncı kurulumu gerekir.
Sorun halinde mevcut verileri veya Docker volume'larını silmeyin; teknik sorumluya bağlantı hatasını iletin.
""";
    }
}
