using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Controllers;

[Authorize]
public class ApiTokensController(IApiTokenService apiTokenService) : Controller
{
    private readonly IApiTokenService _apiTokenService = apiTokenService;

    [HttpGet]
    public IActionResult Index()
    {
        string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(User.Identity?.Name ?? "Generic User");
        var tokens = _apiTokenService.GetTokensForUser(userId);
        return View(tokens);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(string label, DateTime expiry)
    {
        string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(User.Identity?.Name ?? "Generic User");
        if (string.IsNullOrWhiteSpace(label))
        {
            ModelState.AddModelError("label", "Label is required.");
            return View();
        }

        DateTime effectiveExpiryUtc = expiry.ToUniversalTime();
        if (effectiveExpiryUtc > DateTime.UtcNow.AddDays(90))
        {
            effectiveExpiryUtc = DateTime.UtcNow.AddDays(90);
        }

        var result = _apiTokenService.CreateToken(userId, label.Trim(), effectiveExpiryUtc);
        TempData["CreatedToken"] = result.RawToken;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Revoke(string tokenId)
    {
        string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(User.Identity?.Name ?? "Generic User");
        _ = _apiTokenService.RevokeToken(tokenId, userId);
        return RedirectToAction(nameof(Index));
    }
}
