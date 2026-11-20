using System.Text.RegularExpressions;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

internal static class PanelModelContent
{
    private const string Prefix = "/api/image-assets/model-content/structure-models/";
    private static readonly Regex Key = new(@"\A(?:defaults|[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12})/[0-9a-f]{32}\.(?:glb|gltf|obj|fbx)\z");

    internal static string? BrowserUrl(string? url)
    {
        if (url is null) return null;
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
            ? uri.AbsolutePath : url;
        return path.StartsWith(Prefix, StringComparison.Ordinal) && Key.IsMatch(path[Prefix.Length..])
            ? path : url;
    }

    internal static void MapPanelModelContent(this WebApplication app)
    {
        app.MapGet(Prefix + "{**key}", async (string key, HttpContext context, IHttpClientFactory clients, CancellationToken ct) =>
        {
            if (!Key.IsMatch(key))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            using var client = clients.CreateClient("model-content");
            using var response = await client.GetAsync(Prefix + key, HttpCompletionOption.ResponseHeadersRead, ct);
            context.Response.StatusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode) return;

            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            context.Response.ContentLength = response.Content.Headers.ContentLength;
            context.Response.Headers.CacheControl = "private, max-age=86400";
            await response.Content.CopyToAsync(context.Response.Body, ct);
        }).RequireAuthorization("platform.admin");
    }
}
