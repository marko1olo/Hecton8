using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed bootstrap and dispatch layers used by the registry-backed runtime core.
    /// </summary>
    public enum PriorityLayer : byte
    {
        Core = 0x00,
        Environment = 0x20,
        Player = 0x40,
        UI = 0x60,
    }

    /// <summary>
    /// Zero-allocation update contract used by <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Minimal render callback contract for registry-managed render systems.
    /// </summary>
    public interface IRenderable
    {
        /// <summary>
        /// Executes one render-side callback.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the caller.</param>
        void Render(float deltaTime);
    }

    /// <summary>
    /// Canonical abyssal weather state bitmask published through <see cref="GlobalRegistry"/>.
    /// </summary>
    [System.Flags]
    public enum WeatherState : uint
    {
        Calm = 1u << 0,
        Storm = 1u << 1,
        UpdraftActive = 1u << 2,
        ThermoclineActive = 1u << 3,
        HaloclineActive = 1u << 4,
        BiolumeSurge = 1u << 5,
    }

    /// <summary>
    /// Shared current-metadata payload mandated for flow-field-derived systems.
    /// </summary>
    public struct CurrentMeta
    {
        /// <summary>
        /// Base world-space current vector before local modifiers.
        /// </summary>
        public float3 GlobalBaseVector;

        /// <summary>
        /// Scalar applied to the base vector.
        /// </summary>
        public float GlobalScale;

        /// <summary>
        /// Thermocline / halocline response strength.
        /// </summary>
        public float ThermalIntensity;

        /// <summary>
        /// Monotonic weather-side time accumulator for wave phase evolution.
        /// </summary>
        public float TimeAccumulator;
    }

    /// <summary>
    /// Blittable Gerstner-wave component consumed by Burst jobs.
    /// </summary>
    public struct GerstnerWaveComponent
    {
        /// <summary>
        /// Normalized XZ travel direction.
        /// </summary>
        public float2 DirectionXZ;

        /// <summary>
        /// Vertical amplitude in meters.
        /// </summary>
        public float Amplitude;

        /// <summary>
        /// Wavelength in meters.
        /// </summary>
        public float Wavelength;

        /// <summary>
        /// Horizontal-displacement factor.
        /// </summary>
        public float Steepness;

        /// <summary>
        /// Authoring-time phase offset in radians.
        /// </summary>
        public float PhaseOffset;

        /// <summary>
        /// Speed multiplier applied to the analytic phase velocity.
        /// </summary>
        public float SpeedMultiplier;
    }

    /// <summary>
    /// Zero-allocation weather snapshot consumed by physics and VFX systems.
    /// </summary>
    public struct WeatherRuntimeSnapshot
    {
        /// <summary>
        /// Active weather-state flags for this frame.
        /// </summary>
        public WeatherState StateMask;

        /// <summary>
        /// Transition alpha across the active weather-state change.
        /// </summary>
        public float WeatherIntensity;

        /// <summary>
        /// Resolved world-space global current vector.
        /// </summary>
        public float3 GlobalCurrentVector;

        /// <summary>
        /// Resolved world-space global wind vector.
        /// </summary>
        public float3 GlobalWindVector;

        /// <summary>
        /// Shared metadata for current-driven consumers.
        /// </summary>
        public CurrentMeta CurrentMeta;

        /// <summary>
        /// First wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave0;

        /// <summary>
        /// Second wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave1;

        /// <summary>
        /// Third wave component in the weather-driven fallback spectrum.
        /// </summary>
        public GerstnerWaveComponent Wave2;
    }

    /// <summary>
    /// Minimal input service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// True when the authoritative player-input map is enabled and safe for gameplay reads.
        /// </summary>
        bool IsPlayerInputEnabled { get; }

        /// <summary>
        /// Discrete interact input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnInteract;

        /// <summary>
        /// Discrete tool-slot-one input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot1;

        /// <summary>
        /// Discrete tool-slot-two input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot2;

        /// <summary>
        /// Discrete tool-slot-three input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot3;

        /// <summary>
        /// Discrete tool-slot-four input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnToolSlot4;

        /// <summary>
        /// Returns the cached input snapshot captured once at the start of the current frame.
        /// </summary>
        /// <returns>Zero-GC input snapshot for the current frame.</returns>
        PlayerInputState GetState();

        /// <summary>
        /// Adds a buffered action token into the bounded input ring for delayed consumption.
        /// </summary>
        /// <param name="action">Buffered action token to record.</param>
        void BufferAction(PlayerBufferedAction action);

        /// <summary>
        /// Consumes the newest valid buffered action matching the requested token.
        /// </summary>
        /// <param name="action">Buffered action token to resolve.</param>
        /// <param name="maxAgeSeconds">Maximum valid input age in seconds. Values below zero fall back to the service default.</param>
        /// <returns>True when a valid buffered action was consumed.</returns>
        bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds);
    }

    /// <summary>
    /// Authoritative physics routing service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPhysicsService
    {
        /// <summary>
        /// True once the physics routing owner is initialized and ready to accept packets.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Queues a force packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues a torque packet for deferred main-thread application.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true);

        /// <summary>
        /// Clears all queued packets and cached body slots.
        /// </summary>
        void ClearQueuedPackets();
    }

    /// <summary>
    /// Minimal audio service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Authoritative scene transition service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>
        /// True when scene transitions are permitted by bootstrap state.
        /// </summary>
        bool CanLoadScene { get; }

        /// <summary>
        /// Performs a guarded scene transition.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        void LoadScene(string sceneName);
    }

    /// <summary>
    /// Minimal UI service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Authoritative global weather contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Active weather-state flags for the current frame.
        /// </summary>
        WeatherState CurrentWeatherState { get; }

        /// <summary>
        /// Global world-space current vector in meters per second.
        /// </summary>
        Vector3 GlobalCurrentVector { get; }

        /// <summary>
        /// Global world-space wind vector in meters per second.
        /// </summary>
        Vector3 GlobalWindVector { get; }

        /// <summary>
        /// Transition alpha used by consumers for smooth blending.
        /// </summary>
        float WeatherIntensity { get; }

        /// <summary>
        /// Returns the latest zero-allocation runtime snapshot.
        /// </summary>
        WeatherRuntimeSnapshot GetRuntimeSnapshot();
    }

    /// <summary>
    /// Authoritative queued interaction-signal service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInteractionSignalService
    {
        /// <summary>
        /// True once the service is registered and ready to accept interaction packets.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Publishes one queued interaction signal for deferred late-frame dispatch.
        /// </summary>
        /// <param name="signal">Signal payload copied into the queue.</param>
        /// <param name="targetCollider">Resolved collider reference associated with the signal target.</param>
        /// <returns>True when the signal was accepted.</returns>
        bool Publish(in InteractionSignal signal, Collider targetCollider);

        /// <summary>
        /// Performs the shared zero-allocation tool hit query using the service-owned buffers.
        /// </summary>
        /// <param name="origin">Runtime-space ray origin.</param>
        /// <param name="direction">Runtime-space ray direction.</param>
        /// <param name="range">Maximum query range.</param>
        /// <param name="layerMask">Physics layer mask.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool TryRaycastPrimary(Vector3 origin, Vector3 direction, float range, int layerMask, out RaycastHit hit);

        /// <summary>
        /// Clears all queued interaction signals and associated transient target references.
        /// </summary>
        void ClearQueuedSignals();
    }
}
