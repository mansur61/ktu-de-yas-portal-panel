using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

// The installed panel is a management console. Human login belongs to the portal.
// This token stays on the server and authenticates the panel's own service account.
public sealed class PanelServiceSession(IHttpClientFactory factory, IConfiguration configuration)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;
    public async Task<string> TokenAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddMinutes(-1)) return _token;
            var name=configuration["PanelService:UserName"];
            var password=configuration["PanelService:Password"];
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
                throw new HttpRequestException("Panelin servis hesabı kurulum ayarlarında eksik.");
            using var response=await factory.CreateClient("panel-auth-login").PostAsJsonAsync("api/auth/login",new { UserName=name,Password=password },ct);
            response.EnsureSuccessStatusCode();
            var session=await response.Content.ReadFromJsonAsync<PanelLoginResponse>(ct);
            if(session is null || string.IsNullOrWhiteSpace(session.Token) || !session.Permissions.Contains("platform.admin"))
                throw new HttpRequestException("Panelin servis hesabı yönetim yetkisine sahip değil.");
            var payload=session.Token.Split('.')[1].Replace('-','+').Replace('_','/');
            payload=payload.PadRight((payload.Length+3)/4*4,'=');
            using var claims=JsonDocument.Parse(Convert.FromBase64String(payload));
            _expiresAt=DateTimeOffset.FromUnixTimeSeconds(claims.RootElement.GetProperty("exp").GetInt64());
            _token=session.Token;
            return _token;
        }
        finally { _gate.Release(); }
    }
}

public sealed class PanelServiceTokenHandler(PanelServiceSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await session.TokenAsync(ct));
        return await base.SendAsync(request,ct);
    }
}

public sealed class PanelInstallationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"panel-installation"),new Claim(ClaimTypes.Name,"Yönetim Paneli"),new Claim("permission","platform.admin")},Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
    }
}
