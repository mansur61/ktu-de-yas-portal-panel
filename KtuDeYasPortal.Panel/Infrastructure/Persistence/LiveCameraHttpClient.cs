using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        using var response = await SendAsync(_http, client => client.PostAsync(
            $"api/live-cameras/{structureId:D}/{Uri.EscapeDataString(deviceId)}/sessions", null, ct));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ErrorMessageAsync("Canlı izleme", response, ct));
        return await response.Content.ReadFromJsonAsync<LiveCameraSession>(ct)
            ?? throw new InvalidOperationException("Canlı izleme oturum bilgisi alınamadı.");
    }

    public async Task CaptureAsync(Guid structureId, string deviceId, int seconds, string kind, CancellationToken ct = default)
    {
        using var captureClient = factory.CreateClient("video-api");
        captureClient.Timeout = TimeSpan.FromSeconds(200);
        using var response = await SendAsync(captureClient, client => client.PostAsJsonAsync(
            $"api/live-cameras/{structureId:D}/{Uri.EscapeDataString(deviceId)}/recordings", new { seconds, kind }, ct));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ErrorMessageAsync(kind == "image" ? "Fotoğraf" : "Video kaydı", response, ct));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send(client);
        }
        catch (HttpRequestException ex)
        {
            var address = client.BaseAddress?.ToString().TrimEnd('/') ?? "tanımsız adres";
            throw new InvalidOperationException(
                $"Merkez video servisine ({address}) ulaşılamadı. video-service çalışıyor mu ve Services__VideoApi adresi doğru mu?", ex);
        }
    }

    private static async Task<string> ErrorMessageAsync(string action, HttpResponseMessage response, CancellationToken ct)
    {
        var detail = await ReadErrorAsync(response, ct);
        if (!string.IsNullOrWhiteSpace(detail))
            return $"{action} başlatılamadı: {detail}";
        return CaptureErrorMessage(action, response.StatusCode);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            using var json = JsonDocument.Parse(raw);
            foreach (var field in new[] { "error", "detail", "message", "title" })
                if (json.RootElement.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    private static string CaptureErrorMessage(string assetName, HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"{assetName} almak için yetkiniz bulunmuyor.",
            HttpStatusCode.Conflict =>
                $"{assetName} şu anda oluşturulamadı. Kamera yayını hazırlanıyor olabilir; 10 saniye bekleyip yeniden deneyin. Sorun sürerse kamera ve saha bağlantısını kontrol edin.",
            HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout =>
                $"Kamera zamanında yanıt vermedi. 10 saniye bekleyip yeniden deneyin. Sorun sürerse kamera ve saha bağlantısını kontrol edin.",
            _ =>
                $"{assetName} şu anda oluşturulamadı. Birkaç saniye sonra yeniden deneyin; sorun sürerse sistem yöneticinize bildirin."
        };
    }

    public async Task StopAsync(Guid structureId, string sessionId, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync(
            $"api/live-cameras/{structureId:D}/sessions/{Uri.EscapeDataString(sessionId)}", ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Canlı izleme kapatılamadı ({(int)response.StatusCode}).");
    }
}
