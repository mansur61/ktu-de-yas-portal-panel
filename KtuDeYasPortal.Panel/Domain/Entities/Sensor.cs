namespace KtuDeYasPortal.Panel.Domain.Entities;

public enum SensorType
{
    // ── Temel tipler (test-sensor-worker ve StructureTestData ile uyumlu) ──
    Vibration,
    Accelerometer,
    Temperature,
    Pressure,
    Humidity,
    Lidar,
    Ultrasonic,
    Camera,
    Image,
    Gps,
    Water,
    Wind,
    Generic
}

public enum SensorStatus
{
    Online,
    Warning,
    Alarm,
    Offline
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
    /// <summary>Bu sensörün yayınladığı metrik key'leri (CSV). Örnek: "temperature,humidity"</summary>
    public string Metrics { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string ProtocolType { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }
    public double RotationX { get; set; }
    public double RotationY { get; set; }
    public double RotationZ { get; set; }

    // ── Görsel üzerinde pozisyon (2D pin) ──
    public double? ImagePositionX { get; set; }
    public double? ImagePositionY { get; set; }

    // ── Anlık durum (runtime, DB'ye kaydedilmez) ──
    public SensorStatus Status { get; set; } = SensorStatus.Offline;
    public double? LastValue { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? AlertMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
