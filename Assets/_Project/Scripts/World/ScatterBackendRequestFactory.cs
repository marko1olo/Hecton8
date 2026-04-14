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

            public ScatterBackendScheduleRequest Create(in ScatterBackendShadowScheduleContext context)
            {
                if (_owner == null)
                    return default;

                return new ScatterBackendScheduleRequest(
                    context.ObserverPosition,
                    context.TotalCells,
                    _owner._runtimeStreamingState.CellSize,
                    _owner._runtimeStreamingState.RadiusCells,
                    context.GroundBudget,
                    context.ClusterBudget,
                    context.StructureStride,
                    context.SpawnStride,
                    _owner.surfaceYOffset,
                    ComputeSeed(context.ObserverPosition),
                    ScatterSimulationEligibilityFlags.All,
                    ScatterSimulationSuppressionState.None,
                    ScatterSimulationDirtyFlags.Heights
                    | ScatterSimulationDirtyFlags.Eligibility
                    | ScatterSimulationDirtyFlags.Quotas
                    | ScatterSimulationDirtyFlags.Suppression
                    | ScatterSimulationDirtyFlags.Candidates,
                    context.ClassicParityReference);
            }

            private uint ComputeSeed(Vector3 observerPosition)
            {
                unchecked
                {
                    uint x = (uint)Mathf.Abs(Mathf.RoundToInt(observerPosition.x * 10f));
                    uint z = (uint)Mathf.Abs(Mathf.RoundToInt(observerPosition.z * 10f));
                    uint radius = (uint)Mathf.Max(1, _owner._runtimeStreamingState.RadiusCells);
                    uint cell = (uint)Mathf.Max(1, Mathf.RoundToInt(_owner._runtimeStreamingState.CellSize * 10f));
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
