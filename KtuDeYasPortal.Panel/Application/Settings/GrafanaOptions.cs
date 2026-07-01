namespace KtuDeYasPortal.Panel.Application.Settings;

/// <summary>
/// Grafana bağlantı ve dashboard ayarları.
/// appsettings.json → "Grafana" bölümü ile eşleşir.
/// </summary>
public sealed class GrafanaOptions
{
    public const string Section = "Grafana";

    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string TimeseriesDashboard { get; set; } = "iot-platform-timeseries";
    public string TimeseriesSlug { get; set; } = "iot-platform-timescaledb";
    public string DefaultTimeRange { get; set; } = "1h";

    /// <summary>
    /// Tek bir Grafana panelini gömülü (d-solo) olarak açar.
    /// </summary>
    public string BuildPanelUrl(
        int panelId,
        string? metricVar = null,
        string? deviceVar = null,
        string? structureId = null,
        string timeRange = "1h",
        string interval = "1 minute")
    {
        // d-solo modunda template variable'lar URL'den set edilmezse boş gelir.
        // $interval boş → COALESCE fallback devreye girer ama yine de explicit set et.
        var url = $"{BaseUrl}/d-solo/{TimeseriesDashboard}/{TimeseriesSlug}"
                + $"?orgId=1&from=now-{timeRange}&to=now"
                + $"&panelId={panelId}"
                + "&timezone=browser&refresh=10s&kiosk"
                + $"&var-interval={Uri.EscapeDataString(interval)}";

        if (!string.IsNullOrWhiteSpace(deviceVar))
            url += $"&var-device={Uri.EscapeDataString(deviceVar)}";

        if (!string.IsNullOrWhiteSpace(metricVar))
            url += $"&var-metric={Uri.EscapeDataString(metricVar)}";

        if (!string.IsNullOrWhiteSpace(structureId))
            url += $"&var-structure={Uri.EscapeDataString(structureId)}";

        return url;
    }

    /// <summary>
    /// Tam dashboard URL'ini oluşturur (yeni sekmede açmak için).
    /// </summary>
    public string BuildDashboardUrl(string? deviceVar = null, string timeRange = "1h")
    {
        var url = $"{BaseUrl}/d/{TimeseriesDashboard}/{TimeseriesSlug}"
                + $"?orgId=1&from=now-{timeRange}&to=now&timezone=browser";

        if (!string.IsNullOrWhiteSpace(deviceVar))
            url += $"&var-device={Uri.EscapeDataString(deviceVar)}";

        return url;
    }
}
