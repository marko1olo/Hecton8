using System.Runtime.InteropServices;
using Hecton8.AI.Pathfinding;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VoxelPathWaypointLayoutTests
    {
        [Test]
        public void VoxelPathWaypointDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<VoxelPathWaypointDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<VoxelPathWaypointDTO>(nameof(VoxelPathWaypointDTO.PositionAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<VoxelPathWaypointDTO>(nameof(VoxelPathWaypointDTO.NodeIndex)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<VoxelPathWaypointDTO>(nameof(VoxelPathWaypointDTO.Flags)).ToInt32(), Is.EqualTo(28));
        }
    }
}
