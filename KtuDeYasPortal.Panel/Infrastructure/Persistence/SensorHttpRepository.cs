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
            metrics        = sensor.Metrics,       // CSV metrik key'leri
            isEnabled      = sensor.IsEnabled,
            isActive       = sensor.IsActive,
            protocolType   = string.IsNullOrEmpty(sensor.ProtocolType) ? "MQTT" : sensor.ProtocolType,
            positionX      = sensor.PositionX,
            positionY      = sensor.PositionY,
            positionZ      = sensor.PositionZ,
            rotationX      = sensor.RotationX,
            rotationY      = sensor.RotationY,
            rotationZ      = sensor.RotationZ,
            imagePositionX = sensor.ImagePositionX,
            imagePositionY = sensor.ImagePositionY
        };
        var resp = await _http.PostAsJsonAsync("api/sensors", payload, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Sensor>(_json, ct))!;
    }
}
