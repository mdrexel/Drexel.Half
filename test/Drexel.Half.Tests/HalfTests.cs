using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Drexel._Half.Tests
{
    [TestClass]
    public class HalfTests
    {
        [TestMethod]
        public void Half_Parse_RoundTrip_Succeeds()
        {
            Half a = (Half)0.123F;
            string buffer = a.ToString();
            Half b = Half.Parse(buffer);

            Assert.AreEqual(a, b);
        }
    }
}
