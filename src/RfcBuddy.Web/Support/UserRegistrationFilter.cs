using Microsoft.AspNetCore.Mvc.Filters;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Support;

public sealed class UserRegistrationFilter(IUserRegistryService userRegistryService) : IAsyncActionFilter
{
    private readonly IUserRegistryService _userRegistryService = userRegistryService;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            string userName = context.HttpContext.User.Identity.Name ?? "Generic User";
            string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(userName);

            string identity = context.HttpContext.User.FindFirst("name")?.Value
                ?? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? userName;
            string email = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? context.HttpContext.User.FindFirst("email")?.Value
                ?? string.Empty;

            _userRegistryService.EnsureRegistered(userId, identity, email);
        }

        await next().ConfigureAwait(false);
    }
}
