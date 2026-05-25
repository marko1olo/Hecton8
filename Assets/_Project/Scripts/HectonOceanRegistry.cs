using Hecton8.Core;
using Hecton8.Core.Contracts;

namespace Hecton8.Physics
{
    /// <summary>
    /// Compatibility facade over the bootstrap-owned ocean-kinematics selector service.
    /// </summary>
    public static class HectonOceanRegistry
    {
        /// <summary>
        /// Highest-priority registered provider currently available to gameplay systems.
        /// </summary>
        public static IHectonOceanKinematics ActiveProvider
        {
            get
            {
                IHectonOceanKinematicsService service = GlobalRegistry.OceanKinematics;
                if (service != null)
                    return service.ActiveProvider;

                return OceanKinematicsRuntimeService.EnsureRuntimeInstance().ActiveProvider;
            }
        }

        /// <summary>
        /// Registers an ocean provider and recomputes the active backend.
        /// </summary>
        public static void Register(IHectonOceanKinematics provider)
        {
            OceanKinematicsRuntimeService.RegisterProvider(provider);
        }

        /// <summary>
        /// Unregisters an ocean provider and recomputes the active backend.
        /// </summary>
        public static void Unregister(IHectonOceanKinematics provider)
        {
            OceanKinematicsRuntimeService.UnregisterProvider(provider);
        }
    }
}
