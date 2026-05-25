using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public sealed partial class VRSomaticProvider
    {
        private const int VRSomaticComfortDtoBytes = 32;
        private const int SomaticTelemetryEntryBytes = 96;
        private const int SomaticHorizonEntityCapacity = 1;
        private const uint SomaticComfortFlagNaN = 1u << 28;
        private const uint SomaticComfortFlagBudgetExceeded = 1u << 29;
        private const uint SomaticComfortFlagHorizonInitialized = 1u << 30;
        private const uint SomaticHorizonTelemetryHash = 0x485A4E31u; // HZN1
        private const float SomaticHorizonBudgetMicroseconds = 100f;

        private VaultBufferView<VRSomaticKinematicStateMirrorDTO> _somaticKccStateMirror;
        private VaultBufferView<quaternion> _somaticRawRotation;
        private VaultBufferView<VRSomaticComfortDTO> _somaticHorizonWrite;
        private VaultBufferView<VRSomaticComfortDTO> _somaticHorizonRead;
        private VaultBufferView<SomaticTelemetryEntry> _somaticHorizonTelemetry;
        private quaternion _somaticStabilizedRotation = quaternion.identity;
        private float4 _somaticLastQuaternionDelta = new float4(0f, 0f, 0f, 1f);
        private float3 _somaticLastRawAngularVelocity;
        private bool _somaticHasStabilizedRotation;
        private int _somaticHorizonTelemetryCursor;

        private unsafe JobHandle ScheduleHorizonLockKernel(
            in global::Hecton8.World.AbsoluteUniversePosition sourceAup,
            quaternion sourceRotation,
            float simulationTickDelta,
            float globalQualityWeight01,
            JobHandle dependency)
        {
            if (!_somaticKccStateMirror.IsCreated ||
                !_somaticRawRotation.IsCreated ||
                !_somaticHorizonWrite.IsCreated ||
                !_somaticHorizonRead.IsCreated ||
                !_somaticDerivatives.IsCreated ||
                !_somaticProfiles.IsCreated)
            {
                return dependency;
            }

            NativeArray<VRSomaticKinematicStateMirrorDTO> kcc = _somaticKccStateMirror.AsNativeArray();
            NativeArray<quaternion> rawRotations = _somaticRawRotation.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> write = _somaticHorizonWrite.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> read = _somaticHorizonRead.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
            NativeArray<VrComfortProfileDTO> profiles = _somaticProfiles.AsNativeArray();
            if (kcc.Length < SomaticHorizonEntityCapacity ||
                rawRotations.Length < SomaticHorizonEntityCapacity ||
                write.Length < SomaticHorizonEntityCapacity ||
                read.Length < SomaticHorizonEntityCapacity ||
                derivatives.Length == 0 ||
                profiles.Length == 0)
            {
                return dependency;
            }

            PrepareKccStateMirrorJob prepareJob = new PrepareKccStateMirrorJob
            {
                KinematicStates = (VRSomaticKinematicStateMirrorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(kcc),
                RawRotations = (quaternion*)NativeArrayUnsafeUtility.GetUnsafePtr(rawRotations),
                SourceAup = ToDouble3(in sourceAup),
                RawRotation = sourceRotation,
                AngularVelocity = new float3(0f, math.select(_kccAngularVelocityRadiansPerSecond, 0f, !math.isfinite(_kccAngularVelocityRadiansPerSecond)), 0f)
            };
            JobHandle prepareHandle = prepareJob.Schedule(SomaticHorizonEntityCapacity, 1, dependency);
            return SchedulePreparedHorizonLockEvaluation(simulationTickDelta, globalQualityWeight01, prepareHandle);
        }

        private unsafe JobHandle SchedulePreparedHorizonLockEvaluation(
            float simulationTickDelta,
            float globalQualityWeight01,
            JobHandle dependency)
        {
            if (!_somaticKccStateMirror.IsCreated ||
                !_somaticRawRotation.IsCreated ||
                !_somaticHorizonWrite.IsCreated ||
                !_somaticHorizonRead.IsCreated ||
                !_somaticDerivatives.IsCreated ||
                !_somaticProfiles.IsCreated)
            {
                return dependency;
            }

            NativeArray<VRSomaticKinematicStateMirrorDTO> kcc = _somaticKccStateMirror.AsNativeArray();
            NativeArray<quaternion> rawRotations = _somaticRawRotation.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> write = _somaticHorizonWrite.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> read = _somaticHorizonRead.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
            NativeArray<VrComfortProfileDTO> profiles = _somaticProfiles.AsNativeArray();
            if (kcc.Length < SomaticHorizonEntityCapacity ||
                rawRotations.Length < SomaticHorizonEntityCapacity ||
                write.Length < SomaticHorizonEntityCapacity ||
                read.Length < SomaticHorizonEntityCapacity ||
                derivatives.Length == 0 ||
                profiles.Length == 0)
            {
                return dependency;
            }

            CalculateFovTunnelingJob fovJob = new CalculateFovTunnelingJob
            {
                KinematicStates = (VRSomaticKinematicStateMirrorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(kcc),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(derivatives),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(profiles),
                ComfortWrite = (VRSomaticComfortDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                GlobalQualityWeight01 = globalQualityWeight01,
                RuntimeComfortBlend01 = _somaticComfortPresence01,
                DeltaTime = math.max(simulationTickDelta, MinimumDeltaTime)
            };
            JobHandle fovHandle = fovJob.Schedule(SomaticHorizonEntityCapacity, 1, dependency);

            EvaluateHorizonStabilizationJob horizonJob = new EvaluateHorizonStabilizationJob
            {
                KinematicStates = (VRSomaticKinematicStateMirrorDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(kcc),
                RawRotations = (quaternion*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rawRotations),
                PreviousRead = (VRSomaticComfortDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(read),
                ComfortWrite = (VRSomaticComfortDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(profiles),
                GlobalQualityWeight01 = globalQualityWeight01,
                DeltaTime = math.max(simulationTickDelta, MinimumDeltaTime)
            };
            return horizonJob.Schedule(SomaticHorizonEntityCapacity, 1, fovHandle);
        }

        private unsafe JobHandle ScheduleMockKinematicJitter(uint frame, float globalQualityWeight01, JobHandle dependency)
        {
            if (!_somaticKccStateMirror.IsCreated || !_somaticRawRotation.IsCreated)
                return dependency;

            NativeArray<VRSomaticKinematicStateMirrorDTO> kcc = _somaticKccStateMirror.AsNativeArray();
            NativeArray<quaternion> rawRotations = _somaticRawRotation.AsNativeArray();
            if (kcc.Length < SomaticHorizonEntityCapacity || rawRotations.Length < SomaticHorizonEntityCapacity)
                return dependency;

            GenerateMockKinematicJitterJob job = new GenerateMockKinematicJitterJob
            {
                KinematicStates = (VRSomaticKinematicStateMirrorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(kcc),
                RawRotations = (quaternion*)NativeArrayUnsafeUtility.GetUnsafePtr(rawRotations),
                GlobalQualityWeight01 = globalQualityWeight01,
                Frame = frame,
                DeltaTime = math.max(HectonXRRuntimeState.FrameIntervalSeconds, MinimumDeltaTime)
            };
            return job.Schedule(SomaticHorizonEntityCapacity, 1, dependency);
        }

        private unsafe bool TryPublishHorizonLockStateFromWrite(out VRSomaticComfortDTO state)
        {
            state = default;
            if (!_somaticHorizonWrite.IsCreated || !_somaticHorizonRead.IsCreated)
                return false;

            NativeArray<VRSomaticComfortDTO> write = _somaticHorizonWrite.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> read = _somaticHorizonRead.AsNativeArray();
            if (write.Length == 0 || read.Length == 0)
                return false;

            UnsafeUtility.MemCpy(
                NativeArrayUnsafeUtility.GetUnsafePtr(read),
                NativeArrayUnsafeUtility.GetUnsafePtr(write),
                UnsafeUtility.SizeOf<VRSomaticComfortDTO>());

            state = read[0];
            state.StabilizedRotation = SanitizeJobQuaternion(state.StabilizedRotation, quaternion.identity);
            state.FovTunnelScalar = Sanitize01(state.FovTunnelScalar, 0f);
            state.PitchDampening = Sanitize01(state.PitchDampening, 0f);
            _somaticStabilizedRotation = state.StabilizedRotation;
            _somaticHasStabilizedRotation = true;
            _somaticFovTunnelingIntensity01 = math.max(_somaticFovTunnelingIntensity01, state.FovTunnelScalar);
            _somaticHorizonLockBlend01 = math.max(_somaticHorizonLockBlend01, state.PitchDampening);
            RecordHorizonTelemetry(in state);
            return true;
        }

        private void RecordHorizonTelemetry(in VRSomaticComfortDTO state)
        {
            if (!_somaticHorizonTelemetry.IsCreated || !_somaticKccStateMirror.IsCreated || !_somaticRawRotation.IsCreated)
                return;

            NativeArray<SomaticTelemetryEntry> telemetry = _somaticHorizonTelemetry.AsNativeArray();
            NativeArray<VRSomaticKinematicStateMirrorDTO> kcc = _somaticKccStateMirror.AsNativeArray();
            NativeArray<quaternion> rawRotations = _somaticRawRotation.AsNativeArray();
            if (telemetry.Length == 0 || kcc.Length == 0 || rawRotations.Length == 0)
                return;

            VRSomaticKinematicStateMirrorDTO rawState = kcc[0];
            quaternion rawRotation = SanitizeJobQuaternion(rawRotations[0], quaternion.identity);
            quaternion stabilized = SanitizeJobQuaternion(state.StabilizedRotation, quaternion.identity);
            quaternion delta = SanitizeJobQuaternion(math.mul(rawRotation, math.inverse(stabilized)), quaternion.identity);
            float burstMicroseconds = 0f;
            if (_somaticScheduleTimestamp > 0L)
            {
                long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - _somaticScheduleTimestamp;
                burstMicroseconds = (float)(ticks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            }

            uint flags = state.ComfortFlags;
            if (!math.all(math.isfinite(stabilized.value)) || !math.all(math.isfinite(delta.value)))
                flags |= SomaticComfortFlagNaN;
            if (burstMicroseconds > SomaticHorizonBudgetMicroseconds)
                flags |= SomaticComfortFlagBudgetExceeded;

            _somaticLastQuaternionDelta = delta.value;
            _somaticLastRawAngularVelocity = rawState.AngularVelocity;
            int index = PositiveModuloSomatic(_somaticHorizonTelemetryCursor, telemetry.Length);
            telemetry[index] = new SomaticTelemetryEntry
            {
                StabilizedRotation = stabilized,
                QuaternionDelta = delta.value,
                RawAngularVelocity = SanitizeJobFloat3(rawState.AngularVelocity),
                FovTunnelScalar = Sanitize01(state.FovTunnelScalar, 0f),
                PitchDampening = Sanitize01(state.PitchDampening, 0f),
                BurstExecutionMicroseconds = SanitizeNonNegative(burstMicroseconds),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = flags,
                StateHash = ResolveHorizonStateHash(in state, rawState.AngularVelocity, delta.value, flags),
                AupHash = ResolveDouble3Hash(rawState.AUP_Position)
            };

            _somaticHorizonTelemetryCursor = _somaticHorizonTelemetryCursor == int.MaxValue
                ? telemetry.Length
                : _somaticHorizonTelemetryCursor + 1;

            if ((flags & (SomaticComfortFlagNaN | SomaticComfortFlagBudgetExceeded)) != 0u)
                DumpComfortTelemetryOnce();
        }

        private quaternion ResolveSomaticStabilizedRootRotation(Quaternion headRotation)
        {
            quaternion head = SanitizeJobQuaternion((quaternion)headRotation, quaternion.identity);
            if (!_somaticHasStabilizedRotation)
                return head;

            float blend = Sanitize01(_somaticHorizonLockBlend01, 0f);
            return SanitizeJobQuaternion(math.slerp(head, _somaticStabilizedRotation, blend), head);
        }

        private void ResetHorizonLockBuffers()
        {
            _somaticKccStateMirror.Release();
            _somaticRawRotation.Release();
            _somaticHorizonWrite.Release();
            _somaticHorizonRead.Release();
            _somaticHorizonTelemetry.Release();
            _somaticKccStateMirror = default;
            _somaticRawRotation = default;
            _somaticHorizonWrite = default;
            _somaticHorizonRead = default;
            _somaticHorizonTelemetry = default;
            _somaticStabilizedRotation = quaternion.identity;
            _somaticLastQuaternionDelta = new float4(0f, 0f, 0f, 1f);
            _somaticLastRawAngularVelocity = float3.zero;
            _somaticHasStabilizedRotation = false;
            _somaticHorizonTelemetryCursor = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ValidateHorizonLockLayouts()
        {
            if (UnsafeUtility.SizeOf<VRSomaticComfortDTO>() != VRSomaticComfortDtoBytes ||
                OffsetOf<VRSomaticComfortDTO>(nameof(VRSomaticComfortDTO.StabilizedRotation)) != 0 ||
                OffsetOf<VRSomaticComfortDTO>(nameof(VRSomaticComfortDTO.FovTunnelScalar)) != 16 ||
                OffsetOf<VRSomaticComfortDTO>(nameof(VRSomaticComfortDTO.PitchDampening)) != 20 ||
                OffsetOf<VRSomaticComfortDTO>(nameof(VRSomaticComfortDTO.ComfortFlags)) != 24 ||
                OffsetOf<VRSomaticComfortDTO>("_pad0") != 28 ||
                UnsafeUtility.SizeOf<VRSomaticKinematicStateMirrorDTO>() != 64 ||
                OffsetOf<VRSomaticKinematicStateMirrorDTO>(nameof(VRSomaticKinematicStateMirrorDTO.AUP_Position)) != 0 ||
                OffsetOf<VRSomaticKinematicStateMirrorDTO>(nameof(VRSomaticKinematicStateMirrorDTO.Velocity)) != 24 ||
                OffsetOf<VRSomaticKinematicStateMirrorDTO>(nameof(VRSomaticKinematicStateMirrorDTO.AngularVelocity)) != 36 ||
                OffsetOf<VRSomaticKinematicStateMirrorDTO>(nameof(VRSomaticKinematicStateMirrorDTO.Flags)) != 52 ||
                UnsafeUtility.SizeOf<SomaticTelemetryEntry>() != SomaticTelemetryEntryBytes ||
                OffsetOf<SomaticTelemetryEntry>(nameof(SomaticTelemetryEntry.RawAngularVelocity)) != 32 ||
                OffsetOf<SomaticTelemetryEntry>(nameof(SomaticTelemetryEntry.FovTunnelScalar)) != 44 ||
                OffsetOf<SomaticTelemetryEntry>(nameof(SomaticTelemetryEntry.BurstExecutionMicroseconds)) != 52)
            {
                throw new InvalidOperationException("VRSomatic horizon-lock ABI drift.");
            }
        }
#endif

#if UNITY_EDITOR
        private void DrawHorizonLockVectorsGizmo()
        {
            if (!_somaticHasStabilizedRotation || !_somaticRawRotation.IsCreated)
                return;

            NativeArray<quaternion> raw = _somaticRawRotation.AsNativeArray();
            if (!raw.IsCreated || raw.Length == 0)
                return;

            Vector3 origin = transform.position + (Vector3.up * 0.65f);
            quaternion rawRotation = SanitizeJobQuaternion(raw[0], quaternion.identity);
            Vector3 rawForward = ToVector3(math.rotate(rawRotation, new float3(0f, 0f, 1f)));
            Vector3 stabilizedForward = ToVector3(math.rotate(_somaticStabilizedRotation, new float3(0f, 0f, 1f)));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + rawForward);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin + (Vector3.up * 0.08f), origin + (Vector3.up * 0.08f) + stabilizedForward);
        }
#endif

        private static double3 ToDouble3(in global::Hecton8.World.AbsoluteUniversePosition aup)
        {
            const double CellSize = global::Hecton8.World.AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                ((double)aup.GridX * CellSize) + aup.LocalX,
                ((double)aup.GridY * CellSize) + aup.LocalY,
                ((double)aup.GridZ * CellSize) + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveLocalAupDeltaFromDouble(double3 playerAup, double3 gridOriginAup)
        {
            double3 localDelta = playerAup - gridOriginAup;
            return math.all(math.isfinite(localDelta)) ? SanitizeJobFloat3((float3)localDelta) : float3.zero;
        }

        private static uint ResolveHorizonStateHash(in VRSomaticComfortDTO state, float3 rawAngularVelocity, float4 quaternionDelta, uint flags)
        {
            uint hash = SomaticHorizonTelemetryHash;
            hash = MixHash(hash, math.asuint(state.FovTunnelScalar));
            hash = MixHash(hash, math.asuint(state.PitchDampening));
            hash = MixHash(hash, math.asuint(rawAngularVelocity.x));
            hash = MixHash(hash, math.asuint(rawAngularVelocity.y));
            hash = MixHash(hash, math.asuint(rawAngularVelocity.z));
            hash = MixHash(hash, math.asuint(quaternionDelta.x));
            hash = MixHash(hash, math.asuint(quaternionDelta.y));
            hash = MixHash(hash, math.asuint(quaternionDelta.z));
            hash = MixHash(hash, math.asuint(quaternionDelta.w));
            return MixHash(hash, flags);
        }

        private static uint ResolveRoundedDoubleHashComponent(double value)
        {
            return unchecked((uint)(long)math.round(math.clamp(value, -2147483648d, 2147483647d)));
        }

        private static uint ResolveDouble3Hash(double3 value)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, ResolveRoundedDoubleHashComponent(value.x * 1000d));
            hash = MixHash(hash, ResolveRoundedDoubleHashComponent(value.y * 1000d));
            return MixHash(hash, ResolveRoundedDoubleHashComponent(value.z * 1000d));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct PrepareKccStateMirrorJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticKinematicStateMirrorDTO* KinematicStates;
            [NativeDisableUnsafePtrRestriction, NoAlias] public quaternion* RawRotations;
            public double3 SourceAup;
            public quaternion RawRotation;
            public float3 AngularVelocity;

            public void Execute(int index)
            {
                double3 safeAup = math.all(math.isfinite(SourceAup)) ? SourceAup : double3.zero;
                float3 safeAngular = ClampLength(SanitizeJobFloat3(AngularVelocity), 48f);
                KinematicStates[index] = new VRSomaticKinematicStateMirrorDTO
                {
                    AUP_Position = safeAup,
                    Velocity = float3.zero,
                    AngularVelocity = safeAngular,
                    Mass = 1f,
                    Flags = 0u,
                    DragCoefficient = 0f,
                    RestingFrameCount = 0,
                    DeepSleepTickCount = 0,
                    SleepMaterialIndex = 0
                };
                RawRotations[index] = SanitizeJobQuaternion(RawRotation, quaternion.identity);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CalculateFovTunnelingJob : IJobParallelFor
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticKinematicStateMirrorDTO* KinematicStates;
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public SomaticDerivativeDTO* Derivatives;
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public VrComfortProfileDTO* Profile;
            [NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticComfortDTO* ComfortWrite;
            public float GlobalQualityWeight01;
            public float RuntimeComfortBlend01;
            public float DeltaTime;

            public void Execute(int index)
            {
                ref readonly VRSomaticKinematicStateMirrorDTO kinematic = ref UnsafeUtility.AsRef<VRSomaticKinematicStateMirrorDTO>(KinematicStates + index);
                ref readonly SomaticDerivativeDTO derivatives = ref UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives);
                ref VRSomaticComfortDTO state = ref UnsafeUtility.AsRef<VRSomaticComfortDTO>(ComfortWrite + index);
                VrComfortProfileDTO profile = SanitizeJobProfile(UnsafeUtility.AsRef<VrComfortProfileDTO>(Profile));
                float quality = SmoothJob01(SanitizeJob01(GlobalQualityWeight01, 1f));
                float comfort = SanitizeJob01(profile.UserComfortWeight01, 1f);
                float3 angular = ClampLength(SanitizeJobFloat3(kinematic.AngularVelocity) + SanitizeJobFloat3(derivatives.AngularVelocity), 48f);
                float angularMagnitude = math.length(angular);
                float derivativeAcceleration = SanitizeJobNonNegative(derivatives.PeakAngularAccelerationRadS2);
                float safeThreshold = math.max(0.01f, profile.AngularVelocitySoftRadS * math.lerp(0.72f, 1.18f, quality));
                float range = math.max(0.01f, safeThreshold * math.lerp(1.25f, 2.5f, quality));
                float speed01 = math.saturate((angularMagnitude - safeThreshold) * math.rcp(range));
                float acceleration01 = math.saturate((derivativeAcceleration - profile.AngularAccelerationSoftRadS2) * math.rcp(math.max(profile.AngularAccelerationSoftRadS2, 0.01f)));
                float target = math.saturate(math.max(SmoothJob01(speed01), SmoothJob01(acceleration01)) * profile.VrBaselineFovTunnel * profile.FovAggressiveness * comfort * SanitizeJob01(RuntimeComfortBlend01, 0f));
                float current = SanitizeJob01(state.FovTunnelScalar, 0f);
                float sharpness = target > current ? profile.EwmaSharpness : profile.ReleaseSharpness;
                float blend = 1f - MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0.01f, sharpness) * math.max(DeltaTime, MinimumDeltaTime));
                state.FovTunnelScalar = SanitizeJob01(math.lerp(current, target, SanitizeJob01(blend, 0f)), current);
                state.ComfortFlags = state.FovTunnelScalar > 0.001f
                    ? state.ComfortFlags | SomaticComfortFlagFovTunnel
                    : state.ComfortFlags & ~SomaticComfortFlagFovTunnel;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EvaluateHorizonStabilizationJob : IJobParallelFor
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticKinematicStateMirrorDTO* KinematicStates;
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public quaternion* RawRotations;
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticComfortDTO* PreviousRead;
            [NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticComfortDTO* ComfortWrite;
            [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public VrComfortProfileDTO* Profile;
            public float GlobalQualityWeight01;
            public float DeltaTime;

            public void Execute(int index)
            {
                ref readonly VRSomaticKinematicStateMirrorDTO kinematic = ref UnsafeUtility.AsRef<VRSomaticKinematicStateMirrorDTO>(KinematicStates + index);
                ref readonly VRSomaticComfortDTO previous = ref UnsafeUtility.AsRef<VRSomaticComfortDTO>(PreviousRead + index);
                ref VRSomaticComfortDTO state = ref UnsafeUtility.AsRef<VRSomaticComfortDTO>(ComfortWrite + index);
                VrComfortProfileDTO profile = SanitizeJobProfile(UnsafeUtility.AsRef<VrComfortProfileDTO>(Profile));
                quaternion rawRotation = SanitizeJobQuaternion(RawRotations[index], quaternion.identity);
                quaternion previousRotation = (previous.ComfortFlags & SomaticComfortFlagHorizonInitialized) != 0u
                    ? SanitizeJobQuaternion(previous.StabilizedRotation, rawRotation)
                    : rawRotation;

                float3 forward = math.rotate(rawRotation, new float3(0f, 0f, 1f));
                float3 levelForward = new float3(forward.x, 0f, forward.z);
                float levelLengthSq = math.lengthsq(levelForward);
                if (!math.isfinite(levelLengthSq) || levelLengthSq <= 0.000001f)
                    levelForward = new float3(0f, 0f, 1f);
                else
                    levelForward *= math.rsqrt(levelLengthSq);
                quaternion yawOnly = SanitizeJobQuaternion(quaternion.LookRotationSafe(levelForward, new float3(0f, 1f, 0f)), rawRotation);

                float3 rawUp = math.rotate(rawRotation, new float3(0f, 1f, 0f));
                float upError = math.saturate(math.lengthsq(math.cross(SanitizeJobFloat3(rawUp), new float3(0f, 1f, 0f))));
                float angularAssist = SmoothJob01(math.length(ClampLength(SanitizeJobFloat3(kinematic.AngularVelocity), 48f)) * math.rcp(12f));
                float quality = SmoothJob01(SanitizeJob01(GlobalQualityWeight01, 1f));
                float comfort = SanitizeJob01(profile.UserComfortWeight01, 1f);
                float gravityWeight = math.saturate(math.max(SmoothJob01(upError), angularAssist) * comfort * math.lerp(1.25f, 0.82f, quality));
                quaternion targetRotation = SanitizeJobQuaternion(math.slerp(rawRotation, yawOnly, gravityWeight), yawOnly);
                float springOmega = math.max(0.01f, profile.HorizonLockSpeed) * math.lerp(4.75f, 2.35f, quality);
                float blend = ResolveCriticalDampedSpringBlend(springOmega, DeltaTime, quality);
                quaternion stabilized = SanitizeJobQuaternion(math.slerp(previousRotation, targetRotation, blend), targetRotation);

                float3 sdfLocalProbe = ResolveLocalAupDeltaFromDouble(kinematic.AUP_Position, math.floor(kinematic.AUP_Position * 0.001d) * 1000d);
                float precisionProof = math.saturate(math.lengthsq(sdfLocalProbe) * 0.000001f);
                state.StabilizedRotation = stabilized;
                state.PitchDampening = math.saturate(math.max(gravityWeight, precisionProof * 0.001f));
                state.ComfortFlags |= SomaticComfortFlagHorizonInitialized;
                state.ComfortFlags = state.PitchDampening > 0.001f
                    ? state.ComfortFlags | SomaticComfortFlagHorizonLock
                    : state.ComfortFlags & ~SomaticComfortFlagHorizonLock;
                if (!math.all(math.isfinite(stabilized.value)))
                {
                    state.StabilizedRotation = quaternion.identity;
                    state.ComfortFlags |= SomaticComfortFlagNaN;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockKinematicJitterJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public VRSomaticKinematicStateMirrorDTO* KinematicStates;
            [NativeDisableUnsafePtrRestriction, NoAlias] public quaternion* RawRotations;
            public float GlobalQualityWeight01;
            public uint Frame;
            public float DeltaTime;

            public void Execute(int index)
            {
                float quality = SmoothJob01(SanitizeJob01(GlobalQualityWeight01, 1f));
                float t = (Frame + (uint)index) * math.lerp(0.37f, 0.083f, quality);
                float triangle = math.abs(frac(t * 2.37f) * 2f - 1f);
                float pulse = (triangle * 2f) - 1f;
                float amplitude = math.lerp(1.4f, 3.8f, quality);
                float pitch = MathLodApproximation.ApproxSinBhaskara(t * 3.1f) * 0.28f * amplitude;
                float yaw = pulse * 0.72f * amplitude;
                float roll = MathLodApproximation.ApproxSinBhaskara(t * 5.3f) * 0.42f * amplitude;
                quaternion raw = SanitizeJobQuaternion(math.mul(math.mul(quaternion.RotateY(yaw), quaternion.RotateX(pitch)), quaternion.RotateZ(roll)), quaternion.identity);
                RawRotations[index] = raw;
                KinematicStates[index] = new VRSomaticKinematicStateMirrorDTO
                {
                    AUP_Position = new double3(index, 0d, 0d),
                    Velocity = float3.zero,
                    AngularVelocity = ClampLength(new float3(pitch, yaw, roll) * math.rcp(math.max(DeltaTime, MinimumDeltaTime)), 48f),
                    Mass = 1f,
                    Flags = SomaticComfortFlagMockData,
                    DragCoefficient = 0f,
                    RestingFrameCount = 0,
                    DeepSleepTickCount = 0,
                    SleepMaterialIndex = 0
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveCriticalDampedSpringBlend(float omega, float deltaTime, float globalQualityWeight01)
        {
            float x = math.min(math.max(omega, 0.0001f) * math.max(deltaTime, MinimumDeltaTime), 32f);
            float x2 = x * x;
            float cheapCriticalApprox = x2 * math.rcp(math.max(0.0001f, 2f + (2f * x) + x2));
            float quality = SanitizeJob01(globalQualityWeight01, 1f);
            float exactWeight = math.smoothstep(0.3f, 0.85f, quality);
            if (exactWeight <= 0f)
                return SanitizeJob01(cheapCriticalApprox, 0f);

            float exactCritical = 1f - ((1f + x) * MathLodApproximation.ApproxExpNegPade33Wide40(x));
            return SanitizeJob01(math.lerp(cheapCriticalApprox, exactCritical, exactWeight), 0f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearHorizonTelemetryJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<SomaticTelemetryEntry> Telemetry;

            public void Execute(int index)
            {
                Telemetry[index] = default;
            }
        }
    }
}
