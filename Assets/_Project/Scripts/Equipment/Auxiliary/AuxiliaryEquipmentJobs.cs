using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Equipment.Auxiliary
{
    internal static unsafe class AuxiliaryNativeAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T WriteRef<T>(NativeArray<T> array, int index)
            where T : struct
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            Hint.Assume(basePtr != null);
            Hint.Assume(index >= 0);
            Hint.Assume(index < array.Length);
            return ref UnsafeUtility.AsRef<T>((byte*)basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T ReadOnlyRef<T>(NativeArray<T> array, int index)
            where T : struct
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            Hint.Assume(basePtr != null);
            Hint.Assume(index >= 0);
            Hint.Assume(index < array.Length);
            return ref UnsafeUtility.AsRef<T>((byte*)basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockAuxiliaryDeploymentsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DeployedAuxiliaryDTO> Deployments;
        [NoAlias] public NativeArray<AuxiliaryStateDTO> States;
        [NoAlias] public NativeArray<AuxiliaryTetherAnchorDTO> TetherAnchors;
        [NoAlias] public NativeArray<AuxiliaryActiveEquipmentDTO> ActiveEquipment;
        [NoAlias] public NativeArray<AuxiliaryRouteCounterDTO> RouteCounters;
        [NoAlias] public NativeArray<AuxiliaryVfxMatrixDTO> VfxMatrices;
        // ActiveCount is host-owned (written before Schedule) — not a Burst peer of Deployments.
        public AuxiliaryTuningDTO Tuning;
        public double3 OriginAup;
        public int RequestedCount;
        public uint FrameIndex;

        public void Execute(int index)
        {
            int safeCount = math.clamp(RequestedCount, 0, Deployments.Length);

            ref DeployedAuxiliaryDTO deployment = ref AuxiliaryNativeAccess.WriteRef(Deployments, index);

            ref AuxiliaryStateDTO state = ref AuxiliaryNativeAccess.WriteRef(States, index);
            ref AuxiliaryActiveEquipmentDTO active = ref AuxiliaryNativeAccess.WriteRef(ActiveEquipment, index);
            if ((uint)index < (uint)RouteCounters.Length)
                AuxiliaryNativeAccess.WriteRef(RouteCounters, index) = default;
            if ((uint)index < (uint)VfxMatrices.Length)
                AuxiliaryNativeAccess.WriteRef(VfxMatrices, index) = default;

            if (index >= safeCount)
            {
                deployment = default;
                state = default;
                if ((uint)index < (uint)TetherAnchors.Length)
                    AuxiliaryNativeAccess.WriteRef(TetherAnchors, index) = default;
                active = default;
                return;
            }

            uint prefabHash = ResolveMockPrefab(index);
            float baseLifetime = AuxiliaryEquipmentMath.ResolveBaseLifetime(prefabHash, in Tuning);
            double lane = (index % 25) - 12;
            double depth = -8.0 - ((index * 7) % 31);
            double forward = (index / 25) * 2.75;
            double3 aup = OriginAup + new double3(lane * 1.75, depth, forward);

            deployment.AUP_Position = aup;
            deployment.PrefabHashID = prefabHash;
            deployment.RemainingLifetime = baseLifetime * math.lerp(0.45f, 1f, ((index * 37) & 255) * AuxiliaryEquipmentMath.InverseByteMax);

            state.BaseLifetime = baseLifetime;
            state.Scalar0 = ResolveMockScalar(prefabHash, in Tuning);
            state.AccumulatedDelta = 0f;
            state.Flags = AuxiliaryEquipmentMath.ResolveKindFlags(prefabHash) | AuxiliaryEquipmentFlags.Mock;

            ref AuxiliaryTetherAnchorDTO tetherAnchor = ref AuxiliaryNativeAccess.WriteRef(TetherAnchors, index);
            tetherAnchor = default;
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
            {
                float tetherDistance = AuxiliaryEquipmentMath.SanitizePositive(Tuning.TetherMaxDistance, 60f);
                tetherAnchor.AnchorAup = aup - new double3(0.0, 0.0, tetherDistance);
                tetherAnchor.Flags = AuxiliaryEquipmentFlags.Active | AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.Mock;
            }

            active.ToolHashID = prefabHash;
            active.CurrentBattery = math.saturate(deployment.RemainingLifetime * math.rcp(math.max(0.01f, baseLifetime)));
            active.ThermalLoad = state.Scalar0;
            active.StateFlags = AuxiliaryEquipmentFlags.Active;
            active.PowerDrawRate = 0f;
            active.HeatGenerationRate = 0f;
        }

        private static uint ResolveMockPrefab(int index)
        {
            int mode = index % 5;
            if (mode <= 1)
                return AuxiliaryEquipmentConstants.FlarePrefabHash;
            if (mode <= 3)
                return AuxiliaryEquipmentConstants.SensorPingPrefabHash;
            return AuxiliaryEquipmentConstants.GravityTetherPrefabHash;
        }

        private static float ResolveMockScalar(uint prefabHash, in AuxiliaryTuningDTO tuning)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.FlarePrefabHash)
                return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.FlareIntensity, 3f);
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.PingMaxRadius, 96f);
            return AuxiliaryEquipmentMath.SanitizeNonNegative(tuning.TetherMaxDistance, 60f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct UpdateDeployedAuxiliaryJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DeployedAuxiliaryDTO> Deployments;
        [NoAlias] public NativeArray<AuxiliaryStateDTO> States;
        [NoAlias] public NativeArray<AuxiliaryTetherAnchorDTO> TetherAnchors;
        [NoAlias] public NativeArray<AuxiliaryActiveEquipmentDTO> ActiveEquipment;
        // Scalar bound only — ActiveCount vault buffer is not a job peer of Deployments.
        // Passing the vault NativeArray alongside Deployments can surface Burst aliasing
        // (two containers, same pointer) when the host resolve path reuses a generation view.
        // Host already computes activeBound via ResolveActiveBound before Schedule.
        public int ActiveCount;
        [NoAlias] public NativeArray<AuxiliaryRouteCounterDTO> RouteCounters;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1: AuxiliaryEquipmentRouterRuntime schedules UpdateDeployedAuxiliaryJob with IJobParallelFor.Schedule, chains it into _pendingHandle through StageAuxiliaryVFXJob, and registers the combined handle through H8Memory.RegisterActiveJob(SystemID.GameplayTools, _pendingHandle).
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: These fields are SignalBus ParallelWriter producer lanes only; each Execute index appends independent signal records and never reads or aliases queue storage, deployment buffers, state buffers, tether buffers, or VFX buffers.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: LateFrameTick finalizes _pendingHandle through DispatcherJobFence.TryFinalizeCompleted before buffer unlock/readback, and teardown uses the forced dispatcher fence before releasing Vault handles, so queue writers cannot outlive the scheduled producer window.
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<AuxiliaryFlareLightSignal>.ParallelWriter FlareWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> FlareWriterBudget;
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<AuxiliarySonarRequestSignal>.ParallelWriter SonarWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> SonarWriterBudget;
        [WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<AuxiliaryTetherConnectionSignal>.ParallelWriter TetherWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> TetherWriterBudget;
        public AuxiliaryTuningDTO Tuning;
        public uint FrameIndex;
        public float SimulationDeltaTime;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            ref AuxiliaryRouteCounterDTO counter = ref AuxiliaryNativeAccess.WriteRef(RouteCounters, index);
            counter = default;

            ref AuxiliaryActiveEquipmentDTO active = ref AuxiliaryNativeAccess.WriteRef(ActiveEquipment, index);
            active = default;

            int activeLength = math.clamp(ActiveCount, 0, Deployments.Length);
            if (index >= activeLength)
                return;


            ref DeployedAuxiliaryDTO deployment = ref AuxiliaryNativeAccess.WriteRef(Deployments, index);
            if (deployment.PrefabHashID == 0u || deployment.RemainingLifetime <= 0f)
            {
                deployment = default;
                if ((uint)index < (uint)States.Length)
                    AuxiliaryNativeAccess.WriteRef(States, index) = default;
                if ((uint)index < (uint)TetherAnchors.Length)
                    AuxiliaryNativeAccess.WriteRef(TetherAnchors, index) = default;
                return;
            }

            ref AuxiliaryStateDTO state = ref AuxiliaryNativeAccess.WriteRef(States, index);
            uint kindFlags = AuxiliaryEquipmentMath.ResolveKindFlags(deployment.PrefabHashID);
            if ((kindFlags & AuxiliaryEquipmentFlags.Faulted) != 0u ||
                !math.all(math.isfinite(deployment.AUP_Position)) ||
                !math.isfinite(deployment.RemainingLifetime))
            {
                counter.FaultFlags = kindFlags | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                deployment = default;
                state = default;
                if ((uint)index < (uint)TetherAnchors.Length)
                    AuxiliaryNativeAccess.WriteRef(TetherAnchors, index) = default;
                return;
            }

            if (!math.isfinite(state.AccumulatedDelta) || state.AccumulatedDelta < 0f)
                state.AccumulatedDelta = 0f;

            float baseLifetime = math.isfinite(state.BaseLifetime) && state.BaseLifetime > 0f
                ? state.BaseLifetime
                : AuxiliaryEquipmentMath.ResolveBaseLifetime(deployment.PrefabHashID, in Tuning);
            float cadenceHz = AuxiliaryEquipmentMath.ResolveCadenceHz(GlobalQualityWeight, in Tuning);
            float quantumSeconds = math.rcp(math.max(1f, cadenceHz));
            float frameDt = math.select(0f, SimulationDeltaTime, math.isfinite(SimulationDeltaTime) & (SimulationDeltaTime > 0f));
            state.AccumulatedDelta += frameDt;

            if (state.AccumulatedDelta < quantumSeconds)
            {
                MirrorActiveEquipment(ref active, deployment.PrefabHashID, deployment.RemainingLifetime, baseLifetime, state.Scalar0, kindFlags);
                state.Flags = (state.Flags | kindFlags) & ~AuxiliaryEquipmentFlags.RoutedThisFrame;
                return;
            }

            float integratedDelta = state.AccumulatedDelta;
            state.AccumulatedDelta = 0f;
            deployment.RemainingLifetime -= integratedDelta;
            if (deployment.RemainingLifetime <= 0f)
            {
                deployment = default;
                state = default;
                if ((uint)index < (uint)TetherAnchors.Length)
                    AuxiliaryNativeAccess.WriteRef(TetherAnchors, index) = default;
                return;
            }

            uint routeFlags = kindFlags | AuxiliaryEquipmentFlags.RoutedThisFrame;
            state.Flags = (state.Flags | routeFlags) & ~AuxiliaryEquipmentFlags.Faulted;
            if ((kindFlags & AuxiliaryEquipmentFlags.Flare) != 0u)
            {
                RouteFlare(index, in deployment, baseLifetime, ref counter);
            }
            else if ((kindFlags & AuxiliaryEquipmentFlags.SensorPing) != 0u)
            {
                RouteSensorPing(index, in deployment, baseLifetime, state.Scalar0, ref counter);
            }
            else if ((kindFlags & AuxiliaryEquipmentFlags.GravityTether) != 0u)
            {
                RouteGravityTether(index, in deployment, ref counter);
            }

            MirrorActiveEquipment(ref active, deployment.PrefabHashID, deployment.RemainingLifetime, baseLifetime, state.Scalar0, kindFlags);
        }

        private void RouteFlare(int index, in DeployedAuxiliaryDTO deployment, float baseLifetime, ref AuxiliaryRouteCounterDTO counter)
        {
            float life01 = math.saturate(deployment.RemainingLifetime * math.rcp(math.max(0.01f, baseLifetime)));
            float noise = AuxiliaryEquipmentMath.DeterministicNoise01(deployment.AUP_Position, FrameIndex, (uint)index);
            float flicker = math.lerp(0.82f, 1.12f, noise);
            float intensity = AuxiliaryEquipmentMath.SanitizeNonNegative(Tuning.FlareIntensity, 0f) *
                              AuxiliaryEquipmentMath.SanitizeNonNegative(Tuning.SignalIntensityScale, 1f) *
                              life01 *
                              flicker;
            AuxiliaryFlareLightSignal signal = default;
            signal.AUP_Position = deployment.AUP_Position;
            signal.Intensity = intensity;
            signal.RangeMeters = AuxiliaryEquipmentMath.SanitizeNonNegative(Tuning.FlareRange, 15f) *
                                 math.lerp(0.65f, 1.25f, AuxiliaryEquipmentMath.Sanitize01(GlobalQualityWeight, 1f));
            signal.SourceHash = AuxiliaryEquipmentMath.HashAupFrame(deployment.AUP_Position, 0u, deployment.PrefabHashID);
            signal.Frame = FrameIndex;
            signal.ColorRgb = new float3(1f, 0.55f, 0.22f);
            signal.QualityWeight = AuxiliaryEquipmentMath.Sanitize01(GlobalQualityWeight, 1f);
            signal.Flags = AuxiliaryEquipmentFlags.Flare;
            if (!IsFinite(in signal))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.Flare | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            SignalBus<AuxiliaryFlareLightSignal>.TryEnqueueBounded(FlareWriter, FlareWriterBudget, signal);
            counter.FlareSignals = 1u;
        }

        private void RouteSensorPing(int index, in DeployedAuxiliaryDTO deployment, float baseLifetime, float authoredMaxRadius, ref AuxiliaryRouteCounterDTO counter)
        {
            float elapsed = math.max(0f, baseLifetime - deployment.RemainingLifetime);
            float defaultMaxRadius = math.max(1f, AuxiliaryEquipmentMath.SanitizeNonNegative(Tuning.PingMaxRadius, 96f));
            float maxRadius = math.select(defaultMaxRadius, math.max(1f, authoredMaxRadius), authoredMaxRadius > 0f & math.isfinite(authoredMaxRadius));
            float baseRate = AuxiliaryEquipmentMath.SanitizeNonNegative(Tuning.PingExpansionRate, 24f);
            float lifetimeRate = maxRadius * math.rcp(math.max(0.01f, baseLifetime));
            float quality = AuxiliaryEquipmentMath.Sanitize01(GlobalQualityWeight, 1f);
            float rate = math.lerp(lifetimeRate * 0.65f, math.max(lifetimeRate, baseRate), math.smoothstep(0f, 1f, quality));
            float radius = math.min(maxRadius, elapsed * rate);
            float intensity = math.saturate(1f - (radius * math.rcp(math.max(1f, maxRadius))));
            AuxiliarySonarRequestSignal signal = default;
            signal.AUP_Position = deployment.AUP_Position;
            signal.CurrentRadius = radius;
            signal.Intensity = intensity;
            signal.SourceHash = AuxiliaryEquipmentMath.HashAupFrame(deployment.AUP_Position, 0u, (uint)index ^ deployment.PrefabHashID);
            signal.Frame = FrameIndex;
            signal.ExpansionRate = rate;
            signal.MaxRadius = maxRadius;
            signal.Flags = AuxiliaryEquipmentFlags.SensorPing;
            if (!IsFinite(in signal))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.SensorPing | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            SignalBus<AuxiliarySonarRequestSignal>.TryEnqueueBounded(SonarWriter, SonarWriterBudget, signal);
            counter.PingSignals = 1u;
        }

        private void RouteGravityTether(int index, in DeployedAuxiliaryDTO deployment, ref AuxiliaryRouteCounterDTO counter)
        {
            if ((uint)index >= (uint)TetherAnchors.Length)
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.Faulted;
                return;
            }

            ref readonly AuxiliaryTetherAnchorDTO tetherAnchor = ref AuxiliaryNativeAccess.ReadOnlyRef(TetherAnchors, index);
            if ((tetherAnchor.Flags & AuxiliaryEquipmentFlags.Active) == 0u ||
                !math.all(math.isfinite(tetherAnchor.AnchorAup)))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            double3 delta = deployment.AUP_Position - tetherAnchor.AnchorAup;
            double distanceSq = math.dot(delta, delta);
            if (!math.isfinite(distanceSq))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            float maxLength = AuxiliaryEquipmentMath.SanitizePositive(Tuning.TetherMaxDistance, 60f);
            double cappedDistanceSq = math.min(math.max(0.0, distanceSq), (double)maxLength * maxLength);
            float restLength = AuxiliaryEquipmentMath.FastLengthFromSq((float)cappedDistanceSq);
            if (!math.isfinite(restLength))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            AuxiliaryTetherConnectionSignal signal = default;
            signal.ProjectileAup = deployment.AUP_Position;
            signal.AnchorAup = tetherAnchor.AnchorAup;
            signal.RestLength = restLength;
            signal.SourceHash = AuxiliaryEquipmentMath.HashAupFrame(deployment.AUP_Position, 0u, (uint)index ^ deployment.PrefabHashID);
            signal.Frame = FrameIndex;
            signal.Flags = AuxiliaryEquipmentFlags.GravityTether;
            if (!IsFinite(in signal))
            {
                counter.FaultFlags = AuxiliaryEquipmentFlags.GravityTether | AuxiliaryEquipmentFlags.NonFiniteRecovered;
                return;
            }

            SignalBus<AuxiliaryTetherConnectionSignal>.TryEnqueueBounded(TetherWriter, TetherWriterBudget, signal);
            counter.TetherSignals = 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in AuxiliaryFlareLightSignal signal)
        {
            return math.all(math.isfinite(signal.AUP_Position)) &&
                   math.isfinite(signal.Intensity) &&
                   math.isfinite(signal.RangeMeters) &&
                   math.all(math.isfinite(signal.ColorRgb)) &&
                   math.isfinite(signal.QualityWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in AuxiliarySonarRequestSignal signal)
        {
            return math.all(math.isfinite(signal.AUP_Position)) &&
                   math.isfinite(signal.CurrentRadius) &&
                   math.isfinite(signal.Intensity) &&
                   math.isfinite(signal.ExpansionRate) &&
                   math.isfinite(signal.MaxRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in AuxiliaryTetherConnectionSignal signal)
        {
            return math.all(math.isfinite(signal.ProjectileAup)) &&
                   math.all(math.isfinite(signal.AnchorAup)) &&
                   math.isfinite(signal.RestLength);
        }

        private static void MirrorActiveEquipment(
            ref AuxiliaryActiveEquipmentDTO active,
            uint prefabHash,
            float remainingLifetime,
            float baseLifetime,
            float scalar0,
            uint kindFlags)
        {
            active.ToolHashID = prefabHash;
            active.CurrentBattery = math.saturate(remainingLifetime * math.rcp(math.max(0.01f, baseLifetime)));
            active.ThermalLoad = scalar0;
            active.StateFlags = AuxiliaryEquipmentFlags.Active | kindFlags;
            active.PowerDrawRate = 0f;
            active.HeatGenerationRate = 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StageAuxiliaryVFXJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<DeployedAuxiliaryDTO> Deployments;
        [ReadOnly, NoAlias] public NativeArray<AuxiliaryStateDTO> States;
        // Scalar bound — same contract as UpdateDeployedAuxiliaryJob.ActiveCount (int).
        public int ActiveCount;
        [NoAlias] public NativeArray<AuxiliaryVfxMatrixDTO> VfxMatrices;
        public double3 CameraAup;
        public float GlobalQualityWeight;
        public float VfxScale;

        public void Execute(int index)
        {
            ref AuxiliaryVfxMatrixDTO matrix = ref AuxiliaryNativeAccess.WriteRef(VfxMatrices, index);
            int activeLength = math.clamp(ActiveCount, 0, Deployments.Length);
            if ((uint)index >= (uint)Deployments.Length || index >= activeLength)

            {
                matrix = default;
                return;
            }

            ref readonly DeployedAuxiliaryDTO deployment = ref AuxiliaryNativeAccess.ReadOnlyRef(Deployments, index);
            if (deployment.PrefabHashID == 0u || deployment.RemainingLifetime <= 0f || !math.all(math.isfinite(deployment.AUP_Position)))
            {
                matrix = default;
                return;
            }

            double3 localDelta = AupPrecisionMath.LocalDeltaDouble(deployment.AUP_Position, CameraAup);
            float3 local = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero);
            float quality = AuxiliaryEquipmentMath.Sanitize01(GlobalQualityWeight, 1f);
            float scale = AuxiliaryEquipmentMath.SanitizePositive(VfxScale, 1f) * math.lerp(0.55f, 1.75f, quality);
            matrix.Row0 = new float4(scale, 0f, 0f, local.x);
            matrix.Row1 = new float4(0f, scale, 0f, local.y);
            matrix.Row2 = new float4(0f, 0f, scale, local.z);
            matrix.Row3 = new float4(0f, 0f, 0f, 1f);
        }
    }

    public ref struct RecordAuxiliaryTelemetryPass
    {
        public NativeArray<DeployedAuxiliaryDTO> Deployments;
        public NativeArray<AuxiliaryRouteCounterDTO> RouteCounters;
        public NativeArray<AuxiliaryTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<int> ActiveCount;
        public uint FrameIndex;
        public float EffectiveCadenceHz;
        public float CpuMicroseconds;
        public float GlobalQualityWeight;
        public uint LaneDroppedSignals;
        public uint LaneCorruptedSignals;
        public uint LanePeakQueuedSignals;

        public void Execute()
        {
            uint active = 0u;
            uint dropped = 0u;
            uint flare = 0u;
            uint ping = 0u;
            uint tether = 0u;
            uint faults = 0u;
            uint hash = 2166136261u;
            int length = Deployments.IsCreated && ActiveCount.IsCreated && ActiveCount.Length > 0
                ? math.clamp(ActiveCount[0], 0, Deployments.Length)
                : 0;
            for (int i = 0; i < length; i++)
            {
                DeployedAuxiliaryDTO deployment = Deployments[i];
                if (deployment.PrefabHashID != 0u && deployment.RemainingLifetime > 0f)
                {
                    active++;
                    hash = AuxiliaryEquipmentMath.FoldSnapshot(hash, in deployment);
                }
                else
                {
                    dropped++;
                }

                if ((uint)i < (uint)RouteCounters.Length)
                {
                    AuxiliaryRouteCounterDTO counter = RouteCounters[i];
                    flare += counter.FlareSignals;
                    ping += counter.PingSignals;
                    tether += counter.TetherSignals;
                    faults |= counter.FaultFlags;
                }
            }

            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0)
                return;

            int cursor = TelemetryCursor[0];
            if (cursor < 0)
                cursor = 0;

            int write = cursor % TelemetryRing.Length;
            AuxiliaryTelemetryEntry entry = default;
            entry.Frame = FrameIndex;
            entry.ActiveCount = active;
            entry.FlareSignals = flare;
            entry.PingSignals = ping;
            entry.TetherSignals = tether;
            entry.EffectiveCadenceHz = EffectiveCadenceHz;
            entry.CpuMicroseconds = CpuMicroseconds;
            entry.GlobalQualityWeight = AuxiliaryEquipmentMath.Sanitize01(GlobalQualityWeight, 1f);
            entry.FaultFlags = faults;
            entry.SnapshotHash = hash;
            entry.DroppedSlots = dropped;
            entry.DroppedSignals = LaneDroppedSignals;
            entry.CorruptedSignals = LaneCorruptedSignals;
            entry.PeakQueuedSignals = LanePeakQueuedSignals;
            TelemetryRing[write] = entry;
            TelemetryCursor[0] = cursor == int.MaxValue ? 0 : cursor + 1;
        }
    }
}
