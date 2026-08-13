#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    public class ShinobuLogisticsRouterTests
    {
        [Test]
        public void ValidateLayouts_ReturnsTrueAndCorrectSizes()
        {
            bool isValid = ShinobuLogisticsRouter.ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes);
            Assert.IsTrue(isValid, "ValidateLayouts should return true for valid struct layouts.");
            Assert.AreEqual(32, nodeBytes, "LogisticsNodeDTO should be exactly 32 bytes.");
            Assert.AreEqual(32, edgeBytes, "LogisticsEdgeDTO should be exactly 32 bytes.");
            Assert.AreEqual(32, tuningBytes, "LogisticsTuningDTO should be exactly 32 bytes.");
        }
    }
}
#endif
