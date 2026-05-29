using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Interaction;
using Hecton8.SaveSystem;
using Hecton8.Construction;
using Hecton8.Building;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Crafting;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

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
    [StructLayout(LayoutKind.Explicit, Size = 48)]
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
            _pad0 = 0;
        }

        [FieldOffset(0)]
        public readonly double3 UniverseVelocity;

        [FieldOffset(24)]
        public readonly double PlanetDistanceMeters;

        [FieldOffset(32)]
        public readonly float ReentryHeat01;

        [FieldOffset(36)]
        public readonly float CloudWhiteout01;

        [FieldOffset(40)]
        public readonly uint Sequence;

        [FieldOffset(44)]
        public readonly byte MathLod;

        [FieldOffset(45)]
        public readonly byte Flags;

        [FieldOffset(46)]
        private readonly ushort _pad0;
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
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct StreamingHlodImpostorPoint
    {
        [FieldOffset(0)]
        public float3 Center;

        [FieldOffset(12)]
        public float3 Size;

        [FieldOffset(24)]
        public long ChunkId;

        [FieldOffset(32)]
        public int ImpostorType;

        [FieldOffset(36)]
        public float SpawnTimeSeconds;

        [FieldOffset(40)]
        public float Fade01;

        [FieldOffset(44)]
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
        bool TryGetActiveImpostors(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly impostorTypes, out int count);
        bool TryGetActiveImpostorPoints(out NativeArray<StreamingHlodImpostorPoint>.ReadOnly points, out int count);
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
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct DamagePacket
    {
        /// <summary>
        /// Packet channel resolved by the emitting owner.
        /// </summary>
        [FieldOffset(0)] public DamageChannel Channel;
        [FieldOffset(1)] private byte _pad0;
        [FieldOffset(2)] private byte _pad1;
        [FieldOffset(3)] private byte _pad2;

        /// <summary>
        /// Previous normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        [FieldOffset(4)] public float PreviousValue;

        /// <summary>
        /// Next normalized channel value when the packet represents a continuous channel delta.
        /// </summary>
        [FieldOffset(8)] public float NextValue;

        /// <summary>
        /// Primary physical magnitude associated with the event.
        /// Integrity and clarity send normalized magnitudes, hull breaches send pressure delta.
        /// </summary>
        [FieldOffset(12)] public float Magnitude;

        /// <summary>
        /// Local-space point relative to the emitting owner.
        /// </summary>
        [FieldOffset(16)] public float3 LocalPoint;

        /// <summary>
        /// Damage-type bitmask authored by the emitter.
        /// </summary>
        [FieldOffset(28)] public uint DamageType;

        /// <summary>
        /// Quantized integrity delta used by structural diffusion consumers.
        /// </summary>
        [FieldOffset(32)] public byte IntegrityDelta;
        [FieldOffset(33)] private byte _pad3;
        [FieldOffset(34)] private byte _pad4;
        [FieldOffset(35)] private byte _pad5;

        /// <summary>
        /// Depth in meters associated with the damage event when relevant.
        /// </summary>
        [FieldOffset(36)] public float Depth;

        /// <summary>
        /// Stable emitter-local source identifier.
        /// </summary>
        [FieldOffset(40)] public ushort SourceId;

        /// <summary>
        /// Encoded trauma threshold when <see cref="Channel"/> is <see cref="DamageChannel.Trauma"/>.
        /// </summary>
        [FieldOffset(42)] public byte TraumaLevel;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
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
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CurrentMeta
    {
        /// <summary>
        /// Base world-space current vector before local modifiers.
        /// </summary>
        [FieldOffset(0)]
        public float3 GlobalBaseVector;

        /// <summary>
        /// Scalar applied to the base vector.
        /// </summary>
        [FieldOffset(12)]
        public float GlobalScale;

        /// <summary>
        /// Thermocline / halocline response strength.
        /// </summary>
        [FieldOffset(16)]
        public float ThermalIntensity;

        /// <summary>
        /// Monotonic weather-side time accumulator for wave phase evolution.
        /// </summary>
        [FieldOffset(20)]
        public float TimeAccumulator;

        [FieldOffset(24)]
        private ulong _pad0;
    }

    /// <summary>
    /// Blittable Gerstner-wave component consumed by Burst jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GerstnerWaveComponent
    {
        /// <summary>
        /// Normalized XZ travel direction.
        /// </summary>
        [FieldOffset(0)]
        public float2 DirectionXZ;

        /// <summary>
        /// Vertical amplitude in meters.
        /// </summary>
        [FieldOffset(8)]
        public float Amplitude;

        /// <summary>
        /// Wavelength in meters.
        /// </summary>
        [FieldOffset(12)]
        public float Wavelength;

        /// <summary>
        /// Horizontal-displacement factor.
        /// </summary>
        [FieldOffset(16)]
        public float Steepness;

        /// <summary>
        /// Authoring-time phase offset in radians.
        /// </summary>
        [FieldOffset(20)]
        public float PhaseOffset;

        /// <summary>
        /// Speed multiplier applied to the analytic phase velocity.
        /// </summary>
        [FieldOffset(24)]
        public float SpeedMultiplier;

        [FieldOffset(28)]
        private byte _pad0;
        [FieldOffset(29)]
        private byte _pad1;
        [FieldOffset(30)]
        private byte _pad2;
        [FieldOffset(31)]
        private byte _pad3;
    }

    /// <summary>
    /// Shared metadata for the global data-vault Gerstner spectrum buffer.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct OceanGerstnerWaveBufferMeta
    {
        [FieldOffset(0)]
        public int ActiveWaveCount;
        [FieldOffset(4)]
        public float TimeSeconds;
        [FieldOffset(8)]
        public int SleepCount;
        [FieldOffset(12)]
        public int Version;
    }

    /// <summary>
    /// Zero-allocation weather snapshot consumed by physics and VFX systems.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct WeatherRuntimeSnapshot
    {
        /// <summary>
        /// Active weather-state flags for this frame.
        /// </summary>
        [FieldOffset(0)]
        public WeatherState StateMask;

        /// <summary>
        /// Normalized storm/current intensity after active weather-state blending.
        /// </summary>
        [FieldOffset(4)]
        public float WeatherIntensity;

        /// <summary>
        /// Resolved world-space global current vector.
        /// </summary>
        [FieldOffset(8)]
        public float3 GlobalCurrentVector;

        /// <summary>
        /// Resolved world-space global wind vector.
        /// </summary>
        [FieldOffset(20)]
        public float3 GlobalWindVector;

        /// <summary>
        /// Shared metadata for current-driven consumers.
        /// </summary>
        [FieldOffset(32)]
        public CurrentMeta CurrentMeta;

        /// <summary>
        /// First wave component in the weather-driven fallback spectrum.
        /// </summary>
        [FieldOffset(64)]
        public GerstnerWaveComponent Wave0;

        /// <summary>
        /// Second wave component in the weather-driven fallback spectrum.
        /// </summary>
        [FieldOffset(96)]
        public GerstnerWaveComponent Wave1;

        /// <summary>
        /// Third wave component in the weather-driven fallback spectrum.
        /// </summary>
        [FieldOffset(128)]
        public GerstnerWaveComponent Wave2;

        [FieldOffset(160)]
        private ulong _pad0;

        [FieldOffset(168)]
        private ulong _pad1;

        [FieldOffset(176)]
        private ulong _pad2;

        [FieldOffset(184)]
        private ulong _pad3;
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
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct CelestialRuntimeSnapshot
    {
        /// <summary>Authoritative Absolute Universe Time used for the analytical orbit solve.</summary>
        [FieldOffset(0)] public double AbsoluteUniverseTime;

        /// <summary>Observer-to-sun direction in runtime space.</summary>
        [FieldOffset(8)] public float3 SunDirection;

        /// <summary>AUP-safe presentation offset for the gas giant relative to the observer.</summary>
        [FieldOffset(20)] public float3 GasGiantOffset;

        /// <summary>AUP-safe presentation offset for the first moon relative to the observer.</summary>
        [FieldOffset(32)] public float3 Moon0Offset;

        /// <summary>AUP-safe presentation offset for the second moon relative to the observer.</summary>
        [FieldOffset(44)] public float3 Moon1Offset;

        /// <summary>Normalized observer-to-gas-giant direction.</summary>
        [FieldOffset(56)] public float3 GasGiantDirection;

        /// <summary>Normalized observer-to-first-moon direction.</summary>
        [FieldOffset(68)] public float3 Moon0Direction;

        /// <summary>Normalized observer-to-second-moon direction.</summary>
        [FieldOffset(80)] public float3 Moon1Direction;

        /// <summary>Normalized dominant tide pull direction.</summary>
        [FieldOffset(92)] public float3 TidePullVector;

        /// <summary>Signed sea-level offset in meters resolved from the current celestial pull.</summary>
        [FieldOffset(104)] public float TideHeightMeters;

        /// <summary>Normalized high-tide state. 0 is lowest tide, 1 is highest tide.</summary>
        [FieldOffset(108)] public float TideHigh01;

        /// <summary>First moon visual fullness, used by lunar phase materials.</summary>
        [FieldOffset(112)] public float Moon0Phase01;

        /// <summary>Second moon visual fullness, used by lunar phase materials.</summary>
        [FieldOffset(116)] public float Moon1Phase01;

        /// <summary>Gas giant visual fullness.</summary>
        [FieldOffset(120)] public float GasGiantPhase01;

        /// <summary>Current eclipse occlusion factor.</summary>
        [FieldOffset(124)] public float EclipseOcclusion01;

        /// <summary>Current radiation-storm intensity sourced from the global event lane.</summary>
        [FieldOffset(128)] public float RadiationStorm01;

        /// <summary>Global bioluminescence multiplier resolved from full-moon and resonance states.</summary>
        [FieldOffset(132)] public float GlobalBiolumMultiplier;

        /// <summary>Bitmask of <see cref="CelestialRuntimeFlags"/>.</summary>
        [FieldOffset(136)] public uint Flags;

        /// <summary>Monotonic sequence used by frame caches to detect celestial tide updates.</summary>
        [FieldOffset(140)] public uint Sequence;
    }

    /// <summary>
    /// Blittable GI relay state published for watchdogs, diagnostics, and low-cost consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct GIRelayRuntimeSnapshot
    {
        [FieldOffset(0)] public double AbsoluteUniverseTime;
        [FieldOffset(8)] public float TimeOfDay01;
        [FieldOffset(12)] public float DepthMeters;
        [FieldOffset(16)] public float Depth01;
        [FieldOffset(20)] public float EclipseScalar;
        [FieldOffset(24)] public float MoonPhase01;
        [FieldOffset(28)] public float FogLod;
        [FieldOffset(32)] public float LightningScalar;
        [FieldOffset(36)] public int ShadowCascadeLevel;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint Sequence;
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
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct SeismicRuntimeSnapshot
    {
        [FieldOffset(0)] public double AbsoluteUniverseTime;
        [FieldOffset(8)] public float3 SeismicDirection;
        [FieldOffset(20)] public float SeismicIntensity01;
        [FieldOffset(24)] public float TideHeightMeters;
        [FieldOffset(28)] public float TideHigh01;
        [FieldOffset(32)] public float CameraJitter01;
        [FieldOffset(36)] public float AudioRumble01;
        [FieldOffset(40)] public float ThermalEruptionProbabilityScalar;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] private uint _pad0;
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
        /// <param name="maxAgeSeconds">Maximum valid input age converted to the deterministic 60 Hz frame window. Values below zero use the full ten-frame ring.</param>
        /// <returns>True when a valid buffered action was consumed.</returns>
        bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds);

        /// <summary>
        /// Checks whether a raw button bit was present in the deterministic ten-frame mask window.
        /// </summary>
        bool CheckBufferedInput(uint buttonBit, int frames);

        /// <summary>
        /// Reads the contextual input block mask staged by UI/gameplay systems.
        /// </summary>
        uint GetInputBlockMask();

        /// <summary>
        /// Writes the contextual input block mask without coupling UI state to movement code.
        /// </summary>
        void SetInputBlockMask(uint mask);

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
    /// Cold object-pool facade for systems that spawn/despawn without binding to the pool owner class.
    /// </summary>
    public interface IObjectPoolService : ISystem
    {
        void Warmup(GameObject prefab, int count);

        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);

        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool allowExpand);

        Awaitable<bool> WarmupPrefabAsync(GameObject prefab, int count, double frameBudgetMilliseconds, System.Threading.CancellationToken cancellationToken);

        void Despawn(GameObject instance);

        void Despawn(GameObject instance, float delaySeconds);

        bool CanDespawnWithoutDestroy(GameObject instance);

        bool HasPool(GameObject prefab);

        int GetAvailableCount(GameObject prefab);

        bool TryGetAvailableCountForPooledInstance(GameObject instance, out int availableCount);

        bool TryGetPooledRootRenderer(GameObject instance, out Renderer renderer);

        bool TryGetPooledRootRigidbody(GameObject instance, out Rigidbody rigidbody);

        bool TryGetPooledComponent<T>(GameObject instance, out T component) where T : class;

        void TrimInactivePoolsForMemoryPressure(float releaseFraction);

        void FlushInactivePoolsForMemoryPressure();
    }

    /// <summary>
    /// Read-only player look-query cache route. Consumers cache this once and do not bind to the physics cache owner.
    /// </summary>
    public interface IPlayerLookQueryCache : ISystem
    {
        /// <summary>
        /// Reads a cached player-look hit for the current dispatcher frame.
        /// </summary>
        bool TryGetHit(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            out InteractionSurfaceHit hit);

        /// <summary>
        /// Stores a player-look hit for the current dispatcher frame.
        /// </summary>
        void SetHit(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            InteractionSurfaceHit hit);
    }

    /// <summary>
    /// Query telemetry counters exposed without binding diagnostics to physics implementation classes.
    /// </summary>
    public interface IPhysicsQueryTelemetryReadModel : ISystem
    {
        /// <summary>
        /// Number of legacy surface queries processed during the current sampling window.
        /// </summary>
        int LegacySurfaceQueriesProcessed { get; }

        /// <summary>
        /// Number of player-look query cache hits during the current sampling window.
        /// </summary>
        int PlayerLookQueryCacheHits { get; }

        /// <summary>
        /// Clears query telemetry counters after a diagnostics sample has consumed them.
        /// </summary>
        void ResetPhysicsQueryTelemetryCounters();
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
        /// Queues a cinematic breach vortex through the physics owner.
        /// </summary>
        bool QueueDepressurizationVortex(
            Vector3 roomCenter,
            Vector3 breachPosition,
            float radiusMeters,
            float baseAccelerationMetersPerSecondSquared,
            float maximumAccelerationMetersPerSecondSquared,
            float durationSeconds);

        /// <summary>
        /// Applies a cinematic room implosion impulse through the physics owner.
        /// </summary>
        bool QueueImplosionImpulse(
            Vector3 roomCenter,
            float radiusMeters,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds);

        /// <summary>
        /// Queues an authoritative linear velocity assignment through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="linearVelocity">World-space linear velocity.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true);

        /// <summary>
        /// Queues an authoritative angular velocity assignment through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="angularVelocity">World-space angular velocity.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, bool wake = true);

        /// <summary>
        /// Queues an authoritative pose assignment through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="position">World-space target position.</param>
        /// <param name="rotation">World-space target rotation.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueuePoseSet(Rigidbody body, Vector3 position, Quaternion rotation, bool wake = true);

        /// <summary>
        /// Applies a finite kinematic weld snap through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="targetPosition">World-space snapped position.</param>
        /// <param name="targetRotation">World-space snapped rotation.</param>
        /// <returns>True when the snap was accepted.</returns>
        bool ApplyKinematicWeldSnap(Rigidbody body, Vector3 targetPosition, Quaternion targetRotation);

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
        /// Queues an ambient environmental force packet through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueAmbientForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues an ambient environmental force packet at a world-space point through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="worldPosition">World-space application point.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueAmbientForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues an ambient environmental torque packet through the physics owner.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueAmbientTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true);

        /// <summary>
        /// Queues a reduced-mass tractor-beam pull through the physics owner.
        /// </summary>
        /// <param name="anchorBody">Anchor body, or null for world anchor.</param>
        /// <param name="payloadBody">Payload body to pull.</param>
        /// <param name="targetPosition">World-space target point.</param>
        /// <param name="currentPosition">World-space payload point.</param>
        /// <param name="springStiffness">Spring stiffness scalar.</param>
        /// <param name="overDampingMultiplier">Critical damping multiplier.</param>
        /// <param name="maxForceMagnitude">Maximum force magnitude.</param>
        /// <param name="applyReactionForce">True to push back on the anchor body.</param>
        /// <param name="wake">True to wake sleeping bodies before applying.</param>
        /// <returns>True when the packet was accepted.</returns>
        bool QueueTractorBeamPd(
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            Vector3 targetPosition,
            Vector3 currentPosition,
            float springStiffness,
            float overDampingMultiplier,
            float maxForceMagnitude,
            bool applyReactionForce = true,
            bool wake = true);

        /// <summary>
        /// Number of physics-owned late-frame event payloads awaiting dispatch.
        /// </summary>
        int PendingLateFrameEventCount { get; }

        /// <summary>
        /// Flushes physics-owned late-frame event queues through the physics owner.
        /// </summary>
        void FlushLateFrameEvents();

        /// <summary>
        /// Registers a deferred electromagnetic pulse listener through the physics owner route.
        /// </summary>
        void RegisterElectromagneticPulseListener(IElectromagneticPulseEventListener listener);

        /// <summary>
        /// Unregisters a deferred electromagnetic pulse listener through the physics owner route.
        /// </summary>
        void UnregisterElectromagneticPulseListener(IElectromagneticPulseEventListener listener);

        /// <summary>
        /// Clears all queued packets and cached body slots.
        /// </summary>
        void ClearQueuedPackets();

        /// <summary>
        /// Prepares physics-tracked bodies for an origin-shift rebase.
        /// </summary>
        void PrepareTrackedBodiesForOriginShift();

        /// <summary>
        /// Commits a runtime-space shift to physics-tracked bodies.
        /// </summary>
        void CommitTrackedBodiesForOriginShift(Vector3 shiftOffset);

        /// <summary>
        /// Finalizes physics-tracked bodies after an origin-shift rebase.
        /// </summary>
        void FinalizeTrackedBodiesAfterOriginShift();

        /// <summary>
        /// Resets physics-tracked body state before a safe teleport or AUP rebase window.
        /// </summary>
        void ResetTrackedBodiesForSafeTeleportState();

        /// <summary>
        /// Arms speculative CCD safeguards for the safe teleport window.
        /// </summary>
        void ArmSafeTeleportSpeculativeCcd();
    }

    /// <summary>
    /// Physics-state event route for cross-domain systems that must report impacts or temporary body connections.
    /// </summary>
    public interface IPhysicsStateEventService : ISystem
    {
        bool IsInitialized { get; }

        void QueueKinematicImpactEvent(
            Rigidbody primaryBody,
            Rigidbody secondaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond);

        void SetHydrodynamicSubmersion(Rigidbody body, float submersionFactor);

        void RegisterBodyStateTracking(Rigidbody body);

        void UnregisterBodyStateTracking(Rigidbody body);

        void ArmSpeculativeCcdForImpulse(Rigidbody body);

        float ResolveSpeculativeHoverHeightMeters(float baseHeightMeters, float timeSeconds);

        void QueueKinematicImpact(
            Rigidbody primaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond,
            Rigidbody secondaryBody = null);

        void RegisterImpactListener(IPhysicsImpactEventListener listener);

        void UnregisterImpactListener(IPhysicsImpactEventListener listener);

        void RegisterDockConnectionOwner(UnityEngine.Object owner, Rigidbody dockedBody);

        void UnregisterDockConnectionOwner(UnityEngine.Object owner);
    }

    /// <summary>
    /// Local component route for systems that need to mirror external docked mass into a vehicle fluid model.
    /// </summary>
    public interface IDockedExternalMassSink
    {
        void SetDockedExternalMassKilograms(float massKg);
    }

    /// <summary>
    /// Blittable gameplay audio request consumed by the central audio service queue.
    /// EventID maps to an authored clip-table slot owned by the audio runtime.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct AudioEvent
    {
        [FieldOffset(0)] public readonly uint EventID;
        [FieldOffset(4)] public readonly Vector3 Position;
        [FieldOffset(16)] public readonly float Volume;
        [FieldOffset(20)] public readonly float Pitch;
        [FieldOffset(24)] private readonly uint _reserved0;
        [FieldOffset(28)] private readonly uint _reserved1;

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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
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
    /// Blittable sonar echo tap bridge shared by player-critical DSP and cockpit radar presentation.
    /// Layout must stay 64 bytes; DSP and compute upload paths consume it directly.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SonarEchoTap
    {
        [FieldOffset(0)] public float DelaySeconds;
        [FieldOffset(4)] public float PreviousDopplerRatio;
        [FieldOffset(8)] public float DopplerRatio;
        [FieldOffset(12)] public float Attenuation;
        [FieldOffset(16)] public float LeftPanDeltaGain;
        [FieldOffset(20)] public float RightPanDeltaGain;
        [FieldOffset(24)] public float LowPassCutoffHz;
        [FieldOffset(28)] public float LowPassB0;
        [FieldOffset(32)] public float LowPassB1;
        [FieldOffset(36)] public float LowPassB2;
        [FieldOffset(40)] public float LowPassA1;
        [FieldOffset(44)] public float LowPassA2;
        [FieldOffset(48)] public int DelaySamples;
        [FieldOffset(52)] public int UseLowPass;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    /// <summary>
    /// Narrow write route into player-critical procedural DSP without exposing the concrete renderer.
    /// </summary>
    public interface IPlayerCriticalAudioSignalSink
    {
        bool QueuePrologueAudioTransition(in AudioTransitionState state);
        bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal);
    }

    /// <summary>
    /// Read-only cockpit sonar tap route for UI radar presentation.
    /// </summary>
    public interface IPlayerCriticalSonarEchoReadModel
    {
        bool TryGetCockpitSonarEchoTaps(
            out NativeArray<SonarEchoTap>.ReadOnly taps,
            out int tapCount,
            out int sequence);
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
        bool TryGetAcousticRadarPayload(out NativeArray<float>.ReadOnly radialIntensityBins, out int radialResolution);

        /// <summary>
        /// Uploads the 360-degree acoustic radar ring payload into a caller-owned texture.
        /// </summary>
        bool TryUploadAcousticRadarPayload(Texture2D destination, out int uploadedSampleCount, out float peakIntensity);

        /// <summary>
        /// Returns the 8x4 acoustic radar grid payload when available.
        /// </summary>
        bool TryGetAcousticRadarGridPayload(
            out NativeArray<float>.ReadOnly energyGrid,
            out int azimuthBins,
            out int elevationBins,
            out GraphicsBuffer gridBuffer);

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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SpatialAudioImpactEmitterSample
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;

        [FieldOffset(48)]
        public float Amplitude;

        [FieldOffset(52)]
        private uint _pad0;

        [FieldOffset(56)]
        private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SpatialAudioActiveEmitterSample
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;

        [FieldOffset(48)]
        public Vector3 Position;

        [FieldOffset(60)]
        public float Amplitude;
    }

    /// <summary>
    /// Read-only acoustic impact emitter route for HUD/radar presentation.
    /// </summary>
    public interface ISpatialAudioImpactEmitterReadModel
    {
        int CopyActiveImpactEmitterSamples(SpatialAudioImpactEmitterSample[] destination);
    }

    /// <summary>
    /// Read-only active world-emitter route for acoustic occlusion and passive hydrophone presentation.
    /// </summary>
    public interface ISpatialAudioWorldEmitterReadModel
    {
        int CopyActiveWorldEmitterSamples(SpatialAudioActiveEmitterSample[] destination);
    }

    /// <summary>
    /// Read-only listener cave state for signal-noise presentation.
    /// </summary>
    public interface ISpatialAudioListenerCaveReadModel
    {
        bool IsListenerInsideCaveVolume { get; }

        float ListenerCaveInterior01 { get; }

        float ListenerSabineRt60Seconds { get; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SpatialAudioBinauralEmitterTelemetry
    {
        [FieldOffset(0)] public Vector3 Position;
        [FieldOffset(12)] public float DistanceMeters;
        [FieldOffset(16)] public float AzimuthRadians;
        [FieldOffset(20)] public float RightDot;
        [FieldOffset(24)] public float ItdSeconds;
        [FieldOffset(28)] public float ShadowAmount01;
        [FieldOffset(32)] public float ShadowCutoffHertz;
        [FieldOffset(36)] public float Energy;
        [FieldOffset(40)] public float WaterDensityMul;
        [FieldOffset(44)] public int Valid;
    }

    public interface ISpatialAudioBinauralEmitterReadModel
    {
        bool TryGetDominantBinauralEmitter(out SpatialAudioBinauralEmitterTelemetry telemetry);
    }

    /// <summary>
    /// Narrow meteor-boom playback route for random-event presentation.
    /// </summary>
    public interface IMeteorShowerAudioSink
    {
        void PlayMeteorShowerBoom(Vector3 position, float intensity01, float lowPassCutoffHz);
    }

    public interface ISpatialAudioLowPassPlayback
    {
        void PlayAtPointWithLowPass(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            float lowPassCutoffHz);
    }

    public interface ISpatialAudioEnvironmentModulationSink
    {
        float EclipseAcousticPitchShiftCents { get; }

        float EclipseAcousticPitchRatio { get; }

        void SetParasiteRoomAcousticLoad(int parasiteCount);

        void SetEclipseAcousticPitchShiftCents(float shiftCents);
    }

    public interface ISpatialAudioSfxMixerRouteReadModel
    {
        AudioMixerGroup SfxGroup { get; }
    }

    public interface ISpatialAudioNarrativeRadioSink
    {
        bool TryPlayStatic2DBitCrushed(AudioClip clip, float volume);

        void SetNarrativeRadioInterference(float interference01);
    }

    public interface ISpatialAudioInventoryRunawaySink
    {
        void QueueInventoryRunawayExplosion(Vector3 runtimePosition, float volume01);
    }

    public interface ISpatialAudioHarvestPlaybackSink
    {
        void PlayHarvestAtAup(in AbsoluteUniversePosition positionAup, AudioClip clip, float volume = 1f, float pitch = 1f);

        void PlaySporeEmissionAtAup(
            in AbsoluteUniversePosition positionAup,
            AudioClip clip,
            float pulseFrequencyHz,
            float simulationTimeSeconds,
            float phaseOffset01,
            float volume = 1f);
    }

    public interface ISpatialAudioWeatherPlaybackSink
    {
        void PlayWeatherAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup);
    }

    public static class AudioResidencyDomainIds
    {
        public const byte Music = 0;
        public const byte Player = 1;
        public const byte Creatures = 2;
        public const byte Environment = 3;
        public const byte Interface = 4;
    }

    /// <summary>
    /// Cold audio clip residency surface for tools that need explicit prewarm/release without depending on the audio runtime type.
    /// </summary>
    public interface IAudioResidencyService
    {
        void TouchClip(AudioClip clip, byte residencyDomain, bool decodeNow);
        void PrewarmAudioSource(AudioSource source, byte residencyDomain);
        void ReleaseAudioSource(AudioSource source);
        void ReleaseClip(AudioClip clip);
    }

    /// <summary>
    /// Tool-facing acoustic cue surface for one-shot feedback without depending on the audio runtime owner type.
    /// </summary>
    public interface IToolAcousticCueService
    {
        void PlayMantaMisfire(float intensity01);
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
        /// Attempts to enqueue one warning ID into the fixed-priority VWS signal lane.
        /// </summary>
        /// <param name="warningId">Byte ID from <see cref="VocalWarningId"/>.</param>
        /// <param name="severity01">Normalized warning severity.</param>
        /// <param name="cooldownSeconds">Per-warning cooldown override; non-positive uses the runtime fallback.</param>
        /// <param name="flags">Bitmask from <see cref="VocalWarningSignalFlags"/>.</param>
        /// <param name="sourceId">Optional source entity or event hash.</param>
        /// <returns>True when the warning signal was accepted by the unmanaged signal lane.</returns>
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
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public readonly struct VRSomaticChestSocketPose
    {
        [FieldOffset(0)] public readonly AbsoluteUniversePosition SocketAup;
        [FieldOffset(48)] public readonly Vector3 RuntimePosition;
        [FieldOffset(60)] public readonly Quaternion RuntimeRotation;
        [FieldOffset(76)] private readonly uint _pad0;

        public VRSomaticChestSocketPose(
            AbsoluteUniversePosition socketAup,
            Vector3 runtimePosition,
            Quaternion runtimeRotation)
        {
            SocketAup = socketAup;
            RuntimePosition = runtimePosition;
            RuntimeRotation = runtimeRotation;
            _pad0 = 0u;
        }
    }

    /// <summary>
    /// Immutable near-field head contact state emitted by the VR somatic provider.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct VRSomaticCollisionState
    {
        [FieldOffset(0)] public readonly byte HasContactFlag;
        [FieldOffset(1)] private readonly byte _reserved0;
        [FieldOffset(2)] private readonly byte _reserved1;
        [FieldOffset(3)] private readonly byte _reserved2;
        [FieldOffset(4)] private readonly int _pad0;
        [FieldOffset(8)] public readonly AbsoluteUniversePosition ContactAup;
        [FieldOffset(56)] public readonly Vector3 RuntimePoint;
        [FieldOffset(68)] public readonly Vector3 RuntimeNormal;
        [FieldOffset(80)] public readonly float DistanceMeters;
        [FieldOffset(84)] public readonly float Intensity01;
        [FieldOffset(88)] public readonly float ImpactSpeedMetersPerSecond;
        [FieldOffset(92)] private readonly int _pad1;

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
            _reserved0 = 0;
            _reserved1 = 0;
            _reserved2 = 0;
            _pad0 = 0;
            ContactAup = contactAup;
            RuntimePoint = runtimePoint;
            RuntimeNormal = runtimeNormal;
            DistanceMeters = distanceMeters;
            Intensity01 = intensity01;
            ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond;
            _pad1 = 0;
        }

        public bool HasContact => HasContactFlag != 0;
    }

    /// <summary>
    /// Immutable frame snapshot for VR somatic suit systems.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 120)]
    public readonly struct VRSomaticSnapshot
    {
        [FieldOffset(0)] public readonly byte IsActiveFlag;
        [FieldOffset(1)] private readonly byte _reserved0;
        [FieldOffset(2)] private readonly byte _reserved1;
        [FieldOffset(3)] private readonly byte _reserved2;
        [FieldOffset(4)] private readonly int _pad0;
        [FieldOffset(8)] public readonly AbsoluteUniversePosition HeadAup;
        [FieldOffset(56)] public readonly Vector3 HeadRuntimePosition;
        [FieldOffset(68)] public readonly Quaternion HeadRuntimeRotation;
        [FieldOffset(84)] public readonly Quaternion VisorHudWorldRotation;
        [FieldOffset(100)] public readonly float PlayerStress01;
        [FieldOffset(104)] public readonly float Oxygen01;
        [FieldOffset(108)] public readonly float DepthMeters;
        [FieldOffset(112)] public readonly float NearFieldCollision01;
        [FieldOffset(116)] public readonly float Condensation01;

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
            _reserved0 = 0;
            _reserved1 = 0;
            _reserved2 = 0;
            _pad0 = 0;
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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct VRSomaticHandPose
    {
        [FieldOffset(0)] public readonly byte HandIndex;
        [FieldOffset(1)] public readonly byte HasTrackingFlag;
        [FieldOffset(2)] public readonly byte GhostVisibleFlag;
        [FieldOffset(3)] public readonly byte Reserved;
        [FieldOffset(4)] public readonly Vector3 TargetRuntimePosition;
        [FieldOffset(16)] public readonly Vector3 PhysicalRuntimePosition;
        [FieldOffset(28)] public readonly float SeparationMetersSq;

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
    [StructLayout(LayoutKind.Explicit, Size = 88)]
    public struct NarrativeSpatialTriggerAuthoring
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float RadiusSq;
        [FieldOffset(56)] public uint PoiHash;
        [FieldOffset(60)] public uint QuestHash;
        [FieldOffset(64)] public uint BiomeHash;
        [FieldOffset(68)] public uint SoundscapeHash;
        [FieldOffset(72)] public uint LoreHash;
        [FieldOffset(76)] public int BitIndex;
        [FieldOffset(80)] public NarrativeSpatialTriggerFlags Flags;
        [FieldOffset(81)] private byte _reserved0;
        [FieldOffset(82)] private byte _reserved1;
        [FieldOffset(83)] private byte _reserved2;
        [FieldOffset(84)] private uint _pad0;
    }

    /// <summary>
    /// Blittable player pose snapshot for systems that need player AUP and view direction without concrete player-runtime access.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct PlayerRuntimePoseSnapshot
    {
        [FieldOffset(0)] public float3 RuntimePosition;
        [FieldOffset(12)] public float3 Forward;
        [FieldOffset(24)] public AbsoluteUniversePosition Aup;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] private uint _pad0;

        public PlayerRuntimePoseSnapshot(float3 runtimePosition, float3 forward, AbsoluteUniversePosition aup, uint flags)
        {
            RuntimePosition = runtimePosition;
            Forward = forward;
            Aup = aup;
            Flags = flags;
            _pad0 = 0u;
        }
    }

    /// <summary>
    /// Command/read facade for cutter salvage tension owned by the player movement runtime.
    /// </summary>
    public interface IPlayerCuttingTensionService
    {
        bool TryApplyCuttingTensionAnchor(float3 anchorPointWS, float3 anchorNormalWS);

        void ClearCuttingTensionAnchor();

        bool TryReadCuttingTensionNormalized(out float tension01);
    }

    /// <summary>
    /// Command facade for the sargassum cut-mask owner.
    /// </summary>
    public interface ISargassumCutWriteService
    {
        bool TryRegisterExternalCut(Vector3 positionWS, float radiusWS, float strength01, Vector3 directionWS, float bubbleWeight);
    }

    /// <summary>
    /// Command facade for indirect organic harvest/destruction hits owned by the world flora runtime.
    /// </summary>
    public interface IOrganicToolHitService
    {
        bool TryApplyOrganicToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask);

        bool TryApplyAttachedFloraToolHit(
            Vector3 hitPoint,
            float searchRadius,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask);

        bool TryResolveNearestHarvestInteractionPoint(
            Vector3 handRuntimePosition,
            float searchRadius,
            uint toolCapabilityMask,
            out FloraHarvestInteractionPoint interactionPoint);
    }

    /// <summary>
    /// Command facade for localized water-heat presentation/thermal anomaly owners.
    /// </summary>
    public interface IWaterHeatInjectionService
    {
        bool TryInjectLocalizedWaterHeat(Vector3 runtimePoint, Vector3 direction, float cutStrength, float normalizedPower);
    }

    /// <summary>
    /// Narrow damage-interrupt route for delayed player actions without exposing the concrete action owner.
    /// </summary>
    public interface IPlayerActionInterruptSink
    {
        bool IsActionInProgress { get; }

        void OnDamageTaken();
    }

    /// <summary>
    /// Read/command facade for player expression identity UI without exposing the concrete manager.
    /// </summary>
    public interface IPlayerExpressionReadModel
    {
        int ProfileCount { get; }

        bool TryGetNextProfileDisplayName(out string displayName);

        string GetActiveProfileName();

        string GetActiveProfileSummary();

        string GetActiveRecommendedLoadoutName();

        string GetActiveRecommendedSuitName();

        string GetLiveSuitName();

        bool IsActiveRecommendedSuitApplied();

        bool CycleNextProfile(bool applyRecommendedLoadout);
    }

    /// <summary>
    /// Read facade for prefab-bound player tool metadata without binding UI to the concrete tool base class.
    /// </summary>
    public interface IPlayerToolDataReadModel
    {
        ItemData ToolData { get; }

        ToolMetadata Metadata { get; }
    }

    /// <summary>
    /// Narrow command route for emergency systems that must pin the player motor without binding to the motor owner.
    /// </summary>
    public interface IPlayerSeatLockMotorSink
    {
        bool HasControllableBody { get; }

        void MoveSeatLockPosition(Vector3 position);

        void SetSeatLockLinearVelocity(Vector3 velocity);
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
        /// Cached dry-zone/air-state owner resolved from the current player root.
        /// </summary>
        IBuoyancyAirStateReadModel PlayerBuoyancyAirState { get; }

        /// <summary>
        /// Command/read facade for laser-cutter heavy salvage tension owned by player movement.
        /// </summary>
        IPlayerCuttingTensionService CuttingTensionService { get; }

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
        /// Narrow active-transport lifecycle resolver exposed without binding consumers to the coordinator implementation.
        /// </summary>
        IPlayerTransportLifecycleResolver PlayerTransportLifecycleResolver { get; }

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

        /// <summary>
        /// Reads the latest owner-published movement snapshot without synchronizing scene state.
        /// </summary>
        bool TryGetMovementRuntimeState(out PlayerMovementRuntimeState state);

        /// <summary>
        /// Reads the latest owner-published look snapshot without synchronizing scene state.
        /// </summary>
        bool TryGetLookRuntimeState(out PlayerLookState state);

        /// <summary>
        /// Reads the latest owner-published movement stress snapshot without exposing the concrete movement owner.
        /// </summary>
        bool TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState state);

        /// <summary>
        /// Reads the latest owner-published survival snapshot without synchronizing scene state.
        /// </summary>
        bool TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState state);
    }

    /// <summary>
    /// Compact survival-owned environment scalars for scanner and presentation consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PlayerSurvivalEnvironmentSnapshot
    {
        [FieldOffset(0)] public float EnvironmentTemperatureCelsius;
        [FieldOffset(4)] public float DepthMeters;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] private uint _pad0;
    }

    /// <summary>
    /// Read-only survival environment scalar route that does not expose the survival concrete owner.
    /// </summary>
    public interface IPlayerSurvivalEnvironmentReadModel
    {
        bool TryGetSurvivalEnvironmentSnapshot(out PlayerSurvivalEnvironmentSnapshot snapshot);
    }

    /// <summary>
    /// Read-only bleeding signal route for fauna sensory consumers.
    /// </summary>
    public interface IPlayerBleedingReadModel
    {
        bool IsBleeding { get; }
        float BleedingSeverity01 { get; }
    }

    /// <summary>
    /// Narrow presentation command route for player hypoxia distortion.
    /// </summary>
    public interface IPlayerHypoxiaPresentationSink
    {
        void RequestHypoxiaVisorDistortion(float intensity, float holdDuration, float recoverySpeed);
    }

    /// <summary>
    /// Marker route for the player-owned internal achievement registry.
    /// </summary>
    public interface IPlayerAchievementRegistryRuntime
    {
    }

    /// <summary>
    /// Read-only exploration route for systems that need PDA-owned discovered chunk keys.
    /// </summary>
    public interface IPlayerExplorationChunkReadModel
    {
        float ChunkWorldSize { get; }

        int CopyExploredChunks(Vector2Int[] buffer);

        int CopyExploredChunkKeys(long[] buffer);

        bool IsChunkExplored(Vector2Int chunkCoordinates);
    }

    /// <summary>
    /// Narrow save-runtime callback for owners that need to observe mapped inventory writes
    /// without binding the save pipeline to the concrete inventory implementation.
    /// </summary>
    public interface IMappedInventoryWriteCommitSink
    {
        void NotifyMappedInventoryWriteCommitted();
    }

    /// <summary>
    /// Marker route for authored fauna distractors.
    /// </summary>
    public interface IFaunaDistractorSignalSource
    {
    }

    /// <summary>
    /// Read-only bait route for fauna sensory consumers.
    /// </summary>
    public interface IFaunaBaitSource
    {
        bool IsFaunaBait { get; }
    }

    /// <summary>
    /// Read-only fauna contact route for spatial hash consumers.
    /// </summary>
    public interface IFaunaSpatialContact
    {
        int SpeciesId { get; }
        bool IsDead { get; }
        bool IsAggressiveContact { get; }
        bool IsFlockingContact { get; }
        bool HasActiveApexIntimidation { get; }
        bool IsLeviathanContact { get; }
        bool IsApexPredatorContact { get; }
        bool RespondsToParentalDefenseSignal { get; }
        float ApexTerritoryRadiusMeters { get; }
        float ApexTerritoryMassScore { get; }
        uint PreyMaskBits { get; }
        Transform ContactTransform { get; }

        bool TryResolveLogicAup(out AbsoluteUniversePosition selfAup);
        bool CanConsumePrey(uint preyMaskBits);
        bool IsValidPreyFor(IFaunaSpatialContact predatorContact);
        void ApplyParentalDefenseStimulus(Vector3 sourcePosition);
        void TriggerPanicPulse(Vector3 predatorPos);
        void ApplyCleanerSymbiosis(float fatigueRelief);
        Vector3 ResolveContactForward();
        float ResolveApexIntimidationRadiusMeters();
    }

    /// <summary>
    /// Narrow mutable route used only by fauna predation resolution.
    /// Keeps predator logic off the concrete fauna controller type.
    /// </summary>
    public interface IFaunaPredationTarget : IFaunaSpatialContact
    {
        float HealthNormalized { get; }
        bool IsBiolumFlashBangPrey { get; }
        void ApplyPredationDamage(float amount, Vector3 predatorPosition);
        void ForceApexRetreatFrom(Vector3 rivalPosition);
    }

    public interface IFaunaDirectorCueSink : IFaunaSpatialContact
    {
        bool UsesPackHuntBehaviorContact { get; }

        bool ShouldIgnoreAcousticPing(float energyJoules, float intensity01);

        void ApplyAcousticPingAggro(Vector3 sourcePosition, float intensity01, float durationSeconds);

        void ApplyPredatorDeafening(Vector3 sourcePosition, float durationSeconds);

        bool ApplyDirectorColdTickCull(bool enableColdTick);

        void ApplyDirectorLineOfSight(
            bool hasLineOfSight,
            Vector3 playerPosition,
            Vector3 playerForward,
            Vector3 playerVelocity);
    }

    /// <summary>
    /// Read-only chemical influence grid route for scanner and sensory consumers.
    /// </summary>
    public interface IChemicalInfluenceReadModel
    {
        bool TryReadNormalizedChannels(Vector3 runtimePosition, out float4 normalizedChannels);

        bool TryReadAttractantGradient(
            Vector3 runtimePosition,
            float now,
            out float bloodSignal01,
            out float exhaustSignal01,
            out float3 bloodGradient,
            out float3 exhaustGradient);

        bool TryFindNearestBloodWaypoint(
            Vector3 runtimePosition,
            out float distanceMeters,
            out float intensity01);
    }

    /// <summary>
    /// Read-only brine density sampler for corrosion and environmental consumers.
    /// </summary>
    public interface IBrineFluidDensityReadModel
    {
        bool TrySampleBrineFluidDensity(Vector3 runtimePosition, out float fluidDensityKgPerCubicMeter);

        bool TrySampleBrineLayer(Vector3 runtimePosition, out BrineLayerSample sample);
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AtlasSignalReadSnapshot
    {
        public const uint IsDetectedFlag = 1u << 0;
        public const uint HasNavigationFlag = 1u << 1;

        [FieldOffset(0)] public float3 DirectionToCore;
        [FieldOffset(12)] public float Strength01;
        [FieldOffset(16)] public int RevealStage;
        [FieldOffset(20)] public int StrengthBand;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EmergencyRelayRouteTargetSnapshot
    {
        public const uint ActiveFlag = 1u << 0;

        [FieldOffset(0)] public AbsoluteUniversePosition RelayAup;
        [FieldOffset(48)] public uint RelayHash;
        [FieldOffset(52)] public uint ChainHash;
        [FieldOffset(56)] public int RelayOrder;
        [FieldOffset(60)] public uint Flags;
    }

    public interface IAtlasSignalReadModel
    {
        float CurrentAtlasSignalStrength01 { get; }
        int CurrentAtlasSignalRevealStage { get; }
        bool IsAtlasSignalDetected { get; }

        bool TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition coreAup);

        bool TryReadAtlasSignalSnapshot(
            in AbsoluteUniversePosition observerAup,
            out AtlasSignalReadSnapshot snapshot);
    }

    public interface IAtlasSignalDecodeSink : ISystem
    {
        void DecodeSignal(uint messageHash);
    }

    public interface IAtlas6DirectiveCommandSink : ISystem
    {
        void RegisterBarterTransaction();
    }

    public interface IEmergencyRelayRouteReadModel
    {
        bool HasDiscoveredRelayInDrivenChain();

        bool IsRelayDiscoveryHash(uint discoveryHash);

        bool TryBuildContextualGuidanceMessageSpan(out ReadOnlySpan<char> message);

        bool TryReadActiveRouteTarget(out EmergencyRelayRouteTargetSnapshot snapshot);
    }

    public interface INarrativeDiscoveryReadModel
    {
        bool HasDiscovery(uint discoveryHash);
    }

    public interface IFirstHourReadModel
    {
        bool IsFirstHourComplete { get; }
        bool IsFirstHourMilestoneComplete(int milestoneCode);
    }

    public interface IFirstHourRouteContactSink
    {
        void RegisterServiceRelayRouteContact();
    }

    public interface IEndingRuntimeService : ISystem
    {
        bool IsConditionMet { get; }
        bool IsEndingComplete { get; }
        bool CanChooseEnding { get; }

        void ForceConditionMetFromQuestDAG();

        void ChooseEnding(byte endingChoiceCode);
    }

    public interface IAudioLogRuntime
    {
        int DiscoveredAudioLogCount { get; }
        bool IsAudioLogDiscovered(string logId);
        bool IsAudioLogDiscovered(uint logHash);
        bool TryPlayAudioLog(string logId);
        bool TryPlayAudioLogByHash(uint logHash);
        void NotifyAtmosphericWarningStarted(float durationSeconds);
        uint GetRecoveredEncryptedAudioLogBits(uint logHash);
        bool RecoverEncryptedAudioLogFragment(uint logHash, uint fragmentHash);
    }

    public interface ILocalizationTextReadModel
    {
        ushort ActiveLanguageId { get; }
        string GetOrFallback(string key, string fallback);
        string GetFormatted(string key, params object[] args);
        ReadOnlySpan<char> GetRawSpanOrFallback(int keyHash, ReadOnlySpan<char> fallback);
    }

    public interface ILocalizationTextExpansionReadModel : ILocalizationTextReadModel
    {
        bool TryExpandText(ReadOnlySpan<char> text, char[] destination, out int length);
        string GetExpandedOrFallback(ushort languageId, string key, string fallback);
    }

    public interface ILocalizationLanguageControl : ILocalizationTextReadModel
    {
        void CycleLanguage();
    }

    public interface ILocalizationStressPresentationReadModel : ILocalizationTextReadModel
    {
        float GetHullStressCorruptionIntensity();
        int GetHullStressCorruptionBucket();
        bool IsMadnessWhisperVisualActive();
        bool TryApplyHullStressCorruptionIfNeeded(ReadOnlySpan<char> text, char[] destination, out int length);
        bool TryGetHullStressHudWhisperBuffer(ReadOnlySpan<char> fallback, char[] destination, out int length);
    }

    public interface ILocalizationMadnessPresentationReadModel : ILocalizationStressPresentationReadModel
    {
        int ComputeMadnessSourceTokenHash(ReadOnlySpan<char> sourceToken);
        int ComputeMadnessSourceTokenHash(ReadOnlySpan<char> prefix, ReadOnlySpan<char> separator, ReadOnlySpan<char> suffix);
        bool TryApplyPdaLoreCorruptionIfNeeded(int sourceTokenHash, ReadOnlySpan<char> text, char[] destination, out int length);
        bool TryResolveMadnessWhisperPreview(int sourceTokenHash, int cycle, char[] destination, out int length);
    }

    public static class LocalizationMadnessHash
    {
        public static int ComputeSourceTokenHash(ReadOnlySpan<char> sourceToken)
        {
            unchecked
            {
                ReadOnlySpan<char> token = sourceToken.Length == 0 ? "<null>".AsSpan() : sourceToken;
                int hash = 17;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash * 31) + token[i];

                return hash;
            }
        }

        public static int ComputeSourceTokenHash(
            ReadOnlySpan<char> prefix,
            ReadOnlySpan<char> separator,
            ReadOnlySpan<char> suffix)
        {
            unchecked
            {
                int hash = 17;
                AppendTokenHash(prefix, ref hash);
                AppendTokenHash(separator, ref hash);
                AppendTokenHash(suffix, ref hash);
                return hash;
            }
        }

        private static void AppendTokenHash(ReadOnlySpan<char> value, ref int hash)
        {
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
        }
    }

    public interface ILocalizationStressHudRefreshSink
    {
        void RefreshHullStressHudCorruptionVisuals();
    }

    public interface IPdaCorrosionPresentationSink
    {
        void RequestExternalPdaCorrosion(float intensity, float duration);
    }

    public interface ILocalizationTransientOverrideSink
    {
        void SetTransientLanguageOverride(ushort languageId, bool enableGlyphMode = false);
        void ClearTransientLanguageOverride();
    }

    public interface ILoreUnlockReadModel
    {
        bool IsLoreUnlocked(uint logHash);
    }

    public interface ILoreDatabaseReadModel
    {
        int UnlockedCount { get; }

        bool TryGetRecordIndex(uint logHash, out int index);

        bool TryGetPackedUnlockWords(out NativeArray<uint>.ReadOnly words);

        bool TryGetTitleBuffer(uint logHash, out char[] buffer, out int length, out bool rtl);

        bool TryGetBodyBuffer(uint logHash, out char[] buffer, out int length, out bool rtl);
    }

    public interface ILoreUnlockSink
    {
        bool TryUnlockByHash(uint logHash);
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
        /// Authoritative player carry-capacity ceiling used by loadout/readiness consumers.
        /// </summary>
        float CarryCapacityKilograms { get; }

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
        void SetToolActive(uint toolId, bool active);
        void SetToolActive(uint toolId, bool active, float batteryDrainPerSecond);
        bool TryGetPublishedActiveEquipmentState(uint toolId, out ActiveEquipmentDTO state);
        bool TryGetWirelessBrownoutFeedback(uint toolId, out float flickerScalar);
        bool TryGetToolBrownoutFeedback(uint toolId, out float flickerScalar);
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
    /// Read-only hazard sampling contract for consumers that only need immutable exposure scalars.
    /// </summary>
    public interface IHazardZoneReadModel
    {
        /// <summary>
        /// Returns the summed hazard intensity at the supplied absolute-universe point.
        /// </summary>
        float GetHazardIntensity(in AbsoluteUniversePosition pointAup, HazardType type);

        float GetToxicityIntensity(in AbsoluteUniversePosition pointAup);

        bool TrySampleHazardAvoidance(Vector3 runtimePoint, float sampleRadius, out Vector3 fleeDirection, out float hazardPressure01);
    }

    /// <summary>
    /// Read-only buoyancy contact state for consumers that only need shelter/air exposure.
    /// </summary>
    public interface IBuoyancyAirStateReadModel
    {
        /// <summary>True when an owner-local shelter route marks the body as being inside a dry compartment.</summary>
        bool IsInDryZone { get; }

        /// <summary>True when the buoyancy body is not currently water-supported.</summary>
        bool IsInAir { get; }
    }

    /// <summary>
    /// Read-only atmosphere scalars used by physics and AI without depending on the atmosphere owner class.
    /// </summary>
    public interface IAtmosphereReadModel : ISystem
    {
        float CurrentFogAttenuationDistance { get; }

        float CurrentFogDensity { get; }

        float CurrentTemperature { get; }

        float CurrentRadiation { get; }

        float CycleDuration { get; }

        float SeaLevelY { get; }

        bool IsUnderwaterState { get; }
    }

    /// <summary>
    /// Owner-local submarine room atmosphere read model.
    /// Consumers resolve this through an owned component contract, not through the concrete atmosphere MonoBehaviour.
    /// </summary>
    public interface ISubmarineAtmosphereRoomReadModel : ISystem
    {
        bool IsAtmosphereRuntimeActive { get; }

        int RoomCount { get; }

        int RuntimeEntityIdHash { get; }

        float GetRoomPressureKPa(int roomIndex);

        float GetRoomOxygenFraction(int roomIndex);

        float GetRoomCarbonDioxidePressureFraction(int roomIndex);

        float GetRoomTemperatureCelsius(int roomIndex);

        float GetRoomFloodFillRatio(int roomIndex);

        int ResolveNearestRoomIndexForWorldPosition(Vector3 worldPosition);

        float ResolveRoomFloodFillNormalized(int roomIndex);

        bool TryResolveRoomFloodFillNormalized(Vector3 worldPosition, out int roomIndex, out float floodFillNormalized);

        float ResolveExternalDepthMeters();

        float ResolveThermalFatigueMultiplier(int roomIndex);
    }

    /// <summary>
    /// Owner-local submarine room atmosphere mutation sink.
    /// This keeps construction and power systems off the concrete atmosphere owner while preserving single-authority writes.
    /// </summary>
    public interface ISubmarineAtmosphereRoomMutationSink : ISubmarineAtmosphereRoomReadModel
    {
        void InjectOxygenUnits(int roomIndex, float oxygenUnits);

        void InjectRoomTemperatureDeltaCelsius(int roomIndex, float deltaCelsius);

        void InjectRoomHeatEnergyJoules(int roomIndex, float heatEnergyJoules);

        void InjectElectrolysisGasPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa);

        void HandleExternalModuleBreach(Vector3 breachWorldPosition, float breachAreaSquareMeters);
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
        /// Authoritative logistics route for module placement and registry reads.
        /// </summary>
        ILogisticsService Logistics { get; }

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

    public static class SurfaceWeatherKindCodes
    {
        public const byte ClearCalm = 0;
        public const byte ClearBreeze = 1;
        public const byte Overcast = 2;
        public const byte HeavyRain = 3;
        public const byte ElectricalStorm = 4;
    }

    /// <summary>
    /// Read-only surface-weather presentation route without binding consumers to the director owner.
    /// </summary>
    public interface ISurfaceWeatherReadModel : ISystem
    {
        bool IsSurfaceSuppressed { get; }
        bool IsLocallySheltered { get; }
        float CurrentPrecipitationIntensity { get; }
        float CurrentElectricalActivity { get; }
        byte CurrentWeatherKindCode { get; }
    }

    /// <summary>
    /// Read-only acoustic-zone route without binding audio consumers to the concrete zone controller.
    /// </summary>
    public interface IAcousticZoneReadModel : ISystem
    {
        bool IsInterior { get; }
    }

    /// <summary>
    /// Narrow madness-whisper cue sink owned by the acoustic-zone runtime.
    /// </summary>
    public interface IAcousticZoneMadnessCueSink : ISystem
    {
        void PlayMadnessWhisperCue();
    }

    /// <summary>
    /// Contract-only hydrothermal flow sample. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermodynamicFlowSampleDTO
    {
        [FieldOffset(0)] public float3 FlowVelocityWS;
        [FieldOffset(12)] public float Heat01;
        [FieldOffset(16)] public float DragMultiplier;
        [FieldOffset(20)] public float3 CableAnchorWS;
        [FieldOffset(32)] public float CableTension01;
        [FieldOffset(36)] public float CableCutProgress01;
        [FieldOffset(40)] public float CableEscapeSuppression01;
        [FieldOffset(44)] public byte HasFlow;
        [FieldOffset(45)] public byte IsCableZone;
        [FieldOffset(46)] private ushort _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
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
        bool SampleThermalFlow(Vector3 positionWS, float radiusWS, out ThermodynamicFlowSampleDTO sample);

        /// <summary>
        /// Samples the latest Celsius heat field without allocating.
        /// </summary>
        bool TrySampleTemperatureCelsius(Vector3 positionWS, out float temperatureCelsius);

        /// <summary>
        /// Exposes the front-buffer coarse thermal map for avoidance/read-only consumers.
        /// </summary>
        bool TryGetThermalMapReadback(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Exposes the front-buffer 32x32x32 Celsius grid for read-only consumers.
        /// </summary>
        bool TryGetThermalGridReadback(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out Vector3 originWS,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Exposes the front-buffer Celsius grid with an owner-local absolute-universe origin.
        /// </summary>
        bool TryGetThermalGridReadbackAup(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out double3 originAup,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Acquires the front-buffer Celsius grid for an asynchronous consumer. Caller must release after its read job finishes.
        /// </summary>
        bool TryAcquireThermalGridReadbackAup(
            out NativeArray<float>.ReadOnly temperatureCelsius,
            out int width,
            out int height,
            out int depth,
            out double3 originAup,
            out float cellSizeMeters,
            out int version);

        /// <summary>
        /// Releases a readback acquired through <see cref="TryAcquireThermalGridReadbackAup"/>.
        /// </summary>
        void ReleaseThermalGridReadback();

        /// <summary>
        /// Injects a transient heat source without exposing thermodynamics internals.
        /// </summary>
        bool TryInjectTransientHeatSource(Vector3 positionWS, float radiusWS, float heatIntensity, uint sourceId);

        bool TryResolveApexMigrationThermalAttractor(out Vector3 attractorPosition, out float strength01);
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
        /// Total number of cached BaseModule entries tracked by the logistics owner.
        /// </summary>
        int SpawnedBaseModuleCount { get; }

        /// <summary>
        /// Authoritative buildable catalog used for module restoration and placement.
        /// </summary>
        ModuleCatalog Catalog { get; }

        /// <summary>
        /// Read-only live module registry.
        /// </summary>
        IReadOnlyList<GameObject> SpawnedModules { get; }

        /// <summary>
        /// Indexed cached BaseModule access without forcing consumers to scan components.
        /// </summary>
        BaseModule GetSpawnedBaseModuleAt(int index);

        /// <summary>
        /// Registers a runtime module with the logistics graph owner.
        /// </summary>
        void RegisterModule(GameObject module);

        /// <summary>
        /// Registers a runtime module with authored buildable metadata.
        /// </summary>
        void RegisterModule(GameObject module, BuildableData data);

        /// <summary>
        /// Clears the active placed-module registry through the logistics owner.
        /// </summary>
        void ClearAllModules();

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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
            _reserved0 = 0;
            _reserved1 = 0;
            _reserved2 = 0;
        }

        [FieldOffset(0)] public readonly int RoomId;
        [FieldOffset(4)] public readonly float Fill01;
        [FieldOffset(8)] public readonly float SurfaceY;
        [FieldOffset(12)] public readonly float FloorY;
        [FieldOffset(16)] public readonly float CeilingY;
        [FieldOffset(20)] public readonly float WaterVolumeM3;
        [FieldOffset(24)] public readonly uint Sequence;
        [FieldOffset(28)] public readonly byte Flags;
        [FieldOffset(29)] private readonly byte _reserved0;
        [FieldOffset(30)] private readonly byte _reserved1;
        [FieldOffset(31)] private readonly byte _reserved2;

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

        bool TryGetHabitatAcousticGraph(out HabitatGraphManager graph);
    }

    /// <summary>
    /// Narrow habitat parasite graph route owned by construction logistics.
    /// </summary>
    public interface IConstructionParasiteGraphService : ISystem
    {
        bool TryResolveFungalMindTarget(
            BaseModule sourceModule,
            out BaseModule targetModule,
            out float targetPotential);

        void NotifyModuleParasiteRootStateChanged(BaseModule module);
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

    public interface IFaunaWorldSeedReadModel : ISystem
    {
        int WorldSeed { get; }
    }

    public interface IResourceScarcityReadModel : ISystem
    {
        int RuntimeVersion { get; }

        float GetCraftPowerMultiplier(RecipeData recipe);

        float GetIngredientMultiplier(int itemHashId);

        int ResolveInflatedIngredientAmount(
            int itemHashId,
            int baseAmount,
            in AbsoluteUniversePosition worldPosition,
            int accessibleUnits);
    }

    public interface IPersistentDroppedItemRegistry : ISystem
    {
        bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition);

        bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse);

        bool TryRegisterDroppedItem(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            Vector3 initialImpulse,
            Vector3 inheritedVelocityChange);

        bool TryRegisterDroppedItemWithState(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            ulong geneticsMask,
            ushort qualityMilli);

        bool TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition);
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
        /// Activates the authored quest by stable quest hash.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        void ActivateQuest(uint questHash);

        /// <summary>
        /// Completes the authored quest when it exists in the registry.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        void CompleteQuest(string questId);

        /// <summary>
        /// Completes the authored quest by stable quest hash.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        void CompleteQuest(uint questHash);

        /// <summary>
        /// Returns true when the quest is currently active.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsActive(string questId);

        /// <summary>
        /// Returns true when the quest hash is currently active.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        bool IsActive(uint questHash);

        /// <summary>
        /// Returns true when the quest is currently completed.
        /// </summary>
        /// <param name="questId">Stable quest identifier.</param>
        bool IsCompleted(string questId);

        /// <summary>
        /// Returns true when the quest hash is currently completed.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        bool IsCompleted(uint questHash);

        /// <summary>
        /// Returns true when the native quest flag bit is set for the supplied stable flag hash.
        /// </summary>
        /// <param name="flagId">Stable quest flag hash.</param>
        bool GetFlag(uint flagId);

        /// <summary>
        /// Updates quest graph depth conditions from the depth-zone owner route.
        /// </summary>
        /// <param name="depthMeters">Current player depth in meters.</param>
        /// <param name="zoneHash">Stable active depth-zone hash, or 0 when no authored zone is active.</param>
        /// <param name="isThermalZone">True when the current authored depth zone is thermal.</param>
        void UpdateDepthContext(float depthMeters, uint zoneHash, bool isThermalZone);

        /// <summary>
        /// Resolves the authored quest identifier from a stable quest hash.
        /// </summary>
        /// <param name="questHash">Stable quest hash.</param>
        /// <param name="questId">Resolved authored quest identifier.</param>
        /// <returns>True when the hash maps to an authored quest.</returns>
        bool TryGetQuestIdByHash(uint questHash, out string questId);

        bool TryCopyQuestPresentation(
            uint questHash,
            char[] titleDestination,
            out int titleLength,
            char[] descriptionDestination,
            out int descriptionLength,
            out uint markerTargetHash,
            out Vector3 markerWorldPosition,
            out float markerHeightOffset);

        bool UpsertProceduralDirective(
            uint questHash,
            uint completionItemHash,
            string title,
            string description,
            uint markerTargetHash,
            Vector3 markerWorldPosition,
            float markerHeightOffset,
            byte phaseGateCode,
            float requiredQuantity,
            bool activateWhenAllowed,
            out bool activatedNow);
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
    [StructLayout(LayoutKind.Explicit, Size = 40)]
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
            _pad0 = 0;
            _pad1 = 0u;
        }

        [FieldOffset(0)] public readonly int RoomId;
        [FieldOffset(4)] public readonly float OxygenKPa;
        [FieldOffset(8)] public readonly float CarbonDioxideKPa;
        [FieldOffset(12)] public readonly float NitrogenKPa;
        [FieldOffset(16)] public readonly float PressureKPa;
        [FieldOffset(20)] public readonly float AmbientPressureKPa;
        [FieldOffset(24)] public readonly float Toxicity01;
        [FieldOffset(28)] public readonly float Narcosis01;
        [FieldOffset(32)] public readonly ushort Flags;
        [FieldOffset(34)] private readonly ushort _pad0;
        [FieldOffset(36)] private readonly uint _pad1;
    }

    /// <summary>
    /// Cold-path hibernation snapshot for one habitat/base atmosphere island.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 88)]
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
            _centerAup = centerAup;
            _hibernatedUnscaledTime = hibernatedUnscaledTime;
            _batteryWattSeconds = batteryWattSeconds;
            _idleDrawWatts = idleDrawWatts;
            _leakRatePerSecond = leakRatePerSecond;
            _baseId = baseId;
            _roomStart = roomStart;
            _roomCount = roomCount;
            _awake = awake ? (byte)1 : (byte)0;
            _playerInside = playerInside ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0u;
        }

        [FieldOffset(0)] private readonly AbsoluteUniversePosition _centerAup;
        [FieldOffset(48)] private readonly double _hibernatedUnscaledTime;
        [FieldOffset(56)] private readonly float _batteryWattSeconds;
        [FieldOffset(60)] private readonly float _idleDrawWatts;
        [FieldOffset(64)] private readonly float _leakRatePerSecond;
        [FieldOffset(68)] private readonly int _baseId;
        [FieldOffset(72)] private readonly int _roomStart;
        [FieldOffset(76)] private readonly int _roomCount;
        [FieldOffset(80)] private readonly byte _awake;
        [FieldOffset(81)] private readonly byte _playerInside;
        [FieldOffset(82)] private readonly ushort _pad0;
        [FieldOffset(84)] private readonly uint _pad1;

        public int BaseId => _baseId;
        public int RoomStart => _roomStart;
        public int RoomCount => _roomCount;
        public AbsoluteUniversePosition CenterAup => _centerAup;
        public bool Awake => _awake != 0;
        public bool PlayerInside => _playerInside != 0;
        public float BatteryWattSeconds => _batteryWattSeconds;
        public float IdleDrawWatts => _idleDrawWatts;
        public float LeakRatePerSecond => _leakRatePerSecond;
        public double HibernatedUnscaledTime => _hibernatedUnscaledTime;
    }

    /// <summary>
    /// Unmanaged gas-to-physiology signal emitted when CO2 toxicity or nitrogen narcosis crosses a scalar threshold.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
            _pad0 = 0;
            _pad1 = 0u;
        }

        [FieldOffset(0)] public readonly int RoomId;
        [FieldOffset(4)] public readonly float CarbonDioxideKPa;
        [FieldOffset(8)] public readonly float PressureAtm;
        [FieldOffset(12)] public readonly float Toxicity01;
        [FieldOffset(16)] public readonly float Narcosis01;
        [FieldOffset(20)] public readonly uint FrameIndex;
        [FieldOffset(24)] public readonly ushort Flags;
        [FieldOffset(26)] private readonly ushort _pad0;
        [FieldOffset(28)] private readonly uint _pad1;
    }

    /// <summary>
    /// Cold-path audit snapshot for the Dalton gas solver's persistent native memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
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
            _pad0 = 0u;
        }

        [FieldOffset(0)] public readonly long LocalRegisteredBytes;
        [FieldOffset(8)] public readonly long LargestAllocationBytes;
        [FieldOffset(16)] public readonly long SentinelTrackedBytes;
        [FieldOffset(24)] public readonly int RoomCapacity;
        [FieldOffset(28)] public readonly int BulkheadCapacity;
        [FieldOffset(32)] public readonly int LocalAllocationCount;
        [FieldOffset(36)] public readonly int SentinelActiveAllocationCount;
        [FieldOffset(40)] public readonly uint LargestAllocationLabelHash;
        [FieldOffset(44)] private readonly uint _pad0;
    }

    /// <summary>
    /// Registry-owned Dalton gas solver. Callers push local room facts; the solver owns native gas arrays and physiology signals.
    /// </summary>
    public interface IGasDynamicsSolver : ISystem
    {
        bool IsInitialized { get; }
        int RoomCount { get; }
        int BaseCount { get; }

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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
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
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
        }

        /// <summary>Detected graphics memory in megabytes.</summary>
        [FieldOffset(0)] public readonly int GraphicsMemoryMegabytes;

        /// <summary>Detected system memory in megabytes.</summary>
        [FieldOffset(4)] public readonly int SystemMemoryMegabytes;

        /// <summary>Detected logical CPU core count.</summary>
        [FieldOffset(8)] public readonly int ProcessorCount;

        /// <summary>Resolved runtime quality tier.</summary>
        [FieldOffset(12)] public readonly HectonQualityTier QualityTier;
        [FieldOffset(13)] private readonly byte _pad0;
        [FieldOffset(14)] private readonly ushort _pad1;

        /// <summary>Cold BIOS local-physics benchmark cost in milliseconds per 0.02s step.</summary>
        [FieldOffset(16)] public readonly double PhysicsBenchmarkMillisecondsPerStep;

        /// <summary>Deterministic 0-100 BIOS hardware score captured at boot.</summary>
        [FieldOffset(24)] public readonly int HardwareScore;

        /// <summary>BIOS-selected math precision level for runtime shader/simulation paths.</summary>
        [FieldOffset(28)] public readonly MathPrecisionLevel MathPrecisionLevel;
        [FieldOffset(29)] private readonly byte _pad2;
        [FieldOffset(30)] private readonly ushort _pad3;
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
        public readonly GlobalRegistryServiceSlot ServiceSlot;

        /// <summary>Previous service instance, or null when the slot was empty.</summary>
        public readonly object PreviousService;

        /// <summary>Current service instance, or null when the slot was cleared.</summary>
        public readonly object CurrentService;
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

        void ShowModal(
            string title,
            char[] messageBuffer,
            int messageLength,
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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RegistryEventPayload
    {
        [FieldOffset(0)] public uint PreviousServiceHash;
        [FieldOffset(4)] public uint CurrentServiceHash;
        [FieldOffset(8)] public int ReferenceSlot;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public ushort ServiceSlot;
        [FieldOffset(18)] public ushort EventType;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    /// <summary>
    /// Listener contract for registry service-rebound payloads.
    /// </summary>
    public interface IRegistryEventListener
    {
        void OnRegistryEvent(in RegistryEventPayload payload);
    }

    /// <summary>
    /// Cold dependency route for the SHINOBU 132 cable solver runtime.
    /// Core owners call this interface instead of referencing the physics solver assembly directly.
    /// </summary>
    public interface ICablePhysics132Service : ISystem
    {
        /// <summary>
        /// Validates the unmanaged cable DTO layout used by the solver.
        /// </summary>
        /// <returns>True when the current DTO layout matches the solver contract.</returns>
        bool ValidateLayout();

        /// <summary>
        /// Checks whether the deterministic mock cable buffers already exist in the vault.
        /// </summary>
        /// <param name="vault">Vault owner that stores cable buffers.</param>
        /// <returns>True when all required buffers are present.</returns>
        bool TryHasMockBuffers(IDataVault vault);

        /// <summary>
        /// Creates or refreshes deterministic mock cable buffers in the owner vault.
        /// </summary>
        /// <param name="vault">Vault owner that stores cable buffers.</param>
        /// <param name="globalQualityWeight">Continuous quality weight used for capacity/fidelity scaling.</param>
        /// <param name="frameIndex">Frame index written to bootstrap telemetry.</param>
        void EnsureMockBuffers(IDataVault vault, float globalQualityWeight, uint frameIndex);

        /// <summary>
        /// Schedules the deterministic mock cable solve from existing vault buffers.
        /// </summary>
        /// <param name="vault">Vault owner that stores cable buffers.</param>
        /// <param name="frameIndex">Frame index for telemetry and signal records.</param>
        /// <param name="fixedDeltaTime">Sanitized fixed delta for the solver step.</param>
        /// <param name="gravity">Gravity vector for the current cable solve.</param>
        /// <param name="abyssalFlow">Authoritative flow vector already sampled by the caller.</param>
        /// <param name="cameraAup">Camera absolute position used for deterministic mock anchoring.</param>
        /// <param name="globalQualityWeight">Continuous quality weight used for Math LOD.</param>
        /// <param name="lastElapsedMicroseconds">Previous solve duration used for black-box telemetry.</param>
        /// <param name="dependency">Input job dependency.</param>
        /// <param name="handle">Scheduled solver handle when the call succeeds.</param>
        /// <returns>True when the schedule was accepted.</returns>
        bool TryScheduleMockFromVault(
            IDataVault vault,
            uint frameIndex,
            float fixedDeltaTime,
            float3 gravity,
            float3 abyssalFlow,
            double3 cameraAup,
            float globalQualityWeight,
            float lastElapsedMicroseconds,
            JobHandle dependency,
            out JobHandle handle);

        /// <summary>
        /// Releases vault buffer pins held by a completed deterministic mock cable solve.
        /// </summary>
        /// <param name="vault">Vault owner that stores cable buffers.</param>
        void ReleaseMockScheduleBufferPins(IDataVault vault);

        /// <summary>
        /// Dumps the latest solver telemetry only when the solver reports a non-finite recovery or constraint fault.
        /// </summary>
        /// <param name="vault">Vault owner that stores cable telemetry.</param>
        /// <returns>True when a fault dump was written.</returns>
        bool TryDumpLatestFault(IDataVault vault);
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
        ChemicalInfluenceRuntime = 173,
        DestructibleOrganicRuntime = 174,
        CablePhysics132Runtime = 175,
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
    /// Thermal vent snapshot consumed by nutrient drift without referencing the persistent-world owner type.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct NutrientThermalVentSnapshotDTO
    {
        [FieldOffset(0)] public long RuntimeKey;
        [FieldOffset(8)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(56)] public float RadiusWS;
        [FieldOffset(60)] public float HeightWS;
        [FieldOffset(64)] public float UpdraftVelocity;
        [FieldOffset(68)] public float HeatIntensity;
        [FieldOffset(72)] public float SmokeDensity;
        [FieldOffset(76)] public float CableRadiusWS;
    }

    /// <summary>
    /// Read-only thermal vent route for consumers that need bounded source snapshots.
    /// </summary>
    public interface INutrientThermalVentReadModel : ISystem
    {
        /// <summary>
        /// Reads the active thermal vent count from the owner snapshot.
        /// </summary>
        int ReadActiveNutrientThermalVentCount();

        /// <summary>
        /// Reads the current vent revision from the owner snapshot.
        /// </summary>
        int ReadActiveNutrientThermalVentRevision();

        /// <summary>
        /// Reads one active thermal vent row into a nutrient-specific DTO.
        /// </summary>
        bool TryGetActiveNutrientThermalVent(int index, out NutrientThermalVentSnapshotDTO record);
    }

    /// <summary>
    /// Read-only abyssal flow volume route for Burst consumers.
    /// </summary>
    public interface IAbyssalFlowVolumeReadModel : ISystem
    {
        /// <summary>
        /// Returns the current read-only abyssal flow volume and ring-buffer metadata.
        /// </summary>
        bool TryGetAbyssalFlowVolumePayload(
            out NativeArray<float3>.ReadOnly flowVolume,
            out Vector3 center,
            out int resolutionXZ,
            out int resolutionY,
            out int ringOffsetX,
            out int ringOffsetY,
            out int ringOffsetZ,
            out float horizontalCellSize,
            out float verticalCellSize,
            out float surfaceY,
            out float depthMeters);

        bool TrySampleAbyssalFlow(Vector3 position, out Vector3 flowVector);
    }

    /// <summary>
    /// Read-only terrain height payload alias exposed without binding consumers to the vegetation bridge owner type.
    /// </summary>
    public readonly ref struct TerrainHeightSamplePayloadDTO
    {
        public TerrainHeightSamplePayloadDTO(
            NativeArray<ushort> heightSamples,
            Vector3 terrainPosition,
            Vector3 terrainSize,
            int heightmapResolution,
            int cacheRevision)
        {
            HeightSamples = heightSamples;
            TerrainPosition = terrainPosition;
            TerrainSize = terrainSize;
            HeightmapResolution = heightmapResolution;
            CacheRevision = cacheRevision;
        }

        public readonly NativeArray<ushort> HeightSamples;
        public readonly Vector3 TerrainPosition;
        public readonly Vector3 TerrainSize;
        public readonly int HeightmapResolution;
        public readonly int CacheRevision;

        public static bool IsValid(in TerrainHeightSamplePayloadDTO payload)
        {
            return payload.HeightSamples.IsCreated &&
                   payload.HeightmapResolution > 1 &&
                   payload.HeightSamples.Length >= payload.HeightmapResolution * payload.HeightmapResolution;
        }
    }

    /// <summary>
    /// Read-only terrain heightmap payload route for physics/fluid jobs.
    /// </summary>
    public interface ITerrainHeightSampleReadModel : ISystem
    {
        bool TryGetActiveTerrainHeightSamplePayload(out TerrainHeightSamplePayloadDTO payload);

        bool TryGetTerrainHeightSamplePayload(float worldX, float worldZ, out TerrainHeightSamplePayloadDTO payload);
    }

    /// <summary>
    /// Read-only GPU abyssal-flow payload for visual consumers without binding to the physics runtime type.
    /// </summary>
    public interface IAbyssalFlowGpuReadModel : ISystem
    {
        int MaxActiveMaelstromCapacity { get; }

        bool TrySampleModAbyssalFlow(Vector3 samplePosition, out float3 flowVector);

        bool TryGetActiveMaelstroms(
            out NativeArray<float4>.ReadOnly maelstroms,
            out int activeCount,
            out Vector4 maelstromMeta);

        bool TryGetGpuAbyssalFlowFieldTexture(
            out Texture flowFieldTexture,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing);

        bool TryGetGpuAbyssalFlowFieldBuffer(
            out GraphicsBuffer flowFieldBuffer,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing);

        bool TryGetDynamicWakeGpuPayload(
            out GraphicsBuffer dynamicWakeBuffer,
            out GraphicsBuffer dynamicWakeVectorBuffer,
            out Vector4 dynamicWakeParams);

        bool TryUploadActiveMaelstroms(GraphicsBuffer destination, int requestedCount);
    }

    public struct FluidAdvectionRenderGraphPayload
    {
        public ComputeShader Compute;
        public int Kernel;
        public int DispatchGroups;
        public GraphicsBuffer SiltRead;
        public GraphicsBuffer SiltWrite;
        public GraphicsBuffer BubbleRead;
        public GraphicsBuffer BubbleWrite;
        public GraphicsBuffer DebrisRead;
        public GraphicsBuffer DebrisWrite;
        public GraphicsBuffer EmptySiltBuffer;
        public GraphicsBuffer EmptyBubbleBuffer;
        public GraphicsBuffer EmptyDebrisBuffer;
        public GraphicsBuffer AbyssalFlowBuffer;
        public GraphicsBuffer EmptyAbyssalFlowBuffer;
        public Texture AbyssalFlowTexture;
        public Texture VoxelSdfTexture;
        public Texture EmptyVoxelSdfTexture;
        public RTHandle AbyssalFlowTextureHandle;
        public RTHandle VoxelSdfTextureHandle;
        public RTHandle EmptyVoxelSdfTextureHandle;
        public Vector4 Counts;
        public Vector4 Params;
        public Vector4 Buoyancy;
        public Vector4 AupShiftDelta;
        public GraphicsBuffer DynamicWakeBuffer;
        public GraphicsBuffer DynamicWakeVectorBuffer;
        public Vector4 DynamicWakeParams;
        public Vector4 AbyssalGridResolution;
        public Vector4 AbyssalFlowCenter;
        public Vector4 AbyssalFlowSpacing;
        public Vector4 AbyssalFlowTextureParams;
        public float AbyssalFlowTextureActive;
        public float AbyssalFlowInterpolationAlpha;
        public Matrix4x4 VoxelSdfWorldToLocal;
        public Vector4 VoxelSdfInvDoubleHalfExtents;
        public Vector4 SdfParams;
    }

    /// <summary>
    /// RenderGraph fluid advection dispatch route exposed without binding presentation to the concrete physics runtime.
    /// </summary>
    public interface IFluidAdvectionRenderGraphDispatchSource : ISystem
    {
        bool TryClaimFluidAdvectionRenderGraphPayload(out FluidAdvectionRenderGraphPayload payload);

        void BindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle abyssalFlowTexture,
            TextureHandle voxelSdfTexture);

        void UnbindFluidAdvectionCompute(
            IComputeCommandBuffer cmd,
            in FluidAdvectionRenderGraphPayload payload,
            TextureHandle emptyTexture);
    }

    /// <summary>
    /// Read-only analytical flow sampler for physics consumers that need a scalar flow vector.
    /// </summary>
    public interface IAnalyticalFlowReadModel : ISystem
    {
        float3 SampleAnalyticalFlow(float3 samplePosition);

        bool TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow>.ReadOnly whirlpools, out int activeCount);
    }

    /// <summary>
    /// Read-only authored/global current sampler exposed without binding consumers to physics CurrentVolume.
    /// </summary>
    public interface IAmbientCurrentReadModel : ISystem
    {
        bool TrySampleCombinedCurrent(Vector3 samplePosition, out Vector3 currentVector);

        bool TrySampleAuthoredCurrent(Vector3 samplePosition, out Vector3 currentVector);
    }

    /// <summary>
    /// Read-only fluid surface/current route for vegetation and presentation systems.
    /// </summary>
    public interface IFluidSurfaceCurrentReadModel : ISystem
    {
        event System.Action CurrentSettingsChanged;

        float WaterLevel { get; }

        float CurrentWaterLevelY { get; }

        Vector3 CurrentVector { get; }

        float CurrentStrength { get; }

        bool EnablePhantomCurrent { get; }

        float PhantomCurrentStrength { get; }

        float CurrentNoiseScale { get; }

        float CurrentTimeScale { get; }

        float CurrentVerticalFactor { get; }
    }

    /// <summary>
    /// Narrow fluid presentation command route for advected bubble bursts.
    /// </summary>
    public interface IFluidBubbleBurstSink : ISystem
    {
        bool TryQueueAdvectedBubbleBurst(Vector3 runtimePosition, int requestedCount, float intensity01);
    }

    /// <summary>
    /// Narrow fluid current write route for the weather owner.
    /// </summary>
    public interface IFluidCurrentWriteSink : ISystem
    {
        void ApplyWeatherCurrent(Vector3 currentVector, float strength);
    }

    /// <summary>
    /// Read-only route for the globally published celestial runtime snapshot.
    /// </summary>
    public interface ICelestialRuntimeSnapshotReadModel : ISystem
    {
        CelestialRuntimeSnapshot RuntimeSnapshot { get; }

        uint RuntimeSnapshotSequence { get; }
    }

    /// <summary>
    /// Read-only celestial sky-direction route for physics/fluid consumers.
    /// </summary>
    public interface ICelestialSkyDirectionReadModel : ISystem
    {
        bool TryGetAegirSkyDirection(out Vector3 direction);
    }

    public interface ICelestialResonanceReadModel : ISystem
    {
        bool IsLunarResonanceActive { get; }
    }

    /// <summary>
    /// Read-only depth-zone route used by spawn and presentation systems.
    /// </summary>
    public interface IDepthZoneReadModel : ISystem
    {
        DepthZoneProfile CurrentZone { get; }
    }

    /// <summary>
    /// Read-only soundscape tier route used by acoustic presentation without binding to the concrete owner.
    /// </summary>
    public interface ISoundscapeTierReadModel : ISystem
    {
        byte CurrentTierCode { get; }
    }

    /// <summary>
    /// Read-only environmental strain route for stress/audio consumers.
    /// </summary>
    public interface IEnvironmentalStrainReadModel : ISystem
    {
        float MicroplasticStrain { get; }
        float GeneralPollution { get; }
    }

    /// <summary>
    /// Narrow industrial-pollution write lane into the environmental strain owner.
    /// </summary>
    public interface IEnvironmentalStrainIndustrialSink : ISystem
    {
        void AccumulateIndustrialStrain(float generalPollutionDelta, float microplasticDelta);
    }

    /// <summary>
    /// Read-only VRAM/RAM pressure gate state exposed without binding bootstrap to the concrete optimization owner.
    /// </summary>
    public interface IVramPressureReadModel : ISystem
    {
        bool HasSample { get; }

        float VramPressureFactor { get; }

        float RamPressureFactor { get; }

        float PressureFactor { get; }

        float BrgLodDistanceScalar { get; }
    }

    /// <summary>
    /// Cold command route for requesting the next late-frame pressure sample without exposing the concrete optimization owner.
    /// </summary>
    public interface IVramPressureSampleSink : ISystem
    {
        void ForceImmediateSampleAndResponse();
    }

    /// <summary>
    /// Narrow UI mip-bias feedback route used by asset dispatch policy.
    /// </summary>
    public interface IVramPressureMipBiasSink : ISystem
    {
        void SetExternalMipPressureResponse(float pressureResponse, long observedVramBytes);
    }

    /// <summary>
    /// Narrow scan-log route for archive writes, scan-lock reads, and signal-source filtering.
    /// </summary>
    public interface IScanLogService : ISystem
    {
        int EntryCount { get; }

        int RecentCount { get; }

        uint ChangeRevision { get; }

        uint SourceId { get; }

        bool ContainsEntry(uint entryHash);

        void ArchiveEntry(string entryId, string title, string category, string summary, bool markRecent = true);
    }

    /// <summary>
    /// Beacon marker snapshot copied from the beacon owner without exposing the owner MonoBehaviour.
    /// Labels remain managed because the current beacon/save/UI path is label-string based; this is not a Vault DTO.
    /// </summary>
    public readonly struct BeaconNetworkSnapshot
    {
        public readonly string Id;
        public readonly string Label;
        public readonly Vector3 Position;
        public readonly AbsoluteUniversePosition PositionAup;
        public readonly Color Color;
        public readonly float LightRange;

        public BeaconNetworkSnapshot(
            string id,
            string label,
            Vector3 position,
            AbsoluteUniversePosition positionAup,
            Color color,
            float lightRange)
        {
            Id = id;
            Label = label;
            Position = position;
            PositionAup = positionAup;
            Color = color;
            LightRange = lightRange;
        }
    }

    /// <summary>
    /// Tool/VFX beacon-network route. Consumers can deploy/read/retract markers without binding to BeaconNetworkSystem.
    /// </summary>
    public interface IBeaconNetworkService : ISystem
    {
        int ActiveCount { get; }

        bool TryDeployBeaconFromTool(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale,
            int maxActive,
            out string label);

        bool TryRetractNearestFromTool(in AbsoluteUniversePosition originAup, out float distance);

        bool TryGetNearestFromTool(in AbsoluteUniversePosition originAup, out BeaconNetworkSnapshot snapshot, out float distance);

        int CopySnapshots(BeaconNetworkSnapshot[] buffer);
    }

    /// <summary>
    /// Read-only VRAM budget counters without binding callers to the concrete monitor owner.
    /// </summary>
    public interface IVramBudgetReadModel : ISystem
    {
        long TextureMemoryBytes { get; }

        long RenderTextureMemoryBytes { get; }

        long TotalVRAMBytes { get; }

        float RenderTextureBudgetUtilization { get; }

        bool IsTextureMemoryOverBudget { get; }

        bool IsRenderTextureMemoryOverBudget { get; }

        bool IsTotalVRAMOverBudget { get; }

        byte PressureStateCode { get; }

        void GetVRAMBreakdown(out long textureMemoryBytes, out long renderTextureMemoryBytes, out long totalVRAMBytes);
    }

    /// <summary>
    /// Cold sample route for consumers that must refresh VRAM counters before pressure decisions.
    /// </summary>
    public interface IVramBudgetSampleSink : ISystem
    {
        void SampleVramCounters();
    }

    public static class VramPressureStateCodes
    {
        public const byte Stable = 0;
        public const byte Warning = 1;
        public const byte Critical = 2;
    }

    public static class AssetPriorityTierCodes
    {
        public const byte Tier0PlayerCritical = 0x00;
        public const byte Tier1Equipped = 0x01;
        public const byte Tier2Proximity = 0x10;
        public const byte Tier3Ambient = 0x20;
        public const byte Tier4MidRange = 0x30;
        public const byte Tier5DistantHlod = 0x40;
        public const byte Tier6Speculative = 0xFF;
    }

    /// <summary>
    /// Narrow pressure/release control route for asset-residency consumers.
    /// </summary>
    public interface IAssetLifecyclePressureSink : ISystem
    {
        long NativeHeapEstimateBytes { get; }

        void SetHeapSanitizerBlindFrameWindow(bool active, float durationSeconds);

        void SetHeapSanitizerVramPanicWindow(bool active, float durationSeconds);

        void ForceDrainPendingReleaseQueue();

        int DrainPendingReleaseQueueBudgeted(int maxCount);

        int EvictLowestPriorityUnusedAssets(int maxCount, byte minimumPriorityCode);

#if UNITY_ADDRESSABLES_EXIST
        bool TryStageExternalAddressableRelease(AsyncOperationHandle handle);

        bool TryReleaseExternalAddressableFault(AsyncOperationHandle handle);
#endif
    }

    /// <summary>
    /// Presentation-only fluid aftermath route. Consumers request visual decals without knowing the world owner type.
    /// </summary>
    public interface IFluidDecalPresentationSink : ISystem
    {
        void RegisterRuptureFluid(Vector3 positionWS, float radiusScale);

        void RegisterPressureSpray(Vector3 positionWS, Vector3 inwardDirectionWS, float intensity01);

        void RegisterWakeSilt(Vector3 positionWS, Vector3 sourceVelocityWS, float intensity01);

        void RegisterWaterSplash(Vector3 positionWS, Vector3 sourceVelocityWS, float intensity01);

        void RegisterSeismicDust(Vector3 positionWS, float radiusScale);

        void RegisterVoxelCaveInDustAup(Vector3 absoluteUniversePosition, Vector3 impulseDirectionWS, float radiusScale);

        void RegisterVoxelCaveInDustAup(double3 absoluteUniversePosition, Vector3 impulseDirectionWS, float radiusScale);
    }

    /// <summary>
    /// Tool durability authority route for UI, equipment, and maintenance consumers.
    /// </summary>
    public interface IToolDurabilityService : ISystem
    {
        float GetDurability(string toolID, float maxDurability);

        float GetDurability(uint itemHashId, float maxDurability);

        bool TryReadDurability(uint itemHashId, float maxDurability, out float durability);

        float GetDurabilityNormalized(string toolID, float maxDurability);

        float GetDurabilityNormalized(uint itemHashId, float maxDurability);

        bool IsBroken(string toolID);

        bool IsBroken(uint itemHashId);

        bool TryReadBroken(uint itemHashId, out bool broken);

        bool IsDegraded(string toolID);

        bool IsDegraded(uint itemHashId);

        void DrainDurability(string toolID, float amount, float maxDurability);

        void DrainDurabilityByTime(string toolID, uint itemHashId, float scaledDeltaTime, float maxDurability);

        bool TryDrainDurabilityByTime(uint itemHashId, float scaledDeltaTime, float maxDurability);

        void RegisterCentralizedEquipmentMirror(string toolID, uint itemHashId, float maxDurability);

        float ResolveCentralizedEquipmentWearMultiplier(uint itemHashId);

        void SetDurabilityNormalizedFromEquipment(string toolID, uint itemHashId, float normalizedDurability, float maxDurability);

        void RepairTool(string toolID, float amount, float maxDurability);

        bool TryRepairTool(uint itemHashId, float amount, float maxDurability);

        void RepairToolFull(string toolID, float maxDurability);

        bool TryRepairToolFull(uint itemHashId, float maxDurability);

        void BreakTool(string toolID);

        bool TryBreakTool(uint itemHashId);

        void ResetDurability(string toolID, float maxDurability);

        bool TryResetDurability(uint itemHashId, float maxDurability);
    }

    /// <summary>
    /// Read-only vegetation threat route used by fauna spawn weighting.
    /// </summary>
    public interface IVegetationThreatReadModel : ISystem
    {
        float GetSpawnWeightModifier(Vector3 position);
    }

    /// <summary>
    /// Vegetation threat pulse sink used by AI without binding to the vegetation owner type.
    /// </summary>
    public interface IVegetationThreatPulseSink : ISystem
    {
        /// <summary>
        /// Records a species-scoped predator fear sector without binding AI callers to the vegetation owner type.
        /// </summary>
        void RegisterPredatorFearNode(int speciesId, Vector3 worldPosition, float normalizedDamage);

        /// <summary>
        /// Applies a temporary generic vegetation threat pulse.
        /// </summary>
        void ApplyExternalThreatPulse(Vector3 position, float radius, float strength, float holdDuration);
    }

    /// <summary>
    /// Read-only biome physics influence route used by fluid/buoyancy jobs.
    /// </summary>
    public interface IBiomePhysicsInfluenceReadModel : ISystem
    {
        bool TrySampleBiomePhysicsInfluence(Vector3 position, out float buoyancyMultiplier);
    }

    /// <summary>
    /// Read-only sargassum drag route used by fluid/buoyancy jobs.
    /// </summary>
    public interface ISargassumDragReadModel : ISystem
    {
        bool SampleInfluence(
            Vector3 positionWS,
            float radius,
            Vector3 movementVelocityWS,
            out float speedMultiplier,
            out float dragMultiplier,
            out float density01);
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
        /// Samples the dominant biome index at runtime-space X/Z without exposing the terrain backend type.
        /// </summary>
        bool TryGetBiomeIndex(float x, float z, out int biomeIndex);

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
    /// Gameplay systems must query this service instead of talking to third-party ocean adapter singletons directly.
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
    /// Registry-facing owner of underwater caustics presentation output.
    /// </summary>
    public interface ICausticsService : ISystem
    {
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InteractionSurfaceHitDTO
    {
        public const uint FlagHit = 1u << 0;

        [FieldOffset(0)] public Vector3 Point;
        [FieldOffset(12)] public Vector3 Normal;
        [FieldOffset(24)] public float Distance;
        [FieldOffset(28)] public int ColliderInstanceId;
        [FieldOffset(32)] public int Layer;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint TargetHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KinematicSurfaceHit
    {
        public const uint FlagHit = 1u << 0;

        [FieldOffset(0)] private Vector3 _point;
        [FieldOffset(12)] private Vector3 _normal;
        [FieldOffset(24)] private float _distance;
        [FieldOffset(28)] public int Layer;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;

        public bool hasHit => (Flags & FlagHit) != 0u;
        public bool HasHit => hasHit;

        public Vector3 point
        {
            readonly get => _point;
            set
            {
                _point = value;
                MarkHit();
            }
        }

        public Vector3 normal
        {
            readonly get => _normal;
            set
            {
                _normal = value;
                MarkHit();
            }
        }

        public float distance
        {
            readonly get => _distance;
            set
            {
                _distance = value;
                MarkHit();
            }
        }

        public static KinematicSurfaceHit FromSurface(Vector3 point, Vector3 normal, float distance, int layer = -1, uint sourceHash = 0u)
        {
            KinematicSurfaceHit hit = default;
            hit._point = point;
            hit._normal = normal;
            hit._distance = distance;
            hit.Layer = layer;
            hit.SourceHash = sourceHash;
            hit.Flags = FlagHit;
            return hit;
        }

        private void MarkHit()
        {
            Flags |= FlagHit;
        }
    }

    public struct InteractionSurfaceHit
    {
        private InteractionSurfaceHitDTO _dto;
        private Collider _collider;

        public bool hasHit => (_dto.Flags & InteractionSurfaceHitDTO.FlagHit) != 0u;
        public bool HasHit => hasHit;

        public Vector3 point
        {
            readonly get => _dto.Point;
            set
            {
                _dto.Point = value;
                MarkHit();
            }
        }

        public Vector3 normal
        {
            readonly get => _dto.Normal;
            set
            {
                _dto.Normal = value;
                MarkHit();
            }
        }

        public float distance
        {
            readonly get => _dto.Distance;
            set
            {
                _dto.Distance = value;
                MarkHit();
            }
        }

        public Collider collider
        {
            readonly get => _collider;
            set
            {
                _collider = value;
                _dto.ColliderInstanceId = ResolveColliderEntityId(value);
                _dto.Layer = value != null ? value.gameObject.layer : _dto.Layer;
                MarkHit();
            }
        }

        public readonly int colliderInstanceId => _dto.ColliderInstanceId;
        public readonly int layer => _dto.Layer;
        public readonly InteractionSurfaceHitDTO Dto => _dto;

        public static InteractionSurfaceHit FromDTO(in InteractionSurfaceHitDTO dto, Collider collider = null)
        {
            return new InteractionSurfaceHit
            {
                _dto = dto,
                _collider = collider
            };
        }

        public static InteractionSurfaceHit FromSurface(Vector3 point, Vector3 normal, float distance, Collider collider = null, int layer = -1)
        {
            InteractionSurfaceHit hit = default;
            hit._dto.Point = point;
            hit._dto.Normal = normal;
            hit._dto.Distance = distance;
            hit._dto.ColliderInstanceId = ResolveColliderEntityId(collider);
            hit._dto.Layer = collider != null ? collider.gameObject.layer : layer;
            hit._dto.Flags = InteractionSurfaceHitDTO.FlagHit;
            hit._collider = collider;
            return hit;
        }

        private void MarkHit()
        {
            _dto.Flags |= InteractionSurfaceHitDTO.FlagHit;
        }

        private static int ResolveColliderEntityId(Collider collider)
        {
            return collider != null ? unchecked((int)UnityEngine.EntityId.ToULong(collider.GetEntityId())) : 0;
        }
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
        /// Requests the shared zero-allocation tool surface query from a preformatted interaction packet and returns the latest completed frame-latent result.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="packet">Blittable tool request packet copied by value into the service-owned surface-query lane.</param>
        /// <param name="layerMask">Layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool RequestPrimarySurfaceHit(ulong requesterId, in Hecton8.Interaction.InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit);

        /// <summary>
        /// Requests the shared zero-allocation tool surface query using the service-owned buffers and returns the latest completed frame-latent result.
        /// </summary>
        /// <param name="requesterId">Stable per-requester identifier used to map frame-latent results.</param>
        /// <param name="origin">Runtime-space query origin.</param>
        /// <param name="direction">Runtime-space query direction.</param>
        /// <param name="range">Maximum query range.</param>
        /// <param name="layerMask">Layer mask.</param>
        /// <param name="queryTriggerInteraction">Whether trigger colliders participate in the batched query.</param>
        /// <param name="hit">Nearest valid hit when one is found.</param>
        /// <returns>True when a valid hit was resolved.</returns>
        bool RequestPrimarySurfaceHit(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit);

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
        /// Current continuously pressure-scaled particle capacity.
        /// </summary>
        int ActiveParticleCapacity { get; }

        /// <summary>
        /// Continuous quality pressure, where 0 is maximum visual budget and 1 is minimum survival budget.
        /// </summary>
        float QualityPressure01 { get; }

        /// <summary>
        /// Clears live GPU debris state without destroying persistent buffers.
        /// </summary>
        void ClearGpuDebris();
    }

    /// <summary>
    /// Immutable ecosystem population sample returned by <see cref="IEcosystemDirectorService"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct EcosystemSectorPopulationSample
    {
        /// <summary>
        /// Quantized 1 km sector coordinate on the X axis.
        /// </summary>
        [FieldOffset(0)] public int SectorX;

        /// <summary>
        /// Quantized 1 km sector coordinate on the Z axis.
        /// </summary>
        [FieldOffset(4)] public int SectorZ;

        /// <summary>
        /// Current prey population carried by the sector simulation.
        /// </summary>
        [FieldOffset(8)] public int PreyPopulation;

        /// <summary>
        /// Current predator population carried by the sector simulation.
        /// </summary>
        [FieldOffset(12)] public int PredatorPopulation;

        /// <summary>
        /// Normalized prey fitness derived from sustained sector stress and survivor adaptation.
        /// </summary>
        [FieldOffset(16)] public float Fitness;

        /// <summary>
        /// Sector-authored prey speed multiplier applied to spawned swarm agents.
        /// </summary>
        [FieldOffset(20)] public float SpeedMultiplier;

        /// <summary>
        /// Sector-authored prey camouflage bias applied to spawned swarm agents.
        /// </summary>
        [FieldOffset(24)] public float CamouflageIndex;

        /// <summary>
        /// Non-zero when the containing sector carries active apex pressure.
        /// </summary>
        [FieldOffset(28)] public byte ApexInSector;
        [FieldOffset(29)] private byte _pad0;
        [FieldOffset(30)] private byte _pad1;
        [FieldOffset(31)] private byte _pad2;

        /// <summary>
        /// Normalized prey biomass in the containing 50 m ecology macro-cell.
        /// </summary>
        [FieldOffset(32)] public float PreyBiomass01;

        /// <summary>
        /// Normalized predator biomass in the containing 50 m ecology macro-cell.
        /// </summary>
        [FieldOffset(36)] public float PredatorBiomass01;

        /// <summary>
        /// Normalized kelp/flora overgrowth pressure derived from local prey depletion.
        /// </summary>
        [FieldOffset(40)] public float FloraOvergrowth01;
        [FieldOffset(44)] private uint _pad3;
    }

    /// <summary>
    /// Allocation-free fauna genome mutation request passed through the ecosystem service boundary.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct FaunaGenomeMutationRequest
    {
        [FieldOffset(0)] public Vector3 RuntimePosition;
        [FieldOffset(12)] private int _pad0;
        [FieldOffset(16)] public ulong Genome;
        [FieldOffset(24)] public uint StableEntityHash;
        [FieldOffset(28)] public int SpeciesId;
        [FieldOffset(32)] public uint RollIndex;
        [FieldOffset(36)] public ushort Slot;
        [FieldOffset(38)] public byte Flags;
        [FieldOffset(39)] public byte ResultFlags;
        [FieldOffset(40)] public float RadiationRads;
        [FieldOffset(44)] public float Toxicity01;
        [FieldOffset(48)] public float BrineDepth01;
        [FieldOffset(52)] private int _pad1;
    }

    /// <summary>
    /// Flags for <see cref="FaunaGenomeMutationRequest"/>.
    /// </summary>
    public static class FaunaGenomeMutationRequestFlags
    {
        public const byte LoadedEntity = 1 << 0;
        public const byte MacroSwarm = 1 << 1;
        public const byte SurvivalPressureMacroSkipped = 1 << 2;
        public const byte LowTierMacroSkipped = SurvivalPressureMacroSkipped;
    }

    /// <summary>
    /// Data-vault resident ambient biota state. Velocity lives in BufferID.BiotaVelocities;
    /// AUP truth lives in BufferID.BiotaAUPs.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AmbientBiotaState
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagSurvivalBillboardPressure = 1u << 1;
        public const uint FlagMacroHydrated = 1u << 2;
        public const uint FlagSdfEmergence = 1u << 3;
        public const uint FlagHeadlightReactive = 1u << 4;
        public const uint FlagLowTierBillboard = FlagSurvivalBillboardPressure;
        public const uint FlagHighTierReactive = FlagHeadlightReactive;
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
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
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
    /// Presentation pulse sink for micro-fauna reactions. Producers stay off the concrete boid owner.
    /// </summary>
    public interface IMicroFaunaPresentationPulseSink : ISystem
    {
        void RegisterLeviathanThreatPulse(Vector3 originWS, Vector3 directionWS, float radiusMeters, float durationSeconds);

        void RegisterPredatorFearBurst(Vector3 originWS, Vector3 directionWS, float radiusMeters, float durationSeconds, float intensity01);

        int RegisterPredatorConsumptionBurst(Vector3 predatorPositionWS, Vector3 biteCenterWS, float biteRangeMeters, uint predatorId, float currentTimeSeconds);

        void RegisterVatHitReaction(Vector3 originWS, float radiusMeters, float intensity01);

        void RegisterAcousticPanicBurst(Vector3 originWS, float radiusMeters, float durationSeconds, float intensity01, uint sourceId);
    }

    /// <summary>
    /// Allocation-free global biomass audit sample returned by <see cref="IEcosystemDirectorService"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct EcosystemBiomassAuditSample
    {
        [FieldOffset(0)] public float PreyBiomassSum;
        [FieldOffset(4)] public float PredatorBiomassSum;
        [FieldOffset(8)] public float CarryingCapacitySum;
        [FieldOffset(12)] public int ActiveCellCount;
        [FieldOffset(16)] public uint Sequence;
        [FieldOffset(20)] public uint Flags;

        public readonly bool IsFinite() =>
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

        void RegisterApexPredatorKill(uint uniqueInstanceUid, Vector3 worldPosition, float hostilityDelta);

        bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition);

        bool TryResolveSpawnWeightMultiplier(
            Hecton8.AI.CreatureArchetypeData archetype,
            Vector3 worldPosition,
            out float selectionMultiplier);

        bool TryConsumeSpawnCredit(
            Hecton8.AI.CreatureArchetypeData archetype,
            bool isLargeThreat,
            bool isPredator);

        void RefundSpawnCredit(
            Hecton8.AI.CreatureArchetypeData archetype,
            bool isLargeThreat,
            bool isPredator);

        bool IsApexTombstoned(uint uniqueInstanceUid);

        bool TryResolveNearestOrganicMass(Vector3 worldPosition, out Vector3 organicPosition);

        bool TryConsumeOrganicMassAtPosition(Vector3 worldPosition, float searchRadius);

        bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target);

        float ScavengerHungerThreshold { get; }

        float ScavengerConsumeDistanceMeters { get; }

        float ScavengerConsumeUnitsPerSecond { get; }

        bool TryResolveCorpseScavengeTarget(
            in AbsoluteUniversePosition queryAup,
            out Vector3 corpsePosition,
            out uint corpseNodeId);

        bool TryConsumeCorpseScavengeTarget(uint corpseNodeId, float consumeUnits);

        bool DoesSpeciesRespondToBait(int speciesId, bool isScavenger, bool isAggressive, bool isLeviathan);

        float BaitFeedingDistanceMeters { get; }

        bool IsHerbivoreSpecies(int speciesId);

        float HerbivoreGrazeHungerThreshold { get; }

        float HerbivoreGrazeSearchRadiusMeters { get; }

        float HerbivoreConsumeDistanceMeters { get; }

        bool TryResolveNearestThermalVentAttractor(
            in AbsoluteUniversePosition queryAup,
            float searchRadiusMeters,
            out Vector3 target,
            out float heat01);

        bool TryResolveHerbivoreGrazeTarget(Vector3 worldPosition, out Vector3 floraPosition, out uint floraInstanceUid);

        bool TryConsumeHerbivoreGrazeTarget(uint floraInstanceUid);

        bool IsCleanerSpecies(int speciesId);

        bool IsCleanerHostSpecies(int speciesId, bool isLeviathan);

        float CleanerHostSearchRadiusMeters { get; }

        float CleanerSymbiosisDistanceMeters { get; }

        float CleanerFatigueReliefPerSecond { get; }

        void PublishBiolumFlashBang(in AbsoluteUniversePosition flashAup, float currentTimeSeconds, float radiusMeters = 42f);

        void RegisterCorpseResourceNode(
            in AbsoluteUniversePosition positionAup,
            int speciesId,
            float capacityUnits,
            uint contaminatedItemHash);

        FaunaLogicalLodTier ResolveLogicalLodTier(
            in AbsoluteUniversePosition observerAup,
            in AbsoluteUniversePosition faunaAup);

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

        /// <summary>
        /// Resolves the first parent-chain component implementing an owner-local service contract.
        /// </summary>
        public static T ResolveParentService<T>(Component owner) where T : class
        {
            return owner != null ? ResolveParentService<T>(owner.transform) : null;
        }

        /// <summary>
        /// Resolves the first parent-chain component implementing an owner-local service contract.
        /// </summary>
        public static T ResolveParentService<T>(Transform root) where T : class
        {
            Transform current = root;
            while (current != null)
            {
                if (current.TryGetComponent(typeof(T), out Component component) &&
                    component is T service)
                {
                    return service;
                }

                current = current.parent;
            }

            return null;
        }
    }
}
