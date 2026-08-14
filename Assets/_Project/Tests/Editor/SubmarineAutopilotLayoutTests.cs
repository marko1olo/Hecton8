using System.Runtime.InteropServices;
using Hecton8.Vehicles.Automation;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineAutopilotLayoutTests
    {
        [Test]
        public void AutopilotStateDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotStateDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.DesiredVelocity)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetSpeed)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.SubmarineHashID)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.NavFlags)).ToInt32(), Is.EqualTo(44));
        }

        [Test]
        public void AutopilotAvoidanceDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotAvoidanceDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Repulsion)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Forward)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.FlowVelocity)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.AverageSdfPressure01)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.NearestHitDistance)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.ActiveFeelerCount)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.HitFeelerCount)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Flags)).ToInt32(), Is.EqualTo(52));
        }
    }
}
