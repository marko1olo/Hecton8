using System.Runtime.InteropServices;
using Hecton8.Crafting;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class FabricationJobSnapshotLayoutTests
    {
        [Test]
        public void FabricationJobSnapshotDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<FabricationJobSnapshotDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<FabricationJobSnapshotDTO>(nameof(FabricationJobSnapshotDTO.TargetAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<FabricationJobSnapshotDTO>(nameof(FabricationJobSnapshotDTO.Progress01)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<FabricationJobSnapshotDTO>(nameof(FabricationJobSnapshotDTO.TargetPrefabHash)).ToInt32(), Is.EqualTo(28));
        }
    }
}
