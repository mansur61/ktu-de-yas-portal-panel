using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

/// <summary>
/// API'den gelen structureType değerini StructureType enum'una çevirir.
/// Hem Türkçe ("Baraj", "Köprü") hem İngilizce ("Dam", "Bridge") değerleri kabul eder.
/// Bilinmeyen değer → Other.
/// </summary>
internal sealed class StructureTypeConverter : JsonConverter<StructureType>
{
    public override StructureType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? string.Empty;
        return value.ToLowerInvariant() switch
        {
            "dam"      or "baraj"  => StructureType.Dam,
            "bridge"   or "köprü" or "kopru" => StructureType.Bridge,
            "tunnel"   or "tünel" or "tunel" => StructureType.Tunnel,
            "building" or "bina"             => StructureType.Building,
            _ => Enum.TryParse<StructureType>(value, ignoreCase: true, out var parsed)
                 ? parsed
                 : StructureType.Other
        };
    }

    public override void Write(Utf8JsonWriter writer, StructureType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

public class StructureHttpRepository : IStructureRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new StructureTypeConverter(),          // Türkçe/İngilizce her iki formatı kabul eder
            new JsonStringEnumConverter()          // diğer enum'lar için fallback
        }
    };

    public StructureHttpRepository(KtuDeYasPortal.Panel.Infrastructure.Persistence.PanelApiClients factory)
    {
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<List<Structure>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<Structure>>("api/structures", _json, ct);
        return result ?? new();
    }

    /// <summary>Yapıları sensörleriyle birlikte tek istekte çeker (include=sensors).</summary>
    public async Task<List<Structure>> GetAllWithSensorsAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<Structure>>("api/structures?include=sensors", _json, ct);
        return result ?? new();
    }

    public async Task<Structure?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<Structure>($"api/structures/{id}", _json, ct); }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<Structure> CreateAsync(Structure structure, List<Guid> sensorIds, CancellationToken ct = default)
    {
        var payload = new
        {
            name = structure.Name,
            description = structure.Description,
            province = structure.Province,
            district = structure.District,
            latitude = structure.Latitude,
            longitude = structure.Longitude,
            address = structure.Address,
            structureType = structure.StructureType.ToString(),
            imageUrl = structure.ImageUrl,
            modelUrl = structure.ModelUrl,
            nodeRedUrl = string.IsNullOrWhiteSpace(structure.NodeRedUrl) ? null : structure.NodeRedUrl.Trim(),
            edgeApiUrl = string.IsNullOrWhiteSpace(structure.EdgeApiUrl) ? null : structure.EdgeApiUrl.Trim(),
            edgeConfigJson = structure.EdgeConfigJson,
            siteEnvironmentJson = structure.SiteEnvironmentJson,
            sensorCount = structure.SensorCount,
            isActive = structure.IsActive,
            sensorIds
        };
        var resp = await _http.PostAsJsonAsync("api/structures", payload, _json, ct);
        await EnsureStructureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<Structure>(_json, ct))!;
    }

    public async Task<Structure> UpdateAsync(Guid id, Structure structure, List<Guid> sensorIds, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/structures/{id}", BuildUpdatePayload(structure, sensorIds), _json, ct);
        await EnsureStructureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<Structure>(_json, ct))!;
    }

    public async Task<Structure> UpdateMetadataAsync(Guid id, Structure structure, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/structures/{id}", BuildUpdatePayload(structure, null), _json, ct);
        await EnsureStructureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<Structure>(_json, ct))!;
    }

    private static object BuildUpdatePayload(Structure structure, List<Guid>? sensorIds) => new
    {
        name = structure.Name,
        description = structure.Description,
        province = structure.Province,
        district = structure.District,
        latitude = structure.Latitude,
        longitude = structure.Longitude,
        address = structure.Address,
        structureType = structure.StructureType.ToString(),
        imageUrl = structure.ImageUrl,
        modelUrl = structure.ModelUrl,
        nodeRedUrl = string.IsNullOrWhiteSpace(structure.NodeRedUrl) ? null : structure.NodeRedUrl.Trim(),
        edgeApiUrl = string.IsNullOrWhiteSpace(structure.EdgeApiUrl) ? null : structure.EdgeApiUrl.Trim(),
        edgeConfigJson = structure.EdgeConfigJson,
        siteEnvironmentJson = structure.SiteEnvironmentJson,
        isActive = structure.IsActive,
        sensorIds
    };

    private static async Task EnsureStructureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? message = null;
        try {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (body.RootElement.TryGetProperty("error", out var error)) message = error.GetString();
        } catch (JsonException) { }
        throw new HttpRequestException(message ?? $"Yapı işlemi tamamlanamadı (HTTP {(int)response.StatusCode}).", null, response.StatusCode);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync($"api/structures/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return;
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Merkez API silme isteğini reddetti (HTTP {(int)resp.StatusCode}). Timeseries servis logunu kontrol edin.", null, resp.StatusCode);
    }
}
