using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

// ─── DTOs — Edge Service WorkspaceModels.cs ile birebir eşleşir ──────────────

public class CreateWorkspaceRequest
{
    public Guid   StructureId   { get; set; }
    public string StructureSlug { get; set; } = string.Empty;
    public string StructureName { get; set; } = string.Empty;
    public string StructureType { get; set; } = string.Empty;
    /// <summary>Panel belirler, araştırmacı değiştiremez. Örn: sensor.dam.structure-15.*</summary>
    public string TopicScope    { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
}

public class WorkspaceInfo
{
    public string   ContainerId   { get; set; } = string.Empty;
    public string   ContainerName { get; set; } = string.Empty;
    public string   WorkspaceName { get; set; } = string.Empty;
    public string   StructureSlug { get; set; } = string.Empty;
    public Guid     StructureId   { get; set; }
    public string   TopicScope    { get; set; } = string.Empty;
    public string   Status        { get; set; } = string.Empty;
    public string   EditorUrl     { get; set; } = string.Empty;
    public string   DashboardUrl  { get; set; } = string.Empty;
    public int      HostPort      { get; set; }
    public DateTime CreatedAt     { get; set; }
}

public class WorkspaceCreateResult
{
    public string  ContainerId   { get; set; } = string.Empty;
    public string  ContainerName { get; set; } = string.Empty;
    public string  EditorUrl     { get; set; } = string.Empty;
    public string  DashboardUrl  { get; set; } = string.Empty;
    public int     HostPort      { get; set; }
    public bool    Success       { get; set; }
    public string? Error         { get; set; }
}

public class WorkspaceStatus
{
    public string    ContainerId   { get; set; } = string.Empty;
    public string    ContainerName { get; set; } = string.Empty;
    public string    Status        { get; set; } = string.Empty;
    public bool      IsRunning     { get; set; }
    public bool      IsHealthy     { get; set; }
    public string?   EditorUrl     { get; set; }
    public DateTime? StartedAt     { get; set; }
}

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IWorkspaceClient
{
    Task<List<WorkspaceInfo>>     ListAsync(CancellationToken ct = default);
    Task<WorkspaceCreateResult>   CreateAsync(CreateWorkspaceRequest req, CancellationToken ct = default);
    Task                          DeleteAsync(string containerId, CancellationToken ct = default);
    Task<WorkspaceStatus>         GetStatusAsync(string containerId, CancellationToken ct = default);
    Task                          RestartAsync(string containerId, CancellationToken ct = default);
    Task<string>                  GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public sealed class WorkspaceHttpClient : IWorkspaceClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public WorkspaceHttpClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("edge-api");
    }

    public async Task<List<WorkspaceInfo>> ListAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("api/workspaces", ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<List<WorkspaceInfo>>(_json, ct)
               ?? new List<WorkspaceInfo>();
    }

    public async Task<WorkspaceCreateResult> CreateAsync(CreateWorkspaceRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/workspaces", req, _json, ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<WorkspaceCreateResult>(_json, ct)
               ?? new WorkspaceCreateResult { Success = false, Error = "Boş yanıt" };
    }

    public async Task DeleteAsync(string containerId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/workspaces/{containerId}", ct);
        await EnsureSuccessAsync(resp, ct);
    }

    public async Task<WorkspaceStatus> GetStatusAsync(string containerId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/workspaces/{containerId}", ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<WorkspaceStatus>(_json, ct)
               ?? new WorkspaceStatus { Status = "unknown" };
    }

    public async Task RestartAsync(string containerId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/workspaces/{containerId}/restart", null, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    public async Task<string> GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"api/workspaces/{containerId}/logs?tail={tail}", ct);
        await EnsureSuccessAsync(resp, ct);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("logs", out var logs)
            ? logs.GetString() ?? string.Empty
            : string.Empty;
    }

    // ── Ortak hata çıkarma ────────────────────────────────────────────────────
    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        string detail;
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var d))      detail = d.GetString() ?? body;
            else if (root.TryGetProperty("error", out var e))  detail = e.GetString() ?? body;
            else                                                detail = body;
        }
        catch { detail = resp.ReasonPhrase ?? "Bilinmeyen hata"; }
        throw new InvalidOperationException($"[{(int)resp.StatusCode}] {detail}");
    }
}
