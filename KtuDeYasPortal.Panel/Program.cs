using DeYas.Realtime.DependencyInjection;
using KtuDeYasPortal.Panel.Application.Services;
using KtuDeYasPortal.Panel.Application.Settings;
using KtuDeYasPortal.Panel.Application.UseCases;
using KtuDeYasPortal.Panel.Components;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using KtuDeYasPortal.Panel.Infrastructure.Hubs;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;
using KtuDeYasPortal.Panel.Infrastructure.Realtime;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Server ──────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Options ────────────────────────────────────────────────────────────────────
builder.Services.Configure<PanelOptions>(
    builder.Configuration.GetSection(PanelOptions.Section));
builder.Services.Configure<GrafanaOptions>(
    builder.Configuration.GetSection(GrafanaOptions.Section));

// ── HTTP Clients ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("timeseries-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:TimeseriesApi"] ?? "http://localhost:5000");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("edge-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:EdgeApi"] ?? "http://localhost:5080");
    c.Timeout = TimeSpan.FromSeconds(30);
});

// ── Panel's own SignalR hub (structure group management for Structures page) ──
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval    = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ── Realtime: SensorData state (singleton, Blazor components read from this) ──
builder.Services.AddSingleton<SensorDataState>();

// ── Redis Pub/Sub subscriber — panel connects directly to Redis, no portal dep ─
//
//    Redis (timeseries.updated)
//      → PanelRealtimeForwarder.HandleAsync
//      → SensorDataState.Update
//      → Blazor components re-render
//
//    Redis:Enabled = false  →  subscriber is a no-op (useful for dev without Redis)
builder.Services.AddRedisRealtimeSubscriber<PanelRealtimeForwarder>(builder.Configuration);

// ── Repositories & Use Cases ───────────────────────────────────────────────────
builder.Services.AddScoped<IStructureRepository, StructureHttpRepository>();
builder.Services.AddScoped<IStructureSimulationClient, StructureSimulationHttpClient>();
builder.Services.AddScoped<IMediaSimulationClient, MediaSimulationHttpClient>();
builder.Services.AddScoped<ISensorRepository, SensorHttpRepository>();
builder.Services.AddScoped<ITimeseriesRepository, TimeseriesRepository>();
builder.Services.AddScoped<StructureUseCases>();
builder.Services.AddScoped<SensorDashboardUseCases>();
// ── Workspace — Docker API üzerinden Edge Service'e bağlanır ──────────────────
// IEdgeWorkspaceClient → edge-api (container lifecycle)
// IWorkspaceClient     → timeseries-api (workspace_data JSONB kayıt)
builder.Services.AddScoped<IEdgeWorkspaceClient, EdgeWorkspaceClient>();
builder.Services.AddScoped<IWorkspaceClient, WorkspaceHttpClient>();
builder.Services.AddScoped<WorkspaceUseCases>();

// ── App pipeline ───────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapHub<SensorHub>(SensorHub.HubPath);

app.MapGet("/media-preview/{fileName}", (string fileName, IConfiguration configuration) =>
{
    var safeName = Path.GetFileName(fileName);
    var tempRoot = configuration["MediaSimulation:UploadTempRoot"]
                ?? Path.Combine(Path.GetTempPath(), "ktu-de-yas-media");
    var path = Path.Combine(tempRoot, safeName);
    if (!System.IO.File.Exists(path)) return Results.NotFound();
    return Results.File(path, GetMediaPreviewContentType(path), fileDownloadName: safeName);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string GetMediaPreviewContentType(string path) =>
    Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".mp4"            => "video/mp4",
        ".webm"           => "video/webm",
        ".mov"            => "video/quicktime",
        _                 => "application/octet-stream"
    };
