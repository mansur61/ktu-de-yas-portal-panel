namespace KtuDeYasPortal.Panel.Domain.Entities;

public class StructureSensor
{
    public Guid Id { get; set; }
    public Guid SensorId { get; set; }
    public string? SensorLabel { get; set; }
    public int SensorOrder { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
