using Microsoft.Extensions.Logging.Abstractions;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class ApiTokenServiceTests
{
    [TestMethod]
    public void CreateAndAuthenticateTokenRoundTrip()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var service = new ApiTokenService(tempFolder, NullLogger<ApiTokenService>.Instance);
            var result = service.CreateToken("user-1", "CI token", DateTime.UtcNow.AddDays(30));

            Assert.IsFalse(string.IsNullOrWhiteSpace(result.RawToken));
            Assert.AreEqual("CI token", result.Token.Label);
            Assert.AreEqual(1, service.GetTokensForUser("user-1").Count);

            var authenticated = service.Authenticate(result.RawToken);
            Assert.IsNotNull(authenticated);
            Assert.AreEqual("user-1", authenticated!.OwnerUserId);

            Assert.IsTrue(service.RevokeToken(result.Token.Id, "user-1"));
            Assert.IsNull(service.Authenticate(result.RawToken));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
