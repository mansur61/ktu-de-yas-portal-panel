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
    /// <summary>Panel belirler, araştırmacı değiştiremez. Örn: sensor/{type}/{structureId}/#</summary>
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

/// <summary>
/// Panel → timeseries-service /api/workspace-data POST isteği.
/// Node-RED Dashboard butonu bu veriyi workspace içinden gönderir;
/// panel üzerinden de manuel kayıt atılabilir.
/// </summary>
public class SaveWorkspaceDataRequest
{
    public Guid    WorkspaceId { get; set; }
    public Guid    StructureId { get; set; }
    /// <summary>Örn: "anomaly_detection", "vibration_analysis", "manual_note"</summary>
    public string  DataType    { get; set; } = "analysis_result";
    /// <summary>Serbest JSON — JSONB olarak saklanır.</summary>
    public object? Data        { get; set; }
    public string? Notes       { get; set; }
    public string? CreatedBy   { get; set; }
}

public class SaveWorkspaceDataResult
{
    public Guid     Id          { get; set; }
    public Guid     WorkspaceId { get; set; }
    public Guid     StructureId { get; set; }
    public string   DataType    { get; set; } = string.Empty;
    public string   Data        { get; set; } = "{}";
    public DateTime CreatedAt   { get; set; }
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
    /// <summary>AI/analiz sonucunu workspace_data tablosuna kaydet.</summary>
    Task<SaveWorkspaceDataResult> SaveWorkspaceDataAsync(SaveWorkspaceDataRequest req, CancellationToken ct = default);
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
        // "timeseries-api" client — timeseries-service base URL
        _http = factory.CreateClient("timeseries-api");
    }

    public async Task<List<WorkspaceInfo>> ListAsync(CancellationToken ct = default)
    {
        // Workspace listesi edge-api'den gelir, ayrı client kullanılır
        // WorkspaceUseCases bunu edge-api clientıyla handle eder
        throw new NotSupportedException("Use edge-api client for workspace listing.");
    }

    public async Task<WorkspaceCreateResult> CreateAsync(CreateWorkspaceRequest req, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use edge-api client for workspace creation.");
    }

    public async Task DeleteAsync(string containerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use edge-api client for workspace deletion.");
    }

    public async Task<WorkspaceStatus> GetStatusAsync(string containerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use edge-api client for workspace status.");
    }

    public async Task RestartAsync(string containerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use edge-api client for workspace restart.");
    }

    public async Task<string> GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default)
    {
        throw new NotSupportedException("Use edge-api client for workspace logs.");
    }

    public async Task<SaveWorkspaceDataResult> SaveWorkspaceDataAsync(
        SaveWorkspaceDataRequest req,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/workspace-data", req, _json, ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<SaveWorkspaceDataResult>(_json, ct)
               ?? throw new InvalidOperationException("Boş yanıt");
    }

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

// ─── EdgeWorkspaceClient — edge-api için ayrı client ─────────────────────────

public interface IEdgeWorkspaceClient
{
    Task<List<WorkspaceInfo>>   ListAsync(CancellationToken ct = default);
    Task<WorkspaceCreateResult> CreateAsync(CreateWorkspaceRequest req, CancellationToken ct = default);
    Task                        DeleteAsync(string containerId, CancellationToken ct = default);
    Task<WorkspaceStatus>       GetStatusAsync(string containerId, CancellationToken ct = default);
    Task                        RestartAsync(string containerId, CancellationToken ct = default);
    Task<string>                GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default);
}

public sealed class EdgeWorkspaceClient : IEdgeWorkspaceClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public EdgeWorkspaceClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("edge-api");
    }

    public async Task<List<WorkspaceInfo>> ListAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("api/workspaces", ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<List<WorkspaceInfo>>(_json, ct) ?? [];
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
