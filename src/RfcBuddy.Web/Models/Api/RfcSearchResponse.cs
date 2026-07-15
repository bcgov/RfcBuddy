using System;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Core;

namespace RfcBuddy.Web.Models.Api;

public class RfcSearchResponse
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime GeneratedAtPt { get; set; } = DateTime.UtcNow.ToPt();

    public int TotalMatched { get; set; }

    public List<RfcResult> Rfcs { get; set; } = [];
}

public class RfcResult
{
    public string RfcNumber { get; set; } = string.Empty;

    public string ApprovalStatus { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string AssetTags { get; set; } = string.Empty;

    public DateTime StartDateUtc { get; set; }

    public DateTime EndDateUtc { get; set; }

    public DateTime StartDatePt { get; set; }

    public DateTime EndDatePt { get; set; }

    public string Description { get; set; } = string.Empty;

    public string RiskAssessment { get; set; } = string.Empty;

    public RfcChangeStatus ChangeStatus { get; set; }
}
