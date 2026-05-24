using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

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
        int TickCount => global::System.Environment.TickCount;
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
    /// Named shutdown facade used by the BIOS reverse-order disposal orchestrator.
    /// </summary>
    public static class ServiceShutdownExtensions
    {
        /// <summary>
        /// Runs the service-owned disposal path.
        /// </summary>
        /// <param name="service">Registry-owned service to shut down.</param>
        public static void DisposeAll(this IServiceShutdown service)
        {
            service?.OnServiceShutdown();
        }
    }

    /// <summary>
    /// Fixed two-tier scalability profile used by platform/settings integration.
    /// </summary>
    public static class ScalabilityTierProfiles
    {
        public const byte LowMx350 = 0;
        public const byte HighRtx = 1;

        public static byte Normalize(byte tier)
        {
            return tier == LowMx350 ? LowMx350 : HighRtx;
        }
    }

    /// <summary>
    /// Platform integration seam owned by contracts. Concrete services may live in leaf assemblies.
    /// </summary>
    public interface IPlatformIntegration
    {
        /// <summary>Persisted profile byte: 0 = Low/MX350, 1 = High/RTX.</summary>
        byte ScalabilityTier { get; }

        /// <summary>Persists and broadcasts a runtime scalability profile change.</summary>
        /// <param name="tier">Profile byte: 0 = Low/MX350, 1 = High/RTX. Other values clamp to High/RTX.</param>
        void SetScalabilityTier(byte tier);
    }

    /// <summary>
    /// Narrow bridge for leaf assemblies that cannot reference Core but must apply platform settings.
    /// </summary>
    public static class PlatformIntegrationBridge
    {
        private static Func<byte> s_resolveCurrentScalabilityTier;
        private static Action<byte> s_applyScalabilityTier;
        private static Action<byte, byte> s_publishScalabilityChanged;

        public static void Configure(
            Func<byte> resolveCurrentScalabilityTier,
            Action<byte> applyScalabilityTier,
            Action<byte, byte> publishScalabilityChanged)
        {
            s_resolveCurrentScalabilityTier = resolveCurrentScalabilityTier;
            s_applyScalabilityTier = applyScalabilityTier;
            s_publishScalabilityChanged = publishScalabilityChanged;
        }

        public static byte ResolveCurrentScalabilityTier(byte fallbackTier)
        {
            return s_resolveCurrentScalabilityTier != null
                ? ScalabilityTierProfiles.Normalize(s_resolveCurrentScalabilityTier())
                : ScalabilityTierProfiles.Normalize(fallbackTier);
        }

        public static void ApplyScalabilityTier(byte tier)
        {
            s_applyScalabilityTier?.Invoke(ScalabilityTierProfiles.Normalize(tier));
        }

        public static void PublishScalabilityChanged(byte previousTier, byte currentTier)
        {
            s_publishScalabilityChanged?.Invoke(
                ScalabilityTierProfiles.Normalize(previousTier),
                ScalabilityTierProfiles.Normalize(currentTier));
        }
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
    /// Bootstrap-owned native input runtime surface used by Core services without binding to the concrete input owner.
    /// Implementers must return cached action references and cached readiness state only.
    /// </summary>
    public interface INativeInputManagerRuntime : IServiceHeartbeat, IServiceShutdown
    {
        event Action<Vector2> OnNavigate;

        event Action OnSubmit;

        event Action OnCancel;

        event Action OnTabNext;

        event Action OnTabPrevious;

        event Action OnPause;

        event Action<byte> OnInputDisplayStyleCodeChanged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        event Action OnDebugToggleBlackBoxDashboard;

        event Action OnDebugToggleEngineHealthOverlay;
#endif

        bool IsPlayerInputEnabled { get; }

        bool IsUIInputEnabled { get; }

        bool CanSwitchActionMaps { get; }

        bool IsSprinting { get; }

        Vector2 MoveInput { get; }

        Vector2 LookInput { get; }

        byte CurrentDisplayStyleCode { get; }

        void EnablePlayerInput();

        void DisablePlayerInput();

        void EnableUIInput();

        void DisableUIInput();

        void SwitchToPlayerInput();

        void SwitchToUIInput();

        InputAction GetAction(string actionName, string actionMap = "Player");

        InputActionMap GetActionMap(string actionMap = "Player");

        int GetPreferredBindingIndex(string actionName, string actionMap = "Player");

        bool TryReadUiPoint(out Vector2 point);

        bool TryReadUiScrollWheel(out Vector2 scrollDelta);

        string GetBindingDisplayString(string actionName, string actionMap = "Player", int bindingIndex = 0);

        bool TryGetBindingDisplayString(InputAction action, int bindingIndex, out string display);

        bool TryWriteBindingDisplayString(
            string actionName,
            string actionMap,
            int bindingIndex,
            char[] buffer,
            int bufferOffset,
            out int charsWritten);

        bool TryWriteBindingDisplayString(
            InputAction action,
            int bindingIndex,
            char[] buffer,
            int bufferOffset,
            out int charsWritten);

        bool TryGetBindingMarkupForToken(string token, out string markup);

        bool TryConfigureUiInputModule(InputSystemUIInputModule inputModule);

        string SaveBindingOverridesAsJson();

        void LoadBindingOverridesFromJson(string json);

        void ClearBindingOverrides();
    }

    public static class NativeInputDisplayStyle
    {
        public const byte KeyboardMouse = 0;
        public const byte Gamepad = 1;
        public const byte SteamDeck = 2;
        public const byte XRTouch = 3;
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
