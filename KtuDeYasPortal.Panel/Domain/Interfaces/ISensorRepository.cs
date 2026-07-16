using KtuDeYasPortal.Panel.Domain.Entities;

namespace KtuDeYasPortal.Panel.Domain.Interfaces;

public interface ISensorRepository
{
    Task<List<Sensor>> GetAllAsync(CancellationToken ct = default);
    Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct = default);
    Task<object> TestTcpConnectionAsync(TcpSensorCreateRequest request, CancellationToken ct = default);
    Task<Sensor> CreateTcpSensorAsync(TcpSensorCreateRequest request, CancellationToken ct = default);
}
