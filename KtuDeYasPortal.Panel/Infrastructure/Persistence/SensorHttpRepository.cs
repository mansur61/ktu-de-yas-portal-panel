using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public class SensorHttpRepository : ISensorRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SensorHttpRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<List<Sensor>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<Sensor>>("api/sensors", _json, ct);
        return result ?? new();
    }

    public async Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct = default)
    {
        var payload = new
        {
            id             = sensor.Id,
            name           = sensor.Name,
            deviceId       = sensor.DeviceId,
            locationId     = sensor.LocationId,
            sensorType     = sensor.SensorType.ToString(),
            topic          = sensor.Topic,
            unit           = sensor.Unit,
            description    = sensor.Description,
            metrics        = sensor.Metrics,
            isEnabled      = sensor.IsEnabled,
            isActive       = sensor.IsActive,
            protocolType   = string.IsNullOrEmpty(sensor.ProtocolType) ? "MQTT" : sensor.ProtocolType,
            connectionId   = sensor.ConnectionId,
            timeoutMs      = sensor.TimeoutMs,
            ipAddress      = sensor.IpAddress,
            port           = sensor.Port,
            serialPort     = sensor.SerialPort,
            baudRate       = sensor.BaudRate,
            pollingIntervalMs = sensor.PollingIntervalMs > 0 ? sensor.PollingIntervalMs : 1000,
            positionX      = sensor.PositionX,
            positionY      = sensor.PositionY,
            positionZ      = sensor.PositionZ,
            rotationX      = sensor.RotationX,
            rotationY      = sensor.RotationY,
            rotationZ      = sensor.RotationZ,
            imagePositionX = sensor.ImagePositionX,
            imagePositionY = sensor.ImagePositionY,
            telemetryIntervalMs = sensor.TelemetryIntervalMs > 0 ? sensor.TelemetryIntervalMs : 5000,
            telemetryBatchSize  = sensor.TelemetryBatchSize  > 0 ? sensor.TelemetryBatchSize  : 1
        };
        var resp = await _http.PostAsJsonAsync("api/sensors", payload, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Sensor>(_json, ct))!;
    }

    public async Task<object> TestTcpConnectionAsync(TcpSensorCreateRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/tcp-sensors/test-connection", request, _json, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<object>(_json, ct))!;
    }

    public async Task<Sensor> CreateTcpSensorAsync(TcpSensorCreateRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/tcp-sensors", request, _json, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Sensor>(_json, ct))!;
    }
}
