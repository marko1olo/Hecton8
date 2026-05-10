using System;

namespace Hecton8.Core
{
    /// <summary>
    /// Bootstrap-visible readiness state for registry-owned services.
    /// Kept in bootstrap contracts so leaf assemblies can expose health without depending on Core.
    /// </summary>
    public enum ServiceHeartbeatState : byte
    {
        NotStarted = 0,
        Booting = 1,
        Ready = 2,
        Degraded = 3,
        Failed = 4,
        Shutdown = 5
    }

    /// <summary>
    /// Optional deterministic readiness contract polled by bootstrap/runtime watchdog code.
    /// Implementers must return cached state only; no hierarchy search or allocation is permitted.
    /// </summary>
    public interface IServiceHeartbeat
    {
        /// <summary>
        /// Current service readiness state.
        /// </summary>
        ServiceHeartbeatState HeartbeatState { get; }

        /// <summary>
        /// True only when the service is safe for the next bootstrap layer to consume.
        /// </summary>
        bool IsServiceReady { get; }

        /// <summary>
        /// Monotonic liveness counter sampled by the runtime watchdog registry guard.
        /// Implementers with a real tick lane should override this; the default prevents legacy ready
        /// services from false-positive alarms until they publish service-owned counters.
        /// </summary>
        int TickCount => Environment.TickCount;
    }

    /// <summary>
    /// Explicit shutdown contract for registry-owned services with native or pooled memory.
    /// </summary>
    public interface IServiceShutdown
    {
        /// <summary>
        /// Releases service-owned runtime state before registry slots are cleared.
        /// </summary>
        void OnServiceShutdown();
    }

    /// <summary>
    /// Registry slots exposed to leaf assemblies that cannot reference the Core runtime assembly.
    /// </summary>
    public enum BootstrapRegistryBridgeSlot : byte
    {
        NativeInputManagerRuntime = 0,
        UserOptionsRuntime = 1
    }

    /// <summary>
    /// Narrow runtime bridge that lets input leaf assemblies publish services without a circular Core dependency.
    /// </summary>
    public static class BootstrapRegistryBridge
    {
        private static Func<BootstrapRegistryBridgeSlot, object> s_resolve;
        private static Action<BootstrapRegistryBridgeSlot, object> s_register;
        private static Action<BootstrapRegistryBridgeSlot, object> s_unregister;

        public static void Configure(
            Func<BootstrapRegistryBridgeSlot, object> resolve,
            Action<BootstrapRegistryBridgeSlot, object> register,
            Action<BootstrapRegistryBridgeSlot, object> unregister)
        {
            s_resolve = resolve;
            s_register = register;
            s_unregister = unregister;
        }

        public static bool TryResolve<T>(BootstrapRegistryBridgeSlot slot, out T service)
            where T : class
        {
            service = s_resolve != null ? s_resolve(slot) as T : null;
            return service != null;
        }

        public static void Register(BootstrapRegistryBridgeSlot slot, object service)
        {
            s_register?.Invoke(slot, service);
        }

        public static void Unregister(BootstrapRegistryBridgeSlot slot, object service)
        {
            s_unregister?.Invoke(slot, service);
        }
    }

    /// <summary>
    /// Registry-owned input binding service used by UI rebinding panels.
    /// Kept in bootstrap contracts so input runtime can publish the service without depending on Core.
    /// </summary>
    public interface IInputBindingService
    {
        /// <summary>True while an interactive rebind operation is active.</summary>
        bool IsRebinding { get; }

        /// <summary>Raised when a binding rebind starts.</summary>
        event Action<string, string, int> OnRebindStarted;

        /// <summary>Raised when a binding rebind completes.</summary>
        event Action<string, string, int, string> OnRebindCompleted;

        /// <summary>Raised when a binding rebind is canceled.</summary>
        event Action<string, string, int> OnRebindCanceled;

        /// <summary>Raised when a new binding conflicts with an existing binding.</summary>
        event Action<string, string, string, Action, Action> OnConflictDetected;

        /// <summary>Raised after binding overrides are loaded.</summary>
        event Action OnOverridesLoaded;

        /// <summary>Raised after binding overrides are saved.</summary>
        event Action OnOverridesSaved;

        /// <summary>Raised after binding overrides are cleared.</summary>
        event Action OnOverridesCleared;

        /// <summary>
        /// Starts an interactive rebind for a binding index.
        /// </summary>
        bool StartInteractiveRebind(
            string actionName,
            string actionMap = "Player",
            int bindingIndex = 0,
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null);

        /// <summary>
        /// Starts an interactive rebind for a binding identifier.
        /// </summary>
        bool StartInteractiveRebindById(
            string actionName,
            string bindingId,
            string actionMap = "Player",
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null);

        /// <summary>Cancels the active rebind operation, if any.</summary>
        void CancelRebind();

        /// <summary>Saves current binding overrides.</summary>
        void SaveOverrides();

        /// <summary>Loads persisted binding overrides.</summary>
        void LoadOverrides();

        /// <summary>Clears persisted binding overrides.</summary>
        void ClearOverrides(bool clearPlayerPrefs = true);
    }

    /// <summary>
    /// Rebind-specific alias for the registry-owned input binding service.
    /// Existing binding callers use <see cref="IInputBindingService"/>; mod and facade code can depend on this narrower name.
    /// </summary>
    public interface IInputRebindService : IInputBindingService
    {
    }
}
