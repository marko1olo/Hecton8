using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticTrajectoryLayoutTests
    {
        [Test]
        public void BallisticTrajectoryDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticTrajectoryDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.OriginAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Direction)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Velocity)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Mass)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.WeaponHash)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.SourceEntityID)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Flags)).ToInt32(), Is.EqualTo(52));
        }
    }
}
