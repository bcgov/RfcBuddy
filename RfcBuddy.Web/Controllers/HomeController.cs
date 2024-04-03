using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Models;
using System.Diagnostics;

namespace RfcBuddy.Web.Controllers;

[Authorize]
public class HomeController(ILogger<HomeController> logger, IAppSettingsService appSettingsService, IUserService userService) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;
    private readonly AppSettings _appSettings = appSettingsService.AppSettings;
    private readonly IUserService _userService = userService;

    //Internal variables
    private const string excelFileName = "ServiceNow-365-Day-Changes.xlsx";
    private readonly string wordFileName = "RFC-" + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".docx";

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
                if (!Directory.Exists(_appSettings.DataFolder))
                {
                    Directory.CreateDirectory(_appSettings.DataFolder);
                }
                await WebHelper.GetLatestChanges(_appSettings.DataFolder + "/" + excelFileName, _appSettings.SourceUrl, _appSettings.SourceUser, _appSettings.SourcePassword, _appSettings.SourceRefreshMinutes).ConfigureAwait(true);
                ExcelHelper.ProcessExcelSheet(_appSettings.DataFolder + "/" + excelFileName, ministryKeywords, generalKeywords, ignoreKeywords, out List<Rfc> ministryRfcs, out List<Rfc> generalRfcs, out List<Rfc> otherRfcs, out int totalRfcs);
                _logger.LogInformation("Total RFCs processed: {totalRfcs}", totalRfcs);
                _logger.LogInformation("Ministry RFCs found: {ministryRfcsCount}", ministryRfcs.Count);
                _logger.LogInformation("General RFCs found: {generalRfcsCount}", generalRfcs.Count);
                _logger.LogInformation("Other RFCs found: {otherRfcsCount}", otherRfcs.Count);
                List<PreviousRfc> previousRfcs = _userService.GetPreviousRfcs();
                WordHelper wordHelper = new();
                Stream wordFileStream = new MemoryStream();
                wordHelper.CreateWordFile(ref wordFileStream, ministryRfcs, generalRfcs, otherRfcs, previousRfcs);
                _userService.SavePreviousRfcs(ministryRfcs.Union(generalRfcs).Union(otherRfcs));
                wordFileStream.Position = 0;  //reset filestream for download
                var cd = new System.Net.Mime.ContentDisposition
                {
                    FileName = wordFileName,
                    Inline = true,
                };
                Response.Headers.Append("Content-Disposition", cd.ToString());
                return File(wordFileStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Exception: {stackTrace}", ex);
                ModelState.AddModelError("Exception", "Error while processing the keywords: " + ex.Message);
            }
            _logger.LogInformation("Processing complete: {currentDateTime}", DateTime.Now.ToString());
        }
        return View(model);
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        _logger.LogError("Request error: {RequestId}", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
