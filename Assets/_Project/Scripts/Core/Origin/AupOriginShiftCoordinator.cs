using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>64-byte AUP authority record used by the origin rebase job.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AUP_StateDTO
    {
        [FieldOffset(0)] public double3 GlobalPosition;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public uint SectorHash;
        [FieldOffset(40)] public uint ShiftFrameId;
        [FieldOffset(44)] public int3 LocalMillimeters;
        [FieldOffset(56)] public uint FiniteFlags;
        [FieldOffset(60)] public uint SourceSystemId;

        /// <summary>Returns a direct ref to an unmanaged AUP record.</summary>
        internal static unsafe ref AUP_StateDTO ElementAt(void* basePointer, int index)
        {
            return ref UnsafeUtility.AsRef<AUP_StateDTO>((byte*)basePointer + (index * UnsafeUtility.SizeOf<AUP_StateDTO>()));
        }
    }

    /// <summary>32-byte aligned origin-shift payload for cross-domain math handoff.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OriginShiftSignalDTO
    {
        [FieldOffset(0)] public double3 ShiftDelta;
        [FieldOffset(24)] public uint NewSectorHash;
        [FieldOffset(28)] public uint _pad0;
    }

    /// <summary>Blind camera mock used when no upstream camera AUP contract is available.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public partial struct MockCameraAUP
    {
        [FieldOffset(0)] public double3 GlobalPosition;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public uint SectorHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint _pad0;
    }

    /// <summary>Raw vault views proving a 50,000-entity origin rebase without sibling-domain dependencies.</summary>
    internal struct MockEntityArrays
    {
        internal NativeArray<AUP_StateDTO> States;
        internal NativeArray<float3> Velocities;
        internal NativeArray<float3> HistoricalPoints;
        internal NativeArray<AupOriginShiftTelemetryEntry> TelemetryRing;
        internal NativeArray<AupOriginShiftRuntimeState> RuntimeState;
        internal int ActiveCount;
        internal int HistoricalCount;
    }

    /// <summary>Editor-facing readback packet for the AUP Universe Tuner.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupUniverseTunerSnapshot
    {
        [FieldOffset(0)] public double3 GlobalPosition;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public float RebaseThresholdMeters;
        [FieldOffset(40)] public float SectorSizeMeters;
        [FieldOffset(44)] public uint ShiftSequence;
        [FieldOffset(48)] public uint SectorHash;
        [FieldOffset(52)] public int IsOriginShiftPending;
        [FieldOffset(56)] public int TimeSliceActive;
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>Per-shift scheduling result consumed after the job fence completes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupOriginShiftScheduleInfo
    {
        [FieldOffset(0)] public OriginShiftSignalDTO Signal;
        [FieldOffset(32)] public int EntitiesScheduled;
        [FieldOffset(36)] public int HotEntitiesScheduled;
        [FieldOffset(40)] public int HistoricalPointsScheduled;
        [FieldOffset(44)] public int BatchStartIndex;
        [FieldOffset(48)] public int BatchCount;
        [FieldOffset(52)] public int TotalActiveEntities;
        [FieldOffset(56)] public uint ShiftSequence;
        [FieldOffset(60)] public byte TimeSliced;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _pad0;
    }

    /// <summary>Runtime constants and flags stored in unmanaged vault memory.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 120)]
    public struct AupOriginShiftRuntimeState
    {
        [FieldOffset(0)] public double3 PendingTimeSliceShiftDelta;
        [FieldOffset(24)] public float RebaseLimitMeters;
        [FieldOffset(28)] public float SectorSizeMeters;
        [FieldOffset(32)] public int BatchSize;
        [FieldOffset(36)] public int ActiveEntityCount;
        [FieldOffset(40)] public int ActiveHistoricalCount;
        [FieldOffset(44)] public int IsOriginShiftPending;
        [FieldOffset(48)] public int ManualRebaseRequested;
        [FieldOffset(52)] public int TimeSliceStartIndex;
        [FieldOffset(56)] public int TimeSliceActive;
        [FieldOffset(60)] public uint RebaseCount;
        [FieldOffset(64)] public uint LastSectorHash;
        [FieldOffset(68)] public uint CsvSourceHash;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public float LastComputeTimeMs;
        [FieldOffset(80)] public int LastEntitiesShifted;
        [FieldOffset(84)] public int LastHistoricalPointsShifted;
        [FieldOffset(88)] public int LastNonFiniteCount;
        [FieldOffset(92)] public int CsvRevision;
        [FieldOffset(96)] public uint PendingTimeSliceSectorHash;
        [FieldOffset(100)] public int LastHotEntitiesShifted;
        [FieldOffset(104)] public uint LastShiftSequence;
        [FieldOffset(108)] public uint PendingTimeSliceShiftSequence;
        [FieldOffset(112)] public int HistoricalTimeSliceStartIndex;
        [FieldOffset(116)] private uint _pad0;
    }

    /// <summary>Fixed 300-frame black-box row for origin-shift post-mortem dumps.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AupOriginShiftTelemetryEntry
    {
        [FieldOffset(0)] public double3 ShiftDelta;
        [FieldOffset(24)] public double3 TotalUniverseOffset;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint RebaseCount;
        [FieldOffset(56)] public uint ShiftSequence;
        [FieldOffset(60)] public uint SectorHash;
        [FieldOffset(64)] public int EntitiesShifted;
        [FieldOffset(68)] public int HistoricalPointsShifted;
        [FieldOffset(72)] public int BatchStartIndex;
        [FieldOffset(76)] public int BatchCount;
        [FieldOffset(80)] public int NonFiniteCount;
        [FieldOffset(84)] public float RebaseComputeTimeMs;
        [FieldOffset(88)] public float SystemHealthIndex01;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public float3 CameraLocalPosition;
        [FieldOffset(108)] public uint CameraSectorHash;
        [FieldOffset(112)] public uint PositionHash;
        [FieldOffset(116)] public int HotEntitiesShifted;
        [FieldOffset(120)] private ulong _pad1;
    }

    /// <summary>Single atomic counter isolated to one cache line for rebase worker contention.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupPaddedAtomicCounter
    {
        [FieldOffset(0)] public int NonFiniteCount;
        [FieldOffset(4)] private uint _pad0;
        [FieldOffset(8)] private ulong _pad1;
        [FieldOffset(16)] private ulong _pad2;
        [FieldOffset(24)] private ulong _pad3;
        [FieldOffset(32)] private ulong _pad4;
        [FieldOffset(40)] private ulong _pad5;
        [FieldOffset(48)] private ulong _pad6;
        [FieldOffset(56)] private ulong _pad7;
    }

    /// <summary>64-byte fixed header for origin-shift blackbox dumps.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupOriginShiftDumpHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint HeaderBytes;
        [FieldOffset(16)] public uint EntryCount;
        [FieldOffset(20)] public uint EntryStrideBytes;
        [FieldOffset(24)] public uint PayloadBytes;
        [FieldOffset(28)] public uint OldestRingIndex;
        [FieldOffset(32)] public uint LatestFrame;
        [FieldOffset(36)] public uint EndianTag;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    /// <summary>Double-precision helpers for AUP comparisons that must never demote to float.</summary>
    public readonly struct H8DoubleMath
    {
        /// <summary>Returns squared distance between two absolute-universe positions.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceSq(double3 a, double3 b)
        {
            double3 delta = a - b;
            double distanceSq = math.lengthsq(delta);
            return math.isfinite(distanceSq) ? distanceSq : double.PositiveInfinity;
        }

        /// <summary>Returns a finite normalized vector, or zero for invalid and near-zero inputs.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 Normalize(double3 value)
        {
            double lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 1e-24d)
                return double3.zero;

            return value * (1.0d / math.sqrt(math.max(lengthSq, 1e-24d)));
        }
    }

    /// <summary>
    /// Vault-backed AUP rebase coordinator. It owns no scene hierarchy and touches no velocities during rebase.
    /// </summary>
    public static unsafe class AupOriginShiftCoordinator
    {
        private const int MockEntityCapacity = 50000;
        private const int MockHistoricalPointCapacity = 50000;
        private const int TelemetryCapacity = 300;
        private const int CsvScratchCapacity = 4096;
        private const int RuntimeStateCount = 1;
        private const int MockCameraCount = 1;
        private const int CounterCount = 1;
        private const int DumpWriteBufferBytes = 4096;
        private const ulong DumpMagic = 0x504D445055413848ul; // H8AUPDMP, fixed little-endian bytes.
        private const uint DumpVersion = 2u;
        private const uint DumpEndianLittleTag = 0x00454C48u; // HLE\0
        private const uint DumpEndianBigTag = 0x00454248u; // HBE\0
        private const float EmergencyRebaseLimitMeters = 4000f;
        private const float DefaultSectorSizeMeters = 5000f;
        private const int DefaultBatchSize = 10000;
        private const int MinimumTimeSliceBatchSize = 10000;
        private const double MockCameraSimulationTickSeconds = 1.0d / 60.0d;
        private const double MockCameraSpeedMetersPerSecond = 125.0d;
        private const float RebaseWatchdogMs = 1.0f;
        private const uint RuntimeFlagEmergencyThresholds = 1u << 0;
        private const uint RuntimeFlagCsvOverride = 1u << 1;
        private const uint RuntimeFlagTimeSliced = 1u << 2;
        private const uint AupStateFlagFinite = 1u << 0;
        private const uint TelemetryFlagNaN = 1u << 0;
        private const uint TelemetryFlagWatchdog = 1u << 1;
        private const uint TelemetryFlagTimeSliced = 1u << 2;
        private const uint TelemetryFlagFrameSample = 1u << 3;
        private const uint TelemetryFlagShiftCommit = 1u << 4;
        private const uint CsvKeyRebaseLimitHash = 0x6FA8B5A4u;
        private const uint CsvKeyRebaseLimitMetersHash = 0x7EA9F2DAu;
        private const uint CsvKeySectorSizeMetersHash = 0xD2AEDCF4u;
        private const uint CsvKeyBatchSizeHash = 0x5E21151Au;
        private const uint CsvKeyEntityCountHash = 0x3599A6B1u;
        private const SystemID OwnerSystemId = SystemID.CoreDeterminism;
        private const BufferID MockStatesBuffer = (BufferID)73030;
        private const BufferID MockVelocitiesBuffer = (BufferID)73031;
        private const BufferID MockHistoricalPointsBuffer = (BufferID)73032;
        private const BufferID TelemetryRingBuffer = (BufferID)73033;
        private const BufferID RuntimeStateBuffer = (BufferID)73034;
        private const BufferID MockCameraBuffer = (BufferID)73035;
        private const BufferID CsvScratchBuffer = (BufferID)73036;
        private const BufferID CounterBuffer = (BufferID)73037;

        private static IDataVault _cachedVault;
        private static VaultGenerationHandle<AUP_StateDTO> _statesHandle;
        private static VaultGenerationHandle<float3> _velocitiesHandle;
        private static VaultGenerationHandle<float3> _historicalPointsHandle;
        private static VaultGenerationHandle<AupOriginShiftTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<AupOriginShiftRuntimeState> _runtimeStateHandle;
        private static VaultGenerationHandle<MockCameraAUP> _mockCameraHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;
        private static VaultGenerationHandle<AupPaddedAtomicCounter> _counterHandle;
        private static long _lastCsvWriteTicks;
        private static string _csvPath;
        private static string _dumpPath;
        private static string _h8DumpPath;

        /// <summary>Cold path fallback used when binary archaeology yields no threshold file.</summary>
        public static void GenerateEmergencyMockThresholds(IDataVault vault)
        {
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return;

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            runtime.RebaseLimitMeters = EmergencyRebaseLimitMeters;
            runtime.SectorSizeMeters = DefaultSectorSizeMeters;
            runtime.BatchSize = DefaultBatchSize;
            runtime.ActiveEntityCount = MockEntityCapacity;
            runtime.ActiveHistoricalCount = MockHistoricalPointCapacity;
            runtime.Flags |= RuntimeFlagEmergencyThresholds;
            arrays.RuntimeState[0] = runtime;
        }

        /// <summary>Ensures all unmanaged origin-shift vault buffers exist and are initialized.</summary>
        internal static bool EnsureRuntimeState(IDataVault vault, out MockEntityArrays arrays)
        {
            arrays = default;
            if (vault == null)
                return false;

            if (!ReferenceEquals(_cachedVault, vault))
            {
                ReleaseVaultHandles(_cachedVault);
                ResetVaultHandles();
                _cachedVault = vault;
            }

            if (!TryResolveOrAcquire(
                    vault,
                    ref _statesHandle,
                    MockStatesBuffer,
                    MockEntityCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out arrays.States,
                    out bool statesCreated) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _velocitiesHandle,
                    MockVelocitiesBuffer,
                    MockEntityCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out arrays.Velocities,
                    out bool velocitiesCreated) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _historicalPointsHandle,
                    MockHistoricalPointsBuffer,
                    MockHistoricalPointCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out arrays.HistoricalPoints,
                    out bool historicalCreated) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _telemetryHandle,
                    TelemetryRingBuffer,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out arrays.TelemetryRing,
                    out _) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _runtimeStateHandle,
                    RuntimeStateBuffer,
                    RuntimeStateCount,
                    NativeArrayOptions.ClearMemory,
                    out arrays.RuntimeState,
                    out bool runtimeCreated) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _mockCameraHandle,
                    MockCameraBuffer,
                    MockCameraCount,
                    NativeArrayOptions.ClearMemory,
                    out _,
                    out _) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _csvScratchHandle,
                    CsvScratchBuffer,
                    CsvScratchCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _,
                    out _) ||
                !TryResolveOrAcquire(
                    vault,
                    ref _counterHandle,
                    CounterBuffer,
                    CounterCount,
                    NativeArrayOptions.ClearMemory,
                    out _,
                    out _))
            {
                arrays = default;
                return false;
            }

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            if (runtime.Flags == 0u || runtimeCreated || statesCreated || velocitiesCreated || historicalCreated)
            {
                runtime.RebaseLimitMeters = EmergencyRebaseLimitMeters;
                runtime.SectorSizeMeters = DefaultSectorSizeMeters;
                runtime.BatchSize = DefaultBatchSize;
                runtime.ActiveEntityCount = MockEntityCapacity;
                runtime.ActiveHistoricalCount = MockHistoricalPointCapacity;
                runtime.Flags = RuntimeFlagEmergencyThresholds;
                arrays.RuntimeState[0] = runtime;

                AupMockInitializeJob mockInitializeJob = new AupMockInitializeJob
                {
                    States = arrays.States,
                    Velocities = arrays.Velocities,
                    HistoricalPoints = arrays.HistoricalPoints,
                    SectorSizeMeters = DefaultSectorSizeMeters
                };
                for (int i = 0; i < MockEntityCapacity; i++)
                    mockInitializeJob.Execute(i);
            }

            arrays.ActiveCount = math.clamp(arrays.RuntimeState[0].ActiveEntityCount, 0, arrays.States.Length);
            arrays.HistoricalCount = math.clamp(arrays.RuntimeState[0].ActiveHistoricalCount, 0, arrays.HistoricalPoints.Length);
            return true;
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            out bool allocatedOrResized)
            where T : struct
        {
            buffer = default;
            allocatedOrResized = false;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                    return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);
            allocatedOrResized = true;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        private static bool TryResolveMockCamera(IDataVault vault, out NativeArray<MockCameraAUP> camera)
        {
            return TryOpenVaultBuffer(vault, in _mockCameraHandle, MockCameraBuffer, MockCameraCount, out camera);
        }

        private static bool TryResolveCounter(IDataVault vault, out NativeArray<AupPaddedAtomicCounter> counters)
        {
            return TryOpenVaultBuffer(vault, in _counterHandle, CounterBuffer, CounterCount, out counters);
        }

        private static bool TryResolveCsvScratch(IDataVault vault, out NativeArray<byte> scratch)
        {
            return TryOpenVaultBuffer(vault, in _csvScratchHandle, CsvScratchBuffer, CsvScratchCapacity, out scratch);
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_statesHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statesHandle);
            if (_velocitiesHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _velocitiesHandle);
            if (_historicalPointsHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _historicalPointsHandle);
            if (_telemetryHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _telemetryHandle);
            if (_runtimeStateHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _runtimeStateHandle);
            if (_mockCameraHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _mockCameraHandle);
            if (_csvScratchHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _csvScratchHandle);
            if (_counterHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _counterHandle);
        }

        private static void ResetVaultHandles()
        {
            _statesHandle = default;
            _velocitiesHandle = default;
            _historicalPointsHandle = default;
            _telemetryHandle = default;
            _runtimeStateHandle = default;
            _mockCameraHandle = default;
            _csvScratchHandle = default;
            _counterHandle = default;
            _lastCsvWriteTicks = 0L;
        }

        /// <summary>PRE_SIMULATION monitor that updates the mock AUP and reports a requested shift.</summary>
        public static bool TickPreSimulation(
            IDataVault vault,
            float deltaTime,
            bool hasRealAnchor,
            Vector3 anchorLocalPosition,
            double3 totalUniverseOffset,
            out Vector3 requestedShift)
        {
            requestedShift = Vector3.zero;
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return false;

            ContinueTimeSlicedRebase(vault, arrays);

            if (!TryResolveMockCamera(vault, out NativeArray<MockCameraAUP> camera))
                return false;

            _ = deltaTime;
            float3 anchorLocal = new float3(anchorLocalPosition.x, anchorLocalPosition.y, anchorLocalPosition.z);
            double3 deterministicMockStep = hasRealAnchor
                ? double3.zero
                : new double3(MockCameraSimulationTickSeconds * MockCameraSpeedMetersPerSecond, 0d, 0d);
            IncrementMockCameraAup(
                camera,
                arrays.RuntimeState,
                deterministicMockStep,
                totalUniverseOffset + new double3(anchorLocal.x, anchorLocal.y, anchorLocal.z),
                totalUniverseOffset,
                hasRealAnchor ? 1 : 0);

            MonitorAupThreshold(camera, arrays.RuntimeState, totalUniverseOffset);

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            MockCameraAUP cameraState = camera[0];
            RecordFrameTelemetry(arrays.TelemetryRing, in runtime, in cameraState, totalUniverseOffset, 0u);
            if (runtime.IsOriginShiftPending == 0 && runtime.ManualRebaseRequested == 0)
                return false;

            double3 localDouble = cameraState.GlobalPosition - totalUniverseOffset;
            double localLengthSq = H8DoubleMath.DistanceSq(cameraState.GlobalPosition, totalUniverseOffset);
            if (!math.all(math.isfinite(localDouble)) || !math.isfinite(localLengthSq))
            {
                runtime.IsOriginShiftPending = 0;
                runtime.ManualRebaseRequested = 0;
                runtime.LastNonFiniteCount++;
                arrays.RuntimeState[0] = runtime;
                RecordFrameTelemetry(arrays.TelemetryRing, in runtime, in cameraState, totalUniverseOffset, TelemetryFlagNaN);
                DumpOriginShiftBlackBox(arrays.TelemetryRing);
                return false;
            }

            if (localLengthSq <= 0.0001d && runtime.ManualRebaseRequested != 0)
            {
                localDouble = new double3(math.max(1f, runtime.RebaseLimitMeters * 0.001f), 0d, 0d);
                localLengthSq = math.lengthsq(localDouble);
            }

            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            if (!math.all(math.isfinite(local)))
            {
                runtime.IsOriginShiftPending = 0;
                runtime.ManualRebaseRequested = 0;
                runtime.LastNonFiniteCount++;
                arrays.RuntimeState[0] = runtime;
                RecordFrameTelemetry(arrays.TelemetryRing, in runtime, in cameraState, totalUniverseOffset, TelemetryFlagNaN);
                DumpOriginShiftBlackBox(arrays.TelemetryRing);
                return false;
            }

            runtime.IsOriginShiftPending = 0;
            runtime.ManualRebaseRequested = 0;
            arrays.RuntimeState[0] = runtime;
            cameraState.LocalPosition = local;
            camera[0] = cameraState;
            requestedShift = new Vector3(local.x, local.y, local.z);
            return localLengthSq > 0.0001d;
        }

        /// <summary>Schedules a vault rebase. Velocity buffers are not passed to this method by design.</summary>
        public static JobHandle ScheduleVaultOriginRebase(
            IDataVault vault,
            Vector3 shiftOffset,
            double3 newTotalUniverseOffset,
            uint shiftSequence,
            out AupOriginShiftScheduleInfo info,
            JobHandle dependency = default)
        {
            info = default;
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return dependency;

            if (!TryResolveCounter(vault, out NativeArray<AupPaddedAtomicCounter> counters))
                return dependency;

            counters[0] = default;
            float3 shiftFloat = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(shiftFloat)) || math.lengthsq(shiftFloat) <= 0.0001f)
                return dependency;

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            float sectorSize = SanitizeSectorSize(runtime.SectorSizeMeters);
            uint sectorHash = ResolveSectorHash(newTotalUniverseOffset, sectorSize);
            double3 shiftDelta = new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            int activeCount = math.clamp(runtime.ActiveEntityCount, 0, arrays.States.Length);
            int historicalCount = math.clamp(runtime.ActiveHistoricalCount, 0, arrays.HistoricalPoints.Length);
            float qualityWeight = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            int batchCount = ResolveQualityScaledBatchSize(runtime.BatchSize, activeCount, qualityWeight);
            int supplementalHistoricalCount = ResolveSupplementalHistoricalMaxLength(vault);
            int totalHistoricalCount = math.max(historicalCount, supplementalHistoricalCount);
            int historicalBatchCount = ResolveQualityScaledBatchSize(runtime.BatchSize, totalHistoricalCount, qualityWeight);
            bool timeSliced = batchCount < activeCount || historicalBatchCount < totalHistoricalCount;

            info.Signal = new OriginShiftSignalDTO
            {
                ShiftDelta = shiftDelta,
                NewSectorHash = sectorHash,
                _pad0 = 0u
            };
            info.EntitiesScheduled = batchCount;
            info.HistoricalPointsScheduled = 0;
            info.BatchStartIndex = 0;
            info.BatchCount = batchCount;
            info.TotalActiveEntities = activeCount;
            info.ShiftSequence = shiftSequence;
            info.TimeSliced = timeSliced ? (byte)1 : (byte)0;

            JobHandle handle = dependency;
            if (batchCount > 0)
            {
                handle = new AupStateRebaseJob
                {
                    States = (AUP_StateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(arrays.States),
                    NonFiniteCounter = (AupPaddedAtomicCounter*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters),
                    ShiftDelta = shiftDelta,
                    NewSectorHash = sectorHash,
                    ShiftFrameId = shiftSequence,
                    StartIndex = 0
                }.Schedule(batchCount, 128, handle);
            }

            handle = ScheduleHotEntityRebase(vault, 0, batchCount, activeCount, shiftFloat, shiftSequence, handle, ref info);
            handle = ScheduleHistoricalRebaseBatch(vault, arrays, 0, historicalBatchCount, shiftFloat, handle, ref info);

            runtime.LastSectorHash = sectorHash;
            runtime.LastEntitiesShifted = info.EntitiesScheduled;
            runtime.LastHotEntitiesShifted = info.HotEntitiesScheduled;
            runtime.LastHistoricalPointsShifted = info.HistoricalPointsScheduled;
            runtime.PendingTimeSliceSectorHash = sectorHash;
            runtime.PendingTimeSliceShiftDelta = shiftDelta;
            runtime.LastShiftSequence = shiftSequence;
            runtime.PendingTimeSliceShiftSequence = timeSliced ? shiftSequence : 0u;
            if (timeSliced)
            {
                runtime.TimeSliceActive = 1;
                runtime.TimeSliceStartIndex = batchCount;
                runtime.HistoricalTimeSliceStartIndex = historicalBatchCount;
                runtime.Flags |= RuntimeFlagTimeSliced;
            }
            else
            {
                runtime.TimeSliceActive = 0;
                runtime.TimeSliceStartIndex = 0;
                runtime.HistoricalTimeSliceStartIndex = 0;
                runtime.PendingTimeSliceShiftSequence = 0u;
                runtime.Flags &= ~RuntimeFlagTimeSliced;
            }

            arrays.RuntimeState[0] = runtime;
            return handle;
        }

        /// <summary>Records rebase cost and dumps the black box on watchdog breach or NaN detection.</summary>
        public static void RecordRebaseCompletion(
            IDataVault vault,
            in AupOriginShiftScheduleInfo info,
            double elapsedMilliseconds,
            double3 totalUniverseOffset)
        {
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return;

            int nonFiniteCount = TryResolveCounter(vault, out NativeArray<AupPaddedAtomicCounter> counters) ? counters[0].NonFiniteCount : 0;
            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            runtime.RebaseCount++;
            runtime.LastShiftSequence = info.ShiftSequence;
            runtime.LastComputeTimeMs = math.isfinite((float)elapsedMilliseconds) ? (float)elapsedMilliseconds : float.MaxValue;
            runtime.LastNonFiniteCount = nonFiniteCount;
            arrays.RuntimeState[0] = runtime;

            int cursor = SystemDispatcher.CurrentFrameIndex % TelemetryCapacity;
            uint flags = TelemetryFlagShiftCommit | (info.TimeSliced != 0 ? TelemetryFlagTimeSliced : 0u);
            if (nonFiniteCount > 0)
                flags |= TelemetryFlagNaN;
            if (elapsedMilliseconds > RebaseWatchdogMs)
                flags |= TelemetryFlagWatchdog;
            float3 shiftLocal = new float3(
                (float)info.Signal.ShiftDelta.x,
                (float)info.Signal.ShiftDelta.y,
                (float)info.Signal.ShiftDelta.z);

            arrays.TelemetryRing[cursor] = new AupOriginShiftTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                RebaseCount = runtime.RebaseCount,
                ShiftSequence = info.ShiftSequence,
                SectorHash = info.Signal.NewSectorHash,
                EntitiesShifted = info.EntitiesScheduled,
                HotEntitiesShifted = info.HotEntitiesScheduled,
                HistoricalPointsShifted = info.HistoricalPointsScheduled,
                BatchStartIndex = info.BatchStartIndex,
                BatchCount = info.BatchCount,
                NonFiniteCount = nonFiniteCount,
                RebaseComputeTimeMs = runtime.LastComputeTimeMs,
                SystemHealthIndex01 = math.saturate(HomeostasisBrain.SystemHealthIndex01),
                Flags = flags,
                ShiftDelta = info.Signal.ShiftDelta,
                TotalUniverseOffset = totalUniverseOffset,
                CameraLocalPosition = shiftLocal,
                CameraSectorHash = info.Signal.NewSectorHash,
                PositionHash = math.hash(shiftLocal)
            };

            if ((flags & (TelemetryFlagNaN | TelemetryFlagWatchdog)) != 0u)
                DumpOriginShiftBlackBox(arrays.TelemetryRing);
        }

        /// <summary>Requests a manual rebase from the editor facade.</summary>
        public static void RequestManualRebase(IDataVault vault)
        {
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return;

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            runtime.ManualRebaseRequested = 1;
            runtime.IsOriginShiftPending = 1;
            arrays.RuntimeState[0] = runtime;
        }

        /// <summary>Updates the threshold stored in unmanaged runtime state.</summary>
        public static void SetRebaseThreshold(IDataVault vault, float thresholdMeters)
        {
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return;

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            runtime.RebaseLimitMeters = math.clamp(math.isfinite(thresholdMeters) ? thresholdMeters : EmergencyRebaseLimitMeters, 2000f, 8000f);
            arrays.RuntimeState[0] = runtime;
        }

        /// <summary>Returns editor/debug readback without scene traversal.</summary>
        public static bool TryGetEditorSnapshot(IDataVault vault, uint shiftSequence, out AupUniverseTunerSnapshot snapshot)
        {
            snapshot = default;
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return false;

            if (!TryResolveMockCamera(vault, out NativeArray<MockCameraAUP> camera))
                return false;

            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            MockCameraAUP cameraState = camera[0];
            snapshot.GlobalPosition = cameraState.GlobalPosition;
            snapshot.LocalPosition = cameraState.LocalPosition;
            snapshot.RebaseThresholdMeters = runtime.RebaseLimitMeters;
            snapshot.SectorSizeMeters = runtime.SectorSizeMeters;
            snapshot.ShiftSequence = shiftSequence;
            snapshot.SectorHash = cameraState.SectorHash;
            snapshot.IsOriginShiftPending = runtime.IsOriginShiftPending;
            snapshot.TimeSliceActive = runtime.TimeSliceActive;
            return true;
        }

        /// <summary>Cold editor/development bridge for designer CSV reloads; never called from the simulation tick.</summary>
        public static bool TryReloadCsvOverrideFromDisk(IDataVault vault)
        {
#if UNITY_EDITOR
            if (!EnsureRuntimeState(vault, out MockEntityArrays arrays))
                return false;

            return TryPollCsvOverride(vault, arrays.RuntimeState);
#else
            return false;
#endif
        }

        private static JobHandle ScheduleHistoricalRebaseBatch(
            IDataVault vault,
            MockEntityArrays arrays,
            int startIndex,
            int requestedCount,
            float3 shiftDelta,
            JobHandle dependency,
            ref AupOriginShiftScheduleInfo info)
        {
            if (requestedCount <= 0)
                return dependency;

            int historicalCount = arrays.HistoricalCount;
            if (arrays.HistoricalPoints.IsCreated && historicalCount > 0)
            {
                dependency = ScheduleNativeHistoricalFloat3Rebase(
                    arrays.HistoricalPoints,
                    historicalCount,
                    startIndex,
                    requestedCount,
                    shiftDelta,
                    dependency,
                    ref info);
            }

            dependency = ScheduleHistoricalFloat3Rebase(vault, BufferID.TetherCablePositions, startIndex, requestedCount, shiftDelta, dependency, ref info);
            dependency = ScheduleHistoricalFloat3Rebase(vault, BufferID.TetherCablePreviousPositions, startIndex, requestedCount, shiftDelta, dependency, ref info);
            dependency = ScheduleHistoricalFloat3Rebase(vault, BufferID.TetherVisualSegmentPositions, startIndex, requestedCount, shiftDelta, dependency, ref info);
            dependency = ScheduleHistoricalFloat3Rebase(vault, BufferID.TetherVisualAnchorPositions, startIndex, requestedCount, shiftDelta, dependency, ref info);
            return dependency;
        }

        private static JobHandle ScheduleNativeHistoricalFloat3Rebase(
            NativeArray<float3> points,
            int activeCount,
            int startIndex,
            int requestedCount,
            float3 shiftDelta,
            JobHandle dependency,
            ref AupOriginShiftScheduleInfo info)
        {
            int count = ResolveHistoricalBatchCount(points, activeCount, startIndex, requestedCount, out int clampedStart);
            if (count <= 0)
                return dependency;

            info.HistoricalPointsScheduled += count;
            return new Float3HistoricalRebaseJob
            {
                Points = points,
                ShiftDelta = shiftDelta,
                StartIndex = clampedStart
            }.Schedule(count, 128, dependency);
        }

        private static JobHandle ScheduleHistoricalFloat3Rebase(
            IDataVault vault,
            BufferID bufferId,
            int startIndex,
            int requestedCount,
            float3 shiftDelta,
            JobHandle dependency,
            ref AupOriginShiftScheduleInfo info)
        {
            if (requestedCount <= 0)
                return dependency;

            if (!TryOpenExistingVaultBuffer(vault, bufferId, 1, out NativeArray<float3> points))
                return dependency;

            int count = ResolveHistoricalBatchCount(points, points.Length, startIndex, requestedCount, out int clampedStart);
            if (count <= 0)
                return dependency;

            info.HistoricalPointsScheduled += count;
            return new Float3HistoricalRebaseJob
            {
                Points = points,
                ShiftDelta = shiftDelta,
                StartIndex = clampedStart
            }.Schedule(count, 128, dependency);
        }

        private static void ContinueTimeSlicedRebase(IDataVault vault, MockEntityArrays arrays)
        {
            AupOriginShiftRuntimeState runtime = arrays.RuntimeState[0];
            if (runtime.TimeSliceActive == 0)
                return;

            if (!TryResolveCounter(vault, out NativeArray<AupPaddedAtomicCounter> counters))
                return;

            int activeCount = math.clamp(runtime.ActiveEntityCount, 0, arrays.States.Length);
            int startIndex = math.clamp(runtime.TimeSliceStartIndex, 0, activeCount);
            int totalHistoricalCount = math.max(
                math.clamp(runtime.ActiveHistoricalCount, 0, arrays.HistoricalPoints.Length),
                ResolveSupplementalHistoricalMaxLength(vault));
            int historicalStartIndex = math.clamp(runtime.HistoricalTimeSliceStartIndex, 0, totalHistoricalCount);
            float qualityWeight = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            int batchCount = activeCount > startIndex
                ? math.min(ResolveQualityScaledBatchSize(runtime.BatchSize, activeCount, qualityWeight), activeCount - startIndex)
                : 0;
            int historicalBatchCount = totalHistoricalCount > historicalStartIndex
                ? math.min(ResolveQualityScaledBatchSize(runtime.BatchSize, totalHistoricalCount, qualityWeight), totalHistoricalCount - historicalStartIndex)
                : 0;

            if (batchCount <= 0 && historicalBatchCount <= 0)
            {
                runtime.TimeSliceActive = 0;
                runtime.TimeSliceStartIndex = 0;
                runtime.HistoricalTimeSliceStartIndex = 0;
                runtime.PendingTimeSliceShiftSequence = 0u;
                runtime.Flags &= ~RuntimeFlagTimeSliced;
                arrays.RuntimeState[0] = runtime;
                return;
            }

            if (batchCount > 0)
            {
                AupStateRebaseJob stateRebaseJob = new AupStateRebaseJob
                {
                    States = (AUP_StateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(arrays.States),
                    NonFiniteCounter = (AupPaddedAtomicCounter*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters),
                    ShiftDelta = runtime.PendingTimeSliceShiftDelta,
                    NewSectorHash = runtime.PendingTimeSliceSectorHash,
                    ShiftFrameId = runtime.PendingTimeSliceShiftSequence != 0u
                        ? runtime.PendingTimeSliceShiftSequence
                        : (runtime.LastShiftSequence != 0u ? runtime.LastShiftSequence : 1u),
                    StartIndex = startIndex
                };
                for (int i = 0; i < batchCount; i++)
                    stateRebaseJob.Execute(i);
            }

            int hotShifted = RunHotEntityRebaseSlice(
                vault,
                startIndex,
                batchCount,
                activeCount,
                new float3(
                    (float)runtime.PendingTimeSliceShiftDelta.x,
                    (float)runtime.PendingTimeSliceShiftDelta.y,
                    (float)runtime.PendingTimeSliceShiftDelta.z),
                runtime.PendingTimeSliceShiftSequence != 0u
                    ? runtime.PendingTimeSliceShiftSequence
                    : (runtime.LastShiftSequence != 0u ? runtime.LastShiftSequence : 1u));
            int historicalShifted = RunHistoricalRebaseBatch(
                vault,
                arrays,
                historicalStartIndex,
                historicalBatchCount,
                new float3(
                    (float)runtime.PendingTimeSliceShiftDelta.x,
                    (float)runtime.PendingTimeSliceShiftDelta.y,
                    (float)runtime.PendingTimeSliceShiftDelta.z));

            runtime.TimeSliceStartIndex = startIndex + batchCount;
            runtime.HistoricalTimeSliceStartIndex = historicalStartIndex + historicalBatchCount;
            runtime.LastEntitiesShifted += batchCount;
            runtime.LastHotEntitiesShifted += hotShifted;
            runtime.LastHistoricalPointsShifted += historicalShifted;
            if (runtime.TimeSliceStartIndex >= activeCount && runtime.HistoricalTimeSliceStartIndex >= totalHistoricalCount)
            {
                runtime.TimeSliceActive = 0;
                runtime.TimeSliceStartIndex = 0;
                runtime.HistoricalTimeSliceStartIndex = 0;
                runtime.PendingTimeSliceShiftSequence = 0u;
                runtime.Flags &= ~RuntimeFlagTimeSliced;
            }

            arrays.RuntimeState[0] = runtime;
        }

        private static void RecordFrameTelemetry(
            NativeArray<AupOriginShiftTelemetryEntry> telemetryRing,
            in AupOriginShiftRuntimeState runtime,
            in MockCameraAUP camera,
            double3 totalUniverseOffset,
            uint extraFlags)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                return;

            float3 cameraLocal = camera.LocalPosition;
            bool finite = math.all(math.isfinite(cameraLocal)) &&
                math.all(math.isfinite(camera.GlobalPosition)) &&
                math.all(math.isfinite(totalUniverseOffset));
            uint flags = TelemetryFlagFrameSample | extraFlags;
            if (!finite)
                flags |= TelemetryFlagNaN;
            if (runtime.TimeSliceActive != 0)
                flags |= TelemetryFlagTimeSliced;

            float qualityWeight = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            int telemetryActiveCount = math.clamp(runtime.ActiveEntityCount, 0, MockEntityCapacity);
            int telemetryBatchCount = ResolveQualityScaledBatchSize(runtime.BatchSize, telemetryActiveCount, qualityWeight);

            telemetryRing[SystemDispatcher.CurrentFrameIndex % TelemetryCapacity] = new AupOriginShiftTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                RebaseCount = runtime.RebaseCount,
                ShiftSequence = runtime.LastShiftSequence,
                SectorHash = camera.SectorHash,
                EntitiesShifted = runtime.LastEntitiesShifted,
                HistoricalPointsShifted = runtime.LastHistoricalPointsShifted,
                BatchStartIndex = runtime.TimeSliceStartIndex,
                BatchCount = telemetryBatchCount,
                NonFiniteCount = runtime.LastNonFiniteCount,
                RebaseComputeTimeMs = runtime.LastComputeTimeMs,
                SystemHealthIndex01 = math.saturate(HomeostasisBrain.SystemHealthIndex01),
                Flags = flags,
                ShiftDelta = double3.zero,
                TotalUniverseOffset = totalUniverseOffset,
                CameraLocalPosition = finite ? cameraLocal : float3.zero,
                CameraSectorHash = camera.SectorHash,
                PositionHash = finite ? math.hash(cameraLocal) : 0u,
                HotEntitiesShifted = runtime.LastHotEntitiesShifted
            };
        }

        private static JobHandle ScheduleHotEntityRebase(
            IDataVault vault,
            int startIndex,
            int requestedCount,
            int activeCount,
            float3 shiftDelta,
            uint shiftFrameId,
            JobHandle dependency,
            ref AupOriginShiftScheduleInfo info)
        {
            if (requestedCount <= 0 ||
                !TryOpenExistingVaultBuffer(vault, BufferID.VaultHotEntityData, 1, out NativeArray<VaultHotEntityData> hotEntities))
                return dependency;

            int hotCount = math.min(math.max(activeCount, 0), hotEntities.Length);
            int clampedStart = math.clamp(startIndex, 0, hotCount);
            int batchCount = math.min(requestedCount, hotCount - clampedStart);
            if (batchCount <= 0)
                return dependency;

            info.HotEntitiesScheduled += batchCount;
            return new VaultHotEntityRebaseJob
            {
                HotEntities = hotEntities,
                ShiftDelta = shiftDelta,
                ShiftFrameId = shiftFrameId,
                StartIndex = clampedStart
            }.Schedule(batchCount, 128, dependency);
        }

        private static int RunHotEntityRebaseSlice(
            IDataVault vault,
            int startIndex,
            int requestedCount,
            int activeCount,
            float3 shiftDelta,
            uint shiftFrameId)
        {
            if (requestedCount <= 0 ||
                !TryOpenExistingVaultBuffer(vault, BufferID.VaultHotEntityData, 1, out NativeArray<VaultHotEntityData> hotEntities))
                return 0;

            int hotCount = math.min(math.max(activeCount, 0), hotEntities.Length);
            int clampedStart = math.clamp(startIndex, 0, hotCount);
            int batchCount = math.min(requestedCount, hotCount - clampedStart);
            if (batchCount <= 0)
                return 0;

            VaultHotEntityRebaseJob hotRebaseJob = new VaultHotEntityRebaseJob
            {
                HotEntities = hotEntities,
                ShiftDelta = shiftDelta,
                ShiftFrameId = shiftFrameId,
                StartIndex = clampedStart
            };
            for (int i = 0; i < batchCount; i++)
                hotRebaseJob.Execute(i);
            return batchCount;
        }

        private static int RunHistoricalRebaseBatch(
            IDataVault vault,
            MockEntityArrays arrays,
            int startIndex,
            int requestedCount,
            float3 shiftDelta)
        {
            if (requestedCount <= 0)
                return 0;

            int shifted = 0;
            if (arrays.HistoricalPoints.IsCreated && arrays.HistoricalCount > 0)
            {
                shifted += RunNativeHistoricalFloat3Rebase(
                    arrays.HistoricalPoints,
                    arrays.HistoricalCount,
                    startIndex,
                    requestedCount,
                    shiftDelta);
            }

            shifted += RunHistoricalFloat3Rebase(vault, BufferID.TetherCablePositions, startIndex, requestedCount, shiftDelta);
            shifted += RunHistoricalFloat3Rebase(vault, BufferID.TetherCablePreviousPositions, startIndex, requestedCount, shiftDelta);
            shifted += RunHistoricalFloat3Rebase(vault, BufferID.TetherVisualSegmentPositions, startIndex, requestedCount, shiftDelta);
            shifted += RunHistoricalFloat3Rebase(vault, BufferID.TetherVisualAnchorPositions, startIndex, requestedCount, shiftDelta);
            return shifted;
        }

        private static int RunNativeHistoricalFloat3Rebase(
            NativeArray<float3> points,
            int activeCount,
            int startIndex,
            int requestedCount,
            float3 shiftDelta)
        {
            int count = ResolveHistoricalBatchCount(points, activeCount, startIndex, requestedCount, out int clampedStart);
            if (count <= 0)
                return 0;

            Float3HistoricalRebaseJob historicalRebaseJob = new Float3HistoricalRebaseJob
            {
                Points = points,
                ShiftDelta = shiftDelta,
                StartIndex = clampedStart
            };
            for (int i = 0; i < count; i++)
                historicalRebaseJob.Execute(i);
            return count;
        }

        private static int RunHistoricalFloat3Rebase(
            IDataVault vault,
            BufferID bufferId,
            int startIndex,
            int requestedCount,
            float3 shiftDelta)
        {
            if (requestedCount <= 0 ||
                !TryOpenExistingVaultBuffer(vault, bufferId, 1, out NativeArray<float3> points))
            {
                return 0;
            }

            return RunNativeHistoricalFloat3Rebase(points, points.Length, startIndex, requestedCount, shiftDelta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveHistoricalBatchCount(
            NativeArray<float3> points,
            int activeCount,
            int startIndex,
            int requestedCount,
            out int clampedStart)
        {
            int count = points.IsCreated ? math.clamp(activeCount, 0, points.Length) : 0;
            clampedStart = math.clamp(startIndex, 0, count);
            return requestedCount > 0 ? math.min(requestedCount, count - clampedStart) : 0;
        }

        private static int ResolveSupplementalHistoricalMaxLength(IDataVault vault)
        {
            int count = 0;
            count = math.max(count, ResolveFloat3BufferLength(vault, BufferID.TetherCablePositions));
            count = math.max(count, ResolveFloat3BufferLength(vault, BufferID.TetherCablePreviousPositions));
            count = math.max(count, ResolveFloat3BufferLength(vault, BufferID.TetherVisualSegmentPositions));
            count = math.max(count, ResolveFloat3BufferLength(vault, BufferID.TetherVisualAnchorPositions));
            return count;
        }

        private static int ResolveFloat3BufferLength(IDataVault vault, BufferID bufferId)
        {
            return TryOpenExistingVaultBuffer(vault, bufferId, 1, out NativeArray<float3> points)
                ? points.Length
                : 0;
        }

#if UNITY_EDITOR
        private static bool TryPollCsvOverride(IDataVault vault, NativeArray<AupOriginShiftRuntimeState> runtimeState)
        {
            string path = ResolveCsvPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryResolveCsvScratch(vault, out NativeArray<byte> scratch))
                return false;

            int bytesRead;
            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks == _lastCsvWriteTicks)
                    return false;

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))
                {
                    bytesRead = stream.Read(new Span<byte>((byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch), scratch.Length));
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (bytesRead <= 0)
                return false;

            ParseCsvOverrides(scratch, bytesRead, runtimeState);
            _lastCsvWriteTicks = ticks;
            return true;
        }

        private static void ParseCsvOverrides(
            NativeArray<byte> bytes,
            int length,
            NativeArray<AupOriginShiftRuntimeState> runtimeState)
        {
            AupOriginShiftRuntimeState runtime = runtimeState[0];
            int index = 0;
            while (index < length)
            {
                SkipCsvWhitespace(bytes, length, ref index);
                uint keyHash = 2166136261u;
                bool hasKey = false;
                while (index < length)
                {
                    byte b = bytes[index];
                    if (b == (byte)'=' || b == (byte)',' || b == (byte)'\n' || b == (byte)'\r')
                        break;

                    if (b >= (byte)'A' && b <= (byte)'Z')
                        b = (byte)(b + 32);
                    keyHash = (keyHash ^ b) * 16777619u;
                    hasKey = true;
                    index++;
                }

                while (index < length && (bytes[index] == (byte)'=' || bytes[index] == (byte)',' || bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                    index++;

                float value = ParseCsvFloat(bytes, length, ref index);
                if (hasKey && math.isfinite(value))
                {
                    if (keyHash == CsvKeyRebaseLimitHash || keyHash == CsvKeyRebaseLimitMetersHash)
                        runtime.RebaseLimitMeters = math.clamp(value, 2000f, 8000f);
                    else if (keyHash == CsvKeySectorSizeMetersHash)
                        runtime.SectorSizeMeters = SanitizeSectorSize(value);
                    else if (keyHash == CsvKeyBatchSizeHash)
                        runtime.BatchSize = ResolveBatchSize((int)value);
                    else if (keyHash == CsvKeyEntityCountHash)
                        runtime.ActiveEntityCount = math.clamp((int)value, 1, MockEntityCapacity);
                }

                while (index < length && bytes[index] != (byte)'\n')
                    index++;
                if (index < length)
                    index++;
            }

            runtime.Flags |= RuntimeFlagCsvOverride;
            runtime.CsvRevision++;
            runtime.CsvSourceHash = unchecked(runtime.CsvSourceHash + 0x43535631u);
            runtimeState[0] = runtime;
        }

        private static void SkipCsvWhitespace(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte b = bytes[index];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r')
                    return;

                index++;
            }
        }

        private static float ParseCsvFloat(NativeArray<byte> bytes, int length, ref int index)
        {
            int sign = 1;
            if (index < length && bytes[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            float value = 0f;
            while (index < length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                value = (value * 10f) + (b - (byte)'0');
                index++;
            }

            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    value += (b - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            return sign * value;
        }
#endif

        private static void DumpOriginShiftBlackBox(NativeArray<AupOriginShiftTelemetryEntry> telemetryRing)
        {
            if (!telemetryRing.IsCreated)
                return;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryRing);
            int entryStride = UnsafeUtility.SizeOf<AupOriginShiftTelemetryEntry>();
            int entryCount = math.min(telemetryRing.Length, TelemetryCapacity);
            int writeCursor = entryCount > 0 ? SystemDispatcher.CurrentFrameIndex % entryCount : 0;
            WriteOriginShiftDump(ResolveDumpPath(), basePtr, entryCount, entryStride, writeCursor);
            WriteOriginShiftDump(ResolveH8DumpPath(), basePtr, entryCount, entryStride, writeCursor);
        }

        private static void WriteOriginShiftDump(string path, byte* basePtr, int entryCount, int entryStride, int writeCursor)
        {
            if (string.IsNullOrEmpty(path) || basePtr == null || entryCount <= 0 || entryStride <= 0)
                return;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, DumpWriteBufferBytes, FileOptions.WriteThrough))
                {
                    int oldestRingIndex = (writeCursor + 1) % entryCount;
                    AupOriginShiftDumpHeader header = CreateDumpHeader(entryCount, entryStride, oldestRingIndex);
                    stream.Write(new ReadOnlySpan<byte>(&header, UnsafeUtility.SizeOf<AupOriginShiftDumpHeader>()));

                    for (int rowIndex = 0; rowIndex < entryCount; rowIndex++)
                    {
                        int ringIndex = (oldestRingIndex + rowIndex) % entryCount;
                        stream.Write(new ReadOnlySpan<byte>(basePtr + (ringIndex * entryStride), entryStride));
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static AupOriginShiftDumpHeader CreateDumpHeader(int entryCount, int entryStride, int oldestRingIndex)
        {
            uint payloadBytes = (uint)math.max(0, entryCount * entryStride);
            return new AupOriginShiftDumpHeader
            {
                Magic = ToLittleEndian(DumpMagic),
                Version = ToLittleEndian(DumpVersion),
                HeaderBytes = ToLittleEndian((uint)UnsafeUtility.SizeOf<AupOriginShiftDumpHeader>()),
                EntryCount = ToLittleEndian((uint)math.max(0, entryCount)),
                EntryStrideBytes = ToLittleEndian((uint)math.max(0, entryStride)),
                PayloadBytes = ToLittleEndian(payloadBytes),
                OldestRingIndex = ToLittleEndian((uint)math.max(0, oldestRingIndex)),
                LatestFrame = ToLittleEndian(Hecton8.Core.SystemDispatcher.CurrentFrameId),
                EndianTag = ToLittleEndian(BitConverter.IsLittleEndian ? DumpEndianLittleTag : DumpEndianBigTag),
                Flags = ToLittleEndian(BitConverter.IsLittleEndian ? 0u : 1u)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ToLittleEndian(uint value)
        {
            return BitConverter.IsLittleEndian ? value : ReverseBytes(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ToLittleEndian(ulong value)
        {
            return BitConverter.IsLittleEndian ? value : ReverseBytes(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                ((value & 0x0000FF00u) << 8) |
                ((value & 0x00FF0000u) >> 8) |
                ((value & 0xFF000000u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReverseBytes(ulong value)
        {
            return ((value & 0x00000000000000FFul) << 56) |
                ((value & 0x000000000000FF00ul) << 40) |
                ((value & 0x0000000000FF0000ul) << 24) |
                ((value & 0x00000000FF000000ul) << 8) |
                ((value & 0x000000FF00000000ul) >> 8) |
                ((value & 0x0000FF0000000000ul) >> 24) |
                ((value & 0x00FF000000000000ul) >> 40) |
                ((value & 0xFF00000000000000ul) >> 56);
        }

        private static string ResolveCsvPath()
        {
            if (!string.IsNullOrEmpty(_csvPath))
                return _csvPath;

#if UNITY_EDITOR
            string dataPath = Application.dataPath;
            _csvPath = string.IsNullOrEmpty(dataPath)
                ? null
                : Path.Combine(dataPath, "_SourceData", "Core", "Origin", "aup_constants.csv");
#else
            _csvPath = null;
#endif
            return _csvPath;
        }

        private static string ResolveDumpPath()
        {
            if (!string.IsNullOrEmpty(_dumpPath))
                return _dumpPath;

            string projectRoot = Directory.GetCurrentDirectory();
            string agentLogs = Path.Combine(projectRoot, "Docs", "AgentLogs");
            if (!Directory.Exists(agentLogs))
            {
                string dataPath = Application.dataPath;
                string dataRoot = !string.IsNullOrEmpty(dataPath) ? Path.GetDirectoryName(dataPath) : null;
                if (!string.IsNullOrEmpty(dataRoot))
                    agentLogs = Path.Combine(dataRoot, "Docs", "AgentLogs");
            }

            _dumpPath = Path.Combine(agentLogs, "Dump_ORIGIN_SHIFT.bin");
            return _dumpPath;
        }

        private static string ResolveH8DumpPath()
        {
            if (!string.IsNullOrEmpty(_h8DumpPath))
                return _h8DumpPath;

            string binaryDumpPath = ResolveDumpPath();
            string directory = !string.IsNullOrEmpty(binaryDumpPath) ? Path.GetDirectoryName(binaryDumpPath) : null;
            if (string.IsNullOrEmpty(directory))
                return string.Empty;

            _h8DumpPath = Path.Combine(directory, "Dump_ORIGIN_SHIFT.h8dump");
            return _h8DumpPath;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeSectorSize(float sectorSize)
        {
            return math.max(math.isfinite(sectorSize) ? sectorSize : DefaultSectorSizeMeters, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveBatchSize(int batchSize)
        {
            return math.clamp(batchSize <= 0 ? DefaultBatchSize : batchSize, MinimumTimeSliceBatchSize, MockEntityCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveQualityScaledBatchSize(int configuredBatchSize, int activeCount, float qualityWeight)
        {
            if (activeCount <= 0)
                return 0;

            int configured = ResolveBatchSize(configuredBatchSize);
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float polynomialQuality = q * q * (3f - (2f * q));
            float promptSliceFloor = math.ceil((float)activeCount * 0.2f);
            float lowTierFloor = math.min((float)configured, math.max((float)MinimumTimeSliceBatchSize, promptSliceFloor));
            float lowTierBatch = math.lerp(lowTierFloor, (float)configured, polynomialQuality);
            float overkillBlend = polynomialQuality * polynomialQuality;
            float desiredBatch = math.lerp(lowTierBatch, (float)activeCount, overkillBlend);
            float activeGate = math.step(1f, (float)activeCount);
            int resolved = (int)math.ceil(desiredBatch * activeGate);
            return math.clamp(resolved, 1, activeCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IncrementMockCameraAup(
            NativeArray<MockCameraAUP> cameraArray,
            NativeArray<AupOriginShiftRuntimeState> runtimeStateArray,
            double3 stepDelta,
            double3 realGlobalPosition,
            double3 totalUniverseOffset,
            int hasRealAnchor)
        {
            AupOriginShiftRuntimeState runtime = runtimeStateArray[0];
            MockCameraAUP camera = cameraArray[0];
            camera.GlobalPosition = hasRealAnchor != 0 ? realGlobalPosition : camera.GlobalPosition + stepDelta;
            double3 localDouble = camera.GlobalPosition - totalUniverseOffset;
            if (!math.all(math.isfinite(localDouble)))
            {
                localDouble = double3.zero;
                runtime.LastNonFiniteCount++;
            }

            camera.LocalPosition = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            if (!math.all(math.isfinite(camera.LocalPosition)))
            {
                camera.LocalPosition = float3.zero;
                runtime.LastNonFiniteCount++;
            }

            camera.SectorHash = ResolveSectorHash(camera.GlobalPosition, SanitizeSectorSize(runtime.SectorSizeMeters));
            cameraArray[0] = camera;
            runtimeStateArray[0] = runtime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MonitorAupThreshold(
            NativeArray<MockCameraAUP> cameraArray,
            NativeArray<AupOriginShiftRuntimeState> runtimeStateArray,
            double3 totalUniverseOffset)
        {
            AupOriginShiftRuntimeState runtime = runtimeStateArray[0];
            MockCameraAUP camera = cameraArray[0];
            double3 local = camera.GlobalPosition - totalUniverseOffset;
            double threshold = math.max((double)runtime.RebaseLimitMeters, 1d);
            double lengthSq = H8DoubleMath.DistanceSq(camera.GlobalPosition, totalUniverseOffset);
            if (!math.all(math.isfinite(local)) || !math.isfinite(lengthSq))
            {
                runtime.LastNonFiniteCount++;
                runtime.IsOriginShiftPending = 0;
            }
            else if (lengthSq > threshold * threshold)
            {
                runtime.IsOriginShiftPending = 1;
            }

            runtimeStateArray[0] = runtime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveSectorHash(double3 absoluteOrigin, float sectorSize)
        {
            int3 sector = ResolveSectorIndex(absoluteOrigin, sectorSize);
            uint hash = 2166136261u;
            hash = (hash ^ unchecked((uint)sector.x)) * 16777619u;
            hash = (hash ^ unchecked((uint)sector.y)) * 16777619u;
            hash = (hash ^ unchecked((uint)sector.z)) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 QuantizeLocalMillimeters(float3 localPosition)
        {
            if (!math.all(math.isfinite(localPosition)))
                return int3.zero;

            return new int3(
                ClampDoubleToInt(math.round((double)localPosition.x * 1000.0d)),
                ClampDoubleToInt(math.round((double)localPosition.y * 1000.0d)),
                ClampDoubleToInt(math.round((double)localPosition.z * 1000.0d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 ResolveSectorIndex(double3 absolutePosition, float sectorSize)
        {
            double invSector = 1.0d / math.max((double)sectorSize, 1.0d);
            return new int3(
                ClampDoubleToInt(math.floor(absolutePosition.x * invSector)),
                ClampDoubleToInt(math.floor(absolutePosition.y * invSector)),
                ClampDoubleToInt(math.floor(absolutePosition.z * invSector)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampDoubleToInt(double value)
        {
            if (!math.isfinite(value))
                return 0;
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AupMockInitializeJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<AUP_StateDTO> States;
            [NoAlias] public NativeArray<float3> Velocities;
            [NoAlias] public NativeArray<float3> HistoricalPoints;
            public float SectorSizeMeters;

            public void Execute(int index)
            {
                double x = (index % 1000) * 4.0d;
                double y = ((index / 1000) % 50) * 2.0d;
                double z = (index / 50000) * 8.0d;
                double3 global = new double3(x, y, z);
                float3 local = new float3((float)x, (float)y, (float)z);
                States[index] = new AUP_StateDTO
                {
                    GlobalPosition = global,
                    LocalPosition = local,
                    SectorHash = ResolveSectorHash(global, SectorSizeMeters),
                    ShiftFrameId = 0u,
                    LocalMillimeters = QuantizeLocalMillimeters(local),
                    FiniteFlags = AupStateFlagFinite,
                    SourceSystemId = unchecked((uint)OwnerSystemId)
                };

                Velocities[index] = new float3(0.25f, 0.05f, -0.1f);
                if (index < HistoricalPoints.Length)
                    HistoricalPoints[index] = local;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AupStateRebaseJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            [NoAlias]
            internal AUP_StateDTO* States;

            [NativeDisableUnsafePtrRestriction]
            [NoAlias]
            internal AupPaddedAtomicCounter* NonFiniteCounter;

            public double3 ShiftDelta;
            public uint NewSectorHash;
            public uint ShiftFrameId;
            public int StartIndex;

            public void Execute(int jobIndex)
            {
                int index = StartIndex + jobIndex;
                ref AUP_StateDTO state = ref UnsafeUtility.AsRef<AUP_StateDTO>(States + index);
                float3 shift = new float3((float)ShiftDelta.x, (float)ShiftDelta.y, (float)ShiftDelta.z);
                float3 local = state.LocalPosition - shift;
                uint finiteFlags = AupStateFlagFinite;
                if (!math.all(math.isfinite(local)) || !math.all(math.isfinite(state.GlobalPosition)))
                {
                    local = float3.zero;
                    finiteFlags = 0u;
                    if (NonFiniteCounter != null)
                        Interlocked.Increment(ref NonFiniteCounter->NonFiniteCount);
                }

                state.LocalPosition = local;
                state.SectorHash = NewSectorHash;
                state.ShiftFrameId = ShiftFrameId;
                state.LocalMillimeters = QuantizeLocalMillimeters(local);
                state.FiniteFlags = finiteFlags;
                state.SourceSystemId = unchecked((uint)OwnerSystemId);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct VaultHotEntityRebaseJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<VaultHotEntityData> HotEntities;
            public float3 ShiftDelta;
            public uint ShiftFrameId;
            public int StartIndex;

            public void Execute(int index)
            {
                int entityIndex = StartIndex + index;
                VaultHotEntityData hot = HotEntities[entityIndex];
                float3 local = hot.LocalPosition - ShiftDelta;
                if (!math.all(math.isfinite(local)))
                    local = float3.zero;

                hot.LocalPosition = local;
                hot.ShiftFrameId = ShiftFrameId;
                HotEntities[entityIndex] = hot;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct Float3HistoricalRebaseJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<float3> Points;
            public float3 ShiftDelta;
            public int StartIndex;

            public void Execute(int index)
            {
                int pointIndex = StartIndex + index;
                float3 point = Points[pointIndex] - ShiftDelta;
                Points[pointIndex] = math.all(math.isfinite(point)) ? point : float3.zero;
            }
        }
    }
}
