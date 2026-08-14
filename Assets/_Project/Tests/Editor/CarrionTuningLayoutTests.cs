using System.Runtime.InteropServices;
using Hecton8.Ecosystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CarrionTuningLayoutTests
    {
        [Test]
        public void CarrionTuningDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<CarrionTuningDTO>(), Is.EqualTo(128));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.GridOriginAup)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.CellSizeMeters)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.BaseDecayRate)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.LinearDecayRate)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.DefaultBiomass)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.EpsilonBiomass)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ColdTemperatureMultiplier)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.HotTemperatureMultiplier)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.NutrientInjectionMultiplier)).ToInt32(), Is.EqualTo(52));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ScavengerAttractionRadius)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ScavengerFoodScalar)).ToInt32(), Is.EqualTo(60));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.GlobalQualityWeight)).ToInt32(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.DeltaSeconds)).ToInt32(), Is.EqualTo(68));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ActiveAxis)).ToInt32(), Is.EqualTo(72));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ActiveCellCount)).ToInt32(), Is.EqualTo(76));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.Flags)).ToInt32(), Is.EqualTo(80));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.FrameIndex)).ToInt32(), Is.EqualTo(84));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.StateHash)).ToInt32(), Is.EqualTo(88));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ProfileHash)).ToInt32(), Is.EqualTo(92));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.MaxAttractionIntensity)).ToInt32(), Is.EqualTo(96));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.ToxicityNutrientPenalty)).ToInt32(), Is.EqualTo(100));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.TemperatureLowCelsius)).ToInt32(), Is.EqualTo(104));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.TemperatureHighCelsius)).ToInt32(), Is.EqualTo(108));
            Assert.That(Marshal.OffsetOf<CarrionTuningDTO>(nameof(CarrionTuningDTO.RouteHash)).ToInt32(), Is.EqualTo(112));
        }
    }
}
