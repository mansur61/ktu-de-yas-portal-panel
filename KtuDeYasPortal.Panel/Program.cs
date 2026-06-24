using KtuDeYasPortal.Panel.Application.Services;
using KtuDeYasPortal.Panel.Application.Settings;
using KtuDeYasPortal.Panel.Application.UseCases;
using KtuDeYasPortal.Panel.Components;
using KtuDeYasPortal.Panel.Domain.Interfaces;
using KtuDeYasPortal.Panel.Infrastructure.Hubs;
using KtuDeYasPortal.Panel.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Server ──
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Panel & Grafana Options ──
builder.Services.Configure<PanelOptions>(
    builder.Configuration.GetSection(PanelOptions.Section));
builder.Services.Configure<GrafanaOptions>(
    builder.Configuration.GetSection(GrafanaOptions.Section));

// ── HTTP Client — timeseries-service ──
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

// ── SignalR Hubs (Panel's own hub for structure group management) ──
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ── Realtime: SensorData-driven state (fed by Portal hub, not Sensor config) ──
builder.Services.AddSingleton<SensorDataState>();
builder.Services.AddSingleton<PortalHubClient>();

// ── Repositories & Use Cases ──
builder.Services.AddScoped<IStructureRepository, StructureHttpRepository>();
builder.Services.AddScoped<IStructureSimulationClient, StructureSimulationHttpClient>();
builder.Services.AddScoped<IMediaSimulationClient, MediaSimulationHttpClient>();
builder.Services.AddScoped<ISensorRepository, SensorHttpRepository>();
builder.Services.AddScoped<ITimeseriesRepository, TimeseriesRepository>();
builder.Services.AddScoped<StructureUseCases>();
builder.Services.AddScoped<SensorDashboardUseCases>();

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

// ── SignalR Hub Endpoint ──
app.MapHub<SensorHub>(SensorHub.HubPath);

app.MapGet("/media-preview/{fileName}", (string fileName, IConfiguration configuration) =>
{
    var safeName = Path.GetFileName(fileName);
    var tempRoot = configuration["MediaSimulation:UploadTempRoot"] ?? Path.Combine(Path.GetTempPath(), "ktu-de-yas-media");
    var path = Path.Combine(tempRoot, safeName);
    if (!System.IO.File.Exists(path))
        return Results.NotFound();

    return Results.File(path, contentType: GetMediaPreviewContentType(path), fileDownloadName: safeName);
});

// ── REST API: Yapılar ve Sensörler ──
// app.MapStructureEndpoints();  // TODO: Implement structure endpoints if needed

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string GetMediaPreviewContentType(string path)
{
    return Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream"
    };
}
