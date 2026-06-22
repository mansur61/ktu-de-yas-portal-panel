using System.Net.Http.Json;
using System.Text.Json;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public class StructureHttpRepository : IStructureRepository
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
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
