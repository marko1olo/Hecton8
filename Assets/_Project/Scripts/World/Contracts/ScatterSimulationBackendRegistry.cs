using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Neutral registry for optional scatter simulation backends that live outside the main runtime assembly.
    /// Prevents a direct compile-time dependency from the owner assembly into DOTS-specific assemblies.
    /// </summary>
    public static class ScatterSimulationBackendRegistry
    {
        private static IScatterSimulationBackendProvider _provider;
        private static uint _version;

        public static uint Version => _version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _provider = null;
            unchecked
            {
                _version++;
            }
        }

        public static void RegisterProvider(IScatterSimulationBackendProvider provider)
        {
            if (provider == null)
                return;

            _provider = provider;
            unchecked
            {
                _version++;
            }
        }

        public static bool TryCreateBackend(ScatterSimulationBackendKind backendKind, out IScatterSimulationBackend backend)
        {
            backend = null;
            if (_provider == null)
                return false;

            if (!_provider.TryCreateBackend(backendKind, out backend) || backend == null)
            {
                backend = null;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Factory provider for optional scatter simulation backends outside the owner assembly.
    /// </summary>
    public interface IScatterSimulationBackendProvider
    {
        bool TryCreateBackend(ScatterSimulationBackendKind backendKind, out IScatterSimulationBackend backend);
    }
}
