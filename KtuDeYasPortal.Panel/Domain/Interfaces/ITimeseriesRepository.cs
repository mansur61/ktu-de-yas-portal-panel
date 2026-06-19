namespace KtuDeYasPortal.Panel.Domain.Interfaces;

public record SensorDataPoint(
    string DeviceId,
    string LocationId,
    DateTime Timestamp,
    Dictionary<string, double> Metrics);

public interface ITimeseriesRepository
{
    Task<List<SensorDataPoint>> QueryAsync(string? deviceId, string? locationId, DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default);
}
