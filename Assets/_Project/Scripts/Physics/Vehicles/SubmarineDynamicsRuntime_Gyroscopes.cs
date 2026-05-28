using System;
#if UNITY_EDITOR
using System.IO;
#endif
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
        private bool _gyroTuningReadPinHeld;
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
            bool gyroLocked = false;
            bool profileLocked = false;
            bool counterLocked = false;
            try
            {
                if (!TryAcquireVaultWriteLock(in _gyroHandle, out NativeArray<SubmarineGyroDTO> gyros))
                    return false;
                gyroLocked = true;

                if (!TryAcquireVaultWriteLock(in _gyroProfileHandle, out NativeArray<SubmarineGyroProfileDTO> profiles))
                    return false;
                profileLocked = true;

                if (!TryAcquireVaultWriteLock(in _gyroCounterHandle, out NativeArray<SubmarineGyroCounterDTO> counters))
                    return false;
                counterLocked = true;

                int count = math.min(math.max(0, capacity), math.min(gyros.Length, profiles.Length));
                for (int i = 0; i < count; i++)
                {
                    SubmarineGyroDTO gyro = gyros[i];
                    if (gyro.MaxCorrectionTorque <= 0f || !math.isfinite(gyro.MaxCorrectionTorque))
                    {
                        gyro = BuildSerializedGyro();
                        gyros[i] = gyro;
                    }

                    SubmarineGyroProfileDTO profile = profiles[i];
                    if (profile.ProfileHash == 0u)
                    {
                        profile.ProfileHash = SubmarineDynamicsConstants.SourceHashGyro ^ (uint)i;
                        profile.ProportionalGainPitch = gyro.ProportionalGainPitch;
                        profile.DerivativeGainPitch = gyro.DerivativeGainPitch;
                        profile.ProportionalGainRoll = gyro.ProportionalGainRoll;
                        profile.DerivativeGainRoll = gyro.DerivativeGainRoll;
                        profile.MaxCorrectionTorque = gyro.MaxCorrectionTorque;
                        profile.Flags = gyro.AutoLevelEnabledFlag;
                        profiles[i] = profile;
                    }
                }

                if (counters.Length > 0 && counters[0].Frame == 0u)
                    counters[0] = default;

                return true;
            }
            finally
            {
                if (counterLocked)
                    ReleaseVaultWriteLock(in _gyroCounterHandle);
                if (profileLocked)
                    ReleaseVaultWriteLock(in _gyroProfileHandle);
                if (gyroLocked)
                    ReleaseVaultWriteLock(in _gyroHandle);
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

            return TryResolveVaultHandle(in _gyroHandle, out gyros) &&
                   TryResolveVaultHandle(in _gyroErrorHandle, out errors) &&
                   TryResolveVaultHandle(in _gyroForcePacketHandle, out packets) &&
                   TryResolveVaultHandle(in _gyroTelemetryHandle, out telemetry) &&
                   TryResolveVaultHandle(in _gyroVisualHandle, out visuals) &&
                   TryResolveVaultHandle(in _gyroCounterHandle, out counters);
        }

        private bool TryLockGyroBuffers()
        {
            bool locked = false;
            try
            {
                _gyroTuningReadPinHeld = false;
                if (!TryAcquireVaultReadPin(in _gyroHandle, BufferID.Shinobu332SubmarineGyros, math.clamp(vehicleCapacity, 1, SubmarineDynamicsConstants.MaxVehicles), out _) ||
                    !TryMarkGyroTuningReadPin() ||
                    !TryAcquireVaultWriteLock(in _gyroErrorHandle, out _) ||
                    !TryAcquireVaultWriteLock(in _gyroForcePacketHandle, out _) ||
                    !TryAcquireVaultWriteLock(in _gyroTelemetryHandle, out _) ||
                    !TryAcquireVaultWriteLock(in _gyroVisualHandle, out _) ||
                    !TryAcquireVaultWriteLock(in _gyroCounterHandle, out _))
                {
                    return false;
                }

                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    UnlockGyroBuffers();
            }
        }

        private bool TryMarkGyroTuningReadPin()
        {
            _gyroTuningReadPinHeld = true;
            return true;
        }

        private void UnlockGyroBuffers()
        {
            ReleaseVaultWriteLock(in _gyroErrorHandle);
            ReleaseVaultWriteLock(in _gyroForcePacketHandle);
            ReleaseVaultWriteLock(in _gyroTelemetryHandle);
            ReleaseVaultWriteLock(in _gyroVisualHandle);
            ReleaseVaultWriteLock(in _gyroCounterHandle);
            if (_gyroTuningReadPinHeld)
            {
                ReleaseVaultReadPin(BufferID.Shinobu332SubmarineGyros);
                _gyroTuningReadPinHeld = false;
            }
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

            if (!TryResolveVaultHandle(in _gyroTelemetryHandle, out NativeArray<GyroTelemetryEntry> telemetry) || telemetry.Length == 0)
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

            NativeArray<SubmarineGyroVisualStateDTO> mapped = writeBuffer.LockBufferForWrite<SubmarineGyroVisualStateDTO>(0, count);
            try
            {
                unsafe
                {
                    void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(visuals);
                    void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                    UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)SubmarineDynamicsConstants.GyroVisualStateBytes * count);
                }
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<SubmarineGyroVisualStateDTO>(count);
            }
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
            if (!_buffersReady || _dataVault == null || _integratorPending || _buffersLocked)
                return false;

            if (string.IsNullOrEmpty(_gyroProfilesCsvPath) || !File.Exists(_gyroProfilesCsvPath))
                return false;

            FileInfo info;
            try
            {
                info = new FileInfo(_gyroProfilesCsvPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (info.Length <= 0L || info.Length > MaxGyroProfileCsvBytes || info.LastWriteTimeUtc.Ticks == _gyroProfilesCsvLastWriteTicks)
                return false;

            bool gyroLocked = false;
            bool profileLocked = false;
            bool scratchLocked = false;
            try
            {
                if (!TryAcquireVaultWriteLock(in _gyroHandle, out NativeArray<SubmarineGyroDTO> gyros))
                    return false;
                gyroLocked = true;

                if (!TryAcquireVaultWriteLock(in _gyroProfileHandle, out NativeArray<SubmarineGyroProfileDTO> profiles))
                    return false;
                profileLocked = true;

                if (!TryAcquireVaultWriteLock(in _gyroCsvScratchHandle, out NativeArray<byte> scratchBytes))
                    return false;
                scratchLocked = true;

                int length = (int)info.Length;
                if (scratchBytes.Length < length)
                    return false;

                unsafe
                {
                    byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchBytes);
                    Span<byte> scratch = new Span<byte>(scratchPtr, length);
                    using (FileStream stream = new FileStream(_gyroProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))
                    {
                        int read = stream.Read(scratch);
                        if (read <= 0)
                            return false;

                        int parsed = ParseGyroProfilesCsv(new ReadOnlySpan<byte>(scratchPtr, read), gyros, profiles);
                        if (parsed <= 0)
                            return false;
                    }
                }

                _gyroProfilesCsvLastWriteTicks = info.LastWriteTimeUtc.Ticks;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (scratchLocked)
                    ReleaseVaultWriteLock(in _gyroCsvScratchHandle);
                if (profileLocked)
                    ReleaseVaultWriteLock(in _gyroProfileHandle);
                if (gyroLocked)
                    ReleaseVaultWriteLock(in _gyroHandle);
            }
        }
#endif

#if UNITY_EDITOR
        private static int ParseGyroProfilesCsv(
            ReadOnlySpan<byte> bytes,
            NativeArray<SubmarineGyroDTO> gyros,
            NativeArray<SubmarineGyroProfileDTO> profiles)
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
