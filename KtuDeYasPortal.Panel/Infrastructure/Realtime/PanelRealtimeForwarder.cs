using System.Text.Json;
using DeYas.Contracts.Realtime;
using DeYas.Realtime;
using KtuDeYasPortal.Panel.Application.Services;

namespace KtuDeYasPortal.Panel.Infrastructure.Realtime;

/// <summary>
/// Panel-side Redis Pub/Sub message handler.
///
/// Data flow (fully independent of Portal):
///   Kafka → Timeseries Service → TimescaleDB insert
///   → Redis Pub/Sub (timeseries.updated)
///   → PanelRealtimeForwarder.HandleAsync (this class)
///   → SensorDataState.Update (singleton in-memory state)
///   → Blazor components re-render via OnSensorDataReceived event
///
/// The panel subscribes directly to Redis — no dependency on the Portal process.
/// </summary>
public sealed class PanelRealtimeForwarder : IRealtimeMessageHandler
{
    private readonly SensorDataState _state;
    private readonly ILogger<PanelRealtimeForwarder> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PanelRealtimeForwarder(SensorDataState state, ILogger<PanelRealtimeForwarder> logger)
    {
        _state  = state;
        _logger = logger;
    }

    public async Task HandleAsync(string channel, string message, CancellationToken ct = default)
    {
        // Panel only cares about timeseries/sensor data channels
        if (channel != RealtimeChannels.TimeseriesUpdated &&
            channel != RealtimeChannels.SensorUpdated)
            return;

        try
        {
            await HandleTimeseriesAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[panel-forwarder] Parse error ch={Channel}", channel);
        }
    }

    private Task HandleTimeseriesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Unwrap envelope: { eventType, payload: { deviceId, metrics, ... } }
        var payload = root.TryGetPropertyIgnoreCase("payload", out var p)
            ? p
            : root;

        var deviceId = payload.TryGetString("deviceId")
                    ?? root.TryGetString("deviceId")
                    ?? payload.TryGetString("device_id")
                    ?? root.TryGetString("device_id")
                    ?? payload.TryGetString("sensorId")
                    ?? root.TryGetString("sensorId")
                    ?? payload.TryGetString("sensor_id")
                    ?? root.TryGetString("sensor_id")
                    ?? string.Empty;

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _logger.LogDebug("[panel-forwarder] Skipped — deviceId missing");
            return Task.CompletedTask;
        }

        // Parse metrics — try nested "metrics" object first, then flat keys
        var metrics = payload.TryGetMetrics();
        if (metrics.Count == 0) metrics = root.TryGetMetrics();
        if (metrics.Count == 0) metrics = payload.TryGetFlatMetrics();
        if (metrics.Count == 0) metrics = root.TryGetFlatMetrics();

        var evt = new SensorUpdatedEvent(
            SensorId     : payload.TryGetString("sensorId")
                        ?? root.TryGetString("sensorId")
                        ?? payload.TryGetString("sensor_id")
                        ?? root.TryGetString("sensor_id")
                        ?? deviceId,
            DeviceId     : deviceId,
            LocationId   : payload.TryGetString("locationId")
                        ?? root.TryGetString("locationId")
                        ?? payload.TryGetString("location_id")
                        ?? root.TryGetString("location_id")
                        ?? "default",
            CurrentValue : metrics.Values.FirstOrDefault(),
            MetricKey    : metrics.Keys.FirstOrDefault() ?? "value",
            Timestamp    : payload.TryGetDateTime(),
            Status       : "ok",
            AllMetrics   : metrics);

        _logger.LogDebug(
            "[panel-forwarder] device={DeviceId} metrics={Count}",
            evt.DeviceId, evt.AllMetrics.Count);

        _state.Update(evt);
        return Task.CompletedTask;
    }
}

// ── JSON helpers (self-contained, no dependency on portal's extensions) ────────

file static class JsonElementExtensions
{
    public static bool TryGetPropertyIgnoreCase(this JsonElement el, string name, out JsonElement value)
    {
        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static string? TryGetString(this JsonElement el, string key) =>
        el.TryGetPropertyIgnoreCase(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static DateTime TryGetDateTime(this JsonElement el, string key = "timestamp")
    {
        if (el.TryGetPropertyIgnoreCase(key, out var v) && v.TryGetDateTime(out var dt))
            return dt;
        return DateTime.UtcNow;
    }

    private static bool TryGetNumber(this JsonElement v, out double d)
    {
        if (v.TryGetDouble(out d)) return true;
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d))
            return true;
        d = 0;
        return false;
    }

    public static Dictionary<string, double> TryGetMetrics(this JsonElement el)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (!el.TryGetPropertyIgnoreCase("metrics", out var m) ||
            m.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var p in m.EnumerateObject())
            if (p.Value.TryGetNumber(out var d)) result[p.Name] = d;
        return result;
    }

    private static readonly HashSet<string> MetaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "deviceId","sensorId","locationId","timestamp","status","eventType",
        "payload","id","sourceClientId","serviceType","protocolType","dataKind","metrics"
    };

    public static Dictionary<string, double> TryGetFlatMetrics(this JsonElement el)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in el.EnumerateObject())
        {
            if (MetaKeys.Contains(p.Name)) continue;
            if (p.Value.TryGetNumber(out var d)) result[p.Name] = d;
        }
        return result;
    }
}
