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

    // ── Yeni: Yapı Tipi ve Görsel ──
    public StructureType StructureType { get; set; } = StructureType.Other;
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<StructureSensor> Sensors { get; set; } = new();
}
