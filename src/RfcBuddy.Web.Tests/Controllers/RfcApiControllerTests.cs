using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Models.Api;

namespace RfcBuddy.Web.Controllers.Tests;

[TestClass]
public class RfcApiControllerTests : TestBase
{
    [TestMethod]
    public async Task SearchReturnsFilteredAndAnnotatedRfcs()
    {
        var userService = new Mock<IUserService>();
        var excelService = new Mock<IRfcService>();
        var changeTracker = new Mock<IRfcChangeTracker>();
        var archiveService = new Mock<IRfcArchiveService>();

        var rfcs = new List<Rfc>
        {
            new("RFC-1") { AssetTags = "payments", Description = "payment gateway", RiskAssessment = "Low", StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(2) },
            new("RFC-2") { AssetTags = "identity", Description = "identity service", RiskAssessment = "Low", StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(2) },
            new("RFC-3") { AssetTags = "sandbox", Description = "sandbox", RiskAssessment = "Low", StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(2) }
        };

        excelService.Setup(x => x.GetLatestChanges()).Returns(Task.CompletedTask);
        excelService.Setup(x => x.GetAllRfcs()).Returns(rfcs);
        excelService.Setup(x => x.FilterRfcs(It.IsAny<IEnumerable<Rfc>>(), It.IsAny<List<string>>(), It.IsAny<List<string>>())).Returns((IEnumerable<Rfc> source, List<string> include, List<string> ignore) => source.Where(x => include.Any(k => x.AssetTags.Contains(k, StringComparison.OrdinalIgnoreCase)) && !ignore.Any(k => x.AssetTags.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList());
        changeTracker.Setup(x => x.GetStatus(It.IsAny<Rfc>(), It.IsAny<IReadOnlyList<PreviousRfc>>())).Returns(RfcChangeStatus.New);
        archiveService.Setup(x => x.UpdateArchive(It.IsAny<List<Rfc>>()));
        archiveService.Setup(x => x.GetCompletedRfcs()).Returns(new List<Rfc>());

        userService.Setup(x => x.GetPreviousRfcs(BaselineScope.Api)).Returns(new List<PreviousRfc>());
        userService.Setup(x => x.SavePreviousRfcs(It.IsAny<IEnumerable<Rfc>>(), BaselineScope.Api));

        var controller = new RfcApiController(
            TestBase.InitializeLogger<RfcApiController>(),
            userService.Object,
            excelService.Object,
            archiveService.Object,
            changeTracker.Object);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var request = new RfcSearchRequest { IncludeKeywords = ["payments"], IgnoreKeywords = ["sandbox"] };
        var result = await controller.Search(request);

        Assert.IsNotNull(result.Result);
        var response = (result.Result as ObjectResult)?.Value as RfcSearchResponse;
        Assert.IsNotNull(response);
        Assert.AreEqual(1, response!.TotalMatched);
        Assert.AreEqual("RFC-1", response.Rfcs[0].RfcNumber);
        Assert.AreEqual(RfcChangeStatus.New.ToString(), response.Rfcs[0].ChangeStatus.ToString());
    }
}
