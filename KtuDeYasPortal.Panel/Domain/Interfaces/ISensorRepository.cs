using KtuDeYasPortal.Panel.Domain.Entities;

namespace KtuDeYasPortal.Panel.Domain.Interfaces;

public interface ISensorRepository
{
    Task<List<Sensor>> GetAllAsync(CancellationToken ct = default);
    Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct = default);
}
