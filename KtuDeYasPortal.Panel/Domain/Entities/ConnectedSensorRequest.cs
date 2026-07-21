namespace KtuDeYasPortal.Panel.Domain.Entities;

/// <summary>
/// POST /api/structures/{structureId}/sensors/connected — request body.
/// Backend transaction içinde sensors + tcp_sensor_configurations + structure_sensors oluşturur.
/// Herhangi biri başarısız olursa tüm işlem geri alınır.
/// </summary>
public sealed class ConnectedSensorRequest
{
    // ── Sensör bilgileri ────────────────────────────────────────────────────
    public string  Name               { get; set; } = string.Empty;
    public string  DeviceId           { get; set; } = string.Empty;
    public string  LocationId         { get; set; } = "default";
    public string  SensorType         { get; set; } = "Generic";
    public string  Topic              { get; set; } = string.Empty;
    public string  Unit               { get; set; } = string.Empty;
    public string  Description        { get; set; } = string.Empty;
    public int     TelemetryIntervalMs { get; set; } = 5000;
    public int     TelemetryBatchSize  { get; set; } = 1;

    // ── Yapı ilişkisi ───────────────────────────────────────────────────────
    public string? SensorLabel { get; set; }
    public int     SensorOrder { get; set; } = 0;

    // ── TCP bağlantı bilgileri ───────────────────────────────────────────────
    public string IpAddress        { get; set; } = string.Empty;
    public int    Port             { get; set; } = 502;
    public string ConnectionMode   { get; set; } = "Client";
    public string Profile          { get; set; } = "Unknown";
    public int    TimeoutMs        { get; set; } = 5000;
    public int    PollingIntervalMs { get; set; } = 1000;
    public string Encoding         { get; set; } = "utf-8";
    public bool   ReconnectEnabled { get; set; } = true;
}

/// <summary>
/// POST /api/structures/{id}/sensors/connected — başarılı response.
/// Backend'deki AddConnectedSensorResponse ile field-for-field eşleşir.
/// </summary>
public sealed class ConnectedSensorResponse
{
    public Guid    SensorId               { get; set; }
    public Guid?   StructureSensorId      { get; set; }
    public string  Name                   { get; set; } = string.Empty;
    public string  DeviceId               { get; set; } = string.Empty;
    public string  ProtocolType           { get; set; } = "TCP";
    public string  LifecycleStatus        { get; set; } = "Draft";
    public string  ConnectionTestStatus   { get; set; } = "NotStarted";
    public string  ParserValidationStatus { get; set; } = "NotStarted";
    public string  MetricPreviewStatus    { get; set; } = "NotStarted";
    public string  NextRequiredStep       { get; set; } = "TestConnection";
    public string? IpAddress              { get; set; }
    public int?    Port                   { get; set; }
}
