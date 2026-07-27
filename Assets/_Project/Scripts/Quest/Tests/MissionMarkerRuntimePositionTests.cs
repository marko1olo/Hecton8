using Hecton8.Quest;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Quest.Tests
{
    /// <summary>
    /// Locks the floating-origin contract of <see cref="MissionMarkerSystem.TryResolveMarkerRuntimePosition"/>.
    ///
    /// Regression guarded: the authored-marker branch of the marker resolver used to draw from
    /// <c>QuestMarkerCache.FallbackPosition</c>, a runtime-space vector captured once when the cache entry
    /// was first built. Runtime space is rebased by <c>HectonFloatingOrigin</c>, so after any origin shift
    /// the marker was drawn at the stale runtime coordinate while the range test still used the correct
    /// absolute anchor. Nothing threw: the objective marker simply sat the accumulated rebase distance away
    /// from the objective, and the player swam to the wrong place.
    /// </summary>
    [TestFixture]
    public sealed class MissionMarkerRuntimePositionTests
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void ZeroOrigin_ResolvesAbsoluteAnchorUnchanged()
        {
            AbsoluteUniversePosition markerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(1000d, -40d, 2500d));
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);

            Assert.IsTrue(MissionMarkerSystem.TryResolveMarkerRuntimePosition(
                in markerAup,
                in originAup,
                out Vector3 runtimePosition));

            Assert.AreEqual(1000f, runtimePosition.x, Tolerance);
            Assert.AreEqual(-40f, runtimePosition.y, Tolerance);
            Assert.AreEqual(2500f, runtimePosition.z, Tolerance);
        }

        [Test]
        public void ShiftedOrigin_MovesDrawPositionByTheNegatedShift()
        {
            AbsoluteUniversePosition markerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(1000d, -40d, 2500d));
            AbsoluteUniversePosition originBefore = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
            AbsoluteUniversePosition originAfter = AbsoluteUniversePosition.FromAbsolutePosition(new double3(900d, 0d, 2400d));

            Assert.IsTrue(MissionMarkerSystem.TryResolveMarkerRuntimePosition(
                in markerAup,
                in originBefore,
                out Vector3 before));
            Assert.IsTrue(MissionMarkerSystem.TryResolveMarkerRuntimePosition(
                in markerAup,
                in originAfter,
                out Vector3 after));

            // The absolute anchor is unchanged, so the runtime draw position must move by exactly the
            // negated origin shift. A cached runtime vector would report the same value twice.
            Assert.AreEqual(100f, after.x, Tolerance);
            Assert.AreEqual(-40f, after.y, Tolerance);
            Assert.AreEqual(100f, after.z, Tolerance);
            Assert.AreEqual(-900f, after.x - before.x, Tolerance, "Marker draw position must follow the origin rebase on X.");
            Assert.AreEqual(0f, after.y - before.y, Tolerance);
            Assert.AreEqual(-2400f, after.z - before.z, Tolerance, "Marker draw position must follow the origin rebase on Z.");
        }

        [Test]
        public void NonFiniteOrigin_FailsClosedWithoutEmittingPosition()
        {
            AbsoluteUniversePosition markerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(1000d, -40d, 2500d));
            AbsoluteUniversePosition invalidOrigin = AbsoluteUniversePosition.Invalid();

            Assert.IsFalse(MissionMarkerSystem.TryResolveMarkerRuntimePosition(
                in markerAup,
                in invalidOrigin,
                out Vector3 runtimePosition));

            Assert.AreEqual(Vector3.zero, runtimePosition, "A refused resolve must not hand a non-finite matrix to the instanced batch.");
        }

        [Test]
        public void NonFiniteMarkerAnchor_FailsClosedWithoutEmittingPosition()
        {
            AbsoluteUniversePosition invalidMarker = AbsoluteUniversePosition.Invalid();
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);

            Assert.IsFalse(MissionMarkerSystem.TryResolveMarkerRuntimePosition(
                in invalidMarker,
                in originAup,
                out Vector3 runtimePosition));

            Assert.AreEqual(Vector3.zero, runtimePosition);
        }
    }
}
