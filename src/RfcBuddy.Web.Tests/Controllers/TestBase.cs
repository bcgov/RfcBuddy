using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Controllers.Tests;

public class TestBase
{
    #region Initialization

    protected TestBase()
    {
    }
    #endregion

    #region Common Methods
    protected static ILogger<T> InitializeLogger<T>()
    {
        var serviceProvider = new ServiceCollection().AddLogging(builder => builder.AddDebug()).BuildServiceProvider();
        var factory = serviceProvider.GetService<ILoggerFactory>();
        return factory!.CreateLogger<T>();
    }

    protected static IUserService MockUserService()
    {
        var mockService = new Mock<IUserService>();
        List<string> list1 = [];
        List<string> list2 = [];
        List<string> list3 = [];
        mockService.Setup(x => x.GetUserKeywords(out list1, out list2, out list3));
        return mockService.Object;
    }

    protected static IRfcService MockExcelService()
    {
        var mockService = new Mock<IRfcService>();
        return mockService.Object;
    }

    protected static IWordService MockWordService()
    {
        var mockService = new Mock<IWordService>();
        return mockService.Object;
    }
    #endregion
}
