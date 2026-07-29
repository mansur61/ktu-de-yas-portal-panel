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

/// <summary>Redis'ten gelen aktif alert'lerin panel belleğindeki canlı listesi.</summary>
public sealed class AlertState
{
    private readonly ConcurrentDictionary<string, PortalAlert> _alerts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<PortalAlert>? OnAlertReceived;

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
}
