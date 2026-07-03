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

    public StructureHttpRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<List<Structure>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<Structure>>("api/structures", _json, ct);
        return result ?? new();
    }

    public async Task<Structure?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<Structure>($"api/structures/{id}", _json, ct); }
        catch (HttpRequestException) { return null; }
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
            nodeRedUrl = string.IsNullOrWhiteSpace(structure.NodeRedUrl) ? null : structure.NodeRedUrl.Trim(),
            sensorCount = structure.SensorCount,
            isActive = structure.IsActive,
            sensorIds
        };
        var resp = await _http.PostAsJsonAsync("api/structures", payload, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Structure>(_json, ct))!;
    }

    public async Task<Structure> UpdateAsync(Guid id, Structure structure, List<Guid> sensorIds, CancellationToken ct = default)
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
            nodeRedUrl = string.IsNullOrWhiteSpace(structure.NodeRedUrl) ? null : structure.NodeRedUrl.Trim(),
            isActive = structure.IsActive,
            sensorIds
        };
        var resp = await _http.PutAsJsonAsync($"api/structures/{id}", payload, _json, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Structure>(_json, ct))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/structures/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }
}
