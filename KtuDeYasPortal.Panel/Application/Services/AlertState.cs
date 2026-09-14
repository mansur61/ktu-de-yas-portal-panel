using System.Collections.Concurrent;

namespace KtuDeYasPortal.Panel.Application.Services;

public sealed record PortalAlert(
    string AlarmId,
    string DeviceId,
    string LocationId,
    string Severity,
    string Message,
    DateTime Timestamp,
    string? Metric,
    double? Value,
    double? Threshold);

/// <summary>Portal kullanıcılarının "Merkeze İlet" ile gönderdiği escalation DTO.</summary>
public sealed record PortalEscalation(
    Guid     Id,
    string   AlarmId,
    string   DeviceId,
    string?  LocationId,
    string   Severity,
    string   Message,
    string   EscalatedBy,
    Guid?    StructureId,
    string?  Note,
    DateTime EscalatedAt);

/// <summary>
/// Redis'ten gelen aktif alert'lerin ve portal escalation'larının
/// panel belleğindeki canlı listesi.
/// </summary>
public sealed class AlertState
{
    private readonly ConcurrentDictionary<string, PortalAlert> _alerts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<PortalAlert>?       OnAlertReceived;
    public event Action<PortalEscalation>?  OnEscalationReceived;

    public int ActiveCount => _alerts.Count;

    public IReadOnlyList<PortalAlert> Alerts => _alerts.Values
        .OrderByDescending(alert => alert.Timestamp)
        .Take(100)
        .ToList();

    public void Upsert(PortalAlert alert)
    {
        _alerts[alert.AlarmId] = alert;
        OnAlertReceived?.Invoke(alert);
    }

    /// <summary>
    /// PanelRealtimeForwarder tarafından alert.escalation Kafka/Redis mesajı
    /// geldiğinde çağrılır; LiveAlerts sayfasına iletir.
    /// </summary>
    public void PushEscalation(PortalEscalation escalation) =>
        OnEscalationReceived?.Invoke(escalation);
}
