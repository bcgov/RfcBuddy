using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Controllers.Tests;

[TestClass]
public class ApiTokensControllerTests
{
    private static ClaimsPrincipal CreateUserPrincipal(string username)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username)
        }, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [TestMethod]
    public void IndexReturnsViewWithTokens()
    {
        var mockService = new Mock<IApiTokenService>();
        var hashedUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("test-user");
        var tokens = new List<ApiToken>
        {
            new() { Label = "Token 1", OwnerUserId = hashedUserId }
        };
        mockService.Setup(x => x.GetTokensForUser(hashedUserId)).Returns(tokens);

        var controller = new ApiTokensController(mockService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal("test-user") }
            }
        };

        var result = controller.Index() as ViewResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(tokens, result!.Model);
    }

    [TestMethod]
    public void CreateGetReturnsView()
    {
        var mockService = new Mock<IApiTokenService>();
        var controller = new ApiTokensController(mockService.Object);

        var result = controller.Create() as ViewResult;

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void CreatePostCreatesTokenAndRedirects()
    {
        var mockService = new Mock<IApiTokenService>();
        var hashedUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("test-user");
        var token = new ApiToken { Id = "123", Label = "New Token", OwnerUserId = hashedUserId };
        var creationResult = new TokenCreationResult { Token = token, RawToken = "raw-secret-value" };

        mockService.Setup(x => x.CreateToken(hashedUserId, "New Token", It.IsAny<DateTime>()))
            .Returns(creationResult);

        var httpContext = new DefaultHttpContext { User = CreateUserPrincipal("test-user") };
        var tempDataProvider = new Mock<ITempDataProvider>();
        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

        var controller = new ApiTokensController(mockService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = tempData
        };

        var result = controller.Create("New Token", DateTime.UtcNow.AddDays(30)) as RedirectToActionResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("Index", result!.ActionName);
        Assert.AreEqual("raw-secret-value", controller.TempData["CreatedToken"]);
    }

    [TestMethod]
    public void RevokePostRevokesTokenAndRedirects()
    {
        var mockService = new Mock<IApiTokenService>();
        var hashedUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("test-user");
        mockService.Setup(x => x.RevokeToken("token-id", hashedUserId)).Returns(true);

        var controller = new ApiTokensController(mockService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal("test-user") }
            }
        };

        var result = controller.Revoke("token-id") as RedirectToActionResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("Index", result!.ActionName);
        mockService.Verify(x => x.RevokeToken("token-id", hashedUserId), Times.Once);
    }
}
