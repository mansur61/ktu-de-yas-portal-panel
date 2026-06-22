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

// ── Panel Options ──
builder.Services.Configure<PanelOptions>(
    builder.Configuration.GetSection(PanelOptions.Section));

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

// ── SignalR Hubs ──
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ── Repositories & Use Cases ──
builder.Services.AddScoped<IStructureRepository, StructureHttpRepository>();
builder.Services.AddScoped<IStructureSimulationClient, StructureSimulationHttpClient>();
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
