using System.Net.Http.Json;
using System.Text.Json;

namespace KtuDeYasPortal.Panel.Application.Services;

/// <summary>
/// Resolves the newest published, versioned DE-YAS images immediately before a
/// field deployment package is generated.  The package stays reproducible:
/// the resolved tags are written into its .env file rather than relying on a
/// mutable Docker tag at the field site.
/// </summary>
public sealed class DockerHubDeploymentImageResolver(IHttpClientFactory clients)
{
    private const string Namespace = "mansur61";

    private static readonly IReadOnlyDictionary<string, string> ImageRepositories =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NODERED_IMAGE"] = "ktu-nodered",
            ["EDGE_LAYER_IMAGE"] = "ktu-edge-layer",
            ["TIMESERIES_IMAGE"] = "ktu-timeseries-service",
            ["SAHA_BRIDGE_IMAGE"] = "ktu-saha-bridge",
            ["SAHA_MEDIA_BRIDGE_IMAGE"] = "ktu-saha-media-bridge",
            ["PORTAL_IMAGE"] = "ktu-deyas-portal"
        };

    public async Task<IReadOnlyDictionary<string, string>> ResolveLatestAsync(CancellationToken cancellationToken = default)
    {
        var resolutions = await Task.WhenAll(ImageRepositories.Select(async image =>
            (EnvironmentKey: image.Key, Image: await ResolveImageAsync(image.Value, cancellationToken))));

        return resolutions.ToDictionary(
            resolution => resolution.EnvironmentKey,
            resolution => resolution.Image,
            StringComparer.Ordinal);
    }

    private async Task<string> ResolveImageAsync(string repository, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient("docker-hub");
        using var response = await client.GetAsync(
            $"v2/repositories/{Namespace}/{repository}/tags?page_size=100&ordering=last_updated",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DockerHubTagsResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        var tag = payload?.Results?
            .Where(candidate => candidate.Name.StartsWith("dsi-", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.LastUpdated)
            .FirstOrDefault();

        if (tag is null)
            throw new InvalidOperationException($"Docker Hub'da {Namespace}/{repository} için yayınlanmış dsi-* etiketi bulunamadı.");

        return $"{Namespace}/{repository}:{tag.Name}";
    }

    private sealed record DockerHubTagsResponse(IReadOnlyList<DockerHubTag>? Results);
    private sealed record DockerHubTag(string Name, DateTimeOffset LastUpdated);
}
