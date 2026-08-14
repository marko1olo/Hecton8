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
        [Test]
        public void FlockingTelemetryEntry_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<ShinobuEcosystemBalancer.FlockingTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.Frame)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.StateHash)), Is.EqualTo(4));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.SimulatedBoidCount)), Is.EqualTo(8));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.NeighborSamplesTotal)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.AverageNeighbors)), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.ActiveThreatCount)), Is.EqualTo(20));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.BurstExecutionMicroseconds)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.GlobalQualityWeight)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.Flags)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.PanicBoidCount)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.MaxNeighborsPerBoid)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.SpatialHashOverflowCount)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.InvalidMathCount)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.SpatialHashMicroseconds)), Is.EqualTo(52));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.MatrixUploadMicroseconds)), Is.EqualTo(56));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(ShinobuEcosystemBalancer.FlockingTelemetryEntry), nameof(ShinobuEcosystemBalancer.FlockingTelemetryEntry.Pad0)), Is.EqualTo(60));
        }
    }
}
#endif
