using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal struct PredictiveIndexedPagingProjection
    {
        public long CurrentSectorHash;
        public long ProjectedSectorHash;
        public int3 CurrentChunkId;
        public int3 ProjectedChunkId;
    }

    internal static class SavePredictivePagingMath
    {
        internal static bool TryComputeIndexedSectorProjection(
            Vector3 currentRuntimePosition,
            Vector3 worldVelocity,
            float lookaheadSeconds,
            int chunkSizeMeters,
            out PredictiveIndexedPagingProjection projection)
        {
            projection = default;
            if (!IsFinite(currentRuntimePosition) ||
                !IsFinite(worldVelocity) ||
                !math.isfinite(lookaheadSeconds) ||
                lookaheadSeconds <= 0f)
            {
                return false;
            }

            Vector3 projectedRuntimePosition = currentRuntimePosition + (worldVelocity * lookaheadSeconds);
            if (!IsFinite(projectedRuntimePosition))
                return false;

            AbsoluteUniversePosition currentAup = AbsoluteUniversePosition.FromRuntimePosition(currentRuntimePosition);
            AbsoluteUniversePosition projectedAup = AbsoluteUniversePosition.FromRuntimePosition(projectedRuntimePosition);
            int safeChunkSizeMeters = math.max(1, chunkSizeMeters);
            projection.CurrentChunkId = AbsoluteUniversePosition.ResolveChunkId(in currentAup, safeChunkSizeMeters);
            projection.ProjectedChunkId = AbsoluteUniversePosition.ResolveChunkId(in projectedAup, safeChunkSizeMeters);
            projection.CurrentSectorHash = SaveBinaryStorage.ComputePersistentWorldPagedSectorHash(in currentAup);
            projection.ProjectedSectorHash = SaveBinaryStorage.ComputePersistentWorldPagedSectorHash(in projectedAup);
            return true;
        }

        internal static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        internal static bool IsIndexedSectorBlockWithinFileBounds(
            long byteOffset,
            int compressedSize,
            long minimumByteOffset,
            long fileLength)
        {
            return SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                byteOffset,
                compressedSize,
                minimumByteOffset,
                fileLength);
        }
    }
}
