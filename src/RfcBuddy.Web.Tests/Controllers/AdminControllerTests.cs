using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Models;

namespace RfcBuddy.Web.Controllers.Tests;

[TestClass]
public class AdminControllerTests
{
    private static ClaimsPrincipal CreateAdminPrincipal(string username)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username)
        }, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [TestMethod]
    public void IndexReturnsViewWithAdminViewModel()
    {
        var mockTokenService = new Mock<IApiTokenService>();
        var mockRegistryService = new Mock<IUserRegistryService>();

        var users = new List<UserListEntry>
        {
            new() { UserId = "user-1", Identity = "John Doe", Email = "john@gov.bc.ca", IsAdmin = true, LastActiveUtc = DateTime.UtcNow }
        };
        var activeTokens = new List<ApiToken>
        {
            new() { Id = "token-1", Label = "Integrator", OwnerUserId = "user-1" }
        };

        mockRegistryService.Setup(x => x.GetAllUsers()).Returns(users);
        mockTokenService.Setup(x => x.GetAllTokens()).Returns(activeTokens);

        var controller = new AdminController(mockTokenService.Object, mockRegistryService.Object);

        var result = controller.Index() as ViewResult;

        Assert.IsNotNull(result);
        var model = result!.Model as AdminViewModel;
        Assert.IsNotNull(model);
        Assert.AreSequenceEqual(users, model!.Users);
        Assert.AreSequenceEqual(activeTokens, model.ActiveTokens);
    }

    [TestMethod]
    public void SetAdminPostChangesRoleAndRedirects()
    {
        var mockTokenService = new Mock<IApiTokenService>();
        var mockRegistryService = new Mock<IUserRegistryService>();
        var hashedAdminUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("admin-user");

        mockRegistryService.Setup(x => x.SetAdmin("user-2", true, hashedAdminUserId)).Returns(true);

        var controller = new AdminController(mockTokenService.Object, mockRegistryService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateAdminPrincipal("admin-user") }
            }
        };

        var result = controller.SetAdmin("user-2", true) as RedirectToActionResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("Index", result!.ActionName);
        mockRegistryService.Verify(x => x.SetAdmin("user-2", true, hashedAdminUserId), Times.Once);
    }

    [TestMethod]
    public void RevokeTokenPostRevokesTokenAsAdminAndRedirects()
    {
        var mockTokenService = new Mock<IApiTokenService>();
        var mockRegistryService = new Mock<IUserRegistryService>();

        mockTokenService.Setup(x => x.RevokeTokenAsAdmin("token-1")).Returns(true);

        var controller = new AdminController(mockTokenService.Object, mockRegistryService.Object);

        var result = controller.RevokeToken("token-1") as RedirectToActionResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("Index", result!.ActionName);
        mockTokenService.Verify(x => x.RevokeTokenAsAdmin("token-1"), Times.Once);
    }
}
