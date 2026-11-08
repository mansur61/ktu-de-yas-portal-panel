using System.Text.Json;
using DeYas.Contracts.Sensors;
using DeYas.DigitalTwin;
using KtuDeYasPortal.Panel.Domain.Entities;
using Microsoft.JSInterop;

namespace KtuDeYasPortal.Panel.Components.Pages;

public partial class Structures
{
    private static bool IsGlbModelUrl(string? modelUrl) =>
        modelUrl?.Split('?', '#')[0].EndsWith(".glb", StringComparison.OrdinalIgnoreCase) == true;

    private static readonly JsonSerializerOptions SourceJson = new(JsonSerializerDefaults.Web);
    private bool _registerSourceSensor;
    private string _bindingDeviceId="", _bindingStreamKey="";
    private Guid? _wizardPlacingSensorId;
    private DateTime _lastSensorRefresh;
    private string? _editingMqttConnectionId;
    private IReadOnlyList<TwinModelSensor> WizardModelSensors => _newStructureSensors.Select(sensor =>
        new TwinModelSensor(
            sensor.Id,
            sensor.EffectiveDisplayName,
            sensor.SensorType.ToString(),
            sensor.PositionX,
            sensor.PositionY,
            sensor.PositionZ,
            sensor.RotationX,
            sensor.RotationY,
            sensor.RotationZ)).ToArray();
    private IReadOnlyList<TwinModelSensor> DetailModelSensors => (_editingStructure?.Sensors ?? [])
        .Select(sensor => new TwinModelSensor(
            sensor.SensorId,
            sensor.EffectiveDisplayName,
            sensor.SensorType,
            sensor.PositionX,
            sensor.PositionY,
            sensor.PositionZ,
            sensor.RotationX,
            sensor.RotationY,
            sensor.RotationZ))
        .ToArray();
    private bool HasManualSensorBinding => !string.IsNullOrWhiteSpace(_bindingDeviceId) || !string.IsNullOrWhiteSpace(_bindingStreamKey);
    private bool ShouldRegisterSourceSensor => _registerSourceSensor && (_sensorSourceKind == "camera" || HasManualSensorBinding);
    private bool ValidSensorBinding => !string.IsNullOrWhiteSpace(_bindingDeviceId)
        && _bindingStreamKey.Split('/').Length==2 && _bindingStreamKey.Split('/').All(x=>!string.IsNullOrWhiteSpace(x))
        && (_sensorSourceKind!="mqtt" || (!_sourceTopic.Contains('#')&&!_sourceTopic.Contains('+')));

    private async Task AddSensorToExistingAsync(Guid id)
    {
        await EditSavedSourcesAsync(id);
        if (_createdStructureId == id && _wizardStep == 4) OpenWizardSensorEditor();
    }
    private void OpenConnectionEditor() { OpenWizardSensorEditor(); _registerSourceSensor=false; }
    private string SensorSourceLabel(Sensor sensor)
    {
        var bindings=ReadSensorBindings();
        if(bindings.TryGetValue(sensor.Id,out var binding))return $"{binding.Kind.ToUpperInvariant()} · {binding.Host}:{binding.Port}"+(binding.Kind=="mqtt"?$" · {binding.Topic}":"");
        return $"{sensor.ProtocolType} · {sensor.IpAddress ?? sensor.DeviceId}"+(sensor.Port.HasValue?$":{sensor.Port}":"");
    }
    private sealed record SensorBinding(string Kind,string Host,int Port,string Topic,string StreamKey,string DeviceId,string SourceKey);
    private Dictionary<Guid,SensorBinding> ReadSensorBindings() => JsonSerializer.Deserialize<Dictionary<Guid,SensorBinding>>(string.IsNullOrWhiteSpace(GetEnv("DEYAS_SETUP_SENSOR_BINDINGS"))?"{}":GetEnv("DEYAS_SETUP_SENSOR_BINDINGS"),SourceJson)??[];
    private string CurrentSourceKey => _sensorSourceKind=="udp" ? $"{_sourceHost.Trim()}:{_sourcePort}" : MqttKey(_sourceHost.Trim(),_sourcePort,_sourceTopic.Trim(),_sourcePayloadMode,_sourceMetric.Trim());
    private static string MqttKey(string host,int port,string topic,string mode,string metric)=>$"mqtt://{host}:{port}/{topic}|{mode}|{metric}";

    private async Task RegisterSourceSensorAsync()
    {
        var camera=_sensorSourceKind=="camera"?_setupCameraDraft.Cameras.Single():null;
        var deviceId=camera is not null?$"camera-{camera.Id}":_bindingDeviceId.Trim();
        var locationId = string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_LOCATION_ID")) ? "default" : GetEnv("DEYAS_BRIDGE_LOCATION_ID");
        if (camera is null && ReadSensorBindings().Values.Any(b => b.SourceKey == CurrentSourceKey && b.StreamKey == _bindingStreamKey.Trim() && b.DeviceId != deviceId))
            throw new InvalidOperationException("Bu bağlantıdaki kanal başka bir sensöre eşlenmiş. Farklı kanal seçin veya mevcut sensörü kullanın.");
        var existing=(await StructureUC.GetAllSensorsAsync()).FirstOrDefault(s=>s.DeviceId==deviceId&&s.LocationId==locationId);
        if(existing is not null && !_newStructureSensors.Any(s=>s.Id==existing.Id))
            throw new InvalidOperationException("Bu cihaz kimliği başka bir sensör kaydında var. Kayıtlı sensörü listeden seçin veya farklı kimlik kullanın.");
        var kind=camera is null?Enum.Parse<SensorType>(_tcpSensorType):SensorType.Camera;
        var sensor=existing??await StructureUC.CreateSensorAsync(new Sensor {
            Name=camera is null?_tcpSensorName.Trim():camera.Id, DeviceId=deviceId, LocationId=locationId, SensorType=kind,
            ProtocolType=_sensorSourceKind.ToUpperInvariant(),IpAddress=camera is null?_sourceHost.Trim():null,Port=camera is null?_sourcePort:null,
            Topic=camera is null&&_sensorSourceKind=="mqtt"?_sourceTopic.Trim():"",
            Metrics=SensorMetricsConfiguration.GetMetrics(kind),Unit=SensorMetricsConfiguration.GetUnit(kind),
            ImagePositionX=_pendingPos?.x,ImagePositionY=_pendingPos?.y,
            Description="Panelden tanımlanan sensör; gerçek veri bekleniyor."
        });
        if(!_newStructureSensors.Any(s=>s.Id==sensor.Id))_newStructureSensors.Add(sensor);
        var structure=await StructureUC.GetByIdAsync(_createdStructureId)??throw new InvalidOperationException("Baraj bulunamadı.");
        await StructureUC.UpdateAsync(structure.Id,structure,structure.Sensors.Select(s=>s.SensorId).Append(sensor.Id).Distinct().ToList());
        if(_pendingPos.HasValue) {
            await StructureUC.UpdateSensorImagePositionAsync(sensor.Id,_pendingPos.Value.x,_pendingPos.Value.y);
            sensor.ImagePositionX=_pendingPos.Value.x;sensor.ImagePositionY=_pendingPos.Value.y;
        }
        if(camera is null) {
            var bindings=ReadSensorBindings();
            bindings[sensor.Id]=new(_sensorSourceKind,_sourceHost.Trim(),_sourcePort,_sourceTopic.Trim(),_bindingStreamKey.Trim(),deviceId,CurrentSourceKey);
            SetEnv("DEYAS_SETUP_SENSOR_BINDINGS",JsonSerializer.Serialize(bindings,SourceJson));
            var deviceMap=JsonSerializer.Deserialize<Dictionary<string,string>>(string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_DEVICE_MAP"))?"{}":GetEnv("DEYAS_BRIDGE_DEVICE_MAP"))??[];
            deviceMap[CurrentSourceKey+"/"+_bindingStreamKey.Trim()]=deviceId;
            SetEnv("DEYAS_BRIDGE_DEVICE_MAP",JsonSerializer.Serialize(deviceMap));
        }
        _wizardImageInitialized=false;
    }
    private async Task PlaceWizardSensorAsync(Sensor sensor)
    {
        _wizardPlacingSensorId=sensor.Id;
        if (string.IsNullOrWhiteSpace(_editingStructure?.ModelUrl))
            await JS.InvokeVoidAsync("openWizardPlacement");
        else
            StateHasChanged();
    }

    private void SelectWizardSensorForPlacement(Guid sensorId)
    {
        if (_newStructureSensors.Any(sensor => sensor.Id == sensorId))
            _wizardPlacingSensorId = sensorId;
    }

    private void SelectWizardSensorForPlacement(Sensor sensor) =>
        SelectWizardSensorForPlacement(sensor.Id);

    private void OnDetailTwinSensorSelected(Guid sensorId)
    {
        if (DetailModelSensors.Any(sensor => sensor.Id == sensorId))
            _detailSelectedSensorId = sensorId;
    }

    private void OnDetailSensorSelected(Sensor sensor) => _detailSelectedSensorId = sensor.Id;

    private async Task SaveDetailModelPositionAsync(ModelPositionSelection position)
    {
        var sensor = _editingStructure?.Sensors?.FirstOrDefault(item => item.SensorId == position.SensorId);
        if (sensor is null || string.IsNullOrWhiteSpace(_editingStructure?.ModelUrl))
        {
            _saveError = "GLB konumu kaydedilemedi. Sensör veya yapı modeli bulunamadı.";
            return;
        }

        try
        {
            await StructureUC.UpdateSensor3DPositionAsync(
                sensor.SensorId,
                position.PositionX,
                position.PositionY,
                position.PositionZ,
                position.RotationX,
                position.RotationY,
                position.RotationZ);
            sensor.PositionX = position.PositionX;
            sensor.PositionY = position.PositionY;
            sensor.PositionZ = position.PositionZ;
            sensor.RotationX = position.RotationX;
            sensor.RotationY = position.RotationY;
            sensor.RotationZ = position.RotationZ;
            _detailSelectedSensorId = sensor.SensorId;
            _saveError = null;
            _setupNotice = $"{sensor.EffectiveDisplayName} GLB üzerinde konumlandırıldı.";
        }
        catch (HttpRequestException ex)
        {
            _saveError = $"GLB sensör konumu kaydedilemedi. {ex.Message}";
            throw;
        }
    }

    private async Task SaveWizardModelPositionAsync(ModelPositionSelection position)
    {
        var sensor = _newStructureSensors.FirstOrDefault(item => item.Id == position.SensorId);
        if (sensor is null)
        {
            _saveError = "GLB konumu kaydedilemedi. Sensör artık bu yapıya bağlı değil.";
            return;
        }

        try
        {
            await StructureUC.UpdateSensor3DPositionAsync(
                sensor.Id,
                position.PositionX,
                position.PositionY,
                position.PositionZ,
                position.RotationX,
                position.RotationY,
                position.RotationZ);
            sensor.PositionX = position.PositionX;
            sensor.PositionY = position.PositionY;
            sensor.PositionZ = position.PositionZ;
            sensor.RotationX = position.RotationX;
            sensor.RotationY = position.RotationY;
            sensor.RotationZ = position.RotationZ;
            _wizardPlacingSensorId = null;
            _setupNotice = $"{sensor.EffectiveDisplayName} GLB üzerinde konumlandırıldı.";
            _saveError = null;
        }
        catch (HttpRequestException ex)
        {
            _saveError = $"GLB sensör konumu kaydedilemedi. {ex.Message}";
            throw;
        }
    }

    private static bool Has3DPosition(Sensor sensor) =>
        double.IsFinite(sensor.PositionX) &&
        double.IsFinite(sensor.PositionY) &&
        double.IsFinite(sensor.PositionZ) &&
        (sensor.PositionX != 0 || sensor.PositionY != 0 || sensor.PositionZ != 0);

    private static bool HasImagePosition(Sensor sensor) =>
        sensor.ImagePositionX is >= 0 and <= 100 &&
        sensor.ImagePositionY is >= 0 and <= 100;

    private Dictionary<Guid,GeoPoint> SensorLocations()=>TwinLocations.Read(GetEnv("DEYAS_SETUP_SENSOR_LOCATIONS"));
    private sealed class MqttConnection {
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        public string Name {get;set;}="MQTT kaynağı";
        public string Host {get;set;}="";
        public int Port {get;set;}=1883;
        public string Topic {get;set;}="";
        public string PayloadMode {get;set;}="scalar";
        public string ScalarMetric {get;set;}="temperature";
        public string ScalarMetrics {get;set;}="";
        public string UsernameEnv {get;set;}="";
        public string PasswordEnv {get;set;}="";
        public string Key=>MqttKey(Host,Port,Topic,PayloadMode,ScalarMetric);
    }
    private List<MqttConnection> MqttConnections() {
        var list=JsonSerializer.Deserialize<List<MqttConnection>>(string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_MQTT_SOURCES"))?"[]":GetEnv("DEYAS_BRIDGE_MQTT_SOURCES"),SourceJson)??[];
        if(list.Count==0&&_sourceMqtt&&!string.IsNullOrWhiteSpace(GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_HOST")))list.Add(new(){Id="legacy",Name=GetEnv("DEYAS_SETUP_MQTT_NAME"),Host=GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_HOST"),Port=int.TryParse(GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_PORT"),out var p)?p:1883,Topic=GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_TOPIC"),PayloadMode=GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_PAYLOAD_MODE"),ScalarMetric=GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_SCALAR_METRIC"),ScalarMetrics=GetEnv("DEYAS_BRIDGE_SOURCE_MQTT_SCALAR_METRICS"),UsernameEnv="DEYAS_BRIDGE_SOURCE_MQTT_USERNAME",PasswordEnv="DEYAS_BRIDGE_SOURCE_MQTT_PASSWORD"});
        return list;
    }
    private void AddMqttConnection() {
        var sources=MqttConnections();
        var key=MqttKey(_sourceHost.Trim(),_sourcePort,_sourceTopic.Trim(),_sourcePayloadMode,_sourceMetric.Trim());
        var source=sources.FirstOrDefault(s=>s.Id==_editingMqttConnectionId)??sources.FirstOrDefault(s=>s.Key==key);
        if(source is null){source=new();sources.Add(source);}
        var oldKey=source.Key;
        source.Name=_tcpSensorName.Trim();source.Host=_sourceHost.Trim();source.Port=_sourcePort;source.Topic=_sourceTopic.Trim();source.PayloadMode=_sourcePayloadMode;source.ScalarMetric=_sourceMetric.Trim();
        source.UsernameEnv=$"DEYAS_SOURCE_{source.Id}_USER";source.PasswordEnv=$"DEYAS_SOURCE_{source.Id}_PASSWORD";
        _deploymentSecrets[source.UsernameEnv]=_sourceUsername;_deploymentSecrets[source.PasswordEnv]=_sourcePassword;
        SetEnv("DEYAS_BRIDGE_MQTT_SOURCES",JsonSerializer.Serialize(sources,SourceJson));
        if(oldKey!=source.Key) {
            foreach(var env in new[]{"DEYAS_BRIDGE_DEVICE_MAP"}) {
                var map=JsonSerializer.Deserialize<Dictionary<string,string>>(string.IsNullOrWhiteSpace(GetEnv(env))?"{}":GetEnv(env))??[];
                foreach(var pair in map.Where(p=>p.Key.StartsWith(oldKey+"/",StringComparison.Ordinal)).ToList()){map.Remove(pair.Key);map[source.Key+pair.Key[oldKey.Length..]]=pair.Value;}
                SetEnv(env,JsonSerializer.Serialize(map));
            }
            var bindings=ReadSensorBindings();foreach(var pair in bindings.Where(p=>p.Value.SourceKey==oldKey).ToList())bindings[pair.Key]=pair.Value with {Host=source.Host,Port=source.Port,Topic=source.Topic,SourceKey=source.Key};
            SetEnv("DEYAS_SETUP_SENSOR_BINDINGS",JsonSerializer.Serialize(bindings,SourceJson));
        }
    }
    private void EditMqttConnection(MqttConnection source) {
        OpenConnectionEditor();_sensorSourceKind="mqtt";_editingMqttConnectionId=source.Id;
        _tcpSensorName=source.Name;_tcpSensorType="Generic";_sourceHost=source.Host;_sourcePort=source.Port;_sourceTopic=source.Topic;_sourcePayloadMode=source.PayloadMode;_sourceMetric=source.ScalarMetric;
        _sourceUsername=_deploymentSecrets.GetValueOrDefault(source.UsernameEnv,"");_sourcePassword=_deploymentSecrets.GetValueOrDefault(source.PasswordEnv,"");
    }
    private void RemoveMqttConnection(MqttConnection source) {
        if(ReadSensorBindings().Values.Any(b=>b.SourceKey==source.Key)){_saveError="Önce bu kaynağa bağlı sensörleri barajdan kaldırın.";return;}
        var sources=MqttConnections().Where(s=>s.Id!=source.Id).ToList();SetEnv("DEYAS_BRIDGE_MQTT_SOURCES",JsonSerializer.Serialize(sources,SourceJson));
        _sourceMqtt=sources.Count>0;UpdateSourceSelection();
    }
}
