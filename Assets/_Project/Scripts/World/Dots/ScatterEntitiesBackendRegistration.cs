using UnityEngine;

namespace Hecton8.World.Dots
{
    internal sealed class ScatterEntitiesSimulationBackendProvider : IScatterSimulationBackendProvider
    {
        public bool TryCreateBackend(ScatterSimulationBackendKind backendKind, out IScatterSimulationBackend backend)
        {
            backend = null;
            if (backendKind != ScatterSimulationBackendKind.EntitiesDots)
                return false;

            backend = new ScatterEntitiesSimulationBackend();
            return true;
        }
    }

    internal static class ScatterEntitiesBackendRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            // COLD ALLOC: ScatterEntitiesSimulationBackendProvider[1] - optional DOTS scatter backend provider - owner: ScatterEntitiesBackendRegistration
            ScatterSimulationBackendRegistry.RegisterProvider(new ScatterEntitiesSimulationBackendProvider());
        }
    }
}
