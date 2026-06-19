using KtuDeYasPortal.Panel.Domain.Entities;

namespace KtuDeYasPortal.Panel.Domain.Interfaces;

public interface IStructureRepository
{
    Task<List<Structure>> GetAllAsync(CancellationToken ct = default);
    Task<Structure?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Structure> CreateAsync(Structure structure, List<Guid> sensorIds, CancellationToken ct = default);
    Task<Structure> UpdateAsync(Guid id, Structure structure, List<Guid> sensorIds, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
