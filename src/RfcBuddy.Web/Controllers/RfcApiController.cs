using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;
using RfcBuddy.App.Core;
using RfcBuddy.Web.Models.Api;

namespace RfcBuddy.Web.Controllers;

[ApiController]
[Route("api/v1/rfcs")]
[Authorize(AuthenticationSchemes = "ApiToken")]
public class RfcApiController(
    ILogger<RfcApiController> logger,
    IUserService userService,
    IRfcService excelService,
    IRfcArchiveService archiveService,
    IRfcChangeTracker changeTracker) : ControllerBase
{
    private readonly ILogger<RfcApiController> _logger = logger;
    private readonly IUserService _userService = userService;
    private readonly IRfcService _excelService = excelService;
    private readonly IRfcArchiveService _archiveService = archiveService;
    private readonly IRfcChangeTracker _changeTracker = changeTracker;

    [HttpPost("search")]
    public async Task<ActionResult<RfcSearchResponse>> Search([FromBody] RfcSearchRequest request)
    {
        if (request is null)
        {
            return BadRequest();
        }

        List<string> includeKeywords = request.IncludeKeywords?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];
        List<string> ignoreKeywords = request.IgnoreKeywords?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];

        if (includeKeywords.Count == 0)
        {
            return new OkObjectResult(new RfcSearchResponse { TotalMatched = 0, Rfcs = [] });
        }

        try
        {
            await _excelService.GetLatestChanges().ConfigureAwait(true);
            List<Rfc> currentSchedule = _excelService.GetAllRfcs();
            _archiveService.UpdateArchive(currentSchedule);
            List<Rfc> completedRfcs = _archiveService.GetCompletedRfcs();
            List<Rfc> combined = currentSchedule.Union(completedRfcs, new RfcComparer()).ToList();
            List<Rfc> matched = _excelService.FilterRfcs(combined, includeKeywords, ignoreKeywords);

            List<PreviousRfc> baseline = _userService.GetPreviousRfcs(BaselineScope.Api);
            List<RfcResult> results = matched.Select(rfc => new RfcResult
            {
                RfcNumber = rfc.RfcNumber,
                ApprovalStatus = rfc.ApprovalStatus,
                Platform = rfc.Platform,
                AssetTags = rfc.AssetTags,
                StartDatePt = rfc.StartDate.ToUniversalTime().ToPt(),
                EndDatePt = rfc.EndDate.ToUniversalTime().ToPt(),
                Description = rfc.Description,
                RiskAssessment = rfc.RiskAssessment,
                ChangeStatus = _changeTracker.GetStatus(rfc, baseline)
            }).ToList();

            _userService.SavePreviousRfcs(matched, BaselineScope.Api);
            return new OkObjectResult(new RfcSearchResponse { TotalMatched = results.Count, Rfcs = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API search failed.");
            return Problem(statusCode: 500, title: "Unable to process RFC search");
        }
    }

    private sealed class RfcComparer : IEqualityComparer<Rfc>
    {
        public bool Equals(Rfc? x, Rfc? y) => string.Equals(x?.RfcNumber, y?.RfcNumber, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Rfc obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RfcNumber);
    }
}
