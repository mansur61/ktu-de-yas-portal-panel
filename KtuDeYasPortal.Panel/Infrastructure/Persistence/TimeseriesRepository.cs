using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public class TimeseriesRepository : ITimeseriesRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public TimeseriesRepository(KtuDeYasPortal.Panel.Infrastructure.Persistence.PanelApiClients factory)
    {
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<SensorDataPoint?> GetLatestAsync(
        string deviceId,
        Guid structureId,
        CancellationToken ct = default)
    {
        var url = $"api/sensor-data/latest/{Uri.EscapeDataString(deviceId)}?structureId={structureId:D}";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var row = await response.Content.ReadFromJsonAsync<ApiRow>(_json, ct)
            ?? throw new InvalidDataException("Timeseries API returned an empty latest-data response.");
        return ToDataPoint(row);
    }

    public async Task<List<SensorDataPoint>> QueryAsync(
        string? deviceId,
        string? locationId,
        DateTime from,
        DateTime to,
        int limit = 1000,
        string? structureId = null,
        CancellationToken ct = default)
    {
        var q = new List<string>
        {
            $"from={Uri.EscapeDataString(from.ToString("o"))}",
            $"to={Uri.EscapeDataString(to.ToString("o"))}",
            $"limit={limit}"
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
            q.Add($"deviceId={Uri.EscapeDataString(deviceId)}");
        if (!string.IsNullOrWhiteSpace(locationId))
            q.Add($"locationId={Uri.EscapeDataString(locationId)}");
        if (!string.IsNullOrWhiteSpace(structureId))
            q.Add($"structureId={Uri.EscapeDataString(structureId)}");
        var url = $"api/sensor-data?{string.Join('&', q)}";

        var rows = await _http.GetFromJsonAsync<List<ApiRow>>(url, _json, ct)
            ?? throw new InvalidDataException("Timeseries API returned an empty query response.");
        return rows.Select(ToDataPoint).ToList();
    }

    private static SensorDataPoint ToDataPoint(ApiRow row) =>
        new(row.DeviceId, row.LocationId, row.Timestamp, row.Metrics ?? new());

    private sealed class ApiRow
    {
        public string DeviceId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double>? Metrics { get; set; } = new();
    }
}
