namespace KtuDeYasPortal.Panel.Domain.Entities;

public class Structure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SensorCount { get; set; }
    public string Province { get; set; } = string.Empty;
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Yapı Tipi ve Görsel ────────────────────────────────────────────────
    public StructureType StructureType { get; set; } = StructureType.Other;
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Yapıya özgü Node-RED instance URL'i.
    /// Null → ortak instance (appsettings NodeRed:BaseUrl).
    /// Docker/K8s: "http://nodered-{slug}:1880"
    /// </summary>
    public string? NodeRedUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<StructureSensor> Sensors { get; set; } = new();

    // ── Prod Lifecycle Aggregate (API'den dolduruluyor, prod switch aktifken anlamlı) ─
    /// <summary>Yapıya bağlı toplam aktif sensör sayısı.</summary>
    public int TotalSensorCount             { get; set; }
    /// <summary>LifecycleStatus = Ready/Stopped/Running olan sensör sayısı.</summary>
    public int ReadySensorCount             { get; set; }
    /// <summary>Doğrulama eksik sensör sayısı.</summary>
    public int NotReadySensorCount          { get; set; }
    /// <summary>Prod çalıştırma komutuna dahil edilebilecek sensör sayısı.</summary>
    public int StartableSensorCount         { get; set; }
    /// <summary>Şu an Running/Reconnecting durumundaki sensör sayısı.</summary>
    public int RunningSensorCount           { get; set; }
    /// <summary>Bağlantı testi eksik/başarısız sensör sayısı.</summary>
    public int ConnectionPendingSensorCount { get; set; }
    /// <summary>Parser doğrulaması eksik/başarısız sensör sayısı.</summary>
    public int ParserPendingSensorCount     { get; set; }
    /// <summary>Metrik önizlemesi eksik sensör sayısı.</summary>
    public int MetricPreviewPendingCount    { get; set; }
    /// <summary>StartableSensorCount > 0 ise true — Çalıştır butonunun aktiflik kuralı.</summary>
    public bool CanStart                    { get; set; }
}
