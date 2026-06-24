using DeYas.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace KtuDeYasPortal.Panel.Application.Services;

/// <summary>
/// Singleton background service that maintains a persistent SignalR connection
/// to the Portal's DashboardHub (/hubs/dashboard).
///
/// Data flow:
///   TimescaleDB insert → Redis pub/sub → RealtimeForwarder (portal)
///   → DashboardHub → ReceiveSensorUpdate → SensorDataState (panel)
///
/// Only receives real SensorData records — never Sensor config metadata.
/// Kafka lag messages may arrive but are handled by structure-scoped filtering
/// in the dashboard (only devices belonging to the selected structure are shown).
/// </summary>
public sealed class PortalHubClient : IAsyncDisposable
{
    private readonly SensorDataState _state;
    private readonly ILogger<PortalHubClient> _logger;
    private readonly string _hubUrl;
    private HubConnection? _connection;
    private bool _started;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public PortalHubClient(
        SensorDataState state,
        IConfiguration configuration,
        ILogger<PortalHubClient> logger)
    {
        _state = state;
        _logger = logger;
        _hubUrl = configuration["SignalR:PortalHubUrl"] ?? "http://localhost:5050/hubs/dashboard";
    }

    /// <summary>
    /// Returns the current connection state. Components can check this
    /// to show a realtime indicator.
    /// </summary>
    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Lazily starts the hub connection on first call.
    /// Safe to call multiple times — will only start once.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        if (_started) return;

        await _startLock.WaitAsync(ct);
        try
        {
            if (_started) return;

            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // SensorData updates from real TimescaleDB inserts
            _connection.On<SensorUpdatedEvent>("ReceiveSensorUpdate", evt =>
            {
                if (string.IsNullOrWhiteSpace(evt.DeviceId)) return;
                _state.Update(evt);
                _logger.LogDebug("[hub-client] SensorData received device={DeviceId} metrics={Count}",
                    evt.DeviceId, evt.AllMetrics?.Count ?? 0);
            });

            _connection.Reconnecting += ex =>
            {
                _logger.LogWarning("[hub-client] Reconnecting to portal hub: {Reason}", ex?.Message);
                return Task.CompletedTask;
            };

            _connection.Reconnected += connId =>
            {
                _logger.LogInformation("[hub-client] Reconnected to portal hub connId={ConnId}", connId);
                return Task.CompletedTask;
            };

            _connection.Closed += ex =>
            {
                _logger.LogWarning("[hub-client] Connection closed: {Reason}", ex?.Message);
                return Task.CompletedTask;
            };

            await _connection.StartAsync(ct);
            _started = true;
            _logger.LogInformation("[hub-client] Connected to portal hub at {Url}", _hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[hub-client] Failed to connect to portal hub at {Url}", _hubUrl);
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
