using System;

namespace Hecton8.World
{
    /// <summary>
    /// Static event bus for emergency service relay interactions.
    /// </summary>
    public static class EmergencyServiceRelayEvents
    {
        /// <summary>
        /// Fired when a relay is accessed.
        /// </summary>
        public static event Action<EmergencyServiceRelay, bool> OnRelayActivated;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnRelayActivated = null;
        }

        /// <summary>
        /// Raises the relay activation event.
        /// </summary>
        /// <param name="relay">Relay that was accessed.</param>
        /// <param name="firstActivation">True when this was the first discovery-grade access.</param>
        public static void RaiseRelayActivated(EmergencyServiceRelay relay, bool firstActivation)
        {
            if (relay != null)
                OnRelayActivated?.Invoke(relay, firstActivation);
        }
    }
}
