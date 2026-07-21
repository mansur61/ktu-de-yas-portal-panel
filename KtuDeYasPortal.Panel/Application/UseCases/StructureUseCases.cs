using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using DeYas.Contracts.Sensors;

namespace KtuDeYasPortal.Panel.Application.UseCases;

public class StructureUseCases
{
    private readonly IStructureRepository _structureRepo;
    private readonly ISensorRepository _sensorRepo;

    public StructureUseCases(IStructureRepository structureRepo, ISensorRepository sensorRepo)
    {
        _structureRepo = structureRepo;
        _sensorRepo = sensorRepo;
    }

    public Task<List<Structure>> GetAllAsync(CancellationToken ct = default) =>
        _structureRepo.GetAllAsync(ct);

    public Task<Structure?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _structureRepo.GetByIdAsync(id, ct);

    public async Task<Structure> CreateAsync(Structure structure, List<Guid> sensorIds, CancellationToken ct = default)
    {
        structure.Id = Guid.NewGuid();
        structure.CreatedAt = DateTime.UtcNow;
        structure.UpdatedAt = DateTime.UtcNow;
        return await _structureRepo.CreateAsync(structure, sensorIds, ct);
    }

    public async Task<Structure> UpdateAsync(Guid id, Structure structure, List<Guid> sensorIds, CancellationToken ct = default)
    {
        structure.UpdatedAt = DateTime.UtcNow;
        return await _structureRepo.UpdateAsync(id, structure, sensorIds, ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        _structureRepo.DeleteAsync(id, ct);

    public Task<List<Sensor>> GetAllSensorsAsync(CancellationToken ct = default) =>
        _sensorRepo.GetAllAsync(ct);

    public Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default) =>
        _sensorRepo.CreateAsync(sensor, ct);

    public Task<object> TestTcpConnectionAsync(TcpSensorCreateRequest request, CancellationToken ct = default) =>
        _sensorRepo.TestTcpConnectionAsync(request, ct);

    public Task<Sensor> CreateTcpSensorAsync(TcpSensorCreateRequest request, CancellationToken ct = default) =>
        _sensorRepo.CreateTcpSensorAsync(request, ct);

    /// <summary>
    /// Sensör + TCP config + yapı ilişkisini tek transaction ile oluşturur.
    /// Herhangi biri başarısız olursa backend tüm işlemi geri alır.
    /// POST /api/structures/{structureId}/sensors/connected
    /// </summary>
    public Task<ConnectedSensorResponse> CreateConnectedSensorAsync(
        Guid structureId,
        ConnectedSensorRequest request,
        CancellationToken ct = default) =>
        _sensorRepo.CreateConnectedSensorAsync(structureId, request, ct);

    // ── Prod Lifecycle Use Cases ──────────────────────────────────────────────

    /// <summary>
    /// Sensöre bağlantı testi yapar ve sonucu DB'ye kaydeder.
    /// Yalnızca prod switch aktifken çağrılır.
    /// </summary>
    public Task<ConnectionTestResultDto> TestConnectionLifecycleAsync(
        Guid sensorId,
        ConnectionTestRequestDto request,
        CancellationToken ct = default) =>
        _sensorRepo.TestConnectionLifecycleAsync(sensorId, request, ct);

    /// <summary>
    /// Seçili parser profiliyle bağlantıdan paket alır ve doğrular.
    /// Bağlantı testi başarılı olmadan çağrılamaz.
    /// </summary>
    public Task<ParserValidationResultDto> ValidateParserAsync(
        Guid sensorId,
        ParserValidationRequestDto request,
        CancellationToken ct = default) =>
        _sensorRepo.ValidateParserAsync(sensorId, request, ct);

    /// <summary>
    /// Parser doğrulamasından gelen metrikleri kalıcı olarak kaydeder.
    /// Tüm koşullar sağlandıysa sensör otomatik Ready'e geçer.
    /// </summary>
    public Task<MetricPreviewGeneratedDto> GenerateMetricPreviewAsync(
        Guid sensorId,
        GenerateMetricPreviewRequestDto request,
        CancellationToken ct = default) =>
        _sensorRepo.GenerateMetricPreviewAsync(sensorId, request, ct);

    /// <summary>
    /// Sensörün prod hazırlık durumunu döner.
    /// </summary>
    public Task<SensorReadinessDto> GetSensorReadinessAsync(
        Guid sensorId,
        CancellationToken ct = default) =>
        _sensorRepo.GetReadinessAsync(sensorId, ct);

    /// <summary>
    /// Sensörün oluşturulmuş metrik önizlemelerini döner.
    /// </summary>
    public Task<List<MetricPreviewItemDto>> GetMetricPreviewsAsync(
        Guid sensorId,
        CancellationToken ct = default) =>
        _sensorRepo.GetMetricPreviewsAsync(sensorId, ct);

    /// <summary>
    /// Sensörü Draft'a sıfırlar. Çalışan sensör için önce yapı durdurulmalıdır.
    /// </summary>
    public Task ResetSensorLifecycleAsync(Guid sensorId, CancellationToken ct = default) =>
        _sensorRepo.ResetLifecycleAsync(sensorId, ct);

    /// <summary>
    /// Yapıya ait sensörlerin prod hazırlık özetini döner.
    /// StructureDto içindeki aggregate alanlar bu data ile doldurulur.
    /// </summary>
    public async Task<StructureReadinessSummaryDto?> GetStructureReadinessAsync(
        Guid structureId,
        CancellationToken ct = default)
    {
        var structure = await _structureRepo.GetByIdAsync(structureId, ct);
        if (structure is null) return null;

        // Yapıdaki sensörlerin readiness durumlarını tek sorguda topla
        var sensorReadinessTasks = structure.Sensors
            .Select(ss => _sensorRepo.GetReadinessAsync(ss.SensorId, ct))
            .ToList();

        var results = await Task.WhenAll(sensorReadinessTasks);

        int total        = results.Length;
        int ready        = results.Count(r => r.ParsedLifecycleStatus is
            SensorLifecycleStatus.Ready or SensorLifecycleStatus.Stopped or SensorLifecycleStatus.Running);
        int startable    = results.Count(r => r.IsStartable);
        int running      = results.Count(r => r.ParsedLifecycleStatus.IsLive());
        int connPending  = results.Count(r => r.ParsedConnectionTestStatus != SensorValidationStatus.Succeeded);
        int parserPending = results.Count(r => r.ParsedParserValidationStatus != SensorValidationStatus.Succeeded);
        int metricPending = results.Count(r => r.ParsedMetricPreviewStatus != SensorValidationStatus.Succeeded);

        return new StructureReadinessSummaryDto
        {
            StructureId                  = structureId,
            TotalSensorCount             = total,
            ReadySensorCount             = ready,
            NotReadySensorCount          = total - ready,
            StartableSensorCount         = startable,
            RunningSensorCount           = running,
            ConnectionPendingSensorCount = connPending,
            ParserPendingSensorCount     = parserPending,
            MetricPreviewPendingCount    = metricPending,
            CanStart                     = startable > 0
        };
    }

    /// <summary>
    /// Prod modunda yapı için başlatılabilir sensörlerin ProductionSensorSimRequest listesini
    /// oluşturur. Yalnızca Ready/Stopped ve tüm doğrulamaları tamamlanmış sensörler dahil edilir.
    /// </summary>
    public async Task<(List<DeYas.Contracts.Simulation.Production.ProductionSensorSimRequest> Startable,
                        List<(Guid SensorId, string DeviceId, string Reason)> Skipped)>
        BuildProdSensorListAsync(Structure structure, CancellationToken ct = default)
    {
        var allSensors = await _sensorRepo.GetAllAsync(ct);
        var startable  = new List<DeYas.Contracts.Simulation.Production.ProductionSensorSimRequest>();
        var skipped    = new List<(Guid, string, string)>();

        foreach (var ss in structure.Sensors)
        {
            var sdet = allSensors.FirstOrDefault(x => x.Id == ss.SensorId);
            if (sdet is null)
            {
                skipped.Add((ss.SensorId, ss.DeviceId, "Sensor not found"));
                continue;
            }

            SensorReadinessDto? readiness = null;
            try { readiness = await _sensorRepo.GetReadinessAsync(sdet.Id, ct); }
            catch { skipped.Add((sdet.Id, sdet.DeviceId, "Readiness check failed")); continue; }

            if (!readiness.IsStartable)
            {
                var reason = readiness.MissingRequirements.Count > 0
                    ? string.Join("; ", readiness.MissingRequirements)
                    : $"Lifecycle status: {readiness.LifecycleStatus}";
                skipped.Add((sdet.Id, sdet.DeviceId, reason));
                continue;
            }

            startable.Add(new DeYas.Contracts.Simulation.Production.ProductionSensorSimRequest
            {
                SensorId                 = sdet.Id,
                DeviceId                 = sdet.DeviceId,
                SensorType               = sdet.SensorType.ToString(),
                SensorLabel              = ss.SensorLabel ?? sdet.Name,
                ProtocolType             = sdet.ProtocolType,
                IpAddress                = sdet.IpAddress,
                Port                     = sdet.Port,
                Unit                     = sdet.Unit,
                Metrics                  = sdet.Metrics,
                Topic                    = sdet.Topic,
                TelemetryIntervalMs      = sdet.TelemetryIntervalMs,
                TelemetryBatchSize       = sdet.TelemetryBatchSize,
                LifecycleStatusRaw       = readiness.LifecycleStatus,
                ConnectionTestStatusRaw  = readiness.ConnectionTestStatus,
                ParserValidationStatusRaw = readiness.ParserValidationStatus,
                MetricPreviewStatusRaw   = readiness.MetricPreviewStatus,
                DiscoveredMetricCount    = readiness.MetricCount
            });
        }

        return (startable, skipped);
    }
}
