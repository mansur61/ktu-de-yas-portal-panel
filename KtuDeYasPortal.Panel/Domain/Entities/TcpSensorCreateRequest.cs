namespace KtuDeYasPortal.Panel.Domain.Entities;

public sealed class TcpSensorCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? LocationId { get; set; }
    public string? SensorType { get; set; }
    public string? Topic { get; set; }
    public string? Description { get; set; }
    public string ConnectionMode { get; set; } = "Client";
    public string Profile { get; set; } = "GenericTcp";
    public string Encoding { get; set; } = "utf-8";
    public bool ReconnectEnabled { get; set; } = true;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public int PollingIntervalMs { get; set; } = 1000;
    public List<string> DiscoveredMetrics { get; set; } = [];
}
