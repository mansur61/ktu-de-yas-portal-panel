using KtuDeYasPortal.Panel.Domain.Entities;

namespace KtuDeYasPortal.Panel.Domain.Interfaces;

public interface ISensorRepository
{
    Task<List<Sensor>> GetAllAsync(CancellationToken ct = default);
    Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct = default);
    Task<object> TestTcpConnectionAsync(TcpSensorCreateRequest request, CancellationToken ct = default);
    Task<Sensor> CreateTcpSensorAsync(TcpSensorCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sensör + TCP config + yapı ilişkisini tek transaction ile oluşturur.
    /// POST /api/structures/{structureId}/sensors/connected
    /// </summary>
    Task<ConnectedSensorResponse> CreateConnectedSensorAsync(
        Guid structureId,
        ConnectedSensorRequest request,
        CancellationToken ct = default);

    // ── Prod Lifecycle endpoint'leri ─────────────────────────────────────────
    Task<DeYas.Contracts.Sensors.ConnectionTestResultDto> TestConnectionLifecycleAsync(
        Guid sensorId,
        DeYas.Contracts.Sensors.ConnectionTestRequestDto request,
        CancellationToken ct = default);

    Task<DeYas.Contracts.Sensors.ParserValidationResultDto> ValidateParserAsync(
        Guid sensorId,
        DeYas.Contracts.Sensors.ParserValidationRequestDto request,
        CancellationToken ct = default);

    Task<DeYas.Contracts.Sensors.MetricPreviewGeneratedDto> GenerateMetricPreviewAsync(
        Guid sensorId,
        DeYas.Contracts.Sensors.GenerateMetricPreviewRequestDto request,
        CancellationToken ct = default);

    Task<DeYas.Contracts.Sensors.SensorReadinessDto> GetReadinessAsync(
        Guid sensorId,
        CancellationToken ct = default);

    Task<List<DeYas.Contracts.Sensors.MetricPreviewItemDto>> GetMetricPreviewsAsync(
        Guid sensorId,
        CancellationToken ct = default);

    Task ResetLifecycleAsync(Guid sensorId, CancellationToken ct = default);
}
