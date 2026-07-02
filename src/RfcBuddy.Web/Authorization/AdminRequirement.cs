using Microsoft.AspNetCore.Authorization;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Authorization;

public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminAuthorizationHandler(IUserRegistryService userRegistryService) : AuthorizationHandler<AdminRequirement>
{
    private readonly IUserRegistryService _userRegistryService = userRegistryService;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string userName = context.User.Identity.Name ?? "Generic User";
            string userId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(userName);
            if (_userRegistryService.IsAdmin(userId))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
