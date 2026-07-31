using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RfcBuddy.App.Services;
using System.Security.Claims;

namespace RfcBuddy.Web.Controllers;

[Authorize]
[EnableRateLimiting("ApiPolicy")]
public class ApiTokensController(IApiTokenService apiTokenService) : Controller
{
    private readonly IApiTokenService _apiTokenService = apiTokenService;

    [HttpGet]
    public IActionResult Index()
    {
        string userId = GetHashedUserId();
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
        if (!ModelState.IsValid)
        {
            return View();
        }

        string userId = GetHashedUserId();
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
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Index));
        }

        string userId = GetHashedUserId();
        _ = _apiTokenService.RevokeToken(tokenId, userId);
        return RedirectToAction(nameof(Index));
    }

    private string GetHashedUserId()
    {
        string userUniqueId = User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.Identity?.Name
            ?? "Generic User";

        return RfcBuddy.App.Core.Cryptography.GetSha256Hash(userUniqueId);
    }
}
