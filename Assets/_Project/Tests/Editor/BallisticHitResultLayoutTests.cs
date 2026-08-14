using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticHitResultLayoutTests
    {
        [Test]
        public void BallisticHitResultDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticHitResultDTO>(), Is.EqualTo(112));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.HitAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.LocalHitPoint)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.Normal)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.ImpactDirection)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.Damage)).ToInt32(), Is.EqualTo(60));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.RemainingVelocity)).ToInt32(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.Distance)).ToInt32(), Is.EqualTo(68));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.TargetEntityID)).ToInt32(), Is.EqualTo(72));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.SourceEntityID)).ToInt32(), Is.EqualTo(76));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.WeaponHash)).ToInt32(), Is.EqualTo(80));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.MaterialHash)).ToInt32(), Is.EqualTo(84));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.Flags)).ToInt32(), Is.EqualTo(88));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.Frame)).ToInt32(), Is.EqualTo(92));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.RicochetCount)).ToInt32(), Is.EqualTo(96));
            Assert.That(Marshal.OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.PrimitiveHash)).ToInt32(), Is.EqualTo(100));
        }
    }
}
