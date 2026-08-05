using NUnit.Framework;
using Hecton8.Power;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
namespace Hecton8.Power.Tests
{
    public class BatteryChargerLogisticsLayoutAuditTests
    {
        [Test]
        public void ValidateAll_ReturnsTrue()
        {
            Assert.IsTrue(BatteryChargerLogisticsLayoutAudit.ValidateAll());
        }
    }
}
#endif
