using System.Runtime.InteropServices;
using Hecton8.Ecosystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CarrionRuntimeCountersLayoutTests
    {
        [Test]
        public void CarrionRuntimeCountersDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<CarrionRuntimeCountersDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.DeathIngressReadCursor)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.DeathIngressWriteCursor)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.DeathIngressCount)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.CarrionWriteCursor)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.ActiveCarrion)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.LastAttractionCount)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.TelemetryCursor)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.Flags)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.Frame)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.OverflowCount)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.LastInjectedBiomass)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.TotalActiveBiomass)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.StateHash)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<CarrionRuntimeCountersDTO>(nameof(CarrionRuntimeCountersDTO.LastProcessedDeaths)).ToInt32(), Is.EqualTo(52));
        }
    }
}
