using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;

namespace KtuDeYasPortal.Panel.Application.UseCases;

public class WorkspaceUseCases
{
    private readonly IWorkspaceClient      _workspace;
    private readonly IStructureRepository  _structures;

    public WorkspaceUseCases(IWorkspaceClient workspace, IStructureRepository structures)
    {
        _workspace  = workspace;
        _structures = structures;
    }

    // ── Yapı listesi (dropdown için) ─────────────────────────────────────────
    public Task<List<Structure>> GetAllStructuresAsync(CancellationToken ct = default) =>
        _structures.GetAllAsync(ct);

    // ── Çalışan workspace'leri listele ───────────────────────────────────────
    public Task<List<WorkspaceInfo>> ListWorkspacesAsync(CancellationToken ct = default) =>
        _workspace.ListAsync(ct);

    // ── Workspace oluştur ────────────────────────────────────────────────────
    /// <summary>
    /// Panel TopicScope'u otomatik üretir: sensor.{typeSlug}.{structureSlug}.*
    /// Araştırmacı bu değeri görmez veya değiştiremez.
    /// </summary>
    public async Task<WorkspaceCreateResult> CreateAsync(
        Structure       structure,
        string          workspaceName,
        string?         customTopicScope = null,
        CancellationToken ct             = default)
    {
        var structureSlug = BuildSlug(structure.Name);
        var typeSlug      = structure.StructureType.ToString().ToLowerInvariant();

        // TopicScope: panel belirlerse kullan, yoksa otomatik üret
        var topicScope = !string.IsNullOrWhiteSpace(customTopicScope)
            ? customTopicScope
            : $"sensor.{typeSlug}.{structureSlug}.*";

        var req = new CreateWorkspaceRequest
        {
            StructureId   = structure.Id,
            StructureSlug = structureSlug,
            StructureName = structure.Name,
            StructureType = typeSlug,
            TopicScope    = topicScope,
            WorkspaceName = workspaceName.Trim()
        };

        return await _workspace.CreateAsync(req, ct);
    }

    // ── Workspace sil ────────────────────────────────────────────────────────
    public Task DeleteAsync(string containerId, CancellationToken ct = default) =>
        _workspace.DeleteAsync(containerId, ct);

    // ── Workspace durumu ─────────────────────────────────────────────────────
    public Task<WorkspaceStatus> GetStatusAsync(string containerId, CancellationToken ct = default) =>
        _workspace.GetStatusAsync(containerId, ct);

    // ── Workspace yeniden başlat ─────────────────────────────────────────────
    public Task RestartAsync(string containerId, CancellationToken ct = default) =>
        _workspace.RestartAsync(containerId, ct);

    // ── Log'ları getir ───────────────────────────────────────────────────────
    public Task<string> GetLogsAsync(string containerId, int tail = 100, CancellationToken ct = default) =>
        _workspace.GetLogsAsync(containerId, tail, ct);

    // ── TopicScope önizlemesi (UI'da göster) ─────────────────────────────────
    public string PreviewTopicScope(Structure? structure)
    {
        if (structure is null) return string.Empty;
        var typeSlug      = structure.StructureType.ToString().ToLowerInvariant();
        var structureSlug = BuildSlug(structure.Name);
        return $"sensor.{typeSlug}.{structureSlug}.*";
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
