using System.Net.Http.Json;
using System.Text.Json;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public interface IStructureSimulationClient
{
    Task StartAsync(string edgeApiUrl, Guid structureId, CancellationToken ct = default);
    Task StopAsync(string edgeApiUrl, Guid structureId, CancellationToken ct = default);
}

public sealed class StructureSimulationHttpClient : IStructureSimulationClient
{
    private readonly IHttpClientFactory _factory;

    public StructureSimulationHttpClient(IHttpClientFactory factory) => _factory = factory;

    public async Task StartAsync(string edgeApiUrl, Guid structureId, CancellationToken ct = default)
    {
        using var http = CreateEdgeClient(edgeApiUrl);
        var resp = await http.PostAsync($"api/edge/lifecycle/start/{structureId}", null, ct);

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

    public async Task StopAsync(string edgeApiUrl, Guid structureId, CancellationToken ct = default)
    {
        using var http = CreateEdgeClient(edgeApiUrl);
        var resp = await http.PostAsync($"api/edge/lifecycle/stop/{structureId}", null, ct);

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

    private HttpClient CreateEdgeClient(string edgeApiUrl)
    {
        if (!Uri.TryCreate(edgeApiUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress)
            || (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Yapının Edge URL değeri geçerli bir http/https adresi değil.");

        var http = _factory.CreateClient("edge-api");
        http.BaseAddress = baseAddress;
        return http;
    }
}
