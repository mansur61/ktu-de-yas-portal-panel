namespace KtuDeYasPortal.Panel.Domain.Entities;

public enum SensorType
{
    Vibration, Temperature, Pressure, Humidity, Camera, Image, Generic
}

public class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string LocationId { get; set; } = "default";
    public SensorType SensorType { get; set; } = SensorType.Generic;
    public string Topic { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string ProtocolType { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }
    public double RotationX { get; set; }
    public double RotationY { get; set; }
    public double RotationZ { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
