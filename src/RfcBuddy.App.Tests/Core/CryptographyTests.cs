namespace RfcBuddy.App.Core.Tests
{
    [TestClass()]
    public class CryptographyTests
    {
        [TestMethod()]
        public void GetSha256HashTest()
        {
            string value = Cryptography.GetSha256Hash("Unit Test");
            Assert.AreEqual("4d7eefdafd575f112899dd31e5c9e50203e8d32dcbe7d78967bf8cbddbb0f938", value);
        }

        [TestMethod()]
        public void VerifySha256HashTest()
        {
            bool value = Cryptography.VerifySha256Hash("Unit Test", "4d7eefdafd575f112899dd31e5c9e50203e8d32dcbe7d78967bf8cbddbb0f938");
            Assert.IsTrue(value);
        }
    }
}