using System.Net.Http.Json;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public sealed record LiveCameraSession(string SessionId, string Path, string WhepUrl,
    string ViewerToken, DateTimeOffset ExpiresAt, int SessionSeconds);

public interface ILiveCameraClient
{
    Task<LiveCameraSession> StartAsync(Guid structureId, string deviceId, CancellationToken ct = default);
    Task CaptureAsync(Guid structureId, string deviceId, int seconds, string kind, CancellationToken ct = default);
    Task StopAsync(Guid structureId, string sessionId, CancellationToken ct = default);
}

public sealed class LiveCameraHttpClient(PanelApiClients factory) : ILiveCameraClient
{
    private readonly HttpClient _http = factory.CreateClient("video-api");

    public async Task<LiveCameraSession> StartAsync(Guid structureId, string deviceId, CancellationToken ct = default)
    {
        using var response = await _http.PostAsync(
            $"api/live-cameras/{structureId:D}/{Uri.EscapeDataString(deviceId)}/sessions", null, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Canlı izleme başlatılamadı ({(int)response.StatusCode}).");
        return await response.Content.ReadFromJsonAsync<LiveCameraSession>(ct)
            ?? throw new InvalidOperationException("Canlı izleme oturum bilgisi alınamadı.");
    }

    public async Task CaptureAsync(Guid structureId, string deviceId, int seconds, string kind, CancellationToken ct = default)
    {
        using var captureClient = factory.CreateClient("video-api");
        captureClient.Timeout = TimeSpan.FromSeconds(200);
        using var response = await captureClient.PostAsJsonAsync(
            $"api/live-cameras/{structureId:D}/{Uri.EscapeDataString(deviceId)}/recordings", new { seconds, kind }, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Kayıt alınamadı ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");
    }

    public async Task StopAsync(Guid structureId, string sessionId, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync(
            $"api/live-cameras/{structureId:D}/sessions/{Uri.EscapeDataString(sessionId)}", ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Canlı izleme kapatılamadı ({(int)response.StatusCode}).");
    }
}
