using System.Collections.Concurrent;

namespace KtuDeYasPortal.Panel.Application.Services;

public sealed record FieldAlert(
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
    private readonly ConcurrentDictionary<string, FieldAlert> _alerts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<FieldAlert>?        OnFieldAlertReceived;
    public event Action<PortalEscalation>?  OnEscalationReceived;

    public int ActiveCount => _alerts.Count;

    public IReadOnlyList<FieldAlert> FieldAlerts => _alerts.Values
        .OrderByDescending(alert => alert.Timestamp)
        .Take(100)
        .ToList();

    public void UpsertFieldAlert(FieldAlert alert)
    {
        _alerts[alert.AlarmId] = alert;
        OnFieldAlertReceived?.Invoke(alert);
    }

    /// <summary>
    /// PanelRealtimeForwarder tarafından paydaş eskalasyon mesajı geldiğinde
    /// çağrılır; Paydaş Alertleri sayfasına iletir.
    /// </summary>
    public void PushEscalation(PortalEscalation escalation) =>
        OnEscalationReceived?.Invoke(escalation);
}
