namespace RfcBuddy.Web.Models;

/// <summary>
/// Used to store information in case of a server error.
/// </summary>
public class ErrorViewModel
{
    /// <summary>
    /// The request ID / Trace ID
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Whether or not to show the request ID.
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
