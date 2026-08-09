using NUnit.Framework;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Pins the semantics of <see cref="WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter"/>,
    /// the accessor <c>HectonPlayerSpawner.ForceFallbackSpawn</c> aims its degraded spawn at.
    ///
    /// WHY THIS EXISTS. The accessor shipped with ZERO callers and ZERO tests: a repo-wide search for its
    /// name returned only its own declaration and the spawner. The fallback is the one release path in the
    /// spawner not behind <c>IsSpawnPointPhysicsReady</c>, so if this accessor is wrong the player is
    /// teleported onto unproven ground - the exact failure the Kinematic Arrest Gate exists to prevent
    /// (`AGENTS.md`:199). Every assertion below is a property the spawner actually relies on.
    ///
    /// The four cases that matter to the caller, and why:
    ///   - empty latch -> false, because the caller must keep world centre rather than read a zeroed Vector3;
    ///   - accepted centre satisfies <see cref="WorldChunkPhysicsBakedEvents.IsWorldPointPhysicsBaked"/>,
    ///     because the accessor's own doc claims acceptance parity with that predicate and the fallback's
    ///     telemetry scalar is computed from it;
    ///   - a terminally failed chunk is never returned, because the gate refuses those by design;
    ///   - <c>center.y</c> is the chunk's terrain BASE height, NOT a ground height - the spawner must keep
    ///     using its own water-level maths for Y, and taking this Y would drop the player under the sea floor.
    ///
    /// <see cref="WorldChunkPhysicsBakedEvents.ClearLatch"/> runs before and after every case: the latch is
    /// static state that would otherwise leak between tests and between these tests and the rest of the suite.
    /// </summary>
    public sealed class WorldChunkPhysicsBakedNearestCenterEditTests
    {
        private const float ChunkSize = 100f;

        [SetUp]
        public void ClearLatchBefore()
        {
            WorldChunkPhysicsBakedEvents.ClearLatch();
        }

        [TearDown]
        public void ClearLatchAfter()
        {
            WorldChunkPhysicsBakedEvents.ClearLatch();
        }

        /// <summary>
        /// Builds a signal that passes <c>WorldChunkPhysicsBakedSignal.IsValid</c>: non-zero terrain hash,
        /// non-zero flags, finite position/size, positive XZ extent.
        /// </summary>
        private static WorldChunkPhysicsBakedSignal MakeSignal(
            int chunkX,
            int chunkZ,
            float minX,
            float baseY,
            float minZ,
            uint flags)
        {
            WorldChunkPhysicsBakedSignal signal = default;
            signal.ChunkX = chunkX;
            signal.ChunkZ = chunkZ;
            signal.TerrainEntityHash = (uint)(chunkX * 73856093 ^ chunkZ * 19349663) | 1u;
            signal.Frame = 1u;
            signal.TerrainPosition = new float3(minX, baseY, minZ);
            signal.TerrainSize = new float3(ChunkSize, 50f, ChunkSize);
            signal.Flags = flags;
            return signal;
        }

        private static WorldChunkPhysicsBakedSignal MakeBakedSignal(int chunkX, int chunkZ, float baseY = -12f)
        {
            return MakeSignal(
                chunkX,
                chunkZ,
                chunkX * ChunkSize,
                baseY,
                chunkZ * ChunkSize,
                WorldChunkPhysicsBakedSignal.FlagColliderActive |
                WorldChunkPhysicsBakedSignal.FlagHeightmapSynced);
        }

        [Test]
        public void EmptyLatchReturnsFalseSoTheCallerKeepsItsOwnOrigin()
        {
            bool found = WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center);

            Assert.IsFalse(found, "An empty latch must not offer a spawn aim.");
            Assert.AreEqual(Vector3.zero, center, "The out value must stay default so a caller that ignores the bool cannot silently aim at a real-looking point.");
        }

        [Test]
        public void SingleBakedChunkReturnsItsFootprintCentreInWorldXz()
        {
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(3, 5)));

            bool found = WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center);

            Assert.IsTrue(found);
            // min corner (300, 500) + half extent (50, 50).
            Assert.AreEqual(350f, center.x, 0.001f);
            Assert.AreEqual(550f, center.z, 0.001f);
        }

        [Test]
        public void CentreYIsTheChunkTerrainBaseHeightNotAGroundHeight()
        {
            const float baseY = -37.5f;
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(0, 0, baseY)));

            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center));

            // If this ever becomes a mid-height or a ground height, HectonPlayerSpawner.ForceFallbackSpawn
            // must be revisited: it deliberately discards this Y and keeps waterLevel + spawnHeightOffset.
            Assert.AreEqual(baseY, center.y, 0.001f,
                "center.y must remain TerrainPosition.y - the spawner relies on owning vertical placement.");
        }

        [Test]
        public void NearestChunkWinsAmongSeveralBakedChunks()
        {
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(10, 10)));
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(1, 0)));
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(20, 20)));

            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center));

            // Chunk (1,0) centre is (150, 50); the others are far further from the origin.
            Assert.AreEqual(150f, center.x, 0.001f);
            Assert.AreEqual(50f, center.z, 0.001f);
        }

        [Test]
        public void TerminallyFailedChunkIsNeverOfferedAsASpawnAim()
        {
            WorldChunkPhysicsBakedSignal failed = MakeSignal(
                0,
                0,
                0f,
                -10f,
                0f,
                WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagBakeFailed);
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(failed));

            bool found = WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out _);

            Assert.IsFalse(found, "A chunk that reported terminal bake failure must never become a spawn aim.");
        }

        [Test]
        public void ChunkWithoutAnActiveColliderIsNotOfferedAsASpawnAim()
        {
            WorldChunkPhysicsBakedSignal colliderMissing = MakeSignal(
                0,
                0,
                0f,
                -10f,
                0f,
                WorldChunkPhysicsBakedSignal.FlagHeightmapSynced |
                WorldChunkPhysicsBakedSignal.FlagColliderMissing);
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(colliderMissing));

            bool found = WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out _);

            Assert.IsFalse(found, "A synced heightmap without a live collider is exactly what the gate refuses.");
        }

        /// <summary>
        /// The property the spawner's telemetry scalar depends on: an accepted aim must also satisfy the
        /// point predicate, so the degraded spawn reports case 0 (destination baked) rather than case 2
        /// (released onto unproven ground).
        /// </summary>
        [Test]
        public void AcceptedCentreAlsoSatisfiesTheWorldPointPredicate()
        {
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(2, 7)));

            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center));

            Assert.IsTrue(
                WorldChunkPhysicsBakedEvents.IsWorldPointPhysicsBaked(center.x, center.z),
                "Acceptance parity is the whole contract: the returned centre must pass the same predicate the readiness gate uses.");
        }

        [Test]
        public void FailedChunkDoesNotMaskAGoodOneFurtherAway()
        {
            WorldChunkPhysicsBakedSignal nearFailed = MakeSignal(
                0,
                0,
                0f,
                -10f,
                0f,
                WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagBakeFailed);
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(nearFailed));
            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryPublish(MakeBakedSignal(4, 0)));

            Assert.IsTrue(WorldChunkPhysicsBakedEvents.TryGetNearestPhysicsBakedChunkCenter(
                0f, 0f, out Vector3 center));

            // The nearer chunk is refused, so the aim must fall through to the good one at (450, 50).
            Assert.AreEqual(450f, center.x, 0.001f);
            Assert.AreEqual(50f, center.z, 0.001f);
        }
    }
}
