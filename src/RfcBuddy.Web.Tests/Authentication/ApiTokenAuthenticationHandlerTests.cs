using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Authentication.Tests;

[TestClass]
public class ApiTokenAuthenticationHandlerTests
{
    private sealed class OptionMonitorFake : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions Get(string? name) => new();
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
        public AuthenticationSchemeOptions CurrentValue => new();
    }

    [TestMethod]
    public async Task AuthenticateWithValidTokenReturnsSuccess()
    {
        var mockService = new Mock<IApiTokenService>();
        mockService.Setup(x => x.Authenticate("valid-token"))
            .Returns(new AuthenticatedToken { OwnerUserId = "user-123", TokenId = "token-abc" });

        var monitor = new OptionMonitorFake();
        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;

        var handler = new ApiTokenAuthenticationHandler(monitor, loggerFactory, encoder, mockService.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var scheme = new AuthenticationScheme("ApiToken", "ApiToken", typeof(ApiTokenAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Principal);
        Assert.AreEqual("user-123", result.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [TestMethod]
    public async Task AuthenticateWithMissingHeaderReturnsNoResult()
    {
        var mockService = new Mock<IApiTokenService>();
        var monitor = new OptionMonitorFake();
        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;

        var handler = new ApiTokenAuthenticationHandler(monitor, loggerFactory, encoder, mockService.Object);

        var context = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("ApiToken", "ApiToken", typeof(ApiTokenAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Principal);
    }

    [TestMethod]
    public async Task AuthenticateWithInvalidTokenReturnsFailure()
    {
        var mockService = new Mock<IApiTokenService>();
        mockService.Setup(x => x.Authenticate("bad-token")).Returns((AuthenticatedToken?)null);

        var monitor = new OptionMonitorFake();
        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;

        var handler = new ApiTokenAuthenticationHandler(monitor, loggerFactory, encoder, mockService.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer bad-token";

        var scheme = new AuthenticationScheme("ApiToken", "ApiToken", typeof(ApiTokenAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
    }
}
