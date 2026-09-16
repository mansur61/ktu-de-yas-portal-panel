using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

/// <summary>
/// alert-config-service /api/alert-rules endpoint'lerine HTTP üzerinden erişim.
/// Panel doğrudan DB'ye bağlanmaz.
/// </summary>
public class AlertRuleHttpRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AlertRuleHttpRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("alert-config-api");
    }

    public async Task<List<AlertRuleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<AlertRuleDto>>("api/alert-rules", _json, ct);
        return result ?? [];
    }

    public async Task<AlertRuleDto?> CreateAsync(AlertRuleDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/alert-rules", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AlertRuleDto>(_json, ct);
    }

    public async Task<AlertRuleDto?> UpdateAsync(Guid id, AlertRuleDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/alert-rules/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AlertRuleDto>(_json, ct);
    }

    public async Task ToggleAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsync($"api/alert-rules/{id}/toggle", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/alert-rules/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }
}

public class AlertRuleDto
{
    public Guid     Id         { get; set; }
    public string   DeviceId   { get; set; } = string.Empty;
    public string   Metric     { get; set; } = string.Empty;
    public string   Operator   { get; set; } = ">";
    public double   Threshold  { get; set; }
    public string   Severity   { get; set; } = "warning";
    public bool     IsActive   { get; set; } = true;
    /// <summary>"timeseries" | "video" | "lidar" | "media" | "*"</summary>
    public string   DataSource { get; set; } = "timeseries";
    public DateTime CreatedAt  { get; set; }
    public DateTime UpdatedAt  { get; set; }
}
