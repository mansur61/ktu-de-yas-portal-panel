using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

namespace KtuDeYasPortal.Panel.Application.UseCases;

public class SensorDashboardUseCases
{
    private readonly IStructureRepository _structureRepo;
    private readonly ITimeseriesRepository _timeseriesRepo;

    public SensorDashboardUseCases(
        IStructureRepository structureRepo,
        ITimeseriesRepository timeseriesRepo)
    {
        _structureRepo = structureRepo;
        _timeseriesRepo = timeseriesRepo;
    }

    public Task<List<Structure>> GetAllStructuresAsync(CancellationToken ct = default) =>
        _structureRepo.GetAllAsync(ct);

    public Task<List<SensorDataPoint>> QueryTimeseriesAsync(
        string? deviceId, string? locationId,
        DateTime from, DateTime to, int limit = 1000,
        CancellationToken ct = default) =>
        _timeseriesRepo.QueryAsync(deviceId, locationId, from, to, limit, ct);
}
