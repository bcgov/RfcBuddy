using System.ComponentModel.DataAnnotations;

namespace RfcBuddy.Web.Models;

/// <summary>
/// Holds the user's various comma-separated keyword collections
/// </summary>
public class HomeViewModel
{
    /// <summary>
    /// Ministry-specific keywords, e.g. server names or file shares
    /// </summary>
    [Required(ErrorMessage = @"Please enter the ministry-specific keywords, or ""N/A"" to confirm that this field should be ignored.")]
    [Display(Name = "Ministry-specific keywords, e.g. server names or file shares")]
    public string MinistryKeywords { get; set; } = string.Empty;

    /// <summary>
    /// Generic keywords that affect all ministries, e.g. Siteminder
    /// </summary>
    [Required(ErrorMessage = @"Please enter the generic keywords, or ""N/A"" to confirm that this field should be ignored.")]
    [Display(Name = "Generic keywords that affect all ministries, e.g. Siteminder")]
    public string GeneralKeywords { get; set; } = string.Empty;

    /// <summary>
    /// Keywords for RFCs that should be filtered out
    /// </summary>
    [Required(ErrorMessage = @"Please enter the keywords to filter out, or ""N/A"" to confirm that this field should be ignored.")]
    [Display(Name = "Keywords for RFCs that should be filtered out")]
    public string IgnoreKeywords { get; set; } = string.Empty;
}
