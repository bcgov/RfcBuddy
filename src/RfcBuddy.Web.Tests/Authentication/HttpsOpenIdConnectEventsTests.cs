using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace RfcBuddy.Web.Authentication.Tests;

[TestClass]
public class HttpsOpenIdConnectEventsTests
{
    private const string Host = "rfcbuddy-ca61f6-dev.apps.emerald.devops.gov.bc.ca";

    [TestMethod]
    [DataRow("http://example.test/signin-oidc", "https://example.test/signin-oidc")]
    [DataRow("http://example.test:80/signin-oidc", "https://example.test/signin-oidc")]
    [DataRow("https://example.test/signin-oidc", "https://example.test/signin-oidc")]
    [DataRow("https://example.test:443/signin-oidc", "https://example.test/signin-oidc")]
    [DataRow("http://localhost:5001/buddy/signin-oidc?value=a%2Fb", "https://localhost:5001/buddy/signin-oidc?value=a%2Fb")]
    [DataRow("https://localhost:5001/buddy/signin-oidc", "https://localhost:5001/buddy/signin-oidc")]
    [DataRow("http://[::1]:5001/signin-oidc", "https://[::1]:5001/signin-oidc")]
    [DataRow("http://example.test/%E1%90%83/signin-oidc", "https://example.test/%E1%90%83/signin-oidc")]
    public async Task RedirectEventsEnforceHttpsAndPreserveUrlComponents(string original, string expected)
    {
        var events = new HttpsOpenIdConnectEvents();
        var context = new RedirectContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("oidc", null, typeof(OpenIdConnectHandler)),
            new OpenIdConnectOptions(),
            new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage
            {
                RedirectUri = original,
                PostLogoutRedirectUri = original
            }
        };

        await events.RedirectToIdentityProvider(context);
        await events.RedirectToIdentityProviderForSignOut(context);

        Assert.AreEqual(expected, context.ProtocolMessage.RedirectUri);
        Assert.AreEqual(expected, context.ProtocolMessage.PostLogoutRedirectUri);
    }

    [TestMethod]
    [DataRow("http", "", false)]
    [DataRow("http", "/buddy", false)]
    [DataRow("https", "", false)]
    [DataRow("http", "", true)]
    [DataRow("http", "/buddy", true)]
    [DataRow("https", "", true)]
    public async Task ChallengeUsesHttpsForAuthorizationAndCodeRedemption(string requestScheme, string pathBase, bool usePar)
    {
        using var provider = CreateServices(usePar);
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider, requestScheme, pathBase);
        string? pushedRedirectUri = null;
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        options.Events.OnPushAuthorization = pushContext =>
        {
            pushedRedirectUri = pushContext.ProtocolMessage.RedirectUri;
            pushContext.HandlePush("urn:example:request");
            return Task.CompletedTask;
        };
        var properties = new AuthenticationProperties { RedirectUri = "/return?value=a%2Fb" };

        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);

        string expected = $"https://{Host}{pathBase}/signin-oidc";
        var authorizationParameters = QueryHelpers.ParseQuery(new Uri(context.Response.Headers.Location.ToString()).Query);
        if (usePar)
        {
            Assert.AreEqual(expected, pushedRedirectUri);
            Assert.AreEqual("urn:example:request", authorizationParameters["request_uri"].ToString());
            Assert.IsFalse(authorizationParameters.ContainsKey("redirect_uri"));
        }
        else
        {
            Assert.AreEqual(expected, authorizationParameters["redirect_uri"].ToString());
            var savedProperties = options.StateDataFormat.Unprotect(authorizationParameters["state"].ToString());
            Assert.IsNotNull(savedProperties);
            Assert.AreEqual(expected, savedProperties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey]);
        }

        Assert.AreEqual(expected, properties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey]);
        Assert.AreEqual("/return?value=a%2Fb", properties.RedirectUri);
    }

    [TestMethod]
    [DataRow("http", "")]
    [DataRow("http", "/buddy")]
    [DataRow("https", "")]
    public async Task SignOutUsesHttpsForPostLogoutCallback(string requestScheme, string pathBase)
    {
        using var provider = CreateServices(false);
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider, requestScheme, pathBase);

        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/signed-out" });

        var parameters = QueryHelpers.ParseQuery(new Uri(context.Response.Headers.Location.ToString()).Query);
        Assert.AreEqual($"https://{Host}{pathBase}/signout-callback-oidc", parameters["post_logout_redirect_uri"].ToString());
    }

    private static ServiceProvider CreateServices(bool usePar)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ClientId = "test-client";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.Events = new HttpsOpenIdConnectEvents();
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.UseIfAvailable;
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://keycloak.example.test",
                    AuthorizationEndpoint = "https://keycloak.example.test/authorize",
                    EndSessionEndpoint = "https://keycloak.example.test/logout",
                    PushedAuthorizationRequestEndpoint = usePar ? "https://keycloak.example.test/par" : null
                };
            });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string scheme, string pathBase)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(Host);
        context.Request.PathBase = pathBase;
        context.Request.Path = "/";
        return context;
    }
}
