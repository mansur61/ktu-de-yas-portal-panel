using System.Net.Http.Headers;
using System.Security.Claims;
using DeYas.Contracts.Authorization;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

// Scoped to the Blazor circuit. Never put a user's bearer token in a pooled handler.
public sealed class PanelApiClients(IHttpClientFactory factory, IHttpContextAccessor accessor)
{
    private readonly string? _token = accessor.HttpContext?.User.FindFirstValue("access_token");
    public HttpClient CreateClient(string name)
    {
        var client = factory.CreateClient(name);
        if (!string.IsNullOrWhiteSpace(_token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return client;
    }
}

public sealed record PanelAccessSnapshot(bool IsAdmin, Guid[] StructureIds);

public sealed class PanelAccess(PanelApiClients clients)
{
    public async Task<PanelAccessSnapshot> GetAsync(string permission, string menu, CancellationToken ct = default)
    {
        var http = clients.CreateClient("auth-api");
        return await http.GetFromJsonAsync<PanelAccessSnapshot>(
            $"api/structure-permissions/access?permission={Uri.EscapeDataString(permission)}&menu={Uri.EscapeDataString(menu)}", ct) ?? new(false, []);
    }
}
