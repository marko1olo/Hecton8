#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.AI;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class StressDrivenSpawnDirectorLayoutTests
    {
        [Test]
        public void DirectorInputDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<DirectorInputDTO>(), Is.EqualTo(208));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.PlayerAup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.FloatingOriginAup)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.PlayerForward)), Is.EqualTo(96));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.CurrentBiomeMask)), Is.EqualTo(124));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.WorldSeed)), Is.EqualTo(156));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.SectorHash)), Is.EqualTo(172));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.MacroEcosystemStateHash)), Is.EqualTo(196));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.MacroEcosystemFlags)), Is.EqualTo(200));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(DirectorInputDTO), nameof(DirectorInputDTO.OriginShiftSequence)), Is.EqualTo(204));
        }
    }
}
#endif
