using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using DeYas.Contracts.Sensors;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public class SensorHttpRepository : ISensorRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SensorHttpRepository(KtuDeYasPortal.Panel.Infrastructure.Persistence.PanelApiClients factory)
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
            imageUrl       = sensor.ImageUrl,
            telemetryIntervalMs = sensor.TelemetryIntervalMs > 0 ? sensor.TelemetryIntervalMs : 5000,
            telemetryBatchSize  = sensor.TelemetryBatchSize  > 0 ? sensor.TelemetryBatchSize  : 1
        };
        var resp = await _http.PostAsJsonAsync("api/sensors", payload, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Sensor>(_json, ct))!;
    }

    public async Task UpdateImagePositionAsync(Guid sensorId, double x, double y, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/sensors/{sensorId}/image-position", new { x, y }, _json, ct);
        resp.EnsureSuccessStatusCode();
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
        var created = await response.Content.ReadFromJsonAsync<TcpSensorCreateResponse>(_json, ct)
            ?? throw new InvalidOperationException("TCP sensör oluşturma yanıtı boş.");
        return new Sensor
        {
            Id = created.SensorId,
            Name = created.Name,
            DisplayName = created.DisplayName,
            DeviceId = created.DeviceId,
            LocationId = created.LocationId,
            SensorType = Enum.TryParse<SensorType>(created.SensorType, true, out var sensorType)
                ? sensorType
                : SensorType.Generic,
            Topic = created.Topic,
            Description = created.Description,
            Metrics = created.Metrics,
            ProtocolType = created.ProtocolType,
            IpAddress = created.IpAddress,
            Port = created.Port,
            TimeoutMs = created.TimeoutMs,
            PollingIntervalMs = created.PollingIntervalMs
        };
    }

    public async Task<ConnectedSensorResponse> CreateConnectedSensorAsync(
        Guid structureId,
        ConnectedSensorRequest request,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"api/structures/{structureId}/sensors/connected", request, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConnectedSensorResponse>(_json, ct))!;
    }

    // ── Prod Lifecycle endpoint'leri ──────────────────────────────────────────

    /// <summary>
    /// Bağlantı testini çalıştırır ve sonucu DB'ye kaydeder.
    /// POST /api/sensors/{sensorId}/lifecycle/test-connection
    /// </summary>
    public async Task<ConnectionTestResultDto> TestConnectionLifecycleAsync(
        Guid sensorId,
        ConnectionTestRequestDto request,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"api/sensors/{sensorId}/lifecycle/test-connection", request, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConnectionTestResultDto>(_json, ct))!;
    }

    /// <summary>
    /// Parser doğrulaması yapar ve sonucu kaydeder.
    /// POST /api/sensors/{sensorId}/lifecycle/validate-parser
    /// </summary>
    public async Task<ParserValidationResultDto> ValidateParserAsync(
        Guid sensorId,
        ParserValidationRequestDto request,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"api/sensors/{sensorId}/lifecycle/validate-parser", request, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ParserValidationResultDto>(_json, ct))!;
    }

    /// <summary>
    /// Parser çıktısından metrik önizleme oluşturur ve kaydeder.
    /// Başarılıysa sensör otomatik Ready'e geçer.
    /// POST /api/sensors/{sensorId}/lifecycle/generate-metric-preview
    /// </summary>
    public async Task<MetricPreviewGeneratedDto> GenerateMetricPreviewAsync(
        Guid sensorId,
        GenerateMetricPreviewRequestDto request,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"api/sensors/{sensorId}/lifecycle/generate-metric-preview", request, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<MetricPreviewGeneratedDto>(_json, ct))!;
    }

    /// <summary>
    /// Sensörün hazırlık durumunu döner.
    /// GET /api/sensors/{sensorId}/lifecycle/readiness
    /// </summary>
    public async Task<SensorReadinessDto> GetReadinessAsync(
        Guid sensorId,
        CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<SensorReadinessDto>(
            $"api/sensors/{sensorId}/lifecycle/readiness", _json, ct);
        return result ?? new SensorReadinessDto();
    }

    /// <summary>
    /// Sensörün oluşturulmuş metrik önizlemelerini döner.
    /// GET /api/sensors/{sensorId}/lifecycle/metric-previews
    /// </summary>
    public async Task<List<MetricPreviewItemDto>> GetMetricPreviewsAsync(
        Guid sensorId,
        CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MetricPreviewItemDto>>(
            $"api/sensors/{sensorId}/lifecycle/metric-previews", _json, ct);
        return result ?? new();
    }

    /// <summary>
    /// Sensörü Draft'a sıfırlar, doğrulamaları temizler.
    /// POST /api/sensors/{sensorId}/lifecycle/reset
    /// </summary>
    public async Task ResetLifecycleAsync(Guid sensorId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync(
            $"api/sensors/{sensorId}/lifecycle/reset", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    private sealed class TcpSensorCreateResponse
    {
        public Guid SensorId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string LocationId { get; init; } = "default";
        public string SensorType { get; init; } = "Generic";
        public string Topic { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Metrics { get; init; } = string.Empty;
        public string ProtocolType { get; init; } = "TCP";
        public string? IpAddress { get; init; }
        public int? Port { get; init; }
        public int? TimeoutMs { get; init; }
        public int PollingIntervalMs { get; init; }
    }
}
