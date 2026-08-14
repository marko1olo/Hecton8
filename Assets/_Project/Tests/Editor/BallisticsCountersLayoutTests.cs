using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticsCountersLayoutTests
    {
        [Test]
        public void BallisticsCountersDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticsCountersDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.TrajectoriesProcessed)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.HitCount)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.RicochetCount)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.NanGuardCount)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.SignalCount)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.RejectedCount)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.Flags)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.Frame)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.GlobalQualityWeight)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.SolveMicroseconds)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.ActiveTrajectoryBufferId)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<BallisticsCountersDTO>(nameof(BallisticsCountersDTO.PrimitiveCount)).ToInt32(), Is.EqualTo(44));
        }
    }
}
