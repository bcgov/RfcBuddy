using Microsoft.AspNetCore.Authorization;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Authorization;

public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminAuthorizationHandler(IUserRegistryService userRegistryService) : AuthorizationHandler<AdminRequirement>
{
    private readonly IUserRegistryService _userRegistryService = userRegistryService;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        string? userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId) && _userRegistryService.IsAdmin(userId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
