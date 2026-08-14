using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticsTuningLayoutTests
    {
        [Test]
        public void BallisticsTuningDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticsTuningDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.DragCoefficient)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.LethalityThreshold)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.RicochetFriction)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.RicochetIncidenceThreshold)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.DamageEnergyScale)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.MaxRangeMeters)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.FloraBaseVelocity)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.FloraSpikeMassKg)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.GlobalQualityWeight)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.LimbAdmissionFloor)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.MockGridSpacingMeters)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.Revision)).ToInt32(), Is.EqualTo(44));
        }
    }
}
