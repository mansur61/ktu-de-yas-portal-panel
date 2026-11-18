using System.Net.Http.Headers;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

internal sealed class CentralApiBearerHandler(IConfiguration configuration) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = configuration["Panel:TokenPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("Merkez panel oturumu hazır değil. Auth servisini ve Panel:TokenPath ayarını kontrol edin.");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using var reader = new StreamReader(file);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await reader.ReadToEndAsync(ct));
        return await base.SendAsync(request, ct);
    }
}
