namespace KtuDeYasPortal.Panel.Domain.Entities;

public class StructureSensor
{
    public Guid    Id             { get; set; }
    public Guid    SensorId       { get; set; }
    public string? SensorLabel    { get; set; }
    public int     SensorOrder    { get; set; }
    public string  DeviceId       { get; set; } = string.Empty;
    public string  Name           { get; set; } = string.Empty;
    public string  SensorType     { get; set; } = string.Empty;
    public string  Unit           { get; set; } = string.Empty;
    public string  Topic          { get; set; } = string.Empty;
    public string  Description    { get; set; } = string.Empty;
    /// <summary>Virgülle ayrılmış metrik key listesi. Örnek: "temperature,humidity"</summary>
    public string  Metrics        { get; set; } = string.Empty;
    public string  ProtocolType   { get; set; } = string.Empty;
    public string  Encoding       { get; set; } = "utf-8";
    public string? IpAddress      { get; set; }
    public int?    Port           { get; set; }
    public int?    TimeoutMs      { get; set; } = 5000;
    public double? ImagePositionX { get; set; }
    public double? ImagePositionY { get; set; }
    public bool    IsActive       { get; set; }
}
