using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

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
}
