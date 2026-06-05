// ============================================================================
// CameraJuiceSystem - procedural camera shake impulse.
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
        private const float CameraJuiceProjectionTranslationScale = 0.035f;
        private const float CameraJuiceProjectionRotationScale = 0.00125f;
        private const float CameraJuiceProjectionRollScale = 0.0015f;
        private const float CameraJuiceBurstBudgetMicroseconds = 100f;
        private const float CameraJuiceMaximumSignalAmplitudeScale = 4f;
        private const float CameraJuiceMaximumSignalGain = 4f;
        private const float CameraJuiceMinimumSignalGain = 0f;
        private const int CameraJuiceSignalLowPriorityThreshold = 1;
        private const int CameraJuiceSignalNormalPriorityThreshold = 96;
        private const int CameraJuiceSignalHighPriorityThreshold = 160;
        private const int CameraJuiceSignalCriticalPriorityThreshold = 224;
        private const int CameraJuicePriorityActiveShift = 8;
        private const int CameraJuicePriorityHoldShift = 16;
        private const int CameraJuiceLowPriorityHoldFrames = 2;
        private const int CameraJuiceNormalPriorityHoldFrames = 4;
        private const int CameraJuiceHighPriorityHoldFrames = 7;
        private const int CameraJuiceCriticalPriorityHoldFrames = 12;
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
        private VaultGenerationHandle<LockstepPlayerKinematicState> _cameraJuicePlayerKinematicStateHandle;
        private bool _ownsCameraJuiceStateBuffer;
        private bool _ownsCameraJuiceImpulseBuffer;
        private bool _ownsCameraJuiceProjectionBuffer;
        private bool _ownsCameraJuiceTuningBuffer;
        private bool _ownsCameraJuiceProfilesBuffer;
        private bool _ownsCameraJuiceMockSignalsBuffer;
        private bool _cameraJuiceBuffersSeeded;
        private bool _cameraJuiceMockSignalsEnabled;
        private bool _cameraJuiceNativeStateDirty;
        private int _cameraJuiceMockSignalCount;
        private float _cameraJuiceMockSeverity01 = 0.65f;
        private float _cameraJuiceMockRadiusMeters = 18f;
        private float _cameraJuiceManualTrauma01;
        private float3 _cameraJuiceManualDirectionalImpulseLocal;
        private float3 _cameraJuiceProjectionTranslation;
        private float3 _cameraJuiceProjectionRotationDegrees;
        private bool _cameraJuiceProjectionDirty;
        private bool _cameraJuiceProjectionResetDirty;
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

        [StructLayout(LayoutKind.Explicit, Size = 72)]
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
            [FieldOffset(52)] public float RotationGain;
            [FieldOffset(56)] public uint DominantProfileHash;
            [FieldOffset(60)] public uint PriorityAndFlags;
            [FieldOffset(64)] public float DominantProfileDecayPerSecond;
            [FieldOffset(68)] public float DominantProfileFrequencyHz;
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
                Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Camera juice ABI violation.");
#endif
            bool ready =
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceStateHandle,
                    ref _ownsCameraJuiceStateBuffer,
                    CameraJuiceStateBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceImpulseHandle,
                    ref _ownsCameraJuiceImpulseBuffer,
                    CameraJuiceImpulseBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceProjectionHandle,
                    ref _ownsCameraJuiceProjectionBuffer,
                    CameraJuiceProjectionBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceTuningHandle,
                    ref _ownsCameraJuiceTuningBuffer,
                    CameraJuiceTuningBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceProfilesHandle,
                    ref _ownsCameraJuiceProfilesBuffer,
                    CameraJuiceProfilesBufferId,
                    CameraJuiceProfileCapacity,
                    NativeArrayOptions.UninitializedMemory) &&
                AcquireCameraJuiceBuffer(
                    ref _cameraJuiceMockSignalsHandle,
                    ref _ownsCameraJuiceMockSignalsBuffer,
                    CameraJuiceMockSignalsBufferId,
                    CameraJuiceMockSignalCapacity,
                    NativeArrayOptions.UninitializedMemory);
            if (!ready)
                return false;

            if (!_cameraJuiceBuffersSeeded)
            {
                if (!SeedProceduralCameraJuiceBuffers())
                    return false;

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
            _cameraJuicePlayerKinematicStateHandle = default;
            _cameraJuiceBuffersSeeded = false;
            _cameraJuiceNativeStateDirty = false;
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

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuicePlayerKinematicStateHandle = default;
                return;
            }

            if (_cameraJuicePlayerKinematicStateHandle.BufferID != 0u &&
                _cameraJuicePlayerKinematicStateHandle.Generation != 0u &&
                vault.TryReadOnlyHandle(in _cameraJuicePlayerKinematicStateHandle, out NativeArray<LockstepPlayerKinematicState>.ReadOnly cachedStates) &&
                !vault.IsCompactionFenceActive &&
                cachedStates.IsCreated &&
                cachedStates.Length > 0)
            {
                return;
            }

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuicePlayerKinematicStateHandle = default;
                return;
            }

            if (vault.TryGetGenerationHandle<LockstepPlayerKinematicState>(
                    BufferID.PlayerKinematicState,
                    out VaultGenerationHandle<LockstepPlayerKinematicState> playerHandle))
            {
                if (vault.IsCompactionFenceActive)
                {
                    _cameraJuicePlayerKinematicStateHandle = default;
                    return;
                }

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
            NativeArrayOptions options)
            where T : unmanaged
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || count <= 0)
                return false;

            if (handle.BufferID != 0u &&
                handle.Generation != 0u &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                !vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= count)
            {
                return true;
            }

            if (vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> borrowedHandle) &&
                vault.TryReadOnlyHandle(in borrowedHandle, out buffer) &&
                !vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= count)
            {
                handle = borrowedHandle;
                ownsHandle = false;
                return true;
            }

            if (vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                return false;
            }

            VaultGenerationHandle<T> acquiredHandle = vault.EnsureGenerationHandle<T>(
                bufferId,
                count,
                SystemID.Vfx,
                options);
            if (acquiredHandle.BufferID == 0u ||
                acquiredHandle.Generation == 0u ||
                !vault.TryReadOnlyHandle(in acquiredHandle, out buffer) ||
                vault.IsCompactionFenceActive ||
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

        private bool SeedProceduralCameraJuiceBuffers()
        {
            float quality = ResolveCameraJuiceGlobalQualityWeight();
            CameraJuiceProjectionDTO projection = new CameraJuiceProjectionDTO
            {
                ComfortRotation = quaternion.identity,
                GlobalQualityWeight01 = quality,
                StateHash = 0u
            };
            bool ready =
                TryWriteCameraJuiceBufferValue(in _cameraJuiceStateHandle, default(CameraJuiceStateDTO)) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceImpulseHandle, default(CameraJuiceImpulseDTO)) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceProjectionHandle, in projection) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceTuningHandle, CameraJuiceBurstMath.DefaultTuning(quality)) &
                TrySeedCameraJuiceProfiles() &
                TryClearCameraJuiceMockSignals();

#if UNITY_EDITOR
            if (ready)
                TryLoadCameraJuiceTraumaProfilesFromCsv();
#endif
            return ready;
        }

        private void InitializeCameraJuiceTelemetryRing(NativeArray<CameraJuiceTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length < CAMERA_JUICE_TELEMETRY_CAPACITY)
                return;

            InitializeCameraJuiceTelemetryJob job = default;
            job.Telemetry = telemetry;
            job.Execute();
        }

        private bool CanSkipProceduralCameraJuiceFrame()
        {
            if (!_cameraJuiceBuffersSeeded ||
                _cameraJuiceMockSignalsEnabled ||
                _cameraJuiceNativeStateDirty ||
                _cameraJuiceProjectionDirty ||
                _cameraJuiceProjectionResetDirty ||
                _cameraJuiceLastTraumaScalar > 0.0001f ||
                _cameraJuiceManualTrauma01 > 0.0001f ||
                math.lengthsq(_cameraJuiceManualDirectionalImpulseLocal) > 0.000001f)
            {
                return false;
            }

            return !HasProceduralCameraJuiceSignalSnapshot();
        }

        private static bool HasProceduralCameraJuiceSignalSnapshot()
        {
            NativeArray<ImpactSignal>.ReadOnly impactSignals = SignalBus<ImpactSignal>.GetFrameSnapshotArray();
            if (impactSignals.IsCreated && impactSignals.Length > 0)
                return true;

            NativeArray<HighSpeedImpactSignal>.ReadOnly highSpeedImpactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshotArray();
            if (highSpeedImpactSignals.IsCreated && highSpeedImpactSignals.Length > 0)
                return true;

            NativeArray<CombatDamageSignal>.ReadOnly combatDamageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray();
            if (combatDamageSignals.IsCreated && combatDamageSignals.Length > 0)
                return true;

            NativeArray<SeismicSignal>.ReadOnly seismicSignals = SignalBus<SeismicSignal>.GetFrameSnapshotArray();
            if (seismicSignals.IsCreated && seismicSignals.Length > 0)
                return true;

            NativeArray<CameraJuiceImpactSignal>.ReadOnly cameraImpactSignals = SignalBus<CameraJuiceImpactSignal>.GetFrameSnapshotArray();
            return cameraImpactSignals.IsCreated && cameraImpactSignals.Length > 0;
        }

        private void MarkProceduralCameraJuiceCalmFrame()
        {
            _cameraJuiceLastFlags = 0u;
            _cameraJuiceLastTraumaScalar = 0f;
            _cameraJuiceLastMaxTranslationMagnitude = 0f;
            _cameraJuiceLastBurstExecutionMicros = 0f;
            _cameraJuiceLastQualityWeight = ResolveCameraJuiceGlobalQualityWeight();
            _cameraJuiceLastDirectionalImpulseMagnitude = 0f;
            _cameraJuiceLastIncomingSignalCount = 0;
            CameraJuiceStateDTO calmState = default;
            _cameraJuiceLastStateHash = CameraJuiceBurstMath.HashState(in calmState, _cameraJuiceLastQualityWeight);
            _cameraJuiceNativeStateDirty = false;
            _trauma = 0f;
            _shakeOffset = Vector3.zero;
            _proceduralShakeTranslation = float3.zero;
            _proceduralShakeRotationDegrees = float3.zero;
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

            if (!OpenCameraJuiceBufferReadOnly(in _cameraJuiceStateHandle, 1, out NativeArray<CameraJuiceStateDTO>.ReadOnly state) ||
                !OpenCameraJuiceBufferReadOnly(in _cameraJuiceImpulseHandle, 1, out NativeArray<CameraJuiceImpulseDTO>.ReadOnly impulse) ||
                !OpenCameraJuiceBufferReadOnly(in _cameraJuiceTuningHandle, 1, out NativeArray<CameraJuiceTuningDTO>.ReadOnly tuning) ||
                !OpenCameraJuiceBufferReadOnly(in _cameraJuiceProfilesHandle, CameraJuiceProfileCapacity, out NativeArray<CameraTraumaProfileDTO>.ReadOnly profiles))
            {
                FailClosedProceduralCameraJuiceFrame(CameraJuiceFlagVaultUnavailable);
                return;
            }

            float quality = ResolveCameraJuiceGlobalQualityWeight();
            CameraJuiceStateDTO stateValue = state[0];
            CameraJuiceImpulseDTO impulseValue = impulse[0];
            CameraJuiceTuningDTO tuningValue = tuning[0];
            tuningValue.QualityWeight01 = quality;
            float fluidDrag01 = ResolveCameraJuiceFluidDrag01();
            if (fluidDrag01 > 0.0001f)
            {
                float dragT = EvaluateSmoothStep01(fluidDrag01);
                tuningValue.TraumaDecayPerSecond *= math.lerp(1f, 0.66f, dragT);
                tuningValue.BaseFrequencyHz *= math.lerp(1f, 0.72f, dragT);
                tuningValue.HighOctaveGain *= math.lerp(1f, 0.65f, dragT);
            }
            uint frameSequence = _cameraJuiceSequence;

            int mockCount = 0;
            NativeArray<CameraJuiceMockSignalDTO>.ReadOnly mockSignals = default;
            if (_cameraJuiceMockSignalsEnabled)
            {
                uint seed = math.hash(new uint2(frameSequence, 0x53483335u));
                if (!TryWriteCameraJuiceMockSignals(
                        playerAup,
                        frameSequence,
                        seed,
                        math.saturate(_cameraJuiceMockSeverity01),
                        math.max(1f, _cameraJuiceMockRadiusMeters)) ||
                    !OpenCameraJuiceBufferReadOnly(in _cameraJuiceMockSignalsHandle, CameraJuiceMockSignalCapacity, out mockSignals))
                {
                    FailClosedProceduralCameraJuiceFrame(CameraJuiceFlagVaultUnavailable);
                    return;
                }

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
            evaluateJob.MockSignals = mockSignals;
            evaluateJob.Tuning = tuningValue;
            evaluateJob.Profiles = profiles;
            evaluateJob.InputImpulse = impulseValue;
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
            impulseValue = evaluateJob.ResultImpulse;

            IntegrateProceduralShakeJob integrateJob = default;
            integrateJob.InputState = stateValue;
            integrateJob.InputImpulse = impulseValue;
            integrateJob.Tuning = tuningValue;
            integrateJob.DeltaTime = math.clamp(math.isfinite(dt) ? dt : 0f, 0f, 0.1f);
            integrateJob.EffectiveShakeScale = effectiveShakeScale;
            integrateJob.GlobalQualityWeight01 = quality;
            integrateJob.XrActive = HectonXRRuntimeState.IsXRActive ? 1u : 0u;
            integrateJob.Sequence = frameSequence;
            integrateJob.Execute();
            stateValue = integrateJob.ResultState;
            impulseValue = integrateJob.ResultImpulse;
            CameraJuiceProjectionDTO projectionValue = integrateJob.ResultProjection;
            _cameraJuiceSequence = frameSequence + 1u;
            _cameraJuiceLastBurstExecutionMicros = (float)((Stopwatch.GetTimestamp() - startTicks) * 1000000.0 / Stopwatch.Frequency);
            if (_cameraJuiceLastBurstExecutionMicros > CameraJuiceBurstBudgetMicroseconds)
            {
                projectionValue.Flags |= CameraJuiceFlagBurstBudgetExceeded;
                _cameraJuiceTelemetryDumpRequested = true;
            }

            ClearPendingProceduralCameraJuiceManualImpulse();

            PublishCameraJuiceStateFromValues(ref stateValue, ref impulseValue, ref projectionValue, quality);

            bool committed =
                TryWriteCameraJuiceBufferValue(in _cameraJuiceStateHandle, in stateValue) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceImpulseHandle, in impulseValue) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceProjectionHandle, in projectionValue);
            if (!committed)
                _cameraJuiceLastFlags |= CameraJuiceFlagVaultUnavailable;
        }

        private bool OpenCameraJuiceBufferReadOnly<T>(
            in VaultGenerationHandle<T> handle,
            int requiredCount,
            out NativeArray<T>.ReadOnly buffer)
            where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                handle.BufferID == 0u ||
                handle.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredCount)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryWriteCameraJuiceBufferValue<T>(
            in VaultGenerationHandle<T> handle,
            in T value)
            where T : unmanaged
        {
            IDataVault vault = _dataVault;
            NativeArray<T> buffer = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                handle.BufferID == 0u ||
                handle.Generation == 0u)
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in handle, CameraJuiceOwnerSystemId, out buffer);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length == 0)
                {
                    return false;
                }

                buffer[0] = value;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in handle, CameraJuiceOwnerSystemId);
            }
        }

        private bool TryWriteCameraJuiceMockSignals(
            double3 playerAup,
            uint frameSequence,
            uint seed,
            float severity01,
            float radiusMeters)
        {
            Span<CameraJuiceMockSignalDTO> generated = stackalloc CameraJuiceMockSignalDTO[CameraJuiceMockSignalCapacity];
            for (int i = 0; i < generated.Length; i++)
            {
                uint hash = math.hash(new uint3(seed, (uint)i, frameSequence));
                float angle = ((hash & 1023u) * (math.PI * 2f)) * math.rcp(1024f);
                float ring = math.lerp(3f, math.max(3f, radiusMeters), ((hash >> 10) & 255u) * math.rcp(255f));
                int yQuantized = (int)((hash >> 18) & 31u) - 15;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
                float3 offset = new float3(angleCos * ring, yQuantized * 0.05f, angleSin * ring);
                generated[i] = new CameraJuiceMockSignalDTO
                {
                    EpicenterAup = playerAup + new double3(offset.x, offset.y, offset.z),
                    Direction = -NormalizeSafe(offset, new float3(0f, 0f, -1f)),
                    Severity01 = math.saturate(severity01),
                    RadiusMeters = math.max(1f, radiusMeters),
                    Frame = frameSequence,
                    Seed = hash,
                    Flags = 1u
                };
            }

            IDataVault vault = _dataVault;
            NativeArray<CameraJuiceMockSignalDTO> mockSignals = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _cameraJuiceMockSignalsHandle.BufferID == 0u ||
                _cameraJuiceMockSignalsHandle.Generation == 0u)
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceMockSignalsHandle, CameraJuiceOwnerSystemId, out mockSignals);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !mockSignals.IsCreated ||
                    mockSignals.Length < CameraJuiceMockSignalCapacity)
                {
                    return false;
                }

                for (int i = 0; i < generated.Length; i++)
                    mockSignals[i] = generated[i];
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceMockSignalsHandle, CameraJuiceOwnerSystemId);
            }
        }

        private bool TrySeedCameraJuiceProfiles()
        {
            IDataVault vault = _dataVault;
            NativeArray<CameraTraumaProfileDTO> profiles = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _cameraJuiceProfilesHandle.BufferID == 0u ||
                _cameraJuiceProfilesHandle.Generation == 0u)
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceProfilesHandle, CameraJuiceOwnerSystemId, out profiles);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !profiles.IsCreated ||
                    profiles.Length < CameraJuiceProfileCapacity)
                {
                    return false;
                }

                CameraJuiceBurstMath.WriteDefaultProfiles(profiles);
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceProfilesHandle, CameraJuiceOwnerSystemId);
            }
        }

        private bool TryClearCameraJuiceMockSignals()
        {
            IDataVault vault = _dataVault;
            NativeArray<CameraJuiceMockSignalDTO> mockSignals = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _cameraJuiceMockSignalsHandle.BufferID == 0u ||
                _cameraJuiceMockSignalsHandle.Generation == 0u)
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceMockSignalsHandle, CameraJuiceOwnerSystemId, out mockSignals);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !mockSignals.IsCreated ||
                    mockSignals.Length < CameraJuiceMockSignalCapacity)
                {
                    return false;
                }

                for (int i = 0; i < mockSignals.Length; i++)
                    mockSignals[i] = default;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceMockSignalsHandle, CameraJuiceOwnerSystemId);
            }
        }

        private void PublishCameraJuiceStateFromValues(
            ref CameraJuiceStateDTO stateValue,
            ref CameraJuiceImpulseDTO impulseValue,
            ref CameraJuiceProjectionDTO projectionValue,
            float quality)
        {
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
                projectionValue.TranslationOffset = float3.zero;
                projectionValue.RotationDegrees = float3.zero;
                projectionValue.TraumaScalar = 0f;
                projectionValue.Flags |= CameraJuiceFlagNanSanitized;
            }

            bool previousProjectionDirty = _cameraJuiceProjectionDirty;
            _cameraJuiceProjectionTranslation = projectionValue.TranslationOffset;
            _cameraJuiceProjectionRotationDegrees = projectionValue.RotationDegrees;
            bool nextProjectionDirty = math.lengthsq(_cameraJuiceProjectionTranslation) > 0.0000001f ||
                                       math.lengthsq(_cameraJuiceProjectionRotationDegrees) > 0.0000001f;
            _cameraJuiceProjectionDirty = nextProjectionDirty;
            if (previousProjectionDirty && !nextProjectionDirty)
                _cameraJuiceProjectionResetDirty = true;
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
            _cameraJuiceLastFlags |= projectionValue.Flags;
            _cameraJuiceNativeStateDirty =
                _cameraJuiceLastTraumaScalar > 0.0001f ||
                _cameraJuiceLastIncomingSignalCount > 0 ||
                _cameraJuiceProjectionDirty ||
                impulseValue.PriorityAndFlags != 0u;
            ApplyImpactFovPunchFromImpulse(in impulseValue);
            if (invalid)
            {
                RecordCameraJuiceTelemetry();
                RequestDeferredCameraJuiceTelemetryDump();
            }
        }

        private void ApplyImpactFovPunchFromImpulse(in CameraJuiceImpulseDTO impulse)
        {
            if (!_fovEnabled || HectonXRRuntimeState.IsXRActive || impulse.SignalCount <= 0)
                return;

            float severity = math.saturate(math.isfinite(impulse.MaxSignalMagnitude) ? impulse.MaxSignalMagnitude : 0f);
            byte priority = (byte)(impulse.PriorityAndFlags & 0xFFu);
            if (severity < PROCEDURAL_IMPACT_FOV_MIN_SEVERITY && priority < CameraJuiceSignals.HighPriority)
                return;

            float quality = math.saturate(math.isfinite(_cameraJuiceLastQualityWeight) ? _cameraJuiceLastQualityWeight : 1f);
            float qualityT = EvaluateSmoothStep01(quality);
            if (qualityT <= 0.0001f)
                return;

            float severityT = EvaluateSmoothStep01(math.saturate((severity - PROCEDURAL_IMPACT_FOV_MIN_SEVERITY) * math.rcp(1f - PROCEDURAL_IMPACT_FOV_MIN_SEVERITY)));
            float priorityT = math.saturate(priority * math.rcp(255f));
            float priorityBoost = math.lerp(0.85f, 1.2f, priorityT);
            float amount = math.clamp(math.lerp(0.75f, PROCEDURAL_IMPACT_FOV_MAX_DEGREES, severityT) * priorityBoost * qualityT, 0f, PROCEDURAL_IMPACT_FOV_MAX_DEGREES);
            if (amount <= 0.001f)
                return;

            float duration = math.lerp(PROCEDURAL_IMPACT_FOV_DURATION * 0.65f, PROCEDURAL_IMPACT_FOV_DURATION, qualityT);
            TriggerFOVKick(amount, duration);
        }

        private void PublishProceduralCameraJuiceProjection()
        {
            if (_cameraJuiceProjectionDirty || _cameraJuiceProjectionResetDirty)
            {
                ApplyCameraJuiceProjectionToCamera();
                _cameraJuiceProjectionResetDirty = false;
            }
        }

        private void ClearProceduralCameraJuiceProjection()
        {
            bool projectionWasApplied = _cameraJuiceProjectionDirty || _cameraJuiceProjectionResetDirty;
            _shakeOffset = Vector3.zero;
            _cameraJuiceProjectionTranslation = float3.zero;
            _cameraJuiceProjectionRotationDegrees = float3.zero;
            _cameraJuiceProjectionDirty = false;
            _cameraJuiceProjectionResetDirty = false;
            _proceduralShakeTranslation = float3.zero;
            _proceduralShakeRotationDegrees = float3.zero;
            _cameraJuiceLastTraumaScalar = 0f;
            _cameraJuiceLastMaxTranslationMagnitude = 0f;
            _cameraJuiceLastIncomingSignalCount = 0;
            _cameraJuiceLastDirectionalImpulseMagnitude = 0f;
            _cameraJuiceLastStateHash = 0u;
            if (projectionWasApplied && _mainCamera != null && !_mainCamera.orthographic)
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
            RequestDeferredCameraJuiceTelemetryDump();
        }

        private void ClearSuppressedProceduralCameraJuiceState(uint flags)
        {
            if (!_cameraJuiceNativeStateDirty &&
                _cameraJuiceManualTrauma01 <= 0.0001f &&
                math.lengthsq(_cameraJuiceManualDirectionalImpulseLocal) <= 0.000001f)
            {
                return;
            }

            ClearPendingProceduralCameraJuiceManualImpulse();
            ClearProceduralCameraJuiceNativeState(flags);
        }

        private void ClearPendingProceduralCameraJuiceManualImpulse()
        {
            _cameraJuiceManualTrauma01 = 0f;
            _cameraJuiceManualDirectionalImpulseLocal = float3.zero;
        }

        private void ClearProceduralCameraJuiceNativeState(uint flags)
        {
            CameraJuiceProjectionDTO projection = new CameraJuiceProjectionDTO
            {
                ComfortRotation = quaternion.identity,
                Flags = flags | CameraJuiceFlagVRSomaticWriteRejected,
                GlobalQualityWeight01 = _cameraJuiceLastQualityWeight
            };

            _ = TryWriteCameraJuiceBufferValue(in _cameraJuiceStateHandle, default(CameraJuiceStateDTO)) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceImpulseHandle, default(CameraJuiceImpulseDTO)) &
                TryWriteCameraJuiceBufferValue(in _cameraJuiceProjectionHandle, in projection);
            _cameraJuiceNativeStateDirty = false;
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
                !vault.IsCompactionFenceActive &&
                _cameraJuicePlayerKinematicStateHandle.BufferID != 0u &&
                _cameraJuicePlayerKinematicStateHandle.Generation != 0u &&
                vault.TryReadOnlyHandle(in _cameraJuicePlayerKinematicStateHandle, out NativeArray<LockstepPlayerKinematicState>.ReadOnly states) &&
                !vault.IsCompactionFenceActive &&
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
        private bool TryLoadCameraJuiceTraumaProfilesFromCsv()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project", "Data", "VFX", CameraJuiceTraumaProfilesFileName));
            if (!File.Exists(path))
                return false;

            Span<byte> csvScratch = stackalloc byte[CameraJuiceCsvScratchBytes];
            if (!TryReadCameraJuiceCsvBytes(path, csvScratch, out int byteCount))
                return false;

            Span<CameraTraumaProfileDTO> parsedProfiles = stackalloc CameraTraumaProfileDTO[CameraJuiceProfileCapacity];
            int loaded = CameraJuiceBurstMath.ParseProfilesCsv(csvScratch.Slice(0, byteCount), parsedProfiles);
            if (loaded <= 0)
                return false;

            CameraTraumaProfileDTO firstProfile = parsedProfiles[0];
            float lastRadiusMeters = parsedProfiles[loaded - 1].RadiusMeters;
            IDataVault vault = _dataVault;
            NativeArray<CameraTraumaProfileDTO> profiles = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _cameraJuiceProfilesHandle.BufferID == 0u ||
                _cameraJuiceProfilesHandle.Generation == 0u)
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceProfilesHandle, CameraJuiceOwnerSystemId, out profiles);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !profiles.IsCreated ||
                    profiles.Length < CameraJuiceProfileCapacity)
                {
                    return false;
                }

                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = i < loaded ? parsedProfiles[i] : default;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceProfilesHandle, CameraJuiceOwnerSystemId);
            }

            CameraJuiceTuningDTO tuningValue = CameraJuiceBurstMath.DefaultTuning(ResolveCameraJuiceGlobalQualityWeight());
            tuningValue.ProfileCount = (uint)loaded;
            tuningValue.MaxTranslationMeters = math.max(0.001f, PROCEDURAL_TRANSLATION_AMPLITUDE_METERS * math.max(0.1f, firstProfile.TranslationGain));
            tuningValue.MaxRotationDegrees = math.max(0.01f, PROCEDURAL_ROTATION_AMPLITUDE_DEGREES * math.max(0.1f, firstProfile.RotationGain));
            tuningValue.TraumaDecayPerSecond = math.max(0.1f, firstProfile.DecayPerSecond);
            tuningValue.BaseFrequencyHz = math.max(1f, firstProfile.FrequencyHz);
            tuningValue.LowTierRadiusMeters = math.max(1f, firstProfile.RadiusMeters);
            tuningValue.UltraRadiusMeters = math.max(tuningValue.LowTierRadiusMeters, lastRadiusMeters);
            return TryWriteCameraJuiceBufferValue(in _cameraJuiceTuningHandle, in tuningValue);
        }

        private static bool TryReadCameraJuiceCsvBytes(string path, Span<byte> destination, out int byteCount)
        {
            byteCount = 0;
            if (destination.Length == 0)
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    while (byteCount < destination.Length)
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

            return byteCount > 0;
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
            if (!EnsureProceduralCameraJuiceBuffers())
            {
                return;
            }

            IDataVault vault = _dataVault;
            NativeArray<CameraJuiceTuningDTO> tuning = default;
            bool acquired = false;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceTuningHandle, CameraJuiceOwnerSystemId, out tuning);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !tuning.IsCreated ||
                    tuning.Length == 0)
                {
                    return;
                }

                CameraJuiceTuningDTO value = tuning[0];
                value.MaxTranslationMeters = math.clamp(translationMeters, 0.001f, 0.25f);
                value.MaxRotationDegrees = math.clamp(rotationDegrees, 0.01f, 12f);
                value.TraumaDecayPerSecond = math.clamp(decayPerSecond, 0.1f, 8f);
                value.BaseFrequencyHz = math.clamp(frequencyHz, 1f, 55f);
                tuning[0] = value;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceTuningHandle, CameraJuiceOwnerSystemId);
            }
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
        private struct EvaluateCameraTraumaJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<ImpactSignal>.ReadOnly ImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<HighSpeedImpactSignal>.ReadOnly HighSpeedImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<CombatDamageSignal>.ReadOnly CombatDamageSignals;
            [ReadOnly, NoAlias] public NativeArray<SeismicSignal>.ReadOnly SeismicSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceImpactSignal>.ReadOnly CameraImpactSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraJuiceMockSignalDTO>.ReadOnly MockSignals;
            [ReadOnly, NoAlias] public NativeArray<CameraTraumaProfileDTO>.ReadOnly Profiles;
            public CameraJuiceTuningDTO Tuning;
            public CameraJuiceImpulseDTO InputImpulse;
            public CameraJuiceImpulseDTO ResultImpulse;
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
                CameraJuiceImpulseDTO impulse = InputImpulse;
                byte activePriorityFloor = UnpackActivePriority(InputImpulse.PriorityAndFlags);
                int activePriorityHoldFrames = UnpackPriorityHoldFrames(InputImpulse.PriorityAndFlags);
                if (activePriorityHoldFrames > 0)
                    activePriorityHoldFrames--;
                if (activePriorityHoldFrames <= 0)
                    activePriorityFloor = 0;

                uint carriedProfileHash = activePriorityFloor != 0 ? InputImpulse.DominantProfileHash : 0u;
                float carriedProfileDecay = activePriorityFloor != 0 && math.isfinite(InputImpulse.DominantProfileDecayPerSecond)
                    ? math.max(0f, InputImpulse.DominantProfileDecayPerSecond)
                    : 0f;
                float carriedProfileFrequency = activePriorityFloor != 0 && math.isfinite(InputImpulse.DominantProfileFrequencyHz)
                    ? math.max(0f, InputImpulse.DominantProfileFrequencyHz)
                    : 0f;
                impulse.DirectionalImpulse = float3.zero;
                impulse.TraumaDelta = 0f;
                impulse.SignalCount = 0;
                impulse.Flags = 0u;
                impulse.MaxSignalMagnitude = 0f;
                impulse.DistanceAttenuation = 0f;
                impulse.Sequence = Frame;
                impulse.RotationGain = 1f;
                impulse.DominantProfileHash = carriedProfileHash;
                impulse.DominantProfileDecayPerSecond = carriedProfileDecay;
                impulse.DominantProfileFrequencyHz = carriedProfileFrequency;
                impulse.PriorityAndFlags = PackPriorityState(0, activePriorityFloor, activePriorityHoldFrames);

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
                byte manualPriority = ResolveSignalPriorityFromSeverity(trauma);
                if (ShouldRejectBelowActivePriority(manualPriority, activePriorityFloor))
                {
                    trauma = 0f;
                    manualDirection = float3.zero;
                }

                float manualDirectionSq = math.lengthsq(manualDirection);
                float3 direction = manualDirectionSq > 0.000001f ? manualDirection : float3.zero;
                int count = manualDirectionSq > 0.000001f || trauma > 0f ? 1 : 0;
                float maxMagnitude = trauma;
                float attenuationSum = count > 0 ? 1f : 0f;
                int maxSignals = math.max(1, MaxSignalsPerFrame);
                float rotationGainSum = 0f;
                float rotationGainWeight = 0f;
                byte dominantPriority = count > 0 ? manualPriority : (byte)0;
                uint dominantProfileHash = carriedProfileHash;
                float dominantProfileWeight = count > 0 ? trauma : 0f;
                float dominantProfileDecay = carriedProfileDecay;
                float dominantProfileFrequency = carriedProfileFrequency;
                if (!math.isfinite(GlobalQualityWeight01))
                    impulse.Flags |= CameraJuiceFlagNanSanitized;
                float quality = math.saturate(math.isfinite(GlobalQualityWeight01) ? GlobalQualityWeight01 : 1f);
                float lowRadiusMeters = 32f;
                float ultraRadiusMeters = 120f;
                if (!math.isfinite(Tuning.LowTierRadiusMeters) || !math.isfinite(Tuning.UltraRadiusMeters))
                    impulse.Flags |= CameraJuiceFlagNanSanitized;
                lowRadiusMeters = math.max(1f, math.isfinite(Tuning.LowTierRadiusMeters) ? Tuning.LowTierRadiusMeters : lowRadiusMeters);
                ultraRadiusMeters = math.max(lowRadiusMeters, math.isfinite(Tuning.UltraRadiusMeters) ? Tuning.UltraRadiusMeters : ultraRadiusMeters);
                float radius = math.lerp(lowRadiusMeters, ultraRadiusMeters, quality);
                if (sanitizedManual)
                    impulse.Flags |= CameraJuiceFlagNanSanitized;

                if (CameraImpactSignals.IsCreated)
                {
                    AccumulateCameraImpactSignalsByPriority(
                        CameraJuiceSignalCriticalPriorityThreshold,
                        256,
                        activePriorityFloor,
                        radius,
                        ref trauma,
                        ref direction,
                        ref count,
                        ref maxMagnitude,
                        ref attenuationSum,
                        ref rotationGainSum,
                        ref rotationGainWeight,
                        ref dominantPriority,
                        ref dominantProfileHash,
                        ref dominantProfileWeight,
                        ref dominantProfileDecay,
                        ref dominantProfileFrequency,
                        ref impulse.Flags);
                    if (dominantPriority < CameraJuiceSignals.CriticalPriority)
                    {
                        AccumulateCameraImpactSignalsByPriority(
                            CameraJuiceSignalHighPriorityThreshold,
                            CameraJuiceSignalCriticalPriorityThreshold,
                            activePriorityFloor,
                            radius,
                            ref trauma,
                            ref direction,
                            ref count,
                            ref maxMagnitude,
                            ref attenuationSum,
                            ref rotationGainSum,
                            ref rotationGainWeight,
                            ref dominantPriority,
                            ref dominantProfileHash,
                            ref dominantProfileWeight,
                            ref dominantProfileDecay,
                            ref dominantProfileFrequency,
                            ref impulse.Flags);
                    }
                    if (dominantPriority < CameraJuiceSignals.HighPriority)
                    {
                        AccumulateCameraImpactSignalsByPriority(
                            CameraJuiceSignalNormalPriorityThreshold,
                            CameraJuiceSignalHighPriorityThreshold,
                            activePriorityFloor,
                            radius,
                            ref trauma,
                            ref direction,
                            ref count,
                            ref maxMagnitude,
                            ref attenuationSum,
                            ref rotationGainSum,
                            ref rotationGainWeight,
                            ref dominantPriority,
                            ref dominantProfileHash,
                            ref dominantProfileWeight,
                            ref dominantProfileDecay,
                            ref dominantProfileFrequency,
                            ref impulse.Flags);
                    }
                    if (dominantPriority < CameraJuiceSignals.NormalPriority)
                    {
                        AccumulateCameraImpactSignalsByPriority(
                            CameraJuiceSignalLowPriorityThreshold,
                            CameraJuiceSignalNormalPriorityThreshold,
                            activePriorityFloor,
                            radius,
                            ref trauma,
                            ref direction,
                            ref count,
                            ref maxMagnitude,
                            ref attenuationSum,
                            ref rotationGainSum,
                            ref rotationGainWeight,
                            ref dominantPriority,
                            ref dominantProfileHash,
                            ref dominantProfileWeight,
                            ref dominantProfileDecay,
                            ref dominantProfileFrequency,
                            ref impulse.Flags);
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
                        byte priority = ResolveSignalPriorityFromSeverity(severity);
                        if (ShouldRejectBelowActivePriority(priority, activePriorityFloor))
                            continue;

                        if (AccumulateAupImpulse(signal.PointAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags))
                            dominantPriority = MaxPriority(dominantPriority, priority);
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
                        byte priority = ResolveSignalPriorityFromSeverity(severity);
                        if (ShouldRejectBelowActivePriority(priority, activePriorityFloor))
                            continue;

                        if (AccumulateAupImpulse(signal.PointAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags))
                            dominantPriority = MaxPriority(dominantPriority, priority);
                    }
                }

                if (CombatDamageSignals.IsCreated)
                {
                    int signalLimit = CombatDamageSignals.Length;
                    for (int i = 0; i < signalLimit && count < maxSignals; i++)
                    {
                        CombatDamageSignal signal = CombatDamageSignals[i];
                        float severity = SanitizeSignalScalar(signal.Magnitude, ref impulse.Flags) * 0.1f;
                        byte priority = ResolveSignalPriorityFromSeverity(severity);
                        if (ShouldRejectBelowActivePriority(priority, activePriorityFloor))
                            continue;

                        if (AccumulateAbsoluteImpulse(signal.ImpactAup, severity, radius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags))
                            dominantPriority = MaxPriority(dominantPriority, priority);
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
                        byte priority = ResolveSignalPriorityFromSeverity(severity);
                        if (ShouldRejectBelowActivePriority(priority, activePriorityFloor))
                            continue;

                        if (AccumulateAbsoluteImpulse(signal.EpicenterAUP, severity, seismicRadius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags))
                            dominantPriority = MaxPriority(dominantPriority, priority);
                    }
                }

                int mockLimit = MockSignals.IsCreated ? math.min(math.max(0, MockSignalCount), MockSignals.Length) : 0;
                for (int i = 0; i < mockLimit && count < maxSignals; i++)
                {
                    CameraJuiceMockSignalDTO signal = MockSignals[i];
                    float severity = SanitizeSignalScalar(signal.Severity01, ref impulse.Flags);
                    float mockRadius = math.max(radius, SanitizeSignalScalar(signal.RadiusMeters, ref impulse.Flags));
                    byte priority = ResolveSignalPriorityFromSeverity(severity);
                    if (ShouldRejectBelowActivePriority(priority, activePriorityFloor))
                        continue;

                    if (AccumulateAbsoluteImpulse(signal.EpicenterAup, severity, mockRadius, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref impulse.Flags))
                        dominantPriority = MaxPriority(dominantPriority, priority);
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
                impulse.RotationGain = rotationGainWeight > 0.000001f
                    ? math.clamp(rotationGainSum * math.rcp(rotationGainWeight), CameraJuiceMinimumSignalGain, CameraJuiceMaximumSignalGain)
                    : 1f;
                impulse.DominantProfileHash = dominantProfileHash;
                impulse.DominantProfileDecayPerSecond = dominantProfileDecay;
                impulse.DominantProfileFrequencyHz = dominantProfileFrequency;
                if (dominantPriority >= activePriorityFloor && dominantPriority > 0)
                {
                    activePriorityFloor = dominantPriority;
                    activePriorityHoldFrames = ResolvePriorityHoldFrames(dominantPriority);
                }

                impulse.PriorityAndFlags = PackPriorityState(dominantPriority, activePriorityFloor, activePriorityHoldFrames);
                ResultImpulse = impulse;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte UnpackActivePriority(uint priorityAndFlags)
            {
                return (byte)((priorityAndFlags >> CameraJuicePriorityActiveShift) & 0xFFu);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int UnpackPriorityHoldFrames(uint priorityAndFlags)
            {
                return (int)((priorityAndFlags >> CameraJuicePriorityHoldShift) & 0xFFFFu);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint PackPriorityState(byte currentPriority, byte activePriority, int holdFrames)
            {
                uint clampedHoldFrames = (uint)math.clamp(holdFrames, 0, 0xFFFF);
                return currentPriority |
                       ((uint)activePriority << CameraJuicePriorityActiveShift) |
                       (clampedHoldFrames << CameraJuicePriorityHoldShift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte MaxPriority(byte a, byte b)
            {
                return a >= b ? a : b;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRejectBelowActivePriority(byte signalPriority, byte activePriorityFloor)
            {
                return activePriorityFloor != 0 && signalPriority < activePriorityFloor;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolvePriorityHoldFrames(byte priority)
            {
                if (priority >= CameraJuiceSignals.CriticalPriority)
                    return CameraJuiceCriticalPriorityHoldFrames;
                if (priority >= CameraJuiceSignals.HighPriority)
                    return CameraJuiceHighPriorityHoldFrames;
                if (priority >= CameraJuiceSignals.NormalPriority)
                    return CameraJuiceNormalPriorityHoldFrames;
                return CameraJuiceLowPriorityHoldFrames;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveSignalPriorityFromSeverity(float severity01)
            {
                float severity = math.saturate(math.isfinite(severity01) ? severity01 : 0f);
                if (severity >= 0.85f)
                    return CameraJuiceSignals.CriticalPriority;
                if (severity >= 0.55f)
                    return CameraJuiceSignals.HighPriority;
                if (severity >= 0.25f)
                    return CameraJuiceSignals.NormalPriority;
                return CameraJuiceSignals.LowPriority;
            }

            private void AccumulateCameraImpactSignalsByPriority(
                int priorityMin,
                int priorityMaxExclusive,
                byte activePriorityFloor,
                float baseRadiusMeters,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum,
                ref float rotationGainSum,
                ref float rotationGainWeight,
                ref byte dominantPriority,
                ref uint dominantProfileHash,
                ref float dominantProfileWeight,
                ref float dominantProfileDecay,
                ref float dominantProfileFrequency,
                ref uint flags)
            {
                int maxSignals = math.max(1, MaxSignalsPerFrame);
                int effectivePriorityMin = math.max(priorityMin, activePriorityFloor);
                int signalLimit = CameraImpactSignals.Length;
                for (int i = 0; i < signalLimit && count < maxSignals; i++)
                {
                    CameraJuiceImpactSignal signal = CameraImpactSignals[i];
                    byte priority = ResolveSignalPriority(in signal, ref flags);
                    if (priority < effectivePriorityMin || priority >= priorityMaxExclusive)
                        continue;

                    AccumulateCameraImpactSignal(
                        in signal,
                        priority,
                        baseRadiusMeters,
                        ref trauma,
                        ref direction,
                        ref count,
                        ref maxMagnitude,
                        ref attenuationSum,
                        ref rotationGainSum,
                        ref rotationGainWeight,
                        ref dominantPriority,
                        ref dominantProfileHash,
                        ref dominantProfileWeight,
                        ref dominantProfileDecay,
                        ref dominantProfileFrequency,
                        ref flags);
                }
            }

            private void AccumulateCameraImpactSignal(
                in CameraJuiceImpactSignal signal,
                byte priority,
                float baseRadiusMeters,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum,
                ref float rotationGainSum,
                ref float rotationGainWeight,
                ref byte dominantPriority,
                ref uint dominantProfileHash,
                ref float dominantProfileWeight,
                ref float dominantProfileDecay,
                ref float dominantProfileFrequency,
                ref uint flags)
            {
                float rawSeverity = MaxFinite(signal.Severity, signal.Impact.Intensity, ref flags);
                if (rawSeverity <= 0.0001f)
                    return;

                uint profileHash = signal.ProfileHash != 0u
                    ? signal.ProfileHash
                    : CameraJuiceImpactSignal.ProfileSharpKineticImpactHash;
                CameraTraumaProfileDTO profile = ResolveCameraTraumaProfile(profileHash);
                float amplitudeScale = SanitizePositiveSignalScalar(signal.AmplitudeScale, 1f, CameraJuiceMaximumSignalAmplitudeScale, ref flags);
                float translationGain = SanitizePositiveSignalScalar(signal.TranslationGain, 1f, CameraJuiceMaximumSignalGain, ref flags);
                float rotationGain = SanitizePositiveSignalScalar(signal.RotationGain, 1f, CameraJuiceMaximumSignalGain, ref flags);
                float profileTranslationGain = SanitizePositiveSignalScalar(profile.TranslationGain, 1f, CameraJuiceMaximumSignalGain, ref flags);
                float profileRotationGain = SanitizePositiveSignalScalar(profile.RotationGain, 1f, CameraJuiceMaximumSignalGain, ref flags);
                float profileRadius = SanitizePositiveSignalScalar(profile.RadiusMeters, baseRadiusMeters, 512f, ref flags);
                float profileDecay = SanitizePositiveSignalScalar(profile.DecayPerSecond, PROCEDURAL_TRAUMA_DECAY_RATE, 8f, ref flags);
                float profileFrequency = SanitizePositiveSignalScalar(profile.FrequencyHz, PROCEDURAL_SHAKE_FREQUENCY, 55f, ref flags);
                float radiusOverride = SanitizeSignalScalar(signal.RadiusOverrideMeters, ref flags);
                float radius = radiusOverride > 0.0001f ? math.min(radiusOverride, 512f) : math.max(1f, profileRadius);
                float severity = math.saturate(rawSeverity * amplitudeScale * translationGain * profileTranslationGain);
                if (severity <= 0.0001f)
                    return;

                bool accepted;
                if (TryResolveSignalLocalDirection(signal.Direction, ref flags, out float3 localDirection))
                {
                    accepted = AccumulateAupImpulseWithLocalDirection(
                        signal.Impact.PointAup,
                        severity,
                        radius,
                        localDirection,
                        ref trauma,
                        ref direction,
                        ref count,
                        ref maxMagnitude,
                        ref attenuationSum,
                        ref flags);
                }
                else
                {
                    accepted = AccumulateAupImpulse(
                        signal.Impact.PointAup,
                        severity,
                        radius,
                        ref trauma,
                        ref direction,
                        ref count,
                        ref maxMagnitude,
                        ref attenuationSum,
                        ref flags);
                }

                if (!accepted)
                    return;

                float gainWeight = math.max(0.0001f, severity);
                rotationGainSum += math.clamp(rotationGain * profileRotationGain, CameraJuiceMinimumSignalGain, CameraJuiceMaximumSignalGain) * gainWeight;
                rotationGainWeight += gainWeight;
                if (priority > dominantPriority || (priority == dominantPriority && severity >= dominantProfileWeight))
                {
                    dominantPriority = priority;
                    dominantProfileHash = profile.ProfileHash != 0u ? profile.ProfileHash : profileHash;
                    dominantProfileWeight = severity;
                    dominantProfileDecay = profileDecay;
                    dominantProfileFrequency = profileFrequency;
                }
            }

            private CameraTraumaProfileDTO ResolveCameraTraumaProfile(uint profileHash)
            {
                if (Profiles.IsCreated)
                {
                    int count = Profiles.Length;
                    for (int i = 0; i < count; i++)
                    {
                        CameraTraumaProfileDTO profile = Profiles[i];
                        if (profile.ProfileHash == profileHash && profile.ProfileHash != 0u)
                            return profile;
                    }
                }

                return CameraJuiceBurstMath.FallbackProfile(profileHash);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveSignalPriority(in CameraJuiceImpactSignal signal, ref uint flags)
            {
                if (signal.Priority != 0)
                    return signal.Priority;

                float severity = math.saturate(MaxFinite(signal.Severity, signal.Impact.Intensity, ref flags));
                return ResolveSignalPriorityFromSeverity(severity);
            }

            private bool TryResolveSignalLocalDirection(float3 worldDirection, ref uint flags, out float3 localDirection)
            {
                localDirection = float3.zero;
                if (!math.all(math.isfinite(worldDirection)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                float lengthSq = math.lengthsq(worldDirection);
                if (lengthSq <= 0.000001f)
                    return false;

                float3 normalized = worldDirection * math.rsqrt(math.max(lengthSq, 0.000001f));
                localDirection = new float3(
                    math.dot(normalized, CameraRight),
                    math.dot(normalized, CameraUp),
                    math.dot(normalized, CameraForward));
                if (!math.all(math.isfinite(localDirection)))
                {
                    localDirection = float3.zero;
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                float localLengthSq = math.lengthsq(localDirection);
                if (localLengthSq <= 0.000001f)
                    return false;

                localDirection *= math.rsqrt(math.max(localLengthSq, 0.000001f));
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizePositiveSignalScalar(float value, float fallback, float max, ref uint flags)
            {
                if (!math.isfinite(value))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return math.clamp(fallback, 0f, math.max(0f, max));
                }

                if (value <= 0f)
                    return math.clamp(fallback, 0f, math.max(0f, max));

                return math.clamp(value, 0f, math.max(0f, max));
            }

            private bool AccumulateAupImpulse(
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
                    return false;
                }

                return AccumulateAbsoluteImpulse(ToAbsoluteDouble3Job(in epicenter), severity01, radiusMeters, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum, ref flags);
            }

            private bool AccumulateAupImpulseWithLocalDirection(
                in AbsoluteUniversePosition epicenter,
                float severity01,
                float radiusMeters,
                float3 localDirection,
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
                    return false;
                }

                return AccumulateAbsoluteImpulseWithLocalDirection(
                    ToAbsoluteDouble3Job(in epicenter),
                    severity01,
                    radiusMeters,
                    localDirection,
                    ref trauma,
                    ref direction,
                    ref count,
                    ref maxMagnitude,
                    ref attenuationSum,
                    ref flags);
            }

            private bool AccumulateAbsoluteImpulse(
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
                if (!TryResolveImpulseAttenuation(epicenter, severity01, radiusMeters, ref flags, out float severity, out float attenuation, out float3 delta, out float invDist, out float distSq))
                    return false;

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

                AccumulateResolvedLocalImpulse(severity, attenuation, localDirection, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum);
                return true;
            }

            private bool AccumulateAbsoluteImpulseWithLocalDirection(
                double3 epicenter,
                float severity01,
                float radiusMeters,
                float3 localDirectionOverride,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum,
                ref uint flags)
            {
                if (!math.all(math.isfinite(localDirectionOverride)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                if (!TryResolveImpulseAttenuation(epicenter, severity01, radiusMeters, ref flags, out float severity, out float attenuation, out _, out _, out _))
                    return false;

                AccumulateResolvedLocalImpulse(severity, attenuation, localDirectionOverride, ref trauma, ref direction, ref count, ref maxMagnitude, ref attenuationSum);
                return true;
            }

            private bool TryResolveImpulseAttenuation(
                double3 epicenter,
                float severity01,
                float radiusMeters,
                ref uint flags,
                out float severity,
                out float attenuation,
                out float3 delta,
                out float invDist,
                out float distSq)
            {
                severity = 0f;
                attenuation = 0f;
                delta = float3.zero;
                invDist = 0f;
                distSq = 0f;

                if (!math.all(math.isfinite(PlayerAup)) || !math.all(math.isfinite(epicenter)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                if (!math.isfinite(severity01) || !math.isfinite(radiusMeters))
                    flags |= CameraJuiceFlagNanSanitized;

                severity = math.saturate(math.isfinite(severity01) ? severity01 : 0f);
                if (severity <= 0.0001f)
                    return false;

                double3 deltaD = PlayerAup - epicenter;
                if (!math.all(math.isfinite(deltaD)))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                const double maxLocalMeters = 262144.0;
                deltaD = math.clamp(deltaD, new double3(-maxLocalMeters), new double3(maxLocalMeters));
                delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                distSq = math.lengthsq(delta);
                float safeRadius = math.max(1f, math.isfinite(radiusMeters) ? radiusMeters : 1f);
                invDist = math.rsqrt(math.max(0.0001f, distSq));
                float distance = distSq * invDist;
                attenuation = math.saturate(1f - (distance * math.rcp(safeRadius)));
                if (!math.isfinite(attenuation))
                {
                    flags |= CameraJuiceFlagNanSanitized;
                    return false;
                }

                return attenuation > 0.0001f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void AccumulateResolvedLocalImpulse(
                float severity,
                float attenuation,
                float3 localDirection,
                ref float trauma,
                ref float3 direction,
                ref int count,
                ref float maxMagnitude,
                ref float attenuationSum)
            {
                float weight = severity * attenuation;
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
            public CameraJuiceStateDTO InputState;
            public CameraJuiceImpulseDTO InputImpulse;
            public CameraJuiceTuningDTO Tuning;
            public CameraJuiceStateDTO ResultState;
            public CameraJuiceImpulseDTO ResultImpulse;
            public CameraJuiceProjectionDTO ResultProjection;
            public float DeltaTime;
            public float EffectiveShakeScale;
            public float GlobalQualityWeight01;
            public uint XrActive;
            public uint Sequence;

            public void Execute()
            {
                CameraJuiceStateDTO state = InputState;
                CameraJuiceImpulseDTO impulse = InputImpulse;
                CameraJuiceTuningDTO tuning = Tuning;
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
                    !math.isfinite(tuning.HighOctaveGain) ||
                    !math.isfinite(impulse.RotationGain) ||
                    !math.isfinite(impulse.DominantProfileDecayPerSecond) ||
                    !math.isfinite(impulse.DominantProfileFrequencyHz);
                float dt = math.clamp(math.isfinite(DeltaTime) ? DeltaTime : 0f, 0f, 0.1f);
                float quality = math.saturate(math.isfinite(GlobalQualityWeight01) ? GlobalQualityWeight01 : 1f);
                float effectiveScale = math.isfinite(EffectiveShakeScale) ? math.max(0f, EffectiveShakeScale) : 0f;
                float tuningDecay = math.max(0.01f, math.isfinite(tuning.TraumaDecayPerSecond) ? tuning.TraumaDecayPerSecond : PROCEDURAL_TRAUMA_DECAY_RATE);
                float profileDecay = math.isfinite(impulse.DominantProfileDecayPerSecond) && impulse.DominantProfileDecayPerSecond > 0f
                    ? impulse.DominantProfileDecayPerSecond
                    : tuningDecay;
                float decay = math.clamp(profileDecay, 0.01f, 8f);
                float maxTranslationMeters = math.max(0f, math.isfinite(tuning.MaxTranslationMeters) ? tuning.MaxTranslationMeters : 0f);
                float maxRotationDegrees = math.max(0f, math.isfinite(tuning.MaxRotationDegrees) ? tuning.MaxRotationDegrees : 0f);
                float maxRollDegrees = math.max(0f, math.isfinite(tuning.MaxRollDegrees) ? tuning.MaxRollDegrees : 0f);
                float directionalBiasSeconds = math.max(0f, math.isfinite(tuning.DirectionalBiasSeconds) ? tuning.DirectionalBiasSeconds : PROCEDURAL_DIRECTIONAL_BIAS_SECONDS);
                float highOctaveGain = math.max(0f, math.isfinite(tuning.HighOctaveGain) ? tuning.HighOctaveGain : 0f);
                float rotationGain = math.clamp(math.isfinite(impulse.RotationGain) && impulse.RotationGain > 0f ? impulse.RotationGain : 1f, CameraJuiceMinimumSignalGain, CameraJuiceMaximumSignalGain);
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
                    if (state.TraumaScalar <= 0.0001f)
                    {
                        state.TraumaScalar = 0f;
                        impulse.PriorityAndFlags = 0u;
                    }

                    ResultProjection = new CameraJuiceProjectionDTO
                    {
                        ComfortRotation = quaternion.identity,
                        TraumaScalar = state.TraumaScalar,
                        Flags = (XrActive != 0u ? CameraJuiceFlagXrSuppressed | CameraJuiceFlagVRSomaticWriteRejected : CameraJuiceFlagVRSomaticWriteRejected) |
                            (sanitizedInput ? CameraJuiceFlagNanSanitized : 0u),
                        GlobalQualityWeight01 = quality,
                        StateHash = CameraJuiceBurstMath.HashState(in state, quality)
                    };
                    ResultState = state;
                    ResultImpulse = impulse;
                    return;
                }

                float tuningFrequency = math.max(0.1f, math.isfinite(tuning.BaseFrequencyHz) ? tuning.BaseFrequencyHz : PROCEDURAL_SHAKE_FREQUENCY);
                float profileFrequency = math.isfinite(impulse.DominantProfileFrequencyHz) && impulse.DominantProfileFrequencyHz > 0f
                    ? impulse.DominantProfileFrequencyHz
                    : tuningFrequency;
                float frequency = math.clamp(profileFrequency, 0.1f, 55f) * math.lerp(0.55f, 1.35f, quality);
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
                float3 rotation = rotLow * (maxRotationDegrees * intensity * rotationGain);
                rotation.z += -directional.x * maxRollDegrees * intensity * rotationGain;

                bool sanitizedOutput = !math.all(math.isfinite(translation)) || !math.all(math.isfinite(rotation));
                translation = CameraJuiceBurstMath.SanitizeFloat3(translation);
                rotation = CameraJuiceBurstMath.SanitizeFloat3(rotation);
                float maxMagnitude = math.length(translation);
                state.CurrentTranslationalOffset = translation;
                state.CurrentRotationalOffset = rotation;
                state.TraumaScalar = math.isfinite(trauma) ? math.max(0f, trauma - (decay * dt)) : 0f;
                if (state.TraumaScalar <= 0.0001f)
                {
                    state.TraumaScalar = 0f;
                    impulse.PriorityAndFlags = 0u;
                }

                impulse.DirectionalTimer = math.max(0f, impulse.DirectionalTimer - dt);
                if (impulse.DirectionalTimer <= 0.0001f)
                    impulse.DirectionalMemory = float3.zero;

                quaternion comfortRotation = quaternion.EulerXYZ(math.radians(rotation * 0.1f));
                uint flags = 0u;
                if (sanitizedInput || sanitizedOutput || !math.isfinite(state.TraumaScalar))
                    flags |= CameraJuiceFlagNanSanitized;
                uint stateHash = CameraJuiceBurstMath.HashState(in state, quality) ^ math.hash(new uint2(Sequence, impulse.Sequence));
                ResultProjection = new CameraJuiceProjectionDTO
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
                ResultState = state;
                ResultImpulse = impulse;
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
                    ProfileCount = 4u,
                    Flags = 0u
                };
            }

            public static void WriteDefaultProfiles(NativeArray<CameraTraumaProfileDTO> profiles)
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;
                if (profiles.Length > 0)
                    profiles[0] = FallbackProfile(CameraJuiceImpactSignal.ProfileSharpKineticImpactHash);
                if (profiles.Length > 1)
                    profiles[1] = FallbackProfile(CameraJuiceImpactSignal.ProfileLowFreqSeismicHeaveHash);
                if (profiles.Length > 2)
                    profiles[2] = FallbackProfile(CameraJuiceImpactSignal.ProfileHighFreqToolVibrationHash);
                if (profiles.Length > 3)
                    profiles[3] = FallbackProfile(CameraJuiceImpactSignal.ProfileContinuousPressureStressHash);
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CameraTraumaProfileDTO FallbackProfile(uint hash)
            {
                switch (hash)
                {
                    case CameraJuiceImpactSignal.ProfileHighFreqToolVibrationHash:
                        return DefaultProfile(hash, 0.55f, 0.75f, 24f, 2.4f, 28f);
                    case CameraJuiceImpactSignal.ProfileLowFreqSeismicHeaveHash:
                        return DefaultProfile(hash, 1.10f, 1.35f, 120f, 1.20f, 9f);
                    case CameraJuiceImpactSignal.ProfileContinuousPressureStressHash:
                        return DefaultProfile(hash, 0.85f, 0.95f, 72f, 0.95f, 14f);
                    case CameraJuiceImpactSignal.ProfileSharpKineticImpactHash:
                    default:
                        return DefaultProfile(CameraJuiceImpactSignal.ProfileSharpKineticImpactHash, 1.0f, 1.15f, 56f, 1.65f, 20f);
                }
            }

#if UNITY_EDITOR
            public static int ParseProfilesCsv(ReadOnlySpan<byte> csv, Span<CameraTraumaProfileDTO> profiles)
            {
                if (csv.Length == 0 || profiles.Length == 0)
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
                error |= ValidateSizeMultipleOf8<CameraJuiceStateDTO>(32, 1u);
                error |= ValidateSizeMultipleOf8<CameraJuiceImpulseDTO>(72, 2u);
                error |= ValidateSizeMultipleOf8<CameraJuiceProjectionDTO>(64, 4u);
                error |= ValidateSizeMultipleOf8<CameraJuiceTuningDTO>(64, 8u);
                error |= ValidateSizeMultipleOf8<CameraTraumaProfileDTO>(32, 16u);
                error |= ValidateSizeMultipleOf8<CameraJuiceMockSignalDTO>(64, 32u);
                error |= ValidateSizeMultipleOf8<CameraJuiceImpactSignal>(128, 64u);
                return error;
            }

            private static uint ValidateSizeMultipleOf8<T>(int expectedBytes, uint errorBit)
                where T : struct
            {
                int actualBytes = UnsafeUtility.SizeOf<T>();
                return actualBytes == expectedBytes && (actualBytes & 7) == 0 ? 0u : errorBit;
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
