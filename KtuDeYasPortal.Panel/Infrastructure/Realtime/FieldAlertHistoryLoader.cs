using System.Text.Json;
using DeYas.Realtime;
using KtuDeYasPortal.Panel.Application.Services;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KtuDeYasPortal.Panel.Infrastructure.Realtime;

/// <summary>
/// Redis Pub/Sub geçmiş tutmadığı için panel başlangıcında son saha alertlerini
/// kalıcı Redis indeksinden yükler. Paydaş alert akışını kullanmaz.
/// </summary>
public sealed class FieldAlertHistoryLoader : BackgroundService
{
    private const string FieldAlertHash = "deyas:field-alerts";
    private const string FieldAlertOrder = "deyas:field-alerts:order";

    private readonly RealtimeOptions _options;
    private readonly AlertState _state;
    private readonly ILogger<FieldAlertHistoryLoader> _logger;

    public FieldAlertHistoryLoader(IOptions<RealtimeOptions> options, AlertState state, ILogger<FieldAlertHistoryLoader> logger)
    {
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        try
        {
            var cfg = ConfigurationOptions.Parse(_options.ConnectionString);
            cfg.AbortOnConnectFail = false;
            cfg.ConnectRetry = 5;
            cfg.ReconnectRetryPolicy = new ExponentialRetry(1_000, 30_000);

            await using var mux = await ConnectionMultiplexer.ConnectAsync(cfg).ConfigureAwait(false);
            var db = mux.GetDatabase();
            var alarmIds = await db.SortedSetRangeByRankAsync(FieldAlertOrder, 0, 99, Order.Descending).ConfigureAwait(false);
            if (alarmIds.Length == 0) return;

            var values = await db.HashGetAsync(FieldAlertHash, alarmIds).ConfigureAwait(false);
            var loaded = 0;
            foreach (var value in values)
            {
                if (!value.HasValue) continue;
                var alert = JsonSerializer.Deserialize<PersistedFieldAlert>(value.ToString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (alert is null) continue;

                _state.UpsertFieldAlert(new FieldAlert(
                    alert.AlarmId, alert.DeviceId, alert.LocationId ?? "default",
                    alert.Severity, alert.Message, alert.Timestamp,
                    alert.Metric, alert.Value, alert.Threshold));
                loaded++;
            }

            _logger.LogInformation("[field-alert-history] Redis'ten {Count} saha alerti yüklendi", loaded);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[field-alert-history] Saha alert geçmişi yüklenemedi");
        }
    }

    private sealed record PersistedFieldAlert(
        string AlarmId, string DeviceId, string? LocationId, string Severity,
        string Message, DateTime Timestamp, string? Metric, double? Value, double? Threshold);
}
