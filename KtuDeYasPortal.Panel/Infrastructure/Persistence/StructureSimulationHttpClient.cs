using System.Net.Http.Json;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public interface IStructureSimulationClient
{
    Task StartAsync(Guid structureId, CancellationToken ct = default);
}

public sealed class StructureSimulationHttpClient : IStructureSimulationClient
{
    private readonly HttpClient _http;

    public StructureSimulationHttpClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("edge-api");
    }

    public async Task StartAsync(Guid structureId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/simulation/start/{structureId}", null, ct);
        resp.EnsureSuccessStatusCode();
    }
}
