using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using DeYas.Contracts.Sensors;

namespace KtuDeYasPortal.Panel.Application.UseCases;

public class SensorDashboardUseCases
{
    private readonly IStructureRepository _structureRepo;
    private readonly ITimeseriesRepository _timeseriesRepo;
    private readonly ISensorRepository _sensorRepo;

    public SensorDashboardUseCases(
        IStructureRepository structureRepo,
        ITimeseriesRepository timeseriesRepo,
        ISensorRepository sensorRepo)
    {
        _structureRepo  = structureRepo;
        _timeseriesRepo = timeseriesRepo;
        _sensorRepo     = sensorRepo;
    }

    public Task<List<Structure>> GetAllStructuresAsync(CancellationToken ct = default) =>
        _structureRepo.GetAllAsync(ct);

    public Task<Structure?> GetStructureByIdAsync(Guid id, CancellationToken ct = default) =>
        _structureRepo.GetByIdAsync(id, ct);

    public Task<List<SensorDataPoint>> QueryTimeseriesAsync(
        string? deviceId, string? locationId,
        DateTime from, DateTime to, int limit = 1000,
        CancellationToken ct = default) =>
        _timeseriesRepo.QueryAsync(deviceId, locationId, from, to, limit, ct);

    /// <summary>
    /// Sensörün prod hazırlık durumunu döner.
    /// Sensor Dashboard lifecycle panel bu metodu kullanır.
    /// </summary>
    public Task<SensorReadinessDto> GetSensorReadinessAsync(
        Guid sensorId,
        CancellationToken ct = default) =>
        _sensorRepo.GetReadinessAsync(sensorId, ct);

    /// <summary>
    /// Returns true if at least one SensorData record exists for any of the
    /// given device IDs in the last <paramref name="lookbackHours"/> hours.
    /// </summary>
    public async Task<bool> HasSensorDataAsync(
        IEnumerable<string> deviceIds,
        int lookbackHours = 24,
        CancellationToken ct = default)
    {
        var deviceList = deviceIds.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (deviceList.Count == 0) return false;

        var to   = DateTime.UtcNow;
        var from = to.AddHours(-lookbackHours);

        foreach (var deviceId in deviceList)
        {
            var data = await _timeseriesRepo.QueryAsync(deviceId, null, from, to, limit: 1, ct);
            if (data.Count > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the most recent SensorData record for each device in the given list.
    /// </summary>
    public async Task<List<SensorDataPoint>> GetLatestPerDeviceAsync(
        IEnumerable<string> deviceIds,
        int lookbackHours = 24,
        CancellationToken ct = default)
    {
        var deviceList = deviceIds.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (deviceList.Count == 0) return new();

        var to   = DateTime.UtcNow;
        var from = to.AddHours(-lookbackHours);
        var result = new List<SensorDataPoint>();

        foreach (var deviceId in deviceList)
        {
            var data = await _timeseriesRepo.QueryAsync(deviceId, null, from, to, limit: 1, ct);
            if (data.Count > 0) result.Add(data[0]);
        }
        return result;
    }
}
