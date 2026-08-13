#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.AI.Ecosystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class ShinobuSpatialGridLayoutTests
    {
        [Test]
        public void SpatialGridEntryDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<SpatialGridEntryDTO>(), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SpatialGridEntryDTO), nameof(SpatialGridEntryDTO.EntityHashID)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SpatialGridEntryDTO), nameof(SpatialGridEntryDTO.EntityRowIndex)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SpatialGridEntryDTO), nameof(SpatialGridEntryDTO.CellHash)), Is.EqualTo(4));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SpatialGridEntryDTO), nameof(SpatialGridEntryDTO.LocalCellOffset)), Is.EqualTo(8));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SpatialGridEntryDTO), nameof(SpatialGridEntryDTO.CellFingerprint)), Is.EqualTo(8));
        }
    }
}
#endif
