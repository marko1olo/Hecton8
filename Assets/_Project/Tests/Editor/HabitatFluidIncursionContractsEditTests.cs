#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Core.Contracts.Physics;

namespace Hecton8.Core.Contracts.Physics
{
    public class HabitatFluidIncursionContractsEditTests
    {
        [Test]
        public void ValidateFluidCompartmentLayout_ReturnsTrue()
        {
            Assert.IsTrue(FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout());
        }
    }
}
#endif
