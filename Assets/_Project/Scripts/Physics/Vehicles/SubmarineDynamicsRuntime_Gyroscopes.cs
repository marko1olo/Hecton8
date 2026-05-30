using System;
#if UNITY_EDITOR
using System.IO;
using System.Threading;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics.Vehicles
{
    public sealed partial class SubmarineDynamicsRuntime
    {
        private static int s_x001DirectSignalPushDropCount_SubmarineDynamicsRuntime_Gyroscopes;

#if UNITY_EDITOR
        private const long MaxGyroProfileCsvBytes = 4096L;
#endif
        private static readonly int s_GyroVisualBufferId = Shader.PropertyToID("_H8SubmarineGyroVisuals");
        private static readonly int s_GyroVisualCountId = Shader.PropertyToID("_H8SubmarineGyroVisualCount");
        private static readonly ulong GyroScheduleMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332SubmarineGyros) |
            VaultMutationGuardBit(BufferID.Shinobu332GyroErrors) |
            VaultMutationGuardBit(BufferID.Shinobu332GyroForcePackets) |
            VaultMutationGuardBit(BufferID.Shinobu332GyroTelemetry) |
            VaultMutationGuardBit(BufferID.Shinobu332GyroVisualStates) |
            VaultMutationGuardBit(BufferID.Shinobu332GyroCounters);
        private static readonly ulong GyroDefaultGyroMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332SubmarineGyros);
        private static readonly ulong GyroDefaultProfileMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332GyroProfiles);
        private static readonly ulong GyroDefaultCounterMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332GyroCounters);
#if UNITY_EDITOR
        private static readonly ulong GyroProfilesCsvGyroMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332SubmarineGyros);
        private static readonly ulong GyroProfilesCsvProfileMutationGuardMask =
            VaultMutationGuardBit(BufferID.Shinobu332GyroProfiles);
        private static readonly SubmarineGyroDTO[] s_gyroCsvScratch = new SubmarineGyroDTO[SubmarineDynamicsConstants.MaxVehicles];
        private static readonly SubmarineGyroProfileDTO[] s_gyroProfileCsvScratch = new SubmarineGyroProfileDTO[SubmarineDynamicsConstants.MaxVehicles];
#endif

        [Header("Auto-Level Gyro")]
        [SerializeField] private bool enableGyroAutoLevel = true;
        [SerializeField] private bool enableMockGyroTurbulence;
        [SerializeField, Min(0f)] private float gyroPitchProportionalGain = 54000f;
        [SerializeField, Min(0f)] private float gyroPitchDerivativeGain = 11000f;
        [SerializeField, Min(0f)] private float gyroRollProportionalGain = 62000f;
        [SerializeField, Min(0f)] private float gyroRollDerivativeGain = 13000f;
        [SerializeField, Min(0f)] private float gyroMaxCorrectionTorque = 85000f;
        [SerializeField, Min(0f)] private float mockGyroTurbulenceRadPerSecond = 0.55f;

        private VaultGenerationHandle<SubmarineGyroDTO> _gyroHandle;
        private VaultGenerationHandle<SubmarineGyroErrorDTO> _gyroErrorHandle;
        private VaultGenerationHandle<SubmarineGyroForcePacketDTO> _gyroForcePacketHandle;
        private VaultGenerationHandle<GyroTelemetryEntry> _gyroTelemetryHandle;
        private VaultGenerationHandle<SubmarineGyroVisualStateDTO> _gyroVisualHandle;
        private VaultGenerationHandle<SubmarineGyroProfileDTO> _gyroProfileHandle;
        private VaultGenerationHandle<SubmarineGyroCounterDTO> _gyroCounterHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _gyroCsvScratchHandle;
#endif
        private GraphicsBuffer _gyroVisualBufferA;
        private GraphicsBuffer _gyroVisualBufferB;
        private int _gyroVisualBufferCapacity;
        private int _gyroVisualBufferWriteIndex;
        private long _gyroScheduleTicks;
#if UNITY_EDITOR
        private long _gyroProfilesCsvLastWriteTicks;
        private string _gyroProfilesCsvPath;
#endif
        private uint _gyroLastVisualUploadFrame;
        private bool _gyroDumpWritten;

#if UNITY_EDITOR
        private void InitializeGyroRuntimePaths()
        {
            _gyroProfilesCsvPath = Path.Combine(_projectRoot, "Data", "Physics", "vehicle_gyro_profiles.csv");
        }
#endif

        private bool EnsureGyroVaultBuffers(int capacity)
        {
            if (_dataVault == null)
                return false;

            if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                return false;

            int safeCapacity = math.clamp(capacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            _gyroHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroDTO>(BufferID.Shinobu332SubmarineGyros, safeCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _gyroErrorHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroErrorDTO>(BufferID.Shinobu332GyroErrors, safeCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _gyroForcePacketHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroForcePacketDTO>(BufferID.Shinobu332GyroForcePackets, safeCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _gyroTelemetryHandle = _dataVault.EnsureGenerationHandle<GyroTelemetryEntry>(BufferID.Shinobu332GyroTelemetry, SubmarineDynamicsConstants.BlackBoxFrames, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _gyroVisualHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroVisualStateDTO>(BufferID.Shinobu332GyroVisualStates, safeCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _gyroProfileHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroProfileDTO>(BufferID.Shinobu332GyroProfiles, safeCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _gyroCounterHandle = _dataVault.EnsureGenerationHandle<SubmarineGyroCounterDTO>(BufferID.Shinobu332GyroCounters, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            _gyroCsvScratchHandle = _dataVault.EnsureGenerationHandle<byte>(BufferID.Shinobu332GyroCsvScratch, (int)MaxGyroProfileCsvBytes, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
#endif

            if (!IsGenerationHandleCreated(in _gyroHandle) ||
                !IsGenerationHandleCreated(in _gyroErrorHandle) ||
                !IsGenerationHandleCreated(in _gyroForcePacketHandle) ||
                !IsGenerationHandleCreated(in _gyroTelemetryHandle) ||
                !IsGenerationHandleCreated(in _gyroVisualHandle) ||
                !IsGenerationHandleCreated(in _gyroProfileHandle) ||
                !IsGenerationHandleCreated(in _gyroCounterHandle)
#if UNITY_EDITOR
                || !IsGenerationHandleCreated(in _gyroCsvScratchHandle)
#endif
                )
            {
                return false;
            }

            return EnsureGyroVisualGraphicsBuffer(safeCapacity) && TryInitializeGyroDefaults(safeCapacity);
        }

        private bool EnsureGyroVisualGraphicsBuffer(int capacity)
        {
            int safeCapacity = math.clamp(capacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            if (_gyroVisualBufferA != null &&
                _gyroVisualBufferB != null &&
                _gyroVisualBufferCapacity == safeCapacity &&
                _gyroVisualBufferA.stride == SubmarineDynamicsConstants.GyroVisualStateBytes &&
                _gyroVisualBufferB.stride == SubmarineDynamicsConstants.GyroVisualStateBytes)
            {
                return true;
            }

            _gyroVisualBufferA?.Release();
            _gyroVisualBufferB?.Release();
            _gyroVisualBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                safeCapacity,
                SubmarineDynamicsConstants.GyroVisualStateBytes); // COLD ALLOC: GraphicsBuffer[MaxVehicles] - gyro visual upload buffer A - owner: SHINOBU_332
            _gyroVisualBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                safeCapacity,
                SubmarineDynamicsConstants.GyroVisualStateBytes); // COLD ALLOC: GraphicsBuffer[MaxVehicles] - gyro visual upload buffer B - owner: SHINOBU_332
            _gyroVisualBufferCapacity = safeCapacity;
            _gyroVisualBufferWriteIndex = 0;
            _gyroLastVisualUploadFrame = 0u;
            return true;
        }

        private bool TryInitializeGyroDefaults(int capacity)
        {
            if (!TryReadOnlyVaultHandle(in _gyroHandle, out NativeArray<SubmarineGyroDTO>.ReadOnly gyroRead) ||
                !TryReadOnlyVaultHandle(in _gyroProfileHandle, out NativeArray<SubmarineGyroProfileDTO>.ReadOnly profileRead) ||
                !TryReadOnlyVaultHandle(in _gyroCounterHandle, out NativeArray<SubmarineGyroCounterDTO>.ReadOnly counterRead))
            {
                return false;
            }

            int count = math.min(math.max(0, capacity), math.min(gyroRead.Length, profileRead.Length));
            count = math.min(count, SubmarineDynamicsConstants.MaxVehicles);
            Span<SubmarineGyroDTO> gyroScratch = stackalloc SubmarineGyroDTO[SubmarineDynamicsConstants.MaxVehicles];
            Span<SubmarineGyroProfileDTO> profileScratch = stackalloc SubmarineGyroProfileDTO[SubmarineDynamicsConstants.MaxVehicles];
            for (int i = 0; i < count; i++)
            {
                SubmarineGyroDTO gyro = gyroRead[i];
                if (gyro.MaxCorrectionTorque <= 0f || !math.isfinite(gyro.MaxCorrectionTorque))
                {
                    gyro = BuildSerializedGyro();
                }
                gyroScratch[i] = gyro;

                SubmarineGyroProfileDTO profile = profileRead[i];
                if (profile.ProfileHash == 0u)
                {
                    profile.ProfileHash = SubmarineDynamicsConstants.SourceHashGyro ^ (uint)i;
                    profile.ProportionalGainPitch = gyro.ProportionalGainPitch;
                    profile.DerivativeGainPitch = gyro.DerivativeGainPitch;
                    profile.ProportionalGainRoll = gyro.ProportionalGainRoll;
                    profile.DerivativeGainRoll = gyro.DerivativeGainRoll;
                    profile.MaxCorrectionTorque = gyro.MaxCorrectionTorque;
                    profile.Flags = gyro.AutoLevelEnabledFlag;
                }
                profileScratch[i] = profile;
            }

            bool clearCounter = counterRead.Length > 0 && counterRead[0].Frame == 0u;
            if (!TryCommitGyroDefaultGyros(gyroScratch, count) ||
                !TryCommitGyroDefaultProfiles(profileScratch, count))
            {
                return false;
            }

            return !clearCounter || TryCommitGyroDefaultCounter();
        }

        private bool TryCommitGyroDefaultGyros(ReadOnlySpan<SubmarineGyroDTO> gyroScratch, int count)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(GyroDefaultGyroMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _gyroHandle, out NativeArray<SubmarineGyroDTO> gyros) ||
                    gyros.Length == 0)
                {
                    return false;
                }

                int copyLength = math.min(math.min(gyros.Length, gyroScratch.Length), count);
                for (int i = 0; i < copyLength; i++)
                    gyros[i] = gyroScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GyroDefaultGyroMutationGuardMask);
            }
        }

        private bool TryCommitGyroDefaultProfiles(ReadOnlySpan<SubmarineGyroProfileDTO> profileScratch, int count)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(GyroDefaultProfileMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _gyroProfileHandle, out NativeArray<SubmarineGyroProfileDTO> profiles) ||
                    profiles.Length == 0)
                {
                    return false;
                }

                int copyLength = math.min(math.min(profiles.Length, profileScratch.Length), count);
                for (int i = 0; i < copyLength; i++)
                    profiles[i] = profileScratch[i];
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GyroDefaultProfileMutationGuardMask);
            }
        }

        private bool TryCommitGyroDefaultCounter()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(GyroDefaultCounterMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _gyroCounterHandle, out NativeArray<SubmarineGyroCounterDTO> counters) ||
                    counters.Length == 0)
                {
                    return false;
                }

                counters[0] = default;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GyroDefaultCounterMutationGuardMask);
            }
        }

        private SubmarineGyroDTO BuildSerializedGyro()
        {
            SubmarineGyroDTO gyro = default;
            gyro.ProportionalGainPitch = math.max(0f, gyroPitchProportionalGain);
            gyro.DerivativeGainPitch = math.max(0f, gyroPitchDerivativeGain);
            gyro.ProportionalGainRoll = math.max(0f, gyroRollProportionalGain);
            gyro.DerivativeGainRoll = math.max(0f, gyroRollDerivativeGain);
            gyro.MaxCorrectionTorque = math.max(1f, gyroMaxCorrectionTorque);
            gyro.AutoLevelEnabledFlag = enableGyroAutoLevel ? SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled : 0u;
            return gyro;
        }

        private bool TryResolveGyroArrays(
            out NativeArray<SubmarineGyroDTO> gyros,
            out NativeArray<SubmarineGyroErrorDTO> errors,
            out NativeArray<SubmarineGyroForcePacketDTO> packets,
            out NativeArray<GyroTelemetryEntry> telemetry,
            out NativeArray<SubmarineGyroVisualStateDTO> visuals,
            out NativeArray<SubmarineGyroCounterDTO> counters)
        {
            gyros = default;
            errors = default;
            packets = default;
            telemetry = default;
            visuals = default;
            counters = default;

            return TryOpenVaultHandleForOwner(in _gyroHandle, out gyros) &&
                   TryOpenVaultHandleForOwner(in _gyroErrorHandle, out errors) &&
                   TryOpenVaultHandleForOwner(in _gyroForcePacketHandle, out packets) &&
                   TryOpenVaultHandleForOwner(in _gyroTelemetryHandle, out telemetry) &&
                   TryOpenVaultHandleForOwner(in _gyroVisualHandle, out visuals) &&
                   TryOpenVaultHandleForOwner(in _gyroCounterHandle, out counters);
        }

        private bool ValidateGyroScheduleBuffers(int capacity)
        {
            int safeCapacity = math.clamp(capacity, 1, SubmarineDynamicsConstants.MaxVehicles);
            return TryValidateSimulationBuffer(in _gyroHandle, BufferID.Shinobu332SubmarineGyros, safeCapacity) &&
                   TryValidateSimulationBuffer(in _gyroErrorHandle, BufferID.Shinobu332GyroErrors, safeCapacity) &&
                   TryValidateSimulationBuffer(in _gyroForcePacketHandle, BufferID.Shinobu332GyroForcePackets, safeCapacity) &&
                   TryValidateSimulationBuffer(in _gyroTelemetryHandle, BufferID.Shinobu332GyroTelemetry, SubmarineDynamicsConstants.BlackBoxFrames) &&
                   TryValidateSimulationBuffer(in _gyroVisualHandle, BufferID.Shinobu332GyroVisualStates, safeCapacity) &&
                   TryValidateSimulationBuffer(in _gyroCounterHandle, BufferID.Shinobu332GyroCounters, 1);
        }

        private unsafe JobHandle ScheduleGyroPipeline(
            NativeArray<SubmarineKinematicState> states,
            NativeArray<SubmarineForceAccumulator> forces,
            NativeArray<AddedMassProfileDTO> addedMassProfiles,
            NativeArray<SubmarineAddedMassTuningDTO> addedMassTuning,
            float fixedDeltaTime,
            float globalQualityWeight,
            uint frame,
            JobHandle inputDependency)
        {
            if (!TryResolveGyroArrays(
                    out NativeArray<SubmarineGyroDTO> gyros,
                    out NativeArray<SubmarineGyroErrorDTO> errors,
                    out NativeArray<SubmarineGyroForcePacketDTO> packets,
                    out NativeArray<GyroTelemetryEntry> telemetry,
                    out NativeArray<SubmarineGyroVisualStateDTO> visuals,
                    out NativeArray<SubmarineGyroCounterDTO> counters))
            {
                return inputDependency;
            }

            int count = math.min(math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles), states.Length);
            count = math.min(count, math.min(forces.Length, math.min(addedMassProfiles.Length, math.min(gyros.Length, math.min(errors.Length, math.min(packets.Length, visuals.Length))))));
            if (count <= 0)
                return inputDependency;

            _gyroScheduleTicks = Stopwatch.GetTimestamp();
            JobHandle dependency = inputDependency;
            if (enableMockGyroTurbulence)
            {
                GenerateMockTurbulenceJob mockJob = new GenerateMockTurbulenceJob
                {
                    States = (SubmarineKinematicState*)NativeArrayUnsafeUtility.GetUnsafePtr(states),
                    AmplitudeRadiansPerSecond = math.max(0f, mockGyroTurbulenceRadPerSecond) * math.clamp(fixedDeltaTime, 0.001f, 0.05f),
                    Frame = frame,
                    StateLength = states.Length,
                    VehicleCount = count
                };
                dependency = mockJob.Schedule(count, SubmarineDynamicsConstants.IntegratorBatchSize, dependency);
            }

            CalculateGyroscopicErrorJob errorJob = new CalculateGyroscopicErrorJob
            {
                States = (SubmarineKinematicState*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                Errors = (SubmarineGyroErrorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(errors),
                Frame = frame,
                StateLength = states.Length,
                ErrorLength = errors.Length,
                VehicleCount = count
            };
            dependency = errorJob.Schedule(count, SubmarineDynamicsConstants.IntegratorBatchSize, dependency);

            EvaluatePdControllerJob pdJob = new EvaluatePdControllerJob
            {
                States = (SubmarineKinematicState*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states),
                Gyros = (SubmarineGyroDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(gyros),
                Errors = (SubmarineGyroErrorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(errors),
                AddedMassProfiles = (AddedMassProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(addedMassProfiles),
                AddedMassTuning = addedMassTuning.IsCreated && addedMassTuning.Length > 0 ? (SubmarineAddedMassTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(addedMassTuning) : null,
                Forces = (SubmarineForceAccumulator*)NativeArrayUnsafeUtility.GetUnsafePtr(forces),
                Packets = (SubmarineGyroForcePacketDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(packets),
                VisualStates = (SubmarineGyroVisualStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(visuals),
                GlobalQualityWeight = globalQualityWeight,
                Frame = frame,
                StateLength = states.Length,
                GyroLength = gyros.Length,
                ErrorLength = errors.Length,
                AddedMassLength = addedMassProfiles.Length,
                TuningLength = addedMassTuning.IsCreated ? addedMassTuning.Length : 0,
                ForceLength = forces.Length,
                PacketLength = packets.Length,
                VisualLength = visuals.Length,
                VehicleCount = count
            };
            dependency = pdJob.Schedule(count, SubmarineDynamicsConstants.IntegratorBatchSize, dependency);

            RecordGyroTelemetryJob telemetryJob = new RecordGyroTelemetryJob
            {
                Packets = (SubmarineGyroForcePacketDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(packets),
                Telemetry = (GyroTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                Counters = (SubmarineGyroCounterDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                GlobalQualityWeight = globalQualityWeight,
                Frame = frame,
                PacketLength = packets.Length,
                TelemetryLength = telemetry.Length,
                CounterLength = counters.Length,
                VehicleCount = count
            };
            return telemetryJob.Schedule(dependency);
        }

        private void PatchGyroElapsedMicros(float elapsedMicros)
        {
            if (elapsedMicros <= 0f || !math.isfinite(elapsedMicros))
                return;

            if (!TryOpenVaultHandleForOwner(in _gyroTelemetryHandle, out NativeArray<GyroTelemetryEntry> telemetry) || telemetry.Length == 0)
                return;

            int index = (int)(_frameCounter % (uint)telemetry.Length);
            GyroTelemetryEntry entry = telemetry[index];
            if (entry.Frame != _frameCounter)
                return;

            entry.BurstElapsedUs = elapsedMicros;
            telemetry[index] = entry;
        }

        private bool DumpGyroBlackBoxIfFaulted()
        {
            if (_frameCounter == 0u ||
                _gyroDumpWritten ||
                _dataVault == null ||
                !IsGenerationHandleCreated(in _gyroTelemetryHandle))
            {
                return false;
            }

            if (!TryReadOnlyVaultHandle(in _gyroTelemetryHandle, out NativeArray<GyroTelemetryEntry>.ReadOnly telemetry) || telemetry.Length == 0)
                return false;

            int index = (int)(_frameCounter % (uint)telemetry.Length);
            GyroTelemetryEntry latest = telemetry[index];
            bool fatal = latest.Frame == _frameCounter &&
                         (((latest.Flags & SubmarineDynamicsConstants.GyroFlagNonFinite) != 0u) ||
                          latest.NonFiniteCount > 0u ||
                          latest.BurstElapsedUs > 200f);
            if (!fatal)
                return false;

            bool written = TryDumpCoreBlackbox(
                SubmarineGyroFaultEventHash,
                latest.MaxErrorMagnitude,
                latest.StateHash,
                SubmarineGyroFaultDumpHash);
            PublishGyroFaultSignal(in latest);
            _gyroDumpWritten |= written;
            return true;
        }

        private void PublishGyroFaultSignal(in GyroTelemetryEntry latest)
        {
            SystemGlitchSignal signal = default;
            signal.Frame = latest.Frame;
            signal.SourceId = SubmarineDynamicsConstants.SourceHashGyro;
            signal.LocalHash = latest.StateHash;
            signal.ExpectedHash = 0u;
            signal.Intensity01 = math.saturate(latest.MaxErrorMagnitude);
            signal.DurationSeconds = 1.5f;
            signal.Reason = 1;
            signal.Flags = latest.NonFiniteCount > 0u ? (byte)1 : (byte)0;
            if (!SignalBus<SystemGlitchSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SubmarineDynamicsRuntime_Gyroscopes))
                IncrementDroppedSignalCount();
        }

        public bool TryReadGyroTuning(out SubmarineGyroDTO tuning)
        {
            tuning = default;
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryReadOnlyVaultHandle(in _gyroHandle, out NativeArray<SubmarineGyroDTO>.ReadOnly gyros) || gyros.Length == 0)
                return false;

            SubmarineGyroDTO raw = gyros[0];
            tuning = SubmarineGyroMath.Sanitize(in raw);
            return true;
        }

        public bool TryWriteGyroTuning(in SubmarineGyroDTO tuning)
        {
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryAcquireVaultWriteLock(in _gyroHandle, out NativeArray<SubmarineGyroDTO> gyros))
                return false;

            try
            {
                if (gyros.Length == 0)
                    return false;

                SubmarineGyroDTO safe = SubmarineGyroMath.Sanitize(in tuning);
                int count = math.min(gyros.Length, math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles));
                unsafe
                {
                    void* gyroPtr = NativeArrayUnsafeUtility.GetUnsafePtr(gyros);
                    for (int i = 0; i < count; i++)
                    {
                        ref SubmarineGyroDTO slot = ref UnsafeUtility.ArrayElementAsRef<SubmarineGyroDTO>(gyroPtr, i);
                        slot = safe;
                    }
                }

                gyroPitchProportionalGain = safe.ProportionalGainPitch;
                gyroPitchDerivativeGain = safe.DerivativeGainPitch;
                gyroRollProportionalGain = safe.ProportionalGainRoll;
                gyroRollDerivativeGain = safe.DerivativeGainRoll;
                gyroMaxCorrectionTorque = safe.MaxCorrectionTorque;
                enableGyroAutoLevel = (safe.AutoLevelEnabledFlag & SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled) != 0u;
                return true;
            }
            finally
            {
                ReleaseVaultWriteLock(in _gyroHandle);
            }
        }

        public bool TryReadLatestGyroTelemetry(out GyroTelemetryEntry telemetry)
        {
            telemetry = default;
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryReadOnlyVaultHandle(in _gyroTelemetryHandle, out NativeArray<GyroTelemetryEntry>.ReadOnly rows) || rows.Length == 0)
                return false;

            telemetry = rows[(int)(_frameCounter % (uint)rows.Length)];
            return telemetry.Frame != 0u;
        }

        public bool TryReadGyroForcePacketEditorView(out SubmarineGyroForcePacketDTO packet, out SubmarineGyroErrorDTO error)
        {
            packet = default;
            error = default;
            if (!_buffersReady || _buffersLocked || _integratorPending || _dataVault == null)
                return false;

            if (!TryReadOnlyVaultHandle(in _gyroForcePacketHandle, out NativeArray<SubmarineGyroForcePacketDTO>.ReadOnly packets) ||
                !TryReadOnlyVaultHandle(in _gyroErrorHandle, out NativeArray<SubmarineGyroErrorDTO>.ReadOnly errors) ||
                packets.Length == 0 ||
                errors.Length == 0)
            {
                return false;
            }

            packet = packets[0];
            error = errors[0];
            return packet.Frame != 0u || error.Frame != 0u;
        }

        private void SyncGyroVisualBuffer()
        {
            if (!_buffersReady || _dataVault == null || _integratorPending)
                return;

            if (!TryReadOnlyVaultHandle(in _gyroVisualHandle, out NativeArray<SubmarineGyroVisualStateDTO>.ReadOnly visuals) || visuals.Length == 0)
                return;

            int count = math.min(visuals.Length, math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles));
            if (count <= 0)
                return;

            uint uploadFrame = _frameCounter;
            bool hasActiveController = true;
            if (TryReadOnlyVaultHandle(in _gyroCounterHandle, out NativeArray<SubmarineGyroCounterDTO>.ReadOnly counters) && counters.Length > 0)
            {
                SubmarineGyroCounterDTO counter = counters[0];
                uploadFrame = counter.Frame;
                hasActiveController = counter.ActiveControllers > 0;
            }

            if (uploadFrame == _gyroLastVisualUploadFrame)
                return;

            if (!hasActiveController)
            {
                Shader.SetGlobalInt(s_GyroVisualCountId, 0);
                _gyroLastVisualUploadFrame = uploadFrame;
                return;
            }

            GraphicsBuffer writeBuffer = (_gyroVisualBufferWriteIndex & 1) == 0 ? _gyroVisualBufferA : _gyroVisualBufferB;
            if (writeBuffer == null ||
                _gyroVisualBufferCapacity < count ||
                writeBuffer.stride != SubmarineDynamicsConstants.GyroVisualStateBytes)
            {
                return;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, visuals, count);
            Shader.SetGlobalBuffer(s_GyroVisualBufferId, writeBuffer);
            Shader.SetGlobalInt(s_GyroVisualCountId, count);
            _gyroVisualBufferWriteIndex ^= 1;
            _gyroLastVisualUploadFrame = uploadFrame;
        }

        private void DisposeGyroRuntime()
        {
            if (_gyroVisualBufferA != null)
            {
                _gyroVisualBufferA.Release();
                _gyroVisualBufferA = null;
            }

            if (_gyroVisualBufferB != null)
            {
                _gyroVisualBufferB.Release();
                _gyroVisualBufferB = null;
            }

            _gyroVisualBufferCapacity = 0;
            _gyroVisualBufferWriteIndex = 0;
            _gyroLastVisualUploadFrame = 0u;
        }

        private void ReleaseGyroVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _gyroHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroErrorHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroForcePacketHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroTelemetryHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroVisualHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroProfileHandle);
            ReleaseOwnedVaultHandle(vault, ref _gyroCounterHandle);
#if UNITY_EDITOR
            ReleaseOwnedVaultHandle(vault, ref _gyroCsvScratchHandle);
#endif
        }

        private void ClearGyroVaultHandles()
        {
            _gyroHandle = default;
            _gyroErrorHandle = default;
            _gyroForcePacketHandle = default;
            _gyroTelemetryHandle = default;
            _gyroVisualHandle = default;
            _gyroProfileHandle = default;
            _gyroCounterHandle = default;
#if UNITY_EDITOR
            _gyroCsvScratchHandle = default;
#endif
        }

#if UNITY_EDITOR
        private bool TryApplyGyroProfilesCsv()
        {
            IDataVault vault = _dataVault;
            if (!_buffersReady || vault == null || _integratorPending || _buffersLocked)
                return false;

            if (string.IsNullOrEmpty(_gyroProfilesCsvPath) || !File.Exists(_gyroProfilesCsvPath))
                return false;

            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(_gyroProfilesCsvPath).Ticks;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (ticks == _gyroProfilesCsvLastWriteTicks)
                return false;

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                System.Array.Clear(s_gyroCsvScratch, 0, s_gyroCsvScratch.Length);
                System.Array.Clear(s_gyroProfileCsvScratch, 0, s_gyroProfileCsvScratch.Length);

                int read;
                try
                {
                    using FileStream stream = File.Open(_gyroProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long length = stream.Length;
                    if (length <= 0L || length > MaxGyroProfileCsvBytes)
                        return false;

                    read = stream.Read(s_csvImportBytes, 0, (int)length);
                    if (read <= 0)
                        return false;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }

                int parsed = ParseGyroProfilesCsv(
                    s_csvImportBytes.AsSpan(0, read),
                    s_gyroCsvScratch.AsSpan(),
                    s_gyroProfileCsvScratch.AsSpan());
                if (parsed <= 0)
                    return false;

                if (!TryCommitGyroProfilesCsvGyros() ||
                    !TryCommitGyroProfilesCsvProfiles())
                {
                    return false;
                }

                _gyroProfilesCsvLastWriteTicks = ticks;
                return true;
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        private bool TryCommitGyroProfilesCsvGyros()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(GyroProfilesCsvGyroMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _gyroHandle, out NativeArray<SubmarineGyroDTO> gyros) ||
                    gyros.Length == 0)
                {
                    return false;
                }

                int copyLength = math.min(gyros.Length, s_gyroCsvScratch.Length);
                NativeArray<SubmarineGyroDTO>.Copy(s_gyroCsvScratch, 0, gyros, 0, copyLength);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GyroProfilesCsvGyroMutationGuardMask);
            }
        }

        private bool TryCommitGyroProfilesCsvProfiles()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(GyroProfilesCsvProfileMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultHandleForOwner(in _gyroProfileHandle, out NativeArray<SubmarineGyroProfileDTO> profiles) ||
                    profiles.Length == 0)
                {
                    return false;
                }

                int copyLength = math.min(profiles.Length, s_gyroProfileCsvScratch.Length);
                NativeArray<SubmarineGyroProfileDTO>.Copy(s_gyroProfileCsvScratch, 0, profiles, 0, copyLength);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GyroProfilesCsvProfileMutationGuardMask);
            }
        }
#endif

#if UNITY_EDITOR
        private static int ParseGyroProfilesCsv(
            ReadOnlySpan<byte> bytes,
            Span<SubmarineGyroDTO> gyros,
            Span<SubmarineGyroProfileDTO> profiles)
        {
            int cursor = 0;
            int count = 0;
            while (count < gyros.Length && count < profiles.Length && TryReadCsvLine(bytes, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = TrimAscii(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!TryParseGyroProfileLine(line, out SubmarineGyroProfileDTO profile, out SubmarineGyroDTO gyro))
                    continue;

                profiles[count] = profile;
                gyros[count] = gyro;
                count++;
            }

            return count;
        }

        private static bool TryParseGyroProfileLine(ReadOnlySpan<byte> line, out SubmarineGyroProfileDTO profile, out SubmarineGyroDTO gyro)
        {
            profile = default;
            gyro = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = TrimAscii(ReadCsvField(line, ref cursor));
            if (name.Length == 0 || TokenEqualsAsciiLower(name, "name") || TokenEqualsAsciiLower(name, "profile"))
                return false;

            float pitchP = ReadCsvFloat(line, ref cursor, 0f);
            float pitchD = ReadCsvFloat(line, ref cursor, 0f);
            float rollP = ReadCsvFloat(line, ref cursor, 0f);
            float rollD = ReadCsvFloat(line, ref cursor, 0f);
            float maxTorque = ReadCsvFloat(line, ref cursor, 0f);
            float enabled = ReadCsvFloat(line, ref cursor, 1f);
            if (maxTorque <= 0f || !math.isfinite(maxTorque))
                return false;

            gyro.ProportionalGainPitch = math.max(0f, pitchP);
            gyro.DerivativeGainPitch = math.max(0f, pitchD);
            gyro.ProportionalGainRoll = math.max(0f, rollP);
            gyro.DerivativeGainRoll = math.max(0f, rollD);
            gyro.MaxCorrectionTorque = math.max(1f, maxTorque);
            gyro.AutoLevelEnabledFlag = enabled > 0.001f ? SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled : 0u;

            profile.ProfileHash = HashAsciiLower(name);
            profile.ProportionalGainPitch = gyro.ProportionalGainPitch;
            profile.DerivativeGainPitch = gyro.DerivativeGainPitch;
            profile.ProportionalGainRoll = gyro.ProportionalGainRoll;
            profile.DerivativeGainRoll = gyro.DerivativeGainRoll;
            profile.MaxCorrectionTorque = gyro.MaxCorrectionTorque;
            profile.Flags = gyro.AutoLevelEnabledFlag;
            return profile.ProfileHash != 0u;
        }
#endif

#if UNITY_EDITOR
        private void DrawGyroDebugGizmos()
        {
            if (!TryReadGyroForcePacketEditorView(out SubmarineGyroForcePacketDTO packet, out SubmarineGyroErrorDTO error))
                return;

            if (!math.all(math.isfinite(packet.CorrectiveTorque)) || !math.all(math.isfinite(error.ErrorVector)))
                return;

            Vector3 origin = new Vector3((float)packet.CurrentAup.x, (float)packet.CurrentAup.y, (float)packet.CurrentAup.z);
            if (float.IsNaN(origin.x) || float.IsInfinity(origin.x) ||
                float.IsNaN(origin.y) || float.IsInfinity(origin.y) ||
                float.IsNaN(origin.z) || float.IsInfinity(origin.z))
            {
                origin = visualRoot != null ? visualRoot.position : transform.position;
            }

            float torqueScale = math.saturate(packet.TorqueMagnitude / math.max(1f, gyroMaxCorrectionTorque)) * 6f;
            float3 torqueDir = SubmarineDynamicsSimdMath.NormalizeOrFallback(packet.CorrectiveTorque, float3.zero);
            Vector3 torqueEnd = origin + new Vector3(torqueDir.x, torqueDir.y, torqueDir.z) * torqueScale;
            Vector3 errorEnd = origin + new Vector3(error.ErrorVector.x, error.ErrorVector.y, error.ErrorVector.z) * 4f;

            Gizmos.color = new Color(1f, 0.9f, 0.05f, 0.95f);
            Gizmos.DrawLine(origin, torqueEnd);
            Gizmos.DrawWireSphere(torqueEnd, 0.18f);
            Gizmos.color = new Color(1f, 0.05f, 0.05f, 0.35f);
            Gizmos.DrawLine(origin, errorEnd);
            Gizmos.DrawWireSphere(errorEnd, 0.12f);
        }
#endif
    }
}
