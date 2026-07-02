using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RfcBuddy.App.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RfcBuddy.Web.Authentication;

public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiTokenService apiTokenService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly IApiTokenService _apiTokenService = apiTokenService;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string? authorizationValue = authorizationHeader.ToString();
        if (!authorizationValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string rawToken = authorizationValue["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        AuthenticatedToken? authenticatedToken = _apiTokenService.Authenticate(rawToken);
        if (authenticatedToken is null)
        {
            return Task.Delay(100, RequestAborted)
                .ContinueWith(_ => AuthenticateResult.Fail("Invalid or expired token."), TaskScheduler.Default);
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, authenticatedToken.OwnerUserId), new Claim(ClaimTypes.Name, authenticatedToken.OwnerUserId)], Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
