using Unity.Collections;

namespace Hecton8.World.Dots
{
    /// <summary>
    /// DOTS scatter backend placeholder.
    /// The project currently ships without the Unity Entities package, so runtime falls back to the classic jobs backend.
    /// </summary>
    internal sealed class ScatterEntitiesSimulationBackend : IScatterSimulationBackend
    {
        public ScatterSimulationBackendKind BackendKind => ScatterSimulationBackendKind.EntitiesDots;
        public bool IsInitialized => false;
        public bool IsJobActive => false;
        public bool IsJobCompleted => false;

        public void Initialize()
        {
        }

        public bool TrySchedule(
            ScatterSimulationConfig config,
            NativeArray<float> heightSamples,
            NativeArray<ScatterSimulationCellState> cellStates)
        {
            return false;
        }

        public bool TryComplete(out ScatterSimulationResult result)
        {
            result = default;
            return false;
        }

        public void ForceComplete()
        {
        }

        public void Dispose()
        {
        }
    }
}
