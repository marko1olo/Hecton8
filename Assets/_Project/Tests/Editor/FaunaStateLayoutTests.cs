using System.Runtime.InteropServices;
using Hecton8.Ecosystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class FaunaStateLayoutTests
    {
        [Test]
        public void FaunaStateDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<FaunaStateDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.PositionAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Biomass)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.SpeciesHash)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.EntityHash)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Flags)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.CarrionSlot)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<FaunaStateDTO>(nameof(FaunaStateDTO.Health01)).ToInt32(), Is.EqualTo(44));
        }
    }
}
