using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Interaction;
using Hecton8.SaveSystem;
using Hecton8.Construction;
using Hecton8.Building;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Audio.Virtualization;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Meta;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.Core
{
    /// <summary>
    /// Marker for registry-published service contracts.
    /// Concrete runtime owners must publish through an interface that carries this marker.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Monotonic service liveness counter sampled by bootstrap and RuntimeWatchdog.
        /// Services with real update lanes should override this with their own counter.
        /// </summary>
        int TickCount => global::System.Environment.TickCount;
    }

    /// <summary>
    /// Allocation-free localization contract exposed through GlobalRegistry for Babel UI consumers.
    /// </summary>
    public interface IBabelLocalization : ISystem
    {
        /// <summary>Active language as a compact stable id.</summary>
        ushort ActiveLanguageId { get; }

        /// <summary>Resolve UTF-8 bytes for a localization key hash without creating a managed string.</summary>
        bool TryGetLocalizedSpan(uint hash, out ReadOnlySpan<byte> utf8Bytes);

        /// <summary>Resolve a staged char buffer for TMP SetCharArray without creating a managed string.</summary>
        bool TryGetLocalizedBuffer(uint hash, out char[] buffer, out int length);

        /// <summary>Inject one integer payload into a localized template using caller-owned storage.</summary>
        bool TryWriteLocalizedInt(uint templateHash, int value, Span<char> destination, out int length);

        /// <summary>Resolve singular/plural key choice through deterministic integer math.</summary>
        uint ResolvePluralHash(uint singularHash, uint pluralHash, int value);
    }

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
    /// End-of-frame callback executed by <see cref="SystemDispatcher"/> after the main update lanes.
    /// Intended for deferred job ownership recovery and readback commits that must stay out of hot-path ticks.
    /// </summary>
    public interface ILateFrameTickable
    {
        /// <summary>
        /// Executes the owner's end-of-frame swap-window work.
        /// </summary>
        void LateFrameTick();
    }

    /// <summary>
    /// Registry-owned bridge between first-party native signal lanes and managed mod callbacks.
    /// First-party producers must stay on <see cref="GlobalSignals"/> / <see cref="SignalBus{T}"/> lanes.
    /// </summary>
    public interface IModdingBridge : ISystem
    {
        /// <summary>True after the bridge allocated its native projection queues and registered dispatcher ownership.</summary>
        bool IsInitialized { get; }

        /// <summary>Installs the bridge service and its native queue bindings.</summary>
        void Install();

        /// <summary>Stops dispatch and releases native projection state.</summary>
        void Shutdown();

        /// <summary>Schedules post-simulation projection from typed signal snapshots into mod-facing DTOs.</summary>
        void ProjectPostSimulation();

        /// <summary>Runs managed mod delegate dispatch from the late-frame swap window.</summary>
        void DispatchLateFrame();
    }

    /// <summary>
    /// Camera presentation feedback service contract exposed through <see cref="GlobalRegistry"/>.
    /// Shake impulses must enter through signal/listener lanes; this interface is for non-shake control and diagnostics.
    /// </summary>
    public interface ICameraJuiceSystem : ISystem
    {
        /// <summary>
        /// Applies the dispatcher-owned pause/PDA depth-of-field isolation weight.
        /// </summary>
        /// <param name="weight">Normalized focus isolation weight.</param>
        void ApplyPauseDepthOfFieldWeight(float weight);

        /// <summary>
        /// Reclaims gameplay camera FOV from a cinematic transition without a snap.
        /// </summary>
        /// <param name="startFov">Starting field of view in degrees.</param>
        /// <param name="durationSeconds">Blend duration in seconds.</param>
        void BeginInputReclaimFov(float startFov, float durationSeconds);

        /// <summary>Resolved adaptive shake scale for diagnostics.</summary>
        float DebugAdaptiveShakeScale { get; }

        /// <summary>Resolved adaptive FOV scale for diagnostics.</summary>
        float DebugAdaptiveFOVScale { get; }

        /// <summary>Resolved adaptive post-effect scale for diagnostics.</summary>
        float DebugAdaptivePostFxScale { get; }

        /// <summary>Resolved active shake cap for diagnostics.</summary>
        int DebugAdaptiveMaxActiveShakes { get; }

        /// <summary>True when adaptive pressure disables interaction depth-of-field.</summary>
        bool DebugAdaptiveDisableInteractionDoF { get; }
    }

    /// <summary>
    /// Scene domain gate used by isolated runtime systems to avoid cross-domain execution.
    /// </summary>
    public enum Domain : byte
    {
        Unknown = 0,
        Space = 1,
        Ocean = 2,
        Submarine = 3,
        Habitat = 4,
        Surface = 5,
        Menu = 6
    }

    /// <summary>
    /// Read-only orbital prologue snapshot for consumers that need telemetry without owning the simulation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct OrbitalDirectorSnapshot
    {
        public OrbitalDirectorSnapshot(
            double3 universeVelocity,
            double planetDistanceMeters,
            float reentryHeat01,
            float cloudWhiteout01,
            uint sequence,
            byte mathLod,
            byte flags)
        {
            UniverseVelocity = universeVelocity;
            PlanetDistanceMeters = planetDistanceMeters;
            ReentryHeat01 = reentryHeat01;
            CloudWhiteout01 = cloudWhiteout01;
            Sequence = sequence;
            MathLod = mathLod;
            Flags = flags;
        }

        public double3 UniverseVelocity { get; }
        public double PlanetDistanceMeters { get; }
        public float ReentryHeat01 { get; }
        public float CloudWhiteout01 { get; }
        public uint Sequence { get; }
        public byte MathLod { get; }
        public byte Flags { get; }
    }

    /// <summary>
    /// Registry-published authority for the space prologue relativity fake.
    /// </summary>
    public interface IOrbitalDirector : ISystem
    {
        /// <summary>Universe velocity in authoritative double precision space.</summary>
        double3 UniverseVelocity { get; }

        /// <summary>Current fake planet distance from the capsule origin.</summary>
        double PlanetDistanceMeters { get; }

        /// <summary>True after the re-entry plasma phase has started.</summary>
        bool ReentryArmed { get; }

        /// <summary>Copies the latest orbital director snapshot.</summary>
        bool TryGetSnapshot(out OrbitalDirectorSnapshot snapshot);

        /// <summary>Enables or disables player thrust consumption without disabling telemetry.</summary>
        void SetInputEnabled(bool enabled);

        /// <summary>Forces the prologue universe velocity to rest for splashdown handoff without aborting telemetry.</summary>
        void ForceZeroUniverseVelocity(byte reason);

        /// <summary>Fail-fast hook used by bootstrap/integrator code to abort the prologue lane.</summary>
        void ForceAbortReentry(byte reason);
    }

    /// <summary>
    /// Registry-published world streaming IO backpressure read model.
    /// Movement, PDA, and VFX consumers read the dispatcher scalar or this cached service; they do not touch Addressables owners directly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StreamingHlodImpostorPoint
    {
        public float3 Center;
        public float3 Size;
        public long ChunkId;
        public int ImpostorType;
        public float SpawnTimeSeconds;
        public float Fade01;
        public uint Flags;
    }

    /// <summary>
    /// Renderer boundary for streaming-owned HLOD matrix residency.
    /// Implementations may draw through compute-culling, BRG, or a fallback indirect path.
    /// </summary>
    public interface IStreamingHlodMatrixRenderer
    {
        int BoundInstanceCount { get; }
        bool IsUsingVisibleMatrixStream { get; }
        void BindNativeMatrices(NativeArray<float4x4> matrices, int instanceCount, float boundsRadius, bool forceUpload);
        void ClearBinding();
    }

    /// <summary>
    /// Registry-published world streaming IO backpressure and distant HLOD read model.
    /// Consumers read native snapshots only; the streaming owner keeps mutation authority.
    /// </summary>
    public interface IStreamingBackpressureService : ISystem
    {
        float StorageDebt01 { get; }
        float SmoothedStorageDebt01 { get; }
        double LatencyEwmaMs { get; }
        double OldestPendingMs { get; }
        double CriticalHoleDebtMs { get; }
        uint BackpressureSequence { get; }
        bool DataLinkDegraded { get; }
        int ActiveImpostorCount { get; }
        uint ActiveImpostorVersion { get; }
        bool IsChunkResident(long chunkId);
        bool TryGetActiveImpostors(out NativeArray<float4x4> matrices, out NativeArray<int> impostorTypes, out int count);
        bool TryGetActiveImpostorPoints(out NativeArray<StreamingHlodImpostorPoint> points, out int count);
        bool IsChunkImpostorAudioMuted(long chunkId);
        void PurgeImpostorForDestroyedChunk(long chunkId);
    }

    /// <summary>
    /// Post-fixed callback executed by <see cref="SystemDispatcher"/> after the fixed lanes complete.
    /// Intended for deferred job ownership recovery that must stay out of hot-path fixed ticks.
    /// </summary>
    public interface IPostFixedTickable
    {
        /// <summary>
        /// Executes the owner's post-fixed-step swap-window work.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed delta supplied by the dispatcher.</param>
        void PostFixedTick(float fixedDeltaTime);
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
    /// Canonical damage-channel discriminator used by the global packet-based damage receiver contract.
    /// </summary>
    public enum DamageChannel : byte
    {
        Integrity = 0,
        Power = 1,
        Clarity = 2,
        Trauma = 3,
        HullBreach = 4
    }

    /// <summary>
    /// Canonical damage packet routed through the global packet-based damage receiver contract.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DamagePacket
    {
        /// <summary>
        /// Packet channel resolved by the emitting owner.
        /// </summary>
        public DamageChannel Channel;

        /// <summary>
        /// Previous normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        public float PreviousValue;

        /// <summary>
        /// Next normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        public float NextValue;

        /// <summary>
        /// Primary physical magnitude associated with the event.
        /// Integrity and clarity send normalized magnitudes, hull breaches send pressure delta.
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// Local-space point relative to the emitting owner.
        /// </summary>
        public float3 LocalPoint;

        /// <summary>
        /// Damage-type bitmask authored by the emitter.
        /// </summary>
        public uint DamageType;

        /// <summary>
        /// Quantized integrity delta used by structural diffusion consumers.
        /// </summary>
        public byte IntegrityDelta;

        /// <summary>
        /// Depth in meters associated with the damage event when relevant.
        /// </summary>
        public float Depth;

        /// <summary>
        /// Stable emitter-local source identifier.
        /// </summary>
        public ushort SourceId;

        /// <summary>
        /// Encoded trauma threshold when <see cref="Channel"/> is <see cref="DamageChannel.Trauma"/>.
        /// </summary>
        public byte TraumaLevel;
    }

    /// <summary>
    /// Canonical packet-based damage receiver contract.
    /// All first-party damage ingress must terminate here before subsystem-specific fanout.
    /// </summary>
    public interface IDamageReceiver
    {
        /// <summary>
        /// Receives one authoritative damage packet.
        /// </summary>
        /// <param name="packet">Immutable damage payload copied by the caller.</param>
        void ReceiveDamage(in DamagePacket packet);
    }

    /// <summary>
    /// Immutable authored debris definition consumed by the runtime debris manager.
    /// </summary>
    public interface IDebrisDefinition
    {
        /// <summary>
        /// True when the definition has at least one authored chunk and material binding.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Total authored Voronoi chunk count.
        /// </summary>
        int ChunkCount { get; }

        /// <summary>
        /// Shared material used for all runtime chunk draws.
        /// </summary>
        Material SharedMaterial { get; }

        /// <summary>
        /// Shadow-casting mode used by the debris burst.
        /// </summary>
        ShadowCastingMode ShadowCastingMode { get; }

        /// <summary>
        /// True when the runtime chunks should receive shadows.
        /// </summary>
        bool ReceiveShadows { get; }

        /// <summary>
        /// Rendering layer mask applied to the runtime chunk draw calls.
        /// </summary>
        uint RenderingLayerMask { get; }

        /// <summary>
        /// Base outward impulse multiplier applied on burst spawn.
        /// </summary>
        float BaseImpulse { get; }

        /// <summary>
        /// Linear damping applied while collision remains enabled.
        /// </summary>
        float LinearDamping { get; }

        /// <summary>
        /// Angular damping applied per simulated chunk.
        /// </summary>
        float AngularDamping { get; }

        /// <summary>
        /// Extra kinematic sink distance applied after the collision phase ends.
        /// </summary>
        float SinkDistance { get; }

        /// <summary>
        /// Duration of the sink phase after collision is disabled.
        /// </summary>
        float SinkDuration { get; }

        /// <summary>
        /// Offset below the spawn origin used as the simple ground collision plane.
        /// </summary>
        float GroundPlaneOffset { get; }

        /// <summary>
        /// Bounce attenuation used by the simple collision response.
        /// </summary>
        float BounceDamping { get; }

        /// <summary>
        /// Returns the authored mesh for one chunk index.
        /// </summary>
        Mesh GetChunkMesh(int index);

        /// <summary>
        /// Returns the authored local transform matrix for one chunk index.
        /// </summary>
        Matrix4x4 GetLocalMatrix(int index);

        /// <summary>
        /// Returns the authored mass scale for one chunk index.
        /// </summary>
        float GetMassScale(int index);
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
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
    /// Shared metadata for the global data-vault Gerstner spectrum buffer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OceanGerstnerWaveBufferMeta
    {
        public int ActiveWaveCount;
        public float TimeSeconds;
        public int SleepCount;
        public int Version;
    }

    /// <summary>
    /// Zero-allocation weather snapshot consumed by physics and VFX systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WeatherRuntimeSnapshot
    {
        /// <summary>
        /// Active weather-state flags for this frame.
        /// </summary>
        public WeatherState StateMask;

        /// <summary>
        /// Normalized storm/current intensity after active weather-state blending.
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
    /// Canonical celestial-state flags published by the deterministic world-pulse owner.
    /// </summary>
    [System.Flags]
    public enum CelestialRuntimeFlags : uint
    {
        None = 0u,
        Valid = 1u << 0,
        EclipseActive = 1u << 1,
        HighTide = 1u << 2,
        FullMoonBloom = 1u << 3,
        SolarRadiationStorm = 1u << 4,
    }

    /// <summary>
    /// Blittable celestial runtime payload consumed by rendering, fluid, audio, and gameplay systems.
    /// Double universe time is retained for deterministic sync; spatial presentation data is reduced to float vectors.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CelestialRuntimeSnapshot
    {
        /// <summary>Authoritative Absolute Universe Time used for the analytical orbit solve.</summary>
        public double AbsoluteUniverseTime;

        /// <summary>Observer-to-sun direction in runtime space.</summary>
        public float3 SunDirection;

        /// <summary>AUP-safe presentation offset for the gas giant relative to the observer.</summary>
        public float3 GasGiantOffset;

        /// <summary>AUP-safe presentation offset for the first moon relative to the observer.</summary>
        public float3 Moon0Offset;

        /// <summary>AUP-safe presentation offset for the second moon relative to the observer.</summary>
        public float3 Moon1Offset;

        /// <summary>Normalized observer-to-gas-giant direction.</summary>
        public float3 GasGiantDirection;

        /// <summary>Normalized observer-to-first-moon direction.</summary>
        public float3 Moon0Direction;

        /// <summary>Normalized observer-to-second-moon direction.</summary>
        public float3 Moon1Direction;

        /// <summary>Normalized dominant tide pull direction.</summary>
        public float3 TidePullVector;

        /// <summary>Signed sea-level offset in meters resolved from the current celestial pull.</summary>
        public float TideHeightMeters;

        /// <summary>Normalized high-tide state. 0 is lowest tide, 1 is highest tide.</summary>
        public float TideHigh01;

        /// <summary>First moon visual fullness, used by lunar phase materials.</summary>
        public float Moon0Phase01;

        /// <summary>Second moon visual fullness, used by lunar phase materials.</summary>
        public float Moon1Phase01;

        /// <summary>Gas giant visual fullness.</summary>
        public float GasGiantPhase01;

        /// <summary>Current eclipse occlusion factor.</summary>
        public float EclipseOcclusion01;

        /// <summary>Current radiation-storm intensity sourced from the global event lane.</summary>
        public float RadiationStorm01;

        /// <summary>Global bioluminescence multiplier resolved from full-moon and resonance states.</summary>
        public float GlobalBiolumMultiplier;

        /// <summary>Bitmask of <see cref="CelestialRuntimeFlags"/>.</summary>
        public uint Flags;

        /// <summary>Monotonic sequence used by frame caches to detect celestial tide updates.</summary>
        public uint Sequence;
    }

    /// <summary>
    /// Blittable GI relay state published for watchdogs, diagnostics, and low-cost consumers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GIRelayRuntimeSnapshot
    {
        public double AbsoluteUniverseTime;
        public float TimeOfDay01;
        public float DepthMeters;
        public float Depth01;
        public float EclipseScalar;
        public float MoonPhase01;
        public float FogLod;
        public float LightningScalar;
        public int ShadowCascadeLevel;
        public uint Flags;
        public uint Sequence;
    }

    /// <summary>
    /// Registry-facing lighting relay contract. Runtime lighting owners must register here instead of singleton access.
    /// </summary>
    public interface IGIRelaySystem : ISystem
    {
        bool IsAmbientProbeAuthorityActive { get; }

        GIRelayRuntimeSnapshot Snapshot { get; }

        int ShadowCascadeLevel { get; }

        float LastAppliedDepthMeters { get; }

        uint LastAppliedSequence { get; }

        bool ValidateSphericalHarmonicsLayout(out int expectedBytes, out int actualBytes);
    }

    /// <summary>
    /// Flags published by the deterministic seismic/tide runtime.
    /// </summary>
    [System.Flags]
    public enum SeismicRuntimeFlags : uint
    {
        None = 0u,
        Valid = 1u << 0,
        LowTierShaderShakeDisabled = 1u << 1,
        AbyssDepthAttenuation = 1u << 2,
        CollapseDebrisQueued = 1u << 3,
        HighTremor = 1u << 4,
    }

    /// <summary>
    /// Blittable seismic and harmonic-tide payload for systems that need latest deterministic macro-world state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SeismicRuntimeSnapshot
    {
        public double AbsoluteUniverseTime;
        public float3 SeismicDirection;
        public float SeismicIntensity01;
        public float TideHeightMeters;
        public float TideHigh01;
        public float CameraJitter01;
        public float AudioRumble01;
        public float ThermalEruptionProbabilityScalar;
        public uint Flags;
        public uint Sequence;
    }

    /// <summary>
    /// Authoritative deterministic seismic director. Consumers must read snapshots or signals, not concrete managers.
    /// </summary>
    public interface ISeismicDirector : ISystem
    {
        bool IsInitialized { get; }
        float SeismicIntensity01 { get; }
        float3 SeismicDirection { get; }
        float TideHeightMeters { get; }
        float TideHigh01 { get; }
        SeismicRuntimeSnapshot GetRuntimeSnapshot();
    }

    /// <summary>
    /// Deterministic frame-input service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInputDeterminismService : ISystem
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
        /// Offline/network latency simulation delay, clamped to the deterministic 0-2 frame contract.
        /// </summary>
        int InputDelayFrames { get; set; }

        /// <summary>
        /// Latest fixed-cadence deterministic input sample after configured delay.
        /// </summary>
        InputState CurrentInputState { get; }

        /// <summary>
        /// Previous fixed-cadence deterministic input sample used by presentation interpolation.
        /// </summary>
        InputState PreviousInputState { get; }

        /// <summary>
        /// Presentation-only look delta interpolated between deterministic samples.
        /// </summary>
        Vector2 VisualLookDelta { get; }

        /// <summary>
        /// Captures raw hardware input into the deterministic 60 Hz ring before simulation signal flush.
        /// </summary>
        void PreSimulationInputTick(float deltaTime);

        /// <summary>
        /// Reads a retained deterministic input frame from the 60-slot ring.
        /// </summary>
        bool TryGetInputState(uint frame, out InputState state);

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
        /// Discrete primary-action input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnPrimaryAction;

        /// <summary>
        /// Discrete secondary-action input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnSecondaryAction;

        /// <summary>
        /// Discrete PDA toggle input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnPDA;

        /// <summary>
        /// Discrete inventory input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnInventory;

        /// <summary>
        /// Discrete cancel/back input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnCancel;

        /// <summary>
        /// Discrete next-tab input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnTabNext;

        /// <summary>
        /// Discrete previous-tab input event forwarded from the native input backend.
        /// </summary>
        event System.Action OnTabPrevious;

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

        /// <summary>
        /// Switches native input routing to gameplay.
        /// </summary>
        void SwitchToPlayerInput();

        /// <summary>
        /// Switches native input routing to UI.
        /// </summary>
        void SwitchToUIInput();
    }

    /// <summary>
    /// Minimal input service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IInputService : IInputDeterminismService
    {
    }

    /// <summary>
    /// Registry-owned global meta profile service.
    /// </summary>
    public interface IProfileService
    {
        event System.Action ProfileChanged;
        int ExplorerPoints { get; }
        float MaxDepthMeters { get; }
        float LongestLifeWithoutDeathSeconds { get; }
        int HighestBiomeDiscoveriesInSingleRun { get; }
        bool HasUnlockedAchievement(string achievementId);
        int GetUpgradeLevel(string upgradeId);
        bool TryPurchaseUpgrade(string upgradeId, out string error);
        GlobalProfileData GetSnapshot();
    }

    /// <summary>
    /// Authoritative PDA logbook append service exposed through <see cref="GlobalRegistry"/>.
    /// Stored entries are event/localization hashes, not persistent strings.
    /// </summary>
    public interface IPDALogbookService
    {
        /// <summary>
        /// Total number of retained PDA logbook events.
        /// </summary>
        int EntryCount { get; }

        /// <summary>
        /// Appends one deduplicated PDA log event by precomputed localization/source hashes.
        /// </summary>
        bool TryAppendEntry(int originHash, int titleHash, int messageHash);
    }

    /// <summary>
    /// Authoritative physics routing service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPhysicsService : ISystem
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
        /// Queues a force packet for deferred main-thread application at a specific world-space position.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="worldPosition">World-space application point.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true);

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
    /// Blittable gameplay audio request consumed by the central audio service queue.
    /// EventID maps to an authored clip-table slot owned by the audio runtime.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public readonly struct AudioEvent
    {
        public readonly uint EventID;
        public readonly Vector3 Position;
        public readonly float Volume;
        public readonly float Pitch;
        private readonly uint _reserved0;
        private readonly uint _reserved1;

        public AudioEvent(uint eventID, Vector3 position, float volume, float pitch)
        {
            EventID = eventID;
            Position = position;
            Volume = volume;
            Pitch = pitch;
            _reserved0 = 0u;
            _reserved1 = 0u;
        }
    }

    /// <summary>
    /// Blittable prologue audio transition state routed from visual-sync orchestration into procedural DSP.
    /// Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct AudioTransitionState
    {
        public const byte StageSpace = 1;
        public const byte StagePlasma = 2;
        public const byte StageWhiteout = 3;
        public const byte StageOceanHandoff = 4;

        public const byte FlagSplashdown = 1 << 0;
        public const byte FlagPortalActive = 1 << 1;
        public const byte FlagGranularEnabled = 1 << 2;
        public const byte FlagLowTierProxy = 1 << 3;
        public const byte FlagNonFiniteGuard = 1 << 4;

        [FieldOffset(0)] public float UniverseVelocityMetersPerSecond;
        [FieldOffset(4)] public float Heat01;
        [FieldOffset(8)] public float LowPassCutoffHz;
        [FieldOffset(12)] public float LfeGain01;
        [FieldOffset(16)] public float GranularStress01;
        [FieldOffset(20)] public float SplashdownGain01;
        [FieldOffset(24)] public float PortalBlend01;
        [FieldOffset(28)] public float Reserved0;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint SourceHash;
        [FieldOffset(44)] public byte Stage;
        [FieldOffset(45)] public byte Flags;
        [FieldOffset(46)] public byte QualityTier;
        [FieldOffset(47)] public byte Reserved1;
        [FieldOffset(48)] public double AbsoluteTimeSeconds;
        [FieldOffset(56)] public uint Reserved2;
        [FieldOffset(60)] public uint Reserved3;
    }

    /// <summary>
    /// Minimal audio service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IAudioService : ISystem
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Mixer group used for helmet/UI playback.
        /// </summary>
        AudioMixerGroup InterfaceGroup { get; }

        /// <summary>
        /// Mixer group used for ambient bed playback.
        /// </summary>
        AudioMixerGroup AmbientGroup { get; }

        /// <summary>
        /// Plays one world-space clip through the authored 3D pool.
        /// </summary>
        void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f);

        /// <summary>
        /// Plays one world-space clip through the authored 3D pool and explicit mixer route.
        /// </summary>
        void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup);

        /// <summary>
        /// Queues one AUP-authored world-space acoustic emission without converting through float world space first.
        /// </summary>
        bool QueueSoundEmissionSignal(in SoundEmissionSignal signal);

        /// <summary>
        /// Queues one pressure-derived hull stress signal for structural granular synthesis.
        /// </summary>
        bool QueueHullStressSignal(in HullStressSignal signal);

        /// <summary>
        /// Queues one kinematic CCD impact for procedural collision audio.
        /// </summary>
        bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal);

        /// <summary>
        /// Queues one world-space audio event for the central NativeQueue-backed audio drain.
        /// </summary>
        /// <param name="audioEvent">Blittable event payload. EventID is one-based into the authored audio event table.</param>
        /// <returns>True when the event was accepted by the queue.</returns>
        bool QueueAudioEvent(in AudioEvent audioEvent);

        /// <summary>
        /// Queues one prologue vacuum-to-ocean DSP transition state for procedural audio rendering.
        /// </summary>
        /// <param name="state">Blittable state sampled in the visual-sync lane.</param>
        /// <returns>True when the transition state was accepted by the renderer bridge.</returns>
        bool QueuePrologueAudioTransition(in AudioTransitionState state);

        /// <summary>
        /// Plays one helmet/UI clip through the authored 2D pool.
        /// </summary>
        void PlayStatic2D(AudioClip clip, float volume = 1f);

        /// <summary>
        /// Plays one helmet/UI clip through the authored 2D pool and explicit mixer route.
        /// </summary>
        void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup);

        /// <summary>
        /// Returns the 360-degree acoustic radar ring payload when available.
        /// </summary>
        bool TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution);

        /// <summary>
        /// Returns the 8x4 acoustic radar grid payload when available.
        /// </summary>
        bool TryGetAcousticRadarGridPayload(
            out NativeArray<float> energyGrid,
            out int azimuthBins,
            out int elevationBins,
            out ComputeBuffer gridBuffer);

        /// <summary>
        /// Emits one sandboxed mod acoustic ping through the engine-owned sensory path.
        /// </summary>
        /// <param name="runtimePosition">Frame-space ping origin.</param>
        /// <param name="intensity01">Normalized signal intensity.</param>
        /// <returns>True when the ping was accepted into the sensory path.</returns>
        bool TryEmitModAcousticPing(Vector3 runtimePosition, float intensity01);

        /// <summary>
        /// Stops every active world and UI voice immediately.
        /// </summary>
        void StopAll();
    }

    /// <summary>
    /// Canonical byte identifiers for the vocal warning priority queue.
    /// Lower numeric value is higher priority.
    /// </summary>
    public enum VocalWarningId : byte
    {
        None = 0,
        CrushDepth = 1,
        HullBreach = 2,
        OxygenLow = 3,
        Radiation = 4,
        PowerLow = 5
    }

    /// <summary>
    /// Fixed warning-hash table used by signal producers that cannot reference clips.
    /// </summary>
    public static class VocalWarningHashes
    {
        public const uint CrushDepth = 0x43525348u; // CRSH
        public const uint HullBreach = 0x48554C4Cu; // HULL
        public const uint HullTempCritical = 0x4854454Du; // HTEM
        public const uint OxygenLow = 0x4F584C4Fu; // OXLO
        public const uint Radiation = 0x52414449u; // RADI
        public const uint PowerLow = 0x5057524Cu; // PWRL

        public static byte ToWarningId(uint warningHash)
        {
            switch (warningHash)
            {
                case CrushDepth: return (byte)VocalWarningId.CrushDepth;
                case HullBreach: return (byte)VocalWarningId.HullBreach;
                case HullTempCritical: return (byte)VocalWarningId.HullBreach;
                case OxygenLow: return (byte)VocalWarningId.OxygenLow;
                case Radiation: return (byte)VocalWarningId.Radiation;
                case PowerLow: return (byte)VocalWarningId.PowerLow;
                default: return (byte)VocalWarningId.None;
            }
        }

        public static uint FromWarningId(byte warningId)
        {
            switch ((VocalWarningId)warningId)
            {
                case VocalWarningId.CrushDepth: return CrushDepth;
                case VocalWarningId.HullBreach: return HullBreach;
                case VocalWarningId.OxygenLow: return OxygenLow;
                case VocalWarningId.Radiation: return Radiation;
                case VocalWarningId.PowerLow: return PowerLow;
                default: return 0u;
            }
        }
    }

    /// <summary>
    /// Bit flags attached to VWS signal packets.
    /// </summary>
    public static class VocalWarningSignalFlags
    {
        public const byte HabitatIntegrityCompromised = 1 << 0;
    }

    /// <summary>
    /// Registry-published vocal warning service. Producers should prefer signal lanes;
    /// this contract exists for bootstrap visibility and non-hot diagnostics.
    /// </summary>
    public interface IVocalWarningSystem : ISystem
    {
        /// <summary>True when native warning queue, cooldown, and telemetry storage are allocated.</summary>
        bool IsInitialized { get; }

        /// <summary>Number of queued and staged warning IDs waiting for playback.</summary>
        int PendingCount { get; }

        /// <summary>Currently playing warning byte ID, or 0 when idle.</summary>
        byte CurrentWarningId { get; }

        /// <summary>True while the procedural renderer is playing or staging a vocal warning.</summary>
        bool IsWarningActive { get; }

        /// <summary>
        /// Attempts to enqueue one warning ID into the fixed-priority VWS path.
        /// </summary>
        /// <param name="warningId">Byte ID from <see cref="VocalWarningId"/>.</param>
        /// <param name="severity01">Normalized warning severity.</param>
        /// <param name="cooldownSeconds">Per-warning cooldown override; non-positive uses the runtime fallback.</param>
        /// <param name="flags">Bitmask from <see cref="VocalWarningSignalFlags"/>.</param>
        /// <param name="sourceId">Optional source entity or event hash.</param>
        /// <returns>True when the warning was accepted by cooldown and queue admission.</returns>
        bool TryQueueWarning(byte warningId, float severity01, float cooldownSeconds, byte flags, uint sourceId);

        /// <summary>Clears queued IDs and requests cancellation of the active renderer warning.</summary>
        void CancelCurrentWarning();
    }

    /// <summary>
    /// Authoritative scene transition service contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISceneService : ISystem
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

        /// <summary>
        /// Performs a guarded asynchronous scene transition with activation gating.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        Awaitable LoadSceneAsync(string sceneName);
    }

    /// <summary>
    /// Authoritative save-system contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ISaveService : ISystem
    {
        /// <summary>
        /// True once the save owner has completed runtime initialization and registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// True while a save or load transaction is in flight.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Current accumulated play time in seconds.
        /// </summary>
        float CurrentPlayTimeSeconds { get; }

        /// <summary>
        /// Registers one saveable owner into the ordered persistence registry.
        /// </summary>
        /// <param name="saveable">Persistence owner to register.</param>
        void Register(ISaveable saveable);

        /// <summary>
        /// Removes one saveable owner from the ordered persistence registry.
        /// </summary>
        /// <param name="saveable">Persistence owner to unregister.</param>
        void Unregister(ISaveable saveable);

        /// <summary>
        /// Starts an asynchronous save transaction for the requested slot.
        /// </summary>
        /// <param name="slotName">Persistent slot identifier.</param>
        Awaitable SaveGameAsync(string slotName);

        /// <summary>
        /// Starts an asynchronous load transaction for the requested slot.
        /// </summary>
        /// <param name="slotName">Persistent slot identifier.</param>
        Awaitable LoadGameAsync(string slotName);
    }

    /// <summary>
    /// Async persistence command surface. Save requests enter through typed signals or this interface,
    /// while snapshot, compression, and disk promotion remain owned by the registered persistence service.
    /// </summary>
    public interface IAsyncPersistenceService : ISaveService
    {
        /// <summary>
        /// Queues a save request through the typed persistence signal lane.
        /// </summary>
        /// <param name="slotIndex">Manual slot index: 0, 1, or 2.</param>
        /// <param name="sourceHash">Stable caller/system hash for telemetry.</param>
        /// <param name="operationId">Optional caller-owned operation id; zero lets the service assign one.</param>
        /// <returns>False when the request is invalid or another save/load operation is active.</returns>
        bool TryRequestSave(byte slotIndex, uint sourceHash, uint operationId = 0u);

        /// <summary>
        /// Packs and queues one WFC outpost mutable-grid snapshot for MacroDB persistence.
        /// </summary>
        /// <param name="sectorHash">Absolute AUP-derived sector hash.</param>
        /// <param name="wfcGrid">Caller-owned 10x10x5 mutable state grid.</param>
        /// <param name="frame">Producer frame id.</param>
        /// <param name="status">Result status for rejection, no-op, or dirty queue.</param>
        /// <returns>True when the snapshot was accepted or skipped as unchanged.</returns>
        bool TryPersistWfcOutpostStateSnapshot(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            uint frame,
            out WfcOutpostPersistenceStatus status);

        /// <summary>
        /// Applies a saved WFC outpost mutable-grid override before procedural outpost extraction consumes it.
        /// </summary>
        /// <param name="sectorHash">Absolute AUP-derived sector hash.</param>
        /// <param name="wfcGrid">Destination mutable-state grid; do not pass a topology/adjacency-packed WFC cell grid.</param>
        /// <param name="status">Restore result; corrupt length means caller must generate a fresh base.</param>
        /// <returns>True only when saved state was copied into <paramref name="wfcGrid"/>.</returns>
        bool TryApplyWfcOutpostStateOverride(
            ulong sectorHash,
            NativeArray<byte> wfcGrid,
            out WfcOutpostPersistenceStatus status);

        /// <summary>
        /// Copies a chunk-local payload into the async world pager write queue.
        /// </summary>
        /// <param name="sectorHash">Absolute AUP-derived sector hash. Must not be runtime-origin relative.</param>
        /// <param name="payloadType">Stable payload family hash from <see cref="H8WorldPagePayloadTypes"/>.</param>
        /// <param name="payload">Native payload source. The service copies before returning.</param>
        /// <param name="byteCount">Payload bytes to copy.</param>
        /// <param name="sourceHash">Stable producer hash for telemetry.</param>
        /// <param name="frame">Producer frame id.</param>
        /// <returns>False when the queue is full, uninitialized, or payload exceeds one sector.</returns>
        bool TryEnqueueChunkPageWrite(
            long sectorHash,
            uint payloadType,
            NativeArray<byte> payload,
            int byteCount,
            uint sourceHash,
            uint frame);

        /// <summary>
        /// Queues a non-blocking read of a chunk-local pager payload.
        /// </summary>
        /// <param name="sectorHash">Absolute AUP-derived sector hash.</param>
        /// <param name="payloadType">Stable payload family hash from <see cref="H8WorldPagePayloadTypes"/>.</param>
        /// <param name="requestId">Caller-owned non-zero ticket id.</param>
        /// <param name="ticket">Read ticket for later completion copy.</param>
        /// <returns>False when the queue is full or the service is unavailable.</returns>
        bool TryRequestChunkPageRead(
            long sectorHash,
            uint payloadType,
            uint requestId,
            out H8WorldPageReadTicket ticket);

        /// <summary>
        /// Copies one completed page into caller-owned native memory without blocking the main thread.
        /// Corrupt or missing pages return true with a non-ready status so callers can use procedural fallback.
        /// </summary>
        bool TryCopyCompletedChunkPage(
            in H8WorldPageReadTicket ticket,
            NativeArray<byte> destination,
            out int bytesWritten,
            out H8WorldPageStatus status);

        /// <summary>
        /// Releases one completed page result without copying payload bytes. This is for prefetch callers that
        /// need pager backpressure cleared before a dedicated hydration consumer is wired.
        /// </summary>
        bool TryRetireCompletedChunkPage(
            in H8WorldPageReadTicket ticket,
            out H8WorldPageStatus status,
            out int byteCount);

        /// <summary>Returns the current async pager counters for telemetry surfaces.</summary>
        H8WorldPagerTelemetrySnapshot GetWorldPagerTelemetry();

        /// <summary>Flushes the pager handle during controlled shutdown or save synchronization.</summary>
        void FlushWorldPager();

        /// <summary>
        /// Requests one macro database tombstone compaction pass. The service may reject while save/load is busy,
        /// below threshold, under memory pressure, or already compacting.
        /// </summary>
        bool TryRequestMacroDatabaseCompaction(MacroDatabaseTier tier, byte reasonFlags = 0);

        /// <summary>
        /// Attempts the bounded main-thread finalization step for a completed macro database compaction copy.
        /// </summary>
        bool TryCompleteMacroDatabaseCompaction(MacroDatabaseTier tier);

        /// <summary>
        /// Returns current macro database compaction counters for H-PHI, memory sentinel, and diagnostics.
        /// </summary>
        MacroDatabaseCompactionSnapshot GetMacroDatabaseCompactionSnapshot();
    }

    /// <summary>
    /// Minimal UI service contract exposed through <see cref="GlobalRegistry"/>.
    /// Exactly one authoritative UI root may occupy the registry slot at runtime.
    /// </summary>
    public interface IUIService : ISystem
    {
        /// <summary>
        /// True once the service has completed explicit bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Diegetic scanner-interference sink used by cold-path memory recovery flows.
    /// </summary>
    public interface IScannerInterferenceUiSink
    {
        /// <summary>
        /// Enables or disables the scanner interference overlay without text churn.
        /// </summary>
        void SetScannerInterferenceActive(bool active);
    }

    /// <summary>
    /// Authored VR chest socket identifiers exposed through the somatic provider contract.
    /// </summary>
    public enum VRSomaticChestSocketId : byte
    {
        PDA = 0,
        FlareTool = 1
    }

    /// <summary>
    /// Immutable AUP-backed chest socket pose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct VRSomaticChestSocketPose
    {
        public readonly AbsoluteUniversePosition SocketAup;
        public readonly Vector3 RuntimePosition;
        public readonly Quaternion RuntimeRotation;

        public VRSomaticChestSocketPose(
            AbsoluteUniversePosition socketAup,
            Vector3 runtimePosition,
            Quaternion runtimeRotation)
        {
            SocketAup = socketAup;
            RuntimePosition = runtimePosition;
            RuntimeRotation = runtimeRotation;
        }
    }

    /// <summary>
    /// Immutable near-field head contact state emitted by the VR somatic provider.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct VRSomaticCollisionState
    {
        public readonly byte HasContactFlag;
        public readonly AbsoluteUniversePosition ContactAup;
        public readonly Vector3 RuntimePoint;
        public readonly Vector3 RuntimeNormal;
        public readonly float DistanceMeters;
        public readonly float Intensity01;
        public readonly float ImpactSpeedMetersPerSecond;

        public VRSomaticCollisionState(
            bool hasContact,
            AbsoluteUniversePosition contactAup,
            Vector3 runtimePoint,
            Vector3 runtimeNormal,
            float distanceMeters,
            float intensity01,
            float impactSpeedMetersPerSecond)
        {
            HasContactFlag = hasContact ? (byte)1 : (byte)0;
            ContactAup = contactAup;
            RuntimePoint = runtimePoint;
            RuntimeNormal = runtimeNormal;
            DistanceMeters = distanceMeters;
            Intensity01 = intensity01;
            ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond;
        }

        public bool HasContact => HasContactFlag != 0;
    }

    /// <summary>
    /// Immutable frame snapshot for VR somatic suit systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct VRSomaticSnapshot
    {
        public readonly byte IsActiveFlag;
        public readonly AbsoluteUniversePosition HeadAup;
        public readonly Vector3 HeadRuntimePosition;
        public readonly Quaternion HeadRuntimeRotation;
        public readonly Quaternion VisorHudWorldRotation;
        public readonly float PlayerStress01;
        public readonly float Oxygen01;
        public readonly float DepthMeters;
        public readonly float NearFieldCollision01;
        public readonly float Condensation01;

        public VRSomaticSnapshot(
            bool isActive,
            AbsoluteUniversePosition headAup,
            Vector3 headRuntimePosition,
            Quaternion headRuntimeRotation,
            Quaternion visorHudWorldRotation,
            float playerStress01,
            float oxygen01,
            float depthMeters,
            float nearFieldCollision01,
            float condensation01)
        {
            IsActiveFlag = isActive ? (byte)1 : (byte)0;
            HeadAup = headAup;
            HeadRuntimePosition = headRuntimePosition;
            HeadRuntimeRotation = headRuntimeRotation;
            VisorHudWorldRotation = visorHudWorldRotation;
            PlayerStress01 = playerStress01;
            Oxygen01 = oxygen01;
            DepthMeters = depthMeters;
            NearFieldCollision01 = nearFieldCollision01;
            Condensation01 = condensation01;
        }

        public bool IsActive => IsActiveFlag != 0;

        public static readonly VRSomaticSnapshot Inactive = new VRSomaticSnapshot(
            false,
            default,
            Vector3.zero,
            Quaternion.identity,
            Quaternion.identity,
            0f,
            1f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Immutable hand pose pair for VR hand renderers: controller target versus spring-driven physical hand.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct VRSomaticHandPose
    {
        public readonly byte HandIndex;
        public readonly byte HasTrackingFlag;
        public readonly byte GhostVisibleFlag;
        public readonly byte Reserved;
        public readonly Vector3 TargetRuntimePosition;
        public readonly Vector3 PhysicalRuntimePosition;
        public readonly float SeparationMetersSq;

        public VRSomaticHandPose(
            byte handIndex,
            bool hasTracking,
            bool ghostVisible,
            Vector3 targetRuntimePosition,
            Vector3 physicalRuntimePosition,
            float separationMetersSq)
        {
            HandIndex = handIndex;
            HasTrackingFlag = hasTracking ? (byte)1 : (byte)0;
            GhostVisibleFlag = ghostVisible ? (byte)1 : (byte)0;
            Reserved = 0;
            TargetRuntimePosition = targetRuntimePosition;
            PhysicalRuntimePosition = physicalRuntimePosition;
            SeparationMetersSq = separationMetersSq;
        }

        public bool IsTracked => HasTrackingFlag != 0;
        public bool ShouldRenderGhost => GhostVisibleFlag != 0;
    }

    /// <summary>
    /// VR-only somatic suit bridge. PC/console callers must depend on this interface and receive the dummy provider.
    /// </summary>
    public interface IVRSomaticProvider
    {
        bool IsActive { get; }
        VRSomaticSnapshot CurrentSnapshot { get; }
        uint HandGhostMask { get; }

        void BindRig(
            Transform hmdTransform,
            Transform visorHudRoot,
            Transform pdaChestSocket,
            Transform flareToolChestSocket,
            AudioSource breathingSource,
            AudioLowPassFilter breathingLowPassFilter);

        void BindDecoupledRoot(Transform vrRootTransform);

        bool TryGetChestSocket(VRSomaticChestSocketId socketId, out VRSomaticChestSocketPose socketPose);
        bool TryGetHandPose(byte handIndex, out VRSomaticHandPose handPose);
        bool TryGetNearFieldCollision(out VRSomaticCollisionState collisionState);
    }

    /// <summary>
    /// PC/console null-object provider. It never touches XR APIs, physics jobs, shaders, audio, or haptics.
    /// </summary>
    public sealed class PcVRSomaticProvider : IVRSomaticProvider
    {
        // COLD ALLOC: PcVRSomaticProvider[1] - null-object fallback for GlobalRegistry.VRSomatic - owner: GlobalRegistry
        public static readonly PcVRSomaticProvider Shared = new PcVRSomaticProvider();

        private PcVRSomaticProvider()
        {
        }

        public bool IsActive => false;
        public VRSomaticSnapshot CurrentSnapshot => VRSomaticSnapshot.Inactive;
        public uint HandGhostMask => 0u;

        public void BindRig(
            Transform hmdTransform,
            Transform visorHudRoot,
            Transform pdaChestSocket,
            Transform flareToolChestSocket,
            AudioSource breathingSource,
            AudioLowPassFilter breathingLowPassFilter)
        {
        }

        public void BindDecoupledRoot(Transform vrRootTransform)
        {
        }

        public bool TryGetChestSocket(VRSomaticChestSocketId socketId, out VRSomaticChestSocketPose socketPose)
        {
            socketPose = default;
            return false;
        }

        public bool TryGetHandPose(byte handIndex, out VRSomaticHandPose handPose)
        {
            handPose = default;
            return false;
        }

        public bool TryGetNearFieldCollision(out VRSomaticCollisionState collisionState)
        {
            collisionState = default;
            return false;
        }
    }

    /// <summary>
    /// Registry-backed AR waypoint projection service. Static callers route through
    /// <see cref="GlobalRegistry"/> instead of owning a local singleton.
    /// </summary>
    public interface IARWaypointService
    {
        /// <summary>
        /// True once the waypoint overlay has a live HUD target.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Register or refresh an external waypoint bound to a transform target.
        /// </summary>
        void SetWaypoint(int id, Transform target, string label, Color color);

        /// <summary>
        /// Register or refresh an external waypoint bound to a runtime-space position.
        /// </summary>
        void SetWaypoint(int id, Vector3 worldPosition, string label, Color color);

        /// <summary>
        /// Remove a previously registered external waypoint.
        /// </summary>
        void ClearWaypoint(int id);
    }

    /// <summary>
    /// Registry-backed spatial trigger service for authored AUP points of interest.
    /// Implementations must keep hot checks in native data and publish cross-domain state through signals.
    /// </summary>
    public interface ISpatialTriggerSystem : ISystem
    {
        /// <summary>
        /// Number of native POI trigger slots currently registered.
        /// </summary>
        int RegisteredPoiCount { get; }

        /// <summary>
        /// Packed one-shot state for save/RLE consumers.
        /// </summary>
        ulong PoiStateMask { get; }

        /// <summary>
        /// Copies the packed one-shot state without exposing owner storage.
        /// </summary>
        /// <param name="stateMask">Current POI trigger bitmask.</param>
        /// <returns>True when the service has a valid state snapshot.</returns>
        bool TryGetPoiStateMask(out ulong stateMask);
    }

    /// <summary>
    /// Authored flags for AUP narrative POI trigger metadata.
    /// </summary>
    [Flags]
    public enum NarrativeSpatialTriggerFlags : byte
    {
        None = 0,
        HudBreadcrumb = 1 << 0
    }

    /// <summary>
    /// Cold-path authoring payload copied from scene POI components into the native spatial registry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct NarrativeSpatialTriggerAuthoring
    {
        public AbsoluteUniversePosition PositionAup;
        public float RadiusMeters;
        public float RadiusSq;
        public uint PoiHash;
        public uint QuestHash;
        public uint BiomeHash;
        public uint SoundscapeHash;
        public uint LoreHash;
        public int BitIndex;
        public NarrativeSpatialTriggerFlags Flags;
    }

    /// <summary>
    /// Blittable player pose snapshot for systems that need player AUP and view direction without concrete player-runtime access.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PlayerRuntimePoseSnapshot
    {
        public float3 RuntimePosition;
        public float3 Forward;
        public AbsoluteUniversePosition Aup;
        public uint Flags;

        public PlayerRuntimePoseSnapshot(float3 runtimePosition, float3 forward, AbsoluteUniversePosition aup, uint flags)
        {
            RuntimePosition = runtimePosition;
            Forward = forward;
            Aup = aup;
            Flags = flags;
        }
    }

    /// <summary>
    /// Authoritative player runtime-context contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IPlayerRuntimeContext
    {
        /// <summary>
        /// True once the context owner completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current bootstrap-published player object.
        /// </summary>
        GameObject PlayerObject { get; }

        /// <summary>
        /// Current bootstrap-published player transform.
        /// </summary>
        Transform PlayerTransform { get; }

        /// <summary>
        /// Player locomotion owner resolved from the current player root.
        /// </summary>
        HectonPlayerMovement PlayerMovement { get; }

        /// <summary>
        /// Player rigidbody resolved from the current player root.
        /// </summary>
        Rigidbody PlayerRigidbody { get; }

        /// <summary>
        /// Cached survival owner resolved from the current player root.
        /// </summary>
        HectonSurvivalSystem SurvivalSystem { get; }

        /// <summary>
        /// Cached health facade resolved from the current player root.
        /// </summary>
        HectonPlayerHealth PlayerHealth { get; }

        /// <summary>
        /// Cached trauma dispatcher resolved from the current player root.
        /// </summary>
        TraumaDispatcher TraumaDispatcher { get; }

        /// <summary>
        /// Authoritative handheld-tool owner on the current player root.
        /// </summary>
        PlayerToolManager ToolManager { get; }

        /// <summary>
        /// Authoritative player inventory on the current player root.
        /// </summary>
        PlayerInventory Inventory { get; }

        /// <summary>
        /// Cached transport coordinator resolved from the current player root.
        /// </summary>
        PlayerTransportCoordinator PlayerTransportCoordinator { get; }

        /// <summary>
        /// Authoritative player camera resolved from player-owned movement state.
        /// </summary>
        Camera PlayerCamera { get; }

        /// <summary>
        /// Cached PDA owner bound to the active player when available.
        /// </summary>
        PlayerPDA PlayerPDA { get; }

        /// <summary>
        /// Cached builder backend bound to the active player when available.
        /// </summary>
        PlayerBuilder PlayerBuilder { get; }

        /// <summary>
        /// Cached visor controller bound to the active player when available.
        /// </summary>
        VisorHUDController VisorController { get; }

        /// <summary>
        /// Cached player flashlight owner when available.
        /// </summary>
        PlayerFlashlight Flashlight { get; }

        /// <summary>
        /// Cached player thruster-audio owner when available.
        /// </summary>
        PlayerThrusterAudio ThrusterAudio { get; }

        /// <summary>
        /// Cached underwater-visual owner bound to the active player when available.
        /// </summary>
        HectonUnderwaterVisuals UnderwaterVisuals { get; }

        /// <summary>
        /// Hand-anchor transform used by held tools.
        /// </summary>
        Transform HandAnchor { get; }

        /// <summary>
        /// Root collider used by player-centric environment systems.
        /// </summary>
        Collider PlayerCollider { get; }

        /// <summary>
        /// Active HUD notification sink when one is available.
        /// </summary>
        HUDNotification HudNotification { get; }

        /// <summary>
        /// Resolves the current player AUP, runtime position, and camera-facing direction without exposing concrete runtime state.
        /// </summary>
        bool TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot);
    }

    /// <summary>
    /// Focused player inventory/tooling context extracted from the player god object.
    /// Consumers should prefer this service over root-player component scraping.
    /// </summary>
    public interface IPlayerInventoryService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current authoritative handheld-tool owner.
        /// </summary>
        PlayerToolManager ToolManager { get; }

        /// <summary>
        /// Current authoritative player inventory.
        /// </summary>
        PlayerInventory Inventory { get; }

        /// <summary>
        /// Current builder backend bound to the active player when available.
        /// </summary>
        PlayerBuilder PlayerBuilder { get; }

        /// <summary>
        /// Hand-anchor transform used by held tools.
        /// </summary>
        Transform HandAnchor { get; }
    }

    /// <summary>
    /// Focused modular-equipment runtime service exposed through <see cref="GlobalRegistry"/>.
    /// Hot-path consumers read compiled tool stats and state from this owner only.
    /// </summary>
    public interface IModularEquipmentService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Registers or refreshes one active handheld tool and returns its stable runtime ID.
        /// </summary>
        uint RegisterTool(PlayerTool tool);

        /// <summary>
        /// Unregisters one active handheld tool.
        /// </summary>
        void UnregisterTool(PlayerTool tool, uint toolId);

        /// <summary>
        /// Returns the current tool-state snapshot when the tool is active.
        /// </summary>
        bool TryGetToolState(uint toolId, out ToolState state);

        /// <summary>
        /// Returns the current compiled stat snapshot when the tool is active.
        /// </summary>
        bool TryGetToolStats(uint toolId, out ToolRuntimeStats stats);

        /// <summary>
        /// Installs one module into the active tool runtime and recompiles its bitmask-backed stats.
        /// </summary>
        bool TryInstallModule(uint toolId, ToolModuleData module);

        /// <summary>
        /// Removes one module from the active tool runtime and recompiles its bitmask-backed stats.
        /// </summary>
        bool TryRemoveModule(uint toolId, string moduleId);

        /// <summary>
        /// Returns true when the supplied upgrade flag is active on the tool.
        /// </summary>
        bool HasUpgrade(uint toolId, ToolUpgradeBits flag);

        float GetMaxRange(uint toolId, float fallback);
        float GetPowerScalar(uint toolId, float fallback);
        float GetEfficiencyScalar(uint toolId, float fallback);
        float GetSpeedScalar(uint toolId, float fallback);
        float GetHeatGenerationRate(uint toolId, float fallback);
        float GetCooldownRate(uint toolId, float fallback);
        float GetBatteryDrainPerSecond(uint toolId, float fallback);
        float GetDurabilityDrainMultiplier(uint toolId, float fallback);
        float GetRecoilImpulse(uint toolId, float fallback);
        float GetBatteryNormalized(uint toolId, float fallback);
        void SetBattery(uint toolId, float normalizedBattery);
        void ConsumeBattery(uint toolId, float normalizedBatteryDelta);
        void ConsumeBattery(uint toolId, float normalizedBatteryDrainRate, float deltaSeconds);
        void SetHeat(uint toolId, float normalizedHeat);
        void SetDurability(uint toolId, float normalizedDurability);
    }

    /// <summary>
    /// Focused player sensory/presentation context extracted from the player god object.
    /// Consumers should prefer this service over root-player component scraping.
    /// </summary>
    public interface IPlayerSensoryService
    {
        /// <summary>
        /// True once the service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Authoritative player camera.
        /// </summary>
        Camera PlayerCamera { get; }

        /// <summary>
        /// Cached player flashlight owner when available.
        /// </summary>
        PlayerFlashlight Flashlight { get; }

        /// <summary>
        /// Cached player thruster-audio owner when available.
        /// </summary>
        PlayerThrusterAudio ThrusterAudio { get; }

        /// <summary>
        /// Cached underwater-visual owner when available.
        /// </summary>
        HectonUnderwaterVisuals UnderwaterVisuals { get; }

        /// <summary>
        /// Cached visor controller when available.
        /// </summary>
        VisorHUDController VisorController { get; }

        /// <summary>
        /// Active HUD notification sink when available.
        /// </summary>
        HUDNotification HudNotification { get; }
    }

    /// <summary>
    /// Authoritative environment runtime-context contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEnvironmentRuntimeContext
    {
        /// <summary>
        /// True once the context owner completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Authoritative construction manager for module placement and integrity checks.
        /// </summary>
        ConstructionManager ConstructionManager { get; }

        /// <summary>
        /// Authoritative buildable catalog resolved from the construction manager.
        /// </summary>
        ModuleCatalog ModuleCatalog { get; }

        /// <summary>
        /// Authoritative runtime hazard registry.
        /// </summary>
        HazardZoneManager HazardZones { get; }
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
        /// Normalized storm/current intensity used by consumers for macro weather coupling.
        /// </summary>
        float WeatherIntensity { get; }

        /// <summary>
        /// Returns the latest zero-allocation runtime snapshot.
        /// </summary>
        WeatherRuntimeSnapshot GetRuntimeSnapshot();
    }

    /// <summary>
    /// Authoritative thermodynamics service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IThermodynamicsService
    {
        /// <summary>
        /// True once the thermodynamics owner is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Samples hydrothermal flow and cable entanglement without allocating.
        /// </summary>
        /// <param name="positionWS">World-space sample point.</param>
        /// <param name="radiusWS">Additional sample radius.</param>
        /// <param name="sample">Resolved flow and cable payload.</param>
        /// <returns>True when any updraft or cable influence is active at the sample point.</returns>
        bool SampleThermalFlow(Vector3 positionWS, float radiusWS, out AbyssalThermalManager.ThermalFlowSample sample);

        /// <summary>
        /// Samples the latest Celsius heat field without allocating.
        /// </summary>
        bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius);

        /// <summary>
        /// Exposes the front-buffer coarse thermal map for avoidance/read-only consumers.
        /// </summary>
        bool TryGetThermalMapReadback(
            out NativeArray<float> temperatureCelsius,
            out int width,
            out int height,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Exposes the front-buffer 32x32x32 Celsius grid for read-only consumers.
        /// </summary>
        bool TryGetThermalGridReadback(
            out NativeArray<float> temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Injects a transient heat source without exposing thermodynamics internals.
        /// </summary>
        bool TryInjectTransientHeatSource(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId);
    }

    /// <summary>
    /// Authoritative logistics/build-network service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface ILogisticsService
    {
        /// <summary>
        /// True once the logistics owner is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Total number of runtime modules tracked by the logistics owner.
        /// </summary>
        int ModuleCount { get; }

        /// <summary>
        /// Authoritative buildable catalog used for module restoration and placement.
        /// </summary>
        ModuleCatalog Catalog { get; }

        /// <summary>
        /// Read-only live module registry.
        /// </summary>
        IReadOnlyList<GameObject> SpawnedModules { get; }

        /// <summary>
        /// Creates a temporary bypass edge between two placed base modules when permitted.
        /// </summary>
        /// <param name="sourceModule">Source base module.</param>
        /// <param name="destinationModule">Destination base module.</param>
        /// <returns>True when a bypass edge was added.</returns>
        bool TryCreateTemporaryBypass(BaseModule sourceModule, BaseModule destinationModule);
    }

    /// <summary>
    /// Blittable flood readback for one habitat room, expressed in runtime-space meters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct HabitatRoomWaterlineSnapshot
    {
        public const byte FlagBreached = 1 << 0;
        public const byte FlagFlooded = 1 << 1;
        public const byte FlagPowered = 1 << 2;
        public const byte FlagOxygenDisabled = 1 << 3;

        public HabitatRoomWaterlineSnapshot(
            int roomId,
            float fill01,
            float surfaceY,
            float floorY,
            float ceilingY,
            float waterVolumeM3,
            uint sequence,
            byte flags)
        {
            RoomId = roomId;
            Fill01 = fill01;
            SurfaceY = surfaceY;
            FloorY = floorY;
            CeilingY = ceilingY;
            WaterVolumeM3 = waterVolumeM3;
            Sequence = sequence;
            Flags = flags;
        }

        public int RoomId { get; }
        public float Fill01 { get; }
        public float SurfaceY { get; }
        public float FloorY { get; }
        public float CeilingY { get; }
        public float WaterVolumeM3 { get; }
        public uint Sequence { get; }
        public byte Flags { get; }

        public bool IsValid =>
            RoomId >= 0 &&
            math.isfinite(Fill01) &&
            math.isfinite(SurfaceY) &&
            math.isfinite(FloorY) &&
            math.isfinite(CeilingY) &&
            CeilingY > FloorY;
    }

    /// <summary>
    /// Authoritative habitat graph flood read model exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IHabitatGraphService : ISystem
    {
        bool IsInitialized { get; }
        int RoomCount { get; }
        NativeArray<float>.ReadOnly RoomWaterLevels { get; }
        uint FloodStateSequence { get; }

        bool TryResolveRoomWaterline(
            Vector3 runtimePosition,
            int cachedRoomId,
            out HabitatRoomWaterlineSnapshot snapshot);

        bool TryGetRoomWaterline(int roomId, out HabitatRoomWaterlineSnapshot snapshot);
    }

    /// <summary>
    /// Authoritative habitat module deconstruction service exposed through <see cref="GlobalRegistry"/>.
    /// Requests enter through a NativeQueue signal and are validated by the construction graph owner.
    /// </summary>
    public interface IHabitatDeconstructionSystem : ISystem
    {
        /// <summary>
        /// True once the runtime owner is registered and can drain deconstruction requests.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Queues one AUP-space deconstruction request for validation and rollback.
        /// </summary>
        bool EnqueueDeconstruction(in DeconstructRequestSignal signal);

        /// <summary>
        /// Toggles the non-authoritative deconstruction preview state for a target entity.
        /// </summary>
        bool TrySetDeconstructionPreview(uint targetEntityId, bool enabled);
    }

    /// <summary>
    /// Authoritative fluid pipe pressure graph exposed through <see cref="GlobalRegistry"/>.
    /// Implementations must keep simulation data in SOA native buffers and route rupture visuals through signals.
    /// </summary>
    public interface IFluidPipeGraphService
    {
        bool IsInitialized { get; }
        int PipeNodeCount { get; }

        bool TryReadPipeNode(
            int nodeIndex,
            out float pressureKPa,
            out float contents,
            out byte flags);

        bool TryRegisterPipeNode(
            int networkId,
            int roomIndex,
            byte contentKind,
            AbsoluteUniversePosition nodeAup,
            float capacity,
            float maxPressureKPa,
            out int nodeIndex);

        bool TryConnectPipeNodes(int sourceNodeIndex, int destinationNodeIndex);
        bool TryInjectPipeContents(int nodeIndex, float contents);
        bool TrySetPipeSourceRate(int nodeIndex, float contentsPerSecond);
        bool TrySetPipeDemandRate(int nodeIndex, float contentsPerSecond);
        bool TrySetPipeNodeFlags(int nodeIndex, byte setMask, byte clearMask);
    }

    /// <summary>
    /// Authoritative world-generation service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IWorldGenService
    {
        /// <summary>
        /// True once the world-generation owner is registered and ready to process bootstrap/world refresh work.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Primes the cold bootstrap scatter pass when the world scene is ready.
        /// </summary>
        /// <returns>True when the pass was accepted.</returns>
        bool TryPrimeBootstrapScatterPass();

        /// <summary>
        /// Rebuilds the current scatter preview.
        /// </summary>
        void RebuildScatterPreview();

        /// <summary>
        /// Clears the current scatter preview.
        /// </summary>
        void ClearScatterPreview();
    }

    /// <summary>
    /// Deterministic world-seed provider exposed through <see cref="GlobalRegistry"/> for save/header validation.
    /// </summary>
    public interface IWorldSeedProvider
    {
        /// <summary>
        /// True once the seed owner is registered and ready to answer save/load validation queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Runtime world seed used by procedural geology and save-header consistency checks.
        /// </summary>
        int RuntimeWorldSeed { get; }

        /// <summary>
        /// Version identifier for the active procedural world-generation algorithm.
        /// </summary>
        int RuntimeWorldGenerationVersionId { get; }
    }

    /// <summary>
    /// Authoritative mathematical wake displacement service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IWakeDisplacementService : ISystem
    {
        /// <summary>
        /// True once the runtime owner has allocated fixed wake state and can publish shader globals.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Count of active wake entries published to the current frame shader buffer.
        /// </summary>
        int ActiveWakeCount { get; }

        /// <summary>
        /// Injects one producer-agnostic wake packet.
        /// </summary>
        /// <param name="signal">AUP-space emitter position plus runtime velocity and source flags.</param>
        void EmitWake(in WakeGeneratedSignal signal);

        /// <summary>
        /// Clears persistent procedural wake state and publishes an empty buffer.
        /// </summary>
        void ClearWakeBuffer();
    }

    /// <summary>
    /// Authoritative procedural sway director exposed through <see cref="GlobalRegistry"/> for decoupled VFX wake producers.
    /// </summary>
    public interface IProceduralSwayDirector : IWakeDisplacementService
    {
        /// <summary>
        /// Exposes the latest GPU-culled procedural flora matrix buffer for vertex-sway consumers.
        /// </summary>
        bool TryGetCulledFloraVisibleBuffer(out GraphicsBuffer visibleInstancesBuffer, out int visibleInstanceCount);
    }

    /// <summary>
    /// Authoritative encounter-direction service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEncounterDirectorService
    {
        /// <summary>
        /// True once the encounter director is registered and participating in runtime dispatch.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current normalized tension score in the legacy 0..100 presentation range.
        /// </summary>
        float TensionScore { get; }

        /// <summary>
        /// True while predator pressure is allowed to escalate.
        /// </summary>
        bool IsPredatorPressureEnabled { get; }

        /// <summary>
        /// Human-readable current phase name for diagnostics consumers.
        /// </summary>
        string CurrentPhaseName { get; }

        /// <summary>
        /// Attempts to expose the current predator AUP GPU buffer for decoupled visual consumers.
        /// </summary>
        bool TryGetPredatorAupGpuBuffer(out GraphicsBuffer buffer, out int count);

        /// <summary>
        /// Forces the next completed encounter tick into the peak phase.
        /// </summary>
        void ForcePeak();

        /// <summary>
        /// Forces the next completed encounter tick into the relax phase.
        /// </summary>
        void ForceRelax();

        /// <summary>
        /// Resets the runtime encounter state.
        /// </summary>
        void ResetDirector();
    }

    /// <summary>
    /// Authoritative meta-campaign progression service exposed through <see cref="GlobalRegistry"/>.
    /// Consumers query stable uint state only; progression writes enter through signal lanes.
    /// </summary>
    public interface IMetaCampaignService : ISystem
    {
        /// <summary>
        /// True once native state, rules, and registry ownership are ready.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Stable hash of the current global campaign stage.
        /// </summary>
        uint CurrentCampaignStageHash { get; }

        /// <summary>
        /// Numeric stage used by save/debug only.
        /// </summary>
        int CurrentCampaignStage { get; }

        /// <summary>
        /// Global ocean toxicity scalar used by renderer and ecosystem fakes.
        /// </summary>
        float OceanToxicity01 { get; }

        /// <summary>
        /// True when late-game leviathan encounters may enter the pacing budget.
        /// </summary>
        bool IsLeviathanAwakened { get; }

        /// <summary>
        /// Reads a stable FNV1a global variable from the native campaign map.
        /// </summary>
        bool TryGetGlobalVariable(uint variableHash, out int value);

        /// <summary>
        /// Cold-path force set used by hidden developer tooling.
        /// </summary>
        bool TryForceSetGlobalVariable(uint variableHash, int value, byte reason);
    }

    /// <summary>
    /// Authoritative quest-system service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IQuestSystem
    {
        /// <summary>
        /// True once the quest runtime owner is registered and available for gameplay queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Activates the authored quest when it exists in the registry.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        void ActivateQuest(string questId);

        /// <summary>
        /// Completes the authored quest when it exists in the registry.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        void CompleteQuest(string questId);

        /// <summary>
        /// Returns true when the quest is currently active.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsActive(string questId);

        /// <summary>
        /// Returns true when the quest is currently completed.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsCompleted(string questId);

        /// <summary>
        /// Returns true when the native quest flag bit is set for the supplied stable flag hash.
        /// </summary>
        /// <param name="flagId">Stable quest flag hash.</param>
        bool GetFlag(uint flagId);

        /// <summary>
        /// Resolves the authored quest identifier from a stable quest hash.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        /// <param name="questId">Resolved authored quest identifier.</param>
        /// <returns>True when the hash maps to an authored quest.</returns>
        bool TryGetQuestIdByHash(uint questHash, out string questId);
    }

    /// <summary>
    /// Data-only fauna simulation contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IFaunaSim
    {
        /// <summary>
        /// True once the simulation owner has allocated its native residency buffers.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Maximum resident/dehydrated fauna slots available to the simulation.
        /// </summary>
        int ResidentSlotCapacity { get; }
    }

    /// <summary>
    /// Data-only fluid simulation math contract exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IFluidSim
    {
        /// <summary>
        /// True once the math service is ready for deterministic simulation calls.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Resolved water density used by submarine flood mass calculations.
        /// </summary>
        float WaterDensityKilogramsPerCubicMeter { get; }

        /// <summary>
        /// Resolves Torricelli ingress velocity at the supplied depth.
        /// </summary>
        /// <param name="depthMeters">External water depth in meters.</param>
        /// <returns>Ingress velocity in meters per second.</returns>
        float ResolveIngressVelocity(float depthMeters);
    }

    /// <summary>
    /// Room gas flags used by the scalar Dalton solver. Room ids are local to their owning habitat/base.
    /// </summary>
    [System.Flags]
    public enum GasDynamicsRoomFlags : ushort
    {
        None = 0,
        InternalFire = 1 << 0,
        Breached = 1 << 1,
        ScrubberInstalled = 1 << 2,
        Occupied = 1 << 3
    }

    /// <summary>
    /// Runtime gas solve cadence selected from the boot hardware tier.
    /// </summary>
    public enum GasDynamicsMathLod : byte
    {
        Low = 0,
        Mid = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Blittable room gas snapshot expressed as Dalton partial pressures in kPa.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct GasRoomSnapshot
    {
        public GasRoomSnapshot(
            int roomId,
            float oxygenKPa,
            float carbonDioxideKPa,
            float nitrogenKPa,
            float pressureKPa,
            float ambientPressureKPa,
            float toxicity01,
            float narcosis01,
            ushort flags)
        {
            RoomId = roomId;
            OxygenKPa = oxygenKPa;
            CarbonDioxideKPa = carbonDioxideKPa;
            NitrogenKPa = nitrogenKPa;
            PressureKPa = pressureKPa;
            AmbientPressureKPa = ambientPressureKPa;
            Toxicity01 = toxicity01;
            Narcosis01 = narcosis01;
            Flags = flags;
        }

        public int RoomId { get; }
        public float OxygenKPa { get; }
        public float CarbonDioxideKPa { get; }
        public float NitrogenKPa { get; }
        public float PressureKPa { get; }
        public float AmbientPressureKPa { get; }
        public float Toxicity01 { get; }
        public float Narcosis01 { get; }
        public ushort Flags { get; }
    }

    /// <summary>
    /// Cold-path hibernation snapshot for one habitat/base atmosphere island.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct GasBaseHibernationSnapshot
    {
        public GasBaseHibernationSnapshot(
            int baseId,
            int roomStart,
            int roomCount,
            AbsoluteUniversePosition centerAup,
            bool awake,
            bool playerInside,
            float batteryWattSeconds,
            float idleDrawWatts,
            float leakRatePerSecond,
            double hibernatedUnscaledTime)
        {
            BaseId = baseId;
            RoomStart = roomStart;
            RoomCount = roomCount;
            CenterAup = centerAup;
            Awake = awake;
            PlayerInside = playerInside;
            BatteryWattSeconds = batteryWattSeconds;
            IdleDrawWatts = idleDrawWatts;
            LeakRatePerSecond = leakRatePerSecond;
            HibernatedUnscaledTime = hibernatedUnscaledTime;
        }

        public int BaseId { get; }
        public int RoomStart { get; }
        public int RoomCount { get; }
        public AbsoluteUniversePosition CenterAup { get; }
        public bool Awake { get; }
        public bool PlayerInside { get; }
        public float BatteryWattSeconds { get; }
        public float IdleDrawWatts { get; }
        public float LeakRatePerSecond { get; }
        public double HibernatedUnscaledTime { get; }
    }

    /// <summary>
    /// Unmanaged gas-to-physiology signal emitted when CO2 toxicity or nitrogen narcosis crosses a scalar threshold.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ToxicitySignal
    {
        public ToxicitySignal(
            int roomId,
            float carbonDioxideKPa,
            float pressureAtm,
            float toxicity01,
            float narcosis01,
            uint frameIndex,
            ushort flags)
        {
            RoomId = roomId;
            CarbonDioxideKPa = carbonDioxideKPa;
            PressureAtm = pressureAtm;
            Toxicity01 = toxicity01;
            Narcosis01 = narcosis01;
            FrameIndex = frameIndex;
            Flags = flags;
        }

        public int RoomId { get; }
        public float CarbonDioxideKPa { get; }
        public float PressureAtm { get; }
        public float Toxicity01 { get; }
        public float Narcosis01 { get; }
        public uint FrameIndex { get; }
        public ushort Flags { get; }
    }

    /// <summary>
    /// Cold-path audit snapshot for the Dalton gas solver's persistent native memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct GasDynamicsNativeMemoryAudit
    {
        public GasDynamicsNativeMemoryAudit(
            int roomCapacity,
            int bulkheadCapacity,
            int localAllocationCount,
            long localRegisteredBytes,
            long largestAllocationBytes,
            uint largestAllocationLabelHash,
            int sentinelActiveAllocationCount,
            long sentinelTrackedBytes)
        {
            RoomCapacity = roomCapacity;
            BulkheadCapacity = bulkheadCapacity;
            LocalAllocationCount = localAllocationCount;
            LocalRegisteredBytes = localRegisteredBytes;
            LargestAllocationBytes = largestAllocationBytes;
            LargestAllocationLabelHash = largestAllocationLabelHash;
            SentinelActiveAllocationCount = sentinelActiveAllocationCount;
            SentinelTrackedBytes = sentinelTrackedBytes;
        }

        public int RoomCapacity { get; }
        public int BulkheadCapacity { get; }
        public int LocalAllocationCount { get; }
        public long LocalRegisteredBytes { get; }
        public long LargestAllocationBytes { get; }
        public uint LargestAllocationLabelHash { get; }
        public int SentinelActiveAllocationCount { get; }
        public long SentinelTrackedBytes { get; }
    }

    /// <summary>
    /// Registry-owned Dalton gas solver. Callers push local room facts; the solver owns native gas arrays and physiology signals.
    /// </summary>
    public interface IGasDynamicsSolver : ISystem
    {
        bool IsInitialized { get; }
        int RoomCount { get; }
        int BaseCount { get; }
        NativeArray<float>.ReadOnly RoomO2 { get; }
        NativeArray<float>.ReadOnly RoomCO2 { get; }
        NativeArray<float>.ReadOnly RoomPressure { get; }
        NativeArray<byte>.ReadOnly BaseAwakeState { get; }

        bool TryGetRoomSnapshot(int roomId, out GasRoomSnapshot snapshot);
        bool TryGetBaseHibernationSnapshot(int baseId, out GasBaseHibernationSnapshot snapshot);
        bool TryConfigureRoom(
            int roomId,
            float oxygenKPa,
            float carbonDioxideKPa,
            float nitrogenKPa,
            float ambientPressureKPa,
            ushort flags);
        bool TryConfigureBase(
            int baseId,
            int roomStart,
            int roomCount,
            AbsoluteUniversePosition centerAup,
            float batteryWattSeconds,
            float idleDrawWatts,
            float leakRatePerSecond);
        bool TrySetBasePlayerInside(int baseId, bool playerInside);
        bool TrySetBaseCenterAup(int baseId, AbsoluteUniversePosition centerAup);
        bool TrySetBulkhead(int edgeIndex, int roomA, int roomB, bool sealedBulkhead);
        bool TrySetPlayerRoom(int roomId, float playerStress01, float heartRateBpm);
        bool TrySetRoomFlags(int roomId, ushort setMask, ushort clearMask);
        bool TrySetRoomSubmergedFraction(int roomId, float submerged01);
        bool TrySetAmbientPressure(int roomId, float ambientPressureKPa);
        bool TryApplyPlayerRoomCarbonDioxideEquivalentPressure(float carbonDioxideKPa);
        bool TrySetScrubberPowered(int roomId, bool powerActive);
        bool TrySetRoomTemperatureCelsius(int roomId, float temperatureCelsius);
        bool TryDequeueToxicitySignal(out ToxicitySignal signal);
        bool TryGetNativeMemoryAudit(out GasDynamicsNativeMemoryAudit audit);
        float ResolveEffectiveDepthStress01(int roomId, float depthStress01);
    }

    /// <summary>
    /// Boot-time quality tier resolved from immutable hardware facts.
    /// </summary>
    public enum HectonQualityTier : byte
    {
        Unknown = 0,
        Low = 1,
        Mx350 = 2,
        Mid = 3,
        High = 4,
        Ultra = 5
    }

    /// <summary>
    /// BIOS-owned math precision tier. Runtime systems read this instead of selecting their own accuracy path.
    /// </summary>
    public enum MathPrecisionLevel : byte
    {
        Low = 0,
        High = 1
    }

    /// <summary>
    /// Immutable hardware profile captured during the bootstrap HardwareCheck phase.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct HectonHardwareProfile
    {
        /// <summary>
        /// Creates a boot-time hardware profile.
        /// </summary>
        public HectonHardwareProfile(
            int graphicsMemoryMegabytes,
            int systemMemoryMegabytes,
            int processorCount,
            HectonQualityTier qualityTier)
            : this(
                graphicsMemoryMegabytes,
                systemMemoryMegabytes,
                processorCount,
                qualityTier,
                0d,
                0,
                qualityTier == HectonQualityTier.High || qualityTier == HectonQualityTier.Ultra
                    ? MathPrecisionLevel.High
                    : MathPrecisionLevel.Low)
        {
        }

        /// <summary>
        /// Creates a boot-time hardware profile with BIOS physics benchmark telemetry.
        /// </summary>
        public HectonHardwareProfile(
            int graphicsMemoryMegabytes,
            int systemMemoryMegabytes,
            int processorCount,
            HectonQualityTier qualityTier,
            double physicsBenchmarkMillisecondsPerStep)
            : this(
                graphicsMemoryMegabytes,
                systemMemoryMegabytes,
                processorCount,
                qualityTier,
                physicsBenchmarkMillisecondsPerStep,
                0,
                qualityTier == HectonQualityTier.High || qualityTier == HectonQualityTier.Ultra
                    ? MathPrecisionLevel.High
                    : MathPrecisionLevel.Low)
        {
        }

        /// <summary>
        /// Creates a boot-time hardware profile with BIOS physics benchmark telemetry and math precision routing.
        /// </summary>
        public HectonHardwareProfile(
            int graphicsMemoryMegabytes,
            int systemMemoryMegabytes,
            int processorCount,
            HectonQualityTier qualityTier,
            double physicsBenchmarkMillisecondsPerStep,
            int hardwareScore,
            MathPrecisionLevel mathPrecisionLevel)
        {
            GraphicsMemoryMegabytes = graphicsMemoryMegabytes;
            SystemMemoryMegabytes = systemMemoryMegabytes;
            ProcessorCount = processorCount;
            QualityTier = qualityTier;
            PhysicsBenchmarkMillisecondsPerStep = physicsBenchmarkMillisecondsPerStep;
            HardwareScore = hardwareScore;
            MathPrecisionLevel = mathPrecisionLevel;
        }

        /// <summary>Detected graphics memory in megabytes.</summary>
        public int GraphicsMemoryMegabytes { get; }

        /// <summary>Detected system memory in megabytes.</summary>
        public int SystemMemoryMegabytes { get; }

        /// <summary>Detected logical CPU core count.</summary>
        public int ProcessorCount { get; }

        /// <summary>Resolved runtime quality tier.</summary>
        public HectonQualityTier QualityTier { get; }

        /// <summary>Cold BIOS local-physics benchmark cost in milliseconds per 0.02s step.</summary>
        public double PhysicsBenchmarkMillisecondsPerStep { get; }

        /// <summary>Deterministic 0-100 BIOS hardware score captured at boot.</summary>
        public int HardwareScore { get; }

        /// <summary>BIOS-selected math precision level for runtime shader/simulation paths.</summary>
        public MathPrecisionLevel MathPrecisionLevel { get; }
    }

    /// <summary>
    /// Immutable service rebound payload published after a live registry slot is replaced.
    /// </summary>
    public readonly struct ServiceReboundEvent
    {
        /// <summary>
        /// Creates a service rebound notification.
        /// </summary>
        public ServiceReboundEvent(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ServiceSlot = serviceSlot;
            PreviousService = previousService;
            CurrentService = currentService;
        }

        /// <summary>Registry slot that was replaced.</summary>
        public GlobalRegistryServiceSlot ServiceSlot { get; }

        /// <summary>Previous service instance, or null when the slot was empty.</summary>
        public object PreviousService { get; }

        /// <summary>Current service instance, or null when the slot was cleared.</summary>
        public object CurrentService { get; }
    }

    public enum RegistryEventType : byte
    {
        ServiceRebound = 0
    }

    /// <summary>
    /// Registry-owned shader-bent connection renderer. Static callers must route through this service instead of a local singleton.
    /// </summary>
    internal interface IConnectionSplineBatchRendererService
    {
        void SubmitPipeLink(long linkId, SplineDescriptor descriptor, Color color);
        void RemovePipeLink(long linkId);
        void SetPipeNodeRuptured(uint nodeId, bool ruptured);
        void SetPipeNodeFlow(uint nodeId, float flow01);
        void SubmitRelaySpline(long linkId, SplineDescriptor descriptor, bool hasPower, Color poweredColor, Color unpoweredColor);
        void RemoveRelayLink(long linkId);
    }

    /// <summary>
    /// Scene-owned modal UI facade. Static UI callers route through GlobalRegistry instead of a local singleton.
    /// </summary>
    public interface IModalWindowService
    {
        void ShowModal(
            string title,
            string message,
            System.Action onConfirm,
            System.Action onCancel,
            string confirmLabel,
            string cancelLabel);

        void CloseModal();
    }

    /// <summary>
    /// Unmanaged registry event payload drained by <see cref="SystemDispatcher"/>.
    /// Managed service references are carried by GlobalRegistry sidecar slots during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RegistryEventPayload
    {
        public uint PreviousServiceHash;
        public uint CurrentServiceHash;
        public int ReferenceSlot;
        public uint FrameIndex;
        public ushort ServiceSlot;
        public ushort EventType;
    }

    /// <summary>
    /// Listener contract for registry service-rebound payloads.
    /// </summary>
    public interface IRegistryEventListener
    {
        void OnRegistryEvent(in RegistryEventPayload payload);
    }

    /// <summary>
    /// Typed registry service slot identifiers used by GlobalRegistry hot-swap notifications.
    /// </summary>
    public enum GlobalRegistryServiceSlot : byte
    {
        Input = 0,
        Physics = 1,
        Audio = 2,
        Scene = 3,
        Save = 4,
        UI = 5,
        ObjectPool = 6,
        Player = 7,
        PlayerInventory = 8,
        ModularEquipment = 9,
        PlayerSensory = 10,
        Environment = 11,
        Weather = 12,
        OceanKinematics = 13,
        PowerGrid = 14,
        Submarine = 15,
        SubmarineHullBreach = 16,
        InteractionSignals = 17,
        Debris = 18,
        EcosystemDirector = 19,
        ThermodynamicsService = 20,
        Logistics = 21,
        WorldGen = 22,
        EncounterDirector = 23,
        QuestSystem = 24,
        FluidRuntime = 25,
        ThermodynamicsRuntime = 26,
        NarrativeDirectorRuntime = 27,
        QuestRuntime = 28,
        TickManager = 29,
        Dispatcher = 30,
        RenderDispatcher = 31,
        PhysicsStateManager = 32,
        FaunaSimulation = 33,
        FluidSimulation = 34,
        PersistentWorldRegistry = 35,
        PDALogbook = 36,
        PlayerMotor = 37,
        Profile = 38,
        InputBinding = 39,
        CullingRuntime = 40,
        LODSystemRuntime = 41,
        DynamicResolutionRuntime = 42,
        ImpostorRuntime = 43,
        DepthZoneRuntime = 44,
        LocalizationRuntime = 45,
        AudioLogRuntime = 46,
        AtlasSignalRuntime = 47,
        FirstHourRuntime = 48,
        EmergencyRelayRuntime = 49,
        AtmosphereRuntime = 50,
        BeaconNetworkRuntime = 51,
        ScanLogRuntime = 52,
        ToolDurabilityRuntime = 53,
        LoreDatabaseRuntime = 54,
        AssetLifecycleRuntime = 55,
        AssetLoadDispatcherRuntime = 56,
        VRAMMonitorRuntime = 57,
        VRAMPressureRuntime = 58,
        RenderTextureLifecycleRuntime = 59,
        RenderTexturePoolRuntime = 60,
        WorldStateRuntime = 61,
        UserOptionsRuntime = 62,
        BiolumManagerRuntime = 63,
        AbyssalFluidDecalRuntime = 64,
        SargassumDragRuntime = 65,
        SargassumCutRuntime = 66,
        PlayerExpressionRuntime = 67,
        SpectrumRuntime = 68,
        SoundscapeRuntime = 69,
        AcousticZoneRuntime = 70,
        SurfaceWeatherRuntime = 71,
        EnvironmentalStrainRuntime = 72,
        EcosystemHealthRuntime = 73,
        FaunaGeneticsRuntime = 74,
        PlayerExplorationRuntime = 75,
        DiscoveryRuntime = 76,
        ResourceScarcityRuntime = 77,
        PDAExchangeRuntime = 78,
        PlayerActionRuntime = 79,
        PDAMarkerRuntime = 80,
        AmbientWaterMotionRuntime = 81,
        SuitUpgradeRuntime = 82,
        EndingRuntime = 83,
        Atlas6DirectiveRuntime = 84,
        HazardZoneRuntime = 85,
        MissionRuntime = 86,
        RockManagerRuntime = 87,
        CameraJuiceRuntime = 88,
        MusicDirectorRuntime = 89,
        SubtitleRuntime = 90,
        AtlasSignalDecoderRuntime = 91,
        ScrapRuntime = 92,
        AutonomousExtractorRuntime = 93,
        VisorRTRuntime = 94,
        CameraRTRuntime = 95,
        PostFXRTRuntime = 96,
        UIRTRuntime = 97,
        SettingsRuntime = 98,
        RuntimeWatchdogRuntime = 99,
        CrashTelemetryRuntime = 100,
        PlayerCriticalAudioRuntime = 101,
        MapMagicRuntime = 102,
        TerrainProviderRuntime = 139,
        ProceduralFieldSamplerRuntime = 103,
        ResourceDistributionRuntime = 104,
        RandomEventRuntime = 105,
        EclipseGameplayRuntime = 106,
        WorldSeedProvider = 107,
        GeologyTerrainSeamRuntime = 108,
        GeologyVoxelBridgeRuntime = 109,
        SargassumMicroFaunaRuntime = 110,
        FloatingOriginRuntime = 111,
        PDAIntrusionRuntime = 112,
        CelestialEngineRuntime = 113,
        VoxelEngineRuntime = 114,
        BiomeMatrixRuntime = 115,
        UnderwaterVisualsRuntime = 116,
        DynamicDifficultyRuntime = 117,
        ToolHapticsRuntime = 118,
        ARWaypointRuntime = 119,
        VRSomaticProvider = 120,
        ConnectionSplineBatchRendererRuntime = 121,
        NativeInputManagerRuntime = 122,
        RaycastBatchRuntime = 123,
        FieldOperationLogRuntime = 124,
        CorporateOrderRuntime = 125,
        BiolumControllerRuntime = 126,
        UIAudioFeedbackRuntime = 127,
        UITooltipRuntime = 128,
        ScavengePopulatorRuntime = 129,
        RunModifierRuntime = 130,
        MigrationDirectorRuntime = 131,
        BasePollutionRuntime = 132,
        EntityChangeManagerRuntime = 133,
        PerformanceMonitorRuntime = 134,
        MapMagicVegetationRuntime = 135,
        ModWorldPersistenceRuntime = 136,
        LoadingScreenRuntime = 137,
        ModalWindowRuntime = 138,
        ProceduralSwayDirectorRuntime = 140,
        SubmarineState = 141,
        VocalWarningRuntime = 142,
        HabitatDeconstructionRuntime = 143,
        SeismicDirectorRuntime = 144,
        FluidPipeGraph = 145,
        GasDynamicsRuntime = 146,
        SpatialTriggerRuntime = 147,
        GIRelayRuntime = 148,
        DataVault = 149,
        JobAdmissionRuntime = 150,
        StreamingBackpressureRuntime = 151,
        FoveatedSimulationDirector = 152,
        GroundRadarRuntime = 153,
        InertialNavigationRuntime = 154,
        ModdingBridgeRuntime = 155,
        InstanceCullingRuntime = 156,
        WorldResourceSpawnerRuntime = 157,
        MacroDatabase = 158,
        MetaCampaignRuntime = 159,
        OrbitalDirectorRuntime = 160,
        SimulationBucketerRuntime = 161,
        CausticsRuntime = 162,
        PlayerMovementContracts = 163,
        HardwareThermalService = 164,
        AudioVirtualization = 165,
        OutpostGenerationRuntime = 166,
        PrologueSequenceRuntime = 167,
        DebrisComputeRuntime = 168,
        ResolutionScalerService = 169,
        AmbientBiotaRuntime = 170,
        DockingAutopilotRuntime = 171,
        ProceduralLadderClimbRuntime = 172,
        Unknown = 255
    }

    /// <summary>
    /// Explicit dependency-rebind hook used when <see cref="GlobalRegistry"/> safely replaces a live service.
    /// Implementers must re-run the dependency portion of their enable-time wiring without manually invoking Unity lifecycle methods.
    /// </summary>
    public interface IGlobalRegistryHotSwapListener
    {
        /// <summary>
        /// Called after a service slot has been replaced or cleared at runtime.
        /// </summary>
        /// <param name="serviceSlot">Registry slot that changed.</param>
        /// <param name="previousService">Previous service instance, or null if the slot was empty.</param>
        /// <param name="currentService">Current service instance, or null if the slot was cleared.</param>
        void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService);
    }

    /// <summary>
    /// Ref-forwarding hot-swap hook for systems that cache service pointers and must rebind during the registry event.
    /// </summary>
    public interface IGlobalRegistryHotSwapRefListener
    {
        /// <summary>
        /// Called before the compatibility hot-swap notification with a mutable local current-service reference.
        /// Implementers should update cached service fields here instead of polling <see cref="GlobalRegistry"/> per frame.
        /// </summary>
        /// <param name="serviceSlot">Registry slot that changed.</param>
        /// <param name="currentService">Current service instance, or null when the slot was cleared.</param>
        void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService);
    }

    /// <summary>
    /// Terrain height/normal authority exposed to gameplay without leaking MapMagic types.
    /// Implementations must answer from cached terrain ownership and avoid scene-wide scans in hot queries.
    /// </summary>
    public interface ITerrainProvider : ISystem
    {
        /// <summary>
        /// True when the terrain backend can answer samples.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Current water-surface level used by spawn and terrain validation code.
        /// </summary>
        float WaterSurfaceLevel { get; }

        /// <summary>
        /// Samples runtime-space terrain height at X/Z.
        /// </summary>
        bool TryGetHeight(float x, float z, out float height);

        /// <summary>
        /// Samples runtime-space terrain normal at X/Z using caller-provided spacing.
        /// </summary>
        bool TryGetNormal(float x, float z, float sampleDistance, out Vector3 normal);

        /// <summary>
        /// Samples terrain height from an Absolute Universe Position.
        /// </summary>
        bool TryGetHeightAUP(Vector3 absoluteUniversePosition, out float height);

        /// <summary>
        /// Samples terrain height from an Absolute Universe Position encoded as float3.
        /// </summary>
        float GetHeightAt(float3 aup);
    }

    /// <summary>
    /// Registry-backed ocean provider selector published through <see cref="GlobalRegistry"/>.
    /// Gameplay systems must query this service instead of talking to Crest-adapter singletons directly.
    /// </summary>
    public interface IHectonOceanKinematicsService : ISystem
    {
        /// <summary>
        /// True once the selector service has completed bootstrap registration.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Highest-priority currently available ocean kinematics provider.
        /// </summary>
        IHectonOceanKinematics ActiveProvider { get; }
    }

    /// <summary>
    /// Registry-facing owner of the analytical underwater caustics projection pass.
    /// </summary>
    public interface ICausticsService : ISystem
    {
        bool IsComputeActive { get; }
        RenderTexture CausticsMap { get; }
        Vector4 CausticsAup { get; }
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
        bool Publish(in Hecton8.Interaction.InteractionSignal signal, Collider targetCollider);

        /// <summary>
        /// Performs the shared zero-allocation tool hit query from a preformatted interaction packet.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="packet">Blittable tool request packet copied by value into the service-owned raycast lane.</param>
        /// <param name="layerMask">Physics layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool TryRaycastPrimary(ulong requesterId, in Hecton8.Interaction.InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit);

        /// <summary>
        /// Performs the shared zero-allocation tool hit query using the service-owned buffers.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="origin">Runtime-space ray origin.</param>
        /// <param name="direction">Runtime-space ray direction.</param>
        /// <param name="range">Maximum query range.</param>
        /// <param name="layerMask">Physics layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool TryRaycastPrimary(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit);

        /// <summary>
        /// Clears all queued interaction signals and associated transient target references.
        /// </summary>
        void ClearQueuedSignals();
    }

    /// <summary>
    /// Runtime debris burst service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IDebrisService
    {
        /// <summary>
        /// True once the service is registered and ready to accept debris spawn requests.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Spawns one debris burst using a pre-baked chunk definition.
        /// </summary>
        /// <param name="definition">Authoritative chunk definition.</param>
        /// <param name="runtimeOrigin">Runtime-space origin of the intact object.</param>
        /// <param name="runtimeRotation">Runtime-space rotation of the intact object.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="runtimeHitNormal">Runtime-space impact normal.</param>
        /// <param name="power01">Normalized tool power.</param>
        /// <param name="seed">Deterministic burst seed.</param>
        /// <returns>True when the burst was accepted.</returns>
        bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed);

        /// <summary>
        /// Spawns one bounded debris burst using a pre-baked chunk definition.
        /// </summary>
        /// <param name="definition">Authoritative chunk definition.</param>
        /// <param name="runtimeOrigin">Runtime-space origin of the intact object.</param>
        /// <param name="runtimeRotation">Runtime-space rotation of the intact object.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="runtimeHitNormal">Runtime-space impact normal.</param>
        /// <param name="power01">Normalized tool power.</param>
        /// <param name="seed">Deterministic burst seed.</param>
        /// <param name="maxChunkCount">Maximum authored chunks to activate. Values below one use all valid chunks.</param>
        /// <param name="lifetimeSeconds">Optional pooled lifetime override. Values at or below zero use the default profile lifetime.</param>
        /// <returns>True when the burst was accepted.</returns>
        bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed,
            int maxChunkCount,
            float lifetimeSeconds);

        /// <summary>
        /// Clears all active chunk bursts immediately.
        /// </summary>
        void ClearActiveDebris();
    }

    /// <summary>
    /// GPU-resident debris shard service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IDebrisComputeService
    {
        /// <summary>
        /// True when DataVault buffers and GPU buffers are ready for debris injection.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current CPU-side mirror count of live GPU debris particles.
        /// </summary>
        int ActiveDebrisCount { get; }

        /// <summary>
        /// Current tier-gated particle capacity.
        /// </summary>
        int ActiveParticleCapacity { get; }

        /// <summary>
        /// True when the MX350 / low-memory debris budget is active.
        /// </summary>
        bool IsLowTierActive { get; }

        /// <summary>
        /// Clears live GPU debris state without destroying persistent buffers.
        /// </summary>
        void ClearGpuDebris();
    }

    /// <summary>
    /// Immutable ecosystem population sample returned by <see cref="IEcosystemDirectorService"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EcosystemSectorPopulationSample
    {
        /// <summary>
        /// Quantized 1 km sector coordinate on the X axis.
        /// </summary>
        public int SectorX;

        /// <summary>
        /// Quantized 1 km sector coordinate on the Z axis.
        /// </summary>
        public int SectorZ;

        /// <summary>
        /// Current prey population carried by the sector simulation.
        /// </summary>
        public int PreyPopulation;

        /// <summary>
        /// Current predator population carried by the sector simulation.
        /// </summary>
        public int PredatorPopulation;

        /// <summary>
        /// Normalized prey fitness derived from sustained sector stress and survivor adaptation.
        /// </summary>
        public float Fitness;

        /// <summary>
        /// Sector-authored prey speed multiplier applied to spawned swarm agents.
        /// </summary>
        public float SpeedMultiplier;

        /// <summary>
        /// Sector-authored prey camouflage bias applied to spawned swarm agents.
        /// </summary>
        public float CamouflageIndex;

        /// <summary>
        /// True when the containing sector carries active apex pressure.
        /// </summary>
        public bool ApexInSector;

        /// <summary>
        /// Normalized prey biomass in the containing 50 m ecology macro-cell.
        /// </summary>
        public float PreyBiomass01;

        /// <summary>
        /// Normalized predator biomass in the containing 50 m ecology macro-cell.
        /// </summary>
        public float PredatorBiomass01;

        /// <summary>
        /// Normalized kelp/flora overgrowth pressure derived from local prey depletion.
        /// </summary>
        public float FloraOvergrowth01;
    }

    /// <summary>
    /// Allocation-free fauna genome mutation request passed through the ecosystem service boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FaunaGenomeMutationRequest
    {
        public Vector3 RuntimePosition;
        public ulong Genome;
        public uint StableEntityHash;
        public int SpeciesId;
        public uint RollIndex;
        public ushort Slot;
        public byte Flags;
        public byte ResultFlags;
        public float RadiationRads;
        public float Toxicity01;
        public float BrineDepth01;
    }

    /// <summary>
    /// Flags for <see cref="FaunaGenomeMutationRequest"/>.
    /// </summary>
    public static class FaunaGenomeMutationRequestFlags
    {
        public const byte LoadedEntity = 1 << 0;
        public const byte MacroSwarm = 1 << 1;
        public const byte LowTierMacroSkipped = 1 << 2;
    }

    /// <summary>
    /// Data-vault resident ambient biota state. Velocity lives in BufferID.BiotaVelocities;
    /// AUP truth lives in BufferID.BiotaAUPs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct AmbientBiotaState
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagLowTierBillboard = 1u << 1;
        public const uint FlagMacroHydrated = 1u << 2;
        public const uint FlagSdfEmergence = 1u << 3;
        public const uint FlagHighTierReactive = 1u << 4;
        public const uint ReservedDebrisPending = 1u << 0;
        public const uint ReservedFaultSanitized = 1u << 1;

        [FieldOffset(0)] public uint StateFlags;
        [FieldOffset(4)] public uint StableHash;
        [FieldOffset(8)] public ushort SpeciesId;
        [FieldOffset(10)] public ushort BucketId;
        [FieldOffset(12)] public float AgeSeconds;
        [FieldOffset(16)] public float LifetimeSeconds;
        [FieldOffset(20)] public float ScaleMeters;
        [FieldOffset(24)] public float Emission01;
        [FieldOffset(28)] public uint Reserved;
    }

    /// <summary>
    /// Fixed-size ambient-biota black-box sample. Stored in BufferID.BiotaTelemetryRing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct AmbientBiotaTelemetryEntry
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public uint FrameIndex;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public ushort ActiveCount;
        [FieldOffset(58)] public ushort CulledCount;
        [FieldOffset(60)] public ushort Capacity;
        [FieldOffset(62)] public ushort Flags;
    }

    /// <summary>
    /// Registry-facing ambient biota director. It owns no per-biota GameObjects;
    /// consumers read contiguous vault buffers or poll aggregate counters.
    /// </summary>
    public interface IAmbientBiotaService : ISystem
    {
        bool IsInitialized { get; }
        int Capacity { get; }
        int ActiveBiotaCount { get; }
        float CullRatePerSecond { get; }
        NativeArray<AbsoluteUniversePosition>.ReadOnly BiotaAups { get; }
        NativeArray<float4>.ReadOnly BiotaVelocities { get; }
        NativeArray<AmbientBiotaState>.ReadOnly BiotaStates { get; }

        /// <summary>
        /// Claims inactive SOA slots for macro-swarm biomass entering a hydrated sector.
        /// </summary>
        bool TryHydrateMacroSwarms(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            NativeArray<MacroSwarm> swarms,
            int swarmCount,
            byte qualityTier,
            float systemStress01,
            out int spawnedBoidCount);

        /// <summary>
        /// Releases previously macro-hydrated SOA slots back into a compact biomass count for sector unload.
        /// </summary>
        bool TryPackMacroHydratedBiota(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            out int releasedBoidCount,
            out float biomassValue);
    }

    /// <summary>
    /// Allocation-free global biomass audit sample returned by <see cref="IEcosystemDirectorService"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EcosystemBiomassAuditSample
    {
        public float PreyBiomassSum;
        public float PredatorBiomassSum;
        public float CarryingCapacitySum;
        public int ActiveCellCount;
        public uint Sequence;
        public uint Flags;

        public bool IsFinite =>
            math.isfinite(PreyBiomassSum) &&
            math.isfinite(PredatorBiomassSum) &&
            math.isfinite(CarryingCapacitySum);
    }

    /// <summary>
    /// Sector-level ecosystem population service exposed through <see cref="GlobalRegistry"/>.
    /// </summary>
    public interface IEcosystemDirectorService
    {
        /// <summary>
        /// True once the director initialized its runtime buffers and is ready to answer population queries.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Normalized hostility score representing how aggressively the biome is responding to player ecological damage.
        /// </summary>
        float BiomeHostility01 { get; }

        /// <summary>
        /// Number of abstract macro swarms currently moving biomass through unloaded or data-only sectors.
        /// </summary>
        int ActiveMacroSwarmCount { get; }

        /// <summary>
        /// Resolves the sector population sample for the supplied world position.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position to classify into a 1 km sector.</param>
        /// <param name="sample">Resolved predator/prey population sample for the containing sector.</param>
        /// <returns>True when the sector sample is available.</returns>
        bool TryGetSectorPopulation(Vector3 worldPosition, out EcosystemSectorPopulationSample sample);

        /// <summary>
        /// Resolves normalized 50 m biomass availability used by encounter pacing and flora presentation.
        /// </summary>
        bool TryGetBiomassAvailability(Vector3 worldPosition, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01);

        /// <summary>
        /// Mutates a fauna genome against current radiation, toxicity, and brine scalars without exposing ecology storage ownership.
        /// </summary>
        bool TryMutateFaunaGenome(ref FaunaGenomeMutationRequest request);

        /// <summary>
        /// Resolves a global biomass checksum for long-run headless QA without exposing ecology-owned buffers.
        /// </summary>
        bool TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample sample);

        /// <summary>
        /// Copies active macro swarms into caller-owned native storage for save, radar, and diagnostics consumers.
        /// </summary>
        bool TryCopyMacroSwarms(NativeArray<MacroSwarm> destination, out int copiedCount);

        /// <summary>
        /// Projects active macro swarms into radar ping payloads without exposing ecology storage ownership.
        /// </summary>
        bool TryCopyMacroSwarmRadarPings(NativeArray<float4> destination, float3 probeOrigin, float radiusMeters, out int copiedCount);

        /// <summary>
        /// Imports vault-owned macro database payloads into the abstract macro-swarm lane.
        /// </summary>
        bool TryImportMacroSwarmsFromVault(ulong sectorHash, out int importedCount);

        /// <summary>
        /// Claims macro swarms intersecting a hydrated sector into caller-owned native scratch.
        /// </summary>
        bool TryClaimMacroSwarmsForHydration(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            NativeArray<MacroSwarm> destination,
            out int claimedCount,
            out float claimedBiomass01);

        /// <summary>
        /// Converts unloaded active ecology boids back into one abstract macro-swarm.
        /// </summary>
        bool TryRepackHydratedBiotaToMacroSwarm(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            long chunkId,
            int releasedBoidCount,
            ushort flags,
            out float biomassValue);

        /// <summary>
        /// Resolves the deterministic apex-presence flag for the sector containing the supplied world position.
        /// </summary>
        bool IsApexInSector(Vector3 worldPosition);

        /// <summary>
        /// Resolves immediate apex-predator proximity around a runtime-space position without exposing the spatial hash implementation.
        /// </summary>
        bool TryGetApexPredatorThreat(Vector3 worldPosition, float radiusMeters, out float proximity01);

        /// <summary>
        /// Registers prey consumption inside the containing sector so the next cold-tick solve includes the loss.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position where predation occurred.</param>
        /// <param name="preyConsumed">Number of prey removed from the sector population.</param>
        void ReportPredation(Vector3 worldPosition, int preyConsumed);

        /// <summary>
        /// Registers one player-attributed apex predator kill and escalates the biome hostility response.
        /// </summary>
        /// <param name="worldPosition">Runtime-space world position where the apex predator was killed.</param>
        /// <param name="hostilityDelta">Hostility increase applied before clamping.</param>
        void ReportApexPredatorKilled(Vector3 worldPosition, float hostilityDelta);

        /// <summary>
        /// Opens or clears the eclipse predator migration window that suppresses shallow light aversion.
        /// </summary>
        /// <param name="intensity01">Normalized predator migration pressure.</param>
        /// <param name="holdSeconds">Seconds to keep the migration window active.</param>
        void ApplyEclipsePredatorShallowMigration(float intensity01, float holdSeconds);

        /// <summary>
        /// Applies AUP-independent campaign toxicity as a cold-path biomass pressure fake.
        /// </summary>
        void ApplyCampaignToxicityPressure(float toxicity01, uint stageHash, uint frame);

        /// <summary>
        /// Returns normalized eclipse suppression applied to predator light reactions at the supplied position.
        /// </summary>
        float ResolveEclipsePredatorLightSuppression01(Vector3 worldPosition);
    }

    /// <summary>
    /// Deterministic owner-local component resolution helper used to remove runtime hierarchy search APIs.
    /// </summary>
    public static class ComponentReferenceUtility
    {
        /// <summary>
        /// Resolves the first matching component on the supplied owner or its children.
        /// </summary>
        public static T ResolveOwnedComponent<T>(Component owner) where T : Component
        {
            return owner != null ? ResolveOwnedComponent<T>(owner.transform) : null;
        }

        /// <summary>
        /// Resolves the first matching component on the supplied transform or its children.
        /// </summary>
        public static T ResolveOwnedComponent<T>(Transform root) where T : Component
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out T component))
                return component;

            for (int i = 0; i < root.childCount; i++)
            {
                component = ResolveOwnedComponent<T>(root.GetChild(i));
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}
