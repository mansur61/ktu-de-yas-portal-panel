namespace KtuDeYasPortal.Panel.Domain.Entities;

using DeYas.Contracts.Integration;
using DeYas.Contracts.Sensors;

// SensorType is now imported from DeYas.Contracts.Sensors
// This ensures consistency across Panel, Portal, and Edge Layer

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
    public List<SensorMapping>? Mappings { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string ProtocolType { get; set; } = string.Empty;
    public Guid? ConnectionId { get; set; }
    public string? SerialPort { get; set; }
    public int? BaudRate { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public int? TimeoutMs { get; set; } = 5000;
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Kafka'ya kaç milisaniyede bir telemetri mesajı gönderileceği.
    /// test-sensor-worker bu değeri kullanarak sensöre özgü publish hızını belirler.
    /// Varsayılan: 5000 ms (5 saniyede bir).
    /// </summary>
    public int TelemetryIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Her telemetri penceresinde cihaz başı gönderilecek ölçüm sayısı.
    /// test-sensor-worker --batch-size ile aynı mantık.
    /// Örnek: TelemetryIntervalMs=1000 + TelemetryBatchSize=200 → saniyede 200 ölçüm.
    /// Varsayılan: 1.
    /// </summary>
    public int TelemetryBatchSize { get; set; } = 1;
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
