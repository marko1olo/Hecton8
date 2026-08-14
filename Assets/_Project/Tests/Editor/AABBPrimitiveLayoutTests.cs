using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AABBPrimitiveLayoutTests
    {
        [Test]
        public void AABBPrimitiveDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AABBPrimitiveDTO>(), Is.EqualTo(96));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.CenterAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.HalfExtents)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.TargetEntityID)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.Rotation)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.MaterialHash)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.PrimitiveHash)).ToInt32(), Is.EqualTo(60));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.Flags)).ToInt32(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.DamageMultiplier)).ToInt32(), Is.EqualTo(68));
            Assert.That(Marshal.OffsetOf<AABBPrimitiveDTO>(nameof(AABBPrimitiveDTO.ArmorScalar)).ToInt32(), Is.EqualTo(72));
        }
    }
}
