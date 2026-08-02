using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;
using AbsoluteUniversePositionBlit = Hecton8.World.AbsoluteUniversePositionBlit;
using BiomeChangedSignal = Hecton8.Core.Contracts.Signals.BiomeChangedSignal;
using CameraFrustumSignal = Hecton8.Core.Contracts.Signals.CameraFrustumSignal;
using CameraPositionSignal = Hecton8.Core.Contracts.Signals.CameraPositionSignal;
using CombatDamageSignal = Hecton8.Core.Contracts.Signals.CombatDamageSignal;
using CrashTelemetrySignal = Hecton8.Core.Contracts.Signals.CrashTelemetrySignal;
using FocusBrokenSignal = Hecton8.Core.Contracts.Signals.FocusBrokenSignal;
using MixerStateSignal = Hecton8.Core.Contracts.Signals.MixerStateSignal;
using NarrativeFocusSignal = Hecton8.Core.Contracts.Signals.NarrativeFocusSignal;
using NarrativeHudWaypointSignal = Hecton8.Core.Contracts.Signals.NarrativeHudWaypointSignal;
using NarrativePoiStateSignal = Hecton8.Core.Contracts.Signals.NarrativePoiStateSignal;
using ProgressionEventSignal = Hecton8.Core.Contracts.Signals.ProgressionEventSignal;
using SoundscapeProfileSignal = Hecton8.Core.Contracts.Signals.SoundscapeProfileSignal;
using SurvivalVitalsChangedSignal = Hecton8.Core.Contracts.Signals.SurvivalVitalsChangedSignal;
using ToxicBioluminescenceSignal = Hecton8.Atmosphere.ToxicBioluminescenceSignal;
using ToxicityExposureSignal = Hecton8.Atmosphere.ToxicityExposureSignal;
using HullRepairedSignal = Hecton8.Core.Contracts.Signals.HullRepairedSignal;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Registry for every closed <see cref="SignalBus{T}"/> lane touched this session.
    /// </summary>
    [Preserve]
    public static unsafe class SignalBusRegistry
    {
        private const int LaneCapacity = 512;
        private const int StressScale = 1000;

        private static SignalLaneDispatch[] _laneDispatch;
        private static int _laneCount;
        private static int _registrationOverflow;
        private static int _registrationGate;
        private static int _globalQualityMilli = StressScale;
        private static int _systemStressMilli;
        private static int _simulationHalted;
        private static int _systemKillSwitchMask;
        private static int _systemKillSwitchDropCount;
        private static IDataVault _dataVault;

        /// <summary>Current active typed lane count.</summary>
        public static int LaneCount => Volatile.Read(ref _laneCount);

        /// <summary>True after any lane failed registration because registry capacity was exhausted.</summary>
        public static bool RegistrationOverflow => Volatile.Read(ref _registrationOverflow) != 0;

        /// <summary>Number of registered lanes flushed through the native operation table.</summary>
        public static int DispatchLaneCount => Volatile.Read(ref _laneCount);

        /// <summary>Compatibility alias for older diagnostics. All active lanes now use the native dispatch table.</summary>
        [Obsolete("Use DispatchLaneCount. Fallback dispatch was removed when hardcoded DTO tables were deleted.", false)]
        public static int FallbackLaneCount => DispatchLaneCount;

        /// <summary>Runtime stress scalar in [0..1], quantized to avoid float tearing.</summary>
        public static float SystemStress01 => math.saturate(Volatile.Read(ref _systemStressMilli) * 0.001f);

        /// <summary>Continuous corridor quality scalar in [0..1], quantized to avoid float tearing.</summary>
        public static float GlobalQualityWeight01 => math.saturate(Volatile.Read(ref _globalQualityMilli) * 0.001f);

        /// <summary>True after a fatal signal requests an immediate dispatcher halt.</summary>
        public static bool IsSimulationHalted => Volatile.Read(ref _simulationHalted) != 0;

        /// <summary>Emergency bits raised by the signal corridor without hot registry mutation.</summary>
        public static uint RuntimeKillSwitchMask => unchecked((uint)Volatile.Read(ref _systemKillSwitchMask));

        /// <summary>Compatibility alias for diagnostics that still name the overflow-only source.</summary>
        public static uint SignalOverflowKillSwitchMask => RuntimeKillSwitchMask;

        internal static int SystemStressMilli => Volatile.Read(ref _systemStressMilli);
        internal static int GlobalQualityMilli => Volatile.Read(ref _globalQualityMilli);

        public static void ClearSystemKillSwitchBits()
        {
            Volatile.Write(ref _systemKillSwitchMask, 0);
        }

        internal static void SetSignalOverflowKillSwitchBits(uint mask, uint sourceHash)
        {
            SetSystemKillSwitchBits(mask, true, sourceHash);
        }

        public static void SetSystemKillSwitchBits(uint mask, bool enabled, uint sourceHash)
        {
            int bitMask = unchecked((int)mask);
            if (bitMask == 0)
                return;

            int observed;
            int next;
            do
            {
                observed = Volatile.Read(ref _systemKillSwitchMask);
                next = enabled ? observed | bitMask : observed & ~bitMask;
                if (next == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _systemKillSwitchMask, next, observed) != observed);

            uint changedMask = unchecked((uint)(observed ^ next));

            SystemKillSwitchBitsSignal signal = default;
            signal.Frame = global::Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceHash = sourceHash;
            signal.PreviousMask = unchecked((uint)observed);
            signal.CurrentMask = unchecked((uint)next);
            signal.ChangedMask = changedMask;
            signal.EnabledMask = enabled ? changedMask : 0u;
            signal.Flags = SystemKillSwitchBitsSignal.FlagRuntimeOwner;
            if (enabled)
                signal.Flags |= SystemKillSwitchBitsSignal.FlagEnabled;
            SignalBus<SystemKillSwitchBitsSignal>.TryPushTracked(in signal, ref _systemKillSwitchDropCount);
        }

        /// <summary>Binds the Vault used by per-lane snapshot buffers from cold registry ownership routes.</summary>
        public static void BindDataVaultCold(IDataVault vault)
        {
            _dataVault = vault;
        }

        internal static bool TryGetBoundDataVault(out IDataVault vault)
        {
            vault = _dataVault;
            return vault != null && !vault.IsCompactionFenceActive;
        }

        internal static bool Register(
            delegate*<void> dispose,
            delegate*<int, void> flush,
            delegate*<ref SignalLaneTelemetry, void> copyTelemetry,
            bool flushDuringSimulationPause)
        {
            if (dispose == null || flush == null || copyTelemetry == null)
                return false;

            EnterRegistrationGate();
            try
            {
                if (!EnsureDispatchStorage())
                {
                    Volatile.Write(ref _registrationOverflow, 1);
                    return false;
                }

                int laneCount = _laneCount;
                for (int i = 0; i < laneCount; i++)
                {
                    if ((IntPtr)_laneDispatch[i].Dispose == (IntPtr)dispose)
                        return true;
                }

                if (laneCount >= LaneCapacity)
                {
                    int firstOverflow = Interlocked.Exchange(ref _registrationOverflow, 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (firstOverflow == 0)
                        Hecton8.Core.H8Debug.LogError("[SIGNAL LANE REGISTRY OVERFLOW]");
#endif
                    return false;
                }

                _laneDispatch[laneCount] = new SignalLaneDispatch(
                    dispose,
                    flush,
                    copyTelemetry,
                    flushDuringSimulationPause);
                Volatile.Write(ref _laneCount, laneCount + 1);
                return true;
            }
            finally
            {
                ExitRegistrationGate();
            }
        }

        /// <summary>Sets the runtime stress scalar that controls optional lane propagation.</summary>
        /// <param name="stress01">Stress in [0..1]. Non-finite values clamp to full stress.</param>
        public static void SetSystemStress01(float stress01)
        {
            float sanitized = math.isfinite(stress01) ? math.saturate(stress01) : 1f;
            Volatile.Write(ref _systemStressMilli, (int)math.round(sanitized * StressScale));
        }

        /// <summary>Sets the continuous quality scalar that controls lane caps and coalescing pressure.</summary>
        /// <param name="quality01">Quality in [0..1]. Non-finite values clamp to survival minimum.</param>
        public static void SetGlobalQualityWeight01(float quality01)
        {
            float sanitized = math.isfinite(quality01) ? math.saturate(quality01) : 0f;
            Volatile.Write(ref _globalQualityMilli, (int)math.round(sanitized * StressScale));
        }

        /// <summary>Sets the fatal-interrupt latch checked by dispatcher bucket loops.</summary>
        public static void SetSimulationHalted()
        {
            Volatile.Write(ref _simulationHalted, 1);
        }

        /// <summary>Clears the fatal-interrupt latch during deterministic startup/reset.</summary>
        public static void ClearSimulationHalt()
        {
            Volatile.Write(ref _simulationHalted, 0);
        }

        /// <summary>Flushes every active signal queue into contiguous frame snapshots for the next frame.</summary>
        public static void FlushPostSimulation()
        {
            int systemStressMilli = Volatile.Read(ref _systemStressMilli);
            bool simulationPaused = SimulationSignalRoute.SimulationPaused;
            FlushRegisteredSignalLanes(systemStressMilli, simulationPaused);
        }

        /// <summary>Disposes every typed lane. Called on subsystem reset and application quit.</summary>
        public static void DisposeAll()
        {
            EnterRegistrationGate();
            try
            {
                int laneCount = _laneCount;
                SignalLaneDispatch[] dispatch = _laneDispatch;
                for (int i = 0; dispatch != null && i < laneCount && i < dispatch.Length; i++)
                {
                    delegate*<void> dispose = dispatch[i].Dispose;
                    if (dispose != null)
                        dispose();
                }

                if (dispatch != null)
                    Array.Clear(dispatch, 0, dispatch.Length);

                _laneDispatch = null;

                Volatile.Write(ref _laneCount, 0);
                Volatile.Write(ref _registrationOverflow, 0);
                Volatile.Write(ref _globalQualityMilli, StressScale);
                Volatile.Write(ref _systemStressMilli, 0);
                Volatile.Write(ref _simulationHalted, 0);
                Volatile.Write(ref _systemKillSwitchMask, 0);
                _dataVault = null;
            }
            finally
            {
                ExitRegistrationGate();
            }
        }

        /// <summary>Copies per-lane telemetry into a caller-owned buffer.</summary>
        /// <param name="destination">Destination buffer.</param>
        /// <returns>Number of copied entries.</returns>
        public static int CopyTelemetry(NativeArray<SignalLaneTelemetry> destination)
        {
            if (!destination.IsCreated || destination.Length == 0)
                return 0;

            SignalLaneDispatch[] dispatch = _laneDispatch;
            if (dispatch == null)
                return 0;

            int copyCount = Math.Min(Math.Min(Volatile.Read(ref _laneCount), dispatch.Length), destination.Length);
            for (int i = 0; i < copyCount; i++)
            {
                SignalLaneTelemetry telemetry = default;
                delegate*<ref SignalLaneTelemetry, void> copyTelemetry = dispatch[i].CopyTelemetry;
                if (copyTelemetry != null)
                    copyTelemetry(ref telemetry);

                destination[i] = telemetry;
            }

            return copyCount;
        }

        internal static bool TryCopyTelemetryAt(int index, out SignalLaneTelemetry telemetry)
        {
            telemetry = default;
            int laneCount = Volatile.Read(ref _laneCount);
            if ((uint)index >= (uint)laneCount)
                return false;

            SignalLaneDispatch[] dispatch = _laneDispatch;
            if (dispatch == null)
                return false;

            if ((uint)index >= (uint)dispatch.Length)
                return false;

            delegate*<ref SignalLaneTelemetry, void> copyTelemetry = dispatch[index].CopyTelemetry;
            if (copyTelemetry == null)
                return false;

            copyTelemetry(ref telemetry);
            return true;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        internal readonly struct SignalLaneDispatch
        {
            [FieldOffset(0)] public readonly delegate*<void> Dispose;
            [FieldOffset(8)] public readonly delegate*<int, void> Flush;
            [FieldOffset(16)] public readonly delegate*<ref SignalLaneTelemetry, void> CopyTelemetry;
            [FieldOffset(24)] private readonly uint _pad0;
            [FieldOffset(28)] private readonly ushort _pad1;
            [FieldOffset(30)] public readonly byte FlushDuringSimulationPause;
            [FieldOffset(31)] private readonly byte _pad2;

            public SignalLaneDispatch(
                delegate*<void> dispose,
                delegate*<int, void> flush,
                delegate*<ref SignalLaneTelemetry, void> copyTelemetry,
                bool flushDuringSimulationPause)
            {
                Dispose = dispose;
                Flush = flush;
                CopyTelemetry = copyTelemetry;
                _pad0 = 0u;
                _pad1 = 0;
                FlushDuringSimulationPause = flushDuringSimulationPause ? (byte)1 : (byte)0;
                _pad2 = 0;
            }
        }

        private static void FlushRegisteredSignalLanes(int systemStressMilli, bool simulationPaused)
        {
            SignalLaneDispatch[] dispatch = _laneDispatch;
            if (dispatch == null)
                return;

            int dispatchCount = Math.Min(Volatile.Read(ref _laneCount), dispatch.Length);
            for (int i = 0; i < dispatchCount; i++)
            {
                SignalLaneDispatch laneDispatch = dispatch[i];
                if (laneDispatch.Flush == null ||
                    (simulationPaused && laneDispatch.FlushDuringSimulationPause == 0))
                {
                    continue;
                }

                laneDispatch.Flush(systemStressMilli);
            }
        }

        private static bool EnsureDispatchStorage()
        {
            if (_laneDispatch != null)
                return true;

            _laneDispatch = new SignalLaneDispatch[LaneCapacity];
            return _laneDispatch.Length == LaneCapacity;
        }

        private static void EnterRegistrationGate()
        {
            SpinWait spin = default;
            while (Interlocked.CompareExchange(ref _registrationGate, 1, 0) != 0)
                spin.SpinOnce();
        }

        private static void ExitRegistrationGate()
        {
            Volatile.Write(ref _registrationGate, 0);
        }
    }

    /// <summary>
    /// Typed unmanaged signal lane. Each closed generic type owns a bounded MPSC ring and frame snapshot.
    /// </summary>
    /// <typeparam name="T">Unmanaged signal payload type.</typeparam>
    [Preserve]
    public static class SignalBus<T>
        where T : unmanaged, ISignal
    {
        private const int DefaultExpectedCapacity = 64;
        private const int DefaultMaxFrameSignals = DefaultExpectedCapacity;
        private const int DefaultSurvivalFrameSignals = 16;
        private const int LaneOverflowFaultThreshold = 1024;
        private const int HighEndOverkillStressMilli = 200;
        private const int TryPushShedStressMilli = 850;
        private const uint LaneOverflowFaultHash = 0x4C4F5646u; // LOVF
        private const uint NonCriticalVfxKillSwitchMask = 1u << 20;
        private const uint SnapshotBufferIdPrefix = 0x40000000u;
        private const uint SnapshotBufferIdMask = 0x3FFFFFFFu;
        private const ushort LayoutPolicyCacheLineCritical = 1;
        private const byte TelemetryFlagCacheLineStrideDebt = 32;
        private const int TelemetryLayoutPolicyMask = 0x00FF;
        private const int ParallelWriterBudgetRemainingIndex = 0;
        private const int ParallelWriterBudgetDroppedIndex = 1;
        private const int ParallelWriterBudgetLength = 2;

        private static global::Hecton8.Core.MpscSignalRingBuffer<T> _ring;
        private static NativeArray<int> _parallelWriterBudget;
        private static VaultGenerationHandle<T> _frameSnapshotHandle;
        private static IDataVault _frameSnapshotVault;
        private static VaultGenerationHandle<T> _frameSnapshotActiveWriteHandle;
        private static IDataVault _frameSnapshotActiveWriteVault;
        private static BufferID _frameSnapshotBufferId;
        private static int _frameSnapshotCount;
        private static int _frameSnapshotGeneration;
        private static int _expectedCapacity = DefaultExpectedCapacity;
        private static int _maxFrameSignals = DefaultMaxFrameSignals;
        private static int _survivalFrameSignals = DefaultSurvivalFrameSignals;
        private static int _legacyReadCursor;
        private static int _queuedBeforeFlush;
        private static int _pushedLastFlush;
        private static int _droppedLastFlush;
        private static int _droppedPendingFlush;
        private static int _coalescedLastFlush;
        private static int _coalescedTotal;
        private static int _stormDetectedLastFlush;
        private static int _loadShedTotal;
        private static int _corruptedSignalTotal;
        private static int _acceptedSignalTotal;
        private static T _latestSignal;
        private static int _latestSignalSequence;
        private static int _peakQueuedLastFlush;
        private static bool _initialized;
        private static bool _registered;
        private static bool _configured;
        private static bool _layoutFaultLogged;
        private static bool _configurationFaultLogged;
        private static uint _laneHash;
        private static ushort _layoutPolicyFlags;
        private static readonly uint _defaultLaneHash = ComputeTypeHash();

        /// <summary>Stable lane hash used by telemetry and load-shedding reports.</summary>
        public static uint LaneHash
        {
            get
            {
                return _laneHash != 0u ? _laneHash : _defaultLaneHash;
            }
        }

        /// <summary>Current frame snapshot element count.</summary>
        public static int SnapshotCount
        {
            get
            {
                return TryReadFrameSnapshot(out _, out int count) ? count : 0;
            }
        }

        /// <summary>Monotonic snapshot generation advanced by the dispatcher post-simulation flush.</summary>
        public static int SnapshotGeneration
        {
            get
            {
                return TryReadFrameSnapshot(out _, out _) ? _frameSnapshotGeneration : 0;
            }
        }

        /// <summary>Signals dropped during the most recent flush.</summary>
        public static int DroppedLastFlush => _droppedLastFlush;

        /// <summary>Total cosmetic/load-shed drops since the lane was initialized.</summary>
        public static int LoadShedTotal => Volatile.Read(ref _loadShedTotal);

        /// <summary>Total rejected non-finite payloads since the lane was initialized.</summary>
        public static int CorruptedSignalTotal => Volatile.Read(ref _corruptedSignalTotal);

        /// <summary>Peak queue depth observed at the last post-simulation flush.</summary>
        public static int PeakQueuedLastFlush => Volatile.Read(ref _peakQueuedLastFlush);

        /// <summary>True when the lane owns native ring storage. Pure readiness probe; initialization is explicit.</summary>
        public static bool HasNativeStorage => _ring.IsCreated;

        /// <summary>Opens the first-party bounded MPSC ring writer for job producers.</summary>
        public static global::Hecton8.Core.MpscSignalRingBuffer<T>.ParallelWriter OpenParallelWriter()
        {
            EnsureInitialized();
            return _ring.IsCreated ? _ring.AsParallelWriter() : default;
        }

        /// <summary>Native writer budget shared by job producers for pre-enqueue bounded shedding.</summary>
        public static NativeArray<int> ParallelWriterBudget
        {
            get
            {
                EnsureInitialized();
                return _parallelWriterBudget;
            }
        }

        /// <summary>Bounded producer writer facade backed by the first-party MPSC ring.</summary>
        public static global::Hecton8.Core.MpscSignalRingBuffer<T>.ParallelWriter ParallelWriter
        {
            get
            {
                return OpenParallelWriter();
            }
        }

        /// <summary>Compatibility alias for producers that already migrated to the explicit ring name.</summary>
        public static global::Hecton8.Core.MpscSignalRingBuffer<T>.ParallelWriter RingParallelWriter
        {
            get
            {
                return OpenParallelWriter();
            }
        }

        /// <summary>
        /// Configures lane capacity and telemetry. Call from bootstrap before first push.
        /// </summary>
        public static void Configure(int expectedCapacity, int maxFrameSignals = 0, int lowTierFrameSignals = 0, uint laneHash = 0u)
        {
            ConfigureInternal(expectedCapacity, maxFrameSignals, lowTierFrameSignals, laneHash, 0);
        }

        /// <summary>
        /// Configures a high-contention lane whose payload should migrate toward 64/128-byte cache-line strides.
        /// </summary>
        public static void ConfigureCacheLineCritical(int expectedCapacity, int maxFrameSignals = 0, int lowTierFrameSignals = 0, uint laneHash = 0u)
        {
            ConfigureInternal(expectedCapacity, maxFrameSignals, lowTierFrameSignals, laneHash, LayoutPolicyCacheLineCritical);
        }

        private static void ConfigureInternal(int expectedCapacity, int maxFrameSignals, int lowTierFrameSignals, uint laneHash, ushort layoutPolicyFlags)
        {
            if (SignalLanePolicyCache<T>.TryResolveDefaultContract(
                    out int contractExpectedCapacity,
                    out int contractMaxFrameSignals,
                    out int contractLowTierFrameSignals,
                    out uint contractLaneHash))
            {
                expectedCapacity = contractExpectedCapacity;
                maxFrameSignals = contractMaxFrameSignals;
                lowTierFrameSignals = contractLowTierFrameSignals;
                laneHash = contractLaneHash;
            }

            int resolvedExpectedCapacity = Math.Max(1, expectedCapacity);
            int resolvedMaxFrameSignals = maxFrameSignals > 0
                ? maxFrameSignals
                : resolvedExpectedCapacity;
            int resolvedLowTierFrameSignals = lowTierFrameSignals > 0
                ? lowTierFrameSignals
                : ResolveDefaultLowTierFrameSignals(resolvedExpectedCapacity, resolvedMaxFrameSignals);
            int normalizedMaxFrameSignals = Math.Max(1, resolvedMaxFrameSignals);
            int normalizedSurvivalFrameSignals = Math.Max(1, Math.Min(resolvedLowTierFrameSignals, normalizedMaxFrameSignals));
            uint resolvedLaneHash = laneHash != 0u ? laneHash : _defaultLaneHash;

            if (_initialized)
            {
                if (_expectedCapacity == resolvedExpectedCapacity &&
                    _maxFrameSignals == normalizedMaxFrameSignals &&
                    _survivalFrameSignals == normalizedSurvivalFrameSignals &&
                    _laneHash == resolvedLaneHash &&
                    _layoutPolicyFlags == layoutPolicyFlags)
                {
                    _configured = true;
                    EnsureRegistered();
                    return;
                }

                Interlocked.Increment(ref _corruptedSignalTotal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_configurationFaultLogged)
                {
                    _configurationFaultLogged = true;
                    Hecton8.Core.H8Debug.LogError("[SIGNAL CONTRACT] Rejected late reconfigure.");
                }
#endif
                return;
            }

            _expectedCapacity = resolvedExpectedCapacity;
            _maxFrameSignals = normalizedMaxFrameSignals;
            _survivalFrameSignals = normalizedSurvivalFrameSignals;
            _laneHash = resolvedLaneHash;
            _layoutPolicyFlags = layoutPolicyFlags;
            _configured = true;
            EnsureRegistered();
        }

        private static int ResolveDefaultLowTierFrameSignals(int expectedCapacity, int maxFrameSignals)
        {
            int quarterCapacity = Math.Max(1, expectedCapacity >> 2);
            return Math.Min(quarterCapacity, Math.Max(1, maxFrameSignals));
        }

        /// <summary>Ensures native storage exists for this lane.</summary>
        public static void EnsureInitialized()
        {
            ApplyKnownContractDefaultsIfUnconfigured();
            EnsureRegistered();
            if (_initialized)
                return;

            if (!HasValidPayloadStride())
            {
                Interlocked.Increment(ref _corruptedSignalTotal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_layoutFaultLogged)
                {
                    _layoutFaultLogged = true;
                    Hecton8.Core.H8Debug.LogError("[SIGNAL ABI FENCE] Rejected payload stride.");
                }
#endif
                return;
            }

            _ring = new global::Hecton8.Core.MpscSignalRingBuffer<T>(
                _expectedCapacity,
                Allocator.Persistent,
                Hecton8.Core.Memory.SystemID.CoreDataVault);
            if (!_ring.IsCreated)
            {
                _ring.Dispose();
                _ring = default;
                return;
            }

            if (!TryAcquireFrameSnapshotBuffer(_maxFrameSignals))
            {
                _ring.Dispose();
                _ring = default;
                return;
            }

            _parallelWriterBudget = H8Memory.Allocate<int>(
                ParallelWriterBudgetLength,
                Hecton8.Core.Memory.SystemID.CoreDataVault,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD NATIVE: NativeArray<int>[2] via H8Memory - per-lane writer budget/drop counter - owner: SignalBus<T>
            if (!_parallelWriterBudget.IsCreated)
            {
                _ring.Dispose();
                _ring = default;
                ReleaseFrameSnapshotBuffer();
                return;
            }

            ResetParallelWriterBudget();
            _legacyReadCursor = 0;
            _frameSnapshotCount = 0;
            _frameSnapshotGeneration = 0;
            _queuedBeforeFlush = 0;
            _pushedLastFlush = 0;
            _droppedLastFlush = 0;
            _droppedPendingFlush = 0;
            _coalescedLastFlush = 0;
            _coalescedTotal = 0;
            _stormDetectedLastFlush = 0;
            _loadShedTotal = 0;
            _corruptedSignalTotal = 0;
            _acceptedSignalTotal = 0;
            _latestSignal = default;
            _latestSignalSequence = 0;
            _peakQueuedLastFlush = 0;
            _initialized = true;
        }

        /// <summary>Pushes one signal into this type's ring.</summary>
        /// <param name="signal">Signal payload.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Push(in T signal)
        {
            TryPush(in signal);
        }

        /// <summary>Attempts to push one signal, applying finite guards and optional load shedding before enqueue.</summary>
        /// <param name="signal">Signal payload.</param>
        /// <returns>True when the payload entered the lane; false when it was shed or rejected.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryPush(in T signal)
        {
            EnsureInitialized();
            if (!_ring.IsCreated)
                return false;

            if (SignalLanePolicyCache<T>.NonCriticalVfx &&
                SignalBusRegistry.SystemStressMilli > TryPushShedStressMilli)
            {
                Interlocked.Increment(ref _loadShedTotal);
                Interlocked.Increment(ref _droppedPendingFlush);
                return false;
            }

            T sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
            {
                Interlocked.Increment(ref _corruptedSignalTotal);
                global::Hecton8.Core.GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);
                return false;
            }

            if (SignalLanePolicyCache<T>.FatalInterrupt)
                SignalBusRegistry.SetSimulationHalted();

            if (!_ring.TryEnqueue(in sanitizedSignal))
            {
                Interlocked.Increment(ref _loadShedTotal);
                Interlocked.Increment(ref _droppedPendingFlush);
                return false;
            }

            _latestSignal = sanitizedSignal;
            AdvanceLatestSignalSequence();
            Interlocked.Increment(ref _acceptedSignalTotal);
            return true;
        }

        /// <summary>Attempts to push one signal and increments caller-owned refusal telemetry when the lane rejects it.</summary>
        /// <param name="signal">Signal payload.</param>
        /// <param name="ownerDroppedSignalCount">Caller-owned drop counter for local black-box or status reporting.</param>
        /// <returns>True when the payload entered the lane; false when it was shed or rejected.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryPushTracked(in T signal, ref int ownerDroppedSignalCount)
        {
            if (TryPush(in signal))
                return true;

            IncrementOwnerDropCounter(ref ownerDroppedSignalCount);
            return false;
        }

        /// <summary>Attempts a Burst/job writer enqueue through the first-party bounded MPSC ring.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryEnqueueBounded(
            global::Hecton8.Core.MpscSignalRingBuffer<T>.ParallelWriter writer,
            NativeArray<int> writerBudget,
            T signal)
        {
            if (!writerBudget.IsCreated || writerBudget.Length < ParallelWriterBudgetLength)
                return false;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writerBudget);

            int remainingAfterClaim = Interlocked.Decrement(ref budget[ParallelWriterBudgetRemainingIndex]);
            if (remainingAfterClaim < 0)
            {
                Interlocked.Increment(ref budget[ParallelWriterBudgetDroppedIndex]);
                return false;
            }

            if (writer.TryEnqueue(in signal))
                return true;

            Interlocked.Increment(ref budget[ParallelWriterBudgetDroppedIndex]);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IncrementOwnerDropCounter(ref int counter)
        {
            int current = Volatile.Read(ref counter);
            if (current < int.MaxValue)
                Interlocked.Increment(ref counter);
        }

        public static bool TryGetLatest(out T signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestSignalSequence);
            signal = _latestSignal;
            return sequence != 0;
        }

        /// <summary>Returns a contiguous read-only view over the current frame snapshot.</summary>
        public static unsafe ReadOnlySpan<T> GetFrameSnapshot()
        {
            if (!TryReadFrameSnapshot(out NativeArray<T>.ReadOnly frameSnapshot, out int snapshotCount) ||
                snapshotCount == 0)
            {
                return ReadOnlySpan<T>.Empty;
            }

            T* pointer = (T*)frameSnapshot.GetUnsafeReadOnlyPtr();
            return new ReadOnlySpan<T>(pointer, snapshotCount);
        }

        /// <summary>Alias for consumers that read the current deterministic signal snapshot.</summary>
        public static ReadOnlySpan<T> GetSignals()
        {
            return GetFrameSnapshot();
        }

        /// <summary>Returns a NativeArray read-only snapshot for Burst jobs.</summary>
        public static unsafe NativeArray<T>.ReadOnly GetFrameSnapshotArray()
        {
            if (!TryReadFrameSnapshot(out NativeArray<T>.ReadOnly frameSnapshot, out int snapshotCount) ||
                snapshotCount == 0)
            {
                return default;
            }

            void* pointer = frameSnapshot.GetUnsafeReadOnlyPtr();
            NativeArray<T> view = H8Memory.CreateNativeArrayView<T>(pointer, snapshotCount);
            return view.AsReadOnly();
        }

        /// <summary>Legacy destructive consumer over the current frame snapshot.</summary>
        public static bool TryConsumeFrame(out T signal)
        {
            if (!TryReadFrameSnapshot(out NativeArray<T>.ReadOnly frameSnapshot, out int snapshotCount) ||
                _legacyReadCursor >= snapshotCount)
            {
                signal = default;
                return false;
            }

            signal = frameSnapshot[_legacyReadCursor++];
            return true;
        }

        /// <summary>Transforms each snapshot payload in-place without boxing.</summary>
        public static void TransformSnapshot<TTransformer>(TTransformer transformer)
            where TTransformer : struct, ISignalSnapshotTransformer<T>
        {
            if (!TryAcquireFrameSnapshotForOwnerWrite(out NativeArray<T> frameSnapshot))
                return;

            try
            {
                int snapshotCount = _frameSnapshotCount;
                if (snapshotCount <= 0)
                    return;

                for (int i = 0; i < snapshotCount; i++)
                {
                    T signal = frameSnapshot[i];
                    transformer.Transform(ref signal);
                    frameSnapshot[i] = signal;
                }

                AdvanceFrameSnapshotGeneration();
            }
            finally
            {
                ReleaseFrameSnapshotOwnerWrite();
            }
        }

        /// <summary>Compacts the current snapshot in-place, dropping signals rejected by the filter.</summary>
        public static int FilterSnapshot<TFilter>(TFilter filter)
            where TFilter : struct, ISignalSnapshotFilter<T>
        {
            if (!TryAcquireFrameSnapshotForOwnerWrite(out NativeArray<T> frameSnapshot))
                return 0;

            try
            {
                int snapshotCount = _frameSnapshotCount;
                if (snapshotCount == 0)
                    return 0;

                int writeIndex = 0;
                int originalLength = snapshotCount;
                for (int readIndex = 0; readIndex < originalLength; readIndex++)
                {
                    T signal = frameSnapshot[readIndex];
                    if (!filter.Keep(in signal))
                        continue;

                    if (writeIndex != readIndex)
                        frameSnapshot[writeIndex] = signal;

                    writeIndex++;
                }

                int dropped = originalLength - writeIndex;
                if (dropped <= 0)
                    return 0;

                _frameSnapshotCount = writeIndex;
                AdvanceFrameSnapshotGeneration();
                _droppedLastFlush += dropped;
                Interlocked.Add(ref _loadShedTotal, dropped);
                return dropped;
            }
            finally
            {
                ReleaseFrameSnapshotOwnerWrite();
            }
        }

        internal static void FlushPostSimulation(int systemStressMilli)
        {
            if (!_initialized)
                return;

            if (!TryAcquireFrameSnapshotForOwnerWrite(out NativeArray<T> frameSnapshot))
                return;

            try
            {
                _frameSnapshotCount = 0;
                _legacyReadCursor = 0;
                AdvanceFrameSnapshotGeneration();
                _droppedLastFlush = Interlocked.Exchange(ref _droppedPendingFlush, 0);
                int parallelWriterDrops = ConsumeParallelWriterDropsAndResetBudget();
                if (parallelWriterDrops > 0)
                {
                    _droppedLastFlush += parallelWriterDrops;
                    Interlocked.Add(ref _loadShedTotal, parallelWriterDrops);
                }

                _coalescedLastFlush = 0;
                _stormDetectedLastFlush = 0;
                int coalescedThisFlush = 0;
                int loadShedThisFlush = 0;
                int corruptedThisFlush = 0;

                int queued = CountPendingSignals();
                _queuedBeforeFlush = queued;
                _pushedLastFlush = queued + _droppedLastFlush;
                _peakQueuedLastFlush = queued > _peakQueuedLastFlush ? queued : _peakQueuedLastFlush;
                bool nonCriticalVfx = SignalLanePolicyCache<T>.NonCriticalVfx;
                int priority = SignalPriorityTable.GetPriority(_laneHash);
                int frameLimit = ResolveFrameLimit(systemStressMilli, nonCriticalVfx, priority);
                if (frameSnapshot.Length < frameLimit)
                    frameLimit = frameSnapshot.Length;

                if (queued > LaneOverflowFaultThreshold)
                {
                    _droppedLastFlush += queued;
                    Interlocked.Add(ref _loadShedTotal, queued);
                    _stormDetectedLastFlush = 1;
                    ClearPendingSignals();
                    global::Hecton8.Core.GlobalTelemetryBus.PublishSystemDegradation(
                        LaneOverflowFaultHash,
                        NonCriticalVfxKillSwitchMask,
                        queued);
                    SignalBusRegistry.SetSignalOverflowKillSwitchBits(NonCriticalVfxKillSwitchMask, LaneOverflowFaultHash);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning("[LANE_OVERFLOW_FAULT]");
#endif
                    return;
                }

                int overflow = Math.Max(0, queued - frameLimit);
                if (overflow > 0)
                {
                    _droppedLastFlush += overflow;
                    Interlocked.Add(ref _loadShedTotal, overflow);
                    if (queued > _maxFrameSignals)
                        _stormDetectedLastFlush = 1;

                    DropOldest(overflow);
                }

                int copyLimit = Math.Min(CountPendingSignals(), frameLimit);
                for (int i = 0; i < copyLimit; i++)
                {
                    if (!TryDequeuePendingSignal(out T signal))
                        break;

                    int guardCode = SignalPayloadFiniteGuards.Sanitize(ref signal);
                    if (guardCode != 0)
                    {
                        corruptedThisFlush++;
                        _droppedLastFlush++;
                        global::Hecton8.Core.GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);
                        continue;
                    }

                    if (TryCoalesceOrAppend(ref signal, frameLimit, frameSnapshot, ref coalescedThisFlush, ref loadShedThisFlush))
                        continue;
                }

                if (coalescedThisFlush > 0)
                {
                    _coalescedLastFlush = coalescedThisFlush;
                    Interlocked.Add(ref _coalescedTotal, coalescedThisFlush);
                }

                if (loadShedThisFlush > 0)
                    Interlocked.Add(ref _loadShedTotal, loadShedThisFlush);

                if (corruptedThisFlush > 0)
                    Interlocked.Add(ref _corruptedSignalTotal, corruptedThisFlush);

                if (_frameSnapshotCount > 1 && SignalLanePolicyCache<T>.DeterministicMutationOrder)
                    SortSnapshotDeterministically(frameSnapshot);
            }
            finally
            {
                ReleaseFrameSnapshotOwnerWrite();
            }
        }

        private static int ResolveFrameLimit(int systemStressMilli, bool nonCriticalVfx, int priority)
        {
            float qualityWeight = SignalBusRegistry.GlobalQualityWeight01;
            float stressWeight = math.saturate(systemStressMilli * 0.001f);
            float effectiveQuality = math.saturate(qualityWeight * math.lerp(1f, 0.35f, stressWeight));
            float curvedQuality = effectiveQuality * effectiveQuality * (3f - (2f * effectiveQuality));
            int minSignals = _survivalFrameSignals;
            int maxSignals = _maxFrameSignals;
            if (SignalTuningTable.TryGetProfile(_laneHash, out SignalTuningProfile tuning))
            {
                minSignals = math.clamp(tuning.MinFrameSignals, 1, _maxFrameSignals);
                maxSignals = math.clamp(tuning.MaxFrameSignals, minSignals, _maxFrameSignals);
            }

            int continuousLimit = (int)math.round(math.lerp(minSignals, maxSignals, curvedQuality));

            if (priority >= 100)
                return math.clamp(Math.Max(continuousLimit, minSignals << 1), 1, _maxFrameSignals);

            if (nonCriticalVfx)
            {
                int vfxLimit = (int)math.round(math.lerp(1f, continuousLimit, curvedQuality));
                return math.clamp(vfxLimit, 1, _maxFrameSignals);
            }

            return math.clamp(continuousLimit, 1, _maxFrameSignals);
        }

        private static void AdvanceFrameSnapshotGeneration()
        {
            _frameSnapshotGeneration = _frameSnapshotGeneration == int.MaxValue ? 1 : _frameSnapshotGeneration + 1;
        }

        private static bool TryCoalesceOrAppend(
            ref T signal,
            int frameLimit,
            NativeArray<T> frameSnapshot,
            ref int coalescedThisFlush,
            ref int loadShedThisFlush)
        {
            if (SignalLanePolicyCache<T>.CoalescesByAupGrid &&
                TryCoalesceAcousticPing(ref signal, frameSnapshot, ref coalescedThisFlush))
                return true;

            if (SignalLanePolicyCache<T>.CoalescesByImpactGrid &&
                TryCoalesceImpact(ref signal, frameSnapshot, ref coalescedThisFlush))
                return true;

            if (SignalLanePolicyCache<T>.CoalescesByHighSpeedImpactGrid &&
                TryCoalesceHighSpeedImpact(ref signal, frameSnapshot, ref coalescedThisFlush))
                return true;

            if (SignalLanePolicyCache<T>.CoalescesByTargetHash &&
                TryCoalesceCombatDamage(ref signal, frameSnapshot, ref coalescedThisFlush))
                return true;

            if (_frameSnapshotCount >= frameLimit)
            {
                _droppedLastFlush++;
                loadShedThisFlush++;
                return true;
            }

            frameSnapshot[_frameSnapshotCount++] = signal;
            return true;
        }

        private static bool TryCoalesceAcousticPing(ref T signal, NativeArray<T> frameSnapshot, ref int coalescedThisFlush)
        {
            ref AcousticPingSignal incoming = ref UnsafeUtility.As<T, AcousticPingSignal>(ref signal);
            for (int i = 0; i < _frameSnapshotCount; i++)
            {
                T existingGeneric = frameSnapshot[i];
                ref AcousticPingSignal existing = ref UnsafeUtility.As<T, AcousticPingSignal>(ref existingGeneric);
                if (existing.Channel != incoming.Channel ||
                    !IsSameAupMeterCell(in existing.PositionAup, in incoming.PositionAup))
                {
                    continue;
                }

                existing.RadiusMeters = math.max(existing.RadiusMeters, incoming.RadiusMeters);
                existing.Intensity01 = math.saturate(math.max(existing.Intensity01, incoming.Intensity01));
                existing.Flags = (byte)(existing.Flags | incoming.Flags);
                if (existing.SourceId == 0u)
                    existing.SourceId = incoming.SourceId;

                frameSnapshot[i] = existingGeneric;
                coalescedThisFlush++;
                return true;
            }

            return false;
        }

        private static bool TryCoalesceImpact(ref T signal, NativeArray<T> frameSnapshot, ref int coalescedThisFlush)
        {
            ref ImpactSignal incoming = ref UnsafeUtility.As<T, ImpactSignal>(ref signal);
            for (int i = 0; i < _frameSnapshotCount; i++)
            {
                T existingGeneric = frameSnapshot[i];
                ref ImpactSignal existing = ref UnsafeUtility.As<T, ImpactSignal>(ref existingGeneric);
                if (existing.PrimaryBodyId != incoming.PrimaryBodyId ||
                    existing.MaterialHash != incoming.MaterialHash ||
                    !IsSameAupMeterCell(in existing.PointAup, in incoming.PointAup))
                {
                    continue;
                }

                float existingForce = System.Math.Max(0f, existing.Force);
                float incomingForce = System.Math.Max(0f, incoming.Force);
                existing.Force = System.Math.Max(existingForce, incomingForce);
                existing.Intensity = math.saturate(System.Math.Max(existing.Intensity, incoming.Intensity));
                existing.WeightClass = existing.WeightClass >= incoming.WeightClass
                    ? existing.WeightClass
                    : incoming.WeightClass;
                existing.Flags = (byte)(existing.Flags | incoming.Flags);
                if (existing.PrimaryMaterialId == 0)
                    existing.PrimaryMaterialId = incoming.PrimaryMaterialId;
                if (existing.SecondaryMaterialId == 0)
                    existing.SecondaryMaterialId = incoming.SecondaryMaterialId;

                frameSnapshot[i] = existingGeneric;
                coalescedThisFlush++;
                return true;
            }

            return false;
        }

        private static bool TryCoalesceHighSpeedImpact(ref T signal, NativeArray<T> frameSnapshot, ref int coalescedThisFlush)
        {
            ref HighSpeedImpactSignal incoming = ref UnsafeUtility.As<T, HighSpeedImpactSignal>(ref signal);
            for (int i = 0; i < _frameSnapshotCount; i++)
            {
                T existingGeneric = frameSnapshot[i];
                ref HighSpeedImpactSignal existing = ref UnsafeUtility.As<T, HighSpeedImpactSignal>(ref existingGeneric);
                if (existing.SourceHash != incoming.SourceHash ||
                    existing.TargetHash != incoming.TargetHash ||
                    existing.MaterialHash != incoming.MaterialHash ||
                    !IsSameAupMeterCell(in existing.PointAup, in incoming.PointAup))
                {
                    continue;
                }

                float existingEnergy = math.max(0f, existing.KineticEnergy);
                float incomingEnergy = math.max(0f, incoming.KineticEnergy);
                if (incomingEnergy > existingEnergy)
                {
                    existing.PointAup = incoming.PointAup;
                    existing.Normal = incoming.Normal;
                    existing.ImpactSpeed = incoming.ImpactSpeed;
                    existing.Frame = incoming.Frame;
                    existing.SourceKind = incoming.SourceKind;
                    existing.PrimaryMaterialId = incoming.PrimaryMaterialId;
                    existing.SecondaryMaterialId = incoming.SecondaryMaterialId;
                }

                existing.KineticEnergy = math.max(existingEnergy, incomingEnergy);
                existing.EffectiveMass = math.max(existing.EffectiveMass, incoming.EffectiveMass);
                existing.Flags = (byte)(existing.Flags | incoming.Flags);

                frameSnapshot[i] = existingGeneric;
                coalescedThisFlush++;
                return true;
            }

            return false;
        }

        private static bool TryCoalesceCombatDamage(ref T signal, NativeArray<T> frameSnapshot, ref int coalescedThisFlush)
        {
            ref CombatDamageSignal incoming = ref UnsafeUtility.As<T, CombatDamageSignal>(ref signal);
            if (incoming.TargetHash == 0u)
                return false;
            if ((incoming.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                return false;

            for (int i = 0; i < _frameSnapshotCount; i++)
            {
                T existingGeneric = frameSnapshot[i];
                ref CombatDamageSignal existing = ref UnsafeUtility.As<T, CombatDamageSignal>(ref existingGeneric);
                if (existing.TargetHash != incoming.TargetHash ||
                    existing.DamageType != incoming.DamageType ||
                    existing.Channel != incoming.Channel ||
                    ((existing.Flags ^ incoming.Flags) & CombatDamageSignal.VisualOnlyFlag) != 0)
                {
                    continue;
                }

                existing.Magnitude = math.max(0f, existing.Magnitude) + math.max(0f, incoming.Magnitude);
                existing.IntegrityDelta = (byte)math.min(byte.MaxValue, existing.IntegrityDelta + incoming.IntegrityDelta);
                existing.Flags = (byte)(existing.Flags | incoming.Flags);
                if (existing.SourceHash == 0u)
                    existing.SourceHash = incoming.SourceHash;
                if (existing.SourceId == 0)
                    existing.SourceId = incoming.SourceId;

                frameSnapshot[i] = existingGeneric;
                coalescedThisFlush++;
                return true;
            }

            return false;
        }

        private static bool IsSameAupMeterCell(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            float radiusMeters = SignalTuningTable.TryGetProfile(_laneHash, out SignalTuningProfile profile)
                ? math.max(0.0001f, profile.CoalescingRadiusMeters)
                : 1f;
            return a.GridX == b.GridX &&
                   a.GridY == b.GridY &&
                   a.GridZ == b.GridZ &&
                   (int)math.floor(a.LocalX / radiusMeters) == (int)math.floor(b.LocalX / radiusMeters) &&
                   (int)math.floor(a.LocalY / radiusMeters) == (int)math.floor(b.LocalY / radiusMeters) &&
                   (int)math.floor(a.LocalZ / radiusMeters) == (int)math.floor(b.LocalZ / radiusMeters);
        }

        private static void SortSnapshotDeterministically(NativeArray<T> frameSnapshot)
        {
            for (int i = 1; i < _frameSnapshotCount; i++)
            {
                T current = frameSnapshot[i];
                ulong currentKey = ResolveDeterministicSortKey(in current);
                int j = i - 1;
                while (j >= 0)
                {
                    T previous = frameSnapshot[j];
                    if (ResolveDeterministicSortKey(in previous) <= currentKey)
                        break;

                    frameSnapshot[j + 1] = frameSnapshot[j];
                    j--;
                }

                frameSnapshot[j + 1] = current;
            }
        }

        private static ulong ResolveDeterministicSortKey(in T signal)
        {
            T copy = signal;
            if (typeof(T) == typeof(CombatDamageSignal))
            {
                ref CombatDamageSignal combat = ref UnsafeUtility.As<T, CombatDamageSignal>(ref copy);
                return ((ulong)combat.TargetHash << 32) | combat.SourceHash;
            }

            if (typeof(T) == typeof(PlayerStateSignal))
            {
                ref PlayerStateSignal player = ref UnsafeUtility.As<T, PlayerStateSignal>(ref copy);
                return ((ulong)player.SourceHash << 32) | player.Frame;
            }

            if (typeof(T) == typeof(ThermalSourceSignal))
            {
                ref ThermalSourceSignal thermal = ref UnsafeUtility.As<T, ThermalSourceSignal>(ref copy);
                uint sourceId = thermal.SourceId != 0u ? thermal.SourceId : FoldThermalSourceSortId(in thermal);
                return ((ulong)sourceId << 32) | thermal.Frame;
            }

            return ResolveGenericSortKey(in signal);
        }

        private static uint FoldThermalSourceSortId(in ThermalSourceSignal signal)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            hash = FoldSortHash(hash, (uint)signal.PositionAup.GridX, fnvPrime);
            hash = FoldSortHash(hash, (uint)(signal.PositionAup.GridX >> 32), fnvPrime);
            hash = FoldSortHash(hash, (uint)signal.PositionAup.GridY, fnvPrime);
            hash = FoldSortHash(hash, (uint)(signal.PositionAup.GridY >> 32), fnvPrime);
            hash = FoldSortHash(hash, (uint)signal.PositionAup.GridZ, fnvPrime);
            hash = FoldSortHash(hash, (uint)(signal.PositionAup.GridZ >> 32), fnvPrime);
            hash = FoldSortHash(hash, math.asuint(signal.PositionAup.LocalX), fnvPrime);
            hash = FoldSortHash(hash, math.asuint(signal.PositionAup.LocalY), fnvPrime);
            hash = FoldSortHash(hash, math.asuint(signal.PositionAup.LocalZ), fnvPrime);
            hash = FoldSortHash(hash, math.asuint(signal.RadiusMeters), fnvPrime);
            return hash == 0u ? 1u : hash;
        }

        private static uint FoldSortHash(uint hash, uint value, uint prime)
        {
            hash ^= value;
            hash *= prime;
            return hash;
        }

        private static unsafe ulong ResolveGenericSortKey(in T signal)
        {
            T copy = signal;
            byte* bytes = (byte*)UnsafeUtility.AddressOf(ref copy);
            int length = UnsafeUtility.SizeOf<T>();
            ulong hash = 14695981039346656037ul;
            for (int i = 0; i < length; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211ul;
            }

            return hash;
        }

        private static void DropOldest(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryDequeuePendingSignal(out _))
                    break;
            }
        }

        private static void ClearPendingSignals()
        {
            if (_ring.IsCreated)
                _ring.Clear();
        }

        internal static void Dispose()
        {
            if (_ring.IsCreated)
            {
                _ring.Dispose();
                _ring = default;
            }

            if (_parallelWriterBudget.IsCreated)
            {
                H8Memory.Release(
                    ref _parallelWriterBudget,
                    Hecton8.Core.Memory.SystemID.CoreDataVault);
            }

            ReleaseFrameSnapshotBuffer();

            _legacyReadCursor = 0;
            _queuedBeforeFlush = 0;
            _pushedLastFlush = 0;
            _droppedLastFlush = 0;
            _droppedPendingFlush = 0;
            _coalescedLastFlush = 0;
            _coalescedTotal = 0;
            _stormDetectedLastFlush = 0;
            _loadShedTotal = 0;
            _corruptedSignalTotal = 0;
            _acceptedSignalTotal = 0;
            _latestSignal = default;
            _latestSignalSequence = 0;
            _peakQueuedLastFlush = 0;
            _initialized = false;
            _registered = false;
            _configured = false;
            _layoutFaultLogged = false;
            _configurationFaultLogged = false;
            _expectedCapacity = DefaultExpectedCapacity;
            _maxFrameSignals = DefaultMaxFrameSignals;
            _survivalFrameSignals = DefaultSurvivalFrameSignals;
            _laneHash = 0u;
            _layoutPolicyFlags = 0;
        }

        private static void ApplyKnownContractDefaultsIfUnconfigured()
        {
            if (_configured ||
                !SignalLanePolicyCache<T>.TryResolveDefaultContract(
                    out int expectedCapacity,
                    out int maxFrameSignals,
                    out int lowTierFrameSignals,
                    out uint laneHash))
            {
                return;
            }

            _expectedCapacity = Math.Max(1, expectedCapacity);
            _maxFrameSignals = Math.Max(1, maxFrameSignals);
            _survivalFrameSignals = Math.Max(1, Math.Min(lowTierFrameSignals, _maxFrameSignals));
            _laneHash = laneHash != 0u ? laneHash : _defaultLaneHash;
            _configured = true;
        }

        private static unsafe int ConsumeParallelWriterDropsAndResetBudget()
        {
            if (!_parallelWriterBudget.IsCreated)
                return 0;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_parallelWriterBudget);
            int dropped = Interlocked.Exchange(ref budget[ParallelWriterBudgetDroppedIndex], 0);
            Volatile.Write(ref budget[ParallelWriterBudgetRemainingIndex], ResolveParallelWriterBudget());
            return dropped < 0 ? int.MaxValue : dropped;
        }

        private static unsafe void ResetParallelWriterBudget()
        {
            if (!_parallelWriterBudget.IsCreated)
                return;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_parallelWriterBudget);
            Volatile.Write(ref budget[ParallelWriterBudgetRemainingIndex], ResolveParallelWriterBudget());
            Volatile.Write(ref budget[ParallelWriterBudgetDroppedIndex], 0);
        }

        private static int ResolveParallelWriterBudget()
        {
            return Math.Max(1, Math.Min(_expectedCapacity, LaneOverflowFaultThreshold));
        }

        private static void AdvanceLatestSignalSequence()
        {
            int next = unchecked(Volatile.Read(ref _latestSignalSequence) + 1);
            if (next == 0)
                next = 1;

            Volatile.Write(ref _latestSignalSequence, next);
        }

        private static unsafe void EnsureRegistered()
        {
            if (_registered)
                return;

            if (_laneHash == 0u)
                _laneHash = _defaultLaneHash;

            _registered = SignalBusRegistry.Register(
                &Dispose,
                &FlushPostSimulation,
                &CopyTelemetryStatic,
                SignalLanePolicyCache<T>.FlushDuringSimulationPause);
        }

        private static void CopyTelemetryStatic(ref SignalLaneTelemetry telemetry)
        {
            int pushedLastFlush = _pushedLastFlush < 0 ? 0 : _pushedLastFlush;
            int corruptedTotal = Volatile.Read(ref _corruptedSignalTotal);
            if (corruptedTotal < 0)
                corruptedTotal = int.MaxValue;

            telemetry.LaneHash = LaneHash;
            telemetry.QueuedBeforeFlush = _queuedBeforeFlush;
            telemetry.SnapshotCount = SnapshotCount;
            telemetry.DroppedCount = _droppedLastFlush;
            telemetry.CoalescedCount = _coalescedLastFlush;
            byte flags = (byte)(
                (_stormDetectedLastFlush != 0 ? 1 : 0) |
                (SignalLanePolicyCache<T>.NonCriticalVfx ? 2 : 0) |
                (SignalLanePolicyCache<T>.FatalInterrupt ? 4 : 0) |
                (_coalescedLastFlush > 0 ? 8 : 0) |
                (corruptedTotal > 0 ? 16 : 0));
            if (HasCacheLineCriticalStrideDebt())
                flags |= TelemetryFlagCacheLineStrideDebt;

            telemetry.Flags = flags;
            telemetry.Reserved0 = (byte)math.min(UnsafeUtility.SizeOf<T>(), byte.MaxValue);
            telemetry.Reserved1 = PackTelemetryReserved1();
            telemetry.Reserved2 = ((ulong)(uint)corruptedTotal << 32) | (uint)pushedLastFlush;
        }

        private static ushort PackTelemetryReserved1()
        {
            return (ushort)(_layoutPolicyFlags & TelemetryLayoutPolicyMask);
        }

        private static int CountPendingSignals()
        {
            return _ring.IsCreated ? _ring.Count : 0;
        }

        private static bool TryDequeuePendingSignal(out T signal)
        {
            if (_ring.IsCreated && _ring.TryDequeue(out signal))
                return true;

            signal = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasValidPayloadStride()
        {
            int size = UnsafeUtility.SizeOf<T>();
            return size > 0 && size <= 192 && (size & 7) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasCacheLineCriticalStrideDebt()
        {
            if ((_layoutPolicyFlags & LayoutPolicyCacheLineCritical) == 0)
                return false;

            int size = UnsafeUtility.SizeOf<T>();
            return size != 64 && size != 128;
        }

        private static bool TryAcquireFrameSnapshotBuffer(int capacity)
        {
            if (!TryFindFrameSnapshotVaultForBootstrap(out IDataVault vault))
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _frameSnapshotBufferId = ResolveSnapshotBufferId();
            _frameSnapshotHandle = vault.EnsureGenerationHandle<T>(
                _frameSnapshotBufferId,
                Math.Max(1, capacity),
                SystemID.CoreDataVault,
                NativeArrayOptions.UninitializedMemory);
            if (!vault.TryReadOnlyHandle(in _frameSnapshotHandle, out NativeArray<T>.ReadOnly frameSnapshot) ||
                !frameSnapshot.IsCreated ||
                frameSnapshot.Length < capacity)
            {
                if (_frameSnapshotHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _frameSnapshotHandle);

                _frameSnapshotHandle = default;
                _frameSnapshotVault = null;
                _frameSnapshotBufferId = BufferID.Unknown;
                _frameSnapshotCount = 0;
                return false;
            }

            _frameSnapshotVault = vault;
            _frameSnapshotCount = 0;
            _frameSnapshotGeneration = 0;
            return true;
        }

        private static void ReleaseFrameSnapshotBuffer()
        {
            ReleaseFrameSnapshotOwnerWrite();

            // DataVault owns snapshot storage lifetime; lane shutdown only drops handles because
            // application quit/domain reload can invalidate the arena before SignalBus disposal runs.
            _frameSnapshotHandle = default;
            _frameSnapshotVault = null;
            _frameSnapshotActiveWriteHandle = default;
            _frameSnapshotActiveWriteVault = null;
            _frameSnapshotBufferId = BufferID.Unknown;
            _frameSnapshotCount = 0;
            _frameSnapshotGeneration = 0;
        }

        private static bool TryReadFrameSnapshot(out NativeArray<T>.ReadOnly frameSnapshot, out int snapshotCount)
        {
            frameSnapshot = default;
            snapshotCount = 0;
            if (_frameSnapshotVault == null || _frameSnapshotHandle.BufferID == 0u)
                return false;

            if (!_frameSnapshotVault.TryReadOnlyHandle(in _frameSnapshotHandle, out frameSnapshot))
            {
                return false;
            }

            int count = _frameSnapshotCount;
            if (count <= 0)
                return true;

            snapshotCount = math.min(count, frameSnapshot.Length);
            return true;
        }

        private static bool TryAcquireFrameSnapshotForOwnerWrite(out NativeArray<T> frameSnapshot)
        {
            frameSnapshot = default;
            IDataVault snapshotVault = _frameSnapshotVault;
            if (snapshotVault == null || _frameSnapshotHandle.BufferID == 0u || _frameSnapshotActiveWriteVault != null)
                return false;

            if (TryAcquireFrameSnapshotWriteLock(snapshotVault, in _frameSnapshotHandle, out frameSnapshot))
                return true;

            if (_frameSnapshotBufferId != BufferID.Unknown &&
                snapshotVault.TryGetGenerationHandle<T>(_frameSnapshotBufferId, out VaultGenerationHandle<T> refreshedHandle))
            {
                _frameSnapshotHandle = refreshedHandle;
                if (TryAcquireFrameSnapshotWriteLock(snapshotVault, in _frameSnapshotHandle, out frameSnapshot))
                    return true;
            }

            _frameSnapshotCount = 0;
            return false;
        }

        private static bool TryAcquireFrameSnapshotWriteLock(
            IDataVault snapshotVault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> frameSnapshot)
        {
            frameSnapshot = default;
            bool lockAcquired = false;
            bool ownershipTransferred = false;
            try
            {
                if (snapshotVault == null)
                    return false;

                lockAcquired = snapshotVault.TryAcquireWriteLock(in handle, SystemID.CoreDataVault, out frameSnapshot);
                if (!lockAcquired)
                    return false;

                if (frameSnapshot.IsCreated)
                {
                    if (_frameSnapshotCount > frameSnapshot.Length)
                        _frameSnapshotCount = frameSnapshot.Length;

                    ownershipTransferred = true;
                    _frameSnapshotActiveWriteHandle = handle;
                    _frameSnapshotActiveWriteVault = snapshotVault;
                    return true;
                }

                frameSnapshot = default;
                return false;
            }
            finally
            {
                if (lockAcquired && !ownershipTransferred)
                    snapshotVault.ReleaseWriteLock(in handle, SystemID.CoreDataVault);
            }
        }

        private static void ReleaseFrameSnapshotOwnerWrite()
        {
            IDataVault snapshotVault = _frameSnapshotActiveWriteVault;
            if (snapshotVault == null)
                return;

            VaultGenerationHandle<T> snapshotHandle = _frameSnapshotActiveWriteHandle;
            _frameSnapshotActiveWriteVault = null;
            _frameSnapshotActiveWriteHandle = default;
            if (snapshotHandle.BufferID != 0u)
                snapshotVault.ReleaseWriteLock(in snapshotHandle, SystemID.CoreDataVault);
        }

        private static bool TryFindFrameSnapshotVaultForBootstrap(out IDataVault vault)
        {
            return SignalBusRegistry.TryGetBoundDataVault(out vault);
        }

        private static BufferID ResolveSnapshotBufferId()
        {
            uint hash = _laneHash != 0u ? _laneHash : ComputeTypeHash();
            uint id = SnapshotBufferIdPrefix | (hash & SnapshotBufferIdMask);
            return id != 0u ? (BufferID)unchecked((int)id) : BufferID.Unknown;
        }

        private static uint ComputeTypeHash()
        {
            uint hash = unchecked((uint)BurstRuntime.GetHashCode32<T>());
            return hash == 0u ? 1u : hash;
        }

    }

    internal static class SignalLanePolicyCache<T>
        where T : unmanaged, ISignal
    {
        public static readonly bool NonCriticalVfx = ResolveNonCriticalVfx();
        public static readonly bool FlushDuringSimulationPause = ResolveFlushDuringSimulationPause();
        public static readonly bool FatalInterrupt = ResolveFatalInterrupt();
        public static readonly bool CoalescesByAupGrid = ResolveCoalescesByAupGrid();
        public static readonly bool CoalescesByImpactGrid = ResolveCoalescesByImpactGrid();
        public static readonly bool CoalescesByHighSpeedImpactGrid = ResolveCoalescesByHighSpeedImpactGrid();
        public static readonly bool CoalescesByTargetHash = ResolveCoalescesByTargetHash();
        public static readonly bool DeterministicMutationOrder = ResolveDeterministicMutationOrder();

        private static bool ResolveNonCriticalVfx()
        {
            Type type = typeof(T);
            return type == typeof(DebrisSpawnSignal) ||
                   type == typeof(HullDeformedSignal) ||
                   type == typeof(BulletTimeVisualSignal) ||
                   type == typeof(CameraJuiceImpactSignal) ||
                   type == typeof(BubbleSpawnSignal) ||
                   type == typeof(ReentryVfxStateSignal) ||
                   type == typeof(VisorDropletSignal) ||
                   type == typeof(PlayerFootstepSignal) ||
                   type == typeof(PlayerWaterSplashSignal) ||
                   type == typeof(PlayerExhaleSignal) ||
                   type == typeof(PlayerSprintStateSignal) ||
                   type == typeof(PlayerFatalPressureSignal) ||
                   type == typeof(PlayerTransportBailoutSignal) ||
                   type == typeof(VisualFlareSignal) ||
                   type == typeof(ToxicBioluminescenceSignal) ||
                   type == typeof(DebugSignal) ||
                   type == typeof(StreamingTurbulenceSignal);
        }

        private static bool ResolveFlushDuringSimulationPause()
        {
            Type type = typeof(T);
            return type == typeof(SystemPauseSignal) ||
                   type == typeof(SimulationPauseSignal) ||
                   type == typeof(TimeDilationSignal) ||
                   type == typeof(BulletTimeVisualSignal) ||
                   type == typeof(CameraJuiceImpactSignal) ||
                   type == typeof(InputStateSignal) ||
                   type == typeof(PlayerInputSignal) ||
                   type == typeof(DiegeticHudSignal) ||
                   type == typeof(HUDNotificationSignal) ||
                   type == typeof(NarrativeHudWaypointSignal) ||
                   type == typeof(SubtitleSignal) ||
                   type == typeof(VocalWarningSignal) ||
                   type == typeof(VocalCueSignal) ||
                   type == typeof(DataReloadSignal) ||
                   type == typeof(MemoryPressureSignal) ||
                   type == typeof(CrashTelemetrySignal) ||
                   type == typeof(ComplianceViolationSignal) ||
                   type == typeof(SaveLifecycleSignal) ||
                   type == typeof(GlobalTimeSyncSignal) ||
                   type == typeof(FrameTimeSignal) ||
                   type == typeof(SystemHealthSignal) ||
                   type == typeof(KillSwitchSignal) ||
                   type == typeof(LockstepSnapshotSignal) ||
                   type == typeof(SystemGlitchSignal) ||
                   type == typeof(AupPreShiftSignal) ||
                   type == typeof(AupShiftSignal) ||
                   type == typeof(ResolutionChangedSignal) ||
                   type == typeof(ScalabilityChangedEvent) ||
                   type == typeof(SystemHealthIndexSignal) ||
                   type == typeof(CpuStarvationSignal);
        }

        private static bool ResolveFatalInterrupt()
        {
            Type type = typeof(T);
            // KillSwitchSignal is intentionally NOT fatal.
            // HomeostasisBrain publishes it on every pressure-mask / LOD transition
            // (routine load shedding). Treating it as FatalInterrupt permanently set
            // SignalBusRegistry.IsSimulationHalted on the first pressure blip, which
            // blocked SystemDispatcher FastTick and froze ecology dayAcc at 0 in
            // headless smoke (simHalted=1 from post-ready t=0 / stepIdx≈4).
            // True hard-stops remain: player-lethal pressure and lockstep system glitch.
            return type == typeof(PlayerFatalPressureSignal) ||
                   type == typeof(SystemGlitchSignal);
        }

        private static bool ResolveCoalescesByAupGrid()
        {
            Type type = typeof(T);
            return type == typeof(AcousticPingSignal);
        }

        private static bool ResolveCoalescesByImpactGrid()
        {
            Type type = typeof(T);
            return type == typeof(ImpactSignal);
        }

        private static bool ResolveCoalescesByHighSpeedImpactGrid()
        {
            Type type = typeof(T);
            return type == typeof(HighSpeedImpactSignal);
        }

        private static bool ResolveCoalescesByTargetHash()
        {
            Type type = typeof(T);
            return type == typeof(CombatDamageSignal);
        }

        private static bool ResolveDeterministicMutationOrder()
        {
            Type type = typeof(T);
            return type == typeof(CombatDamageSignal) ||
                   type == typeof(PlayerStateSignal) ||
                   type == typeof(ThermalSourceSignal) ||
                   type == typeof(StateCorrectionSignal) ||
                   type == typeof(SyncFenceSignal);
        }

        internal static bool TryResolveDefaultContract(
            out int expectedCapacity,
            out int maxFrameSignals,
            out int lowTierFrameSignals,
            out uint laneHash)
        {
            Type type = typeof(T);
            if (type == typeof(AcousticPingSignal))
            {
                expectedCapacity = AcousticPingSignal.ExpectedCapacity;
                maxFrameSignals = AcousticPingSignal.MaxFrameSignals;
                lowTierFrameSignals = AcousticPingSignal.LowTierFrameSignals;
                laneHash = AcousticPingSignal.LaneHash;
                return true;
            }

            if (type == typeof(CombatDamageSignal))
            {
                expectedCapacity = CombatDamageSignal.ExpectedCapacity;
                maxFrameSignals = CombatDamageSignal.MaxFrameSignals;
                lowTierFrameSignals = CombatDamageSignal.LowTierFrameSignals;
                laneHash = CombatDamageSignal.LaneHash;
                return true;
            }

            if (type == typeof(ImpactSignal))
            {
                expectedCapacity = ImpactSignal.ExpectedCapacity;
                maxFrameSignals = ImpactSignal.MaxFrameSignals;
                lowTierFrameSignals = ImpactSignal.LowTierFrameSignals;
                laneHash = ImpactSignal.LaneHash;
                return true;
            }

            if (type == typeof(HighSpeedImpactSignal))
            {
                expectedCapacity = HighSpeedImpactSignal.ExpectedCapacity;
                maxFrameSignals = HighSpeedImpactSignal.MaxFrameSignals;
                lowTierFrameSignals = HighSpeedImpactSignal.LowTierFrameSignals;
                laneHash = HighSpeedImpactSignal.LaneHash;
                return true;
            }

            if (type == typeof(ToolAcousticSignal))
            {
                expectedCapacity = ToolAcousticSignal.ExpectedCapacity;
                maxFrameSignals = ToolAcousticSignal.MaxFrameSignals;
                lowTierFrameSignals = ToolAcousticSignal.LowTierFrameSignals;
                laneHash = ToolAcousticSignal.LaneHash;
                return true;
            }

            if (type == typeof(AppliedLoreTerminalPreviewSignal))
            {
                expectedCapacity = AppliedLoreTerminalPreviewSignal.ExpectedCapacity;
                maxFrameSignals = AppliedLoreTerminalPreviewSignal.MaxFrameSignals;
                lowTierFrameSignals = AppliedLoreTerminalPreviewSignal.LowTierFrameSignals;
                laneHash = AppliedLoreTerminalPreviewSignal.LaneHash;
                return true;
            }

            if (type == typeof(BubbleSpawnSignal))
            {
                expectedCapacity = BubbleSpawnSignal.ExpectedCapacity;
                maxFrameSignals = BubbleSpawnSignal.MaxFrameSignals;
                lowTierFrameSignals = BubbleSpawnSignal.LowTierFrameSignals;
                laneHash = BubbleSpawnSignal.LaneHash;
                return true;
            }

            if (type == typeof(MovementAcousticSignal))
            {
                expectedCapacity = MovementAcousticSignal.ExpectedCapacity;
                maxFrameSignals = MovementAcousticSignal.MaxFrameSignals;
                lowTierFrameSignals = MovementAcousticSignal.LowTierFrameSignals;
                laneHash = MovementAcousticSignal.LaneHash;
                return true;
            }

            if (type == typeof(SubmarineLightsChangedSignal))
            {
                expectedCapacity = SubmarineLightsChangedSignal.ExpectedCapacity;
                maxFrameSignals = SubmarineLightsChangedSignal.MaxFrameSignals;
                lowTierFrameSignals = SubmarineLightsChangedSignal.LowTierFrameSignals;
                laneHash = SubmarineLightsChangedSignal.LaneHash;
                return true;
            }

            if (type == typeof(AnomalyProximitySignal))
            {
                expectedCapacity = AnomalyProximitySignal.ExpectedCapacity;
                maxFrameSignals = AnomalyProximitySignal.MaxFrameSignals;
                lowTierFrameSignals = AnomalyProximitySignal.LowTierFrameSignals;
                laneHash = AnomalyProximitySignal.LaneHash;
                return true;
            }

            if (type == typeof(BaseModuleCompromisedSignal))
            {
                expectedCapacity = BaseModuleCompromisedSignal.ExpectedCapacity;
                maxFrameSignals = BaseModuleCompromisedSignal.MaxFrameSignals;
                lowTierFrameSignals = BaseModuleCompromisedSignal.LowTierFrameSignals;
                laneHash = BaseModuleCompromisedSignal.LaneHash;
                return true;
            }

            if (type == typeof(HullDeformedSignal))
            {
                expectedCapacity = HullDeformedSignal.ExpectedCapacity;
                maxFrameSignals = HullDeformedSignal.MaxFrameSignals;
                lowTierFrameSignals = HullDeformedSignal.LowTierFrameSignals;
                laneHash = HullDeformedSignal.LaneHash;
                return true;
            }

            if (type == typeof(HullRepairedSignal))
            {
                expectedCapacity = HullRepairedSignal.ExpectedCapacity;
                maxFrameSignals = HullRepairedSignal.MaxFrameSignals;
                lowTierFrameSignals = HullRepairedSignal.LowTierFrameSignals;
                laneHash = HullRepairedSignal.LaneHash;
                return true;
            }

            if (type == typeof(PhysiologyStateSignal))
            {
                expectedCapacity = PhysiologyStateSignal.ExpectedCapacity;
                maxFrameSignals = PhysiologyStateSignal.MaxFrameSignals;
                lowTierFrameSignals = PhysiologyStateSignal.LowTierFrameSignals;
                laneHash = PhysiologyStateSignal.LaneHash;
                return true;
            }

            if (type == typeof(ToxicityExposureSignal))
            {
                expectedCapacity = ToxicityExposureSignal.ExpectedCapacity;
                maxFrameSignals = ToxicityExposureSignal.MaxFrameSignals;
                lowTierFrameSignals = ToxicityExposureSignal.LowTierFrameSignals;
                laneHash = ToxicityExposureSignal.LaneHash;
                return true;
            }

            if (type == typeof(ToxicBioluminescenceSignal))
            {
                expectedCapacity = ToxicBioluminescenceSignal.ExpectedCapacity;
                maxFrameSignals = ToxicBioluminescenceSignal.MaxFrameSignals;
                lowTierFrameSignals = ToxicBioluminescenceSignal.LowTierFrameSignals;
                laneHash = ToxicBioluminescenceSignal.LaneHash;
                return true;
            }

            if (type == typeof(ReactorDamageSignal))
            {
                expectedCapacity = ReactorDamageSignal.ExpectedCapacity;
                maxFrameSignals = ReactorDamageSignal.MaxFrameSignals;
                lowTierFrameSignals = ReactorDamageSignal.LowTierFrameSignals;
                laneHash = ReactorDamageSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerRespawnSignal))
            {
                expectedCapacity = PlayerRespawnSignal.ExpectedCapacity;
                maxFrameSignals = PlayerRespawnSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerRespawnSignal.LowTierFrameSignals;
                laneHash = PlayerRespawnSignal.LaneHash;
                return true;
            }

            if (type == typeof(InventoryRespawnDeathAupSignal))
            {
                expectedCapacity = InventoryRespawnDeathAupSignal.ExpectedCapacity;
                maxFrameSignals = InventoryRespawnDeathAupSignal.MaxFrameSignals;
                lowTierFrameSignals = InventoryRespawnDeathAupSignal.LowTierFrameSignals;
                laneHash = InventoryRespawnDeathAupSignal.LaneHash;
                return true;
            }

            if (type == typeof(InventoryCommandSignal))
            {
                expectedCapacity = InventoryCommandSignal.ExpectedCapacity;
                maxFrameSignals = InventoryCommandSignal.MaxFrameSignals;
                lowTierFrameSignals = InventoryCommandSignal.LowTierFrameSignals;
                laneHash = InventoryCommandSignal.LaneHash;
                return true;
            }

            if (type == typeof(InventoryRespawnPenaltyResultSignal))
            {
                expectedCapacity = InventoryRespawnPenaltyResultSignal.ExpectedCapacity;
                maxFrameSignals = InventoryRespawnPenaltyResultSignal.MaxFrameSignals;
                lowTierFrameSignals = InventoryRespawnPenaltyResultSignal.LowTierFrameSignals;
                laneHash = InventoryRespawnPenaltyResultSignal.LaneHash;
                return true;
            }

            if (type == typeof(InventoryDeathLootCacheSignal))
            {
                expectedCapacity = InventoryDeathLootCacheSignal.ExpectedCapacity;
                maxFrameSignals = InventoryDeathLootCacheSignal.MaxFrameSignals;
                lowTierFrameSignals = InventoryDeathLootCacheSignal.LowTierFrameSignals;
                laneHash = InventoryDeathLootCacheSignal.LaneHash;
                return true;
            }

            if (type == typeof(ItemAcquiredSignal))
            {
                expectedCapacity = ItemAcquiredSignal.ExpectedCapacity;
                maxFrameSignals = ItemAcquiredSignal.MaxFrameSignals;
                lowTierFrameSignals = ItemAcquiredSignal.LowTierFrameSignals;
                laneHash = ItemAcquiredSignal.LaneHash;
                return true;
            }

            if (type == typeof(DeflectSignal))
            {
                expectedCapacity = DeflectSignal.ExpectedCapacity;
                maxFrameSignals = DeflectSignal.MaxFrameSignals;
                lowTierFrameSignals = DeflectSignal.LowTierFrameSignals;
                laneHash = DeflectSignal.LaneHash;
                return true;
            }

            if (type == typeof(DeconstructResultSignal))
            {
                expectedCapacity = DeconstructResultSignal.ExpectedCapacity;
                maxFrameSignals = DeconstructResultSignal.MaxFrameSignals;
                lowTierFrameSignals = DeconstructResultSignal.LowTierFrameSignals;
                laneHash = DeconstructResultSignal.LaneHash;
                return true;
            }

            if (type == typeof(InteractionUiSignal))
            {
                expectedCapacity = InteractionUiSignal.ExpectedCapacity;
                maxFrameSignals = InteractionUiSignal.MaxFrameSignals;
                lowTierFrameSignals = InteractionUiSignal.LowTierFrameSignals;
                laneHash = InteractionUiSignal.LaneHash;
                return true;
            }

            if (type == typeof(FluidIncursionSignal))
            {
                expectedCapacity = FluidIncursionSignal.ExpectedCapacity;
                maxFrameSignals = FluidIncursionSignal.MaxFrameSignals;
                lowTierFrameSignals = FluidIncursionSignal.LowTierFrameSignals;
                laneHash = FluidIncursionSignal.LaneHash;
                return true;
            }

            if (type == typeof(HabitatFloodAcousticMuffleSignal))
            {
                expectedCapacity = HabitatFloodAcousticMuffleSignal.ExpectedCapacity;
                maxFrameSignals = HabitatFloodAcousticMuffleSignal.MaxFrameSignals;
                lowTierFrameSignals = HabitatFloodAcousticMuffleSignal.LowTierFrameSignals;
                laneHash = HabitatFloodAcousticMuffleSignal.LaneHash;
                return true;
            }

            if (type == typeof(DynamicMusicScalarSignal))
            {
                expectedCapacity = DynamicMusicScalarSignal.ExpectedCapacity;
                maxFrameSignals = DynamicMusicScalarSignal.MaxFrameSignals;
                lowTierFrameSignals = DynamicMusicScalarSignal.LowTierFrameSignals;
                laneHash = DynamicMusicScalarSignal.LaneHash;
                return true;
            }

            if (type == typeof(SystemHealthSignal))
            {
                expectedCapacity = SystemHealthSignal.ExpectedCapacity;
                maxFrameSignals = SystemHealthSignal.MaxFrameSignals;
                lowTierFrameSignals = SystemHealthSignal.LowTierFrameSignals;
                laneHash = SystemHealthSignal.LaneHash;
                return true;
            }

            if (type == typeof(FrameTimeSignal))
            {
                expectedCapacity = FrameTimeSignal.ExpectedCapacity;
                maxFrameSignals = FrameTimeSignal.MaxFrameSignals;
                lowTierFrameSignals = FrameTimeSignal.LowTierFrameSignals;
                laneHash = FrameTimeSignal.LaneHash;
                return true;
            }

            if (type == typeof(KillSwitchSignal))
            {
                expectedCapacity = KillSwitchSignal.ExpectedCapacity;
                maxFrameSignals = KillSwitchSignal.MaxFrameSignals;
                lowTierFrameSignals = KillSwitchSignal.LowTierFrameSignals;
                laneHash = KillSwitchSignal.LaneHash;
                return true;
            }

            if (type == typeof(SystemKillSwitchBitsSignal))
            {
                expectedCapacity = SystemKillSwitchBitsSignal.ExpectedCapacity;
                maxFrameSignals = SystemKillSwitchBitsSignal.MaxFrameSignals;
                lowTierFrameSignals = SystemKillSwitchBitsSignal.LowTierFrameSignals;
                laneHash = SystemKillSwitchBitsSignal.LaneHash;
                return true;
            }

            if (type == typeof(ReentryVfxStateSignal))
            {
                expectedCapacity = ReentryVfxStateSignal.ExpectedCapacity;
                maxFrameSignals = ReentryVfxStateSignal.MaxFrameSignals;
                lowTierFrameSignals = ReentryVfxStateSignal.LowTierFrameSignals;
                laneHash = ReentryVfxStateSignal.LaneHash;
                return true;
            }

            if (type == typeof(ReentryAcousticStressSignal))
            {
                expectedCapacity = ReentryAcousticStressSignal.ExpectedCapacity;
                maxFrameSignals = ReentryAcousticStressSignal.MaxFrameSignals;
                lowTierFrameSignals = ReentryAcousticStressSignal.LowTierFrameSignals;
                laneHash = ReentryAcousticStressSignal.LaneHash;
                return true;
            }

            if (type == typeof(VisorDropletSignal))
            {
                expectedCapacity = VisorDropletSignal.ExpectedCapacity;
                maxFrameSignals = VisorDropletSignal.MaxFrameSignals;
                lowTierFrameSignals = VisorDropletSignal.LowTierFrameSignals;
                laneHash = VisorDropletSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerFootstepSignal))
            {
                expectedCapacity = PlayerFootstepSignal.ExpectedCapacity;
                maxFrameSignals = PlayerFootstepSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerFootstepSignal.LowTierFrameSignals;
                laneHash = PlayerFootstepSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerWaterSplashSignal))
            {
                expectedCapacity = PlayerWaterSplashSignal.ExpectedCapacity;
                maxFrameSignals = PlayerWaterSplashSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerWaterSplashSignal.LowTierFrameSignals;
                laneHash = PlayerWaterSplashSignal.LaneHash;
                return true;
            }

            if (type == typeof(WaterTransitionSignal))
            {
                expectedCapacity = WaterTransitionSignal.ExpectedCapacity;
                maxFrameSignals = WaterTransitionSignal.MaxFrameSignals;
                lowTierFrameSignals = WaterTransitionSignal.LowTierFrameSignals;
                laneHash = WaterTransitionSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerExhaleSignal))
            {
                expectedCapacity = PlayerExhaleSignal.ExpectedCapacity;
                maxFrameSignals = PlayerExhaleSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerExhaleSignal.LowTierFrameSignals;
                laneHash = PlayerExhaleSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerSprintStateSignal))
            {
                expectedCapacity = PlayerSprintStateSignal.ExpectedCapacity;
                maxFrameSignals = PlayerSprintStateSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerSprintStateSignal.LowTierFrameSignals;
                laneHash = PlayerSprintStateSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerFatalPressureSignal))
            {
                expectedCapacity = PlayerFatalPressureSignal.ExpectedCapacity;
                maxFrameSignals = PlayerFatalPressureSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerFatalPressureSignal.LowTierFrameSignals;
                laneHash = PlayerFatalPressureSignal.LaneHash;
                return true;
            }

            if (type == typeof(PlayerTransportBailoutSignal))
            {
                expectedCapacity = PlayerTransportBailoutSignal.ExpectedCapacity;
                maxFrameSignals = PlayerTransportBailoutSignal.MaxFrameSignals;
                lowTierFrameSignals = PlayerTransportBailoutSignal.LowTierFrameSignals;
                laneHash = PlayerTransportBailoutSignal.LaneHash;
                return true;
            }

            if (type == typeof(VisualFlareSignal))
            {
                expectedCapacity = VisualFlareSignal.ExpectedCapacity;
                maxFrameSignals = VisualFlareSignal.MaxFrameSignals;
                lowTierFrameSignals = VisualFlareSignal.LowTierFrameSignals;
                laneHash = VisualFlareSignal.LaneHash;
                return true;
            }

            if (type == typeof(SeismicSignal))
            {
                expectedCapacity = SeismicSignal.ExpectedCapacity;
                maxFrameSignals = SeismicSignal.MaxFrameSignals;
                lowTierFrameSignals = SeismicSignal.LowTierFrameSignals;
                laneHash = SeismicSignal.LaneHash;
                return true;
            }

            if (type == typeof(MockNarrativeTriggerSignal))
            {
                expectedCapacity = MockNarrativeTriggerSignal.ExpectedCapacity;
                maxFrameSignals = MockNarrativeTriggerSignal.MaxFrameSignals;
                lowTierFrameSignals = MockNarrativeTriggerSignal.LowTierFrameSignals;
                laneHash = MockNarrativeTriggerSignal.LaneHash;
                return true;
            }

            if (type == typeof(DebrisAvalancheSignal))
            {
                expectedCapacity = DebrisAvalancheSignal.ExpectedCapacity;
                maxFrameSignals = DebrisAvalancheSignal.MaxFrameSignals;
                lowTierFrameSignals = DebrisAvalancheSignal.LowTierFrameSignals;
                laneHash = DebrisAvalancheSignal.LaneHash;
                return true;
            }

            if (type == typeof(AcousticShockwaveSignal))
            {
                expectedCapacity = AcousticShockwaveSignal.ExpectedCapacity;
                maxFrameSignals = AcousticShockwaveSignal.MaxFrameSignals;
                lowTierFrameSignals = AcousticShockwaveSignal.LowTierFrameSignals;
                laneHash = AcousticShockwaveSignal.LaneHash;
                return true;
            }

            if (type == typeof(GlobalPanicSignal))
            {
                expectedCapacity = GlobalPanicSignal.ExpectedCapacity;
                maxFrameSignals = GlobalPanicSignal.MaxFrameSignals;
                lowTierFrameSignals = GlobalPanicSignal.LowTierFrameSignals;
                laneHash = GlobalPanicSignal.LaneHash;
                return true;
            }

            if (type == typeof(SeismicShockwaveSignal))
            {
                expectedCapacity = SeismicShockwaveSignal.ExpectedCapacity;
                maxFrameSignals = SeismicShockwaveSignal.MaxFrameSignals;
                lowTierFrameSignals = SeismicShockwaveSignal.LowTierFrameSignals;
                laneHash = SeismicShockwaveSignal.LaneHash;
                return true;
            }

            if (type == typeof(EclipseGameplayEventPayload))
            {
                expectedCapacity = EclipseGameplayEventPayload.ExpectedCapacity;
                maxFrameSignals = EclipseGameplayEventPayload.MaxFrameSignals;
                lowTierFrameSignals = EclipseGameplayEventPayload.LowTierFrameSignals;
                laneHash = EclipseGameplayEventPayload.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Construction.ConstructionPreviewSignal))
            {
                expectedCapacity = global::Hecton8.Construction.ConstructionPreviewSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Construction.ConstructionPreviewSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Construction.ConstructionPreviewSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Construction.ConstructionPreviewSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Construction.FloraExclusionSignal))
            {
                expectedCapacity = global::Hecton8.Construction.FloraExclusionSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Construction.FloraExclusionSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Construction.FloraExclusionSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Construction.FloraExclusionSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Core.Contracts.Physics.SeaglidePropulsionRequestSignal))
            {
                expectedCapacity = global::Hecton8.Core.Contracts.Physics.SeaglidePropulsionRequestSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Core.Contracts.Physics.SeaglidePropulsionRequestSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Core.Contracts.Physics.SeaglidePropulsionRequestSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Core.Contracts.Physics.SeaglidePropulsionRequestSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.MockItemAcquiredSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.MockItemAcquiredSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.MockItemAcquiredSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.MockItemAcquiredSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.MockItemAcquiredSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.MockCraftingRequestSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.MockCraftingRequestSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.MockCraftingRequestSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.MockCraftingRequestSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.MockCraftingRequestSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.MockConsumeSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.MockConsumeSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.MockConsumeSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.MockConsumeSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.MockConsumeSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.MockToolUsedSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.MockToolUsedSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.MockToolUsedSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.MockToolUsedSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.MockToolUsedSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.ToolBrokenSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.ToolBrokenSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.ToolBrokenSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.ToolBrokenSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.ToolBrokenSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.EncumbranceSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.EncumbranceSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.EncumbranceSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.EncumbranceSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.EncumbranceSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.EquipItemSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.EquipItemSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.EquipItemSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.EquipItemSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.EquipItemSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.MockHotbarSelectSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.MockHotbarSelectSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.MockHotbarSelectSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.MockHotbarSelectSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.MockHotbarSelectSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Inventory.DebrisDestroyedSignal))
            {
                expectedCapacity = global::Hecton8.Inventory.DebrisDestroyedSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Inventory.DebrisDestroyedSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Inventory.DebrisDestroyedSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Inventory.DebrisDestroyedSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Tools.ToolKinematics.Contracts.ToolTriggerPullSignal))
            {
                expectedCapacity = global::Hecton8.Tools.ToolKinematics.Contracts.ToolTriggerPullSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolTriggerPullSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolTriggerPullSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Tools.ToolKinematics.Contracts.ToolTriggerPullSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Tools.ToolKinematics.Contracts.ToolHeatSignal))
            {
                expectedCapacity = global::Hecton8.Tools.ToolKinematics.Contracts.ToolHeatSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolHeatSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolHeatSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Tools.ToolKinematics.Contracts.ToolHeatSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal))
            {
                expectedCapacity = global::Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.Tools.ToolKinematics.Contracts.ToolCarveRequestSignal))
            {
                expectedCapacity = global::Hecton8.Tools.ToolKinematics.Contracts.ToolCarveRequestSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolCarveRequestSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.Tools.ToolKinematics.Contracts.ToolCarveRequestSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.Tools.ToolKinematics.Contracts.ToolCarveRequestSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.UI.TerminalClickSignal))
            {
                expectedCapacity = global::Hecton8.UI.TerminalClickSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.UI.TerminalClickSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.UI.TerminalClickSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.UI.TerminalClickSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.UI.TerminalCommandSignal))
            {
                expectedCapacity = global::Hecton8.UI.TerminalCommandSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.UI.TerminalCommandSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.UI.TerminalCommandSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.UI.TerminalCommandSignal.LaneHash;
                return true;
            }

            if (type == typeof(global::Hecton8.UI.TerminalUnlockedSignal))
            {
                expectedCapacity = global::Hecton8.UI.TerminalUnlockedSignal.ExpectedCapacity;
                maxFrameSignals = global::Hecton8.UI.TerminalUnlockedSignal.MaxFrameSignals;
                lowTierFrameSignals = global::Hecton8.UI.TerminalUnlockedSignal.LowTierFrameSignals;
                laneHash = global::Hecton8.UI.TerminalUnlockedSignal.LaneHash;
                return true;
            }

            expectedCapacity = 0;
            maxFrameSignals = 0;
            lowTierFrameSignals = 0;
            laneHash = 0u;
            return false;
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
        private const int ThermalSourceSignalGuardCode = unchecked((int)0x51A1005Fu);
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
        private const int SystemGlitchSignalGuardCode = unchecked((int)0x51A1004Fu);
        private const int EntitySpawnSignalGuardCode = unchecked((int)0x51A10050u);
        private const int InputSignalGuardCode = unchecked((int)0x51A10051u);
        private const int StateCorrectionSignalGuardCode = unchecked((int)0x51A10052u);
        private const int SyncFenceSignalGuardCode = unchecked((int)0x51A10053u);
        private const int KccVelocitySignalGuardCode = unchecked((int)0x51A10054u);
        private const int LaserCutterEventPayloadGuardCode = unchecked((int)0x51A10055u);
        private const int SplashEventGuardCode = unchecked((int)0x51A10056u);
        private const int PhysicsEventPayloadGuardCode = unchecked((int)0x51A10057u);
        private const int DeferredSubmarineImpactSignalGuardCode = unchecked((int)0x51A10058u);
        private const int DebugSignalGuardCode = unchecked((int)0x51A10059u);
        private const int MockPlayerFootstepSignalGuardCode = unchecked((int)0x51A1005Au);
        private const int SignalWardenMockDamageSignalGuardCode = unchecked((int)0x51A1005Bu);
        private const int MockRockCollisionSignalGuardCode = unchecked((int)0x51A1005Cu);
        private const int MacroCollisionSignalGuardCode = unchecked((int)0x51A1005Du);
        private const int WakeRequestSignalGuardCode = unchecked((int)0x51A1005Eu);
        private const int DynamicMusicScalarSignalGuardCode = unchecked((int)0x51A10060u);
        private const int PlayerRespawnSignalGuardCode = unchecked((int)0x51A10061u);
        private const int VocalCueSignalGuardCode = unchecked((int)0x51A10062u);
        private const int InventoryDeathLootCacheSignalGuardCode = unchecked((int)0x51A10063u);
        private const int InventoryRespawnDeathAupSignalGuardCode = unchecked((int)0x51A10064u);
        private const int SeismicSignalGuardCode = unchecked((int)0x51A10065u);
        private const int SeismicShockwaveSignalGuardCode = unchecked((int)0x51A10066u);
        private const int ItemLifecycleSignalGuardCode = unchecked((int)0x51A10067u);
        private const int SessionLifecycleSignalGuardCode = unchecked((int)0x51A10068u);
        private const int ToxicityExposureSignalGuardCode = unchecked((int)0x51A10069u);
        private const int InventoryCommandSignalGuardCode = unchecked((int)0x51A1006Au);
        private const int ToxicBioluminescenceSignalGuardCode = unchecked((int)0x51A1006Bu);
        private const double MaxSignalAupExtentMeters = 100000.0d;
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
        private const byte GuardSystemGlitch = 79;
        private const byte GuardEntitySpawn = 80;
        private const byte GuardInput = 81;
        private const byte GuardStateCorrection = 82;
        private const byte GuardSyncFence = 83;
        private const byte GuardKccVelocity = 84;
        private const byte GuardLaserCutterEventPayload = 85;
        private const byte GuardSplashEvent = 86;
        private const byte GuardPhysicsEventPayload = 87;
        private const byte GuardDeferredSubmarineImpact = 88;
        private const byte GuardDebugSignal = 89;
        private const byte GuardMockPlayerFootstep = 90;
        private const byte GuardMockDamage = 91;
        private const byte GuardMockRockCollision = 92;
        private const byte GuardMacroCollision = 93;
        private const byte GuardWakeRequest = 94;
        private const byte GuardThermalSource = 95;
        private const byte GuardDynamicMusicScalar = 96;
        private const byte GuardPlayerRespawn = 97;
        private const byte GuardVocalCue = 98;
        private const byte GuardInventoryDeathLootCache = 99;
        private const byte GuardInventoryRespawnDeathAup = 100;
        private const byte GuardSeismicSignal = 101;
        private const byte GuardSeismicShockwaveSignal = 102;
        private const byte GuardItemLifecycle = 103;
        private const byte GuardSessionLifecycle = 104;
        private const byte GuardToxicityExposure = 105;
        private const byte GuardInventoryCommand = 106;
        private const byte GuardToxicBioluminescence = 107;

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
                case GuardToxicityExposure:
                {
                    ref ToxicityExposureSignal typed = ref UnsafeUtility.As<T, ToxicityExposureSignal>(ref signal);
                    return SanitizeToxicityExposureSignal(ref typed);
                }
                case GuardToxicBioluminescence:
                {
                    ref ToxicBioluminescenceSignal typed = ref UnsafeUtility.As<T, ToxicBioluminescenceSignal>(ref signal);
                    return SanitizeToxicBioluminescenceSignal(ref typed);
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
                case GuardThermalSource:
                {
                    ref ThermalSourceSignal typed = ref UnsafeUtility.As<T, ThermalSourceSignal>(ref signal);
                    return SanitizeThermalSourceSignal(ref typed);
                }
                case GuardDynamicMusicScalar:
                {
                    ref DynamicMusicScalarSignal typed = ref UnsafeUtility.As<T, DynamicMusicScalarSignal>(ref signal);
                    return SanitizeDynamicMusicScalarSignal(ref typed);
                }
                case GuardPlayerRespawn:
                {
                    ref PlayerRespawnSignal typed = ref UnsafeUtility.As<T, PlayerRespawnSignal>(ref signal);
                    return SanitizePlayerRespawnSignal(ref typed);
                }
                case GuardVocalCue:
                {
                    ref VocalCueSignal typed = ref UnsafeUtility.As<T, VocalCueSignal>(ref signal);
                    return SanitizeVocalCueSignal(ref typed);
                }
                case GuardInventoryDeathLootCache:
                {
                    ref InventoryDeathLootCacheSignal typed = ref UnsafeUtility.As<T, InventoryDeathLootCacheSignal>(ref signal);
                    return SanitizeInventoryDeathLootCacheSignal(ref typed);
                }
                case GuardInventoryRespawnDeathAup:
                {
                    ref InventoryRespawnDeathAupSignal typed = ref UnsafeUtility.As<T, InventoryRespawnDeathAupSignal>(ref signal);
                    return SanitizeInventoryRespawnDeathAupSignal(ref typed);
                }
                case GuardInventoryCommand:
                {
                    ref InventoryCommandSignal typed = ref UnsafeUtility.As<T, InventoryCommandSignal>(ref signal);
                    return SanitizeInventoryCommandSignal(ref typed);
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
                case GuardEntitySpawn:
                {
                    ref EntitySpawnSignal typed = ref UnsafeUtility.As<T, EntitySpawnSignal>(ref signal);
                    return SanitizeEntitySpawnSignal(ref typed);
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
                case GuardItemLifecycle:
                {
                    ref ItemLifecycleSignal typed = ref UnsafeUtility.As<T, ItemLifecycleSignal>(ref signal);
                    return SanitizeItemLifecycleSignal(ref typed);
                }
                case GuardSessionLifecycle:
                {
                    ref SessionLifecycleSignal typed = ref UnsafeUtility.As<T, SessionLifecycleSignal>(ref signal);
                    return SanitizeSessionLifecycleSignal(ref typed);
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
                case GuardSystemGlitch:
                {
                    ref SystemGlitchSignal typed = ref UnsafeUtility.As<T, SystemGlitchSignal>(ref signal);
                    return SanitizeSystemGlitchSignal(ref typed);
                }
                case GuardInput:
                {
                    ref InputSignal typed = ref UnsafeUtility.As<T, InputSignal>(ref signal);
                    return SanitizeInputSignal(ref typed);
                }
                case GuardStateCorrection:
                {
                    ref StateCorrectionSignal typed = ref UnsafeUtility.As<T, StateCorrectionSignal>(ref signal);
                    return SanitizeStateCorrectionSignal(ref typed);
                }
                case GuardSyncFence:
                {
                    ref SyncFenceSignal typed = ref UnsafeUtility.As<T, SyncFenceSignal>(ref signal);
                    return SanitizeSyncFenceSignal(ref typed);
                }
                case GuardKccVelocity:
                {
                    ref KccVelocitySignal typed = ref UnsafeUtility.As<T, KccVelocitySignal>(ref signal);
                    return SanitizeKccVelocitySignal(ref typed);
                }
                case GuardLaserCutterEventPayload:
                {
                    ref LaserCutterEventPayload typed = ref UnsafeUtility.As<T, LaserCutterEventPayload>(ref signal);
                    return SanitizeLaserCutterEventPayload(ref typed);
                }
                case GuardSplashEvent:
                {
                    ref SplashEvent typed = ref UnsafeUtility.As<T, SplashEvent>(ref signal);
                    return SanitizeSplashEvent(ref typed);
                }
                case GuardPhysicsEventPayload:
                {
                    ref PhysicsEventPayload typed = ref UnsafeUtility.As<T, PhysicsEventPayload>(ref signal);
                    return SanitizePhysicsEventPayload(ref typed);
                }
                case GuardDeferredSubmarineImpact:
                {
                    ref DeferredSubmarineImpactSignal typed = ref UnsafeUtility.As<T, DeferredSubmarineImpactSignal>(ref signal);
                    return SanitizeDeferredSubmarineImpactSignal(ref typed);
                }
                case GuardDebugSignal:
                {
                    ref DebugSignal typed = ref UnsafeUtility.As<T, DebugSignal>(ref signal);
                    return SanitizeDebugSignal(ref typed);
                }
                case GuardMockPlayerFootstep:
                {
                    ref MockPlayerFootstepSignal typed = ref UnsafeUtility.As<T, MockPlayerFootstepSignal>(ref signal);
                    return SanitizeMockPlayerFootstepSignal(ref typed);
                }
                case GuardMockDamage:
                {
                    ref SignalWardenMockDamageSignal typed = ref UnsafeUtility.As<T, SignalWardenMockDamageSignal>(ref signal);
                    return SanitizeSignalWardenMockDamageSignal(ref typed);
                }
                case GuardMockRockCollision:
                {
                    ref MockRockCollisionSignal typed = ref UnsafeUtility.As<T, MockRockCollisionSignal>(ref signal);
                    return SanitizeMockRockCollisionSignal(ref typed);
                }
                case GuardMacroCollision:
                {
                    ref MacroCollisionSignal typed = ref UnsafeUtility.As<T, MacroCollisionSignal>(ref signal);
                    return SanitizeMacroCollisionSignal(ref typed);
                }
                case GuardWakeRequest:
                {
                    ref WakeRequestSignal typed = ref UnsafeUtility.As<T, WakeRequestSignal>(ref signal);
                    return SanitizeWakeRequestSignal(ref typed);
                }
                case GuardSeismicSignal:
                {
                    ref SeismicSignal typed = ref UnsafeUtility.As<T, SeismicSignal>(ref signal);
                    return SanitizeSeismicSignal(ref typed);
                }
                case GuardSeismicShockwaveSignal:
                {
                    ref SeismicShockwaveSignal typed = ref UnsafeUtility.As<T, SeismicShockwaveSignal>(ref signal);
                    return SanitizeSeismicShockwaveSignal(ref typed);
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
            if (typeof(T) == typeof(ToxicityExposureSignal))
                return GuardToxicityExposure;
            if (typeof(T) == typeof(ToxicBioluminescenceSignal))
                return GuardToxicBioluminescence;
            if (typeof(T) == typeof(TemperatureChangedSignal))
                return GuardTemperatureChanged;
            if (typeof(T) == typeof(RadiationSourceSignal))
                return GuardRadiationSource;
            if (typeof(T) == typeof(ThermalSourceSignal))
                return GuardThermalSource;
            if (typeof(T) == typeof(DynamicMusicScalarSignal))
                return GuardDynamicMusicScalar;
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
            if (typeof(T) == typeof(EntitySpawnSignal))
                return GuardEntitySpawn;
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
            if (typeof(T) == typeof(ItemLifecycleSignal))
                return GuardItemLifecycle;
            if (typeof(T) == typeof(SessionLifecycleSignal))
                return GuardSessionLifecycle;
            if (typeof(T) == typeof(InventoryDeathLootCacheSignal))
                return GuardInventoryDeathLootCache;
            if (typeof(T) == typeof(InventoryRespawnDeathAupSignal))
                return GuardInventoryRespawnDeathAup;
            if (typeof(T) == typeof(InventoryCommandSignal))
                return GuardInventoryCommand;
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
            if (typeof(T) == typeof(SystemGlitchSignal))
                return GuardSystemGlitch;
            if (typeof(T) == typeof(InputSignal))
                return GuardInput;
            if (typeof(T) == typeof(StateCorrectionSignal))
                return GuardStateCorrection;
            if (typeof(T) == typeof(SyncFenceSignal))
                return GuardSyncFence;
            if (typeof(T) == typeof(KccVelocitySignal))
                return GuardKccVelocity;
            if (typeof(T) == typeof(LaserCutterEventPayload))
                return GuardLaserCutterEventPayload;
            if (typeof(T) == typeof(SplashEvent))
                return GuardSplashEvent;
            if (typeof(T) == typeof(PhysicsEventPayload))
                return GuardPhysicsEventPayload;
            if (typeof(T) == typeof(DeferredSubmarineImpactSignal))
                return GuardDeferredSubmarineImpact;
            if (typeof(T) == typeof(DebugSignal))
                return GuardDebugSignal;
            if (typeof(T) == typeof(MockPlayerFootstepSignal))
                return GuardMockPlayerFootstep;
            if (typeof(T) == typeof(SignalWardenMockDamageSignal))
                return GuardMockDamage;
            if (typeof(T) == typeof(MockRockCollisionSignal))
                return GuardMockRockCollision;
            if (typeof(T) == typeof(MacroCollisionSignal))
                return GuardMacroCollision;
            if (typeof(T) == typeof(WakeRequestSignal))
                return GuardWakeRequest;
            if (typeof(T) == typeof(PlayerRespawnSignal))
                return GuardPlayerRespawn;
            if (typeof(T) == typeof(VocalCueSignal))
                return GuardVocalCue;
            if (typeof(T) == typeof(SeismicSignal))
                return GuardSeismicSignal;
            if (typeof(T) == typeof(SeismicShockwaveSignal))
                return GuardSeismicShockwaveSignal;

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
        private static int SanitizeToxicityExposureSignal(ref ToxicityExposureSignal signal)
        {
            bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);
            bool outOfRangeAup =
                !repairedAup &&
                (math.abs(signal.AUP.x) > ToxicityExposureSignal.MaxSourceAupExtentMeters ||
                 math.abs(signal.AUP.y) > ToxicityExposureSignal.MaxSourceAupExtentMeters ||
                 math.abs(signal.AUP.z) > ToxicityExposureSignal.MaxSourceAupExtentMeters);
            if (outOfRangeAup)
                signal.AUP = double3.zero;

            int guardCode = repairedAup || outOfRangeAup ? ToxicityExposureSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.Exposure01))
                guardCode = ToxicityExposureSignalGuardCode;
            if (SanitizeUnit01(ref signal.ToxemiaDelta))
                guardCode = ToxicityExposureSignalGuardCode;

            byte supportedFlags = ToxicityExposureSignal.FlagHasSourceAup;
            byte flags = (byte)(signal.Flags & supportedFlags);
            bool hasInvalidSourceAup = (flags & ToxicityExposureSignal.FlagHasSourceAup) != 0 &&
                math.lengthsq(signal.AUP) <= 0.000001d;
            if (repairedAup || outOfRangeAup || hasInvalidSourceAup)
                flags = (byte)(flags & ~ToxicityExposureSignal.FlagHasSourceAup);
            if (signal.Flags != flags)
                guardCode = ToxicityExposureSignalGuardCode;
            signal.Flags = flags;

            if (signal._pad0 != 0 || signal._pad1 != 0 || signal._pad2 != 0ul || signal._pad3 != 0ul)
                guardCode = ToxicityExposureSignalGuardCode;
            signal._pad0 = 0;
            signal._pad1 = 0;
            signal._pad2 = 0ul;
            signal._pad3 = 0ul;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeToxicBioluminescenceSignal(ref ToxicBioluminescenceSignal signal)
        {
            bool repairedAup = SanitizeDouble3Zero(ref signal.AUP);
            bool outOfRangeAup =
                !repairedAup &&
                (math.abs(signal.AUP.x) > ToxicityExposureSignal.MaxSourceAupExtentMeters ||
                 math.abs(signal.AUP.y) > ToxicityExposureSignal.MaxSourceAupExtentMeters ||
                 math.abs(signal.AUP.z) > ToxicityExposureSignal.MaxSourceAupExtentMeters);
            if (outOfRangeAup)
                signal.AUP = double3.zero;

            int guardCode = repairedAup || outOfRangeAup ? ToxicBioluminescenceSignalGuardCode : 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = ToxicBioluminescenceSignalGuardCode;
            if (SanitizeNonNegative(ref signal.ToxicDensity))
                guardCode = ToxicBioluminescenceSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.LocalNormal))
                guardCode = ToxicBioluminescenceSignalGuardCode;

            byte supportedFlags = ToxicBioluminescenceSignal.FlagActive;
            byte flags = (byte)(signal.Flags & supportedFlags);
            bool hasInvalidSourceAup = (flags & ToxicBioluminescenceSignal.FlagActive) != 0 &&
                math.lengthsq(signal.AUP) <= 0.000001d;
            bool hasInactiveScalar = (flags & ToxicBioluminescenceSignal.FlagActive) != 0 &&
                (signal.Intensity01 <= 0.0001f || signal.ToxicDensity <= 0.0001f);
            if (repairedAup || outOfRangeAup || hasInvalidSourceAup || hasInactiveScalar)
                flags = (byte)(flags & ~ToxicBioluminescenceSignal.FlagActive);
            if (signal.Flags != flags)
                guardCode = ToxicBioluminescenceSignalGuardCode;
            signal.Flags = flags;

            if (signal._pad0 != 0 || signal._pad1 != 0ul)
                guardCode = ToxicBioluminescenceSignalGuardCode;
            signal._pad0 = 0;
            signal._pad1 = 0ul;

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
            bool repairedAup = SanitizeAup(ref signal.PositionAup);
            bool repairedIntensity = SanitizeNonNegative(ref signal.Intensity);
            bool repairedRadius = SanitizeNonNegative(ref signal.RadiusMeters);
            int guardCode = repairedAup || repairedIntensity || repairedRadius
                ? RadiationSourceSignalGuardCode
                : 0;

            bool knownOperation =
                signal.Operation == RadiationSourceSignal.OperationUpsert ||
                signal.Operation == RadiationSourceSignal.OperationRemove;
            if (!knownOperation)
            {
                signal.Operation = RadiationSourceSignal.OperationRemove;
                guardCode = RadiationSourceSignalGuardCode;
            }

            if (signal.Operation == RadiationSourceSignal.OperationUpsert &&
                (repairedAup || signal.Intensity <= 0f || signal.RadiusMeters <= 0f))
            {
                signal.Operation = RadiationSourceSignal.OperationRemove;
                signal.Intensity = 0f;
                signal.RadiusMeters = 0f;
                guardCode = RadiationSourceSignalGuardCode;
            }
            else if (signal.Operation == RadiationSourceSignal.OperationRemove &&
                     (signal.Intensity != 0f || signal.RadiusMeters != 0f))
            {
                signal.Intensity = 0f;
                signal.RadiusMeters = 0f;
                guardCode = RadiationSourceSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeThermalSourceSignal(ref ThermalSourceSignal signal)
        {
            bool repairedAup = SanitizeAup(ref signal.PositionAup);
            bool repairedRadius = SanitizeNonNegative(ref signal.RadiusMeters);
            bool repairedIntensity = SanitizeNonNegative(ref signal.IntensityCelsiusPerSecond);
            int guardCode = repairedAup || repairedRadius || repairedIntensity
                ? ThermalSourceSignalGuardCode
                : 0;

            if (repairedAup || signal.RadiusMeters <= 0f || signal.IntensityCelsiusPerSecond <= 0f)
            {
                signal.RadiusMeters = 0f;
                signal.IntensityCelsiusPerSecond = 0f;
                guardCode = ThermalSourceSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDynamicMusicScalarSignal(ref DynamicMusicScalarSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Tension01))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeNonNegative(ref signal.DepthMeters))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeUnit01(ref signal.GlobalQualityWeight))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeUnit01(ref signal.DamageImpulse01))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeUnit01(ref signal.StingerImpulse01))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeUnit01(ref signal.PitchKick01))
                guardCode = DynamicMusicScalarSignalGuardCode;
            if (SanitizeUnit01(ref signal.MusicActivity01))
                guardCode = DynamicMusicScalarSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePlayerRespawnSignal(ref PlayerRespawnSignal signal)
        {
            int guardCode = 0;
            if (SanitizeDouble3Zero(ref signal.DeathAUP))
            {
                signal.Flags |= PlayerRespawnSignalFlags.InvalidDeathAup;
                guardCode = PlayerRespawnSignalGuardCode;
            }
            if (SanitizeDouble3Zero(ref signal.RespawnAUP))
            {
                signal.RespawnAUP = signal.DeathAUP;
                signal.Flags |= PlayerRespawnSignalFlags.InvalidTargetAup;
                guardCode = PlayerRespawnSignalGuardCode;
            }

            if (signal.Phase != PlayerRespawnSignalPhase.Request &&
                signal.Phase != PlayerRespawnSignalPhase.Committed)
            {
                signal.Phase = PlayerRespawnSignalPhase.Request;
                signal.Flags |= PlayerRespawnSignalFlags.Requested;
                guardCode = PlayerRespawnSignalGuardCode;
            }
            else if (signal.Phase == PlayerRespawnSignalPhase.Request &&
                     (signal.Flags & PlayerRespawnSignalFlags.Requested) == 0u)
            {
                signal.Flags |= PlayerRespawnSignalFlags.Requested;
                guardCode = PlayerRespawnSignalGuardCode;
            }
            else if (signal.Phase == PlayerRespawnSignalPhase.Committed &&
                     (signal.Flags & PlayerRespawnSignalFlags.Committed) == 0u)
            {
                signal.Flags |= PlayerRespawnSignalFlags.Committed;
                guardCode = PlayerRespawnSignalGuardCode;
            }

            if (signal.SuspendCollisionFrames > PlayerRespawnSignal.MaxSuspendCollisionFrames)
            {
                signal.SuspendCollisionFrames = PlayerRespawnSignal.MaxSuspendCollisionFrames;
                guardCode = PlayerRespawnSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeInventoryDeathLootCacheSignal(ref InventoryDeathLootCacheSignal signal)
        {
            int guardCode = 0;
            if (SanitizeAup(ref signal.PositionAup))
            {
                signal.Flags |= 0x80000000u;
                guardCode = InventoryDeathLootCacheSignalGuardCode;
            }

            if (signal.Quantity == 0)
            {
                signal.Quantity = 1;
                guardCode = InventoryDeathLootCacheSignalGuardCode;
            }

            if (signal.QualityMilli > 1000)
            {
                signal.QualityMilli = 1000;
                guardCode = InventoryDeathLootCacheSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeInventoryRespawnDeathAupSignal(ref InventoryRespawnDeathAupSignal signal)
        {
            int guardCode = 0;
            if (SanitizeDouble3Zero(ref signal.DeathAUP))
            {
                signal.Flags |= 0x80000000u;
                guardCode = InventoryRespawnDeathAupSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeInventoryCommandSignal(ref InventoryCommandSignal signal)
        {
            if (signal.Command == InventoryCommandSignalCommands.Sort ||
                signal.Command == InventoryCommandSignalCommands.DropNonEquippedResources)
            {
                return 0;
            }

            return InventoryCommandSignalGuardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeVocalCueSignal(ref VocalCueSignal signal)
        {
            int guardCode = 0;
            if (!math.isfinite(signal.VolumeScalar) || signal.VolumeScalar < 0f)
            {
                signal.VolumeScalar = 1f;
                guardCode = VocalCueSignalGuardCode;
            }
            else
            {
                signal.VolumeScalar = math.saturate(signal.VolumeScalar);
            }

            if (!math.isfinite(signal.PlaybackSpeed) || signal.PlaybackSpeed <= 0f)
            {
                signal.PlaybackSpeed = 1f;
                guardCode = VocalCueSignalGuardCode;
            }
            else
            {
                signal.PlaybackSpeed = math.clamp(signal.PlaybackSpeed, 0.25f, 2f);
            }

            if (SanitizeUnit01(ref signal.RadioDistortion01))
                guardCode = VocalCueSignalGuardCode;
            if (SanitizeUnit01(ref signal.SpatialBlend01))
                guardCode = VocalCueSignalGuardCode;
            if (!math.isfinite(signal.SourceAupLocalX))
            {
                signal.SourceAupLocalX = 0f;
                guardCode = VocalCueSignalGuardCode;
            }
            if (!math.isfinite(signal.SourceAupLocalY))
            {
                signal.SourceAupLocalY = 0f;
                guardCode = VocalCueSignalGuardCode;
            }
            if (!math.isfinite(signal.SourceAupLocalZ))
            {
                signal.SourceAupLocalZ = 0f;
                guardCode = VocalCueSignalGuardCode;
            }

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
            if (SanitizeUnit01(ref signal.Supersaturation01))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeUnit01(ref signal.Narcosis01))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.AmbientPressureAtm))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.NitrogenLoadAtm))
                guardCode = PhysiologyStateSignalGuardCode;
            if (SanitizeNonNegative(ref signal.AscentRateMetersPerSecond))
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
        private static int SanitizeEntitySpawnSignal(ref EntitySpawnSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? EntitySpawnSignalGuardCode : 0;
            if (signal.RequestedCount < signal.SpawnedCount)
            {
                signal.RequestedCount = signal.SpawnedCount;
                guardCode = EntitySpawnSignalGuardCode;
            }

            if (signal.EntityKind == 0)
            {
                signal.EntityKind = EntitySpawnSignal.KindEcology;
                guardCode = EntitySpawnSignalGuardCode;
            }

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
            if (SanitizeAup(ref signal.PositionAup) ||
                signal.ItemHash == 0u ||
                signal.Quantity == 0)
            {
                return ItemAcquiredSignalGuardCode;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeItemLifecycleSignal(ref ItemLifecycleSignal signal)
        {
            int guardCode = SanitizeFloat3Zero(ref signal.RuntimePosition) ? ItemLifecycleSignalGuardCode : 0;
            if (!math.isfinite(signal.UnitWeightKg) || signal.UnitWeightKg < 0f)
            {
                signal.UnitWeightKg = 0f;
                guardCode = ItemLifecycleSignalGuardCode;
            }

            if (signal.Quantity < 0)
            {
                signal.Quantity = 0;
                guardCode = ItemLifecycleSignalGuardCode;
            }

            if (signal.YieldUnitCount < 0)
            {
                signal.YieldUnitCount = 0;
                guardCode = ItemLifecycleSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSessionLifecycleSignal(ref SessionLifecycleSignal signal)
        {
            if (!SanitizeFloat3Zero(ref signal.PlayerPosition))
                return 0;

            return SessionLifecycleSignalGuardCode;
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
        private static int SanitizeDebugSignal(ref DebugSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Zero(ref signal.Position))
                guardCode = DebugSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.Vector))
                guardCode = DebugSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.Value0))
                guardCode = DebugSignalGuardCode;
            if (SanitizeFiniteZero(ref signal.Value1))
                guardCode = DebugSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeMockPlayerFootstepSignal(ref MockPlayerFootstepSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.Aup) ? MockPlayerFootstepSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.Normal))
                guardCode = MockPlayerFootstepSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = MockPlayerFootstepSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSignalWardenMockDamageSignal(ref SignalWardenMockDamageSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.Aup) ? SignalWardenMockDamageSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.Normal))
                guardCode = SignalWardenMockDamageSignalGuardCode;
            if (SanitizeNonNegative(ref signal.Damage))
                guardCode = SignalWardenMockDamageSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeMockRockCollisionSignal(ref MockRockCollisionSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.Aup) ? MockRockCollisionSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Magnitude))
                guardCode = MockRockCollisionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeMacroCollisionSignal(ref MacroCollisionSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.Aup) ? MacroCollisionSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Magnitude))
                guardCode = MacroCollisionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeWakeRequestSignal(ref WakeRequestSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.OriginAup) ? WakeRequestSignalGuardCode : 0;
            if (!math.isfinite(signal.RadiusMeters) || signal.RadiusMeters <= 0f)
            {
                signal.RadiusMeters = 0f;
                guardCode = WakeRequestSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSeismicSignal(ref SeismicSignal signal)
        {
            int guardCode = 0;
            if (SanitizeFloat3Forward(ref signal.Direction))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeUnit01(ref signal.CameraJitter01))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeUnit01(ref signal.AudioIntensity01))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeNonNegative(ref signal.ThermalEruptionProbabilityScalar))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeDouble3Zero(ref signal.EpicenterAUP))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeNonNegative(ref signal.CurrentRadiusMeters))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeNonNegative(ref signal.PWaveRadiusMeters))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeNonNegative(ref signal.SWaveRadiusMeters))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeNonNegative(ref signal.MagnitudeRichter))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeUnit01(ref signal.PWaveAmplitude01))
                guardCode = SeismicSignalGuardCode;
            if (SanitizeUnit01(ref signal.SWaveAmplitude01))
                guardCode = SeismicSignalGuardCode;

            float reservedFrequency = math.asfloat(signal.Reserved0);
            if (!math.isfinite(reservedFrequency) || reservedFrequency < 0f)
            {
                signal.Reserved0 = math.asuint(0.1f);
                guardCode = SeismicSignalGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSeismicShockwaveSignal(ref SeismicShockwaveSignal signal)
        {
            int guardCode = SanitizeDouble3Zero(ref signal.EpicenterAUP) ? SeismicShockwaveSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Magnitude))
                guardCode = SeismicShockwaveSignalGuardCode;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = SeismicShockwaveSignalGuardCode;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = SeismicShockwaveSignalGuardCode;

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
        private static int SanitizeSystemGlitchSignal(ref SystemGlitchSignal signal)
        {
            int guardCode = 0;
            if (SanitizeUnit01(ref signal.Intensity01))
                guardCode = SystemGlitchSignalGuardCode;
            if (SanitizeNonNegative(ref signal.DurationSeconds))
                guardCode = SystemGlitchSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeInputSignal(ref InputSignal signal)
        {
            int guardCode = SanitizeFloat2Zero(ref signal.MoveDelta) ? InputSignalGuardCode : 0;
            if (SanitizeFloat2Zero(ref signal.LookDelta))
                guardCode = InputSignalGuardCode;
            if (!math.isfinite(signal.VerticalDelta))
            {
                signal.VerticalDelta = 0f;
                guardCode = InputSignalGuardCode;
            }
            else
            {
                float clamped = math.clamp(signal.VerticalDelta, -1f, 1f);
                if (clamped != signal.VerticalDelta)
                {
                    signal.VerticalDelta = clamped;
                    guardCode = InputSignalGuardCode;
                }
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeStateCorrectionSignal(ref StateCorrectionSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? StateCorrectionSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.RuntimePosition))
                guardCode = StateCorrectionSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.Velocity))
                guardCode = StateCorrectionSignalGuardCode;
            if (SanitizeQuaternionIdentity(ref signal.Rotation))
                guardCode = StateCorrectionSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSyncFenceSignal(ref SyncFenceSignal signal)
        {
            int guardCode = SanitizeAup(ref signal.PositionAup) ? SyncFenceSignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.RuntimePosition))
                guardCode = SyncFenceSignalGuardCode;
            if (SanitizeFloat3Zero(ref signal.Velocity))
                guardCode = SyncFenceSignalGuardCode;
            if (SanitizeQuaternionIdentity(ref signal.Rotation))
                guardCode = SyncFenceSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeKccVelocitySignal(ref KccVelocitySignal signal)
        {
            int guardCode = SanitizeAup(ref signal.BodyAup) ? KccVelocitySignalGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.Velocity))
                guardCode = KccVelocitySignalGuardCode;
            if (SanitizeNonNegative(ref signal.PlanarSpeedSq))
                guardCode = KccVelocitySignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeLaserCutterEventPayload(ref LaserCutterEventPayload signal)
        {
            int guardCode = SanitizeUnit01(ref signal.Heat01) ? LaserCutterEventPayloadGuardCode : 0;

            if (signal.EventType != (ushort)LaserCutterEventType.HeatChanged &&
                signal.EventType != (ushort)LaserCutterEventType.BeamStateChanged)
            {
                signal.EventType = (ushort)LaserCutterEventType.HeatChanged;
                guardCode = LaserCutterEventPayloadGuardCode;
            }

            ushort allowedFlags = signal.EventType == (ushort)LaserCutterEventType.BeamStateChanged
                ? LaserCutterEventPayload.StateFlagBeamActive
                : (ushort)0;
            ushort sanitizedFlags = (ushort)(signal.StateFlags & allowedFlags);
            if (sanitizedFlags != signal.StateFlags)
            {
                signal.StateFlags = sanitizedFlags;
                guardCode = LaserCutterEventPayloadGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeSplashEvent(ref SplashEvent signal)
        {
            int guardCode = SanitizeFloat3Zero(ref signal.RuntimePosition) ? SplashEventGuardCode : 0;
            if (SanitizeFloat3Zero(ref signal.AbsoluteUniversePosition))
                guardCode = SplashEventGuardCode;
            if (!math.all(math.isfinite(signal.SurfaceNormal)))
            {
                signal.SurfaceNormal = new float3(0f, 1f, 0f);
                guardCode = SplashEventGuardCode;
            }
            if (SanitizeNonNegative(ref signal.ImpactSpeedMetersPerSecond))
                guardCode = SplashEventGuardCode;
            if (SanitizeNonNegative(ref signal.KineticEnergyJoules))
                guardCode = SplashEventGuardCode;
            if (SanitizeUnit01(ref signal.SubmersionFactor))
                guardCode = SplashEventGuardCode;
            if (signal.SampleIndex < 0)
            {
                signal.SampleIndex = 0;
                guardCode = SplashEventGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizePhysicsEventPayload(ref PhysicsEventPayload signal)
        {
            int guardCode = SanitizeVector3Zero(ref signal.RuntimePosition) ? PhysicsEventPayloadGuardCode : 0;
            if (SanitizeVector3Zero(ref signal.Direction))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeVector3Zero(ref signal.ForceVector))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeVector3Zero(ref signal.ImpulseVector))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeNonNegative(ref signal.RadiusMeters))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeNonNegative(ref signal.Scalar0))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeNonNegative(ref signal.Scalar1))
                guardCode = PhysicsEventPayloadGuardCode;
            if (SanitizeNonNegative(ref signal.Scalar2))
                guardCode = PhysicsEventPayloadGuardCode;
            if (signal.EventType < (ushort)PhysicsEventType.PressureImpulse ||
                signal.EventType > (ushort)PhysicsEventType.FloodMassShift)
            {
                signal.EventType = (ushort)PhysicsEventType.PressureImpulse;
                guardCode = PhysicsEventPayloadGuardCode;
            }

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeDeferredSubmarineImpactSignal(ref DeferredSubmarineImpactSignal signal)
        {
            int guardCode = SanitizeFloat3Zero(ref signal.LocalPoint) ? DeferredSubmarineImpactSignalGuardCode : 0;
            if (SanitizeNonNegative(ref signal.Magnitude))
                guardCode = DeferredSubmarineImpactSignalGuardCode;
            if (SanitizeNonNegative(ref signal.Depth))
                guardCode = DeferredSubmarineImpactSignalGuardCode;
            if (SanitizeUnit01(ref signal.PreviousIntegrityNormalized))
                guardCode = DeferredSubmarineImpactSignalGuardCode;
            if (SanitizeUnit01(ref signal.NextIntegrityNormalized))
                guardCode = DeferredSubmarineImpactSignalGuardCode;

            return guardCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeCombatDamageSignal(ref CombatDamageSignal signal)
        {
            int guardCode = 0;
            if (!CombatDamageSignalCodec.IsFiniteAup(signal.ImpactAup))
            {
                signal.ImpactAup = double3.zero;
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
        private static bool SanitizeVector3Zero(ref Vector3 value)
        {
            if (math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z))
                return false;

            value = Vector3.zero;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SanitizeAup(ref AbsoluteUniversePosition position)
        {
            bool invalid =
                !math.isfinite(position.LocalX) ||
                !math.isfinite(position.LocalY) ||
                !math.isfinite(position.LocalZ) ||
                math.abs(((double)position.GridX * AbsoluteUniversePosition.CellSizeMeters) + position.LocalX) > MaxSignalAupExtentMeters ||
                math.abs(((double)position.GridY * AbsoluteUniversePosition.CellSizeMeters) + position.LocalY) > MaxSignalAupExtentMeters ||
                math.abs(((double)position.GridZ * AbsoluteUniversePosition.CellSizeMeters) + position.LocalZ) > MaxSignalAupExtentMeters;

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
        private static bool SanitizeQuaternionIdentity(ref quaternion value)
        {
            if (math.all(math.isfinite(value.value)) && math.lengthsq(value.value) > 0.000001f)
                return false;

            value = quaternion.identity;
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
        private static byte EncodeSignalQualityWeightByte(float qualityWeight01)
        {
            float sanitized = math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 0f;
            return (byte)math.clamp((int)math.round(sanitized * byte.MaxValue), 0, byte.MaxValue);
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

#region JulesLink_SignalPrioritySortCalculator
        private static void JulesLink_SignalPrioritySortCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SignalPrioritySortCalculator); }
        #endregion

        #region JulesLink_FixedCapacityRingBuffer
        private static void JulesLink_FixedCapacityRingBuffer() { _ = typeof(Hecton8.PureLogic.Systems.FixedCapacityRingBuffer); }
        #endregion

    }
}
