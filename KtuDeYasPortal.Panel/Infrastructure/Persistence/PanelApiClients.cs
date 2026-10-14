namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

// Server-side clients authenticate with the panel installation token.
public sealed class PanelApiClients(IHttpClientFactory factory)
{
    public HttpClient CreateClient(string name) => factory.CreateClient(name);
}

public sealed record PanelAccessSnapshot(bool IsAdmin, Guid[] StructureIds);

// The installed management panel always has access to every structure.
public sealed class PanelAccess
{
    public Task<PanelAccessSnapshot> GetAsync(string permission, string menu, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new PanelAccessSnapshot(true, []));
    }
}
