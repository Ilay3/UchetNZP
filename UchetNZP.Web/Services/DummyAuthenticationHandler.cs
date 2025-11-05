using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UchetNZP.Web.Services;

public sealed class DummyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DummyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, DummyAuthenticationDefaults.UserId.ToString()),
            new Claim(ClaimTypes.Name, DummyAuthenticationDefaults.UserName),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        var ret = Task.FromResult(AuthenticateResult.Success(ticket));
        return ret;
    }
}

public static class DummyAuthenticationDefaults
{
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public const string UserName = "Администратор";
}
