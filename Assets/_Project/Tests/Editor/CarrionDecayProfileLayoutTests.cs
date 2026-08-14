using System.Runtime.InteropServices;
using Hecton8.Ecosystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CarrionDecayProfileLayoutTests
    {
        [Test]
        public void CarrionDecayProfileDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<CarrionDecayProfileDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.SpeciesHash)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.BaseDecayRate)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.ToxicityEmissionRate)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.NutrientMultiplier)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.AttractionMultiplier)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.BiomassMultiplier)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.Flags)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<CarrionDecayProfileDTO>(nameof(CarrionDecayProfileDTO.SourceHash)).ToInt32(), Is.EqualTo(28));
        }
    }
}
