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
    }
}
