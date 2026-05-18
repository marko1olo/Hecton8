using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct PhysicsCullingDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public int InstanceId;
        [FieldOffset(28)] public float ActivationRadiusSq;
        [FieldOffset(32)] public byte IsAsleep;
        [FieldOffset(33)] public byte _pad0;
        [FieldOffset(34)] public byte _pad1;
        [FieldOffset(35)] public byte _pad2;
        [FieldOffset(36)] public uint _pad3;
        [FieldOffset(36)] public uint CullingFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FrozenVelocityDTO
    {
        [FieldOffset(0)] public float3 LinearVelocity;
        [FieldOffset(12)] public float3 AngularVelocity;
        [FieldOffset(24)] public byte HasVelocity;
        [FieldOffset(25)] public byte _pad0;
        [FieldOffset(26)] public byte _pad1;
        [FieldOffset(27)] public byte _pad2;
        [FieldOffset(28)] public uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PhysicsCullingTargetWakeRequestSignal
    {
        [FieldOffset(0)] public uint TargetInstanceId;
        [FieldOffset(4)] public float3 ImpulseVector;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public partial struct MockSeismicShockwaveSignal
    {
        [FieldOffset(0)] public double3 EpicenterAup;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public uint Seed;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte Fire;
        [FieldOffset(37)] public byte _pad0;
        [FieldOffset(38)] public byte _pad1;
        [FieldOffset(39)] public byte _pad2;
        [FieldOffset(40)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PhysicsCullingFrameTelemetry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public int TotalTrackedBodies;
        [FieldOffset(8)] public int ActiveBodies;
        [FieldOffset(12)] public int AsleepBodies;
        [FieldOffset(16)] public float StateSyncTimeMs;
        [FieldOffset(20)] public int ChangedIndices;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PhysicsCullingTuningDTO
    {
        [FieldOffset(0)] public float DebrisWakeRadiusMeters;
        [FieldOffset(4)] public float VehicleWakeRadiusMeters;
        [FieldOffset(8)] public float FrustumClampDistanceMeters;
        [FieldOffset(12)] public float HysteresisDelaySeconds;
        [FieldOffset(16)] public float SpatialCellSizeMeters;
        [FieldOffset(20)] public float MockShockwaveRadiusMeters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct PhysicsCullingDebugBody
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float AgeSeconds;
        [FieldOffset(28)] public int InstanceId;
        [FieldOffset(32)] public byte IsAsleep;
        [FieldOffset(33)] public byte IsHysteresisLocked;
        [FieldOffset(34)] public byte _pad0;
        [FieldOffset(35)] public byte _pad1;
        [FieldOffset(36)] public uint _pad2;
    }

    public sealed partial class GlobalPhysicsStateManager
    {
        private const int PhysicsCullingFrameTelemetryCapacity = 300;
        private const int PhysicsCullingTargetWakeQueueCapacity = 64;
        private const int PhysicsCullingMockSeismicSignalCapacity = 4;
        private const int PhysicsCullingCsvScratchCapacity = 4096;
        private const int PhysicsCullingLegacyRadiiHeaderBytes = 64;
        private const int PhysicsCullingMaxCandidateCells = 9;
        private const int PhysicsCullingMockBodiesPerGenerate = 1000;
        private const float PhysicsCullingDefaultDebrisWakeRadiusMeters = 50f;
        private const float PhysicsCullingDefaultVehicleWakeRadiusMeters = 200f;
        private const float PhysicsCullingDefaultFrustumClampDistanceMeters = 150f;
        private const float PhysicsCullingDefaultHysteresisSeconds = 2f;
        private const float PhysicsCullingStateSyncDumpThresholdMs = 1f;
        private const float PhysicsCullingSpatialCellSizeMeters = 50f;
        private const float PhysicsCullingInvSpatialCellSizeMeters = 1f / PhysicsCullingSpatialCellSizeMeters;
        private const float PhysicsCullingSpatialHashRebuildIntervalSeconds = 1f;
        private const float PhysicsCullingCsvPollIntervalSeconds = 1f;
        private const float PhysicsCullingFrustumInnerSphereRadiusMeters = 20f;
        private const float PhysicsCullingWakeRadiusSqScale = 0.81f;
        private const uint PhysicsCullingDtoExemptFlag = 1u;
        private const string PhysicsCullingNativeOwner = nameof(GlobalPhysicsStateManager);
        private const string PhysicsCullingProfilesRelativePath = "Docs/Modding/physics_culling_profiles.csv";

        private VaultBufferBinding<PhysicsCullingDTO> _physicsCullingDtos =
            new VaultBufferBinding<PhysicsCullingDTO>(BufferID.ShinobuPhysicsCullingDtos, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<FrozenVelocityDTO> _physicsFrozenVelocities =
            new VaultBufferBinding<FrozenVelocityDTO>(BufferID.ShinobuPhysicsCullingFrozenVelocities, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<float> _physicsCullingStateAges =
            new VaultBufferBinding<float>(BufferID.ShinobuPhysicsCullingStateAges, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<int> _physicsCullingSpatialCandidates =
            new VaultBufferBinding<int>(BufferID.ShinobuPhysicsCullingSpatialCandidates, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _physicsCullingSpatialCandidateMask =
            new VaultBufferBinding<byte>(BufferID.ShinobuPhysicsCullingSpatialCandidateMask, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingFrameTelemetry> _physicsCullingFrameTelemetry =
            new VaultBufferBinding<PhysicsCullingFrameTelemetry>(BufferID.ShinobuPhysicsCullingFrameTelemetry, PhysicsCullingFrameTelemetryCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTuningDTO> _physicsCullingTuning =
            new VaultBufferBinding<PhysicsCullingTuningDTO>(BufferID.ShinobuPhysicsCullingTuning, 1, OwnerSystemId);
        private VaultBufferBinding<MockSeismicShockwaveSignal> _physicsMockSeismicSignals =
            new VaultBufferBinding<MockSeismicShockwaveSignal>(BufferID.ShinobuPhysicsCullingMockSeismicSignals, PhysicsCullingMockSeismicSignalCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTargetWakeRequestSignal> _physicsWakeRequestMirror =
            new VaultBufferBinding<PhysicsCullingTargetWakeRequestSignal>(BufferID.ShinobuPhysicsCullingWakeRequestMirror, PhysicsCullingTargetWakeQueueCapacity, OwnerSystemId);

        private NativeParallelMultiHashMap<int, int> _physicsSpatialHash;
        private NativeQueue<int> _physicsStateChangedIndices;
        private NativeQueue<PhysicsCullingTargetWakeRequestSignal> _physicsTargetWakeRequests;
        private readonly Plane[] _physicsFrustumPlaneScratch = new Plane[6];
        private readonly byte[] _physicsCullingCsvScratch = new byte[PhysicsCullingCsvScratchCapacity];
        private readonly byte[] _physicsCullingLegacyRadiiScratch = new byte[PhysicsCullingLegacyRadiiHeaderBytes];
        private readonly int3[] _physicsCullingNeighborCells = new int3[PhysicsCullingMaxCandidateCells];
        private int _physicsCullingFrameTelemetryWriteIndex;
        private int _physicsCullingMockBodyCount;
        private int _physicsSpatialHashLastCount = -1;
        private long _physicsCullingCsvLastWriteTicks;
        private string _physicsCullingCsvAbsolutePath;
        private float _physicsSpatialHashRebuildAccumulator;
        private float _physicsCullingCsvPollAccumulator;
        private bool _physicsCullingTuningInitialized;
        private bool _physicsSpatialHashDirty = true;

        private void EnsureShinobu37PhysicsCullingState()
        {
            if (!_physicsCullingDtos.IsCreated)
                _physicsCullingDtos.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsFrozenVelocities.IsCreated)
                _physicsFrozenVelocities.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingStateAges.IsCreated)
                _physicsCullingStateAges.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingSpatialCandidates.IsCreated)
                _physicsCullingSpatialCandidates.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingSpatialCandidateMask.IsCreated)
                _physicsCullingSpatialCandidateMask.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingFrameTelemetry.IsCreated)
                _physicsCullingFrameTelemetry.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsCullingTuning.IsCreated)
                _physicsCullingTuning.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsMockSeismicSignals.IsCreated)
                _physicsMockSeismicSignals.Ensure(NativeArrayOptions.UninitializedMemory);
            if (!_physicsWakeRequestMirror.IsCreated)
                _physicsWakeRequestMirror.Ensure(NativeArrayOptions.UninitializedMemory);

            if (!_physicsSpatialHash.IsCreated)
            {
                _physicsSpatialHash = new NativeParallelMultiHashMap<int, int>(MaxTrackedBodies, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeParallelMultiHashMap(
                    _physicsSpatialHash,
                    PhysicsCullingNativeOwner,
                    nameof(_physicsSpatialHash),
                    NativeAllocationLifetime.Session);
            }

            if (!_physicsStateChangedIndices.IsCreated)
            {
                _physicsStateChangedIndices = new NativeQueue<int>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _physicsStateChangedIndices,
                    MaxTrackedBodies,
                    PhysicsCullingNativeOwner,
                    nameof(_physicsStateChangedIndices),
                    NativeAllocationLifetime.Session);
            }

            if (!_physicsTargetWakeRequests.IsCreated)
            {
                _physicsTargetWakeRequests = new NativeQueue<PhysicsCullingTargetWakeRequestSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _physicsTargetWakeRequests,
                    PhysicsCullingTargetWakeQueueCapacity,
                    PhysicsCullingNativeOwner,
                    nameof(_physicsTargetWakeRequests),
                    NativeAllocationLifetime.Session);
            }

            InitializePhysicsCullingTuningIfNeeded();
        }

        private bool HasUndersizedShinobu37PhysicsCullingState()
        {
            return (_physicsCullingDtos.IsCreated && _physicsCullingDtos.Length < MaxTrackedBodies) ||
                (_physicsFrozenVelocities.IsCreated && _physicsFrozenVelocities.Length < MaxTrackedBodies) ||
                (_physicsCullingStateAges.IsCreated && _physicsCullingStateAges.Length < MaxTrackedBodies) ||
                (_physicsCullingSpatialCandidates.IsCreated && _physicsCullingSpatialCandidates.Length < MaxTrackedBodies) ||
                (_physicsCullingSpatialCandidateMask.IsCreated && _physicsCullingSpatialCandidateMask.Length < MaxTrackedBodies) ||
                (_physicsCullingFrameTelemetry.IsCreated && _physicsCullingFrameTelemetry.Length < PhysicsCullingFrameTelemetryCapacity) ||
                (_physicsCullingTuning.IsCreated && _physicsCullingTuning.Length < 1) ||
                (_physicsMockSeismicSignals.IsCreated && _physicsMockSeismicSignals.Length < PhysicsCullingMockSeismicSignalCapacity) ||
                (_physicsWakeRequestMirror.IsCreated && _physicsWakeRequestMirror.Length < PhysicsCullingTargetWakeQueueCapacity);
        }

        private void ReleaseUndersizedShinobu37PhysicsCullingState()
        {
            ReleaseUndersizedVaultBuffer(ref _physicsCullingDtos, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsFrozenVelocities, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingStateAges, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingSpatialCandidates, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingSpatialCandidateMask, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingFrameTelemetry, PhysicsCullingFrameTelemetryCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingTuning, 1);
            ReleaseUndersizedVaultBuffer(ref _physicsMockSeismicSignals, PhysicsCullingMockSeismicSignalCapacity);
            ReleaseUndersizedVaultBuffer(ref _physicsWakeRequestMirror, PhysicsCullingTargetWakeQueueCapacity);
            _physicsCullingTuningInitialized = false;
        }

        private bool HasRequiredShinobu37PhysicsCullingState()
        {
            return _physicsCullingDtos.IsCreated &&
                _physicsCullingDtos.Length >= MaxTrackedBodies &&
                _physicsFrozenVelocities.IsCreated &&
                _physicsFrozenVelocities.Length >= MaxTrackedBodies &&
                _physicsCullingStateAges.IsCreated &&
                _physicsCullingStateAges.Length >= MaxTrackedBodies &&
                _physicsCullingSpatialCandidates.IsCreated &&
                _physicsCullingSpatialCandidates.Length >= MaxTrackedBodies &&
                _physicsCullingSpatialCandidateMask.IsCreated &&
                _physicsCullingSpatialCandidateMask.Length >= MaxTrackedBodies &&
                _physicsCullingFrameTelemetry.IsCreated &&
                _physicsCullingFrameTelemetry.Length >= PhysicsCullingFrameTelemetryCapacity &&
                _physicsCullingTuning.IsCreated &&
                _physicsCullingTuning.Length >= 1 &&
                _physicsMockSeismicSignals.IsCreated &&
                _physicsMockSeismicSignals.Length >= PhysicsCullingMockSeismicSignalCapacity &&
                _physicsWakeRequestMirror.IsCreated &&
                _physicsWakeRequestMirror.Length >= PhysicsCullingTargetWakeQueueCapacity &&
                _physicsSpatialHash.IsCreated &&
                _physicsStateChangedIndices.IsCreated &&
                _physicsTargetWakeRequests.IsCreated;
        }

        private void ReleaseShinobu37PhysicsCullingState()
        {
            if (_physicsSpatialHash.IsCreated)
            {
                _physicsSpatialHash.Dispose();
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(PhysicsCullingNativeOwner, nameof(_physicsSpatialHash));
            }

            if (_physicsStateChangedIndices.IsCreated)
            {
                _physicsStateChangedIndices.Dispose();
                NativeMemorySentinel.UnregisterNativeQueue(PhysicsCullingNativeOwner, nameof(_physicsStateChangedIndices));
            }

            if (_physicsTargetWakeRequests.IsCreated)
            {
                _physicsTargetWakeRequests.Dispose();
                NativeMemorySentinel.UnregisterNativeQueue(PhysicsCullingNativeOwner, nameof(_physicsTargetWakeRequests));
            }

            _physicsCullingDtos.ReleaseView();
            _physicsFrozenVelocities.ReleaseView();
            _physicsCullingStateAges.ReleaseView();
            _physicsCullingSpatialCandidates.ReleaseView();
            _physicsCullingSpatialCandidateMask.ReleaseView();
            _physicsCullingFrameTelemetry.ReleaseView();
            _physicsCullingTuning.ReleaseView();
            _physicsMockSeismicSignals.ReleaseView();
            _physicsWakeRequestMirror.ReleaseView();
            _physicsCullingTuningInitialized = false;
        }

        private void InitializePhysicsCullingTuningIfNeeded()
        {
            if (_physicsCullingTuningInitialized || !_physicsCullingTuning.IsCreated)
                return;

            if (!TryLoadLegacyPhysicsCullingRadii(out PhysicsCullingTuningDTO tuning))
                tuning = GenerateEmergencyMockRadii();
            else
                _physicsCullingTuning[0] = tuning;

            _physicsCullingTuningInitialized = true;
        }

        private PhysicsCullingTuningDTO GenerateEmergencyMockRadii()
        {
            PhysicsCullingTuningDTO tuning = new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = PhysicsCullingDefaultDebrisWakeRadiusMeters,
                VehicleWakeRadiusMeters = PhysicsCullingDefaultVehicleWakeRadiusMeters,
                FrustumClampDistanceMeters = PhysicsCullingDefaultFrustumClampDistanceMeters,
                HysteresisDelaySeconds = PhysicsCullingDefaultHysteresisSeconds,
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = ImpactWakeMaximumRadiusMeters,
                Flags = 1u
            };

            if (_physicsCullingTuning.IsCreated)
                _physicsCullingTuning[0] = tuning;

            return tuning;
        }

        private bool TryLoadLegacyPhysicsCullingRadii(out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (TryLoadLegacyPhysicsCullingRadiiFromRoot(Path.Combine(projectRoot, "Docs", "Archive"), out tuning))
                return true;

            string streamingAssetsPath = Application.streamingAssetsPath;
            return !string.IsNullOrEmpty(streamingAssetsPath) &&
                TryLoadLegacyPhysicsCullingRadiiFromRoot(streamingAssetsPath, out tuning);
        }

        private bool TryLoadLegacyPhysicsCullingRadiiFromRoot(string root, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return false;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "physics_culling_radii.h8bin", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return false;
            }

            for (int i = 0; i < files.Length; i++)
            {
                if (TryReadLegacyPhysicsCullingRadiiHeader(files[i], out tuning))
                    return true;
            }

            return false;
        }

        private bool TryReadLegacyPhysicsCullingRadiiHeader(string path, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                int bytesRead;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, PhysicsCullingLegacyRadiiHeaderBytes, FileOptions.SequentialScan))
                {
                    bytesRead = stream.Read(_physicsCullingLegacyRadiiScratch, 0, _physicsCullingLegacyRadiiScratch.Length);
                }

                return TryParseLegacyPhysicsCullingRadiiHeader(_physicsCullingLegacyRadiiScratch, bytesRead, out tuning);
            }
            catch (Exception)
            {
                tuning = default;
                return false;
            }
        }

        private static bool TryParseLegacyPhysicsCullingRadiiHeader(byte[] bytes, int bytesRead, out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (bytes == null || bytesRead < 16)
                return false;

            if (bytesRead >= 28 &&
                TryBuildPhysicsCullingTuningFromLegacyFloats(
                    ReadSingleLittleEndian(bytes, 8),
                    ReadSingleLittleEndian(bytes, 12),
                    ReadSingleLittleEndian(bytes, 16),
                    ReadSingleLittleEndian(bytes, 20),
                    ReadSingleLittleEndian(bytes, 24),
                    out tuning))
            {
                return true;
            }

            return TryBuildPhysicsCullingTuningFromLegacyFloats(
                ReadSingleLittleEndian(bytes, 0),
                ReadSingleLittleEndian(bytes, 4),
                ReadSingleLittleEndian(bytes, 8),
                ReadSingleLittleEndian(bytes, 12),
                ImpactWakeMaximumRadiusMeters,
                out tuning);
        }

        private static bool TryBuildPhysicsCullingTuningFromLegacyFloats(
            float debrisRadiusMeters,
            float vehicleRadiusMeters,
            float frustumClampDistanceMeters,
            float hysteresisDelaySeconds,
            float mockShockwaveRadiusMeters,
            out PhysicsCullingTuningDTO tuning)
        {
            tuning = default;
            if (!math.isfinite(debrisRadiusMeters) ||
                !math.isfinite(vehicleRadiusMeters) ||
                !math.isfinite(frustumClampDistanceMeters) ||
                !math.isfinite(hysteresisDelaySeconds) ||
                debrisRadiusMeters <= 0f ||
                vehicleRadiusMeters <= 0f ||
                frustumClampDistanceMeters <= 0f ||
                hysteresisDelaySeconds <= 0f)
            {
                return false;
            }

            tuning = new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = math.clamp(debrisRadiusMeters, 1f, 500f),
                VehicleWakeRadiusMeters = math.clamp(vehicleRadiusMeters, 1f, 1000f),
                FrustumClampDistanceMeters = math.clamp(frustumClampDistanceMeters, 20f, 1000f),
                HysteresisDelaySeconds = math.clamp(hysteresisDelaySeconds, 0.1f, 10f),
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = math.isfinite(mockShockwaveRadiusMeters)
                    ? math.clamp(mockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters)
                    : ImpactWakeMaximumRadiusMeters,
                Flags = 2u
            };
            return true;
        }

        private static float ReadSingleLittleEndian(byte[] bytes, int offset)
        {
            if (bytes == null || offset < 0 || offset + 4 > bytes.Length)
                return float.NaN;

            uint bits =
                (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
            return math.asfloat(bits);
        }

        private void ClearShinobu37PhysicsCullingState()
        {
            if (_physicsSpatialHash.IsCreated)
                _physicsSpatialHash.Clear();
            ClearPhysicsStateChangedQueue();
            if (_physicsTargetWakeRequests.IsCreated)
                _physicsTargetWakeRequests.Clear();

            int clearCount = math.min(MaxTrackedBodies, _physicsCullingDtos.IsCreated ? _physicsCullingDtos.Length : 0);
            for (int i = 0; i < clearCount; i++)
            {
                _physicsCullingDtos[i] = default;
                _physicsFrozenVelocities[i] = default;
                _physicsCullingStateAges[i] = default;
                _physicsCullingSpatialCandidates[i] = default;
                _physicsCullingSpatialCandidateMask[i] = default;
            }

            if (_physicsCullingFrameTelemetry.IsCreated)
            {
                int telemetryCount = math.min(_physicsCullingFrameTelemetry.Length, PhysicsCullingFrameTelemetryCapacity);
                for (int i = 0; i < telemetryCount; i++)
                    _physicsCullingFrameTelemetry[i] = default;
            }

            if (_physicsMockSeismicSignals.IsCreated)
            {
                int signalCount = math.min(_physicsMockSeismicSignals.Length, PhysicsCullingMockSeismicSignalCapacity);
                for (int i = 0; i < signalCount; i++)
                    _physicsMockSeismicSignals[i] = default;
            }

            _physicsCullingFrameTelemetryWriteIndex = 0;
            _physicsCullingMockBodyCount = 0;
            _physicsSpatialHashLastCount = -1;
            _physicsSpatialHashRebuildAccumulator = 0f;
            _physicsCullingCsvPollAccumulator = 0f;
            _physicsSpatialHashDirty = true;
        }

        private void InitializePhysicsCullingDtoForBody(int bodyIndex, Rigidbody body, in RigidbodyState bodyState)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            AbsoluteUniversePosition bodyAup = bodyState.HasLastValidAup
                ? bodyState.LastValidAup
                : body != null && IsFinite(body.position)
                    ? AbsoluteUniversePosition.FromRuntimePosition(body.position)
                    : default;
            WritePhysicsCullingDto(bodyIndex, body, in bodyState, in bodyAup);
            _physicsFrozenVelocities[bodyIndex] = default;
            _physicsCullingStateAges[bodyIndex] = ResolvePhysicsCullingHysteresisSeconds();
            MarkPhysicsCullingSpatialHashDirty();
        }

        private void WritePhysicsCullingDto(int bodyIndex, Rigidbody body, in RigidbodyState bodyState, in AbsoluteUniversePosition bodyAup)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            uint flags = (uint)bodyState.CullingFlags;
            if ((bodyState.CullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
                flags |= PhysicsCullingDtoExemptFlag;

            _physicsCullingDtos[bodyIndex] = new PhysicsCullingDTO
            {
                AUP = bodyAup.ToAbsoluteDouble3(),
                InstanceId = body != null ? body.GetInstanceID() : 0,
                ActivationRadiusSq = ResolvePhysicsCullingActivationRadiusSq(body, in bodyState),
                IsAsleep = bodyState.DistanceSleepActive ? (byte)1 : (byte)0,
                CullingFlags = flags
            };
        }

        private void MarkPhysicsCullingSpatialHashDirty()
        {
            _physicsSpatialHashDirty = true;
        }

        private void WritePhysicsCullingDtoSleepState(int bodyIndex, byte isAsleep)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsCullingDtos.IsCreated)
                return;

            PhysicsCullingDTO dto = _physicsCullingDtos[bodyIndex];
            dto.IsAsleep = isAsleep;
            _physicsCullingDtos[bodyIndex] = dto;
            if (_physicsCullingStateAges.IsCreated)
                _physicsCullingStateAges[bodyIndex] = 0f;
        }

        private float ResolvePhysicsCullingActivationRadiusSq(Rigidbody body, in RigidbodyState bodyState)
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            float radiusMeters = tuning.DebrisWakeRadiusMeters;
            if (body != null && (body.mass >= 250f || (bodyState.CullingFlags & PhysicsCullingFlags.HeavyCollider) != 0))
                radiusMeters = tuning.VehicleWakeRadiusMeters;

            radiusMeters = math.max(1f, radiusMeters);
            return radiusMeters * radiusMeters;
        }

        private PhysicsCullingTuningDTO ResolvePhysicsCullingTuning()
        {
            InitializePhysicsCullingTuningIfNeeded();
            return _physicsCullingTuning.IsCreated ? _physicsCullingTuning[0] : new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = PhysicsCullingDefaultDebrisWakeRadiusMeters,
                VehicleWakeRadiusMeters = PhysicsCullingDefaultVehicleWakeRadiusMeters,
                FrustumClampDistanceMeters = PhysicsCullingDefaultFrustumClampDistanceMeters,
                HysteresisDelaySeconds = PhysicsCullingDefaultHysteresisSeconds,
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = ImpactWakeMaximumRadiusMeters,
                Flags = 1u
            };
        }

        private int BuildPhysicsCullingSpatialCandidates(in AbsoluteUniversePosition cameraAup, int jobCount)
        {
            if (!_physicsSpatialHash.IsCreated || !_physicsCullingDtos.IsCreated)
                return 0;

            int count = math.min(jobCount, math.min(_physicsCullingDtos.Length, MaxTrackedBodies));
            for (int i = 0; i < count; i++)
                _physicsCullingSpatialCandidateMask[i] = 0;

            RefreshPhysicsCullingSpatialHashIfNeeded(count);

            double3 cameraAbsolute = cameraAup.ToAbsoluteDouble3();
            int3 cameraCell = ResolvePhysicsCullingCell(cameraAbsolute);
            BuildPhysicsCullingNeighborCells(cameraCell);

            int candidateCount = 0;
            for (int i = 0; i < PhysicsCullingMaxCandidateCells; i++)
            {
                int cellHash = HashPhysicsCullingCell(_physicsCullingNeighborCells[i]);
                if (!_physicsSpatialHash.TryGetFirstValue(cellHash, out int bodyIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                    continue;

                do
                {
                    TryAddPhysicsCullingCandidate(bodyIndex, ref candidateCount);
                }
                while (_physicsSpatialHash.TryGetNextValue(out bodyIndex, ref iterator));
            }

            for (int i = 0; i < count; i++)
            {
                PhysicsCullingDTO dto = _physicsCullingDtos[i];
                byte currentState = i < _rigidbodyCullingStateSnapshot.Length ? _rigidbodyCullingStateSnapshot[i] : (byte)0;
                if (dto.IsAsleep == 0 || (currentState & CullingStateSleepActive) == 0)
                    TryAddPhysicsCullingCandidate(i, ref candidateCount);
            }

            return candidateCount;
        }

        private void RefreshPhysicsCullingSpatialHashIfNeeded(int count)
        {
            _physicsSpatialHashRebuildAccumulator += PhysicsCullingSlowTickIntervalSeconds;
            if (!_physicsSpatialHashDirty &&
                _physicsSpatialHashLastCount == count &&
                _physicsSpatialHashRebuildAccumulator < PhysicsCullingSpatialHashRebuildIntervalSeconds)
            {
                return;
            }

            RebuildPhysicsCullingSpatialHash(count);
            _physicsSpatialHashDirty = false;
            _physicsSpatialHashLastCount = count;
            _physicsSpatialHashRebuildAccumulator = 0f;
        }

        private void RebuildPhysicsCullingSpatialHash(int count)
        {
            _physicsSpatialHash.Clear();
            int safeCount = math.min(count, math.min(_physicsCullingDtos.Length, MaxTrackedBodies));
            for (int i = 0; i < safeCount; i++)
            {
                PhysicsCullingDTO dto = _physicsCullingDtos[i];
                if (dto.InstanceId == 0 && i >= _trackedBodyCount)
                    continue;

                _physicsSpatialHash.Add(HashPhysicsCullingCell(ResolvePhysicsCullingCell(dto.AUP)), i);
            }
        }

        private void TryAddPhysicsCullingCandidate(int bodyIndex, ref int candidateCount)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies ||
                candidateCount >= MaxTrackedBodies ||
                _physicsCullingSpatialCandidateMask[bodyIndex] != 0)
            {
                return;
            }

            _physicsCullingSpatialCandidateMask[bodyIndex] = 1;
            _physicsCullingSpatialCandidates[candidateCount] = bodyIndex;
            candidateCount++;
        }

        private void BuildPhysicsCullingNeighborCells(int3 cameraCell)
        {
            int write = 0;
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    _physicsCullingNeighborCells[write++] = new int3(cameraCell.x + x, cameraCell.y, cameraCell.z + z);
                }
            }
        }

        private static int3 ResolvePhysicsCullingCell(double3 absoluteAup)
        {
            return new int3(
                (int)math.floor(absoluteAup.x * PhysicsCullingInvSpatialCellSizeMeters),
                (int)math.floor(absoluteAup.y * PhysicsCullingInvSpatialCellSizeMeters),
                (int)math.floor(absoluteAup.z * PhysicsCullingInvSpatialCellSizeMeters));
        }

        private static int HashPhysicsCullingCell(int3 cell)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 73856093) ^ cell.x;
                hash = (hash * 19349663) ^ cell.y;
                hash = (hash * 83492791) ^ cell.z;
                return hash;
            }
        }

        private void ClearPhysicsStateChangedQueue()
        {
            if (_physicsStateChangedIndices.IsCreated)
                _physicsStateChangedIndices.Clear();
        }

        private AbsoluteUniversePosition ResolvePhysicsCullingCameraAup(in AbsoluteUniversePosition playerAup, ref float3 cameraForward)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera camera = playerContext != null ? playerContext.PlayerCamera : null;
            if (camera == null)
                return playerAup;

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            if (IsFinite(forward))
                cameraForward = NormalizeWithRsqrtGuard(new float3(forward.x, forward.y, forward.z), cameraForward);

            return IsFinite(position) ? AbsoluteUniversePosition.FromRuntimePosition(position) : playerAup;
        }

        private bool TryResolvePhysicsCullingFrustumPlanes(
            in AbsoluteUniversePosition cameraAup,
            out float4 plane0,
            out float4 plane1,
            out float4 plane2,
            out float4 plane3,
            out float4 plane4,
            out float4 plane5)
        {
            plane0 = plane1 = plane2 = plane3 = plane4 = plane5 = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Camera camera = playerContext != null ? playerContext.PlayerCamera : null;
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, _physicsFrustumPlaneScratch);
            Vector3 cameraPosition = camera.transform.position;
            if (!IsFinite(cameraPosition))
                return false;

            plane0 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[0], cameraPosition);
            plane1 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[1], cameraPosition);
            plane2 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[2], cameraPosition);
            plane3 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[3], cameraPosition);
            plane4 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[4], cameraPosition);
            plane5 = ConvertPlaneToCameraRelative(_physicsFrustumPlaneScratch[5], cameraPosition);
            return math.all(math.isfinite(plane0)) &&
                math.all(math.isfinite(plane1)) &&
                math.all(math.isfinite(plane2)) &&
                math.all(math.isfinite(plane3)) &&
                math.all(math.isfinite(plane4)) &&
                math.all(math.isfinite(plane5));
        }

        private static float4 ConvertPlaneToCameraRelative(Plane plane, Vector3 cameraPosition)
        {
            Vector3 normal = plane.normal;
            float localDistance = plane.distance + Vector3.Dot(normal, cameraPosition);
            return new float4(normal.x, normal.y, normal.z, localDistance);
        }

        private static float ResolvePhysicsCullingHardwareRadiusSqScale()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350)
                return 0.25f;
            if (tier == HectonQualityTier.High)
                return 1.44f;
            if (tier == HectonQualityTier.Ultra)
                return 2.25f;
            return 1f;
        }

        private float ResolvePhysicsCullingHysteresisSeconds()
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            return math.max(0.1f, tuning.HysteresisDelaySeconds);
        }

        private float ResolvePhysicsCullingFrustumInnerSphereSq()
        {
            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            float radius = math.max(PhysicsCullingFrustumInnerSphereRadiusMeters, tuning.FrustumClampDistanceMeters * 0.1f);
            return radius * radius;
        }

        private int CountCulledTrackedBodies(out int activeBodies, out int asleepBodies)
        {
            int culled = 0;
            int active = 0;
            int asleep = 0;
            for (int i = 0; i < _trackedBodyCount; i++)
            {
                RigidbodyState bodyState = _bodyStates[i];
                bool isCulled = bodyState.DistanceSleepActive ||
                    bodyState.DistanceKinematicSleepActive ||
                    bodyState.MeshColliderStripActive;
                if (isCulled)
                    culled++;
                if (bodyState.DistanceSleepActive)
                    asleep++;
                else
                    active++;
            }

            activeBodies = active;
            asleepBodies = asleep;
            return culled;
        }

        private void FreezeBodyVelocityForDistanceSleep(int bodyIndex, Rigidbody body)
        {
            if (body == null || body.isKinematic || (uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsFrozenVelocities.IsCreated)
                return;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            if (frozen.HasVelocity == 0)
            {
                Vector3 linearVelocity = body.linearVelocity;
                Vector3 angularVelocity = body.angularVelocity;
                frozen.LinearVelocity = IsFinite(linearVelocity) ? new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z) : float3.zero;
                frozen.AngularVelocity = IsFinite(angularVelocity) ? new float3(angularVelocity.x, angularVelocity.y, angularVelocity.z) : float3.zero;
                frozen.HasVelocity = 1;
                _physicsFrozenVelocities[bodyIndex] = frozen;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void RestoreFrozenVelocityForDistanceSleep(int bodyIndex, Rigidbody body)
        {
            if (body == null || body.isKinematic || (uint)bodyIndex >= (uint)MaxTrackedBodies || !_physicsFrozenVelocities.IsCreated)
                return;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            if (frozen.HasVelocity == 0)
                return;

            if (math.all(math.isfinite(frozen.LinearVelocity)))
                body.linearVelocity = new Vector3(frozen.LinearVelocity.x, frozen.LinearVelocity.y, frozen.LinearVelocity.z);
            if (math.all(math.isfinite(frozen.AngularVelocity)))
                body.angularVelocity = new Vector3(frozen.AngularVelocity.x, frozen.AngularVelocity.y, frozen.AngularVelocity.z);

            _physicsFrozenVelocities[bodyIndex] = default;
        }

        private byte CacheSleepCollidersForBody(Rigidbody body, int bodyIndex)
        {
            if (body == null || (uint)bodyIndex >= (uint)MaxTrackedBodies)
                return 0;

            _sleepColliderScratch.Clear();
            body.GetComponentsInChildren(false, _sleepColliderScratch);
            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            int count = math.min(_sleepColliderScratch.Count, MaxSleepCollidersPerBody);
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                int slot = baseIndex + i;
                _trackedSleepColliders[slot] = i < count ? _sleepColliderScratch[i] : null;
                _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
            }

            _sleepColliderScratch.Clear();
            return (byte)count;
        }

        private void DisableSleepColliders(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.SleepColliderCount == 0 || bodyState.CollidersDisabledByDistanceSleep)
                return;

            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            int count = math.min((int)bodyState.SleepColliderCount, MaxSleepCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int slot = baseIndex + i;
                Collider collider = _trackedSleepColliders[slot];
                if (collider == null)
                {
                    _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
                    continue;
                }

                _trackedSleepColliderEnabledBeforeSleep[slot] = collider.enabled ? (byte)1 : (byte)0;
                collider.enabled = false;
            }

            bodyState.CollidersDisabledByDistanceSleep = true;
        }

        private void RestoreSleepColliders(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (!bodyState.CollidersDisabledByDistanceSleep)
                return;

            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            int count = math.min((int)bodyState.SleepColliderCount, MaxSleepCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int slot = baseIndex + i;
                Collider collider = _trackedSleepColliders[slot];
                if (collider != null)
                    collider.enabled = _trackedSleepColliderEnabledBeforeSleep[slot] != 0;
                _trackedSleepColliderEnabledBeforeSleep[slot] = 0;
            }

            bodyState.CollidersDisabledByDistanceSleep = false;
        }

        private void MoveSleepColliderRefs(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
                return;

            int fromBase = fromIndex * MaxSleepCollidersPerBody;
            int toBase = toIndex * MaxSleepCollidersPerBody;
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                _trackedSleepColliders[toBase + i] = _trackedSleepColliders[fromBase + i];
                _trackedSleepColliderEnabledBeforeSleep[toBase + i] = _trackedSleepColliderEnabledBeforeSleep[fromBase + i];
            }
        }

        private void ClearSleepColliderRefs(int bodyIndex)
        {
            int baseIndex = bodyIndex * MaxSleepCollidersPerBody;
            for (int i = 0; i < MaxSleepCollidersPerBody; i++)
            {
                _trackedSleepColliders[baseIndex + i] = null;
                _trackedSleepColliderEnabledBeforeSleep[baseIndex + i] = 0;
            }
        }

        private void MovePhysicsCullingDtoLane(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || !_physicsCullingDtos.IsCreated)
                return;

            _physicsCullingDtos[toIndex] = _physicsCullingDtos[fromIndex];
            _physicsFrozenVelocities[toIndex] = _physicsFrozenVelocities[fromIndex];
            _physicsCullingStateAges[toIndex] = _physicsCullingStateAges[fromIndex];
            _physicsCullingDtos[fromIndex] = default;
            _physicsFrozenVelocities[fromIndex] = default;
            _physicsCullingStateAges[fromIndex] = default;
            MarkPhysicsCullingSpatialHashDirty();
        }

        public void QueueTargetedPhysicsWakeRequest(in PhysicsCullingTargetWakeRequestSignal request)
        {
            EnsureNativeState();
            if (_physicsTargetWakeRequests.IsCreated)
                _physicsTargetWakeRequests.Enqueue(request);
        }

        public bool TryGetPhysicsCullingTuning(out PhysicsCullingTuningDTO tuning)
        {
            EnsureNativeState();
            if (_physicsCullingTuning.IsCreated)
            {
                tuning = ResolvePhysicsCullingTuning();
                return true;
            }

            tuning = default;
            return false;
        }

        public void SetPhysicsCullingTuning(in PhysicsCullingTuningDTO tuning)
        {
            EnsureNativeState();
            if (!_physicsCullingTuning.IsCreated)
                return;

            _physicsCullingTuning[0] = new PhysicsCullingTuningDTO
            {
                DebrisWakeRadiusMeters = math.clamp(tuning.DebrisWakeRadiusMeters, 1f, 500f),
                VehicleWakeRadiusMeters = math.clamp(tuning.VehicleWakeRadiusMeters, 1f, 1000f),
                FrustumClampDistanceMeters = math.clamp(tuning.FrustumClampDistanceMeters, 20f, 1000f),
                HysteresisDelaySeconds = math.clamp(tuning.HysteresisDelaySeconds, 0.1f, 10f),
                SpatialCellSizeMeters = PhysicsCullingSpatialCellSizeMeters,
                MockShockwaveRadiusMeters = math.clamp(tuning.MockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters),
                Flags = tuning.Flags
            };
            _physicsCullingTuningInitialized = true;
        }

        public int PhysicsCullingDebugBodyCount
        {
            get
            {
                int mockEnd = _trackedBodyCount + _physicsCullingMockBodyCount;
                return math.min(MaxTrackedBodies, math.max(_trackedBodyCount, mockEnd));
            }
        }

        public bool TryGetPhysicsCullingDebugBody(int index, out PhysicsCullingDebugBody debugBody)
        {
            debugBody = default;
            if ((uint)index >= (uint)PhysicsCullingDebugBodyCount || !_physicsCullingDtos.IsCreated)
                return false;

            PhysicsCullingDTO dto = _physicsCullingDtos[index];
            if (dto.InstanceId == 0 && index >= _trackedBodyCount)
                return false;

            debugBody = new PhysicsCullingDebugBody
            {
                Aup = dto.AUP,
                AgeSeconds = _physicsCullingStateAges.IsCreated ? _physicsCullingStateAges[index] : 0f,
                InstanceId = dto.InstanceId,
                IsAsleep = dto.IsAsleep,
                IsHysteresisLocked = _physicsCullingStateAges.IsCreated &&
                    _physicsCullingStateAges[index] < ResolvePhysicsCullingHysteresisSeconds() ? (byte)1 : (byte)0
            };
            return true;
        }

        private void FlushPhysicsTargetWakeRequests()
        {
            if (!_physicsTargetWakeRequests.IsCreated)
                return;

            int processed = 0;
            while (processed < PhysicsCullingTargetWakeQueueCapacity && _physicsTargetWakeRequests.TryDequeue(out PhysicsCullingTargetWakeRequestSignal request))
            {
                ProcessTargetedPhysicsWakeRequest(in request);
                processed++;
            }
        }

        private void ProcessTargetedPhysicsWakeRequest(in PhysicsCullingTargetWakeRequestSignal request)
        {
            int instanceId = unchecked((int)request.TargetInstanceId);
            if (!_trackedBodyIndexByInstanceId.TryGetValue(instanceId, out int bodyIndex) ||
                (uint)bodyIndex >= (uint)_trackedBodyCount)
            {
                return;
            }

            float3 impulse = request.ImpulseVector;
            if (!math.all(math.isfinite(impulse)))
                impulse = float3.zero;

            FrozenVelocityDTO frozen = _physicsFrozenVelocities[bodyIndex];
            frozen.LinearVelocity += impulse;
            frozen.HasVelocity = 1;
            _physicsFrozenVelocities[bodyIndex] = frozen;

            PhysicsCullingDTO dto = _physicsCullingDtos[bodyIndex];
            dto.IsAsleep = 0;
            _physicsCullingDtos[bodyIndex] = dto;
            _rigidbodyAwakeResults[bodyIndex] = 1;
            _rigidbodyCullingCommandResults[bodyIndex] = CullingCommandAwake;
            _physicsCullingStateAges[bodyIndex] = 0f;
            if (_physicsStateChangedIndices.IsCreated)
                _physicsStateChangedIndices.Enqueue(bodyIndex);
        }

        public int GenerateMockPhysicsBodies(int count = PhysicsCullingMockBodiesPerGenerate)
        {
            EnsureNativeState();
            int available = MaxTrackedBodies - _trackedBodyCount;
            int mockCount = math.clamp(count, 0, available);
            double3 baseAup = default;
            if (TryResolvePhysicsCullingPlayerState(out AbsoluteUniversePosition playerAup, out _, out _))
                baseAup = playerAup.ToAbsoluteDouble3();

            for (int i = 0; i < mockCount; i++)
            {
                uint h = HashMockPhysicsBody((uint)(i + 1));
                float x = ((int)(h & 255u) - 128) * 4f;
                float z = ((int)((h >> 8) & 255u) - 128) * 4f;
                float y = ((int)((h >> 16) & 31u) - 16) * 2f;
                int bodyIndex = _trackedBodyCount + i;
                _physicsCullingDtos[bodyIndex] = new PhysicsCullingDTO
                {
                    AUP = baseAup + new double3(x, y, z),
                    InstanceId = -1000000 - i,
                    ActivationRadiusSq = PhysicsCullingDefaultDebrisWakeRadiusMeters * PhysicsCullingDefaultDebrisWakeRadiusMeters,
                    IsAsleep = 0,
                    CullingFlags = 0u
                };
                _physicsCullingStateAges[bodyIndex] = ResolvePhysicsCullingHysteresisSeconds();
                _physicsFrozenVelocities[bodyIndex] = default;
                _rigidbodyCullingStateSnapshot[bodyIndex] = 0;
            }

            _physicsCullingMockBodyCount = mockCount;
            MarkPhysicsCullingSpatialHashDirty();
            return mockCount;
        }

        private static uint HashMockPhysicsBody(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        public void FireMockSeismicShockwave(uint seed)
        {
            EnsureNativeState();
            if (!_physicsMockSeismicSignals.IsCreated || !_physicsCullingDtos.IsCreated)
                return;

            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            AbsoluteUniversePosition playerAup = default;
            if (!TryResolvePhysicsCullingPlayerState(out playerAup, out _, out _))
                return;

            uint h = HashMockPhysicsBody(seed == 0u ? 1u : seed);
            double3 jitter = new double3(
                ((int)(h & 63u) - 32) * 2.0,
                0.0,
                ((int)((h >> 6) & 63u) - 32) * 2.0);
            MockSeismicShockwaveSignal signal = new MockSeismicShockwaveSignal
            {
                EpicenterAup = playerAup.ToAbsoluteDouble3() + jitter,
                RadiusMeters = math.clamp(tuning.MockShockwaveRadiusMeters, ImpactWakeMinimumRadiusMeters, ImpactWakeMaximumRadiusMeters),
                Seed = seed,
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                Fire = 1
            };
            _physicsMockSeismicSignals[0] = signal;

            ClearPhysicsStateChangedQueue();
            MockSeismicShockwaveWakeJob job = new MockSeismicShockwaveWakeJob
            {
                Dtos = _physicsCullingDtos,
                AwakeResults = _rigidbodyAwakeResults,
                CommandResults = _rigidbodyCullingCommandResults,
                StateAges = _physicsCullingStateAges,
                Signal = signal,
                ChangedIndices = _physicsStateChangedIndices.AsParallelWriter()
            };
            int jobCount = math.min(MaxTrackedBodies, _trackedBodyCount + _physicsCullingMockBodyCount);
            job.Schedule(jobCount, 64).Complete();
            DispatchPhysicsCullingResults(jobCount);
        }

        private void RecordShinobu37PhysicsCullingFrameTelemetry(float stateSyncTimeMs, int changedIndices)
        {
            CountCulledTrackedBodies(out int activeBodies, out int asleepBodies);
            RecordShinobu37PhysicsCullingFrameTelemetry(stateSyncTimeMs, changedIndices, activeBodies, asleepBodies);
        }

        private void RecordShinobu37PhysicsCullingFrameTelemetry(float stateSyncTimeMs, int changedIndices, int activeBodies, int asleepBodies)
        {
            if (!_physicsCullingFrameTelemetry.IsCreated)
                return;

            int index = _physicsCullingFrameTelemetryWriteIndex;
            if ((uint)index >= PhysicsCullingFrameTelemetryCapacity)
                index = 0;

            _physicsCullingFrameTelemetry[index] = new PhysicsCullingFrameTelemetry
            {
                FrameIndex = Time.frameCount,
                TotalTrackedBodies = _trackedBodyCount,
                ActiveBodies = activeBodies,
                AsleepBodies = asleepBodies,
                StateSyncTimeMs = math.isfinite(stateSyncTimeMs) ? stateSyncTimeMs : 0f,
                ChangedIndices = changedIndices,
                Flags = _physicsCullingMockBodyCount > 0 ? 1u : 0u
            };

            int next = index + 1;
            _physicsCullingFrameTelemetryWriteIndex = next >= PhysicsCullingFrameTelemetryCapacity ? 0 : next;
        }

        private void WriteShinobu37PhysicsCullingFrameDump(BinaryWriter writer)
        {
            if (writer == null || !_physicsCullingFrameTelemetry.IsCreated)
                return;

            writer.Write(_physicsCullingFrameTelemetryWriteIndex);
            writer.Write(PhysicsCullingFrameTelemetryCapacity);
            for (int i = 0; i < PhysicsCullingFrameTelemetryCapacity; i++)
            {
                PhysicsCullingFrameTelemetry entry = _physicsCullingFrameTelemetry[i];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.TotalTrackedBodies);
                writer.Write(entry.ActiveBodies);
                writer.Write(entry.AsleepBodies);
                writer.Write(entry.StateSyncTimeMs);
                writer.Write(entry.ChangedIndices);
                writer.Write(entry.Flags);
                writer.Write(entry._pad0);
            }
        }

        private void TickPhysicsCullingCsvOverrideMonitor()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _physicsCullingCsvPollAccumulator += PhysicsCullingSlowTickIntervalSeconds;
            if (_physicsCullingCsvPollAccumulator < PhysicsCullingCsvPollIntervalSeconds)
                return;

            _physicsCullingCsvPollAccumulator = 0f;
            string path = ResolvePhysicsCullingCsvAbsolutePath();
            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _physicsCullingCsvLastWriteTicks)
                return;

            int bytesRead = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(_physicsCullingCsvScratch, 0, _physicsCullingCsvScratch.Length);
            }

            if (bytesRead > 0 && TryIngestPhysicsCullingCsv(new ReadOnlySpan<byte>(_physicsCullingCsvScratch, 0, bytesRead)))
                _physicsCullingCsvLastWriteTicks = ticks;
#endif
        }

        private string ResolvePhysicsCullingCsvAbsolutePath()
        {
            if (!string.IsNullOrEmpty(_physicsCullingCsvAbsolutePath))
                return _physicsCullingCsvAbsolutePath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _physicsCullingCsvAbsolutePath = Path.Combine(projectRoot, PhysicsCullingProfilesRelativePath);
            return _physicsCullingCsvAbsolutePath;
        }

        public bool TryIngestPhysicsCullingCsv(ReadOnlySpan<byte> csv)
        {
            if (!_physicsCullingTuning.IsCreated)
                return false;

            PhysicsCullingTuningDTO tuning = ResolvePhysicsCullingTuning();
            int cursor = 0;
            bool changed = false;
            while (TryReadCsvLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                if (!TryReadCsvKeyValue(line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value))
                    continue;

                if (!TryParseAsciiFloat(value, out float parsedValue))
                    continue;

                uint hash = HashLowerAscii(key);
                if (hash == HashLowerAsciiLiteral("debris_wake_radius"))
                {
                    tuning.DebrisWakeRadiusMeters = math.clamp(parsedValue, 1f, 500f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("vehicle_wake_radius"))
                {
                    tuning.VehicleWakeRadiusMeters = math.clamp(parsedValue, 1f, 1000f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("frustum_clamp_distance"))
                {
                    tuning.FrustumClampDistanceMeters = math.clamp(parsedValue, 20f, 1000f);
                    changed = true;
                }
                else if (hash == HashLowerAsciiLiteral("hysteresis_delay"))
                {
                    tuning.HysteresisDelaySeconds = math.clamp(parsedValue, 0.1f, 10f);
                    changed = true;
                }
            }

            if (changed)
                _physicsCullingTuning[0] = tuning;

            return changed;
        }

        private static bool TryReadCsvLine(ReadOnlySpan<byte> csv, ref int cursor, out ReadOnlySpan<byte> line)
        {
            int start = cursor;
            while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < csv.Length && csv[cursor] == (byte)'\n')
                cursor++;
            if (end > start && csv[end - 1] == (byte)'\r')
                end--;

            line = TrimAscii(csv.Slice(start, end - start));
            return line.Length > 0;
        }

        private static bool TryReadCsvKeyValue(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            key = default;
            value = default;
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int delimiter = -1;
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=' || b == (byte)';')
                {
                    delimiter = i;
                    break;
                }
            }

            if (delimiter <= 0 || delimiter >= line.Length - 1)
                return false;

            key = TrimAscii(line.Slice(0, delimiter));
            value = TrimAscii(line.Slice(delimiter + 1));
            return key.Length > 0 && value.Length > 0;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool hasDigit = false;
            while (index < value.Length)
            {
                byte b = value[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                result = (result * 10f) + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length)
                {
                    byte b = value[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    result += (b - (byte)'0') * place;
                    place *= 0.1f;
                    hasDigit = true;
                    index++;
                }
            }

            parsed = result * sign;
            return hasDigit && math.isfinite(parsed);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static uint HashLowerAsciiLiteral(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                byte b = (byte)(c >= 'A' && c <= 'Z' ? c + 32 : c);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct MockSeismicShockwaveWakeJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<PhysicsCullingDTO> Dtos;
            [NativeDisableParallelForRestriction] public NativeArray<byte> AwakeResults;
            [NativeDisableParallelForRestriction] public NativeArray<byte> CommandResults;
            [NativeDisableParallelForRestriction] public NativeArray<float> StateAges;
            public MockSeismicShockwaveSignal Signal;
            public NativeQueue<int>.ParallelWriter ChangedIndices;

            public unsafe void Execute(int index)
            {
                if (Signal.Fire == 0 || (uint)index >= (uint)Dtos.Length)
                    return;

                ref PhysicsCullingDTO dto = ref UnsafeUtility.ArrayElementAsRef<PhysicsCullingDTO>(Dtos.GetUnsafePtr(), index);
                if ((dto.CullingFlags & PhysicsCullingDtoExemptFlag) != 0u)
                    return;

                double3 delta = dto.AUP - Signal.EpicenterAup;
                if (!math.all(math.isfinite(delta)))
                    return;

                double radiusSq = (double)math.max(0f, Signal.RadiusMeters) * Signal.RadiusMeters;
                if (math.lengthsq(delta) > radiusSq)
                    return;

                if (dto.IsAsleep != 0)
                {
                    dto.IsAsleep = 0;
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake;
                    StateAges[index] = 0f;
                    ChangedIndices.Enqueue(index);
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PhysicsDistanceCullingJobShinobu37 : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<PhysicsCullingDTO> Dtos;
            [ReadOnly] public NativeArray<byte> CurrentStates;
            [ReadOnly] public NativeArray<int> CandidateIndices;
            [NativeDisableParallelForRestriction] public NativeArray<byte> AwakeResults;
            [NativeDisableParallelForRestriction] public NativeArray<byte> CommandResults;
            [NativeDisableParallelForRestriction] public NativeArray<float> DistanceSqResults;
            [NativeDisableParallelForRestriction] public NativeArray<float> StateAges;
            public NativeQueue<int>.ParallelWriter ChangedIndices;
            public double3 CameraAbsoluteAup;
            public float3 CameraForward;
            public float KinematicSleepDistanceMeters;
            public float KinematicWakeDistanceMeters;
            public float MeshColliderStripDistanceMeters;
            public float MeshColliderRestoreDistanceMeters;
            public float4 FrustumPlane0;
            public float4 FrustumPlane1;
            public float4 FrustumPlane2;
            public float4 FrustumPlane3;
            public float4 FrustumPlane4;
            public float4 FrustumPlane5;
            public float FrustumInnerSphereSq;
            public float HardwareRadiusSqScale;
            public float HysteresisSeconds;
            public float DeltaTimeSeconds;
            public byte AbyssalDepthCull;
            public byte UseFrustum;

            public unsafe void Execute(int candidateIndex)
            {
                if ((uint)candidateIndex >= (uint)CandidateIndices.Length)
                    return;

                int index = CandidateIndices[candidateIndex];
                if ((uint)index >= (uint)Dtos.Length)
                    return;

                ref PhysicsCullingDTO dto = ref UnsafeUtility.ArrayElementAsRef<PhysicsCullingDTO>(Dtos.GetUnsafePtr(), index);
                byte currentState = index < CurrentStates.Length ? CurrentStates[index] : (byte)0;
                if ((currentState & CullingStateIgnoreCulling) != 0 || (dto.CullingFlags & PhysicsCullingDtoExemptFlag) != 0u)
                {
                    dto.IsAsleep = 0;
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake;
                    DistanceSqResults[index] = 0f;
                    return;
                }

                float age = StateAges[index];
                bool sleepActive = (currentState & CullingStateSleepActive) != 0 || dto.IsAsleep != 0;
                if (age < HysteresisSeconds)
                {
                    StateAges[index] = math.min(HysteresisSeconds, age + math.max(0f, DeltaTimeSeconds));
                    AwakeResults[index] = sleepActive ? (byte)0 : (byte)1;
                    CommandResults[index] = sleepActive ? (byte)0 : CullingCommandAwake;
                    return;
                }

                double3 deltaDouble = dto.AUP - CameraAbsoluteAup;
                if (!math.all(math.isfinite(deltaDouble)))
                {
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake | CullingCommandInvalidInput;
                    DistanceSqResults[index] = 0f;
                    ChangedIndices.Enqueue(index);
                    return;
                }

                float3 delta = new float3((float)deltaDouble.x, (float)deltaDouble.y, (float)deltaDouble.z);
                float distanceSq = math.lengthsq(delta);
                if (!math.isfinite(distanceSq))
                {
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake | CullingCommandInvalidInput;
                    DistanceSqResults[index] = 0f;
                    ChangedIndices.Enqueue(index);
                    return;
                }

                DistanceSqResults[index] = distanceSq;
                float activationRadiusSq = math.max(1f, dto.ActivationRadiusSq) * math.max(0.01f, HardwareRadiusSqScale);
                if (AbyssalDepthCull != 0)
                    activationRadiusSq *= AbyssalDepthSleepDistanceScale * AbyssalDepthSleepDistanceScale;

                float3 safeCameraForward = NormalizeWithRsqrtGuard(CameraForward, new float3(0f, 0f, 1f));
                if (math.dot(delta, safeCameraForward) < 0f)
                    activationRadiusSq *= BehindCameraSleepDistanceScale * BehindCameraSleepDistanceScale;

                float wakeRadiusSq = math.max(1f, activationRadiusSq * PhysicsCullingWakeRadiusSqScale);
                bool outsideFrustum = UseFrustum != 0 && distanceSq > FrustumInnerSphereSq && IsOutsideFrustum(delta);
                bool shouldSleep = sleepActive
                    ? distanceSq > wakeRadiusSq || outsideFrustum
                    : distanceSq > activationRadiusSq || outsideFrustum;

                bool kinematicActive = (currentState & CullingStateKinematicActive) != 0;
                float kinematicSleepSq = KinematicSleepDistanceMeters * KinematicSleepDistanceMeters;
                float kinematicWakeSq = KinematicWakeDistanceMeters * KinematicWakeDistanceMeters;
                bool shouldKinematic = kinematicActive
                    ? distanceSq > kinematicWakeSq
                    : distanceSq > kinematicSleepSq;

                bool meshStripActive = (currentState & CullingStateMeshColliderStripped) != 0;
                bool hasHeavyCollider = (currentState & CullingStateHeavyCollider) != 0;
                float stripSq = MeshColliderStripDistanceMeters * MeshColliderStripDistanceMeters;
                float restoreSq = MeshColliderRestoreDistanceMeters * MeshColliderRestoreDistanceMeters;
                bool shouldStripMeshColliders = hasHeavyCollider && (meshStripActive
                    ? distanceSq > restoreSq
                    : distanceSq > stripSq);

                byte newSleep = shouldSleep ? (byte)1 : (byte)0;
                byte previousSleep = dto.IsAsleep;
                dto.IsAsleep = newSleep;
                AwakeResults[index] = shouldSleep ? (byte)0 : (byte)1;

                byte command = shouldSleep ? (byte)0 : CullingCommandAwake;
                if (shouldKinematic)
                    command |= CullingCommandKinematic;
                if (shouldStripMeshColliders)
                    command |= CullingCommandStripMeshColliders;
                CommandResults[index] = command;

                byte previousCommand = ResolvePreviousCommand(currentState, previousSleep);
                if (newSleep != previousSleep || command != previousCommand)
                {
                    StateAges[index] = 0f;
                    ChangedIndices.Enqueue(index);
                }
                else
                {
                    StateAges[index] = math.min(HysteresisSeconds, age + math.max(0f, DeltaTimeSeconds));
                }
            }

            private bool IsOutsideFrustum(float3 localPoint)
            {
                return PlaneDistance(FrustumPlane0, localPoint) < 0f ||
                    PlaneDistance(FrustumPlane1, localPoint) < 0f ||
                    PlaneDistance(FrustumPlane2, localPoint) < 0f ||
                    PlaneDistance(FrustumPlane3, localPoint) < 0f ||
                    PlaneDistance(FrustumPlane4, localPoint) < 0f ||
                    PlaneDistance(FrustumPlane5, localPoint) < 0f;
            }

            private static float PlaneDistance(float4 plane, float3 point)
            {
                return (plane.x * point.x) + (plane.y * point.y) + (plane.z * point.z) + plane.w;
            }

            private static byte ResolvePreviousCommand(byte currentState, byte sleep)
            {
                byte command = sleep != 0 ? (byte)0 : CullingCommandAwake;
                if ((currentState & CullingStateKinematicActive) != 0)
                    command |= CullingCommandKinematic;
                if ((currentState & CullingStateMeshColliderStripped) != 0)
                    command |= CullingCommandStripMeshColliders;
                return command;
            }
        }
    }
}
