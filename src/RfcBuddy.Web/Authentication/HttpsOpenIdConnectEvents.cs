using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace RfcBuddy.Web.Authentication;

public sealed class HttpsOpenIdConnectEvents : OpenIdConnectEvents
{
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        context.ProtocolMessage.RedirectUri = UseHttps(context.ProtocolMessage.RedirectUri);
        return Task.CompletedTask;
    }

    public override Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        context.ProtocolMessage.PostLogoutRedirectUri = UseHttps(context.ProtocolMessage.PostLogoutRedirectUri);
        return Task.CompletedTask;
    }

    private static string UseHttps(string redirectUri)
    {
        var uri = new Uri(redirectUri, UriKind.Absolute);
        return new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = uri.IsDefaultPort ? -1 : uri.Port
        }.Uri.AbsoluteUri;
    }
}
