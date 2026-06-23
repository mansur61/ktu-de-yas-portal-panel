using System.Net.Http.Json;
using DeYas.Contracts.Simulation;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public interface IMediaSimulationClient
{
    Task StartAsync(MediaSimRequest request, CancellationToken ct = default);
}

public sealed class MediaSimulationHttpClient : IMediaSimulationClient
{
    private readonly HttpClient _http;

    public MediaSimulationHttpClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("edge-api");
    }

    public async Task StartAsync(MediaSimRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/simulation/media/start", request, ct);
        resp.EnsureSuccessStatusCode();
    }
}
