using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace KtuDeYasPortal.Panel.Infrastructure.Persistence;

public sealed class PanelInstallationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"panel-installation"),new Claim(ClaimTypes.Name,"Yönetim Paneli"),new Claim("permission","platform.admin")},Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
    }
}
