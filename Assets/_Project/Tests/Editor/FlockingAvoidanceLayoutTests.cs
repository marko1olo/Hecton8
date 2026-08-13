#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.AI.Ecosystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class FlockingAvoidanceLayoutTests
    {
        [Test]
        public void FlockingThreatDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<ShinobuEcosystemBalancer.FlockingThreatDTO>(), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.LocalPosition)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.RadiusMeters)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.Intensity01)), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.SourceId)), Is.EqualTo(20));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.TypeHash)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingThreatDTO), nameof(ShinobuEcosystemBalancer.FlockingThreatDTO.DirectionalBias)), Is.EqualTo(28));
        }

        [Test]
        public void AmbientEntityDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<ShinobuEcosystemBalancer.AmbientEntityDTO>(), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.AmbientEntityDTO), nameof(ShinobuEcosystemBalancer.AmbientEntityDTO.Position)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.AmbientEntityDTO), nameof(ShinobuEcosystemBalancer.AmbientEntityDTO.Velocity)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.AmbientEntityDTO), nameof(ShinobuEcosystemBalancer.AmbientEntityDTO.SpeciesHash)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.AmbientEntityDTO), nameof(ShinobuEcosystemBalancer.AmbientEntityDTO.Biomass)), Is.EqualTo(28));
        }
    }
}
#endif
