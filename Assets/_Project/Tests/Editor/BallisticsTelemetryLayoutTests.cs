using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticsTelemetryLayoutTests
    {
        [Test]
        public void BallisticsTelemetryEntry_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticsTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.Frame)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.TrajectoriesProcessed)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.HitCount)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.RicochetCount)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.NanGuardCount)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.Flags)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.SolveMicroseconds)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.GlobalQualityWeight)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.PrimitiveCount)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.SignalCount)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.RejectedCount)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<BallisticsTelemetryEntry>(nameof(BallisticsTelemetryEntry.ActiveTrajectoryBufferId)).ToInt32(), Is.EqualTo(44));
        }
    }
}
