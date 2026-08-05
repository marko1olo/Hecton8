#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Core.Contracts.Physics;

namespace Hecton8.Tests.Core.Contracts.Physics
{
    [TestFixture]
    public class HabitatFluidIncursionContractsTests
    {
        [Test]
        public void ValidateFluidCompartmentLayout_ReturnsTrue()
        {
            Assert.IsTrue(FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout(), "FluidCompartmentDTO layout validation failed.");
        }
    }
}
#endif
