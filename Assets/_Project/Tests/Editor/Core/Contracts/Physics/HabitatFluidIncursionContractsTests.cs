#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Core.Contracts.Physics;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Core.Contracts.Physics
{
    [TestFixture]
    public class HabitatFluidIncursionContractsTests
    {
        [Test]
        public void FluidCompartmentDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<FluidCompartmentDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.LocalCenterOfMass)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.NodeHashID)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.CurrentWaterVolume)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.MaxWaterVolume)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.WaterLevelHeight01)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidCompartmentDTO), nameof(FluidCompartmentDTO.Flags)), Is.EqualTo(40));
        }


        [Test]
        public void ValidateFluidCompartmentLayout_ReturnsTrue()
        {
            Assert.IsTrue(FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout(), "FluidCompartmentDTO layout validation failed.");
        }
    }
}
#endif
