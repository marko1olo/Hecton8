using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    internal static class SeaglideSimdMath
    {
        public const float AuthoritativeQualityWeight = 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            float finiteSq = math.select(0f, lengthSq, math.isfinite(lengthSq));
            float safeSq = math.max(finiteSq, 0.0001f);
            return math.select(0f, safeSq * math.rsqrt(math.max(safeSq, 0.0001f)), finiteSq > 0.0001f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InitializeSeaglideColdBuffersJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<SeaglideFlowSampleDTO> FlowSamples;
        [WriteOnly, NoAlias] public NativeArray<SeaglideTelemetryEntry> TelemetryRing;
        [WriteOnly, NoAlias] public NativeArray<int> TelemetryCursor;
        [WriteOnly, NoAlias] public NativeArray<SeaglideCounterDTO> Counters;
        [WriteOnly, NoAlias] public NativeArray<SeaglideBodyBindingDTO> BodyBindings;
        [WriteOnly, NoAlias] public NativeArray<SeaglideVisualStateDTO> VisualStates;
        [WriteOnly, NoAlias] public NativeArray<SeaglideAudioSignalDTO> AudioSignals;
        [WriteOnly, NoAlias] public NativeArray<SeaglideCavitationVfxSignalDTO> CavitationSignals;

        public void Execute()
        {
            Clear(FlowSamples);
            Clear(TelemetryRing);
            Clear(TelemetryCursor);
            Clear(Counters);
            Clear(BodyBindings);
            Clear(VisualStates);
            Clear(AudioSignals);
            Clear(CavitationSignals);
        }

        private static void Clear<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GenerateMockSeaglidePropulsionDataJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's parallel-for safety is kept enabled: this cold/editor mock job writes only States[index] and Requests[index], and the runtime clamps the scheduled count to the shared minimum capacity.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A serial mock hydrator was rejected because Task 05 requires worst-case vectorized request pressure. Temporary staging arrays were rejected because they would add non-Vault native ownership and extra copy bandwidth before the actual hydrodynamic solver.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one producer job, one index owner, no cross-index reads, and no overlapping write ranges. Live solver scheduling is blocked while this cold/editor mock job runs to completion.
        [NoAlias] public NativeArray<SeaglideStateDTO> States;
        [NoAlias] public NativeArray<SeaglidePropulsionRequestDTO> Requests;
        public int ActiveMockCount;
        public double3 OriginAUP;
        public uint SimulationFrame;

        public void Execute(int index)
        {
            if (!States.IsCreated || !Requests.IsCreated || (uint)index >= (uint)States.Length || (uint)index >= (uint)Requests.Length)
                return;

            bool active = index < math.max(0, ActiveMockCount);
            uint targetHash = math.select(0u, (uint)(index + 1) * 0x9E3779B9u, active);
            float lane = (index & 31) - 15.5f;
            float row = ((index >> 5) & 31) - 15.5f;
            float depth = 4f + ((index * 17) & 127) * 0.125f;
            float phase = (index * 0.03125f) + (SimulationFrame * 0.011f);
            float thrustSwing = TriangleSigned(phase);
            double3 origin = math.select(double3.zero, OriginAUP, math.isfinite(OriginAUP));
            double3 aup = origin + new double3(lane * 2.5f, -depth, row * 2.5f);
            float3 forward = SafeNormalize(new float3(0.12f * thrustSwing, 0.02f * TriangleSigned(phase + 0.21f), 1f), new float3(0f, 0f, 1f));
            float battery = math.saturate(0.35f + ((index * 13) & 63) * (1f / 96f));
            float throttle = math.saturate(0.42f + 0.58f * math.abs(thrustSwing));

            SeaglideStateDTO state = default;
            state.CurrentAUP = math.select(double3.zero, aup, active);
            state.Velocity = math.select(float3.zero, forward * (1.5f + throttle * 5f), active);
            state.BatteryLevel = math.select(0f, battery, active);
            state.ActiveFlags = math.select(0u, SeaglideHydrodynamicsConstants.FlagActive | SeaglideHydrodynamicsConstants.FlagEmergencyMock, active);
            state.TargetEntityHash = targetHash;
            state.MassKg = SeaglideHydrodynamicsConstants.DefaultBaseMassKg;
            state.AddedMassKg = SeaglideHydrodynamicsConstants.DefaultAddedMassKg;
            state.FrameIndex = SimulationFrame;
            States[index] = state;

            SeaglidePropulsionRequestDTO request = default;
            request.CurrentAUP = state.CurrentAUP;
            request.PreviousAUP = RewindAupByLocalVelocity(state.CurrentAUP, state.Velocity, 0.02f);
            request.InputVector = forward;
            request.ForwardVector = forward;
            request.Throttle01 = math.select(0f, throttle, active);
            request.DeltaTime = 0.02f;
            request.TargetEntityHash = targetHash;
            request.RequestHash = SeaglideHydrodynamicsConstants.SourceHash ^ (uint)(index + 1);
            request.Flags = state.ActiveFlags;
            request.FrameIndex = SimulationFrame;
            request.BatteryLevel = state.BatteryLevel;
            request.SurfaceNormal = new float3(0f, 1f, 0f);
            Requests[index] = request;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float sq = math.lengthsq(value);
            return math.select(fallback, value * math.rsqrt(math.max(sq, 0.000001f)), math.isfinite(sq) & sq > 0.000001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 RewindAupByLocalVelocity(double3 currentAup, float3 velocity, float deltaTime)
        {
            if (!math.all(math.isfinite(currentAup)) || !math.all(math.isfinite(velocity)) || !math.isfinite(deltaTime))
                return currentAup;

            double safeDelta = math.max((double)deltaTime, 0.0001d);
            double3 displacementMeters = new double3(
                (double)velocity.x * safeDelta,
                (double)velocity.y * safeDelta,
                (double)velocity.z * safeDelta);
            // Rewind in a local AUP frame, then rehydrate, so mock payloads follow the same precision rule as live requests.
            double3 localOrigin = currentAup;
            double3 currentLocal = AupPrecisionMath.LocalDeltaDouble(currentAup, localOrigin);
            double3 previousLocal = currentLocal - displacementMeters;
            double3 previous = localOrigin + previousLocal;
            return math.all(math.isfinite(previous)) ? previous : currentAup;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CalculateSeaglideThrustJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's parallel-for safety remains enabled: States, ForcePackets, VisualStates, and CavitationSignals are mutated only at Execute(index). The runtime locks all involved BufferIDs before scheduling and uses a clamped active row window.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Splitting this into separate jobs was rejected because it would add extra schedule overhead and additional Vault passes for the same per-row thrust solve. Atomic packet counters were rejected because packet validity is reduced after the parallel loop instead of using contended writes inside it.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is index-local mutation only: States[index], ForcePackets[index], VisualStates[index], and CavitationSignals[index] are the only rows written by Execute(index). Reductions and counts are written later by ReduceSeaglideTelemetryJob after this handle completes.
        [NoAlias] public NativeArray<SeaglideStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<SeaglidePropulsionRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<SeaglideFlowSampleDTO> FlowSamples;
        [ReadOnly, NoAlias] public NativeArray<SeaglideTuningDTO> Tuning;
        [WriteOnly, NoAlias] public NativeArray<SeaglideForcePacketDTO> ForcePackets;
        [WriteOnly, NoAlias] public NativeArray<SeaglideVisualStateDTO> VisualStates;
        [WriteOnly, NoAlias] public NativeArray<SeaglideCavitationVfxSignalDTO> CavitationSignals;
        public int ActiveRequestCount;
        public uint SimulationFrame;
        public float SimulationTickDelta;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!States.IsCreated || !Requests.IsCreated || !ForcePackets.IsCreated || (uint)index >= (uint)States.Length || (uint)index >= (uint)Requests.Length)
                return;

            WriteForce(index, default);
            WriteVisual(index, default);
            WriteCavitation(index, default);

            if ((uint)index >= (uint)math.max(0, ActiveRequestCount))
                return;

            SeaglideTuningDTO tuning = ReadTuning();
            SeaglideStateDTO state = States[index];
            SeaglidePropulsionRequestDTO request = Requests[index];
            bool active = (request.Flags & SeaglideHydrodynamicsConstants.FlagActive) != 0u &&
                          request.TargetEntityHash != 0u &&
                          request.Throttle01 > SeaglideHydrodynamicsConstants.Epsilon;
            bool finiteInput = IsFinite(request.CurrentAUP) &
                               IsFinite(request.PreviousAUP) &
                               math.all(math.isfinite(request.InputVector)) &
                               math.all(math.isfinite(request.ForwardVector)) &
                               math.all(math.isfinite(state.Velocity)) &
                               math.isfinite(request.Throttle01) &
                               math.isfinite(request.BatteryLevel);

            float presentationQuality = math.saturate(math.select(
                SeaglideSimdMath.AuthoritativeQualityWeight,
                GlobalQualityWeight,
                math.isfinite(GlobalQualityWeight)));
            double3 sector = math.select(double3.zero, tuning.SectorAUP, math.isfinite(tuning.SectorAUP));
            double3 currentAup = math.select(double3.zero, request.CurrentAUP, math.isfinite(request.CurrentAUP));
            double3 localAupDelta = AupPrecisionMath.LocalDeltaDouble(currentAup, sector);
            float3 localAup = SanitizeFinite(AupPrecisionMath.DowncastLocalDelta(localAupDelta, float3.zero), float3.zero);
            float3 inputDirection = SafeNormalize(request.InputVector, SafeNormalize(request.ForwardVector, new float3(0f, 0f, 1f)));
            float throttle = math.saturate(SanitizeFinite(request.Throttle01, 0f));
            float battery = math.saturate(SanitizeFinite(request.BatteryLevel, state.BatteryLevel));
            float maxThrust = math.max(0f, SanitizeFinite(math.select(tuning.MaxThrustN, request.MaxThrustOverrideN, request.MaxThrustOverrideN > 0f), SeaglideHydrodynamicsConstants.DefaultMaxThrustN));
            float mass = math.max(1f, SanitizeFinite(math.select(tuning.BaseMassKg, state.MassKg, state.MassKg > 0f), SeaglideHydrodynamicsConstants.DefaultBaseMassKg));
            float addedMass = math.max(0f, SanitizeFinite(math.select(tuning.AddedMassKg, state.AddedMassKg, state.AddedMassKg > 0f), SeaglideHydrodynamicsConstants.DefaultAddedMassKg));
            float waterDensity = math.max(1f, SanitizeFinite(tuning.WaterDensityKgPerM3, SeaglideHydrodynamicsConstants.DefaultWaterDensityKgPerM3));
            float crossSection = math.max(0.01f, SanitizeFinite(math.select(tuning.CrossSectionAreaM2, request.CrossSectionAreaOverrideM2, request.CrossSectionAreaOverrideM2 > 0f), SeaglideHydrodynamicsConstants.DefaultCrossSectionAreaM2));
            float quadraticDragCoefficient = math.max(0f, SanitizeFinite(math.select(tuning.QuadraticDragCoefficient, request.DragCoefficientOverride, request.DragCoefficientOverride > 0f), SeaglideHydrodynamicsConstants.DefaultQuadraticDragCoefficient));
            int flowSampleCount = math.clamp(tuning.FlowSampleCount, 0, FlowSamples.IsCreated ? FlowSamples.Length : 0);

            float3 flowVelocity = ResolveFlowVelocity(request.CurrentAUP, localAup, FlowSamples, flowSampleCount, SimulationFrame);
            float3 relativeVelocity = SanitizeFinite(state.Velocity - flowVelocity, float3.zero);
            float speedSq = math.lengthsq(relativeVelocity);
            float speed = SeaglideSimdMath.LengthFromSq(speedSq);
            float3 thrustForce = inputDirection * (maxThrust * throttle * battery);
            float3 quadraticDrag = -SafeNormalize(relativeVelocity, float3.zero) * (0.5f * waterDensity * speedSq * quadraticDragCoefficient * crossSection);
            float3 dragForce = quadraticDrag;
            float3 flowForce = flowVelocity * math.max(0f, tuning.FlowForceCoefficient) * (mass + addedMass);
            float forceCadenceScale = ResolveForceCadenceScale(SimulationTickDelta, request.DeltaTime);
            float3 netForce = (thrustForce + dragForce + flowForce) * forceCadenceScale;
            float forceMagnitudeSq = math.lengthsq(netForce);
            bool mathFinite = active & finiteInput &
                              math.all(math.isfinite(thrustForce)) &
                              math.all(math.isfinite(dragForce)) &
                              math.all(math.isfinite(flowForce)) &
                              math.all(math.isfinite(netForce)) &
                              math.isfinite(forceMagnitudeSq);
            bool queue = mathFinite & forceMagnitudeSq > 0.000001f;
            float forceMagnitude = math.select(0f, SeaglideSimdMath.LengthFromSq(forceMagnitudeSq), queue);

            state.CurrentAUP = currentAup;
            state.Velocity = SanitizeFinite(state.Velocity, float3.zero);
            state.BatteryLevel = battery;
            state.ActiveFlags = request.Flags | math.select(0u, SeaglideHydrodynamicsConstants.FlagNonFinite, !finiteInput | (active & !mathFinite));
            state.ActiveFlags |= math.select(0u, SeaglideHydrodynamicsConstants.FlagForceQueued, queue);
            state.TargetEntityHash = request.TargetEntityHash;
            state.MassKg = mass;
            state.AddedMassKg = addedMass;
            state.FrameIndex = SimulationFrame;
            States[index] = state;

            SeaglideForcePacketDTO packet = default;
            packet.CurrentAUP = math.select(double3.zero, currentAup, queue);
            packet.NetForce = math.select(float3.zero, netForce, queue);
            packet.ThrustForce = math.select(float3.zero, thrustForce, queue);
            packet.DragForce = math.select(float3.zero, dragForce, queue);
            packet.FlowForce = math.select(float3.zero, flowForce, queue);
            packet.RelativeVelocity = math.select(float3.zero, relativeVelocity, queue);
            packet.TargetEntityHash = math.select(0u, request.TargetEntityHash, queue);
            packet.Flags = math.select(0u, state.ActiveFlags, queue);
            packet.StateIndex = math.select(0, index, queue);
            packet.FrameIndex = SimulationFrame;
            packet.ForceMagnitude = forceMagnitude;
            packet.BatteryLevel = battery;
            packet.MassKg = mass;
            packet.AddedMassKg = addedMass;
            packet.Throttle01 = throttle;
            packet.CurrentSpeed = math.select(0f, speed, queue);
            WriteForce(index, packet);

            float cavitation01 = ResolveCavitation(speed, tuning.CavitationSpeedStart, tuning.CavitationSpeedFull, presentationQuality);
            SeaglideVisualStateDTO visual = default;
            visual.CurrentAUP = currentAup;
            visual.WakeDirection = -inputDirection;
            visual.WakeIntensity01 = math.saturate(throttle * battery);
            visual.Cavitation01 = cavitation01;
            visual.BrakeCloud01 = math.saturate(math.lengthsq(dragForce) * 0.000001f);
            visual.SourceHash = request.RequestHash != 0u ? request.RequestHash : SeaglideHydrodynamicsConstants.SourceHash;
            visual.Flags = SeaglideHydrodynamicsConstants.FlagVisualOnly | SeaglideHydrodynamicsConstants.FlagRollbackExcluded;
            WriteVisual(index, visual);

            SeaglideCavitationVfxSignalDTO cavitation = default;
            cavitation.CurrentAUP = currentAup;
            cavitation.Direction = -inputDirection;
            cavitation.Intensity01 = cavitation01;
            cavitation.RadiusMeters = math.lerp(0.4f, 1.8f, cavitation01);
            cavitation.SourceHash = visual.SourceHash;
            cavitation.Flags = math.select(SeaglideHydrodynamicsConstants.FlagVisualOnly | SeaglideHydrodynamicsConstants.FlagRollbackExcluded,
                SeaglideHydrodynamicsConstants.FlagVisualOnly | SeaglideHydrodynamicsConstants.FlagRollbackExcluded | SeaglideHydrodynamicsConstants.FlagCavitationSignal,
                cavitation01 > 0.01f);
            cavitation.FrameIndex = SimulationFrame;
            WriteCavitation(index, cavitation);
        }

        private SeaglideTuningDTO ReadTuning()
        {
            return Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : SeaglideTuningDTO.Default();
        }

        private void WriteForce(int index, SeaglideForcePacketDTO value)
        {
            if (ForcePackets.IsCreated && (uint)index < (uint)ForcePackets.Length)
                ForcePackets[index] = value;
        }

        private void WriteVisual(int index, SeaglideVisualStateDTO value)
        {
            if (VisualStates.IsCreated && (uint)index < (uint)VisualStates.Length)
                VisualStates[index] = value;
        }

        private void WriteCavitation(int index, SeaglideCavitationVfxSignalDTO value)
        {
            if (CavitationSignals.IsCreated && (uint)index < (uint)CavitationSignals.Length)
                CavitationSignals[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveFlowVelocity(
            double3 currentAup,
            float3 localAup,
            NativeArray<SeaglideFlowSampleDTO> flowSamples,
            int flowSampleCount,
            uint frame)
        {
            if (flowSamples.IsCreated && flowSampleCount >= 8)
            {
                SeaglideFlowSampleDTO anchor = flowSamples[0];
                float cell = math.max(1f, anchor.CellSizeMeters);
                double3 localDelta = AupPrecisionMath.LocalDeltaDouble(currentAup, anchor.SampleAUP);
                float3 local = AupPrecisionMath.DowncastLocalDelta(localDelta, float3.zero) * math.rcp(math.max(cell, SeaglideHydrodynamicsConstants.Epsilon));
                float3 t = math.saturate(local);
                float3 c00 = math.lerp(flowSamples[0].FlowVelocity, flowSamples[1].FlowVelocity, t.x);
                float3 c10 = math.lerp(flowSamples[2].FlowVelocity, flowSamples[3].FlowVelocity, t.x);
                float3 c01 = math.lerp(flowSamples[4].FlowVelocity, flowSamples[5].FlowVelocity, t.x);
                float3 c11 = math.lerp(flowSamples[6].FlowVelocity, flowSamples[7].FlowVelocity, t.x);
                float3 c0 = math.lerp(c00, c10, t.y);
                float3 c1 = math.lerp(c01, c11, t.y);
                return SanitizeFinite(math.lerp(c0, c1, t.z), float3.zero);
            }

            float phaseX = (localAup.x * 0.017f) + (frame * 0.0031f);
            float phaseY = (localAup.y * 0.011f) + (frame * 0.0017f);
            float phaseZ = (localAup.z * 0.019f) + (frame * 0.0023f);
            return new float3(
                TriangleSigned(phaseX) * 0.42f,
                TriangleSigned(phaseY) * 0.08f,
                TriangleSigned(phaseZ) * 0.38f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveCavitation(float speed, float start, float full, float quality)
        {
            float safeStart = math.max(0.1f, SanitizeFinite(start, 7.5f));
            float safeFull = math.max(safeStart + 0.1f, SanitizeFinite(full, 14f));
            float range = math.max(SeaglideHydrodynamicsConstants.Epsilon, safeFull - safeStart);
            float raw = math.saturate((speed - safeStart) * math.rcp(range));
            return Smooth01(raw) * math.lerp(0.65f, 1f, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float sq = math.lengthsq(value);
            return math.select(fallback, value * math.rsqrt(math.max(sq, 0.000001f)), math.isfinite(sq) & sq > 0.000001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveForceCadenceScale(float solverDeltaTime, float requestDeltaTime)
        {
            float fixedDt = math.clamp(math.select(0.02f, requestDeltaTime, math.isfinite(requestDeltaTime) & requestDeltaTime > 0f), 0.0001f, 0.2f);
            float solverDt = math.clamp(math.select(fixedDt, solverDeltaTime, math.isfinite(solverDeltaTime) & solverDeltaTime > 0f), fixedDt, 0.2f);
            return math.clamp(solverDt * math.rcp(math.max(fixedDt, 0.0001f)), 1f, 4f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ProcessSeaglideMetabolismJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's parallel-for safety remains enabled: the runtime resolves the BufferIDs separately and this job mutates only the state row matching the parallel index.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Folding metabolism into the thrust job was rejected because low-quality cadence can skip metabolism independently from force generation. A serial SlowTick pass was rejected because it would touch active rows on the main thread and hide scaling cost.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one write to States[index] after reading ForcePackets[index], with the job dependent on the thrust handle. No other scheduled Seaglide job writes States[index] concurrently outside that dependency chain.
        [NoAlias] public NativeArray<SeaglideStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<SeaglideForcePacketDTO> ForcePackets;
        [ReadOnly, NoAlias] public NativeArray<SeaglideTuningDTO> Tuning;
        public int ActiveRequestCount;
        public int MetabolismEnabled;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            if (MetabolismEnabled == 0 || !States.IsCreated || !ForcePackets.IsCreated || (uint)index >= (uint)States.Length || (uint)index >= (uint)ForcePackets.Length || (uint)index >= (uint)math.max(0, ActiveRequestCount))
                return;

            SeaglideTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : SeaglideTuningDTO.Default();
            SeaglideStateDTO state = States[index];
            SeaglideForcePacketDTO packet = ForcePackets[index];
            float dt = math.clamp(math.select(tuning.SimulationTickDelta, SimulationTickDelta, SimulationTickDelta > 0f), 0.0001f, 0.5f);
            float forceMagnitude = math.max(0f, packet.ForceMagnitude);
            float drain = (math.max(0f, tuning.BatteryBaseDrainPerSecond) + forceMagnitude * math.max(0f, tuning.BatteryLoadDrainPerNewton)) * dt;
            state.BatteryLevel = math.saturate(state.BatteryLevel - drain);
            state.ActiveFlags |= SeaglideHydrodynamicsConstants.FlagMetabolismEvaluated;
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CalculateSeaglideAudioParametersJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SeaglidePropulsionRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<SeaglideForcePacketDTO> ForcePackets;
        [ReadOnly, NoAlias] public NativeArray<SeaglideTuningDTO> Tuning;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's parallel-for safety remains enabled: Runtime BufferIDs and NoAlias guarantee non-overlap, and Execute(index) writes only AudioSignals[index].
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Publishing SignalBus packets directly from this job was rejected because SignalBus publication is a post-simulation owner-phase action. A managed audio callback was rejected because it would reintroduce heap traffic and Unity-object coupling.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is that audio rows are presentation-only, index-local, and consumed only after the combined solver handle is finalized. The job is scheduled after thrust output exists and never mutates physics truth.
        [WriteOnly, NoAlias] public NativeArray<SeaglideAudioSignalDTO> AudioSignals;
        public int ActiveRequestCount;

        public void Execute(int index)
        {
            if (!Requests.IsCreated || !ForcePackets.IsCreated || !AudioSignals.IsCreated || (uint)index >= (uint)Requests.Length || (uint)index >= (uint)ForcePackets.Length || (uint)index >= (uint)AudioSignals.Length)
                return;

            AudioSignals[index] = default;
            if ((uint)index >= (uint)math.max(0, ActiveRequestCount))
                return;

            SeaglideTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : SeaglideTuningDTO.Default();
            SeaglidePropulsionRequestDTO request = Requests[index];
            SeaglideForcePacketDTO packet = ForcePackets[index];
            if (packet.TargetEntityHash == 0u || !math.all(math.isfinite(packet.NetForce)))
                return;

            double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(request.CurrentAUP, request.PreviousAUP);
            float3 localDelta = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
            float distanceSq = math.lengthsq(localDelta);
            float dt = math.max(0.0001f, math.select(tuning.SimulationTickDelta, request.DeltaTime, request.DeltaTime > 0f));
            float speed = math.select(0f, SeaglideSimdMath.LengthFromSq(math.max(0f, distanceSq)) * math.rcp(dt), math.isfinite(distanceSq));
            float cavitation01 = math.saturate((packet.CurrentSpeed - tuning.CavitationSpeedStart) * math.rcp(math.max(0.1f, tuning.CavitationSpeedFull - tuning.CavitationSpeedStart)));

            SeaglideAudioSignalDTO signal = default;
            signal.CurrentAUP = request.CurrentAUP;
            signal.DopplerSpeedMetersPerSecond = speed * math.max(0f, tuning.AudioDopplerScale);
            signal.PitchScalar = math.lerp(0.92f, 1.22f, math.saturate(speed * 0.05f));
            signal.VolumeScalar = math.saturate(packet.Throttle01 * (0.4f + packet.BatteryLevel * 0.6f));
            signal.Cavitation01 = cavitation01;
            signal.SourceHash = request.RequestHash != 0u ? request.RequestHash : SeaglideHydrodynamicsConstants.SourceHash;
            signal.Flags = SeaglideHydrodynamicsConstants.FlagVisualOnly | SeaglideHydrodynamicsConstants.FlagRollbackExcluded;
            signal.TargetEntityHash = packet.TargetEntityHash;
            signal.FrameIndex = packet.FrameIndex;
            AudioSignals[index] = signal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ReduceSeaglideTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SeaglideForcePacketDTO> ForcePackets;
        [ReadOnly, NoAlias] public NativeArray<SeaglideStateDTO> States;
        [NoAlias] public NativeArray<SeaglideCounterDTO> Counters;
        [NoAlias] public NativeArray<SeaglideTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int ActiveRequestCount;
        public uint SimulationFrame;
        public float GlobalQualityWeight;
        public float ComputeMicros;
        public int MetabolismEnabled;

        public void Execute()
        {
            SeaglideCounterDTO counter = default;
            int activeCount = math.max(0, ActiveRequestCount);
            int count = ForcePackets.IsCreated ? math.min(activeCount, ForcePackets.Length) : 0;
            SeaglideForcePacketDTO lastValidPacket = default;
            for (int i = 0; i < count; i++)
            {
                SeaglideForcePacketDTO packet = ForcePackets[i];
                uint stateFlags = States.IsCreated && (uint)i < (uint)States.Length ? States[i].ActiveFlags : 0u;
                bool packetFinite = math.all(math.isfinite(packet.NetForce)) &
                                    math.all(math.isfinite(packet.ThrustForce)) &
                                    math.all(math.isfinite(packet.DragForce)) &
                                    math.all(math.isfinite(packet.FlowForce));
                bool nonFinite = (stateFlags & SeaglideHydrodynamicsConstants.FlagNonFinite) != 0u || !packetFinite;
                bool valid = packet.TargetEntityHash != 0u &&
                             (packet.Flags & SeaglideHydrodynamicsConstants.FlagForceQueued) != 0u &&
                             packetFinite;
                counter.EvaluatedRequests++;
                counter.NonFiniteCount += math.select(0, 1, nonFinite);
                if (!valid)
                    continue;

                counter.ForcePackets++;
                counter.TotalThrustForce += Magnitude(packet.ThrustForce);
                counter.TotalDragForce += Magnitude(packet.DragForce);
                counter.TotalFlowForce += Magnitude(packet.FlowForce);
                counter.MaxForceMagnitude = math.max(counter.MaxForceMagnitude, packet.ForceMagnitude);
                counter.LastTargetEntityHash = packet.TargetEntityHash;
                lastValidPacket = packet;
            }

            counter.MetabolismTicks = math.select(0, count, MetabolismEnabled != 0);
            counter.ComputeMicros = ComputeMicros;
            counter.GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            counter.Flags = math.select(0u, SeaglideHydrodynamicsConstants.FlagNonFinite, counter.NonFiniteCount > 0);

            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = counter;

            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            int writeIndex = math.clamp(cursor, 0, TelemetryRing.Length - 1);
            SeaglideTelemetryEntry entry = default;
            entry.FrameIndex = SimulationFrame;
            entry.EvaluatedRequests = counter.EvaluatedRequests;
            entry.ForcePackets = counter.ForcePackets;
            entry.NonFiniteCount = counter.NonFiniteCount;
            entry.TotalThrustForce = counter.TotalThrustForce;
            entry.TotalDragForce = counter.TotalDragForce;
            entry.TotalFlowForce = counter.TotalFlowForce;
            entry.MaxForceMagnitude = counter.MaxForceMagnitude;
            entry.ComputeMicros = ComputeMicros;
            entry.GlobalQualityWeight = counter.GlobalQualityWeight;
            entry.Flags = counter.Flags;
            entry.LastTargetEntityHash = counter.LastTargetEntityHash;
            entry.LastFlowForce = lastValidPacket.FlowForce;
            entry.LastBatteryLevel = lastValidPacket.BatteryLevel;
            TelemetryRing[writeIndex] = entry;
            TelemetryCursor[0] = (writeIndex + 1) % TelemetryRing.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Magnitude(float3 value)
        {
            float sq = math.lengthsq(value);
            return math.select(0f, SeaglideSimdMath.LengthFromSq(sq), math.isfinite(sq));
        }
    }
}
