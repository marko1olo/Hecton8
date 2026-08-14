using System.Runtime.InteropServices;
using Hecton8.Crafting;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class FabricationJobLayoutTests
    {
        [Test]
        public void FabricationJobDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<FabricationJobDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<FabricationJobDTO>(nameof(FabricationJobDTO.TargetAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<FabricationJobDTO>(nameof(FabricationJobDTO.Progress01)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<FabricationJobDTO>(nameof(FabricationJobDTO.TargetPrefabHash)).ToInt32(), Is.EqualTo(28));
        }
    }
}
