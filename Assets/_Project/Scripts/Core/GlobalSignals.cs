using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory.Layout;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using BiomeChangedSignal = Hecton8.Core.Contracts.Signals.BiomeChangedSignal;
using CameraFrustumSignal = Hecton8.Core.Contracts.Signals.CameraFrustumSignal;
using CameraPositionSignal = Hecton8.Core.Contracts.Signals.CameraPositionSignal;
using CombatDamageSignal = Hecton8.Core.Contracts.Signals.CombatDamageSignal;
using CrashTelemetrySignal = Hecton8.Core.Contracts.Signals.CrashTelemetrySignal;
using DiegeticHudSignal = Hecton8.Core.Contracts.Signals.DiegeticHudSignal;
using FocusBrokenSignal = Hecton8.Core.Contracts.Signals.FocusBrokenSignal;
using MixerStateSignal = Hecton8.Core.Contracts.Signals.MixerStateSignal;
using NarrativeFocusSignal = Hecton8.Core.Contracts.Signals.NarrativeFocusSignal;
using NarrativeHudWaypointSignal = Hecton8.Core.Contracts.Signals.NarrativeHudWaypointSignal;
using NarrativePoiStateSignal = Hecton8.Core.Contracts.Signals.NarrativePoiStateSignal;
using ProgressionEventSignal = Hecton8.Core.Contracts.Signals.ProgressionEventSignal;
using ScanLogChangedSignal = Hecton8.Core.Contracts.Signals.ScanLogChangedSignal;
using SoundscapeProfileSignal = Hecton8.Core.Contracts.Signals.SoundscapeProfileSignal;
using SurvivalVitalsChangedSignal = Hecton8.Core.Contracts.Signals.SurvivalVitalsChangedSignal;
using HullRepairedSignal = Hecton8.Core.Contracts.Signals.HullRepairedSignal;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Marker for unmanaged signal-lane payloads. Implemented only by blittable structs.
    /// </summary>
    [Preserve]
    public interface ISignal
    {
    }

    /// <summary>
    /// Deterministic two-stage initialization contract for registry-pinned systems.
    /// </summary>
    [Preserve]
    public interface IInitializable
    {
        /// <summary>Registers the system with its owning registry without resolving external dependencies.</summary>
        void OnRegister();

        /// <summary>Resolves and caches external dependencies after all systems are registered.</summary>
        void OnDependencyInject();
    }

    /// <summary>
    /// In-place signal snapshot transformer. Used for rare structural passes such as AUP rebases.
    /// </summary>
    /// <typeparam name="T">Signal payload type.</typeparam>
    [Preserve]
    public interface ISignalSnapshotTransformer<T>
        where T : unmanaged, ISignal
    {
        /// <summary>Transforms one signal payload in-place.</summary>
        /// <param name="signal">Signal payload to mutate.</param>
        void Transform(ref T signal);
    }

    /// <summary>
    /// Last flushed state for one typed signal lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SignalLaneTelemetry
    {
        public uint LaneHash;
        public int QueuedBeforeFlush;
        public int SnapshotCount;
        public int DroppedCount;
        public byte Flags;
    }

    /// <summary>
    /// Pre-simulation deterministic input snapshot. Gameplay physics consumes this signal, not hardware APIs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct InputStateSignal : ISignal
    {
        public Hecton8.Core.InputState State;
        public uint CurrentInputSchemeHash;
        public byte InputDelayFrames;
        public byte AppliedDelayFrames;
        public ushort Flags;
    }

    /// <summary>Discrete player input command identifiers for zero-GC UI/gameplay consumers.</summary>
    public static class PlayerInputSignalCommands
    {
        public const byte ToggleInventory = 1;
        public const byte TogglePda = 2;
        public const byte Cancel = 3;
        public const byte TabNext = 4;
        public const byte TabPrevious = 5;
        public const byte Interact = 6;
        public const byte PrimaryAction = 7;
        public const byte SecondaryAction = 8;
        public const byte ToolSlot1 = 9;
        public const byte ToolSlot2 = 10;
        public const byte ToolSlot3 = 11;
        public const byte ToolSlot4 = 12;
        public const byte Flashlight = 13;
    }

    /// <summary>Discrete player input lane for command-style consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerInputSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Command;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player look target state identifiers for diegetic UI consumers.</summary>
    public static class PlayerLookTargetSignalStates
    {
        public const byte Cleared = 0;
        public const byte Acquired = 1;
    }

    /// <summary>Player kinematics look-target lane for diegetic prompts. Hash-only payload. Size: 160 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public struct PlayerLookTargetSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public float3 RuntimeAnchor;
        [FieldOffset(60)] public float DistanceMeters;
        [FieldOffset(64)] public uint TargetHash;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint ColliderHash;
        [FieldOffset(76)] public float3 SurfaceNormal;
        [FieldOffset(88)] public uint PromptHash;
        [FieldOffset(92)] public byte State;
        [FieldOffset(93)] public byte Flags;
        [FieldOffset(94)] private ushort _reserved;
        [FieldOffset(96)] public uint PromptArg0;
        [FieldOffset(100)] public uint PromptArg1;
        [FieldOffset(104)] public uint PromptArg2;
        [FieldOffset(108)] public uint PromptArg3;
    }

    internal interface ISignalLane
    {
        uint LaneHash { get; }
        int QueuedBeforeFlush { get; }
        int SnapshotCount { get; }
        int DroppedLastFlush { get; }
        bool StormDetectedLastFlush { get; }
        void FlushPreSimulation(bool lowTier, int systemStressMilli);
        void ClearPostSimulation();
        void Dispose();
        void CopyTelemetry(ref SignalLaneTelemetry telemetry);
    }

    /// <summary>
    /// Registry for every closed <see cref="SignalBus{T}"/> lane touched this session.
    /// </summary>
    [Preserve]
    public static class SignalBusRegistry
    {
        private const int LaneCapacity = 256;
        private const int StressScale = 1000;

        // COLD ALLOC: ISignalLane[256] - typed signal-lane registry for deterministic pre-simulation flush - owner: SignalBusRegistry
        private static readonly ISignalLane[] _lanes = new ISignalLane[LaneCapacity];
        private static int _laneCount;
        private static int _lowTierMode = 1;
        private static int _registrationOverflow;
        private static int _systemStressMilli;

        /// <summary>Current active typed lane count.</summary>
        public static int LaneCount => _laneCount;

        /// <summary>True when low-tier processing caps are enforced.</summary>
        public static bool LowTierMode => Volatile.Read(ref _lowTierMode) != 0;

        /// <summary>True after any lane failed registration because registry capacity was exhausted.</summary>
        public static bool RegistrationOverflow => Volatile.Read(ref _registrationOverflow) != 0;

        /// <summary>Runtime stress scalar in [0..1], quantized to avoid float tearing.</summary>
        public static float SystemStress01 => math.saturate(Volatile.Read(ref _systemStressMilli) * 0.001f);

        internal static void Register(ISignalLane lane)
        {
            if (lane == null)
                return;

            for (int i = 0; i < _laneCount; i++)
            {
                if (ReferenceEquals(_lanes[i], lane))
                    return;
            }

            if (_laneCount >= LaneCapacity)
            {
                Volatile.Write(ref _registrationOverflow, 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SIGNAL LANE REGISTRY OVERFLOW]");
#endif
                return;
            }

            _lanes[_laneCount++] = lane;
        }

        /// <summary>Sets whether low-tier lane caps are active.</summary>
        /// <param name="enabled">True for MX350/i3 style limits.</param>
        public static void SetLowTierMode(bool enabled)
        {
            Volatile.Write(ref _lowTierMode, enabled ? 1 : 0);
        }

        /// <summary>Sets the runtime stress scalar that controls optional lane propagation.</summary>
        /// <param name="stress01">Stress in [0..1]. Non-finite values clamp to full stress.</param>
        public static void SetSystemStress01(float stress01)
        {
            float sanitized = math.isfinite(stress01) ? math.saturate(stress01) : 1f;
            Volatile.Write(ref _systemStressMilli, (int)math.round(sanitized * StressScale));
        }

        /// <summary>Flushes every active signal queue into contiguous frame snapshots.</summary>
        public static void FlushPreSimulation()
        {
            bool lowTier = LowTierMode;
            int systemStressMilli = Volatile.Read(ref _systemStressMilli);
            int laneCount = _laneCount;
            for (int i = 0; i < laneCount; i++)
                _lanes[i].FlushPreSimulation(lowTier, systemStressMilli);
        }

        /// <summary>Clears every frame snapshot after consumers finish the simulation frame.</summary>
        public static void ClearPostSimulationSnapshots()
        {
            int laneCount = _laneCount;
            for (int i = 0; i < laneCount; i++)
                _lanes[i].ClearPostSimulation();
        }

        /// <summary>Disposes every typed lane. Called on subsystem reset and application quit.</summary>
        public static void DisposeAll()
        {
            int laneCount = _laneCount;
            for (int i = 0; i < laneCount; i++)
            {
                ISignalLane lane = _lanes[i];
                if (lane != null)
                    lane.Dispose();

                _lanes[i] = null;
            }

            _laneCount = 0;
            Volatile.Write(ref _registrationOverflow, 0);
            Volatile.Write(ref _systemStressMilli, 0);
        }

        /// <summary>Copies per-lane telemetry into a caller-owned buffer.</summary>
        /// <param name="destination">Destination buffer.</param>
        /// <returns>Number of copied entries.</returns>
        public static int CopyTelemetry(NativeArray<SignalLaneTelemetry> destination)
        {
            if (!destination.IsCreated || destination.Length == 0)
                return 0;

            int copyCount = Math.Min(_laneCount, destination.Length);
            for (int i = 0; i < copyCount; i++)
            {
                SignalLaneTelemetry telemetry = default;
                _lanes[i].CopyTelemetry(ref telemetry);
                destination[i] = telemetry;
            }

            return copyCount;
        }

        internal static ISignalLane GetLaneAt(int index)
        {
            return index >= 0 && index < _laneCount ? _lanes[index] : null;
        }
    }

    /// <summary>
    /// Typed unmanaged signal lane. Each closed generic type owns a discrete NativeQueue and frame snapshot.
    /// </summary>
    /// <typeparam name="T">Unmanaged signal payload type.</typeparam>
    [Preserve]
    public static class SignalBus<T>
        where T : unmanaged, ISignal
    {
        private const string OwnerLabel = "SignalBus";
        private const int DefaultExpectedCapacity = 64;
        private const int DefaultMaxFrameSignals = 10000;
        private const int DefaultLowTierFrameSignals = 1000;
        private const int LaneOverflowFaultThreshold = 1024;
        private const int HighEndOverkillStressMilli = 200;
        private const int LowTierDropStressMilli = 800;
        private const uint LaneOverflowFaultHash = 0x4C4F5646u; // LOVF
        private const uint NonCriticalVfxKillSwitchMask = 1u << 20;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private static NativeQueue<T> _queue;
        private static NativeList<T> _frameSnapshot;
        private static int _expectedCapacity = DefaultExpectedCapacity;
        private static int _maxFrameSignals = DefaultMaxFrameSignals;
        private static int _lowTierFrameSignals = DefaultLowTierFrameSignals;
        private static int _legacyReadCursor;
        private static int _queuedBeforeFlush;
        private static int _droppedLastFlush;
        private static int _stormDetectedLastFlush;
        private static bool _initialized;
        private static bool _registered;
        private static uint _laneHash;

        // COLD ALLOC: SignalLaneAdapter[1] - typed lane registry bridge - owner: SignalBus<T>
        private static readonly SignalLaneAdapter _laneAdapter = new SignalLaneAdapter();

        /// <summary>Stable lane hash used by telemetry and load-shedding reports.</summary>
        public static uint LaneHash
        {
            get
            {
                EnsureRegistered();
                return _laneHash;
            }
        }

        /// <summary>Current frame snapshot element count.</summary>
        public static int SnapshotCount => _frameSnapshot.IsCreated ? _frameSnapshot.Length : 0;

        /// <summary>Signals dropped during the most recent flush.</summary>
        public static int DroppedLastFlush => _droppedLastFlush;

        /// <summary>Parallel writer for Burst producers.</summary>
        public static NativeQueue<T>.ParallelWriter ParallelWriter
        {
            get
            {
                EnsureInitialized();
                return _queue.AsParallelWriter();
            }
        }

        /// <summary>
        /// Configures lane capacity and telemetry. Call from bootstrap before first push.
        /// </summary>
        public static void Configure(int expectedCapacity, int maxFrameSignals = DefaultMaxFrameSignals, int lowTierFrameSignals = DefaultLowTierFrameSignals, uint laneHash = 0u)
        {
            _expectedCapacity = Math.Max(1, expectedCapacity);
            _maxFrameSignals = Math.Max(1, maxFrameSignals);
            _lowTierFrameSignals = Math.Max(1, Math.Min(lowTierFrameSignals, _maxFrameSignals));
            _laneHash = laneHash != 0u ? laneHash : ComputeTypeHash();
            EnsureRegistered();
        }

        /// <summary>Ensures native storage exists for this lane.</summary>
        public static void EnsureInitialized()
        {
            EnsureRegistered();
            if (_initialized)
                return;

            int snapshotCapacity = SignalBusRegistry.LowTierMode
                ? _lowTierFrameSignals
                : _maxFrameSignals;

            _queue = new NativeQueue<T>(Allocator.Persistent); // COLD ALLOC: NativeQueue<T>[configured capacity] - typed signal queue - owner: SignalBus<T>
            Hecton8.Core.NativeMemorySentinel.RegisterNativeQueue(
                _queue,
                _expectedCapacity,
                OwnerLabel,
                ResolveQueueLabel(),
                Hecton8.Core.NativeAllocationLifetime.Session);

            _frameSnapshot = new NativeList<T>(snapshotCapacity, Allocator.Persistent); // COLD ALLOC: NativeList<T>[tier frame limit] - contiguous signal snapshot - owner: SignalBus<T>
            Hecton8.Core.NativeMemorySentinel.RegisterNativeList(
                _frameSnapshot,
                OwnerLabel,
                ResolveSnapshotLabel(),
                Hecton8.Core.NativeAllocationLifetime.Session);

            PrewarmQueue(_expectedCapacity);
            _legacyReadCursor = 0;
            _queuedBeforeFlush = 0;
            _droppedLastFlush = 0;
            _stormDetectedLastFlush = 0;
            _initialized = true;
        }

        /// <summary>Pushes one signal into this type's queue.</summary>
        /// <param name="signal">Signal payload.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Push(in T signal)
        {
            EnsureInitialized();
            T sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                global::Hecton8.Core.GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _queue.Enqueue(sanitizedSignal);
        }

        /// <summary>Returns a contiguous read-only view over the current frame snapshot.</summary>
        public static unsafe ReadOnlySpan<T> GetFrameSnapshot()
        {
            if (!_frameSnapshot.IsCreated || _frameSnapshot.Length == 0)
                return ReadOnlySpan<T>.Empty;

            T* pointer = _frameSnapshot.GetUnsafeReadOnlyPtr();
            return new ReadOnlySpan<T>(pointer, _frameSnapshot.Length);
        }

        /// <summary>Returns a NativeArray read-only snapshot for Burst jobs.</summary>
        public static NativeArray<T>.ReadOnly GetFrameSnapshotArray()
        {
            if (!_frameSnapshot.IsCreated)
                return default;

            return _frameSnapshot.AsReadOnly();
        }

        /// <summary>Legacy destructive reader over the current frame snapshot.</summary>
        public static bool TryReadFrame(out T signal)
        {
            if (!_frameSnapshot.IsCreated || _legacyReadCursor >= _frameSnapshot.Length)
            {
                signal = default;
                return false;
            }

            signal = _frameSnapshot[_legacyReadCursor++];
            return true;
        }

        /// <summary>Transforms each snapshot payload in-place without boxing.</summary>
        public static void TransformSnapshot<TTransformer>(TTransformer transformer)
            where TTransformer : struct, ISignalSnapshotTransformer<T>
        {
            if (!_frameSnapshot.IsCreated)
                return;

            for (int i = 0; i < _frameSnapshot.Length; i++)
            {
                T signal = _frameSnapshot[i];
                transformer.Transform(ref signal);
                _frameSnapshot[i] = signal;
            }
        }

        internal static NativeQueue<T> GetQueueForLegacyGlobalSignals()
        {
            EnsureInitialized();
            return _queue;
        }

        internal static void FlushPreSimulation(bool lowTier, int systemStressMilli)
        {
            if (!_initialized)
                return;

            _frameSnapshot.Clear();
            _legacyReadCursor = 0;
            _droppedLastFlush = 0;
            _stormDetectedLastFlush = 0;

            int queued = _queue.Count;
            _queuedBeforeFlush = queued;
            bool nonCriticalVfx = SignalLanePolicyCache<T>.NonCriticalVfx;
            int frameLimit = ResolveFrameLimit(lowTier, systemStressMilli, nonCriticalVfx);
            if (_frameSnapshot.Capacity < frameLimit)
                frameLimit = _frameSnapshot.Capacity;

            if (queued > LaneOverflowFaultThreshold)
            {
                _droppedLastFlush = queued;
                _stormDetectedLastFlush = 1;
                ClearQueue();
                global::Hecton8.Core.GlobalTelemetryBus.PublishSystemDegradation(
                    LaneOverflowFaultHash,
                    NonCriticalVfxKillSwitchMask,
                    queued);
                global::Hecton8.Core.GlobalRegistry.SetSystemKillSwitchBits(NonCriticalVfxKillSwitchMask, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[LANE_OVERFLOW_FAULT]");
#endif
                return;
            }

            int overflow = Math.Max(0, queued - frameLimit);
            if (overflow > 0)
            {
                _droppedLastFlush = overflow;
                if (queued > _maxFrameSignals)
                    _stormDetectedLastFlush = 1;

                DropOldest(overflow);
            }

            int copyLimit = Math.Min(_queue.Count, frameLimit);
            for (int i = 0; i < copyLimit; i++)
            {
                if (!_queue.TryDequeue(out T signal))
                    break;

                _frameSnapshot.AddNoResize(signal);
            }
        }

        private static int ResolveFrameLimit(bool lowTier, int systemStressMilli, bool nonCriticalVfx)
        {
            if (nonCriticalVfx && systemStressMilli > LowTierDropStressMilli)
                return 0;

            if (systemStressMilli < HighEndOverkillStressMilli)
                return _maxFrameSignals;

            return lowTier ? _lowTierFrameSignals : _maxFrameSignals;
        }

        private static void DropOldest(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!_queue.TryDequeue(out _))
                    break;
            }
        }

        private static void ClearQueue()
        {
            _queue.Clear();
        }

        internal static void ClearPostSimulation()
        {
            if (!_frameSnapshot.IsCreated)
                return;

            _frameSnapshot.Clear();
            _legacyReadCursor = 0;
        }

        internal static void Dispose()
        {
            if (_queue.IsCreated)
            {
                Hecton8.Core.NativeMemorySentinel.UnregisterNativeQueue(OwnerLabel, ResolveQueueLabel());
                _queue.Dispose();
                _queue = default;
            }

            if (_frameSnapshot.IsCreated)
            {
                Hecton8.Core.NativeMemorySentinel.UnregisterNativeList(OwnerLabel, ResolveSnapshotLabel());
                _frameSnapshot.Dispose();
                _frameSnapshot = default;
            }

            _legacyReadCursor = 0;
            _queuedBeforeFlush = 0;
            _droppedLastFlush = 0;
            _stormDetectedLastFlush = 0;
            _initialized = false;
            _registered = false;
        }

        private static void EnsureRegistered()
        {
            if (_registered)
                return;

            if (_laneHash == 0u)
                _laneHash = ComputeTypeHash();

            SignalBusRegistry.Register(_laneAdapter);
            _registered = true;
        }

        private static void PrewarmQueue(int capacity)
        {
            for (int i = 0; i < capacity; i++)
                _queue.Enqueue(default);

            while (_queue.TryDequeue(out _))
            {
            }
        }

        private static uint ComputeTypeHash()
        {
            string name = typeof(T).FullName;
            if (string.IsNullOrEmpty(name))
                name = typeof(T).Name;

            uint hash = FnvOffset;
            for (int i = 0; i < name.Length; i++)
            {
                hash ^= name[i];
                hash *= FnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static string ResolveQueueLabel()
        {
            return typeof(T).Name + ".Queue";
        }

        private static string ResolveSnapshotLabel()
        {
            return typeof(T).Name + ".FrameSnapshot";
        }

        private sealed class SignalLaneAdapter : ISignalLane
        {
            public uint LaneHash => SignalBus<T>.LaneHash;
            public int QueuedBeforeFlush => _queuedBeforeFlush;
            public int SnapshotCount => SignalBus<T>.SnapshotCount;
            public int DroppedLastFlush => _droppedLastFlush;
            public bool StormDetectedLastFlush => _stormDetectedLastFlush != 0;

            public void FlushPreSimulation(bool lowTier, int systemStressMilli)
            {
                SignalBus<T>.FlushPreSimulation(lowTier, systemStressMilli);
            }

            public void ClearPostSimulation()
            {
                SignalBus<T>.ClearPostSimulation();
            }

            public void Dispose()
            {
                SignalBus<T>.Dispose();
            }

            public void CopyTelemetry(ref SignalLaneTelemetry telemetry)
            {
                telemetry.LaneHash = SignalBus<T>.LaneHash;
                telemetry.QueuedBeforeFlush = _queuedBeforeFlush;
                telemetry.SnapshotCount = SignalBus<T>.SnapshotCount;
                telemetry.DroppedCount = _droppedLastFlush;
                telemetry.Flags = (byte)(_stormDetectedLastFlush != 0 ? 1 : 0);
            }
        }
    }

    internal static class SignalLanePolicyCache<T>
        where T : unmanaged, ISignal
    {
        public static readonly bool NonCriticalVfx = ResolveNonCriticalVfx();

        private static bool ResolveNonCriticalVfx()
        {
            Type type = typeof(T);
            return type == typeof(DebrisSpawnSignal) ||
                   type == typeof(HullDeformedSignal) ||
                   type == typeof(BulletTimeVisualSignal) ||
                   type == typeof(BubbleSpawnSignal) ||
                   type == typeof(ReentryVfxStateSignal) ||
                   type == typeof(VisorDropletSignal) ||
                   type == typeof(VisualFlareSignal) ||
                   type == typeof(StreamingTurbulenceSignal);
        }
    }

    internal static class SignalPayloadFiniteGuards
    {
        private const int ImpactSignalGuardCode = unchecked((int)0x51A10002u);
        private const int HighSpeedImpactSignalGuardCode = unchecked((int)0x51A10003u);
        private const int CombatDamageSignalGuardCode = unchecked((int)0x51A10004u);
        private const int FluidImpulseSignalGuardCode = unchecked((int)0x51A10005u);
        private const int SystemPauseSignalGuardCode = unchecked((int)0x51A10006u);
        private const int WeatherChangedSignalGuardCode = unchecked((int)0x51A10007u);
        private const int TimeDilationSignalGuardCode = unchecked((int)0x51A10008u);
        private const int SimulationPauseSignalGuardCode = unchecked((int)0x51A10009u);
        private const int BulletTimeVisualSignalGuardCode = unchecked((int)0x51A1000Au);
        private const int WeatherStrengthSignalGuardCode = unchecked((int)0x51A1000Bu);
        private const int PlayerLookTargetSignalGuardCode = unchecked((int)0x51A1000Cu);
        private const int PlayerBaseEnterSignalGuardCode = unchecked((int)0x51A1000Du);
        private const int PlayerBaseExitSignalGuardCode = unchecked((int)0x51A1000Eu);
        private const int PlayerStateSignalGuardCode = unchecked((int)0x51A1000Fu);
        private const int SurvivalVitalsChangedSignalGuardCode = unchecked((int)0x51A10010u);
        private const int PlayerActionProgressSignalGuardCode = unchecked((int)0x51A10011u);
        private const int CameraPositionSignalGuardCode = unchecked((int)0x51A10012u);
        private const int CameraFrustumSignalGuardCode = unchecked((int)0x51A10013u);
        private const int HullDeformedSignalGuardCode = unchecked((int)0x51A10014u);
        private const int BaseModuleCompromisedSignalGuardCode = unchecked((int)0x51A10015u);
        private const int AupPreShiftSignalGuardCode = unchecked((int)0x51A10016u);
        private const int AupShiftSignalGuardCode = unchecked((int)0x51A10017u);
        private const int RadiationDoseSignalGuardCode = unchecked((int)0x51A10018u);
        private const int TemperatureChangedSignalGuardCode = unchecked((int)0x51A10019u);
        private const int RadiationSourceSignalGuardCode = unchecked((int)0x51A1001Au);
        private const int CullingOverloadSignalGuardCode = unchecked((int)0x51A1001Bu);
        private const int WakeGeneratedSignalGuardCode = unchecked((int)0x51A1001Cu);
        private const int BiomeGradientSignalGuardCode = unchecked((int)0x51A1001Du);
        private const int MemoryPressureSignalGuardCode = unchecked((int)0x51A1001Eu);
        private const int ResolutionChangedSignalGuardCode = unchecked((int)0x51A1001Fu);
        private const int SystemHealthIndexSignalGuardCode = unchecked((int)0x51A10020u);
        private const int CpuStarvationSignalGuardCode = unchecked((int)0x51A10021u);
        private const int AcousticPingSignalGuardCode = unchecked((int)0x51A10022u);
        private const int FluidIncursionSignalGuardCode = unchecked((int)0x51A10023u);
        private const int SubmarineFloodStateSignalGuardCode = unchecked((int)0x51A10024u);
        private const int FluidDensityChangedSignalGuardCode = unchecked((int)0x51A10025u);
        private const int StreamingTurbulenceSignalGuardCode = unchecked((int)0x51A10026u);
        private const int AtmosphericReentrySignalGuardCode = unchecked((int)0x51A10027u);
        private const int VehicleUpgradesChangedSignalGuardCode = unchecked((int)0x51A10028u);
        private const int SaveLifecycleSignalGuardCode = unchecked((int)0x51A10029u);
        private const int SaveStatusSignalGuardCode = unchecked((int)0x51A1002Au);
        private const int LightLevelSignalGuardCode = unchecked((int)0x51A1002Bu);
        private const int SubmarineLightsChangedSignalGuardCode = unchecked((int)0x51A1002Cu);
        private const int PhysiologyStateSignalGuardCode = unchecked((int)0x51A1002Du);
        private const int PlayerStressSignalGuardCode = unchecked((int)0x51A1002Eu);
        private const int TraumaSignalGuardCode = unchecked((int)0x51A1002Fu);
        private const int ItemDurabilityChangedSignalGuardCode = unchecked((int)0x51A10030u);
        private const int BrownoutSignalGuardCode = unchecked((int)0x51A10031u);
        private const int EntityDeathSignalGuardCode = unchecked((int)0x51A10032u);
        private const int MovementAcousticSignalGuardCode = unchecked((int)0x51A10033u);
        private const int SwarmDispersedSignalGuardCode = unchecked((int)0x51A10034u);
        private const int ScannerToolActiveSignalGuardCode = unchecked((int)0x51A10035u);
        private const int StorageDebtSignalGuardCode = unchecked((int)0x51A10036u);
        private const int PrologueCompleteSignalGuardCode = unchecked((int)0x51A10037u);
        private const int ManualOverridePulledSignalGuardCode = unchecked((int)0x51A10038u);
        private const int WfcOutpostGeneratedSignalGuardCode = unchecked((int)0x51A10039u);
        private const int WfcOutpostDoorPowerSignalGuardCode = unchecked((int)0x51A1003Au);
        private const int HapticRequestGuardCode = unchecked((int)0x51A1003Bu);
        private const int PlayerActionCancelledSignalGuardCode = unchecked((int)0x51A1003Cu);
        private const int DropPodLandedSignalGuardCode = unchecked((int)0x51A1003Du);
        private const int ItemAcquiredSignalGuardCode = unchecked((int)0x51A1003Eu);
        private const int BiomeChangedSignalGuardCode = unchecked((int)0x51A1003Fu);
        private const int SectorResidencyHydratedSignalGuardCode = unchecked((int)0x51A10040u);
        private const int SectorDehydratedSignalGuardCode = unchecked((int)0x51A10041u);
        private const int ChunkDehydratedSignalGuardCode = unchecked((int)0x51A10042u);
        private const int BubbleSpawnSignalGuardCode = unchecked((int)0x51A10043u);
        private const int HullRepairedSignalGuardCode = unchecked((int)0x51A10044u);
        private const int TetherTensionSignalGuardCode = unchecked((int)0x51A10045u);
        private const int TetherSnappedSignalGuardCode = unchecked((int)0x51A10046u);
        private const int VisualFlareSignalGuardCode = unchecked((int)0x51A10047u);
        private const int VoxelCarveEventGuardCode = unchecked((int)0x51A10048u);
        private const int DockingRequestSignalGuardCode = unchecked((int)0x51A10049u);
        private const int DockingCompleteSignalGuardCode = unchecked((int)0x51A1004Au);
        private const int DockingFailedSignalGuardCode = unchecked((int)0x51A1004Bu);
        private const int AnomalyProximitySignalGuardCode = unchecked((int)0x51A1004Cu);
        private const int CompassCalibratedSignalGuardCode = unchecked((int)0x51A1004Du);
        private const int TetherFiredSignalGuardCode = unchecked((int)0x51A1004Eu);
        private const byte GuardNone = 0;
        private const byte GuardImpact = 2;
        private const byte GuardHighSpeedImpact = 3;
        private const byte GuardCombatDamage = 4;
        private const byte GuardFluidImpulse = 5;
        private const byte GuardSystemPause = 6;
        private const byte GuardWeatherChanged = 7;
        private const byte GuardTimeDilation = 8;
        private const byte GuardSimulationPause = 9;
        private const byte GuardBulletTimeVisual = 10;
        private const byte GuardWeatherStrength = 11;
        private const byte GuardPlayerLookTarget = 12;
        private const byte GuardPlayerBaseEnter = 13;
        private const byte GuardPlayerBaseExit = 14;
        private const byte GuardPlayerState = 15;
        private const byte GuardSurvivalVitalsChanged = 16;
        private const byte GuardPlayerActionProgress = 17;
        private const byte GuardCameraPosition = 18;
        private const byte GuardCameraFrustum = 19;
        private const byte GuardHullDeformed = 20;
        private const byte GuardBaseModuleCompromised = 21;
        private const byte GuardAupPreShift = 22;
        private const byte GuardAupShift = 23;
        private const byte GuardRadiationDose = 24;
        private const byte GuardTemperatureChanged = 25;
        private const byte GuardRadiationSource = 26;
        private const byte GuardCullingOverload = 27;
        private const byte GuardWakeGenerated = 28;
        private const byte GuardBiomeGradient = 29;
        private const byte GuardMemoryPressure = 30;
        private const byte GuardResolutionChanged = 31;
        private const byte GuardSystemHealthIndex = 32;
        private const byte GuardCpuStarvation = 33;
        private const byte GuardAcousticPing = 34;
        private const byte GuardFluidIncursion = 35;
        private const byte GuardSubmarineFloodState = 36;
        private const byte GuardFluidDensityChanged = 37;
        private const byte GuardStreamingTurbulence = 38;
        private const byte GuardAtmosphericReentry = 39;
        private const byte GuardVehicleUpgradesChanged = 40;
        private const byte GuardSaveLifecycle = 41;
        private const byte GuardSaveStatus = 42;
        private const byte GuardLightLevel = 43;
        private const byte GuardSubmarineLightsChanged = 44;
        private const byte GuardPhysiologyState = 45;
        private const byte GuardPlayerStress = 46;
        private const byte GuardTrauma = 47;
        private const byte GuardItemDurabilityChanged = 48;
        private const byte GuardBrownout = 49;
        private const byte GuardEntityDeath = 50;
        private const byte GuardMovementAcoustic = 51;
        private const byte GuardSwarmDispersed = 52;
        private const byte GuardScannerToolActive = 53;
        private const byte GuardStorageDebt = 54;
        private const byte GuardPrologueComplete = 55;
        private const byte GuardManualOverridePulled = 56;
        private const byte GuardWfcOutpostGenerated = 57;
        private const byte GuardWfcOutpostDoorPower = 58;
        private const byte GuardHapticRequest = 59;
        private const byte GuardPlayerActionCancelled = 60;
        private const byte GuardDropPodLanded = 61;
        private const byte GuardItemAcquired = 62;
        private const byte GuardBiomeChanged = 63;
        private const byte GuardSectorResidencyHydrated = 64;
        private const byte GuardSectorDehydrated = 65;
        private const byte GuardChunkDehydrated = 66;
        private const byte GuardBubbleSpawn = 67;
        private const byte GuardHullRepaired = 68;
        private const byte GuardTetherTension = 69;
        private const byte GuardTetherSnapped = 70;
        private const byte GuardVisualFlare = 71;
        private const byte GuardVoxelCarve = 72;
        private const byte GuardDockingRequest = 73;
        private const byte GuardDockingComplete = 74;
        private const byte GuardDockingFailed = 75;
        private const byte GuardAnomalyProximity = 76;
        private const byte GuardCompassCalibrated = 77;
        private const byte GuardTetherFired = 78;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sanitize<T>(ref T signal)
            where T : unmanaged, ISignal
        {
            switch (SignalPayloadFiniteGuardCache<T>.Kind)
            {
                case GuardImpact:
                {
                    ref ImpactSignal typed = ref UnsafeUtility.As<T, ImpactSignal>(ref signal);
                    return SanitizeImpactSignal(ref typed);
                }
                case GuardHighSpeedImpact:
                {
                    ref HighSpeedImpactSignal typed = ref UnsafeUtility.As<T, HighSpeedImpactSignal>(ref signal);
                    return SanitizeHighSpeedImpactSignal(ref typed);
                }
                case GuardCombatDamage:
                {
                    ref CombatDamageSignal typed = ref UnsafeUtility.As<T, CombatDamageSignal>(ref signal);
                    return SanitizeCombatDamageSignal(ref typed);
                }
                case GuardFluidImpulse:
                {
                    ref FluidImpulseSignal typed = ref UnsafeUtility.As<T, FluidImpulseSignal>(ref signal);
                    return SanitizeFluidImpulseSignal(ref typed);
                }
                case GuardSystemPause:
                {
                    ref SystemPauseSignal typed = ref UnsafeUtility.As<T, SystemPauseSignal>(ref signal);
                    return SanitizeSystemPauseSignal(ref typed);
                }
                case GuardWeatherChanged:
                {
                    ref WeatherChangedSignal typed = ref UnsafeUtility.As<T, WeatherChangedSignal>(ref signal);
                    return SanitizeWeatherChangedSignal(ref typed);
                }
                case GuardTimeDilation:
                {
                    ref TimeDilationSignal typed = ref UnsafeUtility.As<T, TimeDilationSignal>(ref signal);
                    return SanitizeTimeDilationSignal(ref typed);
                }
                case GuardSimulationPause:
                {
                    ref SimulationPauseSignal typed = ref UnsafeUtility.As<T, SimulationPauseSignal>(ref signal);
                    return SanitizeSimulationPauseSignal(ref typed);
                }
                case GuardBulletTimeVisual:
                {
                    ref BulletTimeVisualSignal typed = ref UnsafeUtility.As<T, BulletTimeVisualSignal>(ref signal);
                    return SanitizeBulletTimeVisualSignal(ref typed);
                }
                case GuardWeatherStrength:
                {
                    ref WeatherStrengthSignal typed = ref UnsafeUtility.As<T, WeatherStrengthSignal>(ref signal);
                    return SanitizeWeatherStrengthSignal(ref typed);
                }
                case GuardPlayerLookTarget:
                {
                    ref PlayerLookTargetSignal typed = ref UnsafeUtility.As<T, PlayerLookTargetSignal>(ref signal);
                    return SanitizePlayerLookTargetSignal(ref typed);
                }
                case GuardPlayerBaseEnter:
                {
                    ref PlayerBaseEnterSignal typed = ref UnsafeUtility.As<T, PlayerBaseEnterSignal>(ref signal);
                    return SanitizePlayerBaseEnterSignal(ref typed);
                }
                case GuardPlayerBaseExit:
                {
                    ref PlayerBaseExitSignal typed = ref UnsafeUtility.As<T, PlayerBaseExitSignal>(ref signal);
                    return SanitizePlayerBaseExitSignal(ref typed);
                }
                case GuardPlayerState:
                {
                    ref PlayerStateSignal typed = ref UnsafeUtility.As<T, PlayerStateSignal>(ref signal);
                    return SanitizePlayerStateSignal(ref typed);
                }
                case GuardSurvivalVitalsChanged:
                {
                    ref SurvivalVitalsChangedSignal typed = ref UnsafeUtility.As<T, SurvivalVitalsChangedSignal>(ref signal);
                    return SanitizeSurvivalVitalsChangedSignal(ref typed);
                }
                case GuardPlayerActionProgress:
                {
                    ref PlayerActionProgressSignal typed = ref UnsafeUtility.As<T, PlayerActionProgressSignal>(ref signal);
                    return SanitizePlayerActionProgressSignal(ref typed);
                }
                case GuardCameraPosition:
                {
                    ref CameraPositionSignal typed = ref UnsafeUtility.As<T, CameraPositionSignal>(ref signal);
                    return SanitizeCameraPositionSignal(ref typed);
                }
                case GuardCameraFrustum:
                {
                    ref CameraFrustumSignal typed = ref UnsafeUtility.As<T, CameraFrustumSignal>(ref signal);
                    return SanitizeCameraFrustumSignal(ref typed);
                }
                case GuardHullDeformed:
                {
                    ref HullDeformedSignal typed = ref UnsafeUtility.As<T, HullDeformedSignal>(ref signal);
                    return SanitizeHullDeformedSignal(ref typed);
                }
                case GuardHullRepaired:
                {
                    ref HullRepairedSignal typed = ref UnsafeUtility.As<T, HullRepairedSignal>(ref signal);
                    return SanitizeHullRepairedSignal(ref typed);
                }
                case GuardBaseModuleCompromised:
                {
                    ref BaseModuleCompromisedSignal typed = ref UnsafeUtility.As<T, BaseModuleCompromisedSignal>(ref signal);
                    return SanitizeBaseModuleCompromisedSignal(ref typed);
                }
                case GuardAupPreShift:
                {
                    ref AupPreShiftSignal typed = ref UnsafeUtility.As<T, AupPreShiftSignal>(ref signal);
                    return SanitizeAupPreShiftSignal(ref typed);
                }
                case GuardAupShift:
                {
                    ref AupShiftSignal typed = ref UnsafeUtility.As<T, AupShiftSignal>(ref signal);
                    return SanitizeAupShiftSignal(ref typed);
                }
                case GuardRadiationDose:
                {
                    ref RadiationDoseSignal typed = ref UnsafeUtility.As<T, RadiationDoseSignal>(ref signal);
                    return SanitizeRadiationDoseSignal(ref typed);
                }
                case GuardTemperatureChanged:
                {
                    ref TemperatureChangedSignal typed = ref UnsafeUtility.As<T, TemperatureChangedSignal>(ref signal);
                    return SanitizeTemperatureChangedSignal(ref typed);
                }
                case GuardRadiationSource:
                {
                    ref RadiationSourceSignal typed = ref UnsafeUtility.As<T, RadiationSourceSignal>(ref signal);
                    return SanitizeRadiationSourceSignal(ref typed);
                }
                case GuardCullingOverload:
                {
                    ref CullingOverloadSignal typed = ref UnsafeUtility.As<T, CullingOverloadSignal>(ref signal);
                    return SanitizeCullingOverloadSignal(ref typed);
                }
                case GuardWakeGenerated:
                {
                    ref WakeGeneratedSignal typed = ref UnsafeUtility.As<T, WakeGeneratedSignal>(ref signal);
                    return SanitizeWakeGeneratedSignal(ref typed);
                }
                case GuardBiomeGradient:
                {
                    ref BiomeGradientSignal typed = ref UnsafeUtility.As<T, BiomeGradientSignal>(ref signal);
                    return SanitizeBiomeGradientSignal(ref typed);
                }
                case GuardMemoryPressure:
                {
                    ref MemoryPressureSignal typed = ref UnsafeUtility.As<T, MemoryPressureSignal>(ref signal);
                    return SanitizeMemoryPressureSignal(ref typed);
                }
                case GuardResolutionChanged:
                {
                    ref ResolutionChangedSignal typed = ref UnsafeUtility.As<T, ResolutionChangedSignal>(ref signal);
                    return SanitizeResolutionChangedSignal(ref typed);
                }
                case GuardSystemHealthIndex:
                {
                    ref SystemHealthIndexSignal typed = ref UnsafeUtility.As<T, SystemHealthIndexSignal>(ref signal);
                    return SanitizeSystemHealthIndexSignal(ref typed);
                }
                case GuardCpuStarvation:
                {
                    ref CpuStarvationSignal typed = ref UnsafeUtility.As<T, CpuStarvationSignal>(ref signal);
                    return SanitizeCpuStarvationSignal(ref typed);
                }
                case GuardAcousticPing:
                {
                    ref AcousticPingSignal typed = ref UnsafeUtility.As<T, AcousticPingSignal>(ref signal);
                    return SanitizeAcousticPingSignal(ref typed);
                }
                case GuardFluidIncursion:
                {
                    ref FluidIncursionSignal typed = ref UnsafeUtility.As<T, FluidIncursionSignal>(ref signal);
                    return SanitizeFluidIncursionSignal(ref typed);
                }
                case GuardSubmarineFloodState:
                {
                    ref SubmarineFloodStateSignal typed = ref UnsafeUtility.As<T, SubmarineFloodStateSignal>(ref signal);
                    return SanitizeSubmarineFloodStateSignal(ref typed);
                }
                case GuardFluidDensityChanged:
                {
                    ref FluidDensityChangedSignal typed = ref UnsafeUtility.As<T, FluidDensityChangedSignal>(ref signal);
                    return SanitizeFluidDensityChangedSignal(ref typed);
                }
                case GuardStreamingTurbulence:
                {
                    ref StreamingTurbulenceSignal typed = ref UnsafeUtility.As<T, StreamingTurbulenceSignal>(ref signal);
                    return SanitizeStreamingTurbulenceSignal(ref typed);
                }
                case GuardAtmosphericReentry:
                {
                    ref AtmosphericReentrySignal typed = ref UnsafeUtility.As<T, AtmosphericReentrySignal>(ref signal);
                    return SanitizeAtmosphericReentrySignal(ref typed);
                }
                case GuardVehicleUpgradesChanged:
                {
                    ref VehicleUpgradesChangedSignal typed = ref UnsafeUtility.As<T, VehicleUpgradesChangedSignal>(ref signal);
                    return SanitizeVehicleUpgradesChangedSignal(ref typed);
                }
                case GuardSaveLifecycle:
                {
                    ref SaveLifecycleSignal typed = ref UnsafeUtility.As<T, SaveLifecycleSignal>(ref signal);
                    return SanitizeSaveLifecycleSignal(ref typed);
                }
                case GuardSaveStatus:
                {
                    ref SaveStatusSignal typed = ref UnsafeUtility.As<T, SaveStatusSignal>(ref signal);
                    return SanitizeSaveStatusSignal(ref typed);
                }
                case GuardLightLevel:
                {
                    ref LightLevelSignal typed = ref UnsafeUtility.As<T, LightLevelSignal>(ref signal);
                    return SanitizeLightLevelSignal(ref typed);
                }
                case GuardSubmarineLightsChanged:
                {
                    ref SubmarineLightsChangedSignal typed = ref UnsafeUtility.As<T, SubmarineLightsChangedSignal>(ref signal);
                    return SanitizeSubmarineLightsChangedSignal(ref typed);
                }
                case GuardPhysiologyState:
                {
                    ref PhysiologyStateSignal typed = ref UnsafeUtility.As<T, PhysiologyStateSignal>(ref signal);
                    return SanitizePhysiologyStateSignal(ref typed);
                }
                case GuardPlayerStress:
                {
                    ref PlayerStressSignal typed = ref UnsafeUtility.As<T, PlayerStressSignal>(ref signal);
                    return SanitizePlayerStressSignal(ref typed);
                }
                case GuardTrauma:
                {
                    ref TraumaSignal typed = ref UnsafeUtility.As<T, TraumaSignal>(ref signal);
                    return SanitizeTraumaSignal(ref typed);
                }
                case GuardItemDurabilityChanged:
                {
                    ref ItemDurabilityChangedSignal typed = ref UnsafeUtility.As<T, ItemDurabilityChangedSignal>(ref signal);
                    return SanitizeItemDurabilityChangedSignal(ref typed);
                }
                case GuardBrownout:
                {
                    ref BrownoutSignal typed = ref UnsafeUtility.As<T, BrownoutSignal>(ref signal);
                    return SanitizeBrownoutSignal(ref typed);
                }
                case GuardEntityDeath:
                {
                    ref EntityDeathSignal typed = ref UnsafeUtility.As<T, EntityDeathSignal>(ref signal);
                    return SanitizeEntityDeathSignal(ref typed);
                }
                case GuardMovementAcoustic:
                {
                    ref MovementAcousticSignal typed = ref UnsafeUtility.As<T, MovementAcousticSignal>(ref signal);
                    return SanitizeMovementAcousticSignal(ref typed);
                }
                case GuardSwarmDispersed:
                {
                    ref SwarmDispersedSignal typed = ref UnsafeUtility.As<T, SwarmDispersedSignal>(ref signal);
                    return SanitizeSwarmDispersedSignal(ref typed);
                }
                case GuardScannerToolActive:
                {
                    ref ScannerToolActiveSignal typed = ref UnsafeUtility.As<T, ScannerToolActiveSignal>(ref signal);
                    return SanitizeScannerToolActiveSignal(ref typed);
                }
                case GuardStorageDebt:
                {
                    ref StorageDebtSignal typed = ref UnsafeUtility.As<T, StorageDebtSignal>(ref signal);
                    return SanitizeStorageDebtSignal(ref typed);
                }
                case GuardPrologueComplete:
                {
                    ref PrologueCompleteSignal typed = ref UnsafeUtility.As<T, PrologueCompleteSignal>(ref signal);
                    return SanitizePrologueCompleteSignal(ref typed);
                }
                case GuardManualOverridePulled:
                {
                    ref ManualOverridePulledSignal typed = ref UnsafeUtility.As<T, ManualOverridePulledSignal>(ref signal);
                    return SanitizeManualOverridePulledSignal(ref typed);
                }
                case GuardWfcOutpostGenerated:
                {
                    ref WfcOutpostGeneratedSignal typed = ref UnsafeUtility.As<T, WfcOutpostGeneratedSignal>(ref signal);
                    return SanitizeWfcOutpostGeneratedSignal(ref typed);
                }
                case GuardWfcOutpostDoorPower:
                {
                    ref WfcOutpostDoorPowerSignal typed = ref UnsafeUtility.As<T, WfcOutpostDoorPowerSignal>(ref signal);
                    return SanitizeWfcOutpostDoorPowerSignal(ref typed);
                }
                case GuardHapticRequest:
                {
                    ref HapticRequest typed = ref UnsafeUtility.As<T, HapticRequest>(ref signal);
                    return SanitizeHapticRequest(ref typed);
                }
                case GuardPlayerActionCancelled:
                {
                    ref PlayerActionCancelledSignal typed = ref UnsafeUtility.As<T, PlayerActionCancelledSignal>(ref signal);
                    return SanitizePlayerActionCancelledSignal(ref typed);
                }
                case GuardDropPodLanded:
                {
                    ref DropPodLandedSignal typed = ref UnsafeUtility.As<T, DropPodLandedSignal>(ref signal);
                    return SanitizeDropPodLandedSignal(ref typed);
                }
                case GuardItemAcquired:
                {
                    ref ItemAcquiredSignal typed = ref UnsafeUtility.As<T, ItemAcquiredSignal>(ref signal);
                    return SanitizeItemAcquiredSignal(ref typed);
                }
                case GuardBiomeChanged:
                {
                    ref BiomeChangedSignal typed = ref UnsafeUtility.As<T, BiomeChangedSignal>(ref signal);
                    return SanitizeBiomeChangedSignal(ref typed);
                }
                case GuardSectorResidencyHydrated:
                {
                    ref SectorResidencyHydratedSignal typed = ref UnsafeUtility.As<T, SectorResidencyHydratedSignal>(ref signal);
                    return SanitizeSectorResidencyHydratedSignal(ref typed);
                }
                case GuardSectorDehydrated:
                {
                    ref SectorDehydratedSignal typed = ref UnsafeUtility.As<T, SectorDehydratedSignal>(ref signal);
                    return SanitizeSectorDehydratedSignal(ref typed);
                }
                case GuardChunkDehydrated:
                {
                    ref ChunkDehydratedSignal typed = ref UnsafeUtility.As<T, ChunkDehydratedSignal>(ref signal);
                    return SanitizeChunkDehydratedSignal(ref typed);
                }
                case GuardBubbleSpawn:
                {
                    ref BubbleSpawnSignal typed = ref UnsafeUtility.As<T, BubbleSpawnSignal>(ref signal);
                    return SanitizeBubbleSpawnSignal(ref typed);
                }
                case GuardTetherTension:
                {
                    ref TetherTensionSignal typed = ref UnsafeUtility.As<T, TetherTensionSignal>(ref signal);
                    return SanitizeTetherTensionSignal(ref typed);
                }
                case GuardTetherSnapped:
                {
                    ref TetherSnappedSignal typed = ref UnsafeUtility.As<T, TetherSnappedSignal>(ref signal);
                    return SanitizeTetherSnappedSignal(ref typed);
                }
                case GuardVisualFlare:
                {
                    ref VisualFlareSignal typed = ref UnsafeUtility.As<T, VisualFlareSignal>(ref signal);
                    return SanitizeVisualFlareSignal(ref typed);
                }
                case GuardVoxelCarve:
                {
                    ref VoxelCarveEvent typed = ref UnsafeUtility.As<T, VoxelCarveEvent>(ref signal);
                    return SanitizeVoxelCarveEvent(ref typed);
                }
                case GuardDockingRequest:
                {
                    ref DockingRequestSignal typed = ref UnsafeUtility.As<T, DockingRequestSignal>(ref signal);
                    return SanitizeDockingRequestSignal(ref typed);
                }
                case GuardDockingComplete:
                {
                    ref DockingCompleteSignal typed = ref UnsafeUtility.As<T, DockingCompleteSignal>(ref signal);
                    return SanitizeDockingCompleteSignal(ref typed);
                }
                case GuardDockingFailed:
                {
                    ref DockingFailedSignal typed = ref UnsafeUtility.As<T, DockingFailedSignal>(ref signal);
                    return SanitizeDockingFailedSignal(ref typed);
                }
                case GuardAnomalyProximity:
                {
                    ref AnomalyProximitySignal typed = ref UnsafeUtility.As<T, AnomalyProximitySignal>(ref signal);
                    return SanitizeAnomalyProximitySignal(ref typed);
                }
                case GuardCompassCalibrated:
                {
                    ref CompassCalibratedSignal typed = ref UnsafeUtility.As<T, CompassCalibratedSignal>(ref signal);
                    return SanitizeCompassCalibratedSignal(ref typed);
                }
                case GuardTetherFired:
                {
                    ref TetherFiredSignal typed = ref UnsafeUtility.As<T, TetherFiredSignal>(ref signal);
                    return SanitizeTetherFiredSignal(ref typed);
                }
            }

            return 0;
        }

        private static byte ResolveGuardKind<T>()
            where T : unmanaged, ISignal
        {
            if (typeof(T) == typeof(ImpactSignal))
                return GuardImpact;
            if (typeof(T) == typeof(HighSpeedImpactSignal))
                return GuardHighSpeedImpact;
            if (typeof(T) == typeof(CombatDamageSignal))
                return GuardCombatDamage;
            if (typeof(T) == typeof(FluidImpulseSignal))
                return GuardFluidImpulse;
            if (typeof(T) == typeof(SystemPauseSignal))
                return GuardSystemPause;
            if (typeof(T) == typeof(WeatherChangedSignal))
                return GuardWeatherChanged;
            if (typeof(T) == typeof(TimeDilationSignal))
                return GuardTimeDilation;
            if (typeof(T) == typeof(SimulationPauseSignal))
                return GuardSimulationPause;
            if (typeof(T) == typeof(BulletTimeVisualSignal))
                return GuardBulletTimeVisual;
            if (typeof(T) == typeof(WeatherStrengthSignal))
                return GuardWeatherStrength;
            if (typeof(T) == typeof(PlayerLookTargetSignal))
                return GuardPlayerLookTarget;
            if (typeof(T) == typeof(PlayerBaseEnterSignal))
                return GuardPlayerBaseEnter;
            if (typeof(T) == typeof(PlayerBaseExitSignal))
                return GuardPlayerBaseExit;
            if (typeof(T) == typeof(PlayerStateSignal))
                return GuardPlayerState;
            if (typeof(T) == typeof(SurvivalVitalsChangedSignal))
                return GuardSurvivalVitalsChanged;
            if (typeof(T) == typeof(PlayerActionProgressSignal))
                return GuardPlayerActionProgress;
            if (typeof(T) == typeof(CameraPositionSignal))
                return GuardCameraPosition;
            if (typeof(T) == typeof(CameraFrustumSignal))
                return GuardCameraFrustum;
            if (typeof(T) == typeof(HullDeformedSignal))
                return GuardHullDeformed;
            if (typeof(T) == typeof(HullRepairedSignal))
                return GuardHullRepaired;
            if (typeof(T) == typeof(BaseModuleCompromisedSignal))
                return GuardBaseModuleCompromised;
            if (typeof(T) == typeof(AupPreShiftSignal))
                return GuardAupPreShift;
            if (typeof(T) == typeof(AupShiftSignal))
                return GuardAupShift;
            if (typeof(T) == typeof(RadiationDoseSignal))
                return GuardRadiationDose;
            if (typeof(T) == typeof(TemperatureChangedSignal))
                return GuardTemperatureChanged;
            if (typeof(T) == typeof(RadiationSourceSignal))
                return GuardRadiationSource;
            if (typeof(T) == typeof(CullingOverloadSignal))
                return GuardCullingOverload;
            if (typeof(T) == typeof(WakeGeneratedSignal))
                return GuardWakeGenerated;
            if (typeof(T) == typeof(BiomeGradientSignal))
                return GuardBiomeGradient;
            if (typeof(T) == typeof(MemoryPressureSignal))
                return GuardMemoryPressure;
            if (typeof(T) == typeof(ResolutionChangedSignal))
                return GuardResolutionChanged;
            if (typeof(T) == typeof(SystemHealthIndexSignal))
                return GuardSystemHealthIndex;
            if (typeof(T) == typeof(CpuStarvationSignal))
                return GuardCpuStarvation;
            if (typeof(T) == typeof(AcousticPingSignal))
                return GuardAcousticPing;
            if (typeof(T) == typeof(FluidIncursionSignal))
                return GuardFluidIncursion;
            if (typeof(T) == typeof(SubmarineFloodStateSignal))
                return GuardSubmarineFloodState;
            if (typeof(T) == typeof(FluidDensityChangedSignal))
                return GuardFluidDensityChanged;
            if (typeof(T) == typeof(StreamingTurbulenceSignal))
                return GuardStreamingTurbulence;
            if (typeof(T) == typeof(AtmosphericReentrySignal))
                return GuardAtmosphericReentry;
            if (typeof(T) == typeof(VehicleUpgradesChangedSignal))
                return GuardVehicleUpgradesChanged;
            if (typeof(T) == typeof(SaveLifecycleSignal))
                return GuardSaveLifecycle;
            if (typeof(T) == typeof(SaveStatusSignal))
                return GuardSaveStatus;
            if (typeof(T) == typeof(LightLevelSignal))
                return GuardLightLevel;
            if (typeof(T) == typeof(SubmarineLightsChangedSignal))
                return GuardSubmarineLightsChanged;
            if (typeof(T) == typeof(PhysiologyStateSignal))
                return GuardPhysiologyState;
            if (typeof(T) == typeof(PlayerStressSignal))
                return GuardPlayerStress;
            if (typeof(T) == typeof(TraumaSignal))
                return GuardTrauma;
            if (typeof(T) == typeof(ItemDurabilityChangedSignal))
                return GuardItemDurabilityChanged;
            if (typeof(T) == typeof(BrownoutSignal))
                return GuardBrownout;
            if (typeof(T) == typeof(EntityDeathSignal))
                return GuardEntityDeath;
            if (typeof(T) == typeof(MovementAcousticSignal))
                return GuardMovementAcoustic;
            if (typeof(T) == typeof(SwarmDispersedSignal))
                return GuardSwarmDispersed;
            if (typeof(T) == typeof(ScannerToolActiveSignal))
                return GuardScannerToolActive;
            if (typeof(T) == typeof(StorageDebtSignal))
                return GuardStorageDebt;
            if (typeof(T) == typeof(PrologueCompleteSignal))
                return GuardPrologueComplete;
            if (typeof(T) == typeof(ManualOverridePulledSignal))
                return GuardManualOverridePulled;
            if (typeof(T) == typeof(WfcOutpostGeneratedSignal))
                return GuardWfcOutpostGenerated;
            if (typeof(T) == typeof(WfcOutpostDoorPowerSignal))
                return GuardWfcOutpostDoorPower;
            if (typeof(T) == typeof(HapticRequest))
                return GuardHapticRequest;
            if (typeof(T) == typeof(PlayerActionCancelledSignal))
                return GuardPlayerActionCancelled;
            if (typeof(T) == typeof(DropPodLandedSignal))
                return GuardDropPodLanded;
            if (typeof(T) == typeof(ItemAcquiredSignal))
                return GuardItemAcquired;
            if (typeof(T) == typeof(BiomeChangedSignal))
                return GuardBiomeChanged;
            if (typeof(T) == typeof(SectorResidencyHydratedSignal))
                return GuardSectorResidencyHydrated;
            if (typeof(T) == typeof(SectorDehydratedSignal))
                return GuardSectorDehydrated;
            if (typeof(T) == typeof(ChunkDehydratedSignal))
                return GuardChunkDehydrated;
            if (typeof(T) == typeof(BubbleSpawnSignal))
                return GuardBubbleSpawn;
            if (typeof(T) == typeof(TetherTensionSignal))
                return GuardTetherTension;
            if (typeof(T) == typeof(TetherSnappedSignal))
                return GuardTetherSnapped;
            if (typeof(T) == typeof(VisualFlareSignal))
                return GuardVisualFlare;
            if (typeof(T) == typeof(VoxelCarveEvent))
                return GuardVoxelCarve;
            if (typeof(T) == typeof(DockingRequestSignal))
                return GuardDockingRequest;
            if (typeof(T) == typeof(DockingCompleteSignal))
                return GuardDockingComplete;
            if (typeof(T) == typeof(DockingFailedSignal))
                return GuardDockingFailed;
            if (typeof(T) == typeof(AnomalyProximitySignal))
                return GuardAnomalyProximity;
            if (typeof(T) == typeof(CompassCalibratedSignal))
                return GuardCompassCalibrated;
            if (typeof(T) == typeof(TetherFiredSignal))
                return GuardTetherFired;

            return GuardNone;
        }

        private static class SignalPayloadFiniteGuardCache<T>
            where T : unmanaged, ISignal
        {
            internal static readonly byte Kind = ResolveGuardKind<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeImpactSignal(ref ImpactSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PointAup) ? ImpactSignalGuardCode : 0;
            if (!math.isfinite(signal.Force))
            {
                signal.Force = 0f;
                guardCode = ImpactSignalGuardCode;
            }

            if (!math.isfinite(signal.Intensity))
            {
                signal.Intensity = 0f;
                guardCode = ImpactSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeFluidImpulseSignal(ref FluidImpulseSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? FluidImpulseSignalGuardCode : 0;
            if (!math.all(math.isfinite(signal.Vector)))
            {
                signal.Vector = float3.zero;
                guardCode = FluidImpulseSignalGuardCode;
            }

            if (!math.isfinite(signal.Radius) || signal.Radius < 0f)
            {
                signal.Radius = 0f;
                guardCode = FluidImpulseSignalGuardCode;
            }

            if (!math.isfinite(signal.Lifetime) || signal.Lifetime < 0f)
            {
                signal.Lifetime = 0f;
                guardCode = FluidImpulseSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBubbleSpawnSignal(ref BubbleSpawnSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? BubbleSpawnSignalGuardCode : 0;
            if (!math.all(math.isfinite(signal.Direction)))
            {
                signal.Direction = new float3(0f, 0f, -1f);
                guardCode = BubbleSpawnSignalGuardCode;
            }

            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = BubbleSpawnSignalGuardCode;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = BubbleSpawnSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSystemPauseSignal(ref SystemPauseSignal signal)
        {
            if (math.isfinite(signal.RestoreScalar))
                return 0;

            signal.RestoreScalar = 0f;
            return SystemPauseSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWeatherChangedSignal(ref WeatherChangedSignal signal)
        {
            int guardCode = 0;
            if (!math.isfinite(signal.Strength01))
            {
                signal.Strength01 = 0f;
                guardCode = WeatherChangedSignalGuardCode;
            }

            if (!math.isfinite(signal.FlowFieldScale))
            {
                signal.FlowFieldScale = 0f;
                guardCode = WeatherChangedSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTimeDilationSignal(ref TimeDilationSignal signal)
        {
            int guardCode = 0;
            if (!math.isfinite(signal.Scalar) || signal.Scalar < 0f)
            {
                signal.Scalar = 0f;
                guardCode = TimeDilationSignalGuardCode;
            }

            if (!math.isfinite(signal.UnscaledDeltaTime) || signal.UnscaledDeltaTime < 0f)
            {
                signal.UnscaledDeltaTime = 0f;
                guardCode = TimeDilationSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSimulationPauseSignal(ref SimulationPauseSignal signal)
        {
            if (math.isfinite(signal.RestoreScalar))
                return 0;

            signal.RestoreScalar = 0f;
            return SimulationPauseSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBulletTimeVisualSignal(ref BulletTimeVisualSignal signal)
        {
            int guardCode = 0;
            if (!math.isfinite(signal.Intensity01))
            {
                signal.Intensity01 = 0f;
                guardCode = BulletTimeVisualSignalGuardCode;
            }

            if (!math.isfinite(signal.Scalar) || signal.Scalar < 0f)
            {
                signal.Scalar = 0f;
                guardCode = BulletTimeVisualSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWeatherStrengthSignal(ref WeatherStrengthSignal signal)
        {
            int guardCode = 0;
            if (!math.isfinite(signal.Strength01))
            {
                signal.Strength01 = 0f;
                guardCode = WeatherStrengthSignalGuardCode;
            }

            if (!math.isfinite(signal.FlowFieldScale))
            {
                signal.FlowFieldScale = 0f;
                guardCode = WeatherStrengthSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerLookTargetSignal(ref PlayerLookTargetSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.TargetAup) ? PlayerLookTargetSignalGuardCode : 0;
            if (!math.all(math.isfinite(signal.RuntimeAnchor)))
            {
                signal.RuntimeAnchor = float3.zero;
                signal.State = PlayerLookTargetSignalStates.Cleared;
                guardCode = PlayerLookTargetSignalGuardCode;
            }

            if (!math.all(math.isfinite(signal.SurfaceNormal)))
            {
                SetUp(ref signal.SurfaceNormal);
                guardCode = PlayerLookTargetSignalGuardCode;
            }

            if (!math.isfinite(signal.DistanceMeters) || signal.DistanceMeters < 0f)
            {
                signal.DistanceMeters = 0f;
                guardCode = PlayerLookTargetSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerBaseEnterSignal(ref PlayerBaseEnterSignal signal)
        {
            if (!SanitizeAup(ref signal.BaseCenterAup))
                return 0;

            signal.Flags = (ushort)(signal.Flags | PlayerBaseEnterSignal.SanitizedBaseCenterFlag);
            return PlayerBaseEnterSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerBaseExitSignal(ref PlayerBaseExitSignal signal)
        {
            if (!SanitizeAup(ref signal.BaseCenterAup))
                return 0;

            signal.Flags = (ushort)(signal.Flags | PlayerBaseExitSignal.SanitizedBaseCenterFlag);
            return PlayerBaseExitSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerStateSignal(ref PlayerStateSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? PlayerStateSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = PlayerStateSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSurvivalVitalsChangedSignal(ref SurvivalVitalsChangedSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Oxygen01))
                guardCode = SurvivalVitalsChangedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Energy01))
                guardCode = SurvivalVitalsChangedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Integrity01))
                guardCode = SurvivalVitalsChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerActionProgressSignal(ref PlayerActionProgressSignal signal)
        {
            if (!SanitizeUnit01(ref signal.Progress01))
                return 0;

            return PlayerActionProgressSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCameraPositionSignal(ref CameraPositionSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.Position))
                guardCode = CameraPositionSignalGuardCode;
            if (SanitizeFloat3Forward(ref signal.Forward))
                guardCode = CameraPositionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCameraFrustumSignal(ref CameraFrustumSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.Position))
                guardCode = CameraFrustumSignalGuardCode;
            if (SanitizeFloat3Forward(ref signal.Forward))
                guardCode = CameraFrustumSignalGuardCode;
            if (SanitizeFloat3Up(ref signal.Up))
                guardCode = CameraFrustumSignalGuardCode;
            if (SanitizePositiveDefault(ref signal.FieldOfViewDegrees, 60f))
                guardCode = CameraFrustumSignalGuardCode;
            if (SanitizeNonNegative(ref signal.NearClipMeters))
                guardCode = CameraFrustumSignalGuardCode;
            if (SanitizeNonNegative(ref signal.FarClipMeters))
                guardCode = CameraFrustumSignalGuardCode;
            if (signal.FarClipMeters < signal.NearClipMeters)
            {
                signal.FarClipMeters = signal.NearClipMeters;
                guardCode = CameraFrustumSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeHullDeformedSignal(ref HullDeformedSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.LocalPoint))
                guardCode = HullDeformedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.Radius))
                guardCode = HullDeformedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.Depth))
                guardCode = HullDeformedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = HullDeformedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeHullRepairedSignal(ref HullRepairedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.HitAup) ? HullRepairedSignalGuardCode : 0;
            if (signal.RoomId < -1)
            {
                signal.RoomId = -1;
                guardCode = HullRepairedSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBaseModuleCompromisedSignal(ref BaseModuleCompromisedSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.ModuleCenter))
                guardCode = BaseModuleCompromisedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Stress01))
                guardCode = BaseModuleCompromisedSignalGuardCode;
            if (SanitizeUnit01(ref signal.PeakStress01))
                guardCode = BaseModuleCompromisedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.DepthMeters))
                guardCode = BaseModuleCompromisedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeAupPreShiftSignal(ref AupPreShiftSignal signal)
        {
            if (!SanitizeFloat3Zero(ref signal.ShiftMeters))
                return 0;

            return AupPreShiftSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeAupShiftSignal(ref AupShiftSignal signal)
        {
            if (!SanitizeFloat3Zero(ref signal.ShiftMeters))
                return 0;

            return AupShiftSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeRadiationDoseSignal(ref RadiationDoseSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? RadiationDoseSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Dose))
                guardCode = RadiationDoseSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = RadiationDoseSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTemperatureChangedSignal(ref TemperatureChangedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? TemperatureChangedSignalGuardCode : 0;
            if (SanitizeFiniteZero(ref signal.TemperatureCelsius))
                guardCode = TemperatureChangedSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.DeltaCelsius))
                guardCode = TemperatureChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeRadiationSourceSignal(ref RadiationSourceSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? RadiationSourceSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Intensity))
                guardCode = RadiationSourceSignalGuardCode;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = RadiationSourceSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCullingOverloadSignal(ref CullingOverloadSignal signal)
        {
            int guardCode = 0;
            if (SanitizeNonNegative(ref signal.CullDistanceMeters))
                guardCode = CullingOverloadSignalGuardCode;
            if (SanitizeNonNegative(ref signal.VramUsedMb))
                guardCode = CullingOverloadSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWakeGeneratedSignal(ref WakeGeneratedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? WakeGeneratedSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.Velocity))
                guardCode = WakeGeneratedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBiomeGradientSignal(ref BiomeGradientSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? BiomeGradientSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.BlendFactor01))
                guardCode = BiomeGradientSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.BoundaryDistanceMeters))
                guardCode = BiomeGradientSignalGuardCode;
            if (SanitizePositiveDefault(ref signal.CellSizeMeters, 1f))
                guardCode = BiomeGradientSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeMemoryPressureSignal(ref MemoryPressureSignal signal)
        {
            if (!SanitizeNonNegative(ref signal.UsageRatio))
                return 0;

            return MemoryPressureSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeResolutionChangedSignal(ref ResolutionChangedSignal signal)
        {
            if (!SanitizeNonNegative(ref signal.VramUsedMb))
                return 0;

            return ResolutionChangedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSystemHealthIndexSignal(ref SystemHealthIndexSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Health01))
                guardCode = SystemHealthIndexSignalGuardCode;
            if (SanitizeUnit01(ref signal.Pressure01))
                guardCode = SystemHealthIndexSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCpuStarvationSignal(ref CpuStarvationSignal signal)
        {
            int guardCode = 0;
            if (SanitizeNonNegative(ref signal.EstimatedCostMs))
                guardCode = CpuStarvationSignalGuardCode;
            if (SanitizeNonNegative(ref signal.RemainingBudgetMs))
                guardCode = CpuStarvationSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeAcousticPingSignal(ref AcousticPingSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? AcousticPingSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = AcousticPingSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = AcousticPingSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeFluidIncursionSignal(ref FluidIncursionSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.LeakAup) ? FluidIncursionSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.FloodLevel01))
                guardCode = FluidIncursionSignalGuardCode;
            if (SanitizeUnit01(ref signal.FlowRate01))
                guardCode = FluidIncursionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSubmarineFloodStateSignal(ref SubmarineFloodStateSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.DynamicCenterOfMassLocal))
                guardCode = SubmarineFloodStateSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.DynamicCenterOfMassOffsetLocal))
                guardCode = SubmarineFloodStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.TotalWaterMassKg))
                guardCode = SubmarineFloodStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.BaseMassKg))
                guardCode = SubmarineFloodStateSignalGuardCode;
            if (SanitizeUnit01(ref signal.FillRatio01))
                guardCode = SubmarineFloodStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.AngularDragMultiplier))
                guardCode = SubmarineFloodStateSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeFluidDensityChangedSignal(ref FluidDensityChangedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? FluidDensityChangedSignalGuardCode : 0;
            if (SanitizePositiveDefault(ref signal.DensityMultiplier, 1f))
                guardCode = FluidDensityChangedSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.BrineHeightY))
                guardCode = FluidDensityChangedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.SubmersionSeconds))
                guardCode = FluidDensityChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeStreamingTurbulenceSignal(ref StreamingTurbulenceSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = StreamingTurbulenceSignalGuardCode;
            if (SanitizeUnit01(ref signal.Debt01))
                guardCode = StreamingTurbulenceSignalGuardCode;
            if (SanitizeNonNegative(ref signal.DurationSeconds))
                guardCode = StreamingTurbulenceSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeAtmosphericReentrySignal(ref AtmosphericReentrySignal signal)
        {
            int guardCode = SanitizeAup(ref signal.CapsuleAup) ? AtmosphericReentrySignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.AltitudeMeters))
                guardCode = AtmosphericReentrySignalGuardCode;
            if (SanitizeNonNegative(ref signal.UniverseVelocityMetersPerSecond))
                guardCode = AtmosphericReentrySignalGuardCode;
            if (SanitizeUnit01(ref signal.Heat01))
                guardCode = AtmosphericReentrySignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeVehicleUpgradesChangedSignal(ref VehicleUpgradesChangedSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFiniteZero(ref signal.SafeDepthBonusMeters))
                guardCode = VehicleUpgradesChangedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.PermanentSafeDepthPenaltyMeters))
                guardCode = VehicleUpgradesChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSaveLifecycleSignal(ref SaveLifecycleSignal signal)
        {
            if (!SanitizeUnit01(ref signal.Progress01))
                return 0;

            return SaveLifecycleSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSaveStatusSignal(ref SaveStatusSignal signal)
        {
            if (!SanitizeUnit01(ref signal.Progress01))
                return 0;

            return SaveStatusSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeLightLevelSignal(ref LightLevelSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.LightLevel01))
                guardCode = LightLevelSignalGuardCode;
            if (SanitizeUnit01(ref signal.Darkness01))
                guardCode = LightLevelSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSubmarineLightsChangedSignal(ref SubmarineLightsChangedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? SubmarineLightsChangedSignalGuardCode : 0;
            if (SanitizeFloat3Forward(ref signal.Forward))
                guardCode = SubmarineLightsChangedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.RangeMeters))
                guardCode = SubmarineLightsChangedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.Intensity))
                guardCode = SubmarineLightsChangedSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.SpotOuterCos))
                guardCode = SubmarineLightsChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePhysiologyStateSignal(ref PhysiologyStateSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.PlayerStress01))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.O2DrainMultiplier))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeUnit01(ref signal.Recovery01))
                guardCode = PhysiologyStateSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerStressSignal(ref PlayerStressSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Stress01))
                guardCode = PlayerStressSignalGuardCode;
            if (SanitizeNonNegative(ref signal.OxygenDrainScale))
                guardCode = PlayerStressSignalGuardCode;
            if (SanitizeNonNegative(ref signal.AggressionScale))
                guardCode = PlayerStressSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTraumaSignal(ref TraumaSignal signal)
        {
            if (!SanitizeUnit01(ref signal.Stress01))
                return 0;

            return TraumaSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeItemDurabilityChangedSignal(ref ItemDurabilityChangedSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Durability01))
                guardCode = ItemDurabilityChangedSignalGuardCode;
            if (SanitizeUnit01(ref signal.AverageEquippedDurability01))
                guardCode = ItemDurabilityChangedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBrownoutSignal(ref BrownoutSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.SupplyRatio))
                guardCode = BrownoutSignalGuardCode;
            if (SanitizeUnit01(ref signal.Severity01))
                guardCode = BrownoutSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeEntityDeathSignal(ref EntityDeathSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? EntityDeathSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = EntityDeathSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeMovementAcousticSignal(ref MovementAcousticSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? MovementAcousticSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Volume))
                guardCode = MovementAcousticSignalGuardCode;
            if (SanitizeNonNegative(ref signal.VelocitySq))
                guardCode = MovementAcousticSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSwarmDispersedSignal(ref SwarmDispersedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? SwarmDispersedSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = SwarmDispersedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = SwarmDispersedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeScannerToolActiveSignal(ref ScannerToolActiveSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Progress01))
                guardCode = ScannerToolActiveSignalGuardCode;
            if (SanitizeUnit01(ref signal.Battery01))
                guardCode = ScannerToolActiveSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeStorageDebtSignal(ref StorageDebtSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Debt01))
                guardCode = StorageDebtSignalGuardCode;
            if (SanitizeNonNegative(ref signal.LatencyEwmaMs))
                guardCode = StorageDebtSignalGuardCode;
            if (SanitizeNonNegative(ref signal.OldestPendingMs))
                guardCode = StorageDebtSignalGuardCode;
            if (SanitizeNonNegative(ref signal.CriticalHoleDebtMs))
                guardCode = StorageDebtSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePrologueCompleteSignal(ref PrologueCompleteSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.CapsuleAup) ? PrologueCompleteSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.WhiteoutHoldSeconds))
                guardCode = PrologueCompleteSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeManualOverridePulledSignal(ref ManualOverridePulledSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.LeverLocalPosition))
                guardCode = ManualOverridePulledSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.AngleDegrees))
                guardCode = ManualOverridePulledSignalGuardCode;
            if (SanitizeUnit01(ref signal.GripStrength01))
                guardCode = ManualOverridePulledSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.PivotLocalPosition))
                guardCode = ManualOverridePulledSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.VelocityDegreesPerSecond))
                guardCode = ManualOverridePulledSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWfcOutpostGeneratedSignal(ref WfcOutpostGeneratedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.OriginAup) ? WfcOutpostGeneratedSignalGuardCode : 0;
            if (SanitizePositiveDefault(ref signal.CellSizeMeters, 1f))
                guardCode = WfcOutpostGeneratedSignalGuardCode;
            if (SanitizePositiveDefault(ref signal.FloorHeightMeters, 1f))
                guardCode = WfcOutpostGeneratedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWfcOutpostDoorPowerSignal(ref WfcOutpostDoorPowerSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.DoorAup) ? WfcOutpostDoorPowerSignalGuardCode : 0;
            if (SanitizeFiniteZero(ref signal.Voltage))
                guardCode = WfcOutpostDoorPowerSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeHapticRequest(ref HapticRequest signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = HapticRequestGuardCode;
            if (SanitizeNonNegative(ref signal.DurationSeconds))
                guardCode = HapticRequestGuardCode;
            if (SanitizeUnit01(ref signal.Frequency01))
                guardCode = HapticRequestGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerActionCancelledSignal(ref PlayerActionCancelledSignal signal)
        {
            if (!SanitizeUnit01(ref signal.Progress01))
                return 0;

            return PlayerActionCancelledSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDropPodLandedSignal(ref DropPodLandedSignal signal)
        {
            if (!SanitizeAup(ref signal.PositionAup))
                return 0;

            return DropPodLandedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeItemAcquiredSignal(ref ItemAcquiredSignal signal)
        {
            if (!SanitizeAup(ref signal.PositionAup))
                return 0;

            return ItemAcquiredSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeBiomeChangedSignal(ref BiomeChangedSignal signal)
        {
            if (!SanitizeAup(ref signal.PositionAup))
                return 0;

            return BiomeChangedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSectorResidencyHydratedSignal(ref SectorResidencyHydratedSignal signal)
        {
            if (!SanitizeAup(ref signal.CenterAup))
                return 0;

            return SectorResidencyHydratedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSectorDehydratedSignal(ref SectorDehydratedSignal signal)
        {
            if (!SanitizeAup(ref signal.CenterAup))
                return 0;

            return SectorDehydratedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeChunkDehydratedSignal(ref ChunkDehydratedSignal signal)
        {
            if (!SanitizeAup(ref signal.CenterAup))
                return 0;

            return ChunkDehydratedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeHighSpeedImpactSignal(ref HighSpeedImpactSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PointAup) ? HighSpeedImpactSignalGuardCode : 0;
            if (!math.all(math.isfinite(signal.Normal)))
            {
                signal.Normal = float3.zero;
                guardCode = HighSpeedImpactSignalGuardCode;
            }

            if (!math.isfinite(signal.LostKineticEnergy) || signal.LostKineticEnergy < 0f)
            {
                signal.LostKineticEnergy = 0f;
                guardCode = HighSpeedImpactSignalGuardCode;
            }

            if (!math.isfinite(signal.ImpactSpeed) || signal.ImpactSpeed < 0f)
            {
                signal.ImpactSpeed = 0f;
                guardCode = HighSpeedImpactSignalGuardCode;
            }

            if (!math.isfinite(signal.EffectiveMass) || signal.EffectiveMass < 0f)
            {
                signal.EffectiveMass = 0f;
                guardCode = HighSpeedImpactSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTetherTensionSignal(ref TetherTensionSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.AnchorAup) ? TetherTensionSignalGuardCode : 0;
            if (SanitizeAup(ref signal.PayloadAup))
                guardCode = TetherTensionSignalGuardCode;
            if (SanitizeFloat3Forward(ref signal.DirectionToPayload))
                guardCode = TetherTensionSignalGuardCode;
            if (SanitizeNonNegative(ref signal.TensionForce))
                guardCode = TetherTensionSignalGuardCode;
            if (SanitizeNonNegative(ref signal.SnapThreshold))
                guardCode = TetherTensionSignalGuardCode;
            if (SanitizeUnit01(ref signal.Tension01))
                guardCode = TetherTensionSignalGuardCode;
            if (SanitizeUnit01(ref signal.ReactiveVfx01))
                guardCode = TetherTensionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTetherSnappedSignal(ref TetherSnappedSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.SnapAup) ? TetherSnappedSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.PeakTension))
                guardCode = TetherSnappedSignalGuardCode;
            if (SanitizeNonNegative(ref signal.SnapThreshold))
                guardCode = TetherSnappedSignalGuardCode;
            if (SanitizeUnit01(ref signal.Severity01))
                guardCode = TetherSnappedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeVisualFlareSignal(ref VisualFlareSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = VisualFlareSignalGuardCode;
            if (SanitizeFloat2Zero(ref signal.ScreenUv))
                guardCode = VisualFlareSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeVoxelCarveEvent(ref VoxelCarveEvent signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.AbsoluteHitPoint))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeFloat3Zero(ref signal.AbsoluteSegmentEnd))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeFloat3Zero(ref signal.AbsoluteHalfExtents))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeFloat3Forward(ref signal.AbsoluteImpulseDirection))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeDouble3Zero(ref signal.AbsoluteHitPointDouble))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeDouble3Zero(ref signal.AbsoluteSegmentEndDouble))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = VoxelCarveEventGuardCode;
            if (SanitizeNonNegative(ref signal.BlendStrengthMeters))
                guardCode = VoxelCarveEventGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDockingRequestSignal(ref DockingRequestSignal signal)
        {
            int guardCode = SanitizeAupBlit(ref signal.DockAup) ? DockingRequestSignalGuardCode : 0;
            if (SanitizeFloat3Forward(ref signal.DockForward))
                guardCode = DockingRequestSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDockingCompleteSignal(ref DockingCompleteSignal signal)
        {
            int guardCode = SanitizeAupBlit(ref signal.DockAup) ? DockingCompleteSignalGuardCode : 0;
            if (SanitizeFloat3Forward(ref signal.DockForward))
                guardCode = DockingCompleteSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDockingFailedSignal(ref DockingFailedSignal signal)
        {
            int guardCode = SanitizeAupBlit(ref signal.LastAup) ? DockingFailedSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.FailureVector))
                guardCode = DockingFailedSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeAnomalyProximitySignal(ref AnomalyProximitySignal signal)
        {
            int guardCode = SanitizeAup(ref signal.SourceAup) ? AnomalyProximitySignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.Proximity01))
                guardCode = AnomalyProximitySignalGuardCode;
            if (SanitizeUnit01(ref signal.Interference01))
                guardCode = AnomalyProximitySignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCompassCalibratedSignal(ref CompassCalibratedSignal signal)
        {
            if (!SanitizeUnit01(ref signal.CalibrationQuality01))
                return 0;

            return CompassCalibratedSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeTetherFiredSignal(ref TetherFiredSignal signal)
        {
            if (!SanitizeNonNegative(ref signal.InitialDistance))
                return 0;

            return TetherFiredSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCombatDamageSignal(ref CombatDamageSignal signal)
        {
            int guardCode = 0;
            if (!math.all(math.isfinite(signal.WorldPoint)))
            {
                signal.WorldPoint = float3.zero;
                guardCode = CombatDamageSignalGuardCode;
            }

            if (!math.all(math.isfinite(signal.Direction)))
            {
                signal.Direction = float3.zero;
                guardCode = CombatDamageSignalGuardCode;
            }

            if (!math.isfinite(signal.Magnitude) || signal.Magnitude < 0f)
            {
                signal.Magnitude = 0f;
                guardCode = CombatDamageSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeAup(ref AbsoluteUniversePosition position)
        {
            bool invalid =
                !math.isfinite(position.LocalX) ||
                !math.isfinite(position.LocalY) ||
                !math.isfinite(position.LocalZ);

            if (!invalid)
                return false;

            position.LocalX = 0f;
            position.LocalY = 0f;
            position.LocalZ = 0f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeAupBlit(ref AbsoluteUniversePositionBlit position)
        {
            if (math.all(math.isfinite(position.Local)))
                return false;

            position.Local = float3.zero;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeFloat3Zero(ref float3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value = float3.zero;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeFloat2Zero(ref float2 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value = float2.zero;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeDouble3Zero(ref double3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value = double3.zero;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeFloat3Forward(ref float3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value.x = 0f;
            value.y = 0f;
            value.z = 1f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeFloat3Up(ref float3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            SetUp(ref value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetUp(ref float3 value)
        {
            value.x = 0f;
            value.y = 1f;
            value.z = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeUnit01(ref float value)
        {
            if (!math.isfinite(value))
            {
                value = 0f;
                return true;
            }

            float clamped = math.saturate(value);
            if (clamped == value)
                return false;

            value = clamped;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeNonNegative(ref float value)
        {
            if (!math.isfinite(value) || value < 0f)
            {
                value = 0f;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeFiniteZero(ref float value)
        {
            if (math.isfinite(value))
                return false;

            value = 0f;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizePositiveDefault(ref float value, float fallback)
        {
            if (math.isfinite(value) && value > 0f)
                return false;

            value = fallback;
            return true;
        }
    }
}

namespace Hecton8.Core
{
    /// <summary>
    /// Global native signal corridor. Producers enqueue unmanaged packets; consumers drain their own lanes.
    /// </summary>
    [Preserve]
    public static class GlobalSignals
    {
        private const int DamageSignalCapacity = 256;
        private const int HullDeformedSignalCapacity = 64;
        private const int HullRepairedSignalCapacity = 64;
        private const int BaseModuleCompromisedSignalCapacity = 64;
        private const int PlayerBaseTransitionSignalCapacity = 32;
        private const int ImpactSignalCapacity = 256;
        private const int HighSpeedImpactSignalCapacity = 128;
        private const int HapticRequestCapacity = 64;
        private const int PlayerStateSignalCapacity = 64;
        private const int SurvivalVitalsChangedSignalCapacity = 64;
        private const int AupPreShiftSignalCapacity = 64;
        private const int AupShiftSignalCapacity = 64;
        private const int DropPodLandedSignalCapacity = 8;
        private const int BrownoutSignalCapacity = 64;
        private const int DebrisSpawnSignalCapacity = 128;
        private const int DeflectSignalCapacity = 128;
        private const int EntityDeathSignalCapacity = 64;
        private const int EntitySpawnSignalCapacity = 128;
        private const int InputStateSignalCapacity = 64;
        private const int PlayerInputSignalCapacity = 64;
        private const int PlayerLookTargetSignalCapacity = 64;
        private const int SolarFlareSignalCapacity = 16;
        private const int RebaseSignalCapacity = 64;
        private const int ControlSignalCapacity = 256;
        private const int AnomalySignalCapacity = 128;
        private const int TelemetryAnomalySignalCapacity = 128;
        private const int CrashTelemetrySignalCapacity = 64;
        private const int HabitatConstructionSignalCapacity = 64;
        private const int DeconstructRequestSignalCapacity = 64;
        private const int DeconstructResultSignalCapacity = 64;
        private const int ModuleDeconstructSignalCapacity = 64;
        private const int VitalWarningSignalCapacity = 32;
        private const int CrushWarningSignalCapacity = 32;
        private const int VocalWarningSignalCapacity = 64;
        private const int SubtitleSignalCapacity = 64;
        private const int DataReloadSignalCapacity = 32;
        private const int MemoryPressureSignalCapacity = 16;
        private const int MemoryAddressShiftSignalCapacity = 64;
        private const int ResolutionChangedSignalCapacity = 16;
        private const int SystemHealthIndexSignalCapacity = 16;
        private const int AcousticPingSignalCapacity = 64;
        private const int MovementAcousticSignalCapacity = 128;
        private const int SwarmDispersedSignalCapacity = 64;
        private const int SonarPingSignalCapacity = 64;
        private const int HypoxiaSignalCapacity = 32;
        private const int OxygenCriticalSignalCapacity = 32;
        private const int InteractionUiSignalCapacity = 128;
        private const int UIRescaleRequestSignalCapacity = 64;
        private const int FluidIncursionSignalCapacity = 64;
        private const int FluidDensityChangedSignalCapacity = 64;
        private const int SubmarineFloodStateSignalCapacity = 64;
        private const int PipeRuptureSignalCapacity = 64;
        private const int SpectrumScanSignalCapacity = 128;
        private const int RigidbodySleepSignalCapacity = 128;
        private const int ScannerToolActiveSignalCapacity = 64;
        private const int ScanCompleteSignalCapacity = 128;
        private const int LoreFragmentScannedSignalCapacity = 128;
        private const int BlueprintUnlockedSignalCapacity = 128;
        private const int CraftingStartedSignalCapacity = 128;
        private const int CraftingCompletedSignalCapacity = 128;
        private const int ToolStateChangedSignalCapacity = 64;
        private const int ToolLoadoutChangedSignalCapacity = 64;
        private const int ToolAcousticSignalCapacity = 128;
        private const int PowerDrainSignalCapacity = 128;
        private const int ToolTriggerSignalCapacity = 128;
        private const int HUDNotificationSignalCapacity = 128;
        private const int ThermalStateChangedSignalCapacity = 32;
        private const int BatteryLevelSignalCapacity = 32;
        private const int ReconDataSignalCapacity = 128;
        private const int SaveLifecycleSignalCapacity = 16;
        private const int WfcOutpostGeneratedSignalCapacity = 16;
        private const int WfcOutpostDoorPowerSignalCapacity = 64;
        private const int ComplianceViolationSignalCapacity = 64;
        private const int GlobalTimeSyncSignalCapacity = 16;
        private const int SeismicSignalCapacity = 64;
        private const int TimeDilationSignalCapacity = 32;
        private const int SimulationPauseSignalCapacity = 32;
        private const int SimulationBucketSyncSignalCapacity = 8;
        private const int FramePacingWarningSignalCapacity = 8;
        private const int BulletTimeVisualSignalCapacity = 32;
        private const int WeatherStrengthSignalCapacity = 32;
        private const int CameraPositionSignalCapacity = 8;
        private const int CameraFrustumSignalCapacity = 8;
        private const int ChunkDehydratedSignalCapacity = 64;
        private const int ItemDecaySignalCapacity = 64;
        private const int InventoryCommandSignalCapacity = 16;
        private const int InventoryChangedSignalCapacity = 64;
        private const int ItemDurabilityChangedSignalCapacity = 64;
        private const int ItemAcquiredSignalCapacity = 128;
        private const int RadiationDoseSignalCapacity = 64;
        private const int RadiationSourceSignalCapacity = 64;
        private const int TemperatureChangedSignalCapacity = 64;
        private const int ResourceDepletionDeltaSignalCapacity = 64;
        private const int LightLevelSignalCapacity = 64;
        private const int SubmarineLightsChangedSignalCapacity = 64;
        private const int FaunaStateChangedSignalCapacity = 128;
        private const int PhysiologyStateSignalCapacity = 64;
        private const int PlayerStressSignalCapacity = 64;
        private const int TraumaSignalCapacity = 16;
        private const int WakeGeneratedSignalCapacity = 128;
        private const int FluidImpulseSignalCapacity = 32;
        private const int BubbleSpawnSignalCapacity = 64;
        private const int ProgressionEventSignalCapacity = 128;
        private const int GlobalWorldStateSignalCapacity = 64;
        private const int BiomeChangedSignalCapacity = 64;
        private const int NarrativeFocusSignalCapacity = 64;
        private const int FocusBrokenSignalCapacity = 32;
        private const int MixerStateSignalCapacity = 32;
        private const int DiegeticHudSignalCapacity = 32;
        private const int NarrativeHudWaypointSignalCapacity = 64;
        private const int SoundscapeProfileSignalCapacity = 64;
        private const int NarrativePoiStateSignalCapacity = 64;
        private const int BiomeGradientSignalCapacity = 64;
        private const int StorageDebtSignalCapacity = 32;
        private const int StreamingTurbulenceSignalCapacity = 32;
        private const int AtmosphericReentrySignalCapacity = 32;
        private const int PrologueCompleteSignalCapacity = 8;
        private const int ManualOverridePulledSignalCapacity = 8;
        private const int CullingOverloadSignalCapacity = 16;
        private const int PlayerActionProgressSignalCapacity = 64;
        private const int PlayerActionCompletedSignalCapacity = 16;
        private const int PlayerActionCancelledSignalCapacity = 16;
        private const int ScanLogChangedSignalCapacity = 32;
        private const int PdaExchangeStateChangedSignalCapacity = 32;
        private const int VehicleUpgradesChangedSignalCapacity = 32;
        private const int SignalTelemetryLaneBudgetPerFrame = 4;

        private static NativeQueue<ImpactSignal> _impactSignals;
        private static NativeQueue<AupPreShiftSignal> _aupPreShiftSignals;
        private static NativeQueue<AupShiftSignal> _aupShiftSignals;
        private static NativeQueue<BrownoutSignal> _brownoutSignals;
        private static NativeQueue<DebrisSpawnSignal> _debrisSpawnSignals;
        private static NativeQueue<DeflectSignal> _deflectSignals;
        private static NativeQueue<EntityDeathSignal> _entityDeathSignals;
        private static NativeQueue<SolarFlareSignal> _solarFlareSignals;
        private static NativeQueue<RebaseSignal> _rebaseSignals;
        private static NativeQueue<ControlSignal> _controlSignals;
        private static NativeQueue<AnomalySignal> _anomalySignals;
        private static NativeQueue<TelemetryAnomalySignal> _telemetryAnomalySignals;
        private static NativeQueue<CrashTelemetrySignal> _crashTelemetrySignals;
        private static NativeQueue<HabitatConstructionSignal> _habitatConstructionSignals;
        private static NativeQueue<DeconstructRequestSignal> _deconstructRequestSignals;
        private static NativeQueue<DeconstructResultSignal> _deconstructResultSignals;
        private static NativeQueue<ModuleDeconstructSignal> _moduleDeconstructSignals;
        private static NativeQueue<VitalWarningSignal> _vitalWarningSignals;
        private static NativeQueue<CrushWarningSignal> _crushWarningSignals;
        private static NativeQueue<VocalWarningSignal> _vocalWarningSignals;
        private static NativeQueue<SubtitleSignal> _subtitleSignals;
        private static NativeQueue<DataReloadSignal> _dataReloadSignals;
        private static NativeQueue<MemoryPressureSignal> _memoryPressureSignals;
        private static NativeQueue<AcousticPingSignal> _acousticPingSignals;
        private static NativeQueue<MovementAcousticSignal> _movementAcousticSignals;
        private static NativeQueue<SonarPingSignal> _sonarPingSignals;
        private static NativeQueue<HypoxiaSignal> _hypoxiaSignals;
        private static NativeQueue<OxygenCriticalSignal> _oxygenCriticalSignals;
        private static NativeQueue<InteractionUiSignal> _interactionUiSignals;
        private static NativeQueue<UIRescaleRequestSignal> _uiRescaleRequestSignals;
        private static NativeQueue<FluidIncursionSignal> _fluidIncursionSignals;
        private static NativeQueue<FluidDensityChangedSignal> _fluidDensityChangedSignals;
        private static NativeQueue<PipeRuptureSignal> _pipeRuptureSignals;
        private static NativeQueue<SpectrumScanSignal> _spectrumScanSignals;
        private static NativeQueue<RigidbodySleepSignal> _rigidbodySleepSignals;
        private static NativeQueue<ScannerToolActiveSignal> _scannerToolActiveSignals;
        private static NativeQueue<ScanCompleteSignal> _scanCompleteSignals;
        private static NativeQueue<BlueprintUnlockedSignal> _blueprintUnlockedSignals;
        private static NativeQueue<CraftingStartedSignal> _craftingStartedSignals;
        private static NativeQueue<CraftingCompletedSignal> _craftingCompletedSignals;
        private static NativeQueue<ToolStateChangedSignal> _toolStateChangedSignals;
        private static NativeQueue<ToolAcousticSignal> _toolAcousticSignals;
        private static NativeQueue<PowerDrainSignal> _powerDrainSignals;
        private static NativeQueue<ToolTriggerSignal> _toolTriggerSignals;
        private static NativeQueue<HUDNotificationSignal> _hudNotificationSignals;
        private static NativeQueue<ReconDataSignal> _reconDataSignals;
        private static NativeQueue<SaveLifecycleSignal> _saveLifecycleSignals;
        private static NativeQueue<ComplianceViolationSignal> _complianceViolationSignals;
        private static NativeQueue<GlobalTimeSyncSignal> _globalTimeSyncSignals;
        private static NativeQueue<SeismicSignal> _seismicSignals;
        private static NativeQueue<TimeDilationSignal> _timeDilationSignals;
        private static NativeQueue<SimulationPauseSignal> _simulationPauseSignals;
        private static NativeQueue<BulletTimeVisualSignal> _bulletTimeVisualSignals;
        private static NativeQueue<WeatherStrengthSignal> _weatherStrengthSignals;
        private static NativeQueue<ItemDecaySignal> _itemDecaySignals;
        private static NativeQueue<ItemAcquiredSignal> _itemAcquiredSignals;
        private static NativeQueue<RadiationDoseSignal> _radiationDoseSignals;
        private static NativeQueue<ResourceDepletionDeltaSignal> _resourceDepletionDeltaSignals;
        private static NativeQueue<LightLevelSignal> _lightLevelSignals;
        private static NativeQueue<SubmarineLightsChangedSignal> _submarineLightsChangedSignals;
        private static NativeQueue<FaunaStateChangedSignal> _faunaStateChangedSignals;
        private static NativeQueue<PhysiologyStateSignal> _physiologyStateSignals;
        private static NativeQueue<PlayerStressSignal> _playerStressSignals;
        private static NativeQueue<TraumaSignal> _traumaSignals;
        private static NativeQueue<ProgressionEventSignal> _progressionEventSignals;
        private static NativeQueue<GlobalWorldStateSignal> _globalWorldStateSignals;
        private static NativeQueue<BiomeChangedSignal> _biomeChangedSignals;
        private static NativeQueue<NarrativeFocusSignal> _narrativeFocusSignals;
        private static NativeQueue<FocusBrokenSignal> _focusBrokenSignals;
        private static NativeQueue<MixerStateSignal> _mixerStateSignals;
        private static NativeQueue<NarrativeHudWaypointSignal> _narrativeHudWaypointSignals;
        private static NativeQueue<SoundscapeProfileSignal> _soundscapeProfileSignals;
        private static NativeQueue<NarrativePoiStateSignal> _narrativePoiStateSignals;
        private static bool _initialized;
        private static CombatDamageSignal _latestDamageSignal;
        private static AcousticPingSignal _latestAcousticPingSignal;
        private static FluidDensityChangedSignal _latestFluidDensityChangedSignal;
        private static LightLevelSignal _latestLightLevelSignal;
        private static PhysiologyStateSignal _latestPhysiologyStateSignal;
        private static PlayerStressSignal _latestPlayerStressSignal;
        private static PlayerStateSignal _latestPlayerStateSignal;
        private static SeismicSignal _latestSeismicSignal;
        private static ScannerToolActiveSignal _latestScannerToolActiveSignal;
        private static ToolStateChangedSignal _latestToolStateChangedSignal;
        private static SurvivalVitalsChangedSignal _latestSurvivalDeathSignal;
        private static int _latestStorageDebtMilli;
        private static int _latestStorageLatencyMilli;
        private static int _latestStorageDebtSequence;
        private static int _latestDamageSignalSequence;
        private static int _latestAcousticPingSignalSequence;
        private static int _latestFluidDensityChangedSignalSequence;
        private static int _latestLightLevelSignalSequence;
        private static int _latestPhysiologyStateSignalSequence;
        private static int _latestPlayerStressSignalSequence;
        private static int _latestPlayerStateSignalSequence;
        private static int _latestSeismicSignalSequence;
        private static int _latestScannerToolActiveSignalSequence;
        private static int _latestToolStateChangedSignalSequence;
        private static int _latestSurvivalDeathSignalSequence;
        private static int _latestCraftingCompletedSignalSequence;
        private static int _latestCraftingCompletedUnitCount;
        private static int _timeDilationScalarMilli = 1000;
        private static int _timeDilationSequence;
        private static int _simulationPaused;
        private static int _bulletTimeVisualMilli;
        private static int _signalTelemetryCursor;

        public static float TimeDilationScalar => Volatile.Read(ref _timeDilationScalarMilli) * 0.001f;

        public static bool SimulationPaused => Volatile.Read(ref _simulationPaused) != 0;

        public static float BulletTimeVisualIntensity01 => Volatile.Read(ref _bulletTimeVisualMilli) * 0.001f;

        public static float LatestStorageDebt01 => math.saturate(Volatile.Read(ref _latestStorageDebtMilli) * 0.001f);

        public static float LatestStorageLatencyEwmaMs => math.max(0f, Volatile.Read(ref _latestStorageLatencyMilli));

        public static uint LatestStorageDebtSequence => unchecked((uint)Volatile.Read(ref _latestStorageDebtSequence));

        public static uint LatestCraftingCompletedSequence => unchecked((uint)Volatile.Read(ref _latestCraftingCompletedSignalSequence));

        public static uint LatestCraftingCompletedUnitCount => unchecked((uint)Volatile.Read(ref _latestCraftingCompletedUnitCount));

        /// <summary>Damage routing writer for Burst jobs or background producers.</summary>
        public static NativeQueue<CombatDamageSignal>.ParallelWriter DamageSignalWriter
        {
            get
            {
                EnsureInitialized();
                return SignalBus<CombatDamageSignal>.ParallelWriter;
            }
        }

        /// <summary>Physics impact writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ImpactSignal>.ParallelWriter ImpactSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _impactSignals.AsParallelWriter();
            }
        }

        /// <summary>AUP shift broadcast writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AupPreShiftSignal>.ParallelWriter AupPreShiftSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _aupPreShiftSignals.AsParallelWriter();
            }
        }

        /// <summary>AUP shift broadcast writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AupShiftSignal>.ParallelWriter AupShiftSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _aupShiftSignals.AsParallelWriter();
            }
        }

        /// <summary>Logistics brownout writer for Burst jobs or background producers.</summary>
        public static NativeQueue<BrownoutSignal>.ParallelWriter BrownoutSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _brownoutSignals.AsParallelWriter();
            }
        }

        /// <summary>Armor deflection writer for Burst combat jobs.</summary>
        public static NativeQueue<DeflectSignal>.ParallelWriter DeflectSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _deflectSignals.AsParallelWriter();
            }
        }

        /// <summary>Entity death writer for Burst producers.</summary>
        public static NativeQueue<EntityDeathSignal>.ParallelWriter EntityDeathSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _entityDeathSignals.AsParallelWriter();
            }
        }

        /// <summary>Runtime anomaly writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AnomalySignal>.ParallelWriter AnomalySignalWriter
        {
            get
            {
                EnsureInitialized();
                return _anomalySignals.AsParallelWriter();
            }
        }

        /// <summary>Acoustic ping writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AcousticPingSignal>.ParallelWriter AcousticPingSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _acousticPingSignals.AsParallelWriter();
            }
        }

        /// <summary>Movement acoustic writer for Burst jobs or background producers.</summary>
        public static NativeQueue<MovementAcousticSignal>.ParallelWriter MovementAcousticSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _movementAcousticSignals.AsParallelWriter();
            }
        }

        /// <summary>Hypoxia writer for Burst jobs or background producers.</summary>
        public static NativeQueue<HypoxiaSignal>.ParallelWriter HypoxiaSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _hypoxiaSignals.AsParallelWriter();
            }
        }

        /// <summary>Scan completion writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ScanCompleteSignal>.ParallelWriter ScanCompleteSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _scanCompleteSignals.AsParallelWriter();
            }
        }

        /// <summary>Blueprint unlock writer for Burst jobs or background producers.</summary>
        public static NativeQueue<BlueprintUnlockedSignal>.ParallelWriter BlueprintUnlockedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _blueprintUnlockedSignals.AsParallelWriter();
            }
        }

        /// <summary>Crafting-start writer for Burst jobs or background producers.</summary>
        public static NativeQueue<CraftingStartedSignal>.ParallelWriter CraftingStartedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _craftingStartedSignals.AsParallelWriter();
            }
        }

        /// <summary>Crafting-completed writer for Burst jobs or background producers.</summary>
        public static NativeQueue<CraftingCompletedSignal>.ParallelWriter CraftingCompletedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _craftingCompletedSignals.AsParallelWriter();
            }
        }

        /// <summary>Tool acoustic writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ToolAcousticSignal>.ParallelWriter ToolAcousticSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _toolAcousticSignals.AsParallelWriter();
            }
        }

        /// <summary>Tool state writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ToolStateChangedSignal>.ParallelWriter ToolStateChangedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _toolStateChangedSignals.AsParallelWriter();
            }
        }

        /// <summary>Power-drain writer for crafting and power-network producers.</summary>
        public static NativeQueue<PowerDrainSignal>.ParallelWriter PowerDrainSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _powerDrainSignals.AsParallelWriter();
            }
        }

        /// <summary>Habitat deconstruction request writer for Burst-capable tool producers.</summary>
        public static NativeQueue<DeconstructRequestSignal>.ParallelWriter DeconstructRequestSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _deconstructRequestSignals.AsParallelWriter();
            }
        }

        /// <summary>Habitat deconstruction result writer for validation/execution consumers.</summary>
        public static NativeQueue<DeconstructResultSignal>.ParallelWriter DeconstructResultSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _deconstructResultSignals.AsParallelWriter();
            }
        }

        /// <summary>Tool trigger writer for device bridge producers.</summary>
        public static NativeQueue<ToolTriggerSignal>.ParallelWriter ToolTriggerSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _toolTriggerSignals.AsParallelWriter();
            }
        }

        /// <summary>HUD notification writer for Burst jobs or background producers.</summary>
        public static NativeQueue<HUDNotificationSignal>.ParallelWriter HUDNotificationSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _hudNotificationSignals.AsParallelWriter();
            }
        }

        /// <summary>Rigidbody sleep-state writer for Burst jobs or background producers.</summary>
        public static NativeQueue<RigidbodySleepSignal>.ParallelWriter RigidbodySleepSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _rigidbodySleepSignals.AsParallelWriter();
            }
        }

        /// <summary>Fluid pipe rupture writer for Burst-backed graph bridges.</summary>
        public static NativeQueue<PipeRuptureSignal>.ParallelWriter PipeRuptureSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _pipeRuptureSignals.AsParallelWriter();
            }
        }

        /// <summary>Scanner-active writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ScannerToolActiveSignal>.ParallelWriter ScannerToolActiveSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _scannerToolActiveSignals.AsParallelWriter();
            }
        }

        /// <summary>Global time synchronization writer for Burst jobs or background producers.</summary>
        public static NativeQueue<GlobalTimeSyncSignal>.ParallelWriter GlobalTimeSyncSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _globalTimeSyncSignals.AsParallelWriter();
            }
        }

        /// <summary>Deterministic seismic shake writer for Burst jobs or background producers.</summary>
        public static NativeQueue<SeismicSignal>.ParallelWriter SeismicSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _seismicSignals.AsParallelWriter();
            }
        }

        /// <summary>Ore/resource yield writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ItemAcquiredSignal>.ParallelWriter ItemAcquiredSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _itemAcquiredSignals.AsParallelWriter();
            }
        }

        /// <summary>Radiation dose writer for physiology and hazard-grid producers.</summary>
        public static NativeQueue<RadiationDoseSignal>.ParallelWriter RadiationDoseSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _radiationDoseSignals.AsParallelWriter();
            }
        }

        /// <summary>Ore depletion delta writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ResourceDepletionDeltaSignal>.ParallelWriter ResourceDepletionDeltaSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _resourceDepletionDeltaSignals.AsParallelWriter();
            }
        }

        /// <summary>Narrative progression writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ProgressionEventSignal>.ParallelWriter ProgressionEventSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _progressionEventSignals.AsParallelWriter();
            }
        }

        /// <summary>Global narrative state writer for Burst jobs or background producers.</summary>
        public static NativeQueue<GlobalWorldStateSignal>.ParallelWriter GlobalWorldStateSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _globalWorldStateSignals.AsParallelWriter();
            }
        }

        /// <summary>Biome transition writer for Burst jobs or background producers.</summary>
        public static NativeQueue<BiomeChangedSignal>.ParallelWriter BiomeChangedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _biomeChangedSignals.AsParallelWriter();
            }
        }

        /// <summary>Crash/postmortem telemetry writer for watchdog producers.</summary>
        public static NativeQueue<CrashTelemetrySignal>.ParallelWriter CrashTelemetrySignalWriter
        {
            get
            {
                EnsureInitialized();
                return _crashTelemetrySignals.AsParallelWriter();
            }
        }

        /// <summary>Initializes every native signal lane during bootstrap prewarm.</summary>
        public static void InitializeAllQueues()
        {
            if (_initialized)
                return;

            SignalBusRegistry.SetLowTierMode(GlobalRegistry.ScalabilityTierProfileByte == 0);
            CreateQueue(ref _impactSignals, ImpactSignalCapacity, nameof(_impactSignals));
            CreateQueue(ref _aupPreShiftSignals, AupPreShiftSignalCapacity, nameof(_aupPreShiftSignals));
            CreateQueue(ref _aupShiftSignals, AupShiftSignalCapacity, nameof(_aupShiftSignals));
            CreateQueue(ref _brownoutSignals, BrownoutSignalCapacity, nameof(_brownoutSignals));
            CreateQueue(ref _debrisSpawnSignals, DebrisSpawnSignalCapacity, nameof(_debrisSpawnSignals));
            CreateQueue(ref _deflectSignals, DeflectSignalCapacity, nameof(_deflectSignals));
            CreateQueue(ref _entityDeathSignals, EntityDeathSignalCapacity, nameof(_entityDeathSignals));
            CreateQueue(ref _solarFlareSignals, SolarFlareSignalCapacity, nameof(_solarFlareSignals));
            CreateQueue(ref _rebaseSignals, RebaseSignalCapacity, nameof(_rebaseSignals));
            CreateQueue(ref _controlSignals, ControlSignalCapacity, nameof(_controlSignals));
            CreateQueue(ref _anomalySignals, AnomalySignalCapacity, nameof(_anomalySignals));
            CreateQueue(ref _telemetryAnomalySignals, TelemetryAnomalySignalCapacity, nameof(_telemetryAnomalySignals));
            CreateQueue(ref _crashTelemetrySignals, CrashTelemetrySignalCapacity, nameof(_crashTelemetrySignals));
            CreateQueue(ref _habitatConstructionSignals, HabitatConstructionSignalCapacity, nameof(_habitatConstructionSignals));
            CreateQueue(ref _deconstructRequestSignals, DeconstructRequestSignalCapacity, nameof(_deconstructRequestSignals));
            CreateQueue(ref _deconstructResultSignals, DeconstructResultSignalCapacity, nameof(_deconstructResultSignals));
            CreateQueue(ref _moduleDeconstructSignals, ModuleDeconstructSignalCapacity, nameof(_moduleDeconstructSignals));
            CreateQueue(ref _vitalWarningSignals, VitalWarningSignalCapacity, nameof(_vitalWarningSignals));
            CreateQueue(ref _crushWarningSignals, CrushWarningSignalCapacity, nameof(_crushWarningSignals));
            CreateQueue(ref _vocalWarningSignals, VocalWarningSignalCapacity, nameof(_vocalWarningSignals));
            CreateQueue(ref _subtitleSignals, SubtitleSignalCapacity, nameof(_subtitleSignals));
            CreateQueue(ref _dataReloadSignals, DataReloadSignalCapacity, nameof(_dataReloadSignals));
            CreateQueue(ref _memoryPressureSignals, MemoryPressureSignalCapacity, nameof(_memoryPressureSignals));
            CreateQueue(ref _acousticPingSignals, AcousticPingSignalCapacity, nameof(_acousticPingSignals));
            CreateQueue(ref _movementAcousticSignals, MovementAcousticSignalCapacity, nameof(_movementAcousticSignals));
            CreateQueue(ref _sonarPingSignals, SonarPingSignalCapacity, nameof(_sonarPingSignals));
            CreateQueue(ref _hypoxiaSignals, HypoxiaSignalCapacity, nameof(_hypoxiaSignals));
            CreateQueue(ref _oxygenCriticalSignals, OxygenCriticalSignalCapacity, nameof(_oxygenCriticalSignals));
            CreateQueue(ref _interactionUiSignals, InteractionUiSignalCapacity, nameof(_interactionUiSignals));
            CreateQueue(ref _uiRescaleRequestSignals, UIRescaleRequestSignalCapacity, nameof(_uiRescaleRequestSignals));
            CreateQueue(ref _fluidIncursionSignals, FluidIncursionSignalCapacity, nameof(_fluidIncursionSignals));
            CreateQueue(ref _fluidDensityChangedSignals, FluidDensityChangedSignalCapacity, nameof(_fluidDensityChangedSignals));
            CreateQueue(ref _pipeRuptureSignals, PipeRuptureSignalCapacity, nameof(_pipeRuptureSignals));
            CreateQueue(ref _spectrumScanSignals, SpectrumScanSignalCapacity, nameof(_spectrumScanSignals));
            CreateQueue(ref _rigidbodySleepSignals, RigidbodySleepSignalCapacity, nameof(_rigidbodySleepSignals));
            CreateQueue(ref _scannerToolActiveSignals, ScannerToolActiveSignalCapacity, nameof(_scannerToolActiveSignals));
            CreateQueue(ref _scanCompleteSignals, ScanCompleteSignalCapacity, nameof(_scanCompleteSignals));
            CreateQueue(ref _blueprintUnlockedSignals, BlueprintUnlockedSignalCapacity, nameof(_blueprintUnlockedSignals));
            CreateQueue(ref _craftingStartedSignals, CraftingStartedSignalCapacity, nameof(_craftingStartedSignals));
            CreateQueue(ref _craftingCompletedSignals, CraftingCompletedSignalCapacity, nameof(_craftingCompletedSignals));
            CreateQueue(ref _toolStateChangedSignals, ToolStateChangedSignalCapacity, nameof(_toolStateChangedSignals));
            CreateQueue(ref _toolAcousticSignals, ToolAcousticSignalCapacity, nameof(_toolAcousticSignals));
            CreateQueue(ref _powerDrainSignals, PowerDrainSignalCapacity, nameof(_powerDrainSignals));
            CreateQueue(ref _toolTriggerSignals, ToolTriggerSignalCapacity, nameof(_toolTriggerSignals));
            CreateQueue(ref _hudNotificationSignals, HUDNotificationSignalCapacity, nameof(_hudNotificationSignals));
            CreateQueue(ref _reconDataSignals, ReconDataSignalCapacity, nameof(_reconDataSignals));
            CreateQueue(ref _saveLifecycleSignals, SaveLifecycleSignalCapacity, nameof(_saveLifecycleSignals));
            CreateQueue(ref _complianceViolationSignals, ComplianceViolationSignalCapacity, nameof(_complianceViolationSignals));
            CreateQueue(ref _globalTimeSyncSignals, GlobalTimeSyncSignalCapacity, nameof(_globalTimeSyncSignals));
            CreateQueue(ref _seismicSignals, SeismicSignalCapacity, nameof(_seismicSignals));
            CreateQueue(ref _timeDilationSignals, TimeDilationSignalCapacity, nameof(_timeDilationSignals));
            CreateQueue(ref _simulationPauseSignals, SimulationPauseSignalCapacity, nameof(_simulationPauseSignals));
            CreateQueue(ref _bulletTimeVisualSignals, BulletTimeVisualSignalCapacity, nameof(_bulletTimeVisualSignals));
            CreateQueue(ref _weatherStrengthSignals, WeatherStrengthSignalCapacity, nameof(_weatherStrengthSignals));
            CreateQueue(ref _itemDecaySignals, ItemDecaySignalCapacity, nameof(_itemDecaySignals));
            CreateQueue(ref _itemAcquiredSignals, ItemAcquiredSignalCapacity, nameof(_itemAcquiredSignals));
            CreateQueue(ref _radiationDoseSignals, RadiationDoseSignalCapacity, nameof(_radiationDoseSignals));
            CreateQueue(ref _resourceDepletionDeltaSignals, ResourceDepletionDeltaSignalCapacity, nameof(_resourceDepletionDeltaSignals));
            CreateQueue(ref _lightLevelSignals, LightLevelSignalCapacity, nameof(_lightLevelSignals));
            CreateQueue(ref _submarineLightsChangedSignals, SubmarineLightsChangedSignalCapacity, nameof(_submarineLightsChangedSignals));
            CreateQueue(ref _faunaStateChangedSignals, FaunaStateChangedSignalCapacity, nameof(_faunaStateChangedSignals));
            CreateQueue(ref _physiologyStateSignals, PhysiologyStateSignalCapacity, nameof(_physiologyStateSignals));
            CreateQueue(ref _playerStressSignals, PlayerStressSignalCapacity, nameof(_playerStressSignals));
            CreateQueue(ref _traumaSignals, TraumaSignalCapacity, nameof(_traumaSignals));
            CreateQueue(ref _progressionEventSignals, ProgressionEventSignalCapacity, nameof(_progressionEventSignals));
            CreateQueue(ref _globalWorldStateSignals, GlobalWorldStateSignalCapacity, nameof(_globalWorldStateSignals));
            CreateQueue(ref _biomeChangedSignals, BiomeChangedSignalCapacity, nameof(_biomeChangedSignals));
            CreateQueue(ref _narrativeFocusSignals, NarrativeFocusSignalCapacity, nameof(_narrativeFocusSignals));
            CreateQueue(ref _focusBrokenSignals, FocusBrokenSignalCapacity, nameof(_focusBrokenSignals));
            CreateQueue(ref _mixerStateSignals, MixerStateSignalCapacity, nameof(_mixerStateSignals));
            CreateQueue(ref _narrativeHudWaypointSignals, NarrativeHudWaypointSignalCapacity, nameof(_narrativeHudWaypointSignals));
            CreateQueue(ref _soundscapeProfileSignals, SoundscapeProfileSignalCapacity, nameof(_soundscapeProfileSignals));
            CreateQueue(ref _narrativePoiStateSignals, NarrativePoiStateSignalCapacity, nameof(_narrativePoiStateSignals));
            InitializeCategorySignalLanes();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateSignalPayload<ImpactSignal>(64);
            ValidateSignalSize<HighSpeedImpactSignal>(96);
            ValidateSignalSize<HapticRequest>(32);
            ValidateSignalSize<PlayerStateSignal>(64);
            ValidateSignalSize<SurvivalVitalsChangedSignal>(32);
            ValidateSignalPayload<AupPreShiftSignal>(32);
            ValidateSignalPayload<AupShiftSignal>(32);
            ValidateSignalSize<DropPodLandedSignal>(64);
            ValidateSignalSize<PlayerLookTargetSignal>(160);
            ValidateSignalSize<BrownoutSignal>(32);
            ValidateSignalSize<DebrisSpawnSignal>(64);
            ValidateSignalSize<DeflectSignal>(32);
            ValidateSignalSize<EntityDeathSignal>(64);
            ValidateSignalSize<EntitySpawnSignal>(64);
            ValidateSignalSize<SolarFlareSignal>(32);
            ValidateSignalSize<RebaseSignal>(32);
            ValidateSignalSize<ControlSignal>(32);
            ValidateSignalPayload<AnomalySignal>(32);
            ValidateSignalSize<TelemetryAnomalySignal>(32);
            ValidateSignalSize<CrashTelemetrySignal>(32);
            ValidateSignalSize<HabitatConstructionSignal>(64);
            ValidateSignalSize<DeconstructRequestSignal>(128);
            ValidateSignalSize<DeconstructResultSignal>(64);
            ValidateSignalSize<ModuleDeconstructSignal>(64);
            ValidateSignalSize<VitalWarningSignal>(32);
            ValidateSignalSize<CrushWarningSignal>(32);
            ValidateSignalSize<VocalWarningSignal>(32);
            ValidateSignalSize<SubtitleSignal>(32);
            ValidateSignalSize<DataReloadSignal>(32);
            ValidateSignalSize<MemoryPressureSignal>(32);
            ValidateSignalSize<MemoryAddressShiftSignal>(32);
            ValidateSignalSize<ResolutionChangedSignal>(32);
            ValidateSignalSize<SystemHealthIndexSignal>(32);
            ValidateSignalPayload<AcousticPingSignal>(64);
            ValidateSignalPayload<MovementAcousticSignal>(64);
            ValidateSignalPayload<SwarmDispersedSignal>(64);
            ValidateSignalSize<MacroDatabaseSectorHydrationSignal>(32);
            ValidateSignalSize<WfcOutpostGeneratedSignal>(128);
            ValidateSignalSize<WfcOutpostStateChangedSignal>(32);
            ValidateSignalSize<WfcOutpostDoorPowerSignal>(96);
            ValidateSignalSize<SectorResidencyHydratedSignal>(64);
            ValidateSignalSize<SectorDehydratedSignal>(64);
            ValidateSignalSize<ChunkDehydratedSignal>(64);
            ValidateSignalSize<SonarPingSignal>(64);
            ValidateSignalPayload<HypoxiaSignal>(32);
            ValidateSignalSize<OxygenCriticalSignal>(32);
            ValidateSignalSize<InteractionUiSignal>(64);
            ValidateSignalSize<UIRescaleRequestSignal>(32);
            ValidateSignalSize<FluidIncursionSignal>(64);
            ValidateSignalSize<SubmarineFloodStateSignal>(64);
            ValidateSignalSize<FluidDensityChangedSignal>(64);
            ValidateSignalSize<PipeRuptureSignal>(64);
            ValidateSignalSize<SpectrumScanSignal>(32);
            ValidateSignalSize<RigidbodySleepSignal>(64);
            ValidateSignalSize<ScannerToolActiveSignal>(32);
            ValidateSignalPayload<ScanCompleteSignal>(64);
            ValidateSignalSize<LoreFragmentScannedSignal>(32);
            ValidateSignalSize<BlueprintUnlockedSignal>(32);
            ValidateSignalSize<CraftingStartedSignal>(32);
            ValidateSignalSize<CraftingCompletedSignal>(32);
            ValidateSignalSize<ToolStateChangedSignal>(32);
            ValidateSignalSize<ToolLoadoutChangedSignal>(32);
            ValidateSignalSize<ToolAcousticSignal>(32);
            ValidateSignalSize<PowerDrainSignal>(32);
            ValidateSignalSize<ToolTriggerSignal>(32);
            ValidateSignalSize<HUDNotificationSignal>(32);
            ValidateSignalSize<ReconDataSignal>(64);
            ValidateSignalSize<SaveLifecycleSignal>(32);
            ValidateSignalSize<ComplianceViolationSignal>(32);
            ValidateSignalSize<GlobalTimeSyncSignal>(32);
            ValidateSignalSize<LockstepSnapshotSignal>(32);
            ValidateSignalSize<SeismicSignal>(32);
            ValidateSignalSize<TimeDilationSignal>(32);
            ValidateSignalSize<SimulationPauseSignal>(32);
            ValidateSignalSize<BulletTimeVisualSignal>(32);
            ValidateSignalSize<WeatherStrengthSignal>(32);
            ValidateSignalSize<ItemDecaySignal>(64);
            ValidateSignalSize<ItemDurabilityChangedSignal>(32);
            ValidateSignalSize<ItemAcquiredSignal>(64);
            ValidateSignalSize<RadiationDoseSignal>(64);
            ValidateSignalSize<TemperatureChangedSignal>(64);
            ValidateSignalSize<ResourceDepletionDeltaSignal>(32);
            ValidateSignalSize<LightLevelSignal>(32);
            ValidateSignalSize<SubmarineLightsChangedSignal>(80);
            ValidateSignalSize<FaunaStateChangedSignal>(64);
            ValidateSignalSize<PhysiologyStateSignal>(32);
            ValidateSignalSize<PlayerStressSignal>(32);
            ValidateSignalSize<TraumaSignal>(32);
            ValidateSignalSize<WakeGeneratedSignal>(64);
            ValidateSignalSize<ProgressionEventSignal>(64);
            ValidateSignalSize<GlobalWorldStateSignal>(64);
            ValidateSignalSize<BiomeChangedSignal>(64);
            ValidateSignalSize<NarrativeFocusSignal>(80);
            ValidateSignalSize<FocusBrokenSignal>(32);
            ValidateSignalSize<MixerStateSignal>(32);
            ValidateSignalSize<DiegeticHudSignal>(32);
            ValidateSignalSize<NarrativeHudWaypointSignal>(64);
            ValidateSignalSize<SoundscapeProfileSignal>(64);
            ValidateSignalSize<NarrativePoiStateSignal>(32);
            ValidateSignalSize<CombatDamageSignal>(64);
            ValidateSignalSize<HullDeformedSignal>(64);
            ValidateSignalSize<BaseModuleCompromisedSignal>(64);
            ValidateSignalSize<PlayerBaseEnterSignal>(64);
            ValidateSignalSize<PlayerBaseExitSignal>(64);
            ValidateSignalSize<CameraPositionSignal>(32);
            ValidateSignalSize<CameraFrustumSignal>(64);
            ValidateSignalSize<WeatherChangedSignal>(32);
            ValidateSignalSize<SystemPauseSignal>(32);
            ValidateSignalSize<SimulationBucketSyncSignal>(32);
            ValidateSignalSize<FramePacingWarningSignal>(64);
            ValidateSignalSize<SaveRequestSignal>(32);
            ValidateSignalSize<SaveCompletedSignal>(32);
            ValidateSignalSize<SaveStatusSignal>(32);
            ValidateSignalSize<SaveMetadataReadySignal>(32);
            ValidateSignalSize<CpuStarvationSignal>(32);
            ValidateSignalSize<StorageDebtSignal>(32);
            ValidateSignalSize<StreamingTurbulenceSignal>(32);
            ValidateSignalSize<AtmosphericReentrySignal>(64);
            ValidateSignalSize<PrologueCompleteSignal>(64);
            ValidateSignalSize<ManualOverridePulledSignal>(64);
            ValidateSignalSize<CullingOverloadSignal>(32);
            ValidateSignalSize<PlayerActionProgressSignal>(32);
            ValidateSignalSize<PlayerActionCompletedSignal>(32);
            ValidateSignalSize<PlayerActionCancelledSignal>(32);
            ValidateSignalSize<ScanLogChangedSignal>(32);
            ValidateSignalSize<PdaExchangeStateChangedSignal>(32);
            ValidateSignalSize<VehicleUpgradesChangedSignal>(32);
            ValidateSignalSize<SystemHealthSignal>(48);
            ValidateSignalSize<FrameTimeSignal>(32);
            ValidateSignalSize<KillSwitchSignal>(32);
            ValidateSignalSize<ReentryVfxStateSignal>(64);
            ValidateSignalSize<VisorDropletSignal>(64);
            ValidateSignalSize<VisualFlareSignal>(32);
            ValidateSignalSize<TetherTensionSignal>(144);
            ValidateSignalSize<TetherSnappedSignal>(72);
            ValidateSignalSize<TetherFiredSignal>(40);
            ValidateSignalSize<VoxelCarveEvent>(128);
            ValidateSignalSize<DockingRequestSignal>(80);
            ValidateSignalSize<DockingCompleteSignal>(80);
            ValidateSignalSize<DockingFailedSignal>(80);
            ValidateSignalSize<AnomalyProximitySignal>(80);
            ValidateSignalSize<CompassCalibratedSignal>(32);
#endif

            _initialized = true;
        }

        /// <summary>Disposes every native signal lane. Call during clean application or session shutdown.</summary>
        public static void DisposeAllQueues()
        {
            DisposeQueue(ref _impactSignals, nameof(_impactSignals));
            DisposeQueue(ref _aupPreShiftSignals, nameof(_aupPreShiftSignals));
            DisposeQueue(ref _aupShiftSignals, nameof(_aupShiftSignals));
            DisposeQueue(ref _brownoutSignals, nameof(_brownoutSignals));
            DisposeQueue(ref _debrisSpawnSignals, nameof(_debrisSpawnSignals));
            DisposeQueue(ref _deflectSignals, nameof(_deflectSignals));
            DisposeQueue(ref _entityDeathSignals, nameof(_entityDeathSignals));
            DisposeQueue(ref _solarFlareSignals, nameof(_solarFlareSignals));
            DisposeQueue(ref _rebaseSignals, nameof(_rebaseSignals));
            DisposeQueue(ref _controlSignals, nameof(_controlSignals));
            DisposeQueue(ref _anomalySignals, nameof(_anomalySignals));
            DisposeQueue(ref _telemetryAnomalySignals, nameof(_telemetryAnomalySignals));
            DisposeQueue(ref _crashTelemetrySignals, nameof(_crashTelemetrySignals));
            DisposeQueue(ref _habitatConstructionSignals, nameof(_habitatConstructionSignals));
            DisposeQueue(ref _deconstructRequestSignals, nameof(_deconstructRequestSignals));
            DisposeQueue(ref _deconstructResultSignals, nameof(_deconstructResultSignals));
            DisposeQueue(ref _moduleDeconstructSignals, nameof(_moduleDeconstructSignals));
            DisposeQueue(ref _vitalWarningSignals, nameof(_vitalWarningSignals));
            DisposeQueue(ref _crushWarningSignals, nameof(_crushWarningSignals));
            DisposeQueue(ref _vocalWarningSignals, nameof(_vocalWarningSignals));
            DisposeQueue(ref _subtitleSignals, nameof(_subtitleSignals));
            DisposeQueue(ref _dataReloadSignals, nameof(_dataReloadSignals));
            DisposeQueue(ref _memoryPressureSignals, nameof(_memoryPressureSignals));
            DisposeQueue(ref _acousticPingSignals, nameof(_acousticPingSignals));
            DisposeQueue(ref _movementAcousticSignals, nameof(_movementAcousticSignals));
            DisposeQueue(ref _sonarPingSignals, nameof(_sonarPingSignals));
            DisposeQueue(ref _hypoxiaSignals, nameof(_hypoxiaSignals));
            DisposeQueue(ref _oxygenCriticalSignals, nameof(_oxygenCriticalSignals));
            DisposeQueue(ref _interactionUiSignals, nameof(_interactionUiSignals));
            DisposeQueue(ref _uiRescaleRequestSignals, nameof(_uiRescaleRequestSignals));
            DisposeQueue(ref _fluidIncursionSignals, nameof(_fluidIncursionSignals));
            DisposeQueue(ref _fluidDensityChangedSignals, nameof(_fluidDensityChangedSignals));
            DisposeQueue(ref _pipeRuptureSignals, nameof(_pipeRuptureSignals));
            DisposeQueue(ref _spectrumScanSignals, nameof(_spectrumScanSignals));
            DisposeQueue(ref _rigidbodySleepSignals, nameof(_rigidbodySleepSignals));
            DisposeQueue(ref _scannerToolActiveSignals, nameof(_scannerToolActiveSignals));
            DisposeQueue(ref _scanCompleteSignals, nameof(_scanCompleteSignals));
            DisposeQueue(ref _blueprintUnlockedSignals, nameof(_blueprintUnlockedSignals));
            DisposeQueue(ref _craftingStartedSignals, nameof(_craftingStartedSignals));
            DisposeQueue(ref _craftingCompletedSignals, nameof(_craftingCompletedSignals));
            DisposeQueue(ref _toolStateChangedSignals, nameof(_toolStateChangedSignals));
            DisposeQueue(ref _toolAcousticSignals, nameof(_toolAcousticSignals));
            DisposeQueue(ref _powerDrainSignals, nameof(_powerDrainSignals));
            DisposeQueue(ref _toolTriggerSignals, nameof(_toolTriggerSignals));
            DisposeQueue(ref _hudNotificationSignals, nameof(_hudNotificationSignals));
            DisposeQueue(ref _reconDataSignals, nameof(_reconDataSignals));
            DisposeQueue(ref _saveLifecycleSignals, nameof(_saveLifecycleSignals));
            DisposeQueue(ref _complianceViolationSignals, nameof(_complianceViolationSignals));
            DisposeQueue(ref _globalTimeSyncSignals, nameof(_globalTimeSyncSignals));
            DisposeQueue(ref _seismicSignals, nameof(_seismicSignals));
            DisposeQueue(ref _timeDilationSignals, nameof(_timeDilationSignals));
            DisposeQueue(ref _simulationPauseSignals, nameof(_simulationPauseSignals));
            DisposeQueue(ref _bulletTimeVisualSignals, nameof(_bulletTimeVisualSignals));
            DisposeQueue(ref _weatherStrengthSignals, nameof(_weatherStrengthSignals));
            DisposeQueue(ref _itemDecaySignals, nameof(_itemDecaySignals));
            DisposeQueue(ref _itemAcquiredSignals, nameof(_itemAcquiredSignals));
            DisposeQueue(ref _radiationDoseSignals, nameof(_radiationDoseSignals));
            DisposeQueue(ref _resourceDepletionDeltaSignals, nameof(_resourceDepletionDeltaSignals));
            DisposeQueue(ref _lightLevelSignals, nameof(_lightLevelSignals));
            DisposeQueue(ref _submarineLightsChangedSignals, nameof(_submarineLightsChangedSignals));
            DisposeQueue(ref _faunaStateChangedSignals, nameof(_faunaStateChangedSignals));
            DisposeQueue(ref _physiologyStateSignals, nameof(_physiologyStateSignals));
            DisposeQueue(ref _playerStressSignals, nameof(_playerStressSignals));
            DisposeQueue(ref _traumaSignals, nameof(_traumaSignals));
            DisposeQueue(ref _progressionEventSignals, nameof(_progressionEventSignals));
            DisposeQueue(ref _globalWorldStateSignals, nameof(_globalWorldStateSignals));
            DisposeQueue(ref _biomeChangedSignals, nameof(_biomeChangedSignals));
            DisposeQueue(ref _narrativeFocusSignals, nameof(_narrativeFocusSignals));
            DisposeQueue(ref _focusBrokenSignals, nameof(_focusBrokenSignals));
            DisposeQueue(ref _mixerStateSignals, nameof(_mixerStateSignals));
            DisposeQueue(ref _narrativeHudWaypointSignals, nameof(_narrativeHudWaypointSignals));
            DisposeQueue(ref _soundscapeProfileSignals, nameof(_soundscapeProfileSignals));
            DisposeQueue(ref _narrativePoiStateSignals, nameof(_narrativePoiStateSignals));
            SignalBusRegistry.DisposeAll();
            ClearLatestSignals();
            _initialized = false;
        }

        /// <summary>Flushes typed signal queues into frame snapshots at the PRE_SIMULATION boundary.</summary>
        public static void FlushPreSimulation()
        {
            EnsureInitialized();
            SignalBusRegistry.SetLowTierMode(GlobalRegistry.ScalabilityTierProfileByte == 0);
            SignalBusRegistry.SetSystemStress01(global::Hecton8.Core.HomeostasisBrain.SystemHealthIndex01);
            SignalBusRegistry.FlushPreSimulation();
            ApplyAupShiftSafety();
            ReportSignalLaneTelemetry();
        }

        /// <summary>Clears typed signal snapshots at the POST_SIMULATION boundary.</summary>
        public static void ClearPostSimulationSnapshots()
        {
            SignalBusRegistry.ClearPostSimulationSnapshots();
        }

        /// <summary>Queues one packet-native damage signal.</summary>
        public static void Publish(in CombatDamageSignal signal)
        {
            EnsureInitialized();
            CombatDamageSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _latestDamageSignal = sanitizedSignal;
            AdvanceSignalSequence(ref _latestDamageSignalSequence);
            SignalBus<CombatDamageSignal>.Push(in sanitizedSignal);
        }

        /// <summary>Queues one physics impact packet from the main thread.</summary>
        public static void Publish(in ImpactSignal signal)
        {
            EnsureInitialized();
            ImpactSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _impactSignals.Enqueue(sanitizedSignal);
            SignalBus<ImpactSignal>.Push(in sanitizedSignal);
        }

        /// <summary>Queues one high-speed kinematic CCD impact packet on the typed native lane.</summary>
        public static void Publish(in HighSpeedImpactSignal signal)
        {
            EnsureInitialized();
            SignalBus<HighSpeedImpactSignal>.Push(in signal);
        }

        /// <summary>Queues one haptic rupture request on the typed native lane.</summary>
        public static void Publish(in HapticRequest signal)
        {
            EnsureInitialized();
            SignalBus<HapticRequest>.Push(in signal);
        }

        /// <summary>Queues one player state packet on the typed native lane.</summary>
        public static void Publish(in PlayerStateSignal signal)
        {
            EnsureInitialized();
            _latestPlayerStateSignal = signal;
            AdvanceSignalSequence(ref _latestPlayerStateSignalSequence);
            SignalBus<PlayerStateSignal>.Push(in signal);
        }

        /// <summary>Queues one player survival-vitals dirty packet on the typed native lane.</summary>
        public static void Publish(in SurvivalVitalsChangedSignal signal)
        {
            EnsureInitialized();
            if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) != 0u)
            {
                _latestSurvivalDeathSignal = signal;
                AdvanceSignalSequence(ref _latestSurvivalDeathSignalSequence);
            }

            SignalBus<SurvivalVitalsChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one hull deformation VFX packet for downstream audio and feedback systems.</summary>
        public static void Publish(in HullDeformedSignal signal)
        {
            EnsureInitialized();
            SignalBus<HullDeformedSignal>.Push(in signal);
        }

        /// <summary>Queues one hull repair completion packet for atmosphere and VFX consumers.</summary>
        public static void Publish(in HullRepairedSignal signal)
        {
            EnsureInitialized();
            SignalBus<HullRepairedSignal>.Push(in signal);
        }

        public static void Publish(in BaseModuleCompromisedSignal signal)
        {
            EnsureInitialized();
            SignalBus<BaseModuleCompromisedSignal>.Push(in signal);
        }

        /// <summary>Queues one player-entered-base packet for habitat atmosphere hibernation gates.</summary>
        public static void Publish(in PlayerBaseEnterSignal signal)
        {
            EnsureInitialized();
            SignalBus<PlayerBaseEnterSignal>.Push(in signal);
        }

        /// <summary>Queues one player-exited-base packet for habitat atmosphere hibernation gates.</summary>
        public static void Publish(in PlayerBaseExitSignal signal)
        {
            EnsureInitialized();
            SignalBus<PlayerBaseExitSignal>.Push(in signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        public static void Publish(in AupPreShiftSignal signal)
        {
            EnsureInitialized();
            GlobalRegistry.JobAdmission?.SetAupBarrierActive(true);
            _aupPreShiftSignals.Enqueue(signal);
            SignalBus<AupPreShiftSignal>.Push(in signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        public static void Publish(in AupShiftSignal signal)
        {
            EnsureInitialized();
            GlobalRegistry.JobAdmission?.SetAupBarrierActive(false);
            _aupShiftSignals.Enqueue(signal);
            SignalBus<AupShiftSignal>.Push(in signal);
        }

        /// <summary>Queues one drop-pod landing anchor packet with AUP precision.</summary>
        public static void Publish(in DropPodLandedSignal signal)
        {
            EnsureInitialized();
            SignalBus<DropPodLandedSignal>.Push(in signal);
        }

        /// <summary>Queues one absolute-position temperature change packet.</summary>
        public static void Publish(in TemperatureChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<TemperatureChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one logistics brownout packet from the main thread.</summary>
        public static void Publish(in BrownoutSignal signal)
        {
            EnsureInitialized();
            _brownoutSignals.Enqueue(signal);
            SignalBus<BrownoutSignal>.Push(in signal);
        }

        /// <summary>Queues one ecosystem debris spawn packet from the main thread.</summary>
        public static void Publish(in DebrisSpawnSignal signal)
        {
            EnsureInitialized();
            _debrisSpawnSignals.Enqueue(signal);
            SignalBus<DebrisSpawnSignal>.Push(in signal);
        }

        /// <summary>Queues one armor deflection packet from the main thread.</summary>
        public static void Publish(in DeflectSignal signal)
        {
            EnsureInitialized();
            _deflectSignals.Enqueue(signal);
        }

        /// <summary>Queues one entity death packet from the main thread.</summary>
        public static void Publish(in EntityDeathSignal signal)
        {
            EnsureInitialized();
            _entityDeathSignals.Enqueue(signal);
            SignalBus<EntityDeathSignal>.Push(in signal);
        }

        /// <summary>Queues one entity spawn packet from the main thread.</summary>
        public static void Publish(in EntitySpawnSignal signal)
        {
            EnsureInitialized();
            SignalBus<EntitySpawnSignal>.Push(in signal);
        }

        /// <summary>Queues one narrative solar flare packet from the main thread.</summary>
        public static void Publish(in SolarFlareSignal signal)
        {
            EnsureInitialized();
            _solarFlareSignals.Enqueue(signal);
        }

        /// <summary>Queues one origin rebase packet from the main thread.</summary>
        public static void Publish(in RebaseSignal signal)
        {
            EnsureInitialized();
            _rebaseSignals.Enqueue(signal);
        }

        /// <summary>Queues one input control packet from the main thread.</summary>
        public static void Publish(in ControlSignal signal)
        {
            EnsureInitialized();
            _controlSignals.Enqueue(signal);
        }

        /// <summary>Queues one runtime anomaly packet from the main thread.</summary>
        public static void Publish(in AnomalySignal signal)
        {
            EnsureInitialized();
            _anomalySignals.Enqueue(signal);
        }

        /// <summary>Queues one telemetry anomaly packet from the main thread.</summary>
        public static void Publish(in TelemetryAnomalySignal signal)
        {
            EnsureInitialized();
            _telemetryAnomalySignals.Enqueue(signal);
        }

        /// <summary>Queues one postmortem crash telemetry packet from the main thread.</summary>
        public static void Publish(in CrashTelemetrySignal signal)
        {
            EnsureInitialized();
            _crashTelemetrySignals.Enqueue(signal);
        }

        /// <summary>Queues one habitat construction packet from the main thread.</summary>
        public static void Publish(in HabitatConstructionSignal signal)
        {
            EnsureInitialized();
            _habitatConstructionSignals.Enqueue(signal);
        }

        /// <summary>Queues one habitat deconstruction request packet from the main thread.</summary>
        public static void Publish(in DeconstructRequestSignal signal)
        {
            EnsureInitialized();
            _deconstructRequestSignals.Enqueue(signal);
        }

        /// <summary>Queues one habitat deconstruction result packet from the main thread.</summary>
        public static void Publish(in DeconstructResultSignal signal)
        {
            EnsureInitialized();
            _deconstructResultSignals.Enqueue(signal);
        }

        /// <summary>Queues one persistence-facing habitat deletion delta packet from the main thread.</summary>
        public static void Publish(in ModuleDeconstructSignal signal)
        {
            EnsureInitialized();
            _moduleDeconstructSignals.Enqueue(signal);
        }

        /// <summary>Queues one player-vital warning packet from the main thread.</summary>
        public static void Publish(in VitalWarningSignal signal)
        {
            EnsureInitialized();
            _vitalWarningSignals.Enqueue(signal);
        }

        /// <summary>Queues one crush-depth warning packet from the main thread.</summary>
        public static void Publish(in CrushWarningSignal signal)
        {
            EnsureInitialized();
            _crushWarningSignals.Enqueue(signal);
        }

        /// <summary>Queues one vocal warning packet from the main thread.</summary>
        public static void Publish(in VocalWarningSignal signal)
        {
            EnsureInitialized();
            _vocalWarningSignals.Enqueue(signal);
        }

        /// <summary>Queues one subtitle packet from the main thread.</summary>
        public static void Publish(in SubtitleSignal signal)
        {
            EnsureInitialized();
            _subtitleSignals.Enqueue(signal);
        }

        /// <summary>Queues one editor data reload packet from the main thread.</summary>
        public static void Publish(in DataReloadSignal signal)
        {
            EnsureInitialized();
            _dataReloadSignals.Enqueue(signal);
        }

        /// <summary>Queues one memory pressure packet from the main thread.</summary>
        public static void Publish(in MemoryPressureSignal signal)
        {
            EnsureInitialized();
            _memoryPressureSignals.Enqueue(signal);
            SignalBus<MemoryPressureSignal>.Push(in signal);
        }

        /// <summary>Queues one vault pointer relocation packet from the main thread.</summary>
        public static void Publish(in MemoryAddressShiftSignal signal)
        {
            EnsureInitialized();
            SignalBus<MemoryAddressShiftSignal>.Push(in signal);
        }

        /// <summary>Queues one runtime resolution/mip residency transition packet.</summary>
        public static void Publish(in ResolutionChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<ResolutionChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one homeostasis health-index packet.</summary>
        public static void Publish(in SystemHealthIndexSignal signal)
        {
            EnsureInitialized();
            SignalBus<SystemHealthIndexSignal>.Push(in signal);
        }

        /// <summary>Queues one CPU job-admission starvation diagnostic signal.</summary>
        public static void Publish(in CpuStarvationSignal signal)
        {
            EnsureInitialized();
            SignalBus<CpuStarvationSignal>.Push(in signal);
        }

        /// <summary>Queues one acoustic ping packet from the main thread.</summary>
        public static void Publish(in AcousticPingSignal signal)
        {
            EnsureInitialized();
            _latestAcousticPingSignal = signal;
            AdvanceSignalSequence(ref _latestAcousticPingSignalSequence);
            _acousticPingSignals.Enqueue(signal);
            SignalBus<AcousticPingSignal>.Push(in signal);
        }

        /// <summary>Queues one movement acoustic packet from the main thread.</summary>
        public static void Publish(in MovementAcousticSignal signal)
        {
            EnsureInitialized();
            _movementAcousticSignals.Enqueue(signal);
            SignalBus<MovementAcousticSignal>.Push(in signal);
        }

        /// <summary>Queues one swarm dispersion packet from the main thread.</summary>
        public static void Publish(in SwarmDispersedSignal signal)
        {
            EnsureInitialized();
            SignalBus<SwarmDispersedSignal>.Push(in signal);
        }

        /// <summary>Queues one sonar ping packet from the main thread.</summary>
        public static void Publish(in SonarPingSignal signal)
        {
            EnsureInitialized();
            _sonarPingSignals.Enqueue(signal);
        }

        /// <summary>Queues one hypoxia packet from the main thread.</summary>
        public static void Publish(in HypoxiaSignal signal)
        {
            EnsureInitialized();
            _hypoxiaSignals.Enqueue(signal);
        }

        /// <summary>Queues one oxygen critical packet from the main thread.</summary>
        public static void Publish(in OxygenCriticalSignal signal)
        {
            EnsureInitialized();
            _oxygenCriticalSignals.Enqueue(signal);
        }

        /// <summary>Queues one interaction UI packet from the main thread.</summary>
        public static void Publish(in InteractionUiSignal signal)
        {
            EnsureInitialized();
            _interactionUiSignals.Enqueue(signal);
        }

        /// <summary>Queues one UI rescale request packet from the main thread.</summary>
        public static void Publish(in UIRescaleRequestSignal signal)
        {
            EnsureInitialized();
            _uiRescaleRequestSignals.Enqueue(signal);
        }

        /// <summary>Queues one fluid incursion packet from the main thread.</summary>
        public static void Publish(in FluidIncursionSignal signal)
        {
            EnsureInitialized();
            _fluidIncursionSignals.Enqueue(signal);
        }

        /// <summary>Queues one submarine flood mass-state packet from the main thread.</summary>
        public static void Publish(in SubmarineFloodStateSignal signal)
        {
            EnsureInitialized();
            SignalBus<SubmarineFloodStateSignal>.Push(in signal);
        }

        /// <summary>Queues one fluid-density transition packet from the main thread.</summary>
        public static void Publish(in FluidDensityChangedSignal signal)
        {
            EnsureInitialized();
            _latestFluidDensityChangedSignal = signal;
            AdvanceSignalSequence(ref _latestFluidDensityChangedSignalSequence);
            _fluidDensityChangedSignals.Enqueue(signal);
        }

        /// <summary>Queues one fluid pipe rupture packet from the main thread.</summary>
        public static void Publish(in PipeRuptureSignal signal)
        {
            EnsureInitialized();
            _pipeRuptureSignals.Enqueue(signal);
        }

        /// <summary>Queues one spectrum scan packet from the main thread.</summary>
        public static void Publish(in SpectrumScanSignal signal)
        {
            EnsureInitialized();
            _spectrumScanSignals.Enqueue(signal);
        }

        /// <summary>Queues one rigidbody sleep packet from the main thread.</summary>
        public static void Publish(in RigidbodySleepSignal signal)
        {
            EnsureInitialized();
            _rigidbodySleepSignals.Enqueue(signal);
        }

        /// <summary>Queues one scanner-active packet from the main thread.</summary>
        public static void Publish(in ScannerToolActiveSignal signal)
        {
            EnsureInitialized();
            _latestScannerToolActiveSignal = signal;
            AdvanceSignalSequence(ref _latestScannerToolActiveSignalSequence);
            _scannerToolActiveSignals.Enqueue(signal);
            SignalBus<ScannerToolActiveSignal>.Push(in signal);
        }

        /// <summary>Queues one scan-complete packet from the main thread.</summary>
        public static void Publish(in ScanCompleteSignal signal)
        {
            EnsureInitialized();
            _scanCompleteSignals.Enqueue(signal);
        }

        /// <summary>Queues one lore-fragment scanned packet from the main thread.</summary>
        public static void Publish(in LoreFragmentScannedSignal signal)
        {
            EnsureInitialized();
            SignalBus<LoreFragmentScannedSignal>.Push(signal);
        }

        /// <summary>Queues one blueprint-unlocked packet from the main thread.</summary>
        public static void Publish(in BlueprintUnlockedSignal signal)
        {
            EnsureInitialized();
            _blueprintUnlockedSignals.Enqueue(signal);
        }

        /// <summary>Queues one crafting-started packet from the main thread.</summary>
        public static void Publish(in CraftingStartedSignal signal)
        {
            EnsureInitialized();
            _craftingStartedSignals.Enqueue(signal);
        }

        /// <summary>Queues one crafting-completed packet from the main thread.</summary>
        public static void Publish(in CraftingCompletedSignal signal)
        {
            EnsureInitialized();
            CraftingCompletedSignal sequencedSignal = signal;
            AdvanceSignalSequence(ref _latestCraftingCompletedSignalSequence);
            sequencedSignal.Sequence = unchecked((uint)Volatile.Read(ref _latestCraftingCompletedSignalSequence));
            if (sequencedSignal.Quantity > 0)
                AdvanceSignalCounter(ref _latestCraftingCompletedUnitCount, sequencedSignal.Quantity);

            _craftingCompletedSignals.Enqueue(sequencedSignal);
            SignalBus<CraftingCompletedSignal>.Push(in sequencedSignal);
        }

        /// <summary>Queues one tool runtime state packet from the main thread.</summary>
        public static void Publish(in ToolStateChangedSignal signal)
        {
            EnsureInitialized();
            _latestToolStateChangedSignal = signal;
            AdvanceSignalSequence(ref _latestToolStateChangedSignalSequence);
            _toolStateChangedSignals.Enqueue(signal);
        }

        /// <summary>Queues one player tool loadout or active-slot dirty packet.</summary>
        public static void Publish(in ToolLoadoutChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<ToolLoadoutChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one tool acoustic packet from the main thread.</summary>
        public static void Publish(in ToolAcousticSignal signal)
        {
            EnsureInitialized();
            _toolAcousticSignals.Enqueue(signal);
        }

        /// <summary>Queues one power-drain packet from the main thread.</summary>
        public static void Publish(in PowerDrainSignal signal)
        {
            EnsureInitialized();
            _powerDrainSignals.Enqueue(signal);
        }

        /// <summary>Queues one tool trigger packet from the main thread.</summary>
        public static void Publish(in ToolTriggerSignal signal)
        {
            EnsureInitialized();
            _toolTriggerSignals.Enqueue(signal);
        }

        /// <summary>Queues one HUD notification packet from the main thread.</summary>
        public static void Publish(in HUDNotificationSignal signal)
        {
            EnsureInitialized();
            _hudNotificationSignals.Enqueue(signal);
        }

        /// <summary>Queues one diegetic HUD prompt packet from the main thread.</summary>
        public static void Publish(in DiegeticHudSignal signal)
        {
            EnsureInitialized();
            SignalBus<DiegeticHudSignal>.Push(in signal);
        }

        /// <summary>Queues one cached platform thermal state packet.</summary>
        public static void Publish(in ThermalStateChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<ThermalStateChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one cached platform battery level packet.</summary>
        public static void Publish(in BatteryLevelSignal signal)
        {
            EnsureInitialized();
            SignalBus<BatteryLevelSignal>.Push(in signal);
        }

        /// <summary>Queues one inventory item durability update packet.</summary>
        public static void Publish(in ItemDurabilityChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<ItemDurabilityChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one player delayed-action progress packet.</summary>
        public static void Publish(in PlayerActionProgressSignal signal)
        {
            EnsureInitialized();
            SignalBus<PlayerActionProgressSignal>.Push(in signal);
        }

        /// <summary>Queues one player delayed-action completion packet.</summary>
        public static void Publish(in PlayerActionCompletedSignal signal)
        {
            EnsureInitialized();
            SignalBus<PlayerActionCompletedSignal>.Push(in signal);
        }

        /// <summary>Queues one player delayed-action cancellation packet.</summary>
        public static void Publish(in PlayerActionCancelledSignal signal)
        {
            EnsureInitialized();
            SignalBus<PlayerActionCancelledSignal>.Push(in signal);
        }

        /// <summary>Queues one scan-log mutation packet.</summary>
        public static void Publish(in ScanLogChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<ScanLogChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one PDA exchange dirty-state packet.</summary>
        public static void Publish(in PdaExchangeStateChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<PdaExchangeStateChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one vehicle upgrade bitmask mutation packet.</summary>
        public static void Publish(in VehicleUpgradesChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<VehicleUpgradesChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one storage IO backpressure scalar packet from the streaming service.</summary>
        public static void Publish(in StorageDebtSignal signal)
        {
            EnsureInitialized();
            Volatile.Write(ref _latestStorageDebtMilli, (int)math.round(math.saturate(signal.Debt01) * 1000f));
            Volatile.Write(ref _latestStorageLatencyMilli, (int)math.round(math.max(0f, signal.LatencyEwmaMs)));
            Volatile.Write(ref _latestStorageDebtSequence, unchecked((int)signal.Sequence));
            SignalBus<StorageDebtSignal>.Push(in signal);
        }

        /// <summary>Queues a visual-only turbulence cover-up packet when IO backpressure is high.</summary>
        public static void Publish(in StreamingTurbulenceSignal signal)
        {
            EnsureInitialized();
            SignalBus<StreamingTurbulenceSignal>.Push(in signal);
        }

        /// <summary>Queues one orbital prologue atmospheric re-entry state packet.</summary>
        public static void Publish(in AtmosphericReentrySignal signal)
        {
            EnsureInitialized();
            SignalBus<AtmosphericReentrySignal>.Push(in signal);
        }

        /// <summary>Queues one orbital prologue completion handoff packet.</summary>
        public static void Publish(in PrologueCompleteSignal signal)
        {
            EnsureInitialized();
            SignalBus<PrologueCompleteSignal>.Push(in signal);
        }

        /// <summary>Queues one physical cockpit manual override latch packet.</summary>
        public static void Publish(in ManualOverridePulledSignal signal)
        {
            EnsureInitialized();
            SignalBus<ManualOverridePulledSignal>.Push(in signal);
        }

        /// <summary>Queues one recon data packet from the main thread.</summary>
        public static void Publish(in ReconDataSignal signal)
        {
            EnsureInitialized();
            _reconDataSignals.Enqueue(signal);
        }

        /// <summary>Queues one save lifecycle packet from the main thread.</summary>
        public static void Publish(in SaveLifecycleSignal signal)
        {
            EnsureInitialized();
            _saveLifecycleSignals.Enqueue(signal);
        }

        /// <summary>Queues one macro database hydration packet on the typed native lane.</summary>
        public static void Publish(in MacroDatabaseSectorHydrationSignal signal)
        {
            EnsureInitialized();
            SignalBus<MacroDatabaseSectorHydrationSignal>.Push(in signal);
        }

        /// <summary>Queues one WFC outpost generation completion packet on the typed native lane.</summary>
        public static void Publish(in WfcOutpostGeneratedSignal signal)
        {
            EnsureInitialized();
            SignalBus<WfcOutpostGeneratedSignal>.Push(in signal);
        }

        /// <summary>Queues one WFC outpost mutable-cell state change on the typed native lane.</summary>
        public static void Publish(in WfcOutpostStateChangedSignal signal)
        {
            EnsureInitialized();
            SignalBus<WfcOutpostStateChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one WFC outpost door-power packet on the typed native lane.</summary>
        public static void Publish(in WfcOutpostDoorPowerSignal signal)
        {
            EnsureInitialized();
            SignalBus<WfcOutpostDoorPowerSignal>.Push(in signal);
        }

        /// <summary>Queues one compliance violation packet from the main thread.</summary>
        public static void Publish(in ComplianceViolationSignal signal)
        {
            EnsureInitialized();
            _complianceViolationSignals.Enqueue(signal);
        }

        /// <summary>Queues one global time sync packet from the main thread.</summary>
        public static void Publish(in GlobalTimeSyncSignal signal)
        {
            EnsureInitialized();
            _globalTimeSyncSignals.Enqueue(signal);
        }

        /// <summary>Queues one deterministic seismic/tide packet from the main thread.</summary>
        public static void Publish(in SeismicSignal signal)
        {
            EnsureInitialized();
            _latestSeismicSignal = signal;
            AdvanceSignalSequence(ref _latestSeismicSignalSequence);
            _seismicSignals.Enqueue(signal);
        }

        /// <summary>Queues one authoritative dispatcher time-dilation packet from the main thread.</summary>
        public static void Publish(in TimeDilationSignal signal)
        {
            EnsureInitialized();
            TimeDilationSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            Volatile.Write(ref _timeDilationScalarMilli, (int)math.round(math.max(0f, sanitizedSignal.Scalar) * 1000f));
            Volatile.Write(ref _timeDilationSequence, unchecked((int)sanitizedSignal.Sequence));
            _timeDilationSignals.Enqueue(sanitizedSignal);
        }

        /// <summary>Queues one pause/unpause packet from the main thread.</summary>
        public static void Publish(in SimulationPauseSignal signal)
        {
            EnsureInitialized();
            SimulationPauseSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            Volatile.Write(ref _simulationPaused, sanitizedSignal.Paused != 0 ? 1 : 0);
            _simulationPauseSignals.Enqueue(sanitizedSignal);
            SystemPauseSignal pauseSignal = default;
            pauseSignal.SourceHash = sanitizedSignal.SourceHash;
            pauseSignal.Frame = sanitizedSignal.Frame;
            pauseSignal.Sequence = sanitizedSignal.Sequence;
            pauseSignal.Paused = sanitizedSignal.Paused;
            pauseSignal.Flags = sanitizedSignal.Flags;
            pauseSignal.RestoreScalar = sanitizedSignal.RestoreScalar;
            SignalBus<SystemPauseSignal>.Push(in pauseSignal);
        }

        /// <summary>Queues one system-pause/input-lock packet without mutating simulation time state.</summary>
        public static void Publish(in SystemPauseSignal signal)
        {
            EnsureInitialized();
            SignalBus<SystemPauseSignal>.Push(in signal);
        }

        /// <summary>Queues one bullet-time post-process fake packet from the main thread.</summary>
        public static void Publish(in BulletTimeVisualSignal signal)
        {
            EnsureInitialized();
            BulletTimeVisualSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            Volatile.Write(ref _bulletTimeVisualMilli, (int)math.round(math.saturate(sanitizedSignal.Intensity01) * 1000f));
            _bulletTimeVisualSignals.Enqueue(sanitizedSignal);
        }

        /// <summary>Queues one weather strength packet from the main thread.</summary>
        public static void Publish(in WeatherStrengthSignal signal)
        {
            EnsureInitialized();
            WeatherStrengthSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _weatherStrengthSignals.Enqueue(sanitizedSignal);
            WeatherChangedSignal weatherSignal = default;
            weatherSignal.Strength01 = sanitizedSignal.Strength01;
            weatherSignal.FlowFieldScale = sanitizedSignal.FlowFieldScale;
            weatherSignal.PreviousWeatherHash = 0u;
            weatherSignal.WeatherHash = sanitizedSignal.WeatherHash;
            weatherSignal.Frame = sanitizedSignal.Frame;
            weatherSignal.QualityTier = GlobalRegistry.ScalabilityTierProfileByte;
            weatherSignal.Flags = sanitizedSignal.Flags;
            SignalBus<WeatherChangedSignal>.Push(in weatherSignal);
        }

        /// <summary>Queues one item decay packet from the main thread.</summary>
        public static void Publish(in ItemDecaySignal signal)
        {
            EnsureInitialized();
            _itemDecaySignals.Enqueue(signal);
        }

        /// <summary>Queues one resource-acquired packet from the main thread.</summary>
        public static void Publish(in ItemAcquiredSignal signal)
        {
            EnsureInitialized();
            _itemAcquiredSignals.Enqueue(signal);
            SignalBus<ItemAcquiredSignal>.Push(in signal);
        }

        /// <summary>Queues one radiation dose packet from the main thread.</summary>
        public static void Publish(in RadiationDoseSignal signal)
        {
            EnsureInitialized();
            _radiationDoseSignals.Enqueue(signal);
        }

        /// <summary>Queues one resource-depletion delta packet from the main thread.</summary>
        public static void Publish(in ResourceDepletionDeltaSignal signal)
        {
            EnsureInitialized();
            _resourceDepletionDeltaSignals.Enqueue(signal);
        }

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        public static void Push(in ItemAcquiredSignal signal) => SignalBus<ItemAcquiredSignal>.Push(in signal);

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        public static void Push(in RadiationDoseSignal signal) => SignalBus<RadiationDoseSignal>.Push(in signal);

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        public static void Push(in ResourceDepletionDeltaSignal signal) => SignalBus<ResourceDepletionDeltaSignal>.Push(in signal);

        /// <summary>Queues one player-light sample packet from the main thread.</summary>
        public static void Publish(in LightLevelSignal signal)
        {
            EnsureInitialized();
            _latestLightLevelSignal = signal;
            AdvanceSignalSequence(ref _latestLightLevelSignalSequence);
            _lightLevelSignals.Enqueue(signal);
        }

        /// <summary>Queues one player/submersible headlight state packet from the main thread.</summary>
        public static void Publish(in SubmarineLightsChangedSignal signal)
        {
            EnsureInitialized();
            _submarineLightsChangedSignals.Enqueue(signal);
        }

        /// <summary>Queues one fauna state transition packet from the main thread.</summary>
        public static void Publish(in FaunaStateChangedSignal signal)
        {
            EnsureInitialized();
            _faunaStateChangedSignals.Enqueue(signal);
            SignalBus<FaunaStateChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one physiology-state packet from the main thread.</summary>
        public static void Publish(in PhysiologyStateSignal signal)
        {
            EnsureInitialized();
            _latestPhysiologyStateSignal = signal;
            AdvanceSignalSequence(ref _latestPhysiologyStateSignalSequence);
            _physiologyStateSignals.Enqueue(signal);
        }

        /// <summary>Queues one player stress packet from the main thread.</summary>
        public static void Publish(in PlayerStressSignal signal)
        {
            EnsureInitialized();
            _latestPlayerStressSignal = signal;
            AdvanceSignalSequence(ref _latestPlayerStressSignalSequence);
            _playerStressSignals.Enqueue(signal);
        }

        /// <summary>Queues one player trauma packet from the main thread.</summary>
        public static void Publish(in TraumaSignal signal)
        {
            EnsureInitialized();
            _traumaSignals.Enqueue(signal);
        }

        /// <summary>Queues one procedural flora wake packet from the main thread.</summary>
        public static void Publish(in WakeGeneratedSignal signal)
        {
            EnsureInitialized();
            SignalBus<WakeGeneratedSignal>.Push(in signal);
        }

        /// <summary>Queues one bounded visual-fluid impulse for GPU advection consumers.</summary>
        public static void Publish(in FluidImpulseSignal signal)
        {
            EnsureInitialized();
            SignalBus<FluidImpulseSignal>.Push(in signal);
        }

        /// <summary>Queues one bounded submarine bubble-spawn marker for VFX consumers.</summary>
        public static void Publish(in BubbleSpawnSignal signal)
        {
            EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.Push(in signal);
        }

        /// <summary>Queues one narrative progression packet from the main thread.</summary>
        public static void Publish(in ProgressionEventSignal signal)
        {
            EnsureInitialized();
            _progressionEventSignals.Enqueue(signal);
        }

        /// <summary>Queues one AUP-independent global world-state mutation from the main thread.</summary>
        public static void Publish(in GlobalWorldStateSignal signal)
        {
            EnsureInitialized();
            _globalWorldStateSignals.Enqueue(signal);
        }

        /// <summary>Queues one biome transition packet from the main thread.</summary>
        public static void Publish(in BiomeChangedSignal signal)
        {
            EnsureInitialized();
            _biomeChangedSignals.Enqueue(signal);
            SignalBus<BiomeChangedSignal>.Push(in signal);
        }

        /// <summary>Queues one procedural narrative camera focus packet from the main thread.</summary>
        public static void Publish(in NarrativeFocusSignal signal)
        {
            EnsureInitialized();
            _narrativeFocusSignals.Enqueue(signal);
        }

        /// <summary>Queues one player-authored focus break packet from the main thread.</summary>
        public static void Publish(in FocusBrokenSignal signal)
        {
            EnsureInitialized();
            _focusBrokenSignals.Enqueue(signal);
        }

        /// <summary>Queues one mixer-state request packet from the main thread.</summary>
        public static void Publish(in MixerStateSignal signal)
        {
            EnsureInitialized();
            _mixerStateSignals.Enqueue(signal);
        }

        /// <summary>Queues one diegetic narrative waypoint packet from the main thread.</summary>
        public static void Publish(in NarrativeHudWaypointSignal signal)
        {
            EnsureInitialized();
            _narrativeHudWaypointSignals.Enqueue(signal);
        }

        /// <summary>Queues one soundscape profile handoff packet from the main thread.</summary>
        public static void Publish(in SoundscapeProfileSignal signal)
        {
            EnsureInitialized();
            _soundscapeProfileSignals.Enqueue(signal);
        }

        /// <summary>Queues one narrative POI save-state packet from the main thread.</summary>
        public static void Publish(in NarrativePoiStateSignal signal)
        {
            EnsureInitialized();
            _narrativePoiStateSignals.Enqueue(signal);
        }

        public static bool TryDequeueImpact(out ImpactSignal signal) => TryDequeue(ref _impactSignals, out signal);
        public static bool TryDequeueAupPreShift(out AupPreShiftSignal signal) => TryDequeue(ref _aupPreShiftSignals, out signal);
        public static bool TryDequeueAupShift(out AupShiftSignal signal) => TryDequeue(ref _aupShiftSignals, out signal);
        public static bool TryDequeueDropPodLanded(out DropPodLandedSignal signal) => SignalBus<DropPodLandedSignal>.TryReadFrame(out signal);
        public static bool TryDequeueBrownout(out BrownoutSignal signal) => TryDequeue(ref _brownoutSignals, out signal);
        public static bool TryDequeueDebrisSpawn(out DebrisSpawnSignal signal) => TryDequeue(ref _debrisSpawnSignals, out signal);
        public static bool TryDequeueDeflect(out DeflectSignal signal) => TryDequeue(ref _deflectSignals, out signal);
        public static bool TryDequeueEntityDeath(out EntityDeathSignal signal) => TryDequeue(ref _entityDeathSignals, out signal);
        public static bool TryDequeueSolarFlare(out SolarFlareSignal signal) => TryDequeue(ref _solarFlareSignals, out signal);
        public static bool TryDequeueRebase(out RebaseSignal signal) => TryDequeue(ref _rebaseSignals, out signal);
        public static bool TryDequeueControl(out ControlSignal signal) => TryDequeue(ref _controlSignals, out signal);
        public static bool TryDequeueAnomaly(out AnomalySignal signal) => TryDequeue(ref _anomalySignals, out signal);
        public static bool TryDequeueTelemetryAnomaly(out TelemetryAnomalySignal signal) => TryDequeue(ref _telemetryAnomalySignals, out signal);
        public static bool TryDequeueCrashTelemetry(out CrashTelemetrySignal signal) => TryDequeue(ref _crashTelemetrySignals, out signal);
        public static bool TryDequeueHabitatConstruction(out HabitatConstructionSignal signal) => TryDequeue(ref _habitatConstructionSignals, out signal);
        public static bool TryDequeueDeconstructRequest(out DeconstructRequestSignal signal) => TryDequeue(ref _deconstructRequestSignals, out signal);
        public static bool TryDequeueDeconstructResult(out DeconstructResultSignal signal) => TryDequeue(ref _deconstructResultSignals, out signal);
        public static bool TryDequeueModuleDeconstruct(out ModuleDeconstructSignal signal) => TryDequeue(ref _moduleDeconstructSignals, out signal);
        public static bool TryDequeueVitalWarning(out VitalWarningSignal signal) => TryDequeue(ref _vitalWarningSignals, out signal);
        public static bool TryDequeueCrushWarning(out CrushWarningSignal signal) => TryDequeue(ref _crushWarningSignals, out signal);
        public static bool TryDequeueVocalWarning(out VocalWarningSignal signal) => TryDequeue(ref _vocalWarningSignals, out signal);
        public static bool TryDequeueSubtitle(out SubtitleSignal signal) => TryDequeue(ref _subtitleSignals, out signal);
        public static bool TryDequeueDataReload(out DataReloadSignal signal) => TryDequeue(ref _dataReloadSignals, out signal);
        public static bool TryDequeueMemoryPressure(out MemoryPressureSignal signal) => TryDequeue(ref _memoryPressureSignals, out signal);
        public static bool TryDequeueAcousticPing(out AcousticPingSignal signal) => TryDequeue(ref _acousticPingSignals, out signal);
        public static bool TryDequeueMovementAcoustic(out MovementAcousticSignal signal) => TryDequeue(ref _movementAcousticSignals, out signal);
        public static bool TryDequeueSonarPing(out SonarPingSignal signal) => TryDequeue(ref _sonarPingSignals, out signal);
        public static bool TryDequeueHypoxia(out HypoxiaSignal signal) => TryDequeue(ref _hypoxiaSignals, out signal);
        public static bool TryDequeueOxygenCritical(out OxygenCriticalSignal signal) => TryDequeue(ref _oxygenCriticalSignals, out signal);
        public static bool TryDequeueInteractionUi(out InteractionUiSignal signal) => TryDequeue(ref _interactionUiSignals, out signal);
        public static bool TryDequeueUIRescaleRequest(out UIRescaleRequestSignal signal) => TryDequeue(ref _uiRescaleRequestSignals, out signal);
        public static bool TryDequeueFluidIncursion(out FluidIncursionSignal signal) => TryDequeue(ref _fluidIncursionSignals, out signal);
        public static bool TryDequeueFluidDensityChanged(out FluidDensityChangedSignal signal) => TryDequeue(ref _fluidDensityChangedSignals, out signal);
        public static bool TryDequeuePipeRupture(out PipeRuptureSignal signal) => TryDequeue(ref _pipeRuptureSignals, out signal);
        public static bool TryDequeueSpectrumScan(out SpectrumScanSignal signal) => TryDequeue(ref _spectrumScanSignals, out signal);
        public static bool TryDequeueRigidbodySleep(out RigidbodySleepSignal signal) => TryDequeue(ref _rigidbodySleepSignals, out signal);
        public static bool TryDequeueScannerToolActive(out ScannerToolActiveSignal signal) => TryDequeue(ref _scannerToolActiveSignals, out signal);
        public static bool TryDequeueScanComplete(out ScanCompleteSignal signal) => TryDequeue(ref _scanCompleteSignals, out signal);
        public static bool TryDequeueLoreFragmentScanned(out LoreFragmentScannedSignal signal) => SignalBus<LoreFragmentScannedSignal>.TryReadFrame(out signal);
        public static bool TryDequeueBlueprintUnlocked(out BlueprintUnlockedSignal signal) => TryDequeue(ref _blueprintUnlockedSignals, out signal);
        public static bool TryDequeueCraftingStarted(out CraftingStartedSignal signal) => TryDequeue(ref _craftingStartedSignals, out signal);
        public static bool TryDequeueCraftingCompleted(out CraftingCompletedSignal signal) => TryDequeue(ref _craftingCompletedSignals, out signal);
        public static bool TryDequeueToolStateChanged(out ToolStateChangedSignal signal) => TryDequeue(ref _toolStateChangedSignals, out signal);
        public static bool TryDequeueToolAcoustic(out ToolAcousticSignal signal) => TryDequeue(ref _toolAcousticSignals, out signal);
        public static bool TryDequeuePowerDrain(out PowerDrainSignal signal) => TryDequeue(ref _powerDrainSignals, out signal);
        public static bool TryDequeueToolTrigger(out ToolTriggerSignal signal) => TryDequeue(ref _toolTriggerSignals, out signal);
        public static bool TryDequeueHUDNotification(out HUDNotificationSignal signal) => TryDequeue(ref _hudNotificationSignals, out signal);
        public static bool TryDequeueStorageDebt(out StorageDebtSignal signal) => SignalBus<StorageDebtSignal>.TryReadFrame(out signal);
        public static bool TryDequeueStreamingTurbulence(out StreamingTurbulenceSignal signal) => SignalBus<StreamingTurbulenceSignal>.TryReadFrame(out signal);
        public static bool TryDequeueAtmosphericReentry(out AtmosphericReentrySignal signal) => SignalBus<AtmosphericReentrySignal>.TryReadFrame(out signal);
        public static bool TryDequeuePrologueComplete(out PrologueCompleteSignal signal) => SignalBus<PrologueCompleteSignal>.TryReadFrame(out signal);
        public static bool TryDequeueManualOverridePulled(out ManualOverridePulledSignal signal) => SignalBus<ManualOverridePulledSignal>.TryReadFrame(out signal);
        public static bool TryDequeueDiegeticHud(out DiegeticHudSignal signal) => SignalBus<DiegeticHudSignal>.TryReadFrame(out signal);
        public static bool TryDequeueReconData(out ReconDataSignal signal) => TryDequeue(ref _reconDataSignals, out signal);
        public static bool TryDequeueSaveLifecycle(out SaveLifecycleSignal signal) => TryDequeue(ref _saveLifecycleSignals, out signal);
        public static bool TryDequeueComplianceViolation(out ComplianceViolationSignal signal) => TryDequeue(ref _complianceViolationSignals, out signal);
        public static bool TryDequeueGlobalTimeSync(out GlobalTimeSyncSignal signal) => TryDequeue(ref _globalTimeSyncSignals, out signal);
        public static bool TryDequeueSeismic(out SeismicSignal signal) => TryDequeue(ref _seismicSignals, out signal);
        public static bool TryDequeueTimeDilation(out TimeDilationSignal signal) => TryDequeue(ref _timeDilationSignals, out signal);
        public static bool TryDequeueSimulationPause(out SimulationPauseSignal signal) => TryDequeue(ref _simulationPauseSignals, out signal);
        public static bool TryDequeueHapticRequest(out HapticRequest signal) => SignalBus<HapticRequest>.TryReadFrame(out signal);
        public static bool TryDequeueSubmarineFloodState(out SubmarineFloodStateSignal signal) => SignalBus<SubmarineFloodStateSignal>.TryReadFrame(out signal);
        public static bool TryDequeueBulletTimeVisual(out BulletTimeVisualSignal signal) => TryDequeue(ref _bulletTimeVisualSignals, out signal);
        public static bool TryDequeueWeatherStrength(out WeatherStrengthSignal signal) => TryDequeue(ref _weatherStrengthSignals, out signal);
        public static bool TryDequeueItemDecay(out ItemDecaySignal signal) => TryDequeue(ref _itemDecaySignals, out signal);
        public static bool TryDequeueItemAcquired(out ItemAcquiredSignal signal) => TryDequeue(ref _itemAcquiredSignals, out signal);
        public static bool TryDequeueRadiationDose(out RadiationDoseSignal signal) => TryDequeue(ref _radiationDoseSignals, out signal);
        public static bool TryDequeueResourceDepletionDelta(out ResourceDepletionDeltaSignal signal) => TryDequeue(ref _resourceDepletionDeltaSignals, out signal);
        public static bool TryDequeueLightLevel(out LightLevelSignal signal) => TryDequeue(ref _lightLevelSignals, out signal);
        public static bool TryDequeueSubmarineLightsChanged(out SubmarineLightsChangedSignal signal) => TryDequeue(ref _submarineLightsChangedSignals, out signal);
        public static bool TryDequeueFaunaStateChanged(out FaunaStateChangedSignal signal) => TryDequeue(ref _faunaStateChangedSignals, out signal);
        public static bool TryDequeuePhysiologyState(out PhysiologyStateSignal signal) => TryDequeue(ref _physiologyStateSignals, out signal);
        public static bool TryDequeuePlayerStress(out PlayerStressSignal signal) => TryDequeue(ref _playerStressSignals, out signal);
        public static bool TryDequeuePlayerState(out PlayerStateSignal signal) => SignalBus<PlayerStateSignal>.TryReadFrame(out signal);
        public static bool TryDequeueTrauma(out TraumaSignal signal) => TryDequeue(ref _traumaSignals, out signal);
        public static bool TryDequeueProgressionEvent(out ProgressionEventSignal signal) => TryDequeue(ref _progressionEventSignals, out signal);
        public static bool TryDequeueGlobalWorldState(out GlobalWorldStateSignal signal) => TryDequeue(ref _globalWorldStateSignals, out signal);
        public static bool TryDequeueBiomeChanged(out BiomeChangedSignal signal) => TryDequeue(ref _biomeChangedSignals, out signal);
        public static bool TryDequeueNarrativeFocus(out NarrativeFocusSignal signal) => TryDequeue(ref _narrativeFocusSignals, out signal);
        public static bool TryDequeueFocusBroken(out FocusBrokenSignal signal) => TryDequeue(ref _focusBrokenSignals, out signal);
        public static bool TryDequeueMixerState(out MixerStateSignal signal) => TryDequeue(ref _mixerStateSignals, out signal);
        public static bool TryDequeueNarrativeHudWaypoint(out NarrativeHudWaypointSignal signal) => TryDequeue(ref _narrativeHudWaypointSignals, out signal);
        public static bool TryDequeueSoundscapeProfile(out SoundscapeProfileSignal signal) => TryDequeue(ref _soundscapeProfileSignals, out signal);
        public static bool TryDequeueNarrativePoiState(out NarrativePoiStateSignal signal) => TryDequeue(ref _narrativePoiStateSignals, out signal);

        public static bool TryGetLatestDamageSignal(out CombatDamageSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestDamageSignalSequence);
            signal = _latestDamageSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestAcousticPingSignal(out AcousticPingSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestAcousticPingSignalSequence);
            signal = _latestAcousticPingSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestFluidDensityChangedSignal(out FluidDensityChangedSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestFluidDensityChangedSignalSequence);
            signal = _latestFluidDensityChangedSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestLightLevelSignal(out LightLevelSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestLightLevelSignalSequence);
            signal = _latestLightLevelSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestPlayerStressSignal(out PlayerStressSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestPlayerStressSignalSequence);
            signal = _latestPlayerStressSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestPlayerStateSignal(out PlayerStateSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestPlayerStateSignalSequence);
            signal = _latestPlayerStateSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestPhysiologyStateSignal(out PhysiologyStateSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestPhysiologyStateSignalSequence);
            signal = _latestPhysiologyStateSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestSurvivalDeathSignal(out SurvivalVitalsChangedSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestSurvivalDeathSignalSequence);
            signal = _latestSurvivalDeathSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestSeismicSignal(out SeismicSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestSeismicSignalSequence);
            signal = _latestSeismicSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestScannerToolActiveSignal(out ScannerToolActiveSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestScannerToolActiveSignalSequence);
            signal = _latestScannerToolActiveSignal;
            return sequence != 0;
        }

        public static bool TryGetLatestToolStateChangedSignal(out ToolStateChangedSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestToolStateChangedSignalSequence);
            signal = _latestToolStateChangedSignal;
            return sequence != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAllQueues();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAllQueues;
            Application.quitting += DisposeAllQueues;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAllQueues();
        }

        private static void ApplyAupShiftSafety()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 shiftMeters = shifts[i].ShiftMeters;
                if (!math.all(math.isfinite(shiftMeters)))
                    continue;

                CombatDamageSignalAupShiftTransformer transformer = default;
                transformer.SetShift(shiftMeters);
                SignalBus<CombatDamageSignal>.TransformSnapshot(transformer);
            }
        }

        private static void ReportSignalLaneTelemetry()
        {
            int laneCount = SignalBusRegistry.LaneCount;
            if (laneCount <= 0)
                return;

            if (SignalBusRegistry.RegistrationOverflow)
            {
                CrashTelemetryBuffer.ReportSignalLaneStats(
                    ComputeStableSignalLaneHash(nameof(SignalBusRegistry)),
                    laneCount,
                    0,
                    1);
            }

            int startIndex = Volatile.Read(ref _signalTelemetryCursor);
            if ((uint)startIndex >= (uint)laneCount)
                startIndex = 0;

            int sampledNonCritical = 0;
            for (int pass = 0; pass < laneCount; pass++)
            {
                int laneIndex = startIndex + pass;
                if (laneIndex >= laneCount)
                    laneIndex -= laneCount;

                ISignalLane lane = SignalBusRegistry.GetLaneAt(laneIndex);
                if (lane == null)
                    continue;

                int snapshotCount = lane.SnapshotCount;
                int droppedCount = lane.DroppedLastFlush;
                if (snapshotCount <= 0 && droppedCount <= 0)
                    continue;

                bool critical = droppedCount > 0 || lane.StormDetectedLastFlush;
                if (!critical && sampledNonCritical >= SignalTelemetryLaneBudgetPerFrame)
                    continue;

                CrashTelemetryBuffer.ReportSignalLaneStats(
                    lane.LaneHash,
                    lane.QueuedBeforeFlush,
                    snapshotCount,
                    droppedCount);

                if (!critical)
                    sampledNonCritical++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (lane.StormDetectedLastFlush)
                    Debug.LogWarning("[SIGNAL STORM DETECTED]");
#endif
            }

            int nextIndex = startIndex + SignalTelemetryLaneBudgetPerFrame;
            if (nextIndex >= laneCount)
                nextIndex %= laneCount;

            Volatile.Write(ref _signalTelemetryCursor, nextIndex);
        }

        private static uint ComputeStableSignalLaneHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldEntityIdToSourceId(ulong entityId)
        {
            uint hash = unchecked((uint)entityId ^ (uint)(entityId >> 32));
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash == 0u ? 1u : hash;
        }

        private static void InitializeCategorySignalLanes()
        {
            SignalBus<InputStateSignal>.Configure(InputStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InputStateSignal)));
            SignalBus<InputStateSignal>.EnsureInitialized();
            SignalBus<PlayerInputSignal>.Configure(PlayerInputSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerInputSignal)));
            SignalBus<PlayerInputSignal>.EnsureInitialized();
            SignalBus<PlayerLookTargetSignal>.Configure(PlayerLookTargetSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerLookTargetSignal)));
            SignalBus<PlayerLookTargetSignal>.EnsureInitialized();
            SignalBus<CombatDamageSignal>.Configure(DamageSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CombatDamageSignal)));
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<ImpactSignal>.Configure(ImpactSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ImpactSignal)));
            SignalBus<ImpactSignal>.EnsureInitialized();
            SignalBus<HullDeformedSignal>.Configure(HullDeformedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(HullDeformedSignal)));
            SignalBus<HullDeformedSignal>.EnsureInitialized();
            SignalBus<HullRepairedSignal>.Configure(HullRepairedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(HullRepairedSignal)));
            SignalBus<HullRepairedSignal>.EnsureInitialized();
            SignalBus<BaseModuleCompromisedSignal>.Configure(BaseModuleCompromisedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BaseModuleCompromisedSignal)));
            SignalBus<BaseModuleCompromisedSignal>.EnsureInitialized();
            SignalBus<PlayerBaseEnterSignal>.Configure(PlayerBaseTransitionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerBaseEnterSignal)));
            SignalBus<PlayerBaseEnterSignal>.EnsureInitialized();
            SignalBus<PlayerBaseExitSignal>.Configure(PlayerBaseTransitionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerBaseExitSignal)));
            SignalBus<PlayerBaseExitSignal>.EnsureInitialized();
            SignalBus<HighSpeedImpactSignal>.Configure(HighSpeedImpactSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(HighSpeedImpactSignal)));
            SignalBus<HighSpeedImpactSignal>.EnsureInitialized();
            SignalBus<AupPreShiftSignal>.Configure(AupPreShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AupPreShiftSignal)));
            SignalBus<AupPreShiftSignal>.EnsureInitialized();
            SignalBus<AupShiftSignal>.Configure(AupShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AupShiftSignal)));
            SignalBus<AupShiftSignal>.EnsureInitialized();
            SignalBus<EntityDeathSignal>.Configure(EntityDeathSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(EntityDeathSignal)));
            SignalBus<EntityDeathSignal>.EnsureInitialized();
            SignalBus<EntitySpawnSignal>.Configure(EntitySpawnSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(EntitySpawnSignal)));
            SignalBus<EntitySpawnSignal>.EnsureInitialized();
            SignalBus<FaunaStateChangedSignal>.Configure(FaunaStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(FaunaStateChangedSignal)));
            SignalBus<FaunaStateChangedSignal>.EnsureInitialized();
            SignalBus<WakeGeneratedSignal>.Configure(WakeGeneratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WakeGeneratedSignal)));
            SignalBus<WakeGeneratedSignal>.EnsureInitialized();
            SignalBus<MemoryPressureSignal>.Configure(MemoryPressureSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MemoryPressureSignal)));
            SignalBus<MemoryPressureSignal>.EnsureInitialized();
            SignalBus<HapticRequest>.Configure(HapticRequestCapacity, laneHash: ComputeStableSignalLaneHash(nameof(HapticRequest)));
            SignalBus<HapticRequest>.EnsureInitialized();
            SignalBus<ThermalStateChangedSignal>.Configure(ThermalStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ThermalStateChangedSignal)));
            SignalBus<ThermalStateChangedSignal>.EnsureInitialized();
            SignalBus<BatteryLevelSignal>.Configure(BatteryLevelSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BatteryLevelSignal)));
            SignalBus<BatteryLevelSignal>.EnsureInitialized();
            SignalBus<PlayerStateSignal>.Configure(PlayerStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerStateSignal)));
            SignalBus<PlayerStateSignal>.EnsureInitialized();
            SignalBus<SurvivalVitalsChangedSignal>.Configure(SurvivalVitalsChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SurvivalVitalsChangedSignal)));
            SignalBus<SurvivalVitalsChangedSignal>.EnsureInitialized();
            SignalBus<DropPodLandedSignal>.Configure(DropPodLandedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(DropPodLandedSignal)));
            SignalBus<DropPodLandedSignal>.EnsureInitialized();
            SignalBus<CameraPositionSignal>.Configure(CameraPositionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CameraPositionSignal)));
            SignalBus<CameraPositionSignal>.EnsureInitialized();
            SignalBus<CameraFrustumSignal>.Configure(CameraFrustumSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CameraFrustumSignal)));
            SignalBus<CameraFrustumSignal>.EnsureInitialized();
            SignalBus<WeatherChangedSignal>.Configure(WeatherStrengthSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WeatherChangedSignal)));
            SignalBus<WeatherChangedSignal>.EnsureInitialized();
            SignalBus<SystemPauseSignal>.Configure(SimulationPauseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SystemPauseSignal)));
            SignalBus<SystemPauseSignal>.EnsureInitialized();
            SignalBus<SimulationBucketSyncSignal>.Configure(SimulationBucketSyncSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SimulationBucketSyncSignal)));
            SignalBus<SimulationBucketSyncSignal>.EnsureInitialized();
            SignalBus<LockstepSnapshotSignal>.Configure(16, maxFrameSignals: 16, lowTierFrameSignals: 16, laneHash: 0x4C535348u);
            SignalBus<LockstepSnapshotSignal>.EnsureInitialized();
            SignalBus<FramePacingWarningSignal>.Configure(FramePacingWarningSignalCapacity, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: ComputeStableSignalLaneHash(nameof(FramePacingWarningSignal)));
            SignalBus<FramePacingWarningSignal>.EnsureInitialized();
            SignalBus<AcousticPingSignal>.Configure(AcousticPingSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AcousticPingSignal)));
            SignalBus<AcousticPingSignal>.EnsureInitialized();
            SignalBus<MovementAcousticSignal>.Configure(MovementAcousticSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MovementAcousticSignal)));
            SignalBus<MovementAcousticSignal>.EnsureInitialized();
            SignalBus<BiomeChangedSignal>.Configure(BiomeChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BiomeChangedSignal)));
            SignalBus<BiomeChangedSignal>.EnsureInitialized();
            SignalBus<BiomeGradientSignal>.Configure(BiomeGradientSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BiomeGradientSignal)));
            SignalBus<BiomeGradientSignal>.EnsureInitialized();
            SignalBus<DiegeticHudSignal>.Configure(DiegeticHudSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(DiegeticHudSignal)));
            SignalBus<DiegeticHudSignal>.EnsureInitialized();
            SignalBus<SaveRequestSignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveRequestSignal)));
            SignalBus<SaveRequestSignal>.EnsureInitialized();
            SignalBus<SaveCompletedSignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveCompletedSignal)));
            SignalBus<SaveCompletedSignal>.EnsureInitialized();
            SignalBus<SaveStatusSignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveStatusSignal)));
            SignalBus<SaveStatusSignal>.EnsureInitialized();
            SignalBus<SaveMetadataReadySignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveMetadataReadySignal)));
            SignalBus<SaveMetadataReadySignal>.EnsureInitialized();
            SignalBus<CpuStarvationSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(CpuStarvationSignal)));
            SignalBus<CpuStarvationSignal>.EnsureInitialized();
            SignalBus<LoreFragmentScannedSignal>.Configure(LoreFragmentScannedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(LoreFragmentScannedSignal)));
            SignalBus<LoreFragmentScannedSignal>.EnsureInitialized();
            SignalBus<ScannerToolActiveSignal>.Configure(ScannerToolActiveSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ScannerToolActiveSignal)));
            SignalBus<ScannerToolActiveSignal>.EnsureInitialized();
            SignalBus<MemoryAddressShiftSignal>.Configure(MemoryAddressShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MemoryAddressShiftSignal)));
            SignalBus<MemoryAddressShiftSignal>.EnsureInitialized();
            SignalBus<ResolutionChangedSignal>.Configure(ResolutionChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ResolutionChangedSignal)));
            SignalBus<ResolutionChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthIndexSignal>.Configure(SystemHealthIndexSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SystemHealthIndexSignal)));
            SignalBus<SystemHealthIndexSignal>.EnsureInitialized();
            SignalBus<StorageDebtSignal>.Configure(StorageDebtSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(StorageDebtSignal)));
            SignalBus<StorageDebtSignal>.EnsureInitialized();
            SignalBus<StreamingTurbulenceSignal>.Configure(StreamingTurbulenceSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(StreamingTurbulenceSignal)));
            SignalBus<StreamingTurbulenceSignal>.EnsureInitialized();
            SignalBus<AtmosphericReentrySignal>.Configure(AtmosphericReentrySignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AtmosphericReentrySignal)));
            SignalBus<AtmosphericReentrySignal>.EnsureInitialized();
            SignalBus<PrologueCompleteSignal>.Configure(PrologueCompleteSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PrologueCompleteSignal)));
            SignalBus<PrologueCompleteSignal>.EnsureInitialized();
            SignalBus<ManualOverridePulledSignal>.Configure(ManualOverridePulledSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ManualOverridePulledSignal)));
            SignalBus<ManualOverridePulledSignal>.EnsureInitialized();
            SignalBus<SwarmDispersedSignal>.Configure(SwarmDispersedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SwarmDispersedSignal)));
            SignalBus<SwarmDispersedSignal>.EnsureInitialized();
            SignalBus<FluidImpulseSignal>.Configure(FluidImpulseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(FluidImpulseSignal)));
            SignalBus<FluidImpulseSignal>.EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.Configure(BubbleSpawnSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BubbleSpawnSignal)));
            SignalBus<BubbleSpawnSignal>.EnsureInitialized();
            SignalBus<SubmarineFloodStateSignal>.Configure(SubmarineFloodStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SubmarineFloodStateSignal)));
            SignalBus<SubmarineFloodStateSignal>.EnsureInitialized();
            SignalBus<MacroDatabaseSectorHydrationSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(MacroDatabaseSectorHydrationSignal)));
            SignalBus<MacroDatabaseSectorHydrationSignal>.EnsureInitialized();
            SignalBus<WfcOutpostGeneratedSignal>.Configure(WfcOutpostGeneratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostGeneratedSignal)));
            SignalBus<WfcOutpostGeneratedSignal>.EnsureInitialized();
            SignalBus<WfcOutpostStateChangedSignal>.Configure(128, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostStateChangedSignal)));
            SignalBus<WfcOutpostStateChangedSignal>.EnsureInitialized();
            SignalBus<WfcOutpostDoorPowerSignal>.Configure(WfcOutpostDoorPowerSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostDoorPowerSignal)));
            SignalBus<WfcOutpostDoorPowerSignal>.EnsureInitialized();
            SignalBus<SectorResidencyHydratedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(SectorResidencyHydratedSignal)));
            SignalBus<SectorResidencyHydratedSignal>.EnsureInitialized();
            SignalBus<SectorDehydratedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(SectorDehydratedSignal)));
            SignalBus<SectorDehydratedSignal>.EnsureInitialized();
            SignalBus<ChunkDehydratedSignal>.Configure(ChunkDehydratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ChunkDehydratedSignal)));
            SignalBus<ChunkDehydratedSignal>.EnsureInitialized();
            SignalBus<InventoryCommandSignal>.Configure(InventoryCommandSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InventoryCommandSignal)));
            SignalBus<InventoryCommandSignal>.EnsureInitialized();
            SignalBus<InventoryChangedSignal>.Configure(InventoryChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InventoryChangedSignal)));
            SignalBus<InventoryChangedSignal>.EnsureInitialized();
            SignalBus<ItemDurabilityChangedSignal>.Configure(ItemDurabilityChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ItemDurabilityChangedSignal)));
            SignalBus<ItemDurabilityChangedSignal>.EnsureInitialized();
            SignalBus<ItemAcquiredSignal>.Configure(ItemAcquiredSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ItemAcquiredSignal)));
            SignalBus<ItemAcquiredSignal>.EnsureInitialized();
            SignalBus<RadiationDoseSignal>.Configure(RadiationDoseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(RadiationDoseSignal)));
            SignalBus<RadiationDoseSignal>.EnsureInitialized();
            SignalBus<RadiationSourceSignal>.Configure(RadiationSourceSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(RadiationSourceSignal)));
            SignalBus<RadiationSourceSignal>.EnsureInitialized();
            SignalBus<ResourceDepletionDeltaSignal>.Configure(ResourceDepletionDeltaSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ResourceDepletionDeltaSignal)));
            SignalBus<ResourceDepletionDeltaSignal>.EnsureInitialized();
            SignalBus<TemperatureChangedSignal>.Configure(TemperatureChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(TemperatureChangedSignal)));
            SignalBus<TemperatureChangedSignal>.EnsureInitialized();
            SignalBus<CullingOverloadSignal>.Configure(CullingOverloadSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CullingOverloadSignal)));
            SignalBus<CullingOverloadSignal>.EnsureInitialized();
            SignalBus<CraftingCompletedSignal>.Configure(CraftingCompletedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CraftingCompletedSignal)));
            SignalBus<CraftingCompletedSignal>.EnsureInitialized();
            SignalBus<ToolLoadoutChangedSignal>.Configure(ToolLoadoutChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ToolLoadoutChangedSignal)));
            SignalBus<ToolLoadoutChangedSignal>.EnsureInitialized();
            SignalBus<PlayerActionProgressSignal>.Configure(PlayerActionProgressSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionProgressSignal)));
            SignalBus<PlayerActionProgressSignal>.EnsureInitialized();
            SignalBus<PlayerActionCompletedSignal>.Configure(PlayerActionCompletedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionCompletedSignal)));
            SignalBus<PlayerActionCompletedSignal>.EnsureInitialized();
            SignalBus<PlayerActionCancelledSignal>.Configure(PlayerActionCancelledSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionCancelledSignal)));
            SignalBus<PlayerActionCancelledSignal>.EnsureInitialized();
            SignalBus<ScanLogChangedSignal>.Configure(ScanLogChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ScanLogChangedSignal)));
            SignalBus<ScanLogChangedSignal>.EnsureInitialized();
            SignalBus<PdaExchangeStateChangedSignal>.Configure(PdaExchangeStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PdaExchangeStateChangedSignal)));
            SignalBus<PdaExchangeStateChangedSignal>.EnsureInitialized();
            SignalBus<VehicleUpgradesChangedSignal>.Configure(VehicleUpgradesChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(VehicleUpgradesChangedSignal)));
            SignalBus<VehicleUpgradesChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthSignal>.Configure(16, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x48484C54u);
            SignalBus<SystemHealthSignal>.EnsureInitialized();
            SignalBus<FrameTimeSignal>.Configure(32, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x46544D53u);
            SignalBus<FrameTimeSignal>.EnsureInitialized();
            SignalBus<KillSwitchSignal>.Configure(8, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: 0x4B534857u);
            SignalBus<KillSwitchSignal>.EnsureInitialized();
            SignalBus<ReentryVfxStateSignal>.Configure(4, 16, 4, ComputeStableSignalLaneHash(nameof(ReentryVfxStateSignal)));
            SignalBus<ReentryVfxStateSignal>.EnsureInitialized();
            SignalBus<VisorDropletSignal>.Configure(8, 32, 8, ComputeStableSignalLaneHash(nameof(VisorDropletSignal)));
            SignalBus<VisorDropletSignal>.EnsureInitialized();
            SignalBus<VisualFlareSignal>.Configure(16, 16, 16, 0x56464C52u);
            SignalBus<VisualFlareSignal>.EnsureInitialized();
            SignalBus<TetherTensionSignal>.Configure(128, laneHash: ComputeStableSignalLaneHash(nameof(TetherTensionSignal)));
            SignalBus<TetherTensionSignal>.EnsureInitialized();
            SignalBus<TetherSnappedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(TetherSnappedSignal)));
            SignalBus<TetherSnappedSignal>.EnsureInitialized();
            SignalBus<TetherFiredSignal>.Configure(16, maxFrameSignals: 16, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(TetherFiredSignal)));
            SignalBus<TetherFiredSignal>.EnsureInitialized();
            SignalBus<VoxelCarveEvent>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(VoxelCarveEvent)));
            SignalBus<VoxelCarveEvent>.EnsureInitialized();
            SignalBus<DockingRequestSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingRequestSignal)));
            SignalBus<DockingRequestSignal>.EnsureInitialized();
            SignalBus<DockingCompleteSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingCompleteSignal)));
            SignalBus<DockingCompleteSignal>.EnsureInitialized();
            SignalBus<DockingFailedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingFailedSignal)));
            SignalBus<DockingFailedSignal>.EnsureInitialized();
            SignalBus<AnomalyProximitySignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: 0xC06A5512u);
            SignalBus<AnomalyProximitySignal>.EnsureInitialized();
            SignalBus<CompassCalibratedSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: 0xC06A5511u);
            SignalBus<CompassCalibratedSignal>.EnsureInitialized();
        }

        private static void CreateQueue<T>(ref NativeQueue<T> queue, int expectedCapacity, string label)
            where T : unmanaged, ISignal
        {
            if (queue.IsCreated)
                return;

            SignalBus<T>.Configure(expectedCapacity, laneHash: ComputeStableSignalLaneHash(label));
            queue = SignalBus<T>.GetQueueForLegacyGlobalSignals();
        }

        private static bool TryDequeue<T>(ref NativeQueue<T> queue, out T signal)
            where T : unmanaged, ISignal
        {
            return SignalBus<T>.TryReadFrame(out signal);
        }

        private static void ClearLatestSignals()
        {
            _latestDamageSignal = default;
            _latestAcousticPingSignal = default;
            _latestFluidDensityChangedSignal = default;
            _latestLightLevelSignal = default;
            _latestPhysiologyStateSignal = default;
            _latestPlayerStressSignal = default;
            _latestPlayerStateSignal = default;
            _latestSeismicSignal = default;
            _latestScannerToolActiveSignal = default;
            _latestToolStateChangedSignal = default;
            _latestSurvivalDeathSignal = default;
            Volatile.Write(ref _latestStorageDebtMilli, 0);
            Volatile.Write(ref _latestStorageLatencyMilli, 0);
            Volatile.Write(ref _latestStorageDebtSequence, 0);
            Volatile.Write(ref _latestDamageSignalSequence, 0);
            Volatile.Write(ref _latestAcousticPingSignalSequence, 0);
            Volatile.Write(ref _latestFluidDensityChangedSignalSequence, 0);
            Volatile.Write(ref _latestLightLevelSignalSequence, 0);
            Volatile.Write(ref _latestPhysiologyStateSignalSequence, 0);
            Volatile.Write(ref _latestPlayerStressSignalSequence, 0);
            Volatile.Write(ref _latestPlayerStateSignalSequence, 0);
            Volatile.Write(ref _latestSeismicSignalSequence, 0);
            Volatile.Write(ref _latestScannerToolActiveSignalSequence, 0);
            Volatile.Write(ref _latestToolStateChangedSignalSequence, 0);
            Volatile.Write(ref _latestSurvivalDeathSignalSequence, 0);
            Volatile.Write(ref _latestCraftingCompletedSignalSequence, 0);
            Volatile.Write(ref _latestCraftingCompletedUnitCount, 0);
            Volatile.Write(ref _timeDilationScalarMilli, 1000);
            Volatile.Write(ref _timeDilationSequence, 0);
            Volatile.Write(ref _simulationPaused, 0);
            Volatile.Write(ref _bulletTimeVisualMilli, 0);
            Volatile.Write(ref _signalTelemetryCursor, 0);
        }

        private static void AdvanceSignalSequence(ref int sequence)
        {
            int next = unchecked(Volatile.Read(ref sequence) + 1);
            if (next == 0)
                next = 1;

            Volatile.Write(ref sequence, next);
        }

        private static void AdvanceSignalCounter(ref int counter, int amount)
        {
            if (amount <= 0)
                return;

            int next = unchecked(Volatile.Read(ref counter) + amount);
            Volatile.Write(ref counter, next);
        }

        private static void DisposeQueue<T>(ref NativeQueue<T> queue, string label)
            where T : unmanaged, ISignal
        {
            if (!queue.IsCreated)
                return;

            SignalBus<T>.Dispose();
            queue = default;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ValidateSignalPayload<T>(int expectedBytes)
            where T : unmanaged
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Debug.LogError("[GlobalSignals] signal managed-reference violation.");

            ValidateSignalSize<T>(expectedBytes);
        }

        private static void ValidateSignalSize<T>(int expectedBytes)
            where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size != expectedBytes)
                Debug.LogError("[GlobalSignals] signal size violation.");
        }
#endif
    }

    /// <summary>Power-of-two single-producer/single-consumer signal fallback using mask wrapping.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private int _mask;
        private int _head;
        private int _tail;

        public SpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
        {
            int capacity = CeilPowerOfTwo(math.max(2, requestedCapacity + 1));
            _buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
            _mask = capacity - 1;
            _head = 0;
            _tail = 0;
        }

        public bool IsCreated => _buffer.IsCreated;
        public int Capacity => _buffer.IsCreated ? _buffer.Length - 1 : 0;

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();

            _buffer = default;
            _mask = 0;
            _head = 0;
            _tail = 0;
        }

        public void Clear()
        {
            Volatile.Write(ref _head, 0);
            Volatile.Write(ref _tail, 0);
        }

        public bool TryEnqueue(in T signal)
        {
            if (!_buffer.IsCreated)
                return false;

            int tail = Volatile.Read(ref _tail);
            int nextTail = (tail + 1) & _mask;
            if (nextTail == Volatile.Read(ref _head))
                return false;

            _buffer[tail] = signal;
            Volatile.Write(ref _tail, nextTail);
            return true;
        }

        public bool TryDequeue(out T signal)
        {
            if (!_buffer.IsCreated)
            {
                signal = default;
                return false;
            }

            int head = Volatile.Read(ref _head);
            if (head == Volatile.Read(ref _tail))
            {
                signal = default;
                return false;
            }

            signal = _buffer[head];
            Volatile.Write(ref _head, (head + 1) & _mask);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CeilPowerOfTwo(int value)
        {
            value = math.clamp(value, 2, 1 << 30);
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Physics-to-sound impact signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ImpactSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
        [FieldOffset(48)] public float Force;
        [FieldOffset(48)] public float Velocity;
        [FieldOffset(52)] public float Intensity;
        [FieldOffset(52)] public float Mass;
        [FieldOffset(56)] public uint PrimaryBodyId;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public byte WeightClass;
        [FieldOffset(61)] public byte PrimaryMaterialId;
        [FieldOffset(62)] public byte SecondaryMaterialId;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Kinematic CCD impact packet with exact AUP hit point and slide normal. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HighSpeedImpactSignal : ISignal
    {
        public const byte SourcePlayer = 1;
        public const byte SourceVehicle = 2;
        public const byte SourceLeviathan = 3;
        public const byte FlagCornerHalt = 1 << 0;
        public const byte FlagLowTierStop = 1 << 1;
        public const byte MaterialOrganic = 0;
        public const byte MaterialMetal = 1;
        public const byte MaterialGlass = 2;

        [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
        [FieldOffset(48)] public float3 Normal;
        [FieldOffset(60)] public float LostKineticEnergy;
        [FieldOffset(60)] public float KineticEnergy;
        [FieldOffset(64)] public float ImpactSpeed;
        [FieldOffset(68)] public uint SourceHash;
        [FieldOffset(72)] public uint TargetHash;
        [FieldOffset(76)] public uint Frame;
        [FieldOffset(80)] public byte SourceKind;
        [FieldOffset(81)] public byte Flags;
        [FieldOffset(82)] public byte PrimaryMaterialId;
        [FieldOffset(83)] public byte SecondaryMaterialId;
        [FieldOffset(84)] public float EffectiveMass;
        [FieldOffset(88)] public uint MaterialHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComposeMaterialHash(uint targetHash, byte primaryMaterialId, byte secondaryMaterialId)
        {
            uint hash = 2166136261u;
            hash = (hash ^ targetHash) * 16777619u;
            hash = (hash ^ primaryMaterialId) * 16777619u;
            hash = (hash ^ secondaryMaterialId) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    /// <summary>Haptic request packet sourced from high-energy physical impacts. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct HapticRequest : ISignal
    {
        public const byte ChannelCollision = 1;
        public const byte ChannelLightThud = 2;
        public const byte ChannelGearScrape = 3;
        public const byte ChannelVehicleCritical = 4;
        public const byte ChannelCrush = 5;
        public const byte ChannelMicroVibration = 6;
        public const byte FlagLightThud = 1 << 0;
        public const byte FlagCrush = 1 << 1;
        public const byte FlagMicroVibration = 1 << 2;

        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float DurationSeconds;
        [FieldOffset(8)] public float Frequency01;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Channel;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Player state adapter lane for animation, traversal, and contextual physiology. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct PlayerStateSignal : ISignal
    {
        public const byte StateSqueezing = 1;
        public const byte StateClimbing = 2;
        public const byte FlagActive = 1 << 0;
        public const byte FlagSqueezing = FlagActive;
        public const byte FlagSdfGradientValid = 1 << 1;
        public const byte FlagLowTierGradient = 1 << 2;
        public const byte FlagAupShiftSafe = 1 << 3;
        public const byte FlagClimbing = 1 << 4;
        public const byte FlagVrGrip = 1 << 5;
        public const byte FlagLadderSlip = 1 << 6;
        public const byte FlagLowTierCameraSlide = 1 << 7;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Intensity01;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte State;
        [FieldOffset(61)] public byte Flags;
    }

    public static class SurvivalVitalsChangedSignalFlags
    {
        public const uint Oxygen = 1u << 0;
        public const uint Energy = 1u << 1;
        public const uint Integrity = 1u << 2;
        public const uint Depth = 1u << 3;
        public const uint Temperature = 1u << 4;
        public const uint Thermal = 1u << 5;
        public const uint Injury = 1u << 6;
        public const uint Death = 1u << 7;
        public const uint OxygenCritical = 1u << 8;
        public const uint Pressure = 1u << 9;
    }

    /// <summary>Player survival-vitals dirty mask for UI and advisory consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SurvivalVitalsChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float Oxygen01;
        [FieldOffset(20)] public float Energy01;
        [FieldOffset(24)] public float Integrity01;
        [FieldOffset(28)] public byte DeathCause;
    }

    /// <summary>Player delayed-action progress lane for UI and feedback consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerActionProgressSignal : ISignal
    {
        public const byte ActionKindGeneric = 0;
        public const byte ActionKindMedical = 1;
        public const byte ActionKindOxygen = 2;
        public const byte ActionKindFood = 3;
        public const byte FlagHasItem = 1 << 0;

        [FieldOffset(0)] public float Progress01;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort ActiveToolSlot;
        [FieldOffset(14)] public byte ActionKind;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player delayed-action completion lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerActionCompletedSignal : ISignal
    {
        public const byte FlagHasItem = 1 << 0;
        public const byte FlagInventoryAnchorValid = 1 << 1;

        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public ushort InventoryAnchorX;
        [FieldOffset(10)] public ushort InventoryAnchorY;
        [FieldOffset(12)] public byte ActionKind;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player delayed-action cancellation lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerActionCancelledSignal : ISignal
    {
        public const byte ReasonGeneric = 0;
        public const byte FlagHasItem = 1 << 0;

        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public byte ActionKind;
        [FieldOffset(13)] public byte Reason;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    public static class InventoryCommandSignalCommands
    {
        public const byte Sort = 1;
    }

    /// <summary>Inventory command lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryCommandSignal : ISignal
    {
        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Command;
        [FieldOffset(13)] public byte Flags;
    }

    /// <summary>Inventory mutation lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint Revision;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort OccupiedCells;
        [FieldOffset(14)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ItemDurabilityChangedSignal : ISignal
    {
        public const byte ReasonCorrosion = 1;
        public const byte ReasonRepair = 2;
        public const byte ReasonBreak = 3;

        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public float Durability01;
        [FieldOffset(12)] public float AverageEquippedDurability01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public ushort SlotIndex;
        [FieldOffset(22)] public byte Reason;
        [FieldOffset(23)] public byte Flags;
        [FieldOffset(24)] public uint BiomeHash;
    }

    /// <summary>Resource-to-inventory yield signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemAcquiredSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ItemHash;
        [FieldOffset(52)] public uint OreHash;
        [FieldOffset(56)] public ushort Quantity;
        [FieldOffset(58)] public byte SourceKind;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Radiation grid/physiology dose signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationDoseSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Dose;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte DoseKind;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Authoritative thermodynamics temperature change signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TemperatureChangedSignal : ISignal
    {
        public const byte FlagPlayerAmbient = 1 << 0;
        public const byte FlagSubmarineAmbient = 1 << 1;
        public const byte FlagThermalShock = 1 << 2;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float TemperatureCelsius;
        [FieldOffset(52)] public float DeltaCelsius;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort SourceId;
        [FieldOffset(62)] public byte Flags;
    }

    /// <summary>Radiation source registration/update signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationSourceSignal : ISignal
    {
        public const byte OperationRemove = 0;
        public const byte OperationUpsert = 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Intensity;
        [FieldOffset(52)] public float RadiusMeters;
        [FieldOffset(56)] public int SourceId;
        [FieldOffset(60)] public byte Operation;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Resource depletion persistence delta signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ResourceDepletionDeltaSignal : ISignal
    {
        [FieldOffset(0)] public long SectorHash;
        [FieldOffset(8)] public ulong DepletionMask;
        [FieldOffset(16)] public uint OreHash;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public ushort WordIndex;
        [FieldOffset(26)] public byte Operation;
        [FieldOffset(27)] public byte Flags;
    }

    /// <summary>AUP sector pre-shift warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct AupPreShiftSignal : ISignal
    {
        public float3 ShiftMeters;
        public uint ShiftFrameId;
        public int3 SectorDelta;
        public uint Flags;
    }

    /// <summary>AUP sector shift broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct AupShiftSignal : ISignal
    {
        public float3 ShiftMeters;
        public uint ShiftFrameId;
        public int3 SectorDelta;
        public uint Flags;
    }

    /// <summary>Drop-pod landing anchor for first-hour economy weighting. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DropPodLandedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public byte Flags;
    }

    /// <summary>Procedural instance culling overload signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CullingOverloadSignal : ISignal
    {
        [FieldOffset(0)] public int VisibleInstances;
        [FieldOffset(4)] public int CulledInstances;
        [FieldOffset(8)] public int SourceInstances;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float CullDistanceMeters;
        [FieldOffset(20)] public float VramUsedMb;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint SourceHash;
    }

    /// <summary>Producer-agnostic procedural flora wake signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WakeGeneratedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public uint SourceFlags;
    }

    /// <summary>Producer-agnostic visual-fluid impulse. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct FluidImpulseSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Vector;
        [FieldOffset(60)] public float Radius;
        [FieldOffset(64)] public float Lifetime;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint SourceHash;
        [FieldOffset(76)] public uint Flags;
    }

    /// <summary>Bounded submarine bubble-spawn marker for visual-fluid VFX. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct BubbleSpawnSignal : ISignal
    {
        public const uint FlagEngineVent = 1u << 0;
        public const uint FlagTailHeavy = 1u << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public float Intensity01;
        [FieldOffset(64)] public float RadiusMeters;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint SourceHash;
        [FieldOffset(76)] public uint Flags;
    }

    /// <summary>Narrative POI-to-progression broadcast. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ProgressionEventSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PoiHash;
        [FieldOffset(52)] public uint QuestHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte Source;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>AUP-independent global narrative state mutation. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GlobalWorldStateSignal : ISignal
    {
        public const byte ChangeKindRule = 1;
        public const byte ChangeKindLoad = 2;
        public const byte ChangeKindDevConsole = 3;
        public const byte FlagAupIndependent = 1 << 0;
        public const byte FlagVisualRefresh = 1 << 1;
        public const byte FlagAudioBroadcast = 1 << 2;
        public const byte FlagCartographyRefresh = 1 << 3;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint VariableHash;
        [FieldOffset(52)] public int Value;
        [FieldOffset(56)] public uint StageHash;
        [FieldOffset(60)] public byte ChangeKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Sequence;
    }

    /// <summary>Narrative-driven biome transition broadcast. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PreviousBiomeHash;
        [FieldOffset(52)] public uint CurrentBiomeHash;
        [FieldOffset(56)] public uint PoiHash;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Mathematical SDF biome boundary blend broadcast. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct BiomeGradientSignal : ISignal
    {
        public const byte FlagLowTierKernel = 1 << 0;
        public const byte FlagExactCellCenter = 1 << 1;
        public const byte FlagMissingMap = 1 << 2;
        public const byte FlagInvalidInput = 1 << 3;
        public const byte FlagHasSecondaryBiome = 1 << 4;
        public const byte FlagOutOfBounds = 1 << 5;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint BiomeAHash;
        [FieldOffset(52)] public uint BiomeBHash;
        [FieldOffset(56)] public float BlendFactor01;
        [FieldOffset(60)] public float BoundaryDistanceMeters;
        [FieldOffset(64)] public float CellSizeMeters;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte BiomeA;
        [FieldOffset(73)] public byte BiomeB;
        [FieldOffset(74)] public byte SampleDiameter;
        [FieldOffset(75)] public byte Flags;
    }

    /// <summary>Soft narrative camera focus target. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct NarrativeFocusSignal : ISignal
    {
        public const byte FlagArtifactTarget = 1 << 0;
        public const byte FlagCreatureTarget = 1 << 1;
        public const byte FlagHeadBoneTarget = 1 << 2;
        public const byte FlagWorldSubtitle = 1 << 3;
        public const byte FlagDisableFovNarrowing = 1 << 4;

        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint FocusHash;
        [FieldOffset(52)] public uint SubtitleHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public float DurationSeconds;
        [FieldOffset(64)] public float SubtitleFadeDistanceSq;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte BoneTarget;
    }

    /// <summary>Player override notification for broken narrative camera focus. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FocusBrokenSignal : ISignal
    {
        public const byte ReasonPlayerLookInput = 1;

        [FieldOffset(0)] public uint FocusHash;
        [FieldOffset(4)] public float PlayerInputDeltaSq;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte Reason;
        [FieldOffset(13)] public byte Flags;
    }

    /// <summary>Signal-only mixer state request. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MixerStateSignal : ISignal
    {
        public const uint FocusStateHash = 0x464F4355u; // FOCU

        [FieldOffset(0)] public uint MixerStateHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float DuckingDb;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>Diegetic HUD waypoint payload sourced from an active narrative POI. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NarrativeHudWaypointSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PoiHash;
        [FieldOffset(52)] public uint QuestHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte Priority;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Audio ambience profile payload sourced from a narrative POI. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SoundscapeProfileSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ProfileHash;
        [FieldOffset(52)] public uint PoiHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Save/RLE sync payload for narrative POI trigger latches. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NarrativePoiStateSignal : ISignal
    {
        [FieldOffset(0)] public ulong StateMask;
        [FieldOffset(8)] public uint PoiHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort PoiIndex;
        [FieldOffset(18)] public byte Operation;
        [FieldOffset(19)] public byte Flags;
    }

    /// <summary>Logistics-to-UI brownout signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BrownoutSignal : ISignal
    {
        [FieldOffset(0)] public uint NetworkId;
        [FieldOffset(4)] public uint NodeId;
        [FieldOffset(8)] public float SupplyRatio;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Priority;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Ecosystem-to-VFX debris spawn signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct DebrisSpawnSignal : ISignal
    {
        public const byte DebrisKindSparks = 1;
        public const byte FlagToolSparks = 1 << 0;
        public const byte FlagComputeShard = 1 << 7;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint SourceEntityId;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte DebrisKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Quantity;
    }

    /// <summary>Combat-to-feedback armor deflection signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct DeflectSignal : ISignal
    {
        public float3 LocalPoint;
        public float FrontDot;
        public uint TargetHash;
        public uint SourceHash;
        public float DamageScalar;
        public byte Flags;
        public byte ArmorClass;
        public ushort Reserved;
    }

    /// <summary>Combat-to-ecosystem death signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityDeathSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntityHash;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Data-only entity activation signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntitySpawnSignal : ISignal
    {
        public const byte KindEcology = 1;
        public const byte FlagEcology = 1 << 0;
        public const byte FlagLowTierVisual = 1 << 1;
        public const byte FlagSdfEmergence = 1 << 2;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public ushort SpawnedCount;
        [FieldOffset(54)] public ushort RequestedCount;
        [FieldOffset(56)] public byte EntityKind;
        [FieldOffset(57)] public byte QualityTier;
        [FieldOffset(58)] public byte Flags;
        [FieldOffset(59)] public byte Reserved;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Narrative-to-celestial solar flare signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SolarFlareSignal : ISignal
    {
        [FieldOffset(0)] public uint QuestStepHash;
        [FieldOffset(4)] public float Intensity01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Seed;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Origin rebase broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RebaseSignal : ISignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 GridDelta;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>Input-to-KCC control signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ControlSignal : ISignal
    {
        [FieldOffset(0)] public uint ControlMask;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float2 Move;
        [FieldOffset(16)] public float2 Look;
        [FieldOffset(24)] public ushort Sequence;
        [FieldOffset(26)] public byte Device;
        [FieldOffset(27)] public byte Flags;
    }

    /// <summary>Runtime anomaly signal for watchdog systems. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AnomalySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Compass anomaly proximity signal. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
    public struct AnomalyProximitySignal : ISignal
    {
        public AbsoluteUniversePosition SourceAup;
        public float Proximity01;
        public float Interference01;
        public uint Frame;
        public uint SourceHash;
        public byte Flags;
    }

    /// <summary>Compass recalibration signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct CompassCalibratedSignal : ISignal
    {
        public uint SourceHash;
        public uint Frame;
        public float CalibrationQuality01;
        public byte Flags;
    }

    /// <summary>Telemetry anomaly signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TelemetryAnomalySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Crash/postmortem telemetry signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CrashTelemetrySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint ReasonHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public int ExitCode;
        [FieldOffset(16)] public int NativeAllocationCount;
        [FieldOffset(20)] public float NativeTrackedBytesMb;
        [FieldOffset(24)] public byte Severity;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Habitat construction graph mutation signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HabitatConstructionSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ModuleHash;
        [FieldOffset(52)] public uint GraphId;
        [FieldOffset(56)] public ushort NodeId;
        [FieldOffset(58)] public byte Operation;
        [FieldOffset(59)] public byte Flags;
    }

    /// <summary>Tool-to-habitat deconstruction request signal. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DeconstructRequestSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public AbsoluteUniversePosition RayOriginAup;
        [FieldOffset(96)] public uint TargetEntityId;
        [FieldOffset(100)] public uint RequesterEntityId;
        [FieldOffset(104)] public float MaxDistance;
        [FieldOffset(108)] public float3 RayDirection;
        [FieldOffset(120)] public uint Frame;
        [FieldOffset(124)] public byte ToolKind;
        [FieldOffset(125)] public byte Flags;
    }

    /// <summary>Habitat deconstruction validation/execution result signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeconstructResultSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint TargetEntityId;
        [FieldOffset(52)] public uint RequesterEntityId;
        [FieldOffset(56)] public ushort RefundItemCount;
        [FieldOffset(58)] public byte Result;
        [FieldOffset(59)] public byte Reason;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Persistence/pipeline deletion marker emitted after a module leaves the graph. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleDeconstructSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ModuleHash;
        [FieldOffset(52)] public uint TargetEntityId;
        [FieldOffset(56)] public ushort NodeId;
        [FieldOffset(58)] public byte Operation;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Player vital warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VitalWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float Vital01;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Priority;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Crush-depth warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CrushWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float DepthMeters;
        [FieldOffset(12)] public float CrushLimitMeters;
        [FieldOffset(16)] public float Severity01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Priority;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Hash-addressed subtitle request signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SubtitleSignal : ISignal
    {
        [FieldOffset(0)] public uint SubtitleHash;
        [FieldOffset(4)] public uint SpeakerHash;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Priority;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Submarine vocal warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VocalWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float Severity01;
        [FieldOffset(12)] public float CooldownSeconds;
        [FieldOffset(16)] public byte Priority;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Editor data reload signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataReloadSignal : ISignal
    {
        [FieldOffset(0)] public uint DataHash;
        [FieldOffset(4)] public uint CategoryHash;
        [FieldOffset(8)] public uint Revision;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Memory pressure signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MemoryPressureSignal : ISignal
    {
        [FieldOffset(0)] public long ReservedMemoryBytes;
        [FieldOffset(8)] public long PhysicalMemoryBytes;
        [FieldOffset(16)] public float UsageRatio;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Severity;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>GlobalDataVault relocation notice for systems caching raw pointers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MemoryAddressShiftSignal : ISignal
    {
        public const byte FlagMemMove = 1 << 0;
        public const byte FlagFenceProtected = 1 << 1;

        [FieldOffset(0)] public long OldPointer;
        [FieldOffset(8)] public long NewPointer;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
    }

    /// <summary>Runtime mip/resolution residency change signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ResolutionChangedSignal : ISignal
    {
        public const byte ReasonVramRedline = 1;
        public const byte ReasonVramRecovered = 2;
        public const byte ReasonRenderScaleDropped = 3;
        public const byte ReasonRenderScaleRaised = 4;
        public const byte FlagTextureMipLimit = 1 << 0;
        public const byte FlagRenderScale = 1 << 1;
        public const byte FlagStpActive = 1 << 2;

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public int OldMipLimit;
        [FieldOffset(12)] public int NewMipLimit;
        [FieldOffset(16)] public float VramUsedMb;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Homeostasis state broadcast. Critical state is the SHI_Critical equivalent. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemHealthIndexSignal : ISignal
    {
        public const byte StateStable = 0;
        public const byte StateWarning = 1;
        public const byte StateCritical = 2;
        public const byte FlagAdrenaline = 1 << 0;

        [FieldOffset(0)] public float Health01;
        [FieldOffset(4)] public float Pressure01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>CPU worker-starvation broadcast emitted when a non-critical job admission is denied. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CpuStarvationSignal : ISignal
    {
        [FieldOffset(0)] public uint JobHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float EstimatedCostMs;
        [FieldOffset(12)] public float RemainingBudgetMs;
        [FieldOffset(16)] public int CriticalDebtFrames;
        [FieldOffset(20)] public byte Lane;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Acoustic ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct AcousticPingSignal : ISignal
    {
        public const byte ChannelActiveSonar = 2;
        public const byte ChannelGloveScrape = 3;
        public const byte ChannelFabricScrape = ChannelGloveScrape;
        public const byte ChannelMetalStress = 4;
        public const byte ChannelLeviathanRoar = 5;
        public const byte ChannelLootZip = 6;
        public const byte ChannelJawSnap = 7;
        public const byte FlagActiveSonar = 1;
        public const byte FlagGloveScrape = 1 << 1;
        public const byte FlagFabricScrape = FlagGloveScrape;
        public const byte FlagLeviathanRoar = 1 << 2;
        public const byte FlagLootZip = 1 << 3;
        public const byte FlagJawSnap = 1 << 4;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Player movement acoustic broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MovementAcousticSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Volume;
        [FieldOffset(52)] public float VelocitySq;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte LocomotionMode;
        [FieldOffset(61)] public byte SurfaceMode;
        [FieldOffset(62)] public byte Flags;
    }

    /// <summary>GPU swarm dispersion broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SwarmDispersedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public ushort EstimatedBoidCount;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte QualityTier;
    }

    /// <summary>World chunk hydration broadcast consumed by data-only ecology systems. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SectorResidencyHydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = 1;
        public const byte FlagPinned = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long ChunkId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>World chunk dehydration broadcast consumed by data-only ecology systems. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SectorDehydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = 1;
        public const byte FlagPinned = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long ChunkId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>Chunk dehydration persistence trigger. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ChunkDehydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = SectorDehydratedSignal.FlagProxyFallback;
        public const byte FlagPinned = SectorDehydratedSignal.FlagPinned;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long SectorHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>Sonar ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SonarPingSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Hypoxia warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HypoxiaSignal : ISignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Oxygen critical signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OxygenCriticalSignal : ISignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Interaction UI show/hide signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InteractionUiSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint TargetHash;
        [FieldOffset(52)] public uint ToolHash;
        [FieldOffset(56)] public byte State;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>UI layout rescale request emitted after staged font swaps. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct UIRescaleRequestSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public ushort Reason;
        [FieldOffset(10)] public ushort Language;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float FontScale;
    }

    /// <summary>Fluid incursion compartment signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidIncursionSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition LeakAup;
        [FieldOffset(48)] public uint CompartmentId;
        [FieldOffset(52)] public float FloodLevel01;
        [FieldOffset(56)] public float FlowRate01;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Submarine dynamic flood mass-state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineFloodStateSignal : ISignal
    {
        public const byte FlagHasFloodMass = 1 << 0;
        public const byte FlagCriticalFlood = 1 << 1;
        public const byte FlagInvalid = 1 << 7;

        [FieldOffset(0)] public float3 DynamicCenterOfMassLocal;
        [FieldOffset(12)] public float3 DynamicCenterOfMassOffsetLocal;
        [FieldOffset(24)] public float TotalWaterMassKg;
        [FieldOffset(28)] public float BaseMassKg;
        [FieldOffset(32)] public float FillRatio01;
        [FieldOffset(36)] public float AngularDragMultiplier;
        [FieldOffset(40)] public uint SourceBodyId;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public ushort RoomCount;
        [FieldOffset(50)] public byte MathLod;
        [FieldOffset(51)] public byte Flags;
    }

    /// <summary>Fluid density transition signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidDensityChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float DensityMultiplier;
        [FieldOffset(52)] public float BrineHeightY;
        [FieldOffset(56)] public float SubmersionSeconds;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] public byte FluidKind;
        [FieldOffset(62)] public ushort SectorHash;
    }

    /// <summary>Fluid pipe rupture signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PipeRuptureSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition RuptureAup;
        [FieldOffset(48)] public uint NetworkId;
        [FieldOffset(52)] public uint NodeId;
        [FieldOffset(56)] public float PressureKPa;
        [FieldOffset(60)] public byte ContentKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public short RoomIndex;
    }

    /// <summary>Spectrum scan frequency signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpectrumScanSignal : ISignal
    {
        [FieldOffset(0)] public uint ScanId;
        [FieldOffset(4)] public float FrequencyHz;
        [FieldOffset(8)] public float Amplitude01;
        [FieldOffset(12)] public float Noise01;
        [FieldOffset(16)] public byte Band;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Rigidbody sleep-state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RigidbodySleepSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint BodyId;
        [FieldOffset(52)] public float DistanceMeters;
        [FieldOffset(56)] public byte SleepState;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>Scanner active-state signal consumed by diegetic tuning UI. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerToolActiveSignal : ISignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint ArtifactHash;
        [FieldOffset(8)] public uint BlueprintHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float Progress01;
        [FieldOffset(20)] public float Battery01;
        [FieldOffset(24)] public byte Active;
        [FieldOffset(25)] public byte Stage;
        [FieldOffset(26)] public byte Flags;
        [FieldOffset(27)] public byte QualityTier;
    }

    /// <summary>Scan-complete signal for PDA/lore unlock consumers. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScanCompleteSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint ScanId;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte ReconKind;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Lore-fragment scan commit signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LoreFragmentScannedSignal : ISignal
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public byte Flags;
    }

    /// <summary>Blueprint unlock signal for crafting and PDA consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BlueprintUnlockedSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint BlueprintHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Category;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Crafting start signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CraftingStartedSignal : ISignal
    {
        [FieldOffset(0)] public uint FabricatorHash;
        [FieldOffset(4)] public uint RecipeHash;
        [FieldOffset(8)] public uint ResultItemHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort Multiplier;
        [FieldOffset(18)] public byte Flags;
    }

    /// <summary>Crafting completion signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CraftingCompletedSignal : ISignal
    {
        [FieldOffset(0)] public uint FabricatorHash;
        [FieldOffset(4)] public uint RecipeHash;
        [FieldOffset(8)] public uint ResultItemHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort Quantity;
        [FieldOffset(18)] public byte Flags;
        [FieldOffset(20)] public uint Sequence;
    }

    /// <summary>Authoritative active tool state signal consumed by diegetic tool screens. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolStateChangedSignal : ISignal
    {
        public const byte FlagEquipped = 1 << 0;
        public const byte FlagVisible = 1 << 1;
        public const byte FlagLowTierFallback = 1 << 2;

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Battery01;
        [FieldOffset(12)] public float Heat01;
        [FieldOffset(16)] public float DistanceMeters;
        [FieldOffset(20)] public float Durability01;
        [FieldOffset(24)] public uint StatusMask;
        [FieldOffset(28)] public ushort AmmoUnits;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte ToolTypeId;
    }

    /// <summary>Player quick-slot assignment and active slot dirty signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolLoadoutChangedSignal : ISignal
    {
        public const ushort NoActiveSlot = ushort.MaxValue;
        public const byte ReasonActiveSlotChanged = 1;
        public const byte ReasonAssignmentsChanged = 2;
        public const byte FlagHasActiveTool = 1 << 0;
        public const byte FlagSwapInProgress = 1 << 1;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Sequence;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ActiveToolHash;
        [FieldOffset(16)] public uint AssignedSlotMask;
        [FieldOffset(20)] public ushort ActiveSlot;
        [FieldOffset(22)] public ushort SlotCount;
        [FieldOffset(24)] public byte Reason;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Tool acoustic state signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolAcousticSignal : ISignal
    {
        public const byte StateLaserLoop = 1;
        public const byte FlagLooping = 1 << 0;

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint TargetHash;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public float PitchScale;
        [FieldOffset(16)] public float Intensity01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte State;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Power drain intent signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PowerDrainSignal : ISignal
    {
        [FieldOffset(0)] public uint ConsumerHash;
        [FieldOffset(4)] public uint NetworkHash;
        [FieldOffset(8)] public float Watts;
        [FieldOffset(12)] public float Progress01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>OpenXR/input bridge trigger signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolTriggerSignal : ISignal
    {
        [FieldOffset(0)] public float Strength;
        [FieldOffset(4)] public float SecondaryStrength;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ControllerMask;
        [FieldOffset(16)] public ushort Sequence;
        [FieldOffset(18)] public byte DominantController;
        [FieldOffset(19)] public byte Flags;
    }

    /// <summary>Storage IO backpressure scalar for movement, PDA, VFX, and telemetry consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StorageDebtSignal : ISignal
    {
        public const byte HighDebtFlag = 1 << 0;
        public const byte DataLinkDegradedFlag = 1 << 1;
        public const byte CriticalHoleFlag = 1 << 2;
        public const byte ProxyFallbackFlag = 1 << 3;

        [FieldOffset(0)] public float Debt01;
        [FieldOffset(4)] public float LatencyEwmaMs;
        [FieldOffset(8)] public float OldestPendingMs;
        [FieldOffset(12)] public float CriticalHoleDebtMs;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] public ushort PendingLoads;
        [FieldOffset(26)] public byte Flags;
    }

    /// <summary>Visual-only streaming turbulence cue for masking high IO debt. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StreamingTurbulenceSignal : ISignal
    {
        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float Debt01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public uint Sequence;
    }

    /// <summary>Orbital prologue re-entry phase packet. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AtmosphericReentrySignal : ISignal
    {
        public const byte PhaseApproach = 1;
        public const byte PhasePlasma = 2;
        public const byte PhaseWhiteout = 3;
        public const byte FlagAuthoritativeHeat = 1 << 0;
        public const byte FlagWhiteoutRequested = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)] public float AltitudeMeters;
        [FieldOffset(52)] public float UniverseVelocityMetersPerSecond;
        [FieldOffset(56)] public float Heat01;
        [FieldOffset(60)] public ushort Sequence;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte Phase;
    }

    /// <summary>Orbital prologue whiteout completion packet. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PrologueCompleteSignal : ISignal
    {
        public const byte PhaseWhiteout = 1;
        public const byte PhaseOceanHandoff = 2;
        public const byte FlagForceWhiteout = 1 << 0;

        [FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public float WhiteoutHoldSeconds;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public ushort Sequence;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte Phase;
    }

    /// <summary>Manual cockpit override latch packet emitted by physical VR lever controls. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ManualOverridePulledSignal : ISignal
    {
        public const byte FlagVrGrip = 1 << 0;
        public const byte FlagNonVrFallback = 1 << 1;
        public const byte FlagLatched = 1 << 2;
        public const byte HandUnknown = 0;
        public const byte HandLeft = 1;
        public const byte HandRight = 2;

        [FieldOffset(0)] public float3 LeverLocalPosition;
        [FieldOffset(12)] public float AngleDegrees;
        [FieldOffset(16)] public float GripStrength01;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public ushort Sequence;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte HandSide;
        [FieldOffset(32)] public float3 PivotLocalPosition;
        [FieldOffset(44)] public float VelocityDegreesPerSecond;
    }

    /// <summary>Hash-only HUD notification signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HUDNotificationSignal : ISignal
    {
        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public uint ContextHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Diegetic physical-HUD prompt signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DiegeticHudSignal : ISignal
    {
        public const byte PromptManualRelease = 1;
        public const byte FlagPersistent = 1 << 0;

        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public uint ContextHash;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte PromptKind;
        [FieldOffset(17)] public byte Priority;
        [FieldOffset(18)] public byte Flags;
    }

    /// <summary>Scan-log dirty-state signal for PDA, crafting, and barter consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct ScanLogChangedSignal : ISignal
    {
        public const byte ReasonLoaded = 1;
        public const byte ReasonEntryAdded = 2;
        public const byte ReasonRecentChanged = 3;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint EntryHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort EntryCount;
        [FieldOffset(14)] public ushort RecentCount;
        [FieldOffset(16)] public byte Reason;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(20)] public uint Revision;
        [FieldOffset(24)] public uint CategoryHash;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>PDA exchange dirty-state signal for barter UI and relay consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PdaExchangeStateChangedSignal : ISignal
    {
        public const byte ReasonExecuted = 1;
        public const byte ReasonLoaded = 2;
        public const byte ReasonInventoryChanged = 3;
        public const byte ReasonScanLogChanged = 4;
        public const byte FlagInventoryDirty = 1 << 0;
        public const byte FlagScanLogDirty = 1 << 1;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public int OfferCount;
        [FieldOffset(12)] public int RecentTransactionCount;
        [FieldOffset(16)] public int ExecutionStateCount;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Vehicle upgrade bitmask mutation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct VehicleUpgradesChangedSignal : ISignal
    {
        public const byte ReasonPenalty = 1;
        public const byte ReasonInstall = 2;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint UpgradeMask;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public float SafeDepthBonusMeters;
        [FieldOffset(16)] public float PermanentSafeDepthPenaltyMeters;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Cached platform thermal state transition signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermalStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Severity;
        [FieldOffset(13)] public byte PreviousSeverity;
        [FieldOffset(14)] public byte ThermalStatus;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(16)] public short TemperatureTenthsCelsius;
        [FieldOffset(18)] public byte BatteryPercent;
        [FieldOffset(20)] public uint ActionMask;
    }

    /// <summary>Cached platform battery level signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BatteryLevelSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte BatteryPercent;
        [FieldOffset(13)] public byte BatteryStatus;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(16)] public uint ActionMask;
    }

    /// <summary>Recon data signal for PDA map population. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReconDataSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint SourceId;
        [FieldOffset(56)] public byte ReconKind;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>Save start/end gate signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveLifecycleSignal : ISignal
    {
        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Macro database sector hydration completion lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MacroDatabaseSectorHydrationSignal : ISignal
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public long FileOffset;
        [FieldOffset(16)] public int PayloadBytes;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte SourceTier;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>WFC outpost generation completion lane. GridHandle resolves native packed cell data. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct WfcOutpostGeneratedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition OriginAup;
        [FieldOffset(48)] public ulong SectorHash;
        [FieldOffset(56)] public uint GridHandle;
        [FieldOffset(60)] public uint GenerationSequence;
        [FieldOffset(64)] public int3 Dimensions;
        [FieldOffset(76)] public float CellSizeMeters;
        [FieldOffset(80)] public float FloorHeightMeters;
        [FieldOffset(84)] public uint GridHash;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public ushort CellCount;
        [FieldOffset(94)] public ushort Flags;
    }

    /// <summary>WFC outpost mutable-cell state change lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WfcOutpostStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ushort CellIndex;
        [FieldOffset(10)] public byte PreviousFlags;
        [FieldOffset(11)] public byte CurrentFlags;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>WFC outpost door power state lane. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct WfcOutpostDoorPowerSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition DoorAup;
        [FieldOffset(48)] public ulong SectorHash;
        [FieldOffset(56)] public uint GridHandle;
        [FieldOffset(60)] public uint NodeId;
        [FieldOffset(64)] public ushort CellIndex;
        [FieldOffset(66)] public ushort DoorId;
        [FieldOffset(68)] public float Voltage;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public byte Unlocked;
        [FieldOffset(77)] public byte Flags;
    }

    /// <summary>Async persistence save request lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveRequestSignal : ISignal
    {
        public const byte ManualSlotFlag = 1 << 0;

        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte SlotIndex;
        [FieldOffset(13)] public byte Flags;
    }

    /// <summary>Async persistence completion lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveCompletedSignal : ISignal
    {
        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint DurationMilliseconds;
        [FieldOffset(12)] public uint CompressedSizeBytes;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Result;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Async persistence status lane payload for diegetic save indicators. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveStatusSignal : ISignal
    {
        public const byte Queued = 0;
        public const byte InProgress = 1;
        public const byte Completed = 2;
        public const byte Failed = 3;
        public const byte Rejected = 4;

        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Save metadata screenshot completion payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveMetadataReadySignal : ISignal
    {
        public const byte Completed = 1;
        public const byte SkippedLowTier = 2;
        public const byte Failed = 3;
        public const byte TimedOut = 4;
        public const byte ReusedExisting = 5;

        public const byte LowTierFlag = 1 << 0;
        public const byte FailureFlag = 1 << 1;
        public const byte ReusedExistingFlag = 1 << 2;

        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint ScreenshotBytes;
        [FieldOffset(12)] public uint ScreenshotHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Result;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Compliance violation signal. Size: 32 bytes.</summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ComplianceViolationSignal : ISignal
    {
        [FieldOffset(0)] public uint RuleHash;
        [FieldOffset(4)] public uint SystemHash;
        [FieldOffset(8)] public uint ContextHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Global time sync signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlobalTimeSyncSignal : ISignal
    {
        [FieldOffset(0)] public double WorldSeconds;
        [FieldOffset(8)] public float TimeScale;
        [FieldOffset(12)] public float MoonPhase01;
        [FieldOffset(16)] public uint Sequence;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>Deterministic seismic presentation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeismicSignal : ISignal
    {
        [FieldOffset(0)] public float3 Direction;
        [FieldOffset(12)] public float Intensity01;
        [FieldOffset(16)] public float CameraJitter01;
        [FieldOffset(20)] public float AudioIntensity01;
        [FieldOffset(24)] public float ThermalEruptionProbabilityScalar;
        [FieldOffset(28)] public ushort Sequence;
        [FieldOffset(30)] public byte DepthFlags;
        [FieldOffset(31)] public byte Flags;
    }

    /// <summary>Authoritative dispatcher time-dilation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TimeDilationSignal : ISignal
    {
        [FieldOffset(0)] public float Scalar;
        [FieldOffset(4)] public float UnscaledDeltaTime;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint ReasonHash;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>Simulation pause request signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SimulationPauseSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Paused;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(16)] public float RestoreScalar;
    }

    /// <summary>Cheap bullet-time post-process control signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BulletTimeVisualSignal : ISignal
    {
        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float Scalar;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint QualityTier;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>Weather strength signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WeatherStrengthSignal : ISignal
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float FlowFieldScale;
        [FieldOffset(8)] public uint WeatherHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Item decay/broken signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemDecaySignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ItemHash;
        [FieldOffset(52)] public float Durability01;
        [FieldOffset(56)] public ushort OwnerSlot;
        [FieldOffset(58)] public byte State;
        [FieldOffset(59)] public byte Flags;
    }

    public static class LightLevelSignalSampleKinds
    {
        public const byte CaveVoxelSdf = 1;
    }

    public static class LightLevelSignalFlags
    {
        public const byte ValidSample = 1 << 0;
    }

    /// <summary>Voxel lighting-to-physiology light sample. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LightLevelSignal : ISignal
    {
        [FieldOffset(0)] public float LightLevel01;
        [FieldOffset(4)] public float Darkness01;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte SampleKind;
        [FieldOffset(17)] public byte Flags;
    }

    public static class SubmarineLightsChangedSignalOperations
    {
        public const byte Remove = 0;
        public const byte Upsert = 1;
        public const byte ClearSource = 2;
    }

    public static class SubmarineLightsChangedSignalFlags
    {
        public const byte Powered = 1 << 0;
        public const byte BrownoutSuppressed = 1 << 1;
    }

    /// <summary>AUP-safe headlight registry delta. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct SubmarineLightsChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Forward;
        [FieldOffset(60)] public float RangeMeters;
        [FieldOffset(64)] public float Intensity;
        [FieldOffset(68)] public uint SourceId;
        [FieldOffset(72)] public ushort Slot;
        [FieldOffset(74)] public byte Operation;
        [FieldOffset(75)] public byte Flags;
        [FieldOffset(76)] public float SpotOuterCos;
    }

    public static class FaunaStateChangedSignalKinds
    {
        public const byte Blind = 1;
        public const byte Mutated = 2;
        public const byte Strike = 3;
    }

    public static class FaunaStateChangedSignalFlags
    {
        public const byte StateActive = 1 << 0;
    }

    /// <summary>Fauna high-level state transition signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct FaunaStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint StateFlags;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Slot;
        [FieldOffset(62)] public byte StateKind;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Authoritative player physiology state signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PhysiologyStateSignal : ISignal
    {
        [FieldOffset(0)] public float PlayerStress01;
        [FieldOffset(4)] public float O2DrainMultiplier;
        [FieldOffset(8)] public float Recovery01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Cause;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Player stress signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerStressSignal : ISignal
    {
        [FieldOffset(0)] public float Stress01;
        [FieldOffset(4)] public float OxygenDrainScale;
        [FieldOffset(8)] public float AggressionScale;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Cause;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Player trauma escalation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TraumaSignal : ISignal
    {
        [FieldOffset(0)] public uint TraumaHash;
        [FieldOffset(4)] public float Stress01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte TraumaKind;
        [FieldOffset(13)] public byte Severity;
        [FieldOffset(14)] public byte Flags;
    }

    /// <summary>Cache-contiguous combat damage lane payload. Size: 64 bytes.</summary>
    /// <summary>Camera position lane for foveated simulation. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CameraPositionSignal : ISignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float3 Forward;
        [FieldOffset(28)] public byte Flags;
    }

    /// <summary>Camera frustum lane for foveated simulation. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CameraFrustumSignal : ISignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Forward;
        [FieldOffset(24)] public float3 Up;
        [FieldOffset(36)] public float FieldOfViewDegrees;
        [FieldOffset(40)] public float NearClipMeters;
        [FieldOffset(44)] public float FarClipMeters;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CombatDamageSignal : ISignal
    {
        public const byte LegacyMirrorFlag = 1 << 0;
        public const byte DirectRuntimeFlag = 1 << 1;

        [FieldOffset(0)] public float3 WorldPoint;
        [FieldOffset(12)] public float3 Direction;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint DamageType;
        [FieldOffset(32)] public uint TargetHash;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public ushort SourceId;
        [FieldOffset(46)] public ushort TargetId;
        [FieldOffset(48)] public byte Channel;
        [FieldOffset(49)] public byte Flags;
        [FieldOffset(50)] public byte IntegrityDelta;
    }

    /// <summary>Visual hull dent notification lane for audio groans and non-authoritative feedback. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct HullDeformedSignal : ISignal
    {
        public const byte LowTierVisualOnlyFlag = 1 << 0;
        public const byte LegacyLocalPointFlag = 1 << 1;

        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float Depth;
        [FieldOffset(20)] public float Intensity01;
        [FieldOffset(24)] public uint TargetHash;
        [FieldOffset(28)] public uint SourceHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public ushort TargetId;
        [FieldOffset(38)] public ushort SourceId;
        [FieldOffset(40)] public byte ActiveDentCount;
        [FieldOffset(41)] public byte Flags;
        [FieldOffset(42)] public byte QualityTier;
        [FieldOffset(43)] public byte Channel;
        [FieldOffset(44)] public uint DamageType;
    }

    /// <summary>Authoritative hull dent repair completion lane for atmosphere sealing and repair feedback. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct HullRepairedSignal : ISignal
    {
        public const byte CompletedFlag = 1 << 0;
        public const byte LowTierVisualOnlyFlag = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition HitAup;
        [FieldOffset(48)] public int RoomId;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte DentIndex;
        [FieldOffset(61)] public byte DentsRepairedCount;
        [FieldOffset(62)] public byte QualityTier;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Habitat module deformation reached the compromise threshold. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseModuleCompromisedSignal : ISignal
    {
        public const ushort MaxDeformationFlag = 1 << 0;
        public const ushort LowTierVisualOnlyFlag = 1 << 1;

        [FieldOffset(0)] public float3 ModuleCenter;
        [FieldOffset(12)] public float Stress01;
        [FieldOffset(16)] public float PeakStress01;
        [FieldOffset(20)] public float DepthMeters;
        [FieldOffset(24)] public uint NodeId;
        [FieldOffset(28)] public uint ModuleHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public ushort SourceId;
        [FieldOffset(42)] public ushort Flags;
        [FieldOffset(44)] public byte StressIndex;
        [FieldOffset(45)] public byte QualityTier;
        [FieldOffset(46)] public ushort Reserved0;
    }

    /// <summary>Player entered a habitat/base volume. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlayerBaseEnterSignal : ISignal
    {
        public const ushort DirectPlayerInsideFlag = 1 << 0;
        public const ushort SanitizedBaseCenterFlag = 1 << 15;

        [FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;
        [FieldOffset(48)] public int BaseId;
        [FieldOffset(52)] public int RoomId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    /// <summary>Player exited a habitat/base volume. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlayerBaseExitSignal : ISignal
    {
        public const ushort DirectPlayerOutsideFlag = 1 << 0;
        public const ushort SanitizedBaseCenterFlag = 1 << 15;

        [FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;
        [FieldOffset(48)] public int BaseId;
        [FieldOffset(52)] public int RoomId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    /// <summary>Weather transition lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WeatherChangedSignal : ISignal
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float FlowFieldScale;
        [FieldOffset(8)] public uint PreviousWeatherHash;
        [FieldOffset(12)] public uint WeatherHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte QualityTier;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>System pause lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemPauseSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Paused;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(16)] public float RestoreScalar;
    }

    /// <summary>Global simulation-bucket presentation sync lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SimulationBucketSyncSignal : ISignal
    {
        [FieldOffset(0)] public float InterpolationAlpha;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public int ActiveSlowBucket;
        [FieldOffset(12)] public int SlowBucketMask;
        [FieldOffset(16)] public uint RebalanceSequence;
        [FieldOffset(20)] public byte ActiveSlowBucketCount;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Frame-pacing warning lane emitted by the master modulo orchestrator. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FramePacingWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public float CurrentFrameMs;
        [FieldOffset(16)] public float TargetFrameMs;
        [FieldOffset(20)] public float PreSimulationMs;
        [FieldOffset(24)] public float ActiveBucketLoadMs;
        [FieldOffset(28)] public float JitterVarianceMs;
        [FieldOffset(32)] public float ExpectedMaxBucketLoadMs;
        [FieldOffset(36)] public float ExpectedMeanBucketLoadMs;
        [FieldOffset(40)] public int ActiveSlowBucket;
        [FieldOffset(44)] public int SlowBucketMask;
        [FieldOffset(48)] public uint RebalanceSequence;
        [FieldOffset(52)] public byte Severity;
    }

    /// <summary>Applies a committed AUP shift to runtime-space combat signal coordinates.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CombatDamageSignalAupShiftTransformer : ISignalSnapshotTransformer<CombatDamageSignal>
    {
        private float3 _shiftMeters;

        public void SetShift(float3 shiftMeters)
        {
            _shiftMeters = shiftMeters;
        }

        public void Transform(ref CombatDamageSignal signal)
        {
            signal.WorldPoint += _shiftMeters;
        }
    }
}
