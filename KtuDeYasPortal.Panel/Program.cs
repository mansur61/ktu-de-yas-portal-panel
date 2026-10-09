using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PanelApiClients>();
builder.Services.AddScoped<PanelAccess>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("platform.admin", p => p.RequireAuthenticatedUser().RequireClaim("permission", "platform.admin"));
    options.AddPolicy("media.access", p => p.RequireAuthenticatedUser().RequireAssertion(c => c.User.HasClaim("permission", "menu.media") || c.User.HasClaim("permission", "platform.admin")));
    options.AddPolicy("workspace.access", p => p.RequireAuthenticatedUser().RequireAssertion(c => c.User.HasClaim("permission", "menu.workspace") || c.User.HasClaim("permission", "platform.admin")));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "deyas.panel.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(50);
    options.SlidingExpiration = false;
});
builder.Services.AddDataProtection().SetApplicationName("ktu-deyas-panel")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys")));

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

builder.Services.AddHttpClient("auth-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:AuthApi"] ?? "http://localhost:5100");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("edge-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:EdgeApi"] ?? "http://localhost:5080");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("video-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:VideoApi"] ?? "http://localhost:5010");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("alert-config-api", c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Services:AlertConfigApi"]
        ?? (builder.Environment.IsDevelopment()
            ? "http://localhost:5020"
            : "http://alert-config-service:5020"));
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
builder.Services.AddSingleton<AlertState>();
builder.Services.AddHostedService<FieldAlertHistoryLoader>();

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
builder.Services.AddScoped<IMediaArchiveClient, MediaArchiveHttpClient>();
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
builder.Services.AddScoped<KtuDeYasPortal.Panel.Infrastructure.Persistence.AlertRuleHttpRepository>();

// ── App pipeline ───────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
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
}).RequireAuthorization("platform.admin");

app.MapPost("/session/register", async (HttpContext context, IHttpClientFactory factory, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, CancellationToken ct) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(); }
    var form = await context.Request.ReadFormAsync(ct);
    using var response = await factory.CreateClient("auth-api").PostAsJsonAsync("api/auth/register", new
    {
        UserName = form["username"].ToString(), Password = form["password"].ToString(), OrganizationName = form["organization"].ToString()
    }, ct);
    return Results.LocalRedirect(response.IsSuccessStatusCode ? "/login?registered=true" : "/login?register=true&error=register");
});

app.MapPost("/session/login", async (HttpContext context, IHttpClientFactory factory, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, CancellationToken ct) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(); }
    var form = await context.Request.ReadFormAsync(ct);
    var client = factory.CreateClient("auth-api");
    using var response = await client.PostAsJsonAsync("api/auth/login", new { UserName = form["username"].ToString(), Password = form["password"].ToString() }, ct);
    if (!response.IsSuccessStatusCode) return Results.LocalRedirect("/login?error=credentials");
    var session = await response.Content.ReadFromJsonAsync<PanelLoginResponse>(cancellationToken: ct);
    if (session is null || string.IsNullOrWhiteSpace(session.Token)) return Results.LocalRedirect("/login?error=token");
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, session.UserId), new(ClaimTypes.Name, session.UserName),
        new(ClaimTypes.Role, session.Role), new("access_token", session.Token) };
    claims.AddRange(session.Permissions.Select(p => new Claim("permission", p)));
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.LocalRedirect("/");
});
app.MapPost("/session/logout", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(); }
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).RequireAuthorization();

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

public sealed record PanelLoginResponse(string UserId, string UserName, string Role, string[] Permissions, string? Token);
