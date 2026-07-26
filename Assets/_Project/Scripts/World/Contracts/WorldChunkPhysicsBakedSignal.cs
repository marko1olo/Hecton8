using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.World
{
    internal static class WorldChunkPhysicsBakedSignalLayout
    {
        public const int WorldChunkPhysicsBakedSignalStrideBytes = 64;
    }

    /// <summary>
    /// R99: broadcast when a world chunk's TERRAIN COLLIDER is proven live, not merely when its heightmap
    /// was applied. `AGENTS.md` requires the spawner/KCC Kinematic Arrest Gate to hold the player suspended
    /// until this signal arrives for the spawn chunk, and bans time-based loading timeouts — but the
    /// identifier existed only in that document: there was no struct, no publisher and no consumer, so the
    /// gate could never be satisfied and the player was released against possibly-uncollidable terrain.
    ///
    /// The chunk is identified BOTH by tile coordinate (identity/telemetry) and by its world-space XZ
    /// footprint. Queries use the footprint on purpose: the project currently carries three disagreeing
    /// chunk-coordinate conventions (WorldChunkCoordinate float-floor, the pager's double/long, and the
    /// spawner's own), and a readiness gate must not be able to disagree with the publisher about which
    /// chunk a point belongs to.
    ///
    /// Failure is a first-class outcome: error paths MUST publish with <see cref="FlagBakeFailed"/> so the
    /// gate always resolves instead of hanging forever.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = WorldChunkPhysicsBakedSignalLayout.WorldChunkPhysicsBakedSignalStrideBytes)]
    public struct WorldChunkPhysicsBakedSignal : ISignal
    {
        /// <summary>Collider exists, is enabled, and its terrain data matches the applied tile.</summary>
        public const uint FlagColliderActive = 1u << 0;
        /// <summary>Heightmap was synchronized to the collider before this signal was published.</summary>
        public const uint FlagHeightmapSynced = 1u << 1;
        /// <summary>Terminal failure for this chunk. The gate must resolve (degraded), never wait forever.</summary>
        public const uint FlagBakeFailed = 1u << 2;
        /// <summary>Tile was applied but carries no usable TerrainCollider.</summary>
        public const uint FlagColliderMissing = 1u << 3;

        [FieldOffset(0)] public int ChunkX;
        [FieldOffset(4)] public int ChunkZ;
        [FieldOffset(8)] public uint TerrainEntityHash;
        [FieldOffset(12)] public uint Frame;
        /// <summary>World-space minimum corner of the chunk footprint (Y is the terrain base height).</summary>
        [FieldOffset(16)] public float3 TerrainPosition;
        /// <summary>World-space size of the chunk footprint.</summary>
        [FieldOffset(28)] public float3 TerrainSize;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;

        public bool IsPhysicsUsable => (Flags & FlagColliderActive) != 0u && (Flags & FlagBakeFailed) == 0u;

        public static bool IsValid(in WorldChunkPhysicsBakedSignal signal)
        {
            return signal.TerrainEntityHash != 0u &&
                signal.Flags != 0u &&
                math.all(math.isfinite(signal.TerrainPosition)) &&
                math.all(math.isfinite(signal.TerrainSize)) &&
                signal.TerrainSize.x > 0f &&
                signal.TerrainSize.z > 0f;
        }

        /// <summary>True when the world-space XZ point lies inside this chunk's footprint.</summary>
        public static bool ContainsWorldXZ(in WorldChunkPhysicsBakedSignal signal, float worldX, float worldZ)
        {
            float minX = signal.TerrainPosition.x;
            float minZ = signal.TerrainPosition.z;
            return worldX >= minX &&
                worldZ >= minZ &&
                worldX <= minX + signal.TerrainSize.x &&
                worldZ <= minZ + signal.TerrainSize.z;
        }
    }
}
