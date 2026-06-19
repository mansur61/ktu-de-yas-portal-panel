using System.Net.Http.Json;
using System.Text.Json;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public class TimeseriesRepository : ITimeseriesRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public TimeseriesRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<List<SensorDataPoint>> QueryAsync(string? deviceId, string? locationId, DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default)
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
        var url = $"api/sensor-data?{string.Join('&', q)}";

        try
        {
            var rows = await _http.GetFromJsonAsync<List<ApiRow>>(url, _json, ct);
            if (rows is null) return new();
            return rows.Select(r => new SensorDataPoint(r.DeviceId, r.LocationId, r.Timestamp, r.Metrics)).ToList();
        }
        catch { return new(); }
    }

    private sealed class ApiRow
    {
        public string DeviceId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
    }
}
