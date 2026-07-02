namespace RfcBuddy.Web.Models.Api;

public class RfcSearchRequest
{
    public List<string> IncludeKeywords { get; set; } = [];

    public List<string> IgnoreKeywords { get; set; } = [];
}
