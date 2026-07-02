using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Models;

namespace RfcBuddy.Web.Controllers;

[Authorize(Policy = "Admin")]
public class AdminController(IApiTokenService apiTokenService, IUserRegistryService userRegistryService) : Controller
{
    private readonly IApiTokenService _apiTokenService = apiTokenService;
    private readonly IUserRegistryService _userRegistryService = userRegistryService;

    [HttpGet]
    public IActionResult Index()
    {
        var users = _userRegistryService.GetAllUsers();
        var tokens = _apiTokenService.GetAllTokens();
        var model = new AdminViewModel
        {
            Users = users,
            ActiveTokens = tokens
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetAdmin(string userId, bool isAdmin)
    {
        string adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "unknown";
        _ = _userRegistryService.SetAdmin(userId, isAdmin, adminUserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RevokeToken(string tokenId)
    {
        _ = _apiTokenService.RevokeTokenAsAdmin(tokenId);
        return RedirectToAction(nameof(Index));
    }
}
