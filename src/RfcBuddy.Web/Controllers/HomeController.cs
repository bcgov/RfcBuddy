using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Models;
using System.Diagnostics;

namespace RfcBuddy.Web.Controllers;

/// <summary>
/// This is a single-page app right now, so everything happens here.
/// </summary>
/// <param name="logger">The logger service</param>
/// <param name="appSettingsService">The service to get the AppSettings object from</param>
/// <param name="userService">The user service</param>
[Authorize]
public class HomeController(ILogger<HomeController> logger, IUserService userService, IRfcService excelService, IWordService wordService) : Controller
{
    /// <summary>
    /// Use to log any events
    /// </summary>
    private readonly ILogger<HomeController> _logger = logger;

    /// <summary>
    /// Service to store and retrieve user-specific data
    /// </summary>
    private readonly IUserService _userService = userService;

    /// <summary>
    /// Service to process the RFCs in the Excel sheet
    /// </summary>
    private readonly IRfcService _excelService = excelService;

    /// <summary>
    /// Service to turn the RFCs into a Word document
    /// </summary>
    private readonly IWordService _wordService = wordService;

    /// <summary>
    /// Gets the homepage
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult Index()
    {
        _userService.GetUserKeywords(out List<string> ministryKeywords, out List<string> generalKeywords, out List<string> ignoreKeywords);
        HomeViewModel model = new()
        {
            MinistryKeywords = string.Join(',', ministryKeywords),
            GeneralKeywords = string.Join(',', generalKeywords),
            IgnoreKeywords = string.Join(',', ignoreKeywords),
        };
        return View(model);
    }

    /// <summary>
    /// Processes the keywords and returns a Word document with the user's relevant changes
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(HomeViewModel model)
    {
        if (ModelState.IsValid)
        {
            _logger.LogInformation("Start processing: {currentDateTime}", DateTime.Now.ToString());
            try
            {
                List<string> ministryKeywords = [.. model.MinistryKeywords.Split(',')];
                List<string> generalKeywords = [.. model.GeneralKeywords.Split(',')];
                List<string> ignoreKeywords = [.. model.IgnoreKeywords.Split(',')];
                _userService.SaveUserKeywords(ministryKeywords, generalKeywords, ignoreKeywords);
                await _excelService.GetLatestChanges().ConfigureAwait(true);
                int totalRfcs = _excelService.ProcessRfcs(ministryKeywords, generalKeywords, ignoreKeywords, out List<Rfc> ministryRfcs, out List<Rfc> generalRfcs, out List<Rfc> otherRfcs);
                _logger.LogInformation("Total RFCs processed: {totalRfcs}", totalRfcs);
                _logger.LogInformation("Ministry RFCs found: {ministryRfcsCount}", ministryRfcs.Count);
                _logger.LogInformation("General RFCs found: {generalRfcsCount}", generalRfcs.Count);
                _logger.LogInformation("Other RFCs found: {otherRfcsCount}", otherRfcs.Count);
                List<PreviousRfc> previousRfcs = _userService.GetPreviousRfcs();
                Stream wordFileStream = new MemoryStream();
                _wordService.CreateWordFile(ref wordFileStream, ministryRfcs, generalRfcs, otherRfcs, previousRfcs);
                _userService.SavePreviousRfcs(ministryRfcs.Union(generalRfcs).Union(otherRfcs));
                wordFileStream.Position = 0;  //reset filestream for download
                System.Net.Mime.ContentDisposition contentDisposition = new()
                {
                    FileName = "RFC-" + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".docx",
                    Inline = true,
                };
                Response.Headers.Append("Content-Disposition", contentDisposition.ToString());
                return File(wordFileStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Exception: ");
                ModelState.AddModelError("Exception", "Error while processing the keywords: " + ex.Message);
            }
            _logger.LogInformation("Processing complete: {currentDateTime}", DateTime.Now.ToString());
        }
        return View(model);
    }

    /// <summary>
    /// In case there's an unhandled server error
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        _logger.LogError("Request error: {RequestId}", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
