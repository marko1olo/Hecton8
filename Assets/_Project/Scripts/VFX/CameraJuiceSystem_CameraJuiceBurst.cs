// ============================================================================
// SHINOBU_354 - procedural camera shake impulse.
// Burst damped sinusoids + AUP epicenter trauma. Runtime camera hierarchy stays static.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.VFX
{
    public sealed unsafe partial class CameraJuiceSystem
    {
        private const int CameraJuiceProfileCapacity = 16;
        private const int CameraJuiceMockSignalCapacity = 32;
#if UNITY_EDITOR
        private const int CameraJuiceCsvScratchBytes = 4096;
        private const string CameraJuiceTraumaProfilesFileName = "camera_trauma_profiles.csv";
#endif
        private const BufferID CameraJuiceStateBufferId = (BufferID)73373;
        private const BufferID CameraJuiceImpulseBufferId = (BufferID)73374;
        private const BufferID CameraJuiceProjectionBufferId = (BufferID)73375;
        private const BufferID CameraJuiceTuningBufferId = (BufferID)73376;
        private const BufferID CameraJuiceProfilesBufferId = (BufferID)73377;
        private const BufferID CameraJuiceMockSignalsBufferId = (BufferID)73378;
#if UNITY_EDITOR
        private const BufferID CameraJuiceCsvScratchBufferId = (BufferID)73379;
#endif
        private const float CameraJuiceProjectionTranslationScale = 0.035f;
        private const float CameraJuiceProjectionRotationScale = 0.00125f;
        private const float CameraJuiceProjectionRollScale = 0.0015f;
        private const float CameraJuiceBurstBudgetMicroseconds = 100f;
        private const uint CameraJuiceFlagXrSuppressed = 1u << 0;
        private const uint CameraJuiceFlagNanSanitized = 1u << 1;
        private const uint CameraJuiceFlagNoPlayerAup = 1u << 2;
        private const uint CameraJuiceFlagVRSomaticWriteRejected = 1u << 3;
        private const uint CameraJuiceFlagVaultUnavailable = 1u << 4;
        private const uint CameraJuiceFlagBurstBudgetExceeded = 1u << 5;

        private VaultGenerationHandle<CameraJuiceStateDTO> _cameraJuiceStateHandle;
        private VaultGenerationHandle<CameraJuiceImpulseDTO> _cameraJuiceImpulseHandle;
        private VaultGenerationHandle<CameraJuiceProjectionDTO> _cameraJuiceProjectionHandle;
        private VaultGenerationHandle<CameraJuiceTuningDTO> _cameraJuiceTuningHandle;
        private VaultGenerationHandle<CameraTraumaProfileDTO> _cameraJuiceProfilesHandle;
        private VaultGenerationHandle<CameraJuiceMockSignalDTO> _cameraJuiceMockSignalsHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _cameraJuiceCsvScratchHandle;
#endif
        private VaultGenerationHandle<LockstepPlayerKinematicState> _cameraJuicePlayerKinematicStateHandle;
        private bool _ownsCameraJuiceStateBuffer;
        private bool _ownsCameraJuiceImpulseBuffer;
        private bool _ownsCameraJuiceProjectionBuffer;
        private bool _ownsCameraJuiceTuningBuffer;
        private bool _ownsCameraJuiceProfilesBuffer;
        private bool _ownsCameraJuiceMockSignalsBuffer;
#if UNITY_EDITOR
        private bool _ownsCameraJuiceCsvScratchBuffer;
#endif
        private bool _cameraJuiceBuffersSeeded;
        private bool _cameraJuiceMockSignalsEnabled;
        private int _cameraJuiceMockSignalCount;
        private float _cameraJuiceMockSeverity01 = 0.65f;
        private float _cameraJuiceMockRadiusMeters = 18f;
        private float _cameraJuiceManualTrauma01;
        private float3 _cameraJuiceManualDirectionalImpulseLocal;
        private float3 _cameraJuiceProjectionTranslation;
        private float3 _cameraJuiceProjectionRotationDegrees;
        private bool _cameraJuiceProjectionDirty;
        private float _cameraJuiceLastTraumaScalar;
        private float _cameraJuiceLastMaxTranslationMagnitude;
        private float _cameraJuiceLastBurstExecutionMicros;
        private float _cameraJuiceLastQualityWeight = 1f;
        private float _cameraJuiceLastDirectionalImpulseMagnitude;
        private int _cameraJuiceLastIncomingSignalCount;
        private uint _cameraJuiceLastStateHash;
        private uint _cameraJuiceLastFlags;
        private uint _cameraJuiceSequence;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct CameraJuiceStateDTO
        {
            [FieldOffset(0)] public float3 CurrentTranslationalOffset;
            [FieldOffset(12)] public float3 CurrentRotationalOffset;
            [FieldOffset(24)] public float TraumaScalar;
            [FieldOffset(28)] public float TimeAccumulator;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CameraJuiceImpulseDTO
        {
            [FieldOffset(0)] public float3 DirectionalImpulse;
            [FieldOffset(12)] public float TraumaDelta;
            [FieldOffset(16)] public float3 DirectionalMemory;
            [FieldOffset(28)] public float DirectionalTimer;
            [FieldOffset(32)] public int SignalCount;
            [FieldOffset(36)] public uint Flags;
            [FieldOffset(40)] public float MaxSignalMagnitude;
            [FieldOffset(44)] public float DistanceAttenuation;
            [FieldOffset(48)] public uint Sequence;
            [FieldOffset(52)] public float Reserved0;
            [FieldOffset(56)] public uint Reserved1;
            [FieldOffset(60)] public uint Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CameraJuiceProjectionDTO
        {
            [FieldOffset(0)] public float3 TranslationOffset;
            [FieldOffset(12)] public float3 RotationDegrees;
            [FieldOffset(24)] public float TraumaScalar;
            [FieldOffset(28)] public float MaxTranslationMagnitude;
            [FieldOffset(32)] public quaternion ComfortRotation;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint StateHash;
            [FieldOffset(56)] public float GlobalQualityWeight01;
            [FieldOffset(60)] public float DirectionalImpulseMagnitude;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CameraJuiceTuningDTO
        {
            [FieldOffset(0)] public float MaxTranslationMeters;
            [FieldOffset(4)] public float MaxRotationDegrees;
            [FieldOffset(8)] public float MaxRollDegrees;
            [FieldOffset(12)] public float TraumaDecayPerSecond;
            [FieldOffset(16)] public float BaseFrequencyHz;
            [FieldOffset(20)] public float DirectionalBiasSeconds;
            [FieldOffset(24)] public float ProjectionTranslationScale;
            [FieldOffset(28)] public float ProjectionRotationScale;
            [FieldOffset(32)] public float LowTierRadiusMeters;
            [FieldOffset(36)] public float UltraRadiusMeters;
            [FieldOffset(40)] public float HighOctaveGain;
            [FieldOffset(44)] public float QualityWeight01;
            [FieldOffset(48)] public uint ProfileCount;
            [FieldOffset(52)] public uint Flags;
            [FieldOffset(56)] public uint Reserved0;
            [FieldOffset(60)] public uint Reserved1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct CameraTraumaProfileDTO
        {
            [FieldOffset(0)] public uint ProfileHash;
            [FieldOffset(4)] public float TranslationGain;
            [FieldOffset(8)] public float RotationGain;
            [FieldOffset(12)] public float RadiusMeters;
            [FieldOffset(16)] public float DecayPerSecond;
            [FieldOffset(20)] public float FrequencyHz;
            [FieldOffset(24)] public uint Flags;
            [FieldOffset(28)] public uint Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CameraJuiceMockSignalDTO
        {
            [FieldOffset(0)] public double3 EpicenterAup;
            [FieldOffset(24)] public float3 Direction;
            [FieldOffset(36)] public float Severity01;
            [FieldOffset(40)] public float RadiusMeters;
            [FieldOffset(44)] public uint Frame;
            [FieldOffset(48)] public uint Seed;
            [FieldOffset(52)] public uint Flags;
            [FieldOffset(56)] public uint Reserved0;
            [FieldOffset(60)] public uint Reserved1;
        }

        private bool EnsureProceduralCameraJuiceBuffers()
        {
#if UNITY_EDITOR
            if (CameraJuiceBurstMath.ValidateLayoutSizes() != 0u)
                Hecton8.Core.H8Debug.LogError("[SHINOBU_354] Camera juice ABI violation.");
#endif
            NativeArray<CameraJuiceStateDTO> state = default;
            NativeArray<CameraJuiceImpulseDTO> impulse = default;
            NativeArray<CameraJuiceProjectionDTO> projection = default;
            NativeArray<CameraJuiceTuningDTO> tuning = default;
            NativeArray<CameraTraumaProfileDTO> profiles = default;
            NativeArray<CameraJuiceMockSignalDTO> mockSignals = default;
#if UNITY_EDITOR
            NativeArray<byte> cameraJuiceCsvScratch = default;
#endif
            bool ready =
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceStateHandle,
                    ref _ownsCameraJuiceStateBuffer,
                    CameraJuiceStateBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out state) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceImpulseHandle,
                    ref _ownsCameraJuiceImpulseBuffer,
                    CameraJuiceImpulseBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out impulse) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceProjectionHandle,
                    ref _ownsCameraJuiceProjectionBuffer,
                    CameraJuiceProjectionBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out projection) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceTuningHandle,
                    ref _ownsCameraJuiceTuningBuffer,
                    CameraJuiceTuningBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out tuning) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceProfilesHandle,
                    ref _ownsCameraJuiceProfilesBuffer,
                    CameraJuiceProfilesBufferId,
                    CameraJuiceProfileCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out profiles) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceMockSignalsHandle,
                    ref _ownsCameraJuiceMockSignalsBuffer,
                    CameraJuiceMockSignalsBufferId,
                    CameraJuiceMockSignalCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out mockSignals);
#if UNITY_EDITOR
            ready = ready &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceCsvScratchHandle,
                    ref _ownsCameraJuiceCsvScratchBuffer,
                    CameraJuiceCsvScratchBufferId,
                    CameraJuiceCsvScratchBytes,
                    NativeArrayOptions.UninitializedMemory,
                    out cameraJuiceCsvScratch);
#endif

            if (!ready)
                return false;

            if (!_cameraJuiceBuffersSeeded)
            {
#if UNITY_EDITOR
                SeedProceduralCameraJuiceBuffers(state, impulse, projection, tuning, profiles, mockSignals, cameraJuiceCsvScratch);
#else
                SeedProceduralCameraJuiceBuffers(state, impulse, projection, tuning, profiles, mockSignals);
#endif
                _cameraJuiceBuffersSeeded = true;
            }

            return true;
        }

        private void ReleaseProceduralCameraJuiceBuffers()
        {
            ReleaseCameraJuiceBuffer(ref _cameraJuiceStateHandle, ref _ownsCameraJuiceStateBuffer);
            ReleaseCameraJuiceBuffer(ref _cameraJuiceImpulseHandle, ref _ownsCameraJuiceImpulseBuffer);
            ReleaseCameraJuiceBuffer(ref _cameraJuiceProjectionHandle, ref _ownsCameraJuiceProjectionBuffer);
            ReleaseCameraJuiceBuffer(ref _cameraJuiceTuningHandle, ref _ownsCameraJuiceTuningBuffer);
            ReleaseCameraJuiceBuffer(ref _cameraJuiceProfilesHandle, ref _ownsCameraJuiceProfilesBuffer);
            ReleaseCameraJuiceBuffer(ref _cameraJuiceMockSignalsHandle, ref _ownsCameraJuiceMockSignalsBuffer);
#if UNITY_EDITOR
            ReleaseCameraJuiceBuffer(ref _cameraJuiceCsvScratchHandle, ref _ownsCameraJuiceCsvScratchBuffer);
#endif
            _cameraJuicePlayerKinematicStateHandle = default;
            _cameraJuiceBuffersSeeded = false;
            ClearProceduralCameraJuiceProjection();
        }

        private void RefreshCameraJuiceColdVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _cameraJuicePlayerKinematicStateHandle = default;
                return;
            }

            if (_cameraJuicePlayerKinematicStateHandle.BufferID != 0u &&
                _cameraJuicePlayerKinematicStateHandle.Generation != 0u &&
                vault.TryReadOnlyHandle(in _cameraJuicePlayerKinematicStateHandle, out NativeArray<LockstepPlayerKinematicState>.ReadOnly cachedStates) &&
                cachedStates.IsCreated &&
                cachedStates.Length > 0)
            {
                return;
            }

            if (vault.TryGetGenerationHandle<LockstepPlayerKinematicState>(
                    BufferID.PlayerKinematicState,
                    out VaultGenerationHandle<LockstepPlayerKinematicState> playerHandle))
            {
                _cameraJuicePlayerKinematicStateHandle = playerHandle;
                return;
            }

            _cameraJuicePlayerKinematicStateHandle = default;
        }

        private bool AcquireCameraJuiceBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            ref bool ownsHandle,
            BufferID bufferId,
            int count,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || count <= 0)
                return false;

            if (handle.BufferID != 0u &&
                handle.Generation != 0u &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= count)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> borrowedHandle) &&
                vault.TryResolveHandle(in borrowedHandle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= count)
            {
                handle = borrowedHandle;
                ownsHandle = false;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> acquiredHandle = vault.EnsureGenerationHandle<T>(
                bufferId,
                count,
                SystemID.Vfx,
                options);
            if (acquiredHandle.BufferID == 0u ||
                acquiredHandle.Generation == 0u ||
                !vault.TryResolveHandle(in acquiredHandle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < count)
            {
                if (acquiredHandle.BufferID != 0u && acquiredHandle.Generation != 0u)
                    vault.ReleaseBuffer(in acquiredHandle);
                return false;
            }

            handle = acquiredHandle;
            ownsHandle = true;
            return true;
        }

        private void ReleaseCameraJuiceBuffer<T>(ref VaultGenerationHandle<T> handle, ref bool ownsHandle)
            where T : unmanaged
        {
            IDataVault vault = _dataVault;
            if (ownsHandle && vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
            ownsHandle = false;
        }

        private void SeedProceduralCameraJuiceBuffers(
            NativeArray<CameraJuiceStateDTO> state,
            NativeArray<CameraJuiceImpulseDTO> impulse,
            NativeArray<CameraJuiceProjectionDTO> projection,
            NativeArray<CameraJuiceTuningDTO> tuning,
            NativeArray<CameraTraumaProfileDTO> profiles,
            NativeArray<CameraJuiceMockSignalDTO> mockSignals
#if UNITY_EDITOR
            , NativeArray<byte> cameraJuiceCsvScratch
#endif
            )
        {
            float quality = ResolveCameraJuiceGlobalQualityWeight();
            SeedCameraJuiceBuffersJob job = default;
            job.State = state;
            job.Impulse = impulse;
            job.Projection = projection;
            job.Tuning = tuning;
            job.Profiles = profiles;
            job.MockSignals = mockSignals;
            job.GlobalQualityWeight01 = quality;
            job.Execute();

#if UNITY_EDITOR
            TryLoadCameraJuiceTraumaProfilesFromCsv(profiles, cameraJuiceCsvScratch, tuning);
#endif
        }

        private void InitializeCameraJuiceTelemetryRing(NativeArray<CameraJuiceTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length < CAMERA_JUICE_TELEMETRY_CAPACITY)
                return;

            InitializeCameraJuiceTelemetryJob job = default;
            job.Telemetry = telemetry;
            job.Execute();
        }

        private void RunProceduralCameraJuice(float dt, float effectiveShakeScale)
        {
            _cameraJuiceLastFlags = 0u;
            if (!_cameraJuiceBuffersSeeded)
            {
                FailClosedProceduralCameraJuiceFrame(CameraJuiceFlagVaultUnavailable);
                return;
            }

            if (!TryResolvePlayerCameraJuiceAup(out double3 playerAup))
            {
                FailClosedProceduralCameraJuiceFrame(CameraJuiceFlagNoPlayerAup);
                return;
            }

            if (!OpenCameraJuiceBuffer(in _cameraJuiceStateHandle, 1, out NativeArray<CameraJuiceStateDTO> state) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceImpulseHandle, 1, out NativeArray<CameraJuiceImpulseDTO> impulse) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceProjectionHandle, 1, out NativeArray<CameraJuiceProjectionDTO> projection) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceTuningHandle, 1, out NativeArray<CameraJuiceTuningDTO> tuning) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceMockSignalsHandle, CameraJuiceMockSignalCapacity, out NativeArray<CameraJuiceMockSignalDTO> mockSignals))
            {
                FailClosedProceduralCameraJuiceFrame(CameraJuiceFlagVaultUnavailable);
                return;
            }

            float quality = ResolveCameraJuiceGlobalQualityWeight();
            ref CameraJuiceTuningDTO tuningValue = ref UnsafeUtility.AsRef<CameraJuiceTuningDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
            tuningValue.QualityWeight01 = quality;
            uint frameSequence = _cameraJuiceSequence;

            int mockCount = 0;
            if (_cameraJuiceMockSignalsEnabled)
            {
                uint seed = math.hash(new uint2(frameSequence, 0x53483335u));
                GenerateMockTraumaSpikesJob mockJob = default;
                mockJob.MockSignals = mockSignals;
                mockJob.PlayerAup = playerAup;
                mockJob.Frame = frameSequence;
                mockJob.Seed = seed;
                mockJob.Severity01 = math.saturate(_cameraJuiceMockSeverity01);
                mockJob.RadiusMeters = math.max(1f, _cameraJuiceMockRadiusMeters);
                mockJob.Execute();
                mockCount = math.clamp(_cameraJuiceMockSignalCount <= 0 ? 4 : _cameraJuiceMockSignalCount, 1, CameraJuiceMockSignalCapacity);
            }

            ResolveCameraBasis(out float3 right, out float3 up, out float3 forward);

            long startTicks = Stopwatch.GetTimestamp();
            EvaluateCameraTraumaJob evaluateJob = default;
            evaluateJob.ImpactSignals = SignalBus<ImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.HighSpeedImpactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.CombatDamageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray();
            evaluateJob.SeismicSignals = SignalBus<SeismicSignal>.GetFrameSnapshotArray();
            evaluateJob.CameraImpactSignals = SignalBus<CameraJuiceImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.MockSignals = mockSignals.AsReadOnly();
            evaluateJob.Tuning = tuning.AsReadOnly();
            evaluateJob.Impulse = impulse;
            evaluateJob.PlayerAup = playerAup;
            evaluateJob.CameraRight = right;
            evaluateJob.CameraUp = up;
            evaluateJob.CameraForward = forward;
            evaluateJob.ManualTrauma01 = _cameraJuiceManualTrauma01;
            evaluateJob.ManualDirectionalImpulseLocal = _cameraJuiceManualDirectionalImpulseLocal;
            evaluateJob.GlobalQualityWeight01 = quality;
            evaluateJob.MockSignalCount = mockCount;
            evaluateJob.Frame = frameSequence;
            evaluateJob.MaxSignalsPerFrame = PROCEDURAL_MAX_IMPACTS_PER_FRAME;
            evaluateJob.Execute();

            IntegrateProceduralShakeJob integrateJob = default;
            integrateJob.State = state;
            integrateJob.Impulse = impulse;
            integrateJob.Projection = projection;
            integrateJob.Tuning = tuning.AsReadOnly();
            integrateJob.DeltaTime = math.clamp(math.isfinite(dt) ? dt : 0f, 0f, 0.1f);
            integrateJob.EffectiveShakeScale = effectiveShakeScale;
            integrateJob.GlobalQualityWeight01 = quality;
            integrateJob.XrActive = HectonXRRuntimeState.IsXRActive ? 1u : 0u;
            integrateJob.Sequence = frameSequence;
            integrateJob.Execute();
            _cameraJuiceSequence = frameSequence + 1u;
            _cameraJuiceLastBurstExecutionMicros = (float)((Stopwatch.GetTimestamp() - startTicks) * 1000000.0 / Stopwatch.Frequency);
            if (_cameraJuiceLastBurstExecutionMicros > CameraJuiceBurstBudgetMicroseconds)
            {
                CameraJuiceProjectionDTO projectionValue = projection[0];
                projectionValue.Flags |= CameraJuiceFlagBurstBudgetExceeded;
                projection[0] = projectionValue;
                _cameraJuiceTelemetryDumpRequested = true;
            }

            ClearPendingProceduralCameraJuiceManualImpulse();

            PublishCameraJuiceStateFromNative(state, impulse, projection, quality);
        }

        private bool OpenCameraJuiceBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount,
            out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredCount;
        }

        private bool OpenCameraJuiceBufferReadOnly<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount,
            out NativeArray<T>.ReadOnly buffer)
            where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredCount;
        }

        private void PublishCameraJuiceStateFromNative(
            NativeArray<CameraJuiceStateDTO> state,
            NativeArray<CameraJuiceImpulseDTO> impulse,
            NativeArray<CameraJuiceProjectionDTO> projection,
            float quality)
        {
            CameraJuiceStateDTO stateValue = state[0];
            CameraJuiceImpulseDTO impulseValue = impulse[0];
            CameraJuiceProjectionDTO projectionValue = projection[0];
            bool invalid =
                !math.all(math.isfinite(stateValue.CurrentTranslationalOffset)) ||
                !math.all(math.isfinite(stateValue.CurrentRotationalOffset)) ||
                !math.isfinite(stateValue.TraumaScalar) ||
                !math.all(math.isfinite(projectionValue.TranslationOffset)) ||
                !math.all(math.isfinite(projectionValue.RotationDegrees));

            if (invalid)
            {
                _cameraJuiceLastFlags |= CameraJuiceFlagNanSanitized;
                stateValue.CurrentTranslationalOffset = float3.zero;
                stateValue.CurrentRotationalOffset = float3.zero;
                stateValue.TraumaScalar = 0f;
                state[0] = stateValue;
                projectionValue.TranslationOffset = float3.zero;
                projectionValue.RotationDegrees = float3.zero;
                projectionValue.TraumaScalar = 0f;
                projectionValue.Flags |= CameraJuiceFlagNanSanitized;
                projection[0] = projectionValue;
            }

            _cameraJuiceProjectionTranslation = projectionValue.TranslationOffset;
            _cameraJuiceProjectionRotationDegrees = projectionValue.RotationDegrees;
            _cameraJuiceProjectionDirty = math.lengthsq(_cameraJuiceProjectionTranslation) > 0.0000001f ||
                                          math.lengthsq(_cameraJuiceProjectionRotationDegrees) > 0.0000001f;
            _proceduralShakeTranslation = stateValue.CurrentTranslationalOffset;
            _proceduralShakeRotationDegrees = stateValue.CurrentRotationalOffset;
            _shakeOffset = Vector3.zero;
            _trauma = stateValue.TraumaScalar;
            _cameraJuiceLastTraumaScalar = math.saturate(stateValue.TraumaScalar);
            _cameraJuiceLastMaxTranslationMagnitude = math.length(stateValue.CurrentTranslationalOffset);
            _cameraJuiceLastQualityWeight = quality;
            _cameraJuiceLastIncomingSignalCount = impulseValue.SignalCount;
            _cameraJuiceLastDirectionalImpulseMagnitude = math.length(impulseValue.DirectionalMemory);
            _cameraJuiceLastStateHash = projectionValue.StateHash;
            _cameraJuiceLastFlags |= projectionValue.Flags | CameraJuiceFlagVRSomaticWriteRejected;
            if (invalid)
            {
                RecordCameraJuiceTelemetry();
                DumpCameraJuiceTelemetry();
            }
        }

        private void PublishProceduralCameraJuiceProjection()
        {
            if (_cameraJuiceProjectionDirty)
                ApplyCameraJuiceProjectionToCamera();
        }

        private void ClearProceduralCameraJuiceProjection()
        {
            _shakeOffset = Vector3.zero;
            _cameraJuiceProjectionTranslation = float3.zero;
            _cameraJuiceProjectionRotationDegrees = float3.zero;
            _cameraJuiceProjectionDirty = false;
            _proceduralShakeTranslation = float3.zero;
            _proceduralShakeRotationDegrees = float3.zero;
            _cameraJuiceLastTraumaScalar = 0f;
            _cameraJuiceLastMaxTranslationMagnitude = 0f;
            _cameraJuiceLastIncomingSignalCount = 0;
            _cameraJuiceLastDirectionalImpulseMagnitude = 0f;
            _cameraJuiceLastStateHash = 0u;
            if (_mainCamera != null && !_mainCamera.orthographic)
                ApplyCameraJuiceProjectionToCamera();
        }

        private void FailClosedProceduralCameraJuiceFrame(uint flags)
        {
            _cameraJuiceLastFlags |= flags | CameraJuiceFlagVRSomaticWriteRejected;
            ClearPendingProceduralCameraJuiceManualImpulse();
            ClearProceduralCameraJuiceProjection();
            ClearProceduralCameraJuiceNativeState(flags);
        }

        private void FailClosedProceduralCameraJuiceFault()
        {
            uint flags = CameraJuiceFlagNanSanitized | CameraJuiceFlagVRSomaticWriteRejected;
            _cameraJuiceLastFlags |= flags;
            ClearPendingProceduralCameraJuiceManualImpulse();
            ClearProceduralCameraJuiceProjection();
            ClearProceduralCameraJuiceNativeState(flags);
            RecordCameraJuiceTelemetry();
            DumpCameraJuiceTelemetry();
        }

        private void ClearPendingProceduralCameraJuiceManualImpulse()
        {
            _cameraJuiceManualTrauma01 = 0f;
            _cameraJuiceManualDirectionalImpulseLocal = float3.zero;
        }

        private void ClearProceduralCameraJuiceNativeState(uint flags)
        {
            if (!OpenCameraJuiceBuffer(in _cameraJuiceStateHandle, 1, out NativeArray<CameraJuiceStateDTO> state) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceImpulseHandle, 1, out NativeArray<CameraJuiceImpulseDTO> impulse) ||
                !OpenCameraJuiceBuffer(in _cameraJuiceProjectionHandle, 1, out NativeArray<CameraJuiceProjectionDTO> projection))
            {
                return;
            }

            state[0] = default;
            impulse[0] = default;
            projection[0] = new CameraJuiceProjectionDTO
            {
                ComfortRotation = quaternion.identity,
                Flags = flags | CameraJuiceFlagVRSomaticWriteRejected,
                GlobalQualityWeight01 = _cameraJuiceLastQualityWeight
            };
        }

        private void ApplyCameraJuiceProjectionToCamera()
        {
            if (_mainCamera == null || _mainCamera.orthographic)
                return;

            Matrix4x4 projection = Matrix4x4.Perspective(
                _mainCamera.fieldOfView,
                _mainCamera.aspect,
                _mainCamera.nearClipPlane,
                _mainCamera.farClipPlane);
            ApplyCameraJuiceProjectionOffset(ref projection);
            _mainCamera.projectionMatrix = projection;
        }

        private void ApplyCameraJuiceProjectionOffset(ref Matrix4x4 projection)
        {
            if (!_cameraJuiceProjectionDirty || HectonXRRuntimeState.IsXRActive)
                return;

            float2 translation = new float2(
                _cameraJuiceProjectionTranslation.x,
                _cameraJuiceProjectionTranslation.y) * CameraJuiceProjectionTranslationScale;
            float2 rotation = new float2(
                _cameraJuiceProjectionRotationDegrees.y,
                -_cameraJuiceProjectionRotationDegrees.x) * CameraJuiceProjectionRotationScale;
            float roll = _cameraJuiceProjectionRotationDegrees.z * CameraJuiceProjectionRollScale;
            float2 jitter = math.clamp(translation + rotation, new float2(-0.03f), new float2(0.03f));
            projection.m02 += jitter.x;
            projection.m12 += jitter.y;
            projection.m01 += math.clamp(roll, -0.015f, 0.015f);
            projection.m10 -= math.clamp(roll, -0.015f, 0.015f);
        }

        private void QueueProceduralCameraJuiceManualImpulse(float severity01, float3 worldDirection)
        {
            float severity = math.saturate(math.isfinite(severity01) ? severity01 : 0f);
            if (severity <= 0f)
                return;

            _cameraJuiceManualTrauma01 = math.saturate(_cameraJuiceManualTrauma01 + ResolveTraumaAddition(severity));
            float3 localDirection = ResolveLocalShakeDirection(worldDirection);
            if (math.lengthsq(localDirection) > 0.000001f)
                _cameraJuiceManualDirectionalImpulseLocal += localDirection * severity;
        }

        private bool TryResolvePlayerCameraJuiceAup(out double3 playerAup)
        {
            playerAup = default;
            IDataVault vault = _dataVault;
            if (vault != null &&
                _cameraJuicePlayerKinematicStateHandle.BufferID != 0u &&
                _cameraJuicePlayerKinematicStateHandle.Generation != 0u &&
                vault.TryReadOnlyHandle(in _cameraJuicePlayerKinematicStateHandle, out NativeArray<LockstepPlayerKinematicState>.ReadOnly states) &&
                states.IsCreated &&
                states.Length > 0)
            {
                LockstepPlayerKinematicState state = states[0];
                playerAup = new double3(
                    ((double)state.SectorX * AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.x,
                    ((double)state.SectorY * AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.y,
                    ((double)state.SectorZ * AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.z);
                if (math.all(math.isfinite(playerAup)))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private bool TryLoadCameraJuiceTraumaProfilesFromCsv(
            NativeArray<CameraTraumaProfileDTO> profiles,
            NativeArray<byte> csvScratch,
            NativeArray<CameraJuiceTuningDTO> tuning)
        {
            if (!profiles.IsCreated || profiles.Length == 0 || !csvScratch.IsCreated || csvScratch.Length == 0)
                return false;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project", "Data", "VFX", CameraJuiceTraumaProfilesFileName));
            if (!File.Exists(path))
                return false;

            int byteCount = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
                    Span<byte> destination = new Span<byte>(scratchPtr, csvScratch.Length);
                    while (byteCount < csvScratch.Length)
                    {
                        int read = stream.Read(destination.Slice(byteCount));
                        if (read <= 0)
                            break;

                        byteCount += read;
                    }
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

            if (byteCount <= 0)
                return false;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(csvScratch);
            int loaded = CameraJuiceBurstMath.ParseProfilesCsv(new ReadOnlySpan<byte>(ptr, byteCount), profiles);
            if (loaded <= 0)
                return false;

            if (tuning.IsCreated && tuning.Length > 0)
            {
                ref CameraJuiceTuningDTO tuningValue = ref UnsafeUtility.AsRef<CameraJuiceTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
                tuningValue.ProfileCount = (uint)loaded;
                CameraTraumaProfileDTO firstProfile = profiles[0];
                tuningValue.MaxTranslationMeters = math.max(0.001f, PROCEDURAL_TRANSLATION_AMPLITUDE_METERS * math.max(0.1f, firstProfile.TranslationGain));
                tuningValue.MaxRotationDegrees = math.max(0.01f, PROCEDURAL_ROTATION_AMPLITUDE_DEGREES * math.max(0.1f, firstProfile.RotationGain));
                tuningValue.TraumaDecayPerSecond = math.max(0.1f, firstProfile.DecayPerSecond);
                tuningValue.BaseFrequencyHz = math.max(1f, firstProfile.FrequencyHz);
                tuningValue.LowTierRadiusMeters = math.max(1f, firstProfile.RadiusMeters);
                if (loaded > 1)
                    tuningValue.UltraRadiusMeters = math.max(tuningValue.LowTierRadiusMeters, profiles[loaded - 1].RadiusMeters);
            }

            return true;
        }
#endif

        private void ResolveCameraBasis(out float3 right, out float3 up, out float3 forward)
        {
            if (_cameraTransform == null)
            {
                right = new float3(1f, 0f, 0f);
                up = new float3(0f, 1f, 0f);
                forward = new float3(0f, 0f, 1f);
                return;
            }

            Vector3 r = _cameraTransform.right;
            Vector3 u = _cameraTransform.up;
            Vector3 f = _cameraTransform.forward;
            right = NormalizeSafe(new float3(r.x, r.y, r.z), new float3(1f, 0f, 0f));
            up = NormalizeSafe(new float3(u.x, u.y, u.z), new float3(0f, 1f, 0f));
            forward = NormalizeSafe(new float3(f.x, f.y, f.z), new float3(0f, 0f, 1f));
        }

        private float ResolveCameraJuiceGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private uint ResolveCameraJuiceTelemetryFlags()
        {
            uint flags = _cameraJuiceLastFlags;
            if (HectonXRRuntimeState.IsXRActive)
                flags |= CameraJuiceFlagXrSuppressed;
            return flags;
        }

#if UNITY_EDITOR
        public Vector4 EditorReadProceduralCameraJuiceState()
        {
            return new Vector4(
                _cameraJuiceLastTraumaScalar,
                _cameraJuiceLastMaxTranslationMagnitude,
                _cameraJuiceLastIncomingSignalCount,
                _cameraJuiceLastBurstExecutionMicros);
        }

        public void EditorSetProceduralCameraJuiceTuning(float translationMeters, float rotationDegrees, float decayPerSecond, float frequencyHz)
        {
            if (!EnsureProceduralCameraJuiceBuffers() ||
                !OpenCameraJuiceBuffer(in _cameraJuiceTuningHandle, 1, out NativeArray<CameraJuiceTuningDTO> tuning))
            {
                return;
            }

            ref CameraJuiceTuningDTO value = ref UnsafeUtility.AsRef<CameraJuiceTuningDTO>(
                NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
            value.MaxTranslationMeters = math.clamp(translationMeters, 0.001f, 0.25f);
            value.MaxRotationDegrees = math.clamp(rotationDegrees, 0.01f, 12f);
            value.TraumaDecayPerSecond = math.clamp(decayPerSecond, 0.1f, 8f);
            value.BaseFrequencyHz = math.clamp(frequencyHz, 1f, 55f);
        }

        public int EditorCopyCameraJuiceTelemetry(float[] trauma, float[] signalCount, float[] burstMicros, int maxSamples)
        {
            if (trauma == null || signalCount == null || burstMicros == null || maxSamples <= 0)
                return 0;

            if (!OpenCameraJuiceTelemetryReadOnly(out NativeArray<CameraJuiceTelemetryEntry>.ReadOnly telemetry))
                return 0;

            int count = math.min(math.min(math.min(maxSamples, trauma.Length), signalCount.Length), burstMicros.Length);
            count = math.min(count, (int)math.min(_cameraJuiceTelemetryCursor, (uint)CAMERA_JUICE_TELEMETRY_CAPACITY));
            uint start = _cameraJuiceTelemetryCursor - (uint)count;
            for (int i = 0; i < count; i++)
            {
                CameraJuiceTelemetryEntry entry = telemetry[(int)((start + (uint)i) % (uint)CAMERA_JUICE_TELEMETRY_CAPACITY)];
                trauma[i] = math.saturate(entry.TraumaScalar);
                signalCount[i] = math.saturate(entry.IncomingSignalCount * (1f / PROCEDURAL_MAX_IMPACTS_PER_FRAME));
                burstMicros[i] = math.saturate(entry.BurstExecutionMicroseconds * (1f / 100f));
            }

            return count;
        }

        public void EditorInjectProceduralCameraJuicePulse(float severity01)
        {
            QueueProceduralCameraJuiceManualImpulse(math.saturate(severity01), new float3(0f, 0.35f, -1f));
        }

        public void EditorSetProceduralCameraJuiceMockSignals(bool enabled, int count, float severity01, float radiusMeters)
        {
            _cameraJuiceMockSignalsEnabled = enabled;
            _cameraJuiceMockSignalCount = math.clamp(count, 0, CameraJuiceMockSignalCapacity);
            _cameraJuiceMockSeverity01 = math.saturate(severity01);
            _cameraJuiceMockRadiusMeters = math.max(1f, radiusMeters);
        }

        private void OnDrawGizmosSelected()
        {
            if (_cameraTransform == null ||
                !OpenCameraJuiceBufferReadOnly(in _cameraJuiceStateHandle, 1, out NativeArray<CameraJuiceStateDTO>.ReadOnly state))
                return;

            CameraJuiceStateDTO stateValue = state[0];
            Vector3 origin = _cameraTransform.position;
            Vector3 offset = new Vector3(
                stateValue.CurrentTranslationalOffset.x,
                stateValue.CurrentTranslationalOffset.y,
                stateValue.CurrentTranslationalOffset.z) * 12f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(origin, Vector3.one * 0.16f);
            Gizmos.color = Color.red;
            Vector3 offsetOrigin = origin +
                (_cameraTransform.right * offset.x) +
                (_cameraTransform.up * offset.y) +
                (_cameraTransform.forward * offset.z);
            Gizmos.DrawWireCube(offsetOrigin, Vector3.one * 0.12f);
            Gizmos.DrawLine(origin, offsetOrigin);
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            return new double3(
                ((double)aup.GridX * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalX,
                ((double)aup.GridY * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalY,
                ((double)aup.GridZ * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalZ);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct InitializeCameraJuiceTelemetryJob : IJob
        {
            [NoAlias] public NativeArray<CameraJuiceTelemetryEntry> Telemetry;

            public void Execute()
            {
                for (int i = 0; i < Telemetry.Length; i++)
                    Telemetry[i] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SeedCameraJuiceBuffersJob : IJob
        {
            [NoAlias] public NativeArray<CameraJuiceStateDTO> State;
            [NoAlias] public NativeArray<CameraJuiceImpulseDTO> Impulse;
            [NoAlias] public NativeArray<CameraJuiceProjectionDTO> Projection;
            [NoAlias] public NativeArray<CameraJuiceTuningDTO> Tuning;
            [NoAlias] public NativeArray<CameraTraumaProfileDTO> Profiles;
            [NoAlias] public NativeArray<CameraJuiceMockSignalDTO> MockSignals;
            public float GlobalQualityWeight01;

            public void Execute()
            {
                State[0] = default;
                Impulse[0] = default;
                Projection[0] = new CameraJuiceProjectionDTO
                {
                    ComfortRotation = quaternion.identity,
                    Flags = CameraJuiceFlagVRSomaticWriteRejected,
                    GlobalQualityWeight01 = math.saturate(GlobalQualityWeight01)
                };
                Tuning[0] = CameraJuiceBurstMath.DefaultTuning(GlobalQualityWeight01);
                CameraJuiceBurstMath.WriteDefaultProfiles(Profiles);
                for (int i = 0; i < MockSignals.Length; i++)
                    MockSignals[i] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockTraumaSpikesJob : IJob
        {
            [NoAlias] public NativeArray<CameraJuiceMockSignalDTO> MockSignals;
            public double3 PlayerAup;
            public uint Frame;
            public uint Seed;
            public float Severity01;
            public float RadiusMeters;

            public void Execute()
            {
                int count = MockSignals.Length;
                for (int i = 0; i < count; i++)
                {
                    uint hash = math.hash(new uint3(Seed, (uint)i, Frame));
                    float angle = ((hash & 1023u) * (math.PI * 2f)) * math.rcp(1024f);
                    float ring = math.lerp(3f, math.max(3f, RadiusMeters), ((hash >> 10) & 255u) * math.rcp(255f));
                    int yQuantized = (int)((hash >> 18) & 31u) - 15;
                    MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
                    float3 offset = new float3(angleCos * ring, yQuantized * 0.05f, angleSin * ring);
                    MockSignals[i] = new CameraJuiceMockSignalDTO
                    {
                        EpicenterAup = PlayerAup + new double3(offset.x, offset.y, offset.z),
                        Direction = -NormalizeSafe(offset, new float3(0f, 0f, -1f)),
                        Severity01 = math.saturate(Severity01),
                        RadiusMeters = math.max(1f, RadiusMeters),
                        Frame = Frame,
                        Seed = hash,
                        Flags = 1u
                    };
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateCameraTraumaJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<ImpactSignal>.ReadOnly ImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<HighSpeedImpactSignal>.ReadOnly HighSpeedImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<CombatDamageSignal>.ReadOnly CombatDamageSignals;
            [ReadOnly, NoAlias] public NativeArray<SeismicSignal>.ReadOnly SeismicSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceImpactSignal>.ReadOnly CameraImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceMockSignalDTO>.ReadOnly MockSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceTuningDTO>.ReadOnly Tuning;
            [NoAlias] public NativeArray<CameraJuiceImpulseDTO> Impulse;
            public double3 PlayerAup;
            public float3 CameraRight;
            public float3 CameraUp;
            public float3 CameraForward;
            public float ManualTrauma01;
            public float3 ManualDirectionalImpulseLocal;
            public float GlobalQualityWeight01;
            public int MockSignalCount;
            public int MaxSignalsPerFrame;
            public uint Frame;

            public void Execute()
            {
                CameraJuiceImpulseDTO impulse = Impulse[0];
                impulse.DirectionalImpulse = float3.zero;
                impulse.TraumaDelta = 0f;
                impulse.SignalCount = 0;
                impulse.Flags = 0u;
                impulse.MaxSignalMagnitude = 0f;
                impulse.DistanceAttenuation = 0f;
                impulse.Sequence = Frame;

                bool manualDirectionFinite = math.all(math.isfinite(ManualDirectionalImpulseLocal));
                bool manualDirectionClamped = manualDirectionFinite && math.any(math.abs(ManualDirectionalImpulseLocal) > 8f);
                bool sanitizedManual =
                    !math.isfinite(ManualTrauma01) ||
                    !manualDirectionFinite ||
                    manualDirectionClamped;
                float trauma = math.saturate(math.isfinite(ManualTrauma01) ? ManualTrauma01 : 0f);
                float3 manualDirection = manualDirectionFinite
                    ? math.clamp(ManualDirectionalImpulseLocal, new float3(-8f), new float3(8f))
                    : float3.zero;
                float manualDirectionSq = math.lengthsq(manualDirection);
                float3 direction = manualDirectionSq > 0.000001f ? manualDirection : float3.zero;
                int count = manualDirectionSq > 0.000001f || trauma > 0f ? 1 : 0;
                float maxMagnitude = trauma;
                float attenuationSum = count > 0 ? 1f : 0f;
                int maxSignals = math.max(1, MaxSignalsPerFrame);
                if (!math.isfinite(GlobalQualityWeight01))
                    impulse.Flags |= CameraJuiceFlagNanSanitized;
                float quality = math.saturate(math.isfinite(GlobalQualityWeight01) ? GlobalQualityWeight01 : 1f);
                float lowRadiusMeters = 32f;
                float ultraRadiusMeters = 120f;
                if (Tuning.IsCreated && Tuning.Length > 0)
                {
                    CameraJuiceTuningDTO tuning = Tuning[0];
                    if (!math.isfinite(tuning.LowTierRadiusMeters) || !math.isfinite(tuning.UltraRadiusMeters))
                        impulse.Flags |= CameraJuiceFlagNanSanitized;
                    lowRadiusMeters = math.max(1f, math.isfinite(tuning.LowTierRadiusMeters) ? tuning.LowTierRadiusMeters : lowRadiusMeters);
                    ultraRadiusMeters = math.max(lowRadiusMeters, math.isfinite(tuning.UltraRadiusMeters) ? tuning.UltraRadiusMeters : ultraRadiusMeters);
                }
                float radius = math.lerp(lowRadiusMeters, ultraRadiusMeters, quality);
                if (sanitizedManual)
                    impulse.Flags |= CameraJuiceFlagNanSanitized;

                if (CameraImpactSignals.IsCreated)
                {
                    int signalLimit = CameraImpactSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        CameraJuiceImpactSignal signal = CameraImpactSignals[i];
                        float severity = MaxFinite(signal.Severity, signal.Impact.Intensity, ref impulse.Flags);
                        AccumulateAupImpulse(signal.Impact.PointAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                    }
                }

                if (ImpactSignals.IsCreated)
                {
                    int signalLimit = ImpactSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        ImpactSignal signal = ImpactSignals[i];
                        float force = SanitizeSignalScalar(signal.Force, ref impulse.Flags);
                        float severity = MaxFinite(signal.Intensity, math.abs(force) * 0.01f, ref impulse.Flags);
                        AccumulateAupImpulse(signal.PointAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                    }
                }

                if (HighSpeedImpactSignals.IsCreated)
                {
                    int signalLimit = HighSpeedImpactSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        HighSpeedImpactSignal signal = HighSpeedImpactSignals[i];
                        float speed = SanitizeSignalScalar(signal.ImpactSpeed, ref impulse.Flags);
                        float kinetic = SanitizeSignalScalar(signal.KineticEnergy, ref impulse.Flags);
                        float severity = math.max(speed * 0.035f, kinetic * 0.00002f);
                        AccumulateAupImpulse(signal.PointAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                    }
                }

                if (CombatDamageSignals.IsCreated)
                {
                    int signalLimit = CombatDamageSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        CombatDamageSignal signal = CombatDamageSignals[i];
                        float severity = SanitizeSignalScalar(signal.Magnitude, ref impulse.Flags) * 0.1f;
                        AccumulateAbsoluteImpulse(signal.ImpactAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                    }
                }

                if (SeismicSignals.IsCreated)
                {
                    int signalLimit = SeismicSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        SeismicSignal signal = SeismicSignals[i];
                        float jitter = MaxFinite(signal.CameraJitter01, signal.Intensity01, ref impulse.Flags);
                        float amplitude = SanitizeSignalScalar(signal.SWaveAmplitude01, ref impulse.Flags) +
                            SanitizeSignalScalar(signal.PWaveAmplitude01, ref impulse.Flags);
                        float severity = jitter * math.max(0.1f, amplitude);
                        float waveRadius = MaxFinite(signal.SWaveRadiusMeters, signal.PWaveRadiusMeters, ref impulse.Flags);
                        float seismicRadius = math.max(radius, waveRadius);
                        AccumulateAbsoluteImpulse(signal.EpicenterAUP, severity, seismicRadius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                    }
                }

                int mockLimit = MockSignals.IsCreated ? math.min(math.max(0, MockSignalCount), MockSignals.Length) : 0;
                for (int i = 0; i < mockLimit && count < maxSignals; i++)
                {
                    CameraJuiceMockSignalDTO signal = MockSignals[i];
                    float severity = SanitizeSignalScalar(signal.Severity01, ref impulse.Flags);
                    float mockRadius = math.max(radius, SanitizeSignalScalar(signal.RadiusMeters, ref impulse.Flags));
                    AccumulateAbsoluteImpulse(signal.EpicenterAup, severity, mockRadius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags);
                }

                float directionSq = math.lengthsq(direction);
                impulse.DirectionalImpulse = directionSq > 0.000001f ? direction * math.rsqrt(directionSq) : float3.zero;
                if (directionSq > 0.000001f)
                {
                    bool previousMemoryFinite = math.all(math.isfinite(impulse.DirectionalMemory));
                    bool previousMemoryClamped = previousMemoryFinite && math.any(math.abs(impulse.DirectionalMemory) > 1f);
                    bool previousTimerFinite = math.isfinite(impulse.DirectionalTimer);
                    bool previousTimerClamped = previousTimerFinite && (impulse.DirectionalTimer < 0f || impulse.DirectionalTimer > 1f);
                    bool sanitizedMemory =
                        !previousMemoryFinite ||
                        previousMemoryClamped ||
                        !previousTimerFinite ||
                        previousTimerClamped;
                    float3 previousMemory = previousMemoryFinite
                        ? math.clamp(impulse.DirectionalMemory, new float3(-1f), new float3(1f))
                        : float3.zero;
                    float previousTimer = previousTimerFinite ? math.clamp(impulse.DirectionalTimer, 0f, 1f) : 0f;
                    impulse.DirectionalMemory = NormalizeSafe((previousMemory * math.saturate(previousTimer)) + impulse.DirectionalImpulse, impulse.DirectionalImpulse);
                    impulse.DirectionalTimer = 0.075f;
                    if (sanitizedMemory)
                        impulse.Flags |= CameraJuiceFlagNanSanitized;
                }

                impulse.TraumaDelta = math.saturate(trauma);
                impulse.SignalCount = count;
                impulse.MaxSignalMagnitude = maxMagnitude;
                impulse.DistanceAttenuation = count > 0 ? attenuationSum * math.rcp(count) : 0f;
                Impulse[0] = impulse;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeSignalScalar(float value, ref uint flags)
            {
                if (math.isfinite(value))
                    return value;

                flags |= CameraJuiceFlagNanSanitized;
                return 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float MaxFinite(float a, float b, ref uint flags)
            {
                return math.max(SanitizeSignalScalar(a, ref flags), SanitizeSignalScalar(b, ref flags));
            }

            private void AccumulateAupImpulse(
                in AbsoluteUniversePosition epicenter,
                float severity01,
                float radiusMeters,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum,
                ref uint flags)
            {
                if (!AbsoluteUniversePosition.IsFinite(in epicenter))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return;
                }

                AccumulateAbsoluteImpulse(ToAbsoluteDouble3Job(in epicenter), severity01, radiusMeters, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref flags);
            }

            private void AccumulateAbsoluteImpulse(
                double3 epicenter,
                float severity01,
                float radiusMeters,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum,
                ref uint flags)
            {
                if (!math.all(math.isfinite(PlayerAup)) || !math.all(math.isfinite(epicenter)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return;
                }

                if (!math.isfinite(severity01) || !math.isfinite(radiusMeters))
                    flags |= CameraJuiceFlagNanSanitized;
                float severity = math.saturate(math.isfinite(severity01) ? severity01 : 0f);
                if (severity <= 0.0001f)
                    return;

                double3 deltaD = PlayerAup - epicenter;
                if (!math.all(math.isfinite(deltaD)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return;
                }

                const double maxLocalMeters = 262144.0;
                deltaD = math.clamp(deltaD, new double3(-maxLocalMeters), new double3(maxLocalMeters));
                float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                float distSq = math.lengthsq(delta);
                float safeRadius = math.max(1f, math.isfinite(radiusMeters) ? radiusMeters : 1f);
                float invDist = math.rsqrt(math.max(0.0001f, distSq));
                float distance = distSq * invDist;
                float attenuation = math.saturate(1f - (distance * math.rcp(safeRadius)));
                if (!math.isfinite(attenuation))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return;
                }
                if (attenuation <= 0.0001f)
                    return;

                float weight = severity * attenuation;
                float3 worldDirection = distSq > 0.0001f ? delta * invDist : new float3(0f, 0f, -1f);
                float3 localDirection = new float3(
                    math.dot(worldDirection, CameraRight),
                    math.dot(worldDirection, CameraUp),
                    math.dot(worldDirection, CameraForward));
                if (!math.all(math.isfinite(localDirection)))
                {
                    localDirection = CameraJuiceBurstMath.SanitizeFloat3(localDirection);
                    flags |= CameraJuiceFlagNanSanitized;
                }
                direction += localDirection * weight;
                trauma = math.saturate(trauma + (weight * 0.45f));
                maxMagnitude = math.max(maxMagnitude, severity);
                attenuationSum += attenuation;
                count++;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct IntegrateProceduralShakeJob : IJob
        {
            [NoAlias] public NativeArray<CameraJuiceStateDTO> State;
            [NoAlias] public NativeArray<CameraJuiceImpulseDTO> Impulse;
            [NoAlias] public NativeArray<CameraJuiceProjectionDTO> Projection;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceTuningDTO>.ReadOnly Tuning;
            public float DeltaTime;
            public float EffectiveShakeScale;
            public float GlobalQualityWeight01;
            public uint XrActive;
            public uint Sequence;

            public void Execute()
            {
                CameraJuiceStateDTO state = State[0];
                CameraJuiceImpulseDTO impulse = Impulse[0];
                CameraJuiceTuningDTO tuning = Tuning[0];
                bool sanitizedScalarInput =
                    !math.isfinite(DeltaTime) ||
                    !math.isfinite(EffectiveShakeScale) ||
                    !math.isfinite(GlobalQualityWeight01) ||
                    !math.isfinite(tuning.MaxTranslationMeters) ||
                    !math.isfinite(tuning.MaxRotationDegrees) ||
                    !math.isfinite(tuning.MaxRollDegrees) ||
                    !math.isfinite(tuning.TraumaDecayPerSecond) ||
                    !math.isfinite(tuning.BaseFrequencyHz) ||
                    !math.isfinite(tuning.DirectionalBiasSeconds) ||
                    !math.isfinite(tuning.ProjectionTranslationScale) ||
                    !math.isfinite(tuning.ProjectionRotationScale) ||
                    !math.isfinite(tuning.LowTierRadiusMeters) ||
                    !math.isfinite(tuning.UltraRadiusMeters) ||
                    !math.isfinite(tuning.HighOctaveGain);
                float dt = math.clamp(math.isfinite(DeltaTime) ? DeltaTime : 0f, 0f, 0.1f);
                float quality = math.saturate(math.isfinite(GlobalQualityWeight01) ? GlobalQualityWeight01 : 1f);
                float effectiveScale = math.isfinite(EffectiveShakeScale) ? math.max(0f, EffectiveShakeScale) : 0f;
                float decay = math.max(0.01f, math.isfinite(tuning.TraumaDecayPerSecond) ? tuning.TraumaDecayPerSecond : PROCEDURAL_TRAUMA_DECAY_RATE);
                float maxTranslationMeters = math.max(0f, math.isfinite(tuning.MaxTranslationMeters) ? tuning.MaxTranslationMeters : 0f);
                float maxRotationDegrees = math.max(0f, math.isfinite(tuning.MaxRotationDegrees) ? tuning.MaxRotationDegrees : 0f);
                float maxRollDegrees = math.max(0f, math.isfinite(tuning.MaxRollDegrees) ? tuning.MaxRollDegrees : 0f);
                float directionalBiasSeconds = math.max(0f, math.isfinite(tuning.DirectionalBiasSeconds) ? tuning.DirectionalBiasSeconds : PROCEDURAL_DIRECTIONAL_BIAS_SECONDS);
                float highOctaveGain = math.max(0f, math.isfinite(tuning.HighOctaveGain) ? tuning.HighOctaveGain : 0f);
                bool directionalMemoryFinite = math.all(math.isfinite(impulse.DirectionalMemory));
                bool directionalMemoryOutOfRange = directionalMemoryFinite && math.any(math.abs(impulse.DirectionalMemory) > 1f);
                bool directionalTimerFinite = math.isfinite(impulse.DirectionalTimer);
                bool directionalTimerOutOfRange = directionalTimerFinite && (impulse.DirectionalTimer < 0f || impulse.DirectionalTimer > 1f);
                bool sanitizedInput =
                    sanitizedScalarInput ||
                    !math.isfinite(state.TraumaScalar) ||
                    !math.isfinite(impulse.TraumaDelta) ||
                    !directionalMemoryFinite ||
                    directionalMemoryOutOfRange ||
                    !directionalTimerFinite ||
                    directionalTimerOutOfRange ||
                    (impulse.Flags & CameraJuiceFlagNanSanitized) != 0u;
                float currentTrauma = math.isfinite(state.TraumaScalar) ? state.TraumaScalar : 0f;
                float incomingTrauma = math.isfinite(impulse.TraumaDelta) ? impulse.TraumaDelta : 0f;
                state.TraumaScalar = math.saturate(currentTrauma + incomingTrauma);
                impulse.DirectionalMemory = directionalMemoryFinite
                    ? math.clamp(impulse.DirectionalMemory, new float3(-1f), new float3(1f))
                    : float3.zero;
                impulse.DirectionalTimer = directionalTimerFinite ? math.clamp(impulse.DirectionalTimer, 0f, 1f) : 0f;

                if (XrActive != 0u || effectiveScale <= 0f)
                {
                    state.CurrentTranslationalOffset = float3.zero;
                    state.CurrentRotationalOffset = float3.zero;
                    state.TraumaScalar = math.isfinite(state.TraumaScalar) ? math.max(0f, state.TraumaScalar - (decay * dt)) : 0f;
                    impulse.DirectionalTimer = 0f;
                    impulse.DirectionalMemory = float3.zero;
                    Projection[0] = new CameraJuiceProjectionDTO
                    {
                        ComfortRotation = quaternion.identity,
                        TraumaScalar = state.TraumaScalar,
                        Flags = (XrActive != 0u ? CameraJuiceFlagXrSuppressed | CameraJuiceFlagVRSomaticWriteRejected : CameraJuiceFlagVRSomaticWriteRejected) |
                            (sanitizedInput ? CameraJuiceFlagNanSanitized : 0u),
                        GlobalQualityWeight01 = quality,
                        StateHash = CameraJuiceBurstMath.HashState(in state, quality)
                    };
                    State[0] = state;
                    Impulse[0] = impulse;
                    return;
                }

                float frequency = math.max(0.1f, math.isfinite(tuning.BaseFrequencyHz) ? tuning.BaseFrequencyHz : PROCEDURAL_SHAKE_FREQUENCY) * math.lerp(0.55f, 1.35f, quality);
                state.TimeAccumulator = CameraJuiceBurstMath.WrapPhase(state.TimeAccumulator + (dt * frequency));
                float trauma = math.saturate(state.TraumaScalar);
                float intensity = trauma * trauma * effectiveScale;
                float octaveWeight = CameraJuiceBurstMath.Smooth01(math.saturate((quality - 0.30f) * math.rcp(0.70f)));
                float phase = state.TimeAccumulator;
                float3 low = new float3(
                    MathLodApproximation.ApproxSinBhaskara((phase * 6.2831855f) + 0.37f),
                    CameraJuiceBurstMath.TriangleSigned((phase * 0.73f) + 0.61f),
                    MathLodApproximation.ApproxSinBhaskara((phase * 4.1887903f) + 1.91f));
                // Math LOD admission: expected tap cost follows quality without hard quality thresholds.
                float highTapAdmission = CameraJuiceBurstMath.TemporalAdmission01(Sequence, 0xC354A11Du, octaveWeight);
                float3 high = float3.zero;
                if (highTapAdmission > 0f)
                {
                    high = new float3(
                        CameraJuiceBurstMath.TriangleSigned((phase * 0.31f) + 0.137f + quality * 0.071f),
                        CameraJuiceBurstMath.TriangleSigned((phase * 0.37f) + 0.719f + quality * 0.113f),
                        CameraJuiceBurstMath.TriangleSigned((phase * 0.41f) + 0.031f + quality * 0.173f));
                }
                float ultraWeight = CameraJuiceBurstMath.Smooth01(math.saturate((quality - 0.65f) * math.rcp(0.35f)));
                float ultraTapAdmission = CameraJuiceBurstMath.TemporalAdmission01(Sequence, 0xC354U, ultraWeight);
                if (ultraTapAdmission > 0f)
                {
                    float3 grit = new float3(
                        CameraJuiceBurstMath.TriangleSigned((phase * 0.83f) + 0.411f + quality * 0.197f),
                        CameraJuiceBurstMath.TriangleSigned((phase * 0.97f) + 0.173f + quality * 0.239f),
                        CameraJuiceBurstMath.TriangleSigned((phase * 1.11f) + 0.530f + quality * 0.293f));
                    high += grit * (ultraWeight * 0.35f);
                }
                float3 wave = low + (high * highOctaveGain * octaveWeight);
                float biasT = directionalBiasSeconds > 0.001f
                    ? math.saturate(impulse.DirectionalTimer * math.rcp(directionalBiasSeconds))
                    : 0f;
                float3 directional = impulse.DirectionalMemory * biasT;
                float3 translation = ((wave * 0.65f) + directional) * (maxTranslationMeters * intensity);

                float3 rotLow = new float3(
                    CameraJuiceBurstMath.TriangleSigned((phase * 0.91f) + 0.13f),
                    MathLodApproximation.ApproxSinBhaskara((phase * 5.497787f) + 2.4f),
                    -directional.x + (MathLodApproximation.ApproxSinBhaskara((phase * 3.1415927f) + 0.8f) * 0.35f));
                float3 rotation = rotLow * (maxRotationDegrees * intensity);
                rotation.z += -directional.x * maxRollDegrees * intensity;

                bool sanitizedOutput = !math.all(math.isfinite(translation)) || !math.all(math.isfinite(rotation));
                translation = CameraJuiceBurstMath.SanitizeFloat3(translation);
                rotation = CameraJuiceBurstMath.SanitizeFloat3(rotation);
                float maxMagnitude = math.length(translation);
                state.CurrentTranslationalOffset = translation;
                state.CurrentRotationalOffset = rotation;
                state.TraumaScalar = math.isfinite(trauma) ? math.max(0f, trauma - (decay * dt)) : 0f;
                if (state.TraumaScalar <= 0.0001f)
                    state.TraumaScalar = 0f;

                impulse.DirectionalTimer = math.max(0f, impulse.DirectionalTimer - dt);
                if (impulse.DirectionalTimer <= 0.0001f)
                    impulse.DirectionalMemory = float3.zero;
                Impulse[0] = impulse;

                quaternion comfortRotation = quaternion.EulerXYZ(math.radians(rotation * 0.1f));
                uint flags = CameraJuiceFlagVRSomaticWriteRejected;
                if (sanitizedInput || sanitizedOutput || !math.isfinite(state.TraumaScalar))
                    flags |= CameraJuiceFlagNanSanitized;
                uint stateHash = CameraJuiceBurstMath.HashState(in state, quality) ^ math.hash(new uint2(Sequence, impulse.Sequence));
                Projection[0] = new CameraJuiceProjectionDTO
                {
                    TranslationOffset = translation,
                    RotationDegrees = rotation,
                    TraumaScalar = state.TraumaScalar,
                    MaxTranslationMagnitude = maxMagnitude,
                    ComfortRotation = comfortRotation,
                    Flags = flags,
                    StateHash = stateHash,
                    GlobalQualityWeight01 = quality,
                    DirectionalImpulseMagnitude = math.length(impulse.DirectionalMemory)
                };
                State[0] = state;
            }
        }

        private static class CameraJuiceBurstMath
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CameraJuiceTuningDTO DefaultTuning(float quality)
            {
                float safeQuality = math.saturate(math.isfinite(quality) ? quality : 1f);
                return new CameraJuiceTuningDTO
                {
                    MaxTranslationMeters = PROCEDURAL_TRANSLATION_AMPLITUDE_METERS,
                    MaxRotationDegrees = PROCEDURAL_ROTATION_AMPLITUDE_DEGREES,
                    MaxRollDegrees = PROCEDURAL_ROLL_AMPLITUDE_DEGREES,
                    TraumaDecayPerSecond = PROCEDURAL_TRAUMA_DECAY_RATE,
                    BaseFrequencyHz = PROCEDURAL_SHAKE_FREQUENCY,
                    DirectionalBiasSeconds = PROCEDURAL_DIRECTIONAL_BIAS_SECONDS,
                    ProjectionTranslationScale = CameraJuiceProjectionTranslationScale,
                    ProjectionRotationScale = CameraJuiceProjectionRotationScale,
                    LowTierRadiusMeters = 32f,
                    UltraRadiusMeters = 120f,
                    HighOctaveGain = math.lerp(0.15f, 0.85f, safeQuality),
                    QualityWeight01 = safeQuality,
                    ProfileCount = 3u,
                    Flags = 0u
                };
            }

            public static void WriteDefaultProfiles(NativeArray<CameraTraumaProfileDTO> profiles)
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;
                if (profiles.Length > 0)
                    profiles[0] = DefaultProfile(0x4C4F5754u, 0.6f, 0.5f, 32f, 1.9f, 12f);
                if (profiles.Length > 1)
                    profiles[1] = DefaultProfile(0x4D494454u, 1.0f, 1.0f, 72f, 1.65f, 18f);
                if (profiles.Length > 2)
                    profiles[2] = DefaultProfile(0x554C5452u, 1.35f, 1.6f, 120f, 1.25f, 26f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CameraTraumaProfileDTO DefaultProfile(uint hash, float translationGain, float rotationGain, float radius, float decay, float frequency)
            {
                return new CameraTraumaProfileDTO
                {
                    ProfileHash = hash,
                    TranslationGain = translationGain,
                    RotationGain = rotationGain,
                    RadiusMeters = radius,
                    DecayPerSecond = decay,
                    FrequencyHz = frequency
                };
            }

#if UNITY_EDITOR
            public static int ParseProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<CameraTraumaProfileDTO> profiles)
            {
                if (csv.Length == 0 || !profiles.IsCreated || profiles.Length == 0)
                    return 0;

                int written = 0;
                int lineStart = 0;
                for (int i = 0; i <= csv.Length && written < profiles.Length; i++)
                {
                    if (i < csv.Length && csv[i] != (byte)'\n')
                        continue;

                    ReadOnlySpan<byte> line = TrimAscii(csv.Slice(lineStart, i - lineStart));
                    lineStart = i + 1;
                    if (line.Length == 0 || line[0] == (byte)'#')
                        continue;

                    if (TryParseProfileLine(line, out CameraTraumaProfileDTO profile))
                        profiles[written++] = profile;
                }

                return written;
            }
#endif

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float TriangleSigned(float phase)
            {
                float wrapped = phase - math.floor(phase);
                return ((1f - math.abs((wrapped * 2f) - 1f)) * 2f) - 1f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Smooth01(float value)
            {
                float t = math.saturate(value);
                return t * t * (3f - (2f * t));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float WrapPhase(float value)
            {
                if (!math.isfinite(value))
                    return 0f;

                const float period = 1024f;
                return value - (math.floor(value * math.rcp(period)) * period);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float TemporalAdmission01(uint sequence, uint salt, float weight01)
            {
                float weight = math.saturate(math.isfinite(weight01) ? weight01 : 0f);
                if (weight <= 0f)
                    return 0f;
                if (weight >= 1f)
                    return 1f;

                uint hash = math.hash(new uint2(sequence, salt));
                float dither = (hash & 0x00FFFFFFu) * (1f / 16777215f);
                return dither < weight ? 1f : 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float3 SanitizeFloat3(float3 value)
            {
                return math.all(math.isfinite(value)) ? value : float3.zero;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static uint HashState(in CameraJuiceStateDTO state, float quality)
            {
                uint a = math.hash(new float4(state.CurrentTranslationalOffset, state.TraumaScalar));
                uint b = math.hash(new float4(state.CurrentRotationalOffset, quality));
                return a ^ math.rol(b, 11);
            }

            public static uint ValidateLayoutSizes()
            {
                uint error = 0u;
                error |= UnsafeUtility.SizeOf<CameraJuiceStateDTO>() == 32 ? 0u : 1u;
                error |= UnsafeUtility.SizeOf<CameraJuiceImpulseDTO>() == 64 ? 0u : 2u;
                error |= UnsafeUtility.SizeOf<CameraJuiceProjectionDTO>() == 64 ? 0u : 4u;
                error |= UnsafeUtility.SizeOf<CameraJuiceTuningDTO>() == 64 ? 0u : 8u;
                error |= UnsafeUtility.SizeOf<CameraTraumaProfileDTO>() == 32 ? 0u : 16u;
                error |= UnsafeUtility.SizeOf<CameraJuiceMockSignalDTO>() == 64 ? 0u : 32u;
                return error;
            }

#if UNITY_EDITOR
            private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out CameraTraumaProfileDTO profile)
            {
                profile = default;
                int field = 0;
                int tokenStart = 0;
                float translation = 1f;
                float rotation = 1f;
                float radius = 72f;
                float decay = PROCEDURAL_TRAUMA_DECAY_RATE;
                float frequency = PROCEDURAL_SHAKE_FREQUENCY;
                uint hash = 2166136261u;
                bool hasName = false;
                bool hasTranslation = false;
                bool hasRotation = false;
                bool hasRadius = false;
                for (int i = 0; i <= line.Length; i++)
                {
                    if (i < line.Length && line[i] != (byte)',')
                        continue;

                    ReadOnlySpan<byte> token = TrimAscii(line.Slice(tokenStart, i - tokenStart));
                    tokenStart = i + 1;
                    if (field == 0)
                    {
                        if (token.Length == 0)
                            return false;
                        hash = Fnv1A(token);
                        hasName = true;
                    }
                    else if (field == 1)
                    {
                        if (!TryParseFloat(token, out float parsedTranslation))
                            return false;
                        translation = parsedTranslation;
                        hasTranslation = true;
                    }
                    else if (field == 2)
                    {
                        if (!TryParseFloat(token, out float parsedRotation))
                            return false;
                        rotation = parsedRotation;
                        hasRotation = true;
                    }
                    else if (field == 3)
                    {
                        if (!TryParseFloat(token, out float parsedRadius))
                            return false;
                        radius = parsedRadius;
                        hasRadius = true;
                    }
                    else if (field == 4)
                    {
                        if (token.Length > 0)
                        {
                            if (!TryParseFloat(token, out float parsedDecay))
                                return false;
                            decay = parsedDecay;
                        }
                    }
                    else if (field == 5)
                    {
                        if (token.Length > 0)
                        {
                            if (!TryParseFloat(token, out float parsedFrequency))
                                return false;
                            frequency = parsedFrequency;
                        }
                    }
                    else if (token.Length > 0)
                    {
                        return false;
                    }
                    field++;
                }

                if (field < 4 || !hasName || !hasTranslation || !hasRotation || !hasRadius)
                    return false;

                profile = DefaultProfile(hash, translation, rotation, math.max(1f, radius), math.max(0.1f, decay), math.max(1f, frequency));
                return true;
            }

            private static uint Fnv1A(ReadOnlySpan<byte> token)
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash ^ token[i]) * 16777619u;
                return hash != 0u ? hash : 1u;
            }

            private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
            {
                value = 0f;
                token = TrimAscii(token);
                if (token.Length == 0)
                    return false;

                int index = 0;
                float sign = 1f;
                if (token[index] == (byte)'-')
                {
                    sign = -1f;
                    index++;
                }
                else if (token[index] == (byte)'+')
                {
                    index++;
                }

                bool hasDigits = false;
                float integer = 0f;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    integer = (integer * 10f) + (token[index] - (byte)'0');
                    index++;
                    hasDigits = true;
                }

                float fraction = 0f;
                float denominator = 1f;
                if (index < token.Length && token[index] == (byte)'.')
                {
                    index++;
                    while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                    {
                        fraction = (fraction * 10f) + (token[index] - (byte)'0');
                        denominator *= 10f;
                        index++;
                        hasDigits = true;
                    }
                }

                if (!hasDigits)
                    return false;

                if (index != token.Length)
                    return false;

                value = sign * (integer + (fraction * math.rcp(math.max(denominator, 1f))));
                return math.isfinite(value);
            }

            private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
            {
                int start = 0;
                int end = token.Length - 1;
                while (start <= end && token[start] <= (byte)' ')
                    start++;
                while (end >= start && token[end] <= (byte)' ')
                    end--;
                return start > end ? ReadOnlySpan<byte>.Empty : token.Slice(start, end - start + 1);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3Job(in AbsoluteUniversePosition aup)
        {
            return new double3(
                ((double)aup.GridX * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalX,
                ((double)aup.GridY * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalY,
                ((double)aup.GridZ * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalZ);
        }
    }
}
