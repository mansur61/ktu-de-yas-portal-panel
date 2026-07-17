using System.Net.Http.Json;
using System.Text.Json;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public interface IStructureSimulationClient
{
    Task StartAsync(Guid structureId, CancellationToken ct = default);
    Task StartAsync(DeYas.Contracts.Simulation.StructureSimRequest request, CancellationToken ct = default);
    Task StopAsync(Guid structureId, CancellationToken ct = default);
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

        if (!resp.IsSuccessStatusCode)
        {
            // Hata detayını body'den oku
            string detail;
            try
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("detail", out var d))
                    detail = d.GetString() ?? body;
                else if (root.TryGetProperty("error", out var e))
                    detail = e.GetString() ?? body;
                else
                    detail = body;
            }
            catch
            {
                detail = resp.ReasonPhrase ?? "Bilinmeyen hata";
            }

            throw new InvalidOperationException(
                $"[{(int)resp.StatusCode}] {detail}");
        }
    }

    public async Task StartAsync(DeYas.Contracts.Simulation.StructureSimRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/simulation/start/{request.StructureId}", request, ct);

        if (!resp.IsSuccessStatusCode)
        {
            string detail;
            try
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("detail", out var d))
                    detail = d.GetString() ?? body;
                else if (root.TryGetProperty("error", out var e))
                    detail = e.GetString() ?? body;
                else
                    detail = body;
            }
            catch
            {
                detail = resp.ReasonPhrase ?? "Bilinmeyen hata";
            }

            throw new InvalidOperationException(
                $"[{(int)resp.StatusCode}] {detail}");
        }
    }

    public async Task StopAsync(Guid structureId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/simulation/stop/{structureId}", null, ct);

        if (!resp.IsSuccessStatusCode)
        {
            string detail;
            try
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                detail = root.TryGetProperty("error", out var e) ? e.GetString() ?? body : body;
            }
            catch { detail = resp.ReasonPhrase ?? "Bilinmeyen hata"; }

            throw new InvalidOperationException($"[{(int)resp.StatusCode}] {detail}");
        }
    }
}
