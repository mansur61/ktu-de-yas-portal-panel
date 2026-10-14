using DeYas.DigitalTwin;
using DeYas.Contracts.Authorization;
using DeYas.Contracts.Realtime;
using KtuDeYasPortal.Panel.Application.UseCases;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;

namespace KtuDeYasPortal.Panel.Application.Services;

public sealed class PanelTwinSource : IDigitalTwinSource, IDisposable
{
    private readonly StructureUseCases _structures;
    private readonly PanelAccess _access;
    private readonly PanelApiClients _clients;
    private readonly SensorDataState _state;
    private readonly AlertState _alerts;
    public event Action<TwinSample>? Updated;
    public event Action<TwinAlarm>? AlarmChanged;
    public PanelTwinSource(StructureUseCases structures, PanelAccess access, PanelApiClients clients, SensorDataState state, AlertState alerts)
    { _structures=structures;_access=access;_clients=clients;_state=state;_alerts=alerts;state.OnSensorDataReceived+=Receive;alerts.OnFieldAlertReceived+=ReceiveAlarm; }
    private void Receive(SensorUpdatedEvent value)=>Updated?.Invoke(new(value.DeviceId,value.Timestamp,value.AllMetrics.Count>0?value.AllMetrics:new Dictionary<string,double>{{value.MetricKey,value.CurrentValue}},value.Status,value.StructureId));
    private void ReceiveAlarm(FieldAlert alarm)=>AlarmChanged?.Invoke(new(alarm.AlarmId,alarm.DeviceId,alarm.Severity,alarm.IsActive,alarm.Timestamp));
    public async Task<TwinSnapshot> LoadAsync(Guid? siteId, CancellationToken ct)
    {
        var scope=await _access.GetAsync(BarajPermissions.Dashboard,PortalMenuPermissions.DigitalTwin,ct);
        var all=await _structures.GetAllAsync(ct);
        var sites=all.Where(s=>scope.IsAdmin||scope.StructureIds.Contains(s.Id)).ToList();
        var chosen=sites.FirstOrDefault(s=>s.Id==siteId)??sites.FirstOrDefault();
        var sensors=new List<TwinSensor>();
        if(chosen is not null){
            var detail=await _structures.GetByIdAsync(chosen.Id,ct);
            var definitions=await _structures.GetAllSensorsAsync(ct);
            foreach(var attached in detail?.Sensors??[]){
                var definition=definitions.FirstOrDefault(s=>s.Id==attached.SensorId);
                sensors.Add(new(attached.SensorId,chosen.Id,attached.DeviceId,definition?.Name??attached.Name,definition?.SensorType.ToString()??attached.SensorType,definition?.Unit??attached.Unit,definition?.IsEnabled??attached.IsActive,definition?.ImagePositionX??attached.ImagePositionX,definition?.ImagePositionY??attached.ImagePositionY,definition?.CreatedAt??DateTime.MinValue));
            }
        }
        var locations=TwinLocations.FromEnvironment(chosen?.SiteEnvironmentJson);
        sensors=sensors.Select(s=>locations.TryGetValue(s.Id,out var p)?s with {Latitude=p.Latitude,Longitude=p.Longitude}:s).ToList();
        var samples=new System.Collections.Concurrent.ConcurrentBag<TwinSample>();
        var http=_clients.CreateClient("timeseries-api");
        await Parallel.ForEachAsync(sensors,new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=ct},async(sensor,token)=>{
            var cached=_state.GetLatest(sensor.DeviceId);
            if(cached is not null && (cached.StructureId is null||cached.StructureId==sensor.SiteId))samples.Add(new(sensor.DeviceId,cached.Timestamp,cached.AllMetrics.Count>0?cached.AllMetrics:new Dictionary<string,double>{{cached.MetricKey,cached.CurrentValue}},cached.Status,sensor.SiteId));
            using var response=await http.GetAsync("api/sensor-data/latest/"+Uri.EscapeDataString(sensor.DeviceId),token);
            if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return;
            response.EnsureSuccessStatusCode();
            var row=await response.Content.ReadFromJsonAsync<TwinLatestReading>(token);
            if(row?.ToSample(sensor.DeviceId,sensor.SiteId) is { } sample)samples.Add(sample);
        });
        var actions=new List<TwinAction>();
        if(chosen is not null){
            foreach(var item in new[]{(BarajPermissions.Dashboard,PortalMenuPermissions.Dashboard,"Ölçümler ve grafikler","sensor-dashboard","⌁"),(BarajPermissions.Media,PortalMenuPermissions.Media,"Kameralar ve kayıtlar","media-dashboard","▣"),(BarajPermissions.Alerts,PortalMenuPermissions.Alerts,"Alarmlar ve incelemeler","field-alerts","◉"),(BarajPermissions.Manage,PortalMenuPermissions.Structures,"Barajı ve sensörleri yönet","structures","⚙")}){
                var access=await _access.GetAsync(item.Item1,item.Item2,ct);
                if(access.IsAdmin||access.StructureIds.Contains(chosen.Id))actions.Add(new(item.Item3,item.Item4,item.Item5));
            }
            if(scope.IsAdmin)actions.Add(new("Genel Ayarlar","settings","⚙"));
        }
        return new(sites.Select(s=>new TwinSite(s.Id,s.Name,s.Province,s.Latitude,s.Longitude,s.ImageUrl)).ToList(),sensors,samples.OrderBy(s=>s.Timestamp).ToList(),actions,scope.IsAdmin,actions.Any(a=>a.Label.StartsWith("Alarmlar"))?_alerts.FieldAlerts.Where(a=>sensors.Any(s=>s.DeviceId==a.DeviceId)).Select(a=>new TwinAlarm(a.AlarmId,a.DeviceId,a.Severity,a.IsActive,a.Timestamp)).ToList():[]);
    }
    public void Dispose(){_state.OnSensorDataReceived-=Receive;_alerts.OnFieldAlertReceived-=ReceiveAlarm;}
}
