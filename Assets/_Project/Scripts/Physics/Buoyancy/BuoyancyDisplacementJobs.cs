using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockBuoyantObjectsJob : IJobParallelFor
    {
        // SAFETY: each scheduled lane writes exactly States[index] after a length guard.
        // No other SHINOBU buoyancy job is scheduled against States until this seed job completes.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<BuoyancyStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyDebugForceDTO> DebugForces;
        public int ActiveMockCount;
        public double3 SurfaceAUP;
        public uint SimulationFrame;

        public unsafe void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)States.Length)
                return;

            bool active = index < math.max(0, ActiveMockCount);
            uint rawHash = (uint)(index + 1) * 0x9E3779B9u;
            uint hash = math.select(1u, rawHash, rawHash != 0u);
            float lane = (index & 31) - 15.5f;
            float row = ((index >> 5) & 31) - 15.5f;
            float depth = 0.15f + ((index * 37) & 127) * 0.085f;
            float mass = 0.5f + ((index * 13) & 63) * 0.18f;
            float volume = 0.0025f + ((index * 19) & 63) * 0.00085f;
            float lateralDrift = TriangleSigned((index * 0.173f) + SimulationFrame * 0.01f);
            double3 safeSurfaceAup = math.select(double3.zero, SurfaceAUP, math.isfinite(SurfaceAUP));

            BuoyancyStateDTO state = default;
            state.CurrentAUP = math.select(double3.zero, safeSurfaceAup + new double3(lane * 1.75f, -depth, row * 1.75f), active);
            state.Velocity = math.select(float3.zero, new float3(lateralDrift * 0.22f, -0.015f + (index & 7) * 0.004f, -lateralDrift * 0.18f), active);
            state.VolumeCubicMeters = math.select(0f, volume, active);
            state.MassKg = math.select(0f, mass, active);
            state.EntityHashID = math.select(0u, hash, active);
            state.Flags = math.select(0u, BuoyancyDisplacementConstants.FlagActive | BuoyancyDisplacementConstants.FlagEmergencyMock, active);

            BuoyancyStateDTO* statesPtr = (BuoyancyStateDTO*)States.GetUnsafePtr();
            ref BuoyancyStateDTO stateRef = ref UnsafeUtility.AsRef<BuoyancyStateDTO>(statesPtr + index);
            stateRef = state;
            if (DebugForces.IsCreated && (uint)index < (uint)DebugForces.Length)
            {
                BuoyancyDebugForceDTO debug = default;
                debug.CurrentAUP = state.CurrentAUP;
                debug.EntityHashID = state.EntityHashID;
                debug.StateIndex = index;
                debug.FrameIndex = SimulationFrame;
                debug.Flags = state.Flags;
                debug.Velocity = state.Velocity;
                DebugForces[index] = debug;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InitializeBuoyancyColdBuffersJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<BuoyancyFlowSampleDTO> FlowSamples;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyTelemetryEntry> TelemetryRing;
        [WriteOnly, NoAlias] public NativeArray<int> TelemetryCursor;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyMaterialVolumeDTO> MaterialVolumes;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyDebugForceDTO> DebugForces;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyCounterDTO> Counters;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyBodyBindingDTO> BodyBindings;

        public void Execute()
        {
            ClearFlowSamples();
            ClearTelemetryRing();
            ClearTelemetryCursor();
            ClearMaterialVolumes();
            ClearDebugForces();
            ClearCounters();
            ClearBodyBindings();
        }

        private void ClearFlowSamples()
        {
            if (!FlowSamples.IsCreated)
                return;

            for (int i = 0; i < FlowSamples.Length; i++)
                FlowSamples[i] = default;
        }

        private void ClearTelemetryRing()
        {
            if (!TelemetryRing.IsCreated)
                return;

            for (int i = 0; i < TelemetryRing.Length; i++)
                TelemetryRing[i] = default;
        }

        private void ClearTelemetryCursor()
        {
            if (!TelemetryCursor.IsCreated)
                return;

            for (int i = 0; i < TelemetryCursor.Length; i++)
                TelemetryCursor[i] = default;
        }

        private void ClearMaterialVolumes()
        {
            if (!MaterialVolumes.IsCreated)
                return;

            for (int i = 0; i < MaterialVolumes.Length; i++)
                MaterialVolumes[i] = default;
        }

        private void ClearDebugForces()
        {
            if (!DebugForces.IsCreated)
                return;

            for (int i = 0; i < DebugForces.Length; i++)
                DebugForces[i] = default;
        }

        private void ClearCounters()
        {
            if (!Counters.IsCreated)
                return;

            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = default;
        }

        private void ClearBodyBindings()
        {
            if (!BodyBindings.IsCreated)
                return;

            for (int i = 0; i < BodyBindings.Length; i++)
                BodyBindings[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateBuoyancyJob : IJobParallelFor
    {
        // SAFETY: workIndex maps to one state row through index = workIndex * max(1, stride) + offset.
        // With stride >= 1 and fixed offset, two lanes cannot target the same state row.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<BuoyancyStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<BuoyancyFlowSampleDTO> FlowSamples;
        [ReadOnly, NoAlias] public NativeArray<BuoyancyTuningDTO> Tuning;
        // SAFETY: debug writes use the same injective state row mapping as States.
        // DebugForces is not read by any later job until this evaluator handle completes.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<BuoyancyDebugForceDTO> DebugForces;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyForcePacketDTO> ForcePackets;
        public int ForcePacketWriteEnabled;
        public int ActiveStateCount;
        public int EvaluationStride;
        public int EvaluationOffset;
        public uint SimulationFrame;
        public float SimulationTickDelta;
        public float GlobalQualityWeight;

        public unsafe void Execute(int workIndex)
        {
            if (!States.IsCreated || (uint)workIndex >= (uint)States.Length)
                return;

            BuoyancyTuningDTO tuning = ResolveTuning();
            int authoredActiveCount = math.select(tuning.ActiveStateCount, ActiveStateCount, ActiveStateCount > 0);
            int activeCount = math.clamp(authoredActiveCount, 0, States.Length);
            int stride = math.max(1, EvaluationStride);
            int offset = math.clamp(EvaluationOffset, 0, stride - 1);
            int stridedIndex = (workIndex * stride) + offset;
            int index = math.select(stridedIndex, workIndex, stride == 1);
            WriteForceCandidate(workIndex, default);
            if ((uint)index >= (uint)activeCount || (uint)index >= (uint)States.Length)
                return;

            BuoyancyStateDTO* statesPtr = (BuoyancyStateDTO*)States.GetUnsafePtr();
            ref BuoyancyStateDTO stateRef = ref UnsafeUtility.AsRef<BuoyancyStateDTO>(statesPtr + index);
            BuoyancyStateDTO state = stateRef;
            const uint transientStateFlags = BuoyancyDisplacementConstants.FlagEvaluated |
                                             BuoyancyDisplacementConstants.FlagForceQueued |
                                             BuoyancyDisplacementConstants.FlagSurfaceSnapped |
                                             BuoyancyDisplacementConstants.FlagNonFinite;
            const float maxSafeMassKg = 100000000f;
            const float maxSafeVolumeCubicMeters = 1000000f;
            state.Flags &= ~transientStateFlags;
            bool inputFinite = IsFinite(state.CurrentAUP) &
                               math.all(math.isfinite(state.Velocity)) &
                               math.isfinite(state.MassKg) &
                               math.isfinite(state.VolumeCubicMeters);
            state.CurrentAUP = math.select(double3.zero, state.CurrentAUP, math.isfinite(state.CurrentAUP));
            state.Velocity = SanitizeFinite(state.Velocity, float3.zero);
            state.MassKg = math.min(maxSafeMassKg, math.max(0f, SanitizeFinite(state.MassKg, 0f)));
            state.VolumeCubicMeters = math.min(maxSafeVolumeCubicMeters, math.max(0f, SanitizeFinite(state.VolumeCubicMeters, 0f)));
            state.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagNonFinite, !inputFinite);
            BuoyancyDebugForceDTO debug = default;
            debug.CurrentAUP = state.CurrentAUP;
            debug.EntityHashID = state.EntityHashID;
            debug.StateIndex = index;
            debug.FrameIndex = SimulationFrame;
            debug.Velocity = state.Velocity;

            bool hasBody = (state.EntityHashID != 0u) &
                           (state.MassKg > BuoyancyDisplacementConstants.Epsilon) &
                           (state.VolumeCubicMeters > BuoyancyDisplacementConstants.Epsilon);
            bool wasSleeping = (state.Flags & BuoyancyDisplacementConstants.FlagSleeping) != 0u;
            bool simulateBody = inputFinite & hasBody & !wasSleeping;
            float simulateWeight = math.select(0f, 1f, simulateBody);

            float schedulerQuality = Sanitize01(GlobalQualityWeight);
            float tuningQuality = Sanitize01(tuning.GlobalQualityWeight);
            float quality = math.min(schedulerQuality, tuningQuality);
            float qualityCurve = Smooth01(quality);
            float exactSpeedBlend = math.step(0.3f, quality) * Smooth01(math.saturate((quality - 0.3f) * 1.4285715f));
            float quadraticDragBlend = math.step(0.25f, quality) * Smooth01(math.saturate((quality - 0.25f) * 1.3333334f));
            double3 oceanSurfaceAup = math.select(double3.zero, tuning.OceanSurfaceAUP, math.isfinite(tuning.OceanSurfaceAUP));
            double3 sectorAup = math.select(double3.zero, tuning.SectorAUP, math.isfinite(tuning.SectorAUP));
            float gravity = math.max(BuoyancyDisplacementConstants.Epsilon, SanitizeFinite(
                tuning.GravityMetersPerSecondSq,
                BuoyancyDisplacementConstants.DefaultGravityMetersPerSecondSq));
            float waterDensity = math.max(BuoyancyDisplacementConstants.Epsilon, SanitizeFinite(
                tuning.WaterDensityKgPerM3,
                BuoyancyDisplacementConstants.DefaultWaterDensityKgPerM3));
            float linearDragCoefficient = math.max(0f, SanitizeFinite(
                tuning.LinearDragCoefficient,
                BuoyancyDisplacementConstants.DefaultLinearDragCoefficient));
            float quadraticDragCoefficient = math.max(0f, SanitizeFinite(
                tuning.QuadraticDragCoefficient,
                BuoyancyDisplacementConstants.DefaultQuadraticDragCoefficient));
            float surfaceDampening = math.saturate(SanitizeFinite(
                tuning.SurfaceDampening,
                BuoyancyDisplacementConstants.DefaultSurfaceDampening));
            float sleepSpeedSq = math.max(0.000001f, SanitizeFinite(tuning.SleepSpeedSq, 0.0009f));
            float sleepForceThreshold = math.max(0.0001f, SanitizeFinite(tuning.SleepForceThreshold, 0.45f));
            float densityDepthCoefficient = math.max(0f, SanitizeFinite(tuning.DensityDepthCoefficient, 0.000045f));
            float seafloorAupY = SanitizeFinite(tuning.SeafloorAUPY, -10000f);
            float flowForceCoefficient = math.max(0f, SanitizeFinite(
                tuning.FlowForceCoefficient,
                BuoyancyDisplacementConstants.DefaultFlowForceCoefficient));
            float surfaceSnapDepthMeters = math.max(0.01f, SanitizeFinite(tuning.SurfaceSnapDepthMeters, 0.18f));
            float minFluidDensity = math.max(1f, SanitizeFinite(tuning.MinFluidDensityKgPerM3, 900f));
            float maxFluidDensity = math.max(minFluidDensity + 1f, SanitizeFinite(tuning.MaxFluidDensityKgPerM3, 1160f));
            float selectedTickDelta = math.select(tuning.SimulationTickDelta, SimulationTickDelta, SimulationTickDelta > 0f);
            float dt = math.clamp(
                SanitizeFinite(selectedTickDelta, 0.02f),
                0.0001f,
                0.2f);

            double3 relativeToSurface = oceanSurfaceAup - state.CurrentAUP;
            float depthMeters = math.max(0f, SanitizeFinite((float)relativeToSurface.y, 0f));
            float objectHeight = EstimateObjectHeightMeters(state.VolumeCubicMeters);
            float halfHeight = objectHeight * 0.5f;
            float submerged01 = math.saturate((depthMeters + halfHeight) * math.rcp(math.max(objectHeight, BuoyancyDisplacementConstants.Epsilon)));

            float snapDepth = math.lerp(0.5f, surfaceSnapDepthMeters, qualityCurve);
            bool nearSurface = simulateBody & (depthMeters <= snapDepth) & (submerged01 > 0.1f) & (submerged01 < 0.95f);
            float nearSurfaceMask = math.select(0f, 1f, nearSurface);
            float damping = math.saturate(surfaceDampening * math.saturate(dt * 50f)) * nearSurfaceMask;
            float dampedVelocityY = SanitizeFinite(state.Velocity.y * (1f - damping), 0f);
            float snapMask = nearSurfaceMask * math.step(math.abs(dampedVelocityY), 0.025f);
            state.CurrentAUP.y = math.select(state.CurrentAUP.y, oceanSurfaceAup.y, snapMask > 0f);
            state.Velocity.y = math.select(dampedVelocityY, 0f, snapMask > 0f);
            state.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagSurfaceSnapped, snapMask > 0f);
            relativeToSurface = oceanSurfaceAup - state.CurrentAUP;
            depthMeters = math.max(0f, SanitizeFinite((float)relativeToSurface.y, 0f));
            submerged01 = math.saturate((depthMeters + halfHeight) * math.rcp(math.max(objectHeight, BuoyancyDisplacementConstants.Epsilon)));

            float depth01 = math.saturate(depthMeters * 0.01f);
            float denseLayer = Smooth01(depth01);
            float densityDepthScale = 1f + densityDepthCoefficient * depthMeters;
            float density = math.clamp(
                waterDensity * densityDepthScale * math.lerp(1f, 1.025f, denseLayer * qualityCurve),
                minFluidDensity,
                maxFluidDensity);

            float displacedVolume = math.max(0f, state.VolumeCubicMeters) * submerged01;
            float3 buoyancyForce = new float3(0f, density * displacedVolume * gravity, 0f) * simulateWeight;
            float3 gravityForce = new float3(0f, -math.max(BuoyancyDisplacementConstants.Epsilon, state.MassKg) * gravity, 0f) * simulateWeight;
            double3 relativeToSector = state.CurrentAUP - sectorAup;
            float3 localAup = SanitizeFinite(new float3((float)relativeToSector.x, (float)relativeToSector.y, (float)relativeToSector.z), float3.zero);
            float3 flowVelocity = ResolveFlowVelocity(state.CurrentAUP, localAup, index, SimulationFrame, qualityCurve) * simulateWeight;
            float3 relativeVelocity = SanitizeFinite((state.Velocity * simulateWeight) - flowVelocity, float3.zero);
            float3 linearDrag = -relativeVelocity * linearDragCoefficient;
            float relativeSpeedSq = math.lengthsq(relativeVelocity);
            float relativeSpeed = FastSpeed(relativeVelocity, relativeSpeedSq, exactSpeedBlend);
            float3 quadraticDrag = -relativeVelocity * relativeSpeed * quadraticDragCoefficient;
            float3 dragForce = math.lerp(linearDrag, quadraticDrag, quadraticDragBlend);

            dragForce *= submerged01;
            float3 flowForce = flowVelocity * flowForceCoefficient * math.max(0.01f, state.MassKg) * submerged01;
            float gravityPacketWeight = math.select(0f, 1f, (tuning.Flags & BuoyancyDisplacementConstants.FlagOwnsGravityInPacket) != 0u);
            float3 netForce = buoyancyForce + (gravityForce * gravityPacketWeight) + dragForce + flowForce;

            float forceMagnitudeSq = math.lengthsq(netForce);
            float speedSq = math.lengthsq(state.Velocity);
            bool seafloorContact = state.CurrentAUP.y <= (double)seafloorAupY + (double)halfHeight;
            bool stableSurface = (state.Flags & BuoyancyDisplacementConstants.FlagSurfaceSnapped) != 0u;
            bool slowEnoughToSleep = speedSq <= sleepSpeedSq;
            bool surfaceBalanced = stableSurface & (forceMagnitudeSq <= sleepForceThreshold * sleepForceThreshold);
            bool sleepNow = simulateBody & slowEnoughToSleep & (seafloorContact | surfaceBalanced);
            state.Velocity = math.select(state.Velocity, float3.zero, sleepNow);
            state.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagSleeping, sleepNow);
            state.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagSeafloorSleeping, sleepNow & seafloorContact);

            bool mathFinite = IsFinite(state.CurrentAUP) &
                              math.all(math.isfinite(state.Velocity)) &
                              math.all(math.isfinite(buoyancyForce)) &
                              math.all(math.isfinite(gravityForce)) &
                              math.all(math.isfinite(dragForce)) &
                              math.all(math.isfinite(flowForce)) &
                              math.all(math.isfinite(netForce));
            bool forceOutputValid = simulateBody & mathFinite & !sleepNow;
            uint flags = state.Flags | math.select(0u, BuoyancyDisplacementConstants.FlagEvaluated, simulateBody);
            flags |= math.select(0u, BuoyancyDisplacementConstants.FlagNonFinite, !inputFinite | (simulateBody & !mathFinite));

            state.Flags = flags;
            stateRef = state;
            debug.Flags = flags;
            debug.BuoyantForce = buoyancyForce;
            debug.GravityForce = gravityForce;
            debug.DragForce = dragForce;
            debug.FlowForce = flowForce;
            debug.NetForce = math.select(float3.zero, netForce, forceOutputValid);
            debug.SubmergedFraction = submerged01 * simulateWeight;
            debug.DepthMeters = depthMeters * simulateWeight;
            debug.SleepScore = (speedSq + forceMagnitudeSq) * simulateWeight;

            bool queueCandidate = (ForcePacketWriteEnabled != 0) & forceOutputValid & (forceMagnitudeSq > 0.00000001f);
            BuoyancyForcePacketDTO packet = default;
            packet.CurrentAUP = math.select(double3.zero, state.CurrentAUP, queueCandidate);
            packet.NetForce = math.select(float3.zero, netForce, queueCandidate);
            packet.BuoyantForce = math.select(float3.zero, buoyancyForce, queueCandidate);
            packet.GravityForce = math.select(float3.zero, gravityForce, queueCandidate);
            packet.DragForce = math.select(float3.zero, dragForce, queueCandidate);
            packet.FlowForce = math.select(float3.zero, flowForce, queueCandidate);
            packet.SubmergedFraction = math.select(0f, submerged01, queueCandidate);
            packet.DepthMeters = math.select(0f, depthMeters, queueCandidate);
            packet.FluidDensityKgPerM3 = math.select(0f, density, queueCandidate);
            packet.EntityHashID = math.select(0u, state.EntityHashID, queueCandidate);
            packet.Flags = math.select(0u, flags | BuoyancyDisplacementConstants.FlagForceQueued, queueCandidate);
            packet.StateIndex = math.select(0, index, queueCandidate);
            packet.FrameIndex = math.select(0u, SimulationFrame, queueCandidate);
            packet.DebugVelocity = math.select(float3.zero, state.Velocity, queueCandidate);
            bool wroteCandidate = WriteForceCandidate(workIndex, packet);
            debug.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagForceQueued, queueCandidate & wroteCandidate);

            WriteDebug(index, debug);
        }

        private BuoyancyTuningDTO ResolveTuning()
        {
            if (Tuning.IsCreated && Tuning.Length > 0)
                return Tuning[0];
            return BuoyancyTuningDTO.Default();
        }

        private void WriteDebug(int index, BuoyancyDebugForceDTO debug)
        {
            if (DebugForces.IsCreated && (uint)index < (uint)DebugForces.Length)
                DebugForces[index] = debug;
        }

        private bool WriteForceCandidate(int workIndex, BuoyancyForcePacketDTO packet)
        {
            if (!ForcePackets.IsCreated || (uint)workIndex >= (uint)ForcePackets.Length)
                return false;

            ForcePackets[workIndex] = packet;
            return true;
        }

        private float3 ResolveFlowVelocity(double3 objectAup, float3 localAup, int index, uint frame, float qualityCurve)
        {
            float3 sampledFlow = float3.zero;
            float sampleMask = 0f;
            if (FlowSamples.IsCreated && FlowSamples.Length > 0)
            {
                int slot = (int)(((uint)index * 2654435761u) % (uint)FlowSamples.Length);
                BuoyancyFlowSampleDTO sample = FlowSamples[slot];
                double3 sampleAup = math.select(objectAup, sample.SampleAUP, math.isfinite(sample.SampleAUP));
                double3 delta = objectAup - sampleAup;
                float3 localDelta = SanitizeFinite(new float3((float)delta.x, (float)delta.y, (float)delta.z), float3.zero);
                float radius = math.min(10000f, math.max(0.01f, SanitizeFinite(sample.RadiusMeters, 0.01f)));
                float radiusSq = radius * radius;
                bool activeSample = (sample.Flags & BuoyancyDisplacementConstants.FlagActive) != 0u;
                bool insideSample = math.lengthsq(localDelta) <= radiusSq;
                bool finiteSample = IsFinite(sample.SampleAUP) & math.all(math.isfinite(sample.FlowVelocity));
                sampleMask = math.select(0f, 1f, activeSample & insideSample & finiteSample);
                sampledFlow = SanitizeFinite(sample.FlowVelocity, float3.zero);
            }

            float phase = TriangleSigned((localAup.x * 0.013f + localAup.z * 0.017f) + frame * 0.00390625f);
            float cross = TriangleSigned((localAup.x * 0.007f - localAup.z * 0.011f) + frame * 0.001953125f);
            float amplitude = math.lerp(0.08f, 0.55f, qualityCurve);
            float3 analyticFlow = new float3(phase * amplitude, 0f, cross * amplitude * 0.65f);
            return math.select(analyticFlow, sampledFlow, sampleMask > 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimateObjectHeightMeters(float volume)
        {
            float finiteVolume = math.select(BuoyancyDisplacementConstants.Epsilon, volume, math.isfinite(volume));
            float safeVolume = math.max(finiteVolume, BuoyancyDisplacementConstants.Epsilon);
            float y = math.max(0.05f, safeVolume * math.rsqrt(safeVolume));
            float yy = math.max(y * y, BuoyancyDisplacementConstants.Epsilon);
            y = (2f * y + safeVolume * math.rcp(yy)) * 0.33333334f;
            yy = math.max(y * y, BuoyancyDisplacementConstants.Epsilon);
            y = (2f * y + safeVolume * math.rcp(yy)) * 0.33333334f;
            return math.clamp(y, 0.05f, 25f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastSpeed(float3 velocity, float speedSq, float qualityCurve)
        {
            float3 a = math.abs(SanitizeFinite(velocity, float3.zero));
            float cheap = math.cmax(a);
            float safeSpeedSq = math.max(0f, SanitizeFinite(speedSq, 0f));
            float exact = safeSpeedSq * math.rsqrt(math.max(safeSpeedSq, BuoyancyDisplacementConstants.Epsilon));
            return math.lerp(cheap, exact, Sanitize01(qualityCurve));
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
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CompactBuoyancyForcePacketsJob : IJob
    {
        [NoAlias] public NativeArray<BuoyancyForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<BuoyancyCounterDTO> Counters;
        public int CandidateCount;

        public void Execute()
        {
            int packetCapacity = 0;
            if (ForcePackets.IsCreated)
                packetCapacity = ForcePackets.Length;

            int count = math.min(math.max(0, CandidateCount), packetCapacity);
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                BuoyancyForcePacketDTO packet = ForcePackets[i];
                bool valid = IsValidPacket(packet);
                BuoyancyForcePacketDTO preserved = ForcePackets[write];
                BuoyancyForcePacketDTO sanitized = SanitizePacket(packet);
                ForcePackets[write] = SelectPacket(preserved, sanitized, valid);
                write += math.select(0, 1, valid);
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                BuoyancyCounterDTO counter = Counters[0];
                counter.ForcePackets = write;
                uint overflow = math.select(0u, BuoyancyDisplacementConstants.FlagForcePacketOverflow, CandidateCount > packetCapacity);
                counter.Flags = (counter.Flags & ~BuoyancyDisplacementConstants.FlagForcePacketOverflow) | overflow;
                Counters[0] = counter;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidPacket(in BuoyancyForcePacketDTO packet)
        {
            return packet.EntityHashID != 0u &
                   math.all(math.isfinite(packet.NetForce)) &
                   math.all(math.isfinite(packet.CurrentAUP));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BuoyancyForcePacketDTO SanitizePacket(BuoyancyForcePacketDTO packet)
        {
            packet.NetForce = math.select(float3.zero, packet.NetForce, math.isfinite(packet.NetForce));
            packet.BuoyantForce = math.select(float3.zero, packet.BuoyantForce, math.isfinite(packet.BuoyantForce));
            packet.GravityForce = math.select(float3.zero, packet.GravityForce, math.isfinite(packet.GravityForce));
            packet.DragForce = math.select(float3.zero, packet.DragForce, math.isfinite(packet.DragForce));
            packet.FlowForce = math.select(float3.zero, packet.FlowForce, math.isfinite(packet.FlowForce));
            packet.DebugVelocity = math.select(float3.zero, packet.DebugVelocity, math.isfinite(packet.DebugVelocity));
            packet.SubmergedFraction = math.saturate(math.select(0f, packet.SubmergedFraction, math.isfinite(packet.SubmergedFraction)));
            packet.DepthMeters = math.max(0f, math.select(0f, packet.DepthMeters, math.isfinite(packet.DepthMeters)));
            packet.FluidDensityKgPerM3 = math.max(0f, math.select(0f, packet.FluidDensityKgPerM3, math.isfinite(packet.FluidDensityKgPerM3)));
            packet.Flags |= BuoyancyDisplacementConstants.FlagForceQueued;
            packet._pad0 = 0u;
            return packet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BuoyancyForcePacketDTO SelectPacket(
            BuoyancyForcePacketDTO preserved,
            BuoyancyForcePacketDTO sanitized,
            bool useSanitized)
        {
            preserved.CurrentAUP = math.select(preserved.CurrentAUP, sanitized.CurrentAUP, useSanitized);
            preserved.NetForce = math.select(preserved.NetForce, sanitized.NetForce, useSanitized);
            preserved.BuoyantForce = math.select(preserved.BuoyantForce, sanitized.BuoyantForce, useSanitized);
            preserved.GravityForce = math.select(preserved.GravityForce, sanitized.GravityForce, useSanitized);
            preserved.DragForce = math.select(preserved.DragForce, sanitized.DragForce, useSanitized);
            preserved.FlowForce = math.select(preserved.FlowForce, sanitized.FlowForce, useSanitized);
            preserved.SubmergedFraction = math.select(preserved.SubmergedFraction, sanitized.SubmergedFraction, useSanitized);
            preserved.DepthMeters = math.select(preserved.DepthMeters, sanitized.DepthMeters, useSanitized);
            preserved.FluidDensityKgPerM3 = math.select(preserved.FluidDensityKgPerM3, sanitized.FluidDensityKgPerM3, useSanitized);
            preserved.EntityHashID = math.select(preserved.EntityHashID, sanitized.EntityHashID, useSanitized);
            preserved.Flags = math.select(preserved.Flags, sanitized.Flags, useSanitized);
            preserved.StateIndex = math.select(preserved.StateIndex, sanitized.StateIndex, useSanitized);
            preserved.FrameIndex = math.select(preserved.FrameIndex, sanitized.FrameIndex, useSanitized);
            preserved.DebugVelocity = math.select(preserved.DebugVelocity, sanitized.DebugVelocity, useSanitized);
            preserved._pad0 = math.select(preserved._pad0, sanitized._pad0, useSanitized);
            return preserved;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ReduceBuoyancyTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BuoyancyDebugForceDTO> DebugForces;
        [NoAlias] public NativeArray<BuoyancyCounterDTO> Counters;
        [WriteOnly, NoAlias] public NativeArray<BuoyancyTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int ActiveStateCount;
        public uint SimulationFrame;
        public float GlobalQualityWeight;
        public float ComputeMicros;

        public void Execute()
        {
            BuoyancyCounterDTO counter = default;
            if (Counters.IsCreated && Counters.Length > 0)
            {
                BuoyancyCounterDTO previous = Counters[0];
                counter.ForcePackets = math.max(0, previous.ForcePackets);
                counter.Flags = previous.Flags & BuoyancyDisplacementConstants.FlagForcePacketOverflow;
            }

            int count = 0;
            if (DebugForces.IsCreated)
                count = math.min(math.max(0, ActiveStateCount), DebugForces.Length);
            float3 lastNetForce = float3.zero;
            int hasLastNetForce = 0;
            for (int i = 0; i < count; i++)
            {
                BuoyancyDebugForceDTO debug = DebugForces[i];
                uint flags = debug.Flags;
                int aliveMask = math.select(0, 1, debug.EntityHashID != 0u);
                int frameOnlyMask = math.select(0, 1, debug.FrameIndex == SimulationFrame);
                int frameMask = aliveMask * frameOnlyMask;
                int sleepingMask = aliveMask * math.select(0, 1, (flags & BuoyancyDisplacementConstants.FlagSleeping) != 0u);
                int evaluatedMask = frameMask * math.select(0, 1, (flags & BuoyancyDisplacementConstants.FlagEvaluated) != 0u);
                int nonFiniteMask = frameOnlyMask * math.select(0, 1, (flags & BuoyancyDisplacementConstants.FlagNonFinite) != 0u);
                float activeFrameWeight = frameMask;

                counter.SleepingObjects += sleepingMask;
                counter.EvaluatedObjects += evaluatedMask;
                counter.NonFiniteCount += nonFiniteMask;
                counter.Flags |= math.select(0u, BuoyancyDisplacementConstants.FlagNonFinite, nonFiniteMask != 0);
                counter.TotalBuoyantForce += LengthSafe(debug.BuoyantForce) * activeFrameWeight;
                counter.TotalDragForce += LengthSafe(debug.DragForce) * activeFrameWeight;
                counter.MaxDepthMeters = math.max(counter.MaxDepthMeters, math.max(0f, debug.DepthMeters) * activeFrameWeight);
                counter.LastEntityHashID = math.select(counter.LastEntityHashID, debug.EntityHashID, frameMask != 0);
                lastNetForce = math.select(lastNetForce, SanitizeFinite(debug.NetForce), frameMask != 0);
                hasLastNetForce = math.select(hasLastNetForce, 1, frameMask != 0);
            }

            counter.ComputeMicros = math.max(0f, ComputeMicros);
            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = counter;

            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            int cursor = math.max(0, TelemetryCursor[0]);
            int slot = cursor % TelemetryRing.Length;
            BuoyancyTelemetryEntry entry = default;
            entry.FrameIndex = SimulationFrame;
            entry.EvaluatedObjects = counter.EvaluatedObjects;
            entry.SleepingObjects = counter.SleepingObjects;
            entry.ForcePackets = counter.ForcePackets;
            entry.TotalBuoyantForce = counter.TotalBuoyantForce;
            entry.TotalDragForce = counter.TotalDragForce;
            entry.ComputeMicros = counter.ComputeMicros;
            entry.GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            entry.Flags = counter.Flags;
            entry.NonFiniteCount = counter.NonFiniteCount;
            entry.LastEntityHashID = counter.LastEntityHashID;
            entry.MaxDepthMeters = counter.MaxDepthMeters;
            entry.LastNetForce = math.select(float3.zero, lastNetForce, hasLastNetForce != 0);
            TelemetryRing[slot] = entry;
            TelemetryCursor[0] = cursor + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LengthSafe(float3 value)
        {
            float3 safe = SanitizeFinite(value);
            float lenSq = math.max(math.lengthsq(safe), 0f);
            float length = lenSq * math.rsqrt(math.max(lenSq, BuoyancyDisplacementConstants.Epsilon));
            return math.select(0f, length, lenSq > BuoyancyDisplacementConstants.Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }
    }
}
