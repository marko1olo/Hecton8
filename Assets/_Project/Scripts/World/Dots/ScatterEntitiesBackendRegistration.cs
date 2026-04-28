using UnityEngine;

namespace Hecton8.World.Dots
{
    internal static class ScatterEntitiesBackendRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            // Unity Entities is not available in this project state.
            // ScatterRuntimeBackendFacade falls back to the classic jobs backend.
        }
    }
}
