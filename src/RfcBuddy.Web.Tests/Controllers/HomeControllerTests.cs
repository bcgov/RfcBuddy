using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RfcBuddy.Web.Models;

namespace RfcBuddy.Web.Controllers.Tests;

[TestClass()]
public class HomeControllerTests : TestBase
{
    #region Initialization
    private readonly HomeController _controller;

    public HomeControllerTests() : base()
    {
        var logger = InitializeLogger<HomeController>();
        var userService = MockUserService();
        var excelService = MockExcelService();
        var wordService = MockWordService();
        _controller = new(logger, userService, excelService, wordService)
        {
            ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
    #endregion

    [TestMethod()]
    public void IndexTest()
    {
        var result = _controller.Index();
        Assert.IsNotNull(result);
        Assert.IsTrue(result is ViewResult);
        var viewModel = (result as ViewResult)!.Model;
        Assert.IsTrue(viewModel is HomeViewModel);
    }

    [TestMethod()]
    public void ErrorTest()
    {
        var result = _controller.Error();
        Assert.IsNotNull(result);
        Assert.IsTrue(result is ViewResult);
        var viewModel = (result as ViewResult)!.Model;
        Assert.IsTrue(viewModel is ErrorViewModel);
    }
}