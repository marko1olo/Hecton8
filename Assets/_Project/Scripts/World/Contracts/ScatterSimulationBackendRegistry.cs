using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Neutral registry for optional scatter simulation backends that live outside the main runtime assembly.
    /// Prevents a direct compile-time dependency from the owner assembly into DOTS-specific assemblies.
    /// </summary>
    internal static class ScatterSimulationBackendRegistry
    {
        private static IScatterSimulationBackendProvider _provider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _provider = null;
        }

        public static void RegisterProvider(IScatterSimulationBackendProvider provider)
        {
            if (provider == null)
                return;

            _provider = provider;
        }

        public static bool TryCreateBackend(ScatterSimulationBackendKind backendKind, out IScatterSimulationBackend backend)
        {
            backend = null;
            return _provider != null && _provider.TryCreateBackend(backendKind, out backend);
        }
    }

    /// <summary>
    /// Factory provider for optional scatter simulation backends outside the owner assembly.
    /// </summary>
    internal interface IScatterSimulationBackendProvider
    {
        bool TryCreateBackend(ScatterSimulationBackendKind backendKind, out IScatterSimulationBackend backend);
    }
}
