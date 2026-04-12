using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local factory for scatter backend schedule requests.
        /// Keeps request shaping and deterministic seed generation out of the main integration partial.
        /// </summary>
        private sealed class ScatterBackendRequestFactory
        {
            private readonly WorldProceduralScatterDirector _owner;

            public ScatterBackendRequestFactory(WorldProceduralScatterDirector owner)
            {
                _owner = owner;
            }

            public ScatterBackendScheduleRequest Create(
                Vector3 observerPosition,
                int totalCells,
                int groundBudget,
                int clusterBudget,
                int structureStride,
                int spawnStride)
            {
                if (_owner == null)
                    return default;

                return new ScatterBackendScheduleRequest(
                    observerPosition,
                    totalCells,
                    _owner._runtimeCellSize,
                    _owner._runtimeRadiusCells,
                    groundBudget,
                    clusterBudget,
                    structureStride,
                    spawnStride,
                    _owner.surfaceYOffset,
                    ComputeSeed(observerPosition));
            }

            private uint ComputeSeed(Vector3 observerPosition)
            {
                unchecked
                {
                    uint x = (uint)Mathf.Abs(Mathf.RoundToInt(observerPosition.x * 10f));
                    uint z = (uint)Mathf.Abs(Mathf.RoundToInt(observerPosition.z * 10f));
                    uint radius = (uint)Mathf.Max(1, _owner._runtimeRadiusCells);
                    uint cell = (uint)Mathf.Max(1, Mathf.RoundToInt(_owner._runtimeCellSize * 10f));
                    uint seed = 2166136261u;
                    seed = (seed ^ x) * 16777619u;
                    seed = (seed ^ z) * 16777619u;
                    seed = (seed ^ radius) * 16777619u;
                    seed = (seed ^ cell) * 16777619u;
                    return seed == 0u ? 1u : seed;
                }
            }
        }
    }
}
