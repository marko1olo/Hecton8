#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.Ecosystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class CarrionLayoutTests
    {
        [Test]
        public void CarrionStateDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<CarrionStateDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.CorpseAUP)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.InitialBiomass)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.CurrentBiomass)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.OriginalSpeciesHash)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.ToxicityEmissionRate)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.AgeSeconds)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.BiomassLostLastTick)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.DecayRate)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.Flags)), Is.EqualTo(52));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionStateDTO), nameof(CarrionStateDTO.EntityHash)), Is.EqualTo(56));
        }

        [Test]
        public void CarrionDeathSignalDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<CarrionDeathSignalDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.CorpseAUP)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.BiomassScale)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.OriginalSpeciesHash)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.SourceHash)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.EntityHash)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.Flags)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionDeathSignalDTO), nameof(CarrionDeathSignalDTO.ToxicitySeed)), Is.EqualTo(44));
        }
        [Test]
        public void CarrionTelemetryEntry_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<CarrionTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.GridOriginAup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.ActiveBiomass)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.BurstExecutionMicroseconds)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.AttractionCount)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.Frame)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.StateHash)), Is.EqualTo(56));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionTelemetryEntry), nameof(CarrionTelemetryEntry.Overflows)), Is.EqualTo(60));
        }

        [Test]
        public void CarrionAttractionRecordDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<CarrionAttractionRecordDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.CorpseAUP)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.FoodValue)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.RadiusMeters)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.OriginalSpeciesHash)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.Toxicity)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.Flags)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(CarrionAttractionRecordDTO), nameof(CarrionAttractionRecordDTO.Temperature)), Is.EqualTo(44));
        }

    }
}
#endif
