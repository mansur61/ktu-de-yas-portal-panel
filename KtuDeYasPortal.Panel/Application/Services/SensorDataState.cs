using System.Collections.Concurrent;
using DeYas.Contracts.Realtime;

namespace KtuDeYasPortal.Panel.Application.Services;

/// <summary>
/// Singleton in-memory state — holds the most recent SensorData per device,
/// received from the Portal's SignalR hub (DashboardHub → ReceiveSensorUpdate).
///
/// This is driven by actual SensorData records that have flowed through:
///   Sensor → Node-RED → Kafka → Timeseries Service → TimescaleDB → Redis → SignalR → here.
///
/// A Sensor definition alone NEVER populates this state.
/// Only real data insertions cause updates.
/// </summary>
public sealed class SensorDataState
{
    // Key: DeviceId → latest SensorData record as SignalR event
    private readonly ConcurrentDictionary<string, SensorUpdatedEvent> _latestData = new();

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
    /// </summary>
    public void Update(SensorUpdatedEvent evt)
    {
        _latestData[evt.DeviceId] = evt;
        OnSensorDataReceived?.Invoke(evt);
    }

    /// <summary>
    /// Returns the latest SensorData event for the given deviceId, or null if
    /// no data has ever arrived (i.e., no SensorData exists for this device).
    /// </summary>
    public SensorUpdatedEvent? GetLatest(string deviceId) =>
        _latestData.TryGetValue(deviceId, out var v) ? v : null;

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
