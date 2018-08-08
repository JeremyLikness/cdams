using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace cdams.tests
{
    [TestClass]
    public class Encoding
    {
        [TestMethod]
        public void Given_1_When_Encoded_Then_Returns_1()
        {
            Assert.AreEqual("1", Utility.Encode(1));
        }

        [TestMethod]
        public void Given_151_When_Encoded_Then_Returns_2Y()
        {
            Assert.AreEqual("2Y", Utility.Encode(151));
        }

        [TestMethod]
        public void Given_151_When_Encoded_Then_Does_Not_Return_B6()
        {
            Assert.AreNotEqual("B6", Utility.Encode(151));
        }
    }
}
