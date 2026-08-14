#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.Ecosystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class NutrientDriftLayoutTests
    {
        [Test]
        public void NutrientSourceDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<NutrientSourceDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.Aup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.RadiusMeters)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.InjectionDensity)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.Temperature)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.ToxinLevel)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.SourceHash)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientSourceDTO), nameof(NutrientSourceDTO.Flags)), Is.EqualTo(44));
        }

        [Test]
        public void FluidGridTelemetryEntry_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<FluidGridTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.GridOriginAup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.TotalDensity)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.MaxDensity)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.MaxVelocity)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.BurstExecutionMicroseconds)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.Frame)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.Flags)), Is.EqualTo(52));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.StateHash)), Is.EqualTo(56));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.ActiveSources)), Is.EqualTo(60));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(FluidGridTelemetryEntry), nameof(FluidGridTelemetryEntry.ActiveAxis)), Is.EqualTo(62));
        }
        [Test]
        public void NutrientDriftGridHeaderDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<NutrientDriftGridHeaderDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.GridOriginAup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.CellSizeMeters)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.TotalDensity)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.LastSolverMicroseconds)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.GlobalQualityWeight)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.ActiveAxis)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.ActiveSources)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.FrontBufferId)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.BackBufferId)), Is.EqualTo(52));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.Flags)), Is.EqualTo(56));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(NutrientDriftGridHeaderDTO), nameof(NutrientDriftGridHeaderDTO.StateHash)), Is.EqualTo(60));
        }
    }
}
#endif
