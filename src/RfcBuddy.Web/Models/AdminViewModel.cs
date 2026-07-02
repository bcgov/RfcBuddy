using System.Collections.Generic;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Models;

public sealed class AdminViewModel
{
    public required IReadOnlyList<UserListEntry> Users { get; init; }
    public required IReadOnlyList<ApiToken> ActiveTokens { get; init; }
}
