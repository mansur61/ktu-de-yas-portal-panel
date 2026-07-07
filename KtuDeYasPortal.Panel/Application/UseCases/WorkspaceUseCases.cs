using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;

namespace KtuDeYasPortal.Panel.Application.UseCases;

public class WorkspaceUseCases
{
    private readonly IEdgeWorkspaceClient  _edgeWorkspace;
    private readonly IWorkspaceClient      _workspaceData;   // timeseries-api (workspace_data)
    private readonly IStructureRepository  _structures;

    public WorkspaceUseCases(
        IEdgeWorkspaceClient edgeWorkspace,
        IWorkspaceClient workspaceData,
        IStructureRepository structures)
    {
        _edgeWorkspace = edgeWorkspace;
        _workspaceData = workspaceData;
        _structures    = structures;
    }

    // ── Yapı listesi (dropdown için) ─────────────────────────────────────────
    public Task<List<Structure>> GetAllStructuresAsync(CancellationToken ct = default) =>
        _structures.GetAllAsync(ct);

    // ── Çalışan workspace'leri listele ───────────────────────────────────────
    public Task<List<WorkspaceInfo>> ListWorkspacesAsync(CancellationToken ct = default) =>
        _edgeWorkspace.ListAsync(ct);

    // ── Workspace oluştur ────────────────────────────────────────────────────
    /// <summary>
    /// TopicScope: sensor/{typeSlug}/{structureId}/#
    /// Araştırmacı bu değeri göremez veya değiştiremez.
    /// </summary>
    public async Task<WorkspaceCreateResult> CreateAsync(
        Structure         structure,
        string            workspaceName,
        string?           customTopicScope = null,
        CancellationToken ct               = default)
    {
        var structureSlug = BuildSlug(structure.Name);
        var typeSlug      = structure.StructureType.ToString().ToLowerInvariant();

        // Yeni şema: sensor/{type}/{structureId}/# — saha izolasyonu
        var topicScope = !string.IsNullOrWhiteSpace(customTopicScope)
            ? customTopicScope
            : $"sensor/{typeSlug}/{structure.Id}/#";

        var req = new CreateWorkspaceRequest
        {
            StructureId   = structure.Id,
            StructureSlug = structureSlug,
            StructureName = structure.Name,
            StructureType = typeSlug,
            TopicScope    = topicScope,
            WorkspaceName = workspaceName.Trim()
        };

        return await _edgeWorkspace.CreateAsync(req, ct);
    }

    // ── Workspace sil ────────────────────────────────────────────────────────
    public Task DeleteAsync(string containerId, CancellationToken ct = default) =>
        _edgeWorkspace.DeleteAsync(containerId, ct);

    // ── Workspace durumu ─────────────────────────────────────────────────────
    public Task<WorkspaceStatus> GetStatusAsync(string containerId, CancellationToken ct = default) =>
        _edgeWorkspace.GetStatusAsync(containerId, ct);

    // ── Workspace yeniden başlat ─────────────────────────────────────────────
    public Task RestartAsync(string containerId, CancellationToken ct = default) =>
        _edgeWorkspace.RestartAsync(containerId, ct);

    // ── Log'ları getir ───────────────────────────────────────────────────────
    public Task<string> GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default) =>
        _edgeWorkspace.GetLogsAsync(containerId, tail, ct);

    // ── TopicScope önizlemesi (UI'da göster) ─────────────────────────────────
    /// <summary>
    /// Yeni şema: sensor/{type}/{structureId}/#
    /// Örn: sensor/baraj/541ae3f0-6e30-4f48-96cb-62e7f9ba21c7/#
    /// </summary>
    public string PreviewTopicScope(Structure? structure)
    {
        if (structure is null) return string.Empty;
        var typeSlug = structure.StructureType.ToString().ToLowerInvariant();
        return $"sensor/{typeSlug}/{structure.Id}/#";
    }

    // ── AI/Analiz sonucunu workspace_data tablosuna kaydet ───────────────────
    /// <summary>
    /// Panel üzerinden test verisi kaydetmek için kullanılır.
    /// Asıl kullanım: Node-RED Dashboard butonu → timeseries-service POST.
    /// </summary>
    public Task<SaveWorkspaceDataResult> SaveWorkspaceDataAsync(
        Guid              workspaceId,
        Guid              structureId,
        string            dataType,
        object            data,
        string?           notes     = null,
        string?           createdBy = null,
        CancellationToken ct        = default)
    {
        return _workspaceData.SaveWorkspaceDataAsync(new SaveWorkspaceDataRequest
        {
            WorkspaceId = workspaceId,
            StructureId = structureId,
            DataType    = dataType,
            Data        = data,
            Notes       = notes,
            CreatedBy   = createdBy ?? "panel"
        }, ct);
    }

    // ── Yapı adından URL-safe slug üret ──────────────────────────────────────
    private static readonly (string From, string To)[] _trMap =
    [
        ("ş","s"),("Ş","s"),("ı","i"),("İ","i"),
        ("ğ","g"),("Ğ","g"),("ü","u"),("Ü","u"),
        ("ö","o"),("Ö","o"),("ç","c"),("Ç","c"),
    ];

    public static string BuildSlug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var s = name.Trim();
        foreach (var (f, t) in _trMap) s = s.Replace(f, t, StringComparison.Ordinal);
        s = new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }
}
