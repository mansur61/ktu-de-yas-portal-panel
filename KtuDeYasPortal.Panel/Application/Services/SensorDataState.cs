using System.Collections.Concurrent;
using DeYas.Contracts.Realtime;
using Microsoft.Extensions.Logging;

namespace KtuDeYasPortal.Panel.Application.Services;

/// <summary>
/// Singleton in-memory state — holds the most recent SensorData per device.
///
/// Data flow (panel-direct, no portal dependency):
///   Kafka → Timeseries Service → TimescaleDB insert
///   → Redis Pub/Sub (timeseries.updated)
///   → PanelRealtimeForwarder.HandleAsync
///   → SensorDataState.Update (this class)
///   → Blazor components via OnSensorDataReceived event
///
/// A Sensor definition alone NEVER populates this state.
/// Only real SensorData insertions cause updates.
/// </summary>
public sealed class SensorDataState
{
    private readonly ILogger<SensorDataState>? _logger;

    // Key: DeviceId → latest SensorData record as SignalR event
    private readonly ConcurrentDictionary<string, SensorUpdatedEvent> _latestData =
        new(StringComparer.OrdinalIgnoreCase);

    public SensorDataState(ILogger<SensorDataState>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fired on the thread that calls Update, which is typically the SignalR
    /// receive loop. Blazor components must call InvokeAsync(StateHasChanged).
    /// </summary>
    public event Action<SensorUpdatedEvent>? OnSensorDataReceived;

    /// <summary>Most recent SensorData record per DeviceId.</summary>
    public IReadOnlyDictionary<string, SensorUpdatedEvent> LatestSensorData => _latestData;

    /// <summary>
    /// Called by the SignalR client when a ReceiveSensorUpdate message arrives.
    /// Only call this when the message comes from an actual SensorData insertion event.
    /// Veri tutarlılığı için StructureId ve DeviceId kontrolü yapılır.
    /// </summary>
    public void Update(SensorUpdatedEvent evt)
    {
        // Veri tutarlılığı kontrolü
        if (evt.StructureId == null || string.IsNullOrWhiteSpace(evt.DeviceId))
        {
            _logger?.LogWarning(
                "[SensorDataState] Sensor update rejected - StructureId={StructureId} DeviceId={DeviceId}",
                evt.StructureId,
                evt.DeviceId);
            return; // Geçersiz veri, state'e ekleme
        }
        
        _latestData[evt.DeviceId] = evt;
        
        _logger?.LogDebug(
            "[SensorDataState] Sensor updated - Device={DeviceId} Structure={StructureId} Metrics={MetricCount}",
            evt.DeviceId,
            evt.StructureId,
            evt.AllMetrics?.Count ?? 0);
        
        OnSensorDataReceived?.Invoke(evt);
    }

    /// <summary>
    /// Returns the latest SensorData event for the given deviceId, or null if
    /// no data has ever arrived (i.e., no SensorData exists for this device).
    /// </summary>
    public SensorUpdatedEvent? GetLatest(string deviceId) =>
        _latestData.TryGetValue(deviceId, out var v) ? v : null;

    /// <summary>
    /// Veri tutarlılığı kontrolü: Verinin gerçekten belirtilen yapıya ait olup olmadığını kontrol eder
    /// </summary>
    public SensorUpdatedEvent? GetLatestForStructure(string deviceId, Guid structureId)
    {
        if (_latestData.TryGetValue(deviceId, out var v) && v.StructureId == structureId)
            return v;
        return null;
    }

    /// <summary>
    /// Belirtilen yapıya ait gerçek zamanlı veri akışı olan sensörleri döndürür
    /// </summary>
    public IReadOnlyList<string> GetActiveDeviceIdsForStructure(Guid structureId)
    {
        return _latestData.Values
            .Where(s => s.StructureId == structureId)
            .Select(s => s.DeviceId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Returns true if at least one real SensorData record has been received
    /// for ANY of the given device IDs via the realtime pipeline.
    /// </summary>
    public bool HasDataForAnyDevice(IEnumerable<string> deviceIds)
    {
        foreach (var id in deviceIds)
            if (_latestData.ContainsKey(id)) return true;
        return false;
    }

    /// <summary>
    /// Returns the subset of device IDs for which live SensorData has been received.
    /// </summary>
    public IReadOnlyList<string> GetActiveDeviceIds(IEnumerable<string> deviceIds)
    {
        return deviceIds.Where(id => _latestData.ContainsKey(id)).ToList();
    }

    /// <summary>
    /// Returns all SensorData events for the given device IDs
    /// (sensors belonging to a specific structure).
    /// </summary>
    public IReadOnlyList<SensorUpdatedEvent> GetDataForDevices(IEnumerable<string> deviceIds)
    {
        var result = new List<SensorUpdatedEvent>();
        foreach (var id in deviceIds)
            if (_latestData.TryGetValue(id, out var v)) result.Add(v);
        return result;
    }

    /// <summary>
    /// Collects all distinct metric keys seen across the given devices.
    /// Metrics come from the actual SensorData JSONB payload, not Sensor config.
    /// </summary>
    public IReadOnlyList<string> GetMetricKeysForDevices(IEnumerable<string> deviceIds)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in deviceIds)
        {
            if (_latestData.TryGetValue(id, out var v) && v.AllMetrics != null)
                foreach (var k in v.AllMetrics.Keys) keys.Add(k);
        }
        return keys.OrderBy(k => k).ToList();
    }
}
