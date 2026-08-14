using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BallisticImpactVfxLayoutTests
    {
        [Test]
        public void BallisticImpactVfxDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<BallisticImpactVfxDTO>(), Is.EqualTo(80));
            Assert.That(Marshal.OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.Matrix)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.MaterialHash)).ToInt32(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.TargetEntityID)).ToInt32(), Is.EqualTo(68));
            Assert.That(Marshal.OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.Flags)).ToInt32(), Is.EqualTo(72));
            Assert.That(Marshal.OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.Frame)).ToInt32(), Is.EqualTo(76));
        }
    }
}
