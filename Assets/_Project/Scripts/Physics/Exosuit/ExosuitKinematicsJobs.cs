using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Exosuit
{
    internal static class ExosuitMathGuards
    {
        public const float AuthoritativeQualityWeight = 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFloat(float value, float fallback)
        {
            return math.select(value, fallback, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 SanitizeFloat2(float2 value, float2 fallback)
        {
            float2 safeFallback = math.select(fallback, float2.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 SanitizeDouble3(double3 value, double3 fallback)
        {
            double3 safeFallback = math.select(fallback, double3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ExosuitTuningDTO SanitizeTuning(ExosuitTuningDTO tuning)
        {
            tuning.BaseMass = math.max(1.0f, SanitizeNonNegative(tuning.BaseMass));
            float currentMass = SanitizeNonNegative(tuning.CurrentMass);
            tuning.CurrentMass = math.select(tuning.BaseMass, math.max(1.0f, currentMass), currentMass > 0.0f);
            tuning.Radius = math.clamp(SanitizeNonNegative(tuning.Radius), 0.25f, 5.0f);
            tuning.SdfEpsilonMeters = math.clamp(SanitizeNonNegative(tuning.SdfEpsilonMeters), 0.005f, 0.25f);
            tuning.GlobalQualityWeight = AuthoritativeQualityWeight;
            tuning.MaxSubsteps = math.clamp(tuning.MaxSubsteps, 2u, 8u);
            return tuning;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockExosuitInputsJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitFrameInputDTO> Input;

        public float2 MoveAxis;
        public float VerticalAxis;
        public float DesiredYawRadians;
        public float GlobalQualityWeight;
        public uint ActionMask;
        public uint Frame;
        public uint ProceduralWeightMilli;
        public uint StableEntityHash;
        public uint SectorHash;

        public void Execute()
        {
            if (!Input.IsCreated || Input.Length <= 0)
                return;

            float proceduralWeight = math.saturate(ProceduralWeightMilli * 0.001f);
            float2 safeMove = SanitizeFloat2(MoveAxis, float2.zero);
            float safeVertical = math.clamp(SanitizeFloat(VerticalAxis, 0.0f), -1.0f, 1.0f);
            float safeYaw = WrapRadians(SanitizeFloat(DesiredYawRadians, 0.0f));
            if (proceduralWeight > 0.0001f)
            {
                uint seed = BuildDeterministicSeed(Frame, GlobalQualityWeight, ActionMask, StableEntityHash, SectorHash);
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
                float2 drift = new float2(random.NextFloat(-1.0f, 1.0f), random.NextFloat(-1.0f, 1.0f)) * 0.18f;
                safeMove = math.clamp(safeMove + drift * proceduralWeight, new float2(-1.0f), new float2(1.0f));
                safeVertical = math.clamp(safeVertical + random.NextFloat(-1.0f, 1.0f) * 0.12f * proceduralWeight, -1.0f, 1.0f);
                safeYaw = WrapRadians(safeYaw + random.NextFloat(-1.0f, 1.0f) * 0.08f * proceduralWeight);
            }

            ExosuitFrameInputDTO input = default;
            input.MoveAxis = safeMove;
            input.VerticalAxis = safeVertical;
            input.DesiredYawRadians = safeYaw;
            input.ActionMask = ActionMask;
            input.Frame = Frame;
            input.GlobalQualityWeight = math.saturate(SanitizeNonNegative(GlobalQualityWeight));
            Input[0] = input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildDeterministicSeed(uint frame, float qualityWeight, uint actionMask, uint stableEntityHash, uint sectorHash)
        {
            const uint SourceHash = 0x53484E34u; // SHN4
            uint seed = 2166136261u;
            seed = (seed ^ SourceHash) * 16777619u;
            seed = (seed ^ math.select(SourceHash, stableEntityHash, stableEntityHash != 0u)) * 16777619u;
            seed = (seed ^ math.select(0x48534653u, sectorHash, sectorHash != 0u)) * 16777619u;
            seed = (seed ^ math.select(1u, frame, frame != 0u)) * 16777619u;
            seed = (seed ^ math.asuint(math.saturate(SanitizeNonNegative(qualityWeight)))) * 16777619u;
            seed = (seed ^ actionMask) * 16777619u;
            return math.select(1u, seed, seed != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeFloat2(float2 value, float2 fallback)
        {
            float2 safeFallback = math.select(fallback, float2.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFloat(float value, float fallback)
        {
            return math.select(value, fallback, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapRadians(float value)
        {
            const float TwoPi = 6.2831853071795864769f;
            const float InvTwoPi = 0.1591549430918953358f;
            if (!math.isfinite(value))
                return 0.0f;

            return value - math.round(value * InvTwoPi) * TwoPi;
        }
    }

    /// <summary>
    /// Deterministic 6D kinematic exosuit solver over DataVault-owned buffers.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ExosuitKinematicIntegrationJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [NoAlias] public NativeArray<ExosuitFrameInputDTO> Input;
        [NoAlias] public NativeArray<ExosuitTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<MockTerrainSDF> Terrain;
        [ReadOnly, NoAlias] public NativeArray<byte> VoxelSdfTexture3D;
        [ReadOnly, NoAlias] public NativeArray<MockFlowField> Flow;
        [ReadOnly, NoAlias] public NativeArray<MockCrushDepthSignal> CrushDepth;
        [NoAlias] public NativeArray<ExosuitSolverOutput> Output;
        [NoAlias] public NativeArray<ExoScreenDTO> Screen;
        [NoAlias] public NativeArray<ExosuitTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<float> FootstepAccumulator;
        [NoAlias] public NativeArray<MechHapticSignalDTO> HapticSignals;
        [NoAlias] public NativeArray<SiltExplosionSignal> SiltSignals;
        [NoAlias] public NativeArray<ExosuitAcousticEchoTap> AcousticTaps;

        public float DeltaTime;
        public int3 VoxelSdfDimensions;
        public float3 VoxelSdfOrigin;
        public float3 VoxelSdfCellSize;
        public float VoxelSdfRangeMeters;
        public uint Frame;
        public uint ProceduralWeightMilli;
        public uint StableEntityHash;
        public uint SectorHash;

        private const float MinDt = 0.0001f;
        private const float MaxDt = 0.05f;
        private const float MinMass = 1.0f;
        private const float ReducedProbeCutoff = 0.5f;
        private const float SecondaryProbeStart = 0.35f;
        private const float SecondaryProbeFull = 0.85f;
        private const float GravityMetersPerSecondSq = 9.80665f;
        private const float InvEncodedByteMax = 0.0039215686274509803f;
        private const uint SourceHash = 0x53484E34u; // SHN4
        private const uint StompHash = 0x45584F53u; // EXOS

        /// <inheritdoc />
        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0 ||
                !Input.IsCreated || Input.Length <= 0 ||
                !Tuning.IsCreated || Tuning.Length <= 0 ||
                !Terrain.IsCreated || Terrain.Length <= 0 ||
                !Output.IsCreated || Output.Length <= 0)
            {
                return;
            }

            float dt = math.clamp(SanitizeNonNegative(DeltaTime), MinDt, MaxDt);
            ExosuitStateDTO state = State[0];
            ExosuitTuningDTO tuning = SanitizeTuning(Tuning[0]);
            ExosuitFrameInputDTO input = Input[0];
            if (ProceduralWeightMilli > 0u)
            {
                ApplyProceduralMockInput(ref input, ProceduralWeightMilli, StableEntityHash, SectorHash);
                Input[0] = input;
            }
            MockTerrainSDF terrain = Terrain[0];
            ExosuitSolverOutput previousOutput = Output[0];
            MockFlowField flow = Flow.IsCreated && Flow.Length > 0 ? Flow[0] : default;
            MockCrushDepthSignal crush = CrushDepth.IsCreated && CrushDepth.Length > 0 ? CrushDepth[0] : default;

            state.AUP_Position = SanitizeDouble3(state.AUP_Position, terrain.CameraAup);
            double3 cameraAup = SanitizeDouble3(terrain.CameraAup, state.AUP_Position);
            float3 localPosition = SanitizeFloat3(ToLocal(state.AUP_Position, cameraAup), float3.zero);
            float3 velocity = SanitizeFloat3(state.Velocity, float3.zero);
            float3 angularVelocity = SanitizeFloat3(state.AngularVelocity, float3.zero);
            uint previousMask = state.Flags;
            uint mask = ExosuitStateFlags.Active;
            float quality = ExosuitMathGuards.AuthoritativeQualityWeight;
            bool voxelSdfAvailable = IsVoxelSdfPayloadValid();
            if (voxelSdfAvailable)
                mask |= ExosuitStateFlags.VoxelSdfSampled;
            float2 moveAxis = SanitizeFloat2(input.MoveAxis, float2.zero);
            float verticalAxis = math.clamp(SanitizeFloat(input.VerticalAxis, 0.0f), -1.0f, 1.0f);
            float desiredYaw = WrapRadians(SanitizeFloat(input.DesiredYawRadians, 0.0f));
            bool jumpRequested = (input.ActionMask & ExosuitInputActions.Jump) != 0u;
            verticalAxis = math.max(verticalAxis, math.select(0.0f, 1.0f, jumpRequested));

            float inputMagnitude = math.saturate(LengthFromSq(math.lengthsq(moveAxis)) + math.abs(verticalAxis));
            if (jumpRequested)
                inputMagnitude = math.saturate(inputMagnitude + 1.0f);

            float pressureTarget = inputMagnitude;
            float latencyScale = math.lerp(1.35f, 0.75f, Smooth01(0.0f, 1.0f, quality));
            float pressureStep = dt * math.rcp(math.max(0.05f, tuning.HydraulicLatencySeconds * latencyScale));
            float previousHydraulicPressure = Screen.IsCreated && Screen.Length > 0
                ? SanitizeNonNegative(Screen[0].HydraulicPressure)
                : 0.0f;
            float pressure = MoveTowards(previousHydraulicPressure, pressureTarget, pressureStep);
            float heat = math.saturate(SanitizeNonNegative(state.ThrusterHeat));
            float externalPressure = math.saturate(SanitizeNonNegative(crush.ExternalPressure01));
            pressure *= math.saturate(1.0f - externalPressure * dt * 0.35f);

            if ((previousMask & ExosuitStateFlags.PurgeLatched) != 0u)
                mask |= ExosuitStateFlags.PurgeLatched;

            bool purgeRequested = (input.ActionMask & ExosuitInputActions.Purge) != 0u;
            bool purgeLatched = (mask & ExosuitStateFlags.PurgeLatched) != 0u;
            bool emitSilt = false;
            if (purgeRequested && !purgeLatched)
            {
                tuning.CurrentMass = math.max(MinMass, tuning.CurrentMass * 0.5f);
                velocity.y = math.abs(velocity.y) + tuning.PurgeImpulse;
                heat = math.min(1.0f, heat + 0.06f);
                pressure = math.min(1.0f, pressure + 0.35f);
                mask |= ExosuitStateFlags.PurgeLatched;
                emitSilt = true;
            }

            float2 yawVector = DeterministicSinCos(desiredYaw);
            float3 yawForward = new float3(yawVector.x, 0.0f, yawVector.y);
            float3 yawRight = new float3(yawForward.z, 0.0f, -yawForward.x);
            float3 desiredDirection = yawRight * moveAxis.x + yawForward * moveAxis.y + new float3(0.0f, verticalAxis, 0.0f);
            desiredDirection = NormalizeWithFallback(desiredDirection, float3.zero);
            float3 rawDesiredVelocity = desiredDirection * tuning.MaxSpeedMetersPerSecond * pressure;
            float3 previousDesiredVelocity = SanitizeFloat3(previousOutput.DesiredVelocity, float3.zero);
            float actuatorRateScale = math.lerp(0.62f, 1.35f, Smooth01(0.0f, 1.0f, quality));
            float actuatorMaxDelta = tuning.MaxSpeedMetersPerSecond * actuatorRateScale * dt * math.rcp(math.max(0.05f, tuning.HydraulicLatencySeconds));
            float3 desiredVelocity = MoveTowardsVector(previousDesiredVelocity, rawDesiredVelocity, actuatorMaxDelta);
            float desiredSpeedSq = math.max(0.0f, math.lengthsq(desiredVelocity));
            float desiredSpeed = desiredSpeedSq * math.rsqrt(math.max(desiredSpeedSq, 0.0001f));
            float actuatorPressure = math.saturate(desiredSpeed * math.rcp(math.max(0.1f, tuning.MaxSpeedMetersPerSecond)));
            float3 actuatorDirection = NormalizeWithFallback(desiredVelocity, desiredDirection);

            bool wasOverheated = (previousMask & ExosuitStateFlags.Overheated) != 0u;
            bool overheated = wasOverheated && heat > 0.58f;
            bool thrusterRequested = actuatorPressure > 0.0001f || jumpRequested;
            if (heat >= 0.995f)
                overheated = true;
            if (overheated)
                actuatorPressure = 0.0f;

            if (thrusterRequested && !overheated)
                heat += dt * (0.12f + actuatorPressure * math.lerp(0.18f, 0.34f, Smooth01(0.0f, 1.0f, pressure)));
            else
                heat -= dt * math.lerp(0.07f, 0.16f, Smooth01(0.0f, 1.0f, quality));

            heat = math.saturate(heat);
            if (heat >= 0.995f)
            {
                heat = 1.0f;
                overheated = true;
            }

            if (overheated)
                mask |= ExosuitStateFlags.Overheated;
            else if (thrusterRequested)
                mask |= ExosuitStateFlags.ThrusterActive;

            float3 thrustAcceleration = actuatorDirection * tuning.ThrusterForce * actuatorPressure * math.rcp(math.max(MinMass, tuning.CurrentMass));
            velocity += thrustAcceleration * dt;
            if ((previousMask & ExosuitStateFlags.Clamped) == 0u)
                velocity.y -= GravityMetersPerSecondSq * tuning.GravityMultiplier * dt;
            velocity = ApplyAnalyticalDrag(velocity, tuning.Drag, dt, quality);
            angularVelocity = ApplyAngularHydraulicDamping(angularVelocity, pressure, dt, quality);

            if ((previousMask & ExosuitStateFlags.Clamped) == 0u)
            {
                float flowScale = math.saturate(SanitizeNonNegative(flow.Intensity01));
                velocity += SanitizeFloat3(flow.FlowVelocity, float3.zero) * flowScale * dt;
            }

            float speedSq = math.lengthsq(velocity);
            float maxSpeed = math.max(0.1f, tuning.MaxSpeedMetersPerSecond);
            if ((mask & ExosuitStateFlags.PurgeLatched) != 0u)
                maxSpeed = math.max(maxSpeed, tuning.PurgeImpulse * math.lerp(0.85f, 1.25f, Smooth01(0.0f, 1.0f, quality)));
            if (speedSq > maxSpeed * maxSpeed)
                velocity *= maxSpeed * math.rsqrt(math.max(speedSq, 0.0001f));

            localPosition += velocity * dt;
            float sdfSkinMeters = math.lerp(tuning.SdfEpsilonMeters * 1.5f, tuning.SdfEpsilonMeters * 0.55f, Smooth01(0.0f, 1.0f, quality));
            float contactRadius = tuning.Radius + sdfSkinMeters;

            float reducedProbeBudget = 1.0f - Smooth01(0.05f, ReducedProbeCutoff, quality);
            mask |= math.select(0u, ExosuitStateFlags.ReducedProbeBudget, reducedProbeBudget > 0.0001f);

            float secondaryWeight = Smooth01(SecondaryProbeStart, SecondaryProbeFull, quality);
            mask |= ((uint)math.step(0.0001f, secondaryWeight)) * ExosuitStateFlags.SecondaryProbeBlend;

            float pushMagnitude = 0.0f;
            float lostVelocityMagnitude = 0.0f;
            float3 pushNormal = new float3(0.0f, 1.0f, 0.0f);
            float pendingSecondaryPush = 0.0f;
            float3 pendingSecondaryNormal = pushNormal;

            int maxSubsteps = math.clamp((int)tuning.MaxSubsteps, 2, 8);
            int iterations = math.clamp((int)math.lerp(2.0f, maxSubsteps, quality), 2, maxSubsteps);
            float ccdWeight = Smooth01(0.72f, 1.0f, quality);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float substepT = (iteration + 1.0f) * math.rcp(iterations);
                float3 midPosition = localPosition - velocity * (dt * 0.5f * substepT);
                SdfSample midSdf = SampleExosuitSdf(midPosition, terrain, quality);
                float midPenetration = math.max(0.0f, contactRadius - midSdf.Distance);
                float sweepPush = midPenetration * ccdWeight * math.rcp(iterations);
                float3 midNormal = NormalizeWithFallback(midSdf.Normal, pushNormal);
                localPosition += midNormal * sweepPush;
                pushNormal = math.select(pushNormal, midNormal, sweepPush > pushMagnitude);
                pushMagnitude = math.max(pushMagnitude, sweepPush);

                SdfSample sdf = SampleExosuitSdf(localPosition, terrain, quality);
                pushNormal = math.select(sdf.Normal, pushNormal, pushMagnitude > 0.0f);
                pendingSecondaryPush = 0.0f;
                pendingSecondaryNormal = pushNormal;

                ApplySecondaryProbe(
                    localPosition,
                    terrain,
                    contactRadius,
                    secondaryWeight,
                    quality,
                    ref pendingSecondaryNormal,
                    ref pendingSecondaryPush);

                float penetration = contactRadius - sdf.Distance;
                float pendingPush = math.max(penetration, pendingSecondaryPush);
                if (pendingPush <= 0.0f)
                    continue;

                pushNormal = math.select(sdf.Normal, pendingSecondaryNormal, pendingSecondaryPush > penetration);
                pushNormal = NormalizeWithFallback(pushNormal, sdf.Normal);
                localPosition += pushNormal * pendingPush;
                velocity = ApplyContactVelocityResponse(velocity, pushNormal, pendingPush, quality, out float contactLostSpeed);
                angularVelocity *= math.lerp(0.45f, 0.76f, Smooth01(0.0f, 1.0f, quality));
                lostVelocityMagnitude = math.max(lostVelocityMagnitude, contactLostSpeed);

                pushMagnitude = math.max(pushMagnitude, pendingPush);
                if (pushNormal.y > 0.5f)
                    mask |= ExosuitStateFlags.Grounded;
            }

            bool grabRequested = (input.ActionMask & ExosuitInputActions.Grab) != 0u;
            SdfSample postPushSdf = SampleExosuitSdf(localPosition, terrain, quality);
            float residualPenetration = contactRadius - postPushSdf.Distance;
            if (residualPenetration > 0.0001f)
            {
                float3 residualNormal = NormalizeWithFallback(postPushSdf.Normal, pushNormal);
                localPosition += residualNormal * residualPenetration;
                velocity = ApplyContactVelocityResponse(velocity, residualNormal, residualPenetration, quality, out float residualLostSpeed);
                lostVelocityMagnitude = math.max(lostVelocityMagnitude, residualLostSpeed);
                pushMagnitude = math.max(pushMagnitude, residualPenetration);
                pushNormal = residualNormal;
                if (residualNormal.y > 0.5f)
                    mask |= ExosuitStateFlags.Grounded;

                postPushSdf = SampleExosuitSdf(localPosition, terrain, quality);
            }

            float residualSecondaryWeight = secondaryWeight * math.select(0.0f, 1.0f, pushMagnitude > 0.0f);
            float residualSecondaryPush = 0.0f;
            float3 residualSecondaryNormal = pushNormal;
            ApplySecondaryProbe(
                localPosition,
                terrain,
                contactRadius,
                residualSecondaryWeight,
                quality,
                ref residualSecondaryNormal,
                ref residualSecondaryPush);

            if (residualSecondaryPush > 0.0001f)
            {
                residualSecondaryNormal = NormalizeWithFallback(residualSecondaryNormal, pushNormal);
                localPosition += residualSecondaryNormal * residualSecondaryPush;
                velocity = ApplyContactVelocityResponse(velocity, residualSecondaryNormal, residualSecondaryPush, quality, out float residualSecondaryLostSpeed);
                lostVelocityMagnitude = math.max(lostVelocityMagnitude, residualSecondaryLostSpeed);
                pushMagnitude = math.max(pushMagnitude, residualSecondaryPush);
                pushNormal = residualSecondaryNormal;
                if (residualSecondaryNormal.y > 0.5f)
                    mask |= ExosuitStateFlags.Grounded;

                postPushSdf = SampleExosuitSdf(localPosition, terrain, quality);
            }

            bool floorContact = postPushSdf.Normal.y > 0.5f && postPushSdf.Distance <= contactRadius + 0.05f;
            if (floorContact)
                mask |= ExosuitStateFlags.Grounded;

            bool wasClamped = (previousMask & ExosuitStateFlags.Clamped) != 0u;
            float clampAcquireRange = math.max(tuning.ClampRange, contactRadius);
            float clampReleaseRange = clampAcquireRange + math.lerp(0.18f, 0.06f, Smooth01(0.0f, 1.0f, quality));
            float clampWallness = 1.0f - Smooth01(0.55f, 0.88f, math.abs(postPushSdf.Normal.y));
            float clampAcquireDistance = math.lerp(-contactRadius, clampAcquireRange, clampWallness);
            float clampReleaseDistance = math.lerp(-contactRadius, clampReleaseRange, clampWallness);
            float clampAcquireWeight = (1.0f - math.step(clampAcquireDistance, postPushSdf.Distance)) * clampWallness;
            float clampReleaseWeight = (1.0f - math.step(clampReleaseDistance, postPushSdf.Distance)) * clampWallness;
            bool clampEligible = math.max(clampAcquireWeight, math.select(0.0f, clampReleaseWeight, wasClamped)) > 0.0001f;
            if (grabRequested && clampEligible)
            {
                float3 clampAnchorNormal = NormalizeWithFallback(postPushSdf.Normal, pushNormal);
                float clampCorrection = contactRadius - postPushSdf.Distance;
                if (math.abs(clampCorrection) > 0.0001f)
                {
                    localPosition += clampAnchorNormal * clampCorrection;
                    postPushSdf = SampleExosuitSdf(localPosition, terrain, quality);
                    float clampResidual = contactRadius - postPushSdf.Distance;
                    if (clampResidual > 0.0001f)
                    {
                        float3 clampResidualNormal = NormalizeWithFallback(postPushSdf.Normal, clampAnchorNormal);
                        localPosition += clampResidualNormal * clampResidual;
                        pushMagnitude = math.max(pushMagnitude, clampResidual);
                        if (clampResidualNormal.y > 0.5f)
                            mask |= ExosuitStateFlags.Grounded;
                        postPushSdf = SampleExosuitSdf(localPosition, terrain, quality);
                    }
                }

                velocity = float3.zero;
                angularVelocity = float3.zero;
                desiredVelocity = float3.zero;
                pushNormal = clampAnchorNormal;
                pushMagnitude = math.max(pushMagnitude, math.abs(clampCorrection));
                mask |= ExosuitStateFlags.Clamped;
            }

            bool badMath = !math.all(math.isfinite(localPosition)) ||
                           !math.all(math.isfinite(velocity)) ||
                           !math.all(math.isfinite(angularVelocity)) ||
                           !math.isfinite(heat) ||
                           !math.isfinite(pressure) ||
                           !math.all(math.isfinite(pushNormal)) ||
                           !math.isfinite(postPushSdf.Distance);
            if (badMath)
            {
                localPosition = float3.zero;
                velocity = float3.zero;
                angularVelocity = float3.zero;
                pressure = 0.0f;
                heat = 0.0f;
                desiredVelocity = float3.zero;
                pushMagnitude = 0.0f;
                lostVelocityMagnitude = 0.0f;
                pushNormal = new float3(0.0f, 1.0f, 0.0f);
                floorContact = false;
                mask |= ExosuitStateFlags.NaNDetected;
            }

            float3 snappedLocalPosition = SnapMillimeter(localPosition);
            float3 snappedVelocity = SnapMillimeter(velocity);
            float3 snappedAngularVelocity = SnapMillimeter(angularVelocity);
            if (pushMagnitude > 0.0001f)
                mask |= ExosuitStateFlags.SdfContact;

            state.AUP_Position = cameraAup + new double3(snappedLocalPosition);
            state.Velocity = snappedVelocity;
            state.AngularVelocity = snappedAngularVelocity;
            state.ThrusterHeat = heat;
            state.Flags = mask | (tuning.Flags & ExosuitStateFlags.EmergencyMockData);
            State[0] = state;
            Tuning[0] = tuning;

            uint outputFlags = 0u;
            if (pushMagnitude > 0.0001f)
                outputFlags |= ExosuitSolverOutput.FlagCollision;
            if (lostVelocityMagnitude > 0.05f || pushMagnitude > 0.02f)
                outputFlags |= ExosuitSolverOutput.FlagHaptic;
            if (emitSilt)
                outputFlags |= ExosuitSolverOutput.FlagSilt;
            if (badMath)
                outputFlags |= ExosuitSolverOutput.FlagFault;

            if (floorContact)
                outputFlags |= AccumulateFootstep(state.AUP_Position, LengthFromSq(math.lengthsq(velocity)) * dt, tuning.FootstepStrideMeters);

            uint stateHash = ComputeStateHash(snappedLocalPosition, snappedVelocity, pressure, state.ThrusterHeat, state.Flags);
            ExosuitSolverOutput solverOutput = default;
            solverOutput.LocalPosition = snappedLocalPosition;
            solverOutput.DesiredVelocity = SnapMillimeter(desiredVelocity);
            solverOutput.PushNormal = pushNormal;
            solverOutput.PushOutMagnitude = pushMagnitude;
            solverOutput.LostVelocityMagnitude = lostVelocityMagnitude;
            solverOutput.Speed = LengthFromSq(math.lengthsq(velocity));
            solverOutput.Flags = outputFlags;
            solverOutput.Frame = Frame;
            solverOutput.StateHash = stateHash;
            Output[0] = solverOutput;

            WriteOptionalOutputs(state, crush, outputFlags, pushMagnitude, lostVelocityMagnitude, tuning.CurrentMass, quality, emitSilt);
            WriteScreen(state, crush, pressure);
            WriteTelemetry(state, pushMagnitude, stateHash, math.select(0u, 1u, badMath));
        }

        private uint AccumulateFootstep(double3 aup, float distanceMeters, float strideMeters)
        {
            if (!FootstepAccumulator.IsCreated || FootstepAccumulator.Length <= 0)
                return 0u;

            float stride = math.max(0.25f, SanitizeNonNegative(strideMeters));
            float accumulated = SanitizeNonNegative(FootstepAccumulator[0]) + SanitizeNonNegative(distanceMeters);
            if (accumulated < stride)
            {
                FootstepAccumulator[0] = accumulated;
                return 0u;
            }

            FootstepAccumulator[0] = accumulated - stride;
            if (AcousticTaps.IsCreated && AcousticTaps.Length > 0)
            {
                ExosuitAcousticEchoTap tap = default;
                tap.AUP = aup;
                tap.Intensity01 = 1.0f;
                tap.EchoHash = StompHash;
                AcousticTaps[0] = tap;
            }

            return ExosuitSolverOutput.FlagAcousticTap;
        }

        private void WriteOptionalOutputs(
            in ExosuitStateDTO state,
            in MockCrushDepthSignal crush,
            uint outputFlags,
            float pushMagnitude,
            float lostVelocityMagnitude,
            float currentMass,
            float quality,
            bool emitSilt)
        {
            if (HapticSignals.IsCreated && HapticSignals.Length > 0)
            {
                MechHapticSignalDTO haptic = default;
                if ((outputFlags & ExosuitSolverOutput.FlagHaptic) != 0u)
                {
                    float massScale = math.max(MinMass, SanitizeNonNegative(currentMass)) * 0.001f;
                    float impact = (lostVelocityMagnitude * massScale) + (pushMagnitude * massScale * 0.35f);
                    float amplitude = math.saturate(LengthFromSq(impact) * math.lerp(0.11f, 0.17f, Smooth01(0.0f, 1.0f, quality)));
                    haptic.Amplitude = amplitude;
                    haptic.Frequency = math.lerp(18.0f, 42.0f, amplitude);
                    haptic.Duration = math.lerp(0.08f, 0.30f, amplitude);
                    haptic.MotorMask = 3u;
                }

                HapticSignals[0] = haptic;
            }

            if (SiltSignals.IsCreated && SiltSignals.Length > 0)
            {
                SiltExplosionSignal silt = default;
                if (emitSilt)
                {
                    silt.AUP = state.AUP_Position;
                    silt.Intensity01 = 1.0f;
                    silt.SourceHash = SourceHash;
                }

                SiltSignals[0] = silt;
            }

            if (AcousticTaps.IsCreated && AcousticTaps.Length > 0 &&
                (outputFlags & ExosuitSolverOutput.FlagAcousticTap) == 0u)
            {
                AcousticTaps[0] = default;
            }

            if (crush.ExternalPressure01 > 0.85f &&
                HapticSignals.IsCreated &&
                HapticSignals.Length > 0 &&
                (outputFlags & ExosuitSolverOutput.FlagHaptic) == 0u)
            {
                float crushAmp = math.saturate(crush.ExternalPressure01);
                MechHapticSignalDTO haptic = default;
                haptic.Amplitude = crushAmp * 0.4f;
                haptic.Frequency = 18.0f;
                haptic.Duration = 0.15f;
                haptic.MotorMask = 3u;
                HapticSignals[0] = haptic;
            }
        }

        private void WriteScreen(in ExosuitStateDTO state, in MockCrushDepthSignal crush, float hydraulicPressure)
        {
            if (!Screen.IsCreated || Screen.Length <= 0)
                return;

            ExoScreenDTO screen = default;
            screen.HydraulicPressure = math.saturate(hydraulicPressure);
            screen.DepthMeters = SanitizeNonNegative(crush.DepthMeters);
            screen.Flags = state.Flags;
            screen.Frame = Frame;
            Screen[0] = screen;
        }

        private void WriteTelemetry(in ExosuitStateDTO state, float pushMagnitude, uint stateHash, uint errorFlags)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
            {
                return;
            }

            int cursor = TelemetryCursor[0];
            if ((uint)cursor >= (uint)TelemetryRing.Length)
                cursor = 0;

            ExosuitTelemetryEntry entry = default;
            entry.AUP = state.AUP_Position;
            entry.Velocity = state.Velocity;
            entry.ThrusterHeat = state.ThrusterHeat;
            entry.HydraulicPressure = Screen.IsCreated && Screen.Length > 0
                ? math.saturate(Screen[0].HydraulicPressure)
                : 0.0f;
            entry.SdfPushOutMagnitude = pushMagnitude;
            entry.SolverComputeTimeMs = 0.0f;
            entry.Frame = Frame;
            entry.Flags = state.Flags | (errorFlags * ExosuitStateFlags.NaNDetected);
            entry.StateHash = stateHash;
            TelemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= TelemetryRing.Length)
                cursor = 0;
            TelemetryCursor[0] = cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyProceduralMockInput(ref ExosuitFrameInputDTO input, uint proceduralWeightMilli, uint stableEntityHash, uint sectorHash)
        {
            float proceduralWeight = math.saturate(proceduralWeightMilli * 0.001f);
            if (proceduralWeight <= 0.0001f)
                return;

            uint seed = BuildDeterministicSeed(input.Frame, input.GlobalQualityWeight, input.ActionMask, stableEntityHash, sectorHash);
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            float2 drift = new float2(random.NextFloat(-1.0f, 1.0f), random.NextFloat(-1.0f, 1.0f)) * 0.18f;
            input.MoveAxis = math.clamp(input.MoveAxis + drift * proceduralWeight, new float2(-1.0f), new float2(1.0f));
            input.VerticalAxis = math.clamp(input.VerticalAxis + random.NextFloat(-1.0f, 1.0f) * 0.12f * proceduralWeight, -1.0f, 1.0f);
            input.DesiredYawRadians = WrapRadians(input.DesiredYawRadians + random.NextFloat(-1.0f, 1.0f) * 0.08f * proceduralWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildDeterministicSeed(uint frame, float qualityWeight, uint actionMask, uint stableEntityHash, uint sectorHash)
        {
            uint seed = 2166136261u;
            seed = (seed ^ SourceHash) * 16777619u;
            seed = (seed ^ math.select(SourceHash, stableEntityHash, stableEntityHash != 0u)) * 16777619u;
            seed = (seed ^ math.select(0x48534653u, sectorHash, sectorHash != 0u)) * 16777619u;
            seed = (seed ^ math.select(1u, frame, frame != 0u)) * 16777619u;
            seed = (seed ^ math.asuint(math.saturate(SanitizeNonNegative(qualityWeight)))) * 16777619u;
            seed = (seed ^ actionMask) * 16777619u;
            return math.select(1u, seed, seed != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ExosuitTuningDTO SanitizeTuning(ExosuitTuningDTO tuning)
        {
            tuning.BaseMass = math.max(MinMass, SanitizeNonNegative(tuning.BaseMass));
            float currentMass = SanitizeNonNegative(tuning.CurrentMass);
            tuning.CurrentMass = math.select(tuning.BaseMass, math.max(MinMass, currentMass), currentMass > 0.0f);
            tuning.Drag = math.clamp(SanitizeNonNegative(tuning.Drag), 0.0f, 8.0f);
            tuning.ThrusterForce = math.max(0.0f, SanitizeNonNegative(tuning.ThrusterForce));
            tuning.Radius = math.clamp(SanitizeNonNegative(tuning.Radius), 0.25f, 5.0f);
            tuning.ClampRange = math.max(tuning.Radius, SanitizeNonNegative(tuning.ClampRange));
            tuning.HydraulicLatencySeconds = math.clamp(SanitizeNonNegative(tuning.HydraulicLatencySeconds), 0.05f, 3.0f);
            tuning.PurgeImpulse = math.clamp(SanitizeNonNegative(tuning.PurgeImpulse), 0.0f, 80.0f);
            tuning.GlobalQualityWeight = ExosuitMathGuards.AuthoritativeQualityWeight;
            tuning.FootstepStrideMeters = math.clamp(SanitizeNonNegative(tuning.FootstepStrideMeters), 0.25f, 12.0f);
            tuning.MaxSpeedMetersPerSecond = math.clamp(SanitizeNonNegative(tuning.MaxSpeedMetersPerSecond), 0.25f, 40.0f);
            tuning.CrushDepthMeters = math.max(1.0f, SanitizeNonNegative(tuning.CrushDepthMeters));
            tuning.SdfEpsilonMeters = math.clamp(SanitizeNonNegative(tuning.SdfEpsilonMeters), 0.005f, 0.25f);
            tuning.GravityMultiplier = math.clamp(SanitizeNonNegative(tuning.GravityMultiplier), 0.0f, 2.0f);
            tuning.MaxSubsteps = math.clamp(tuning.MaxSubsteps, 2u, 8u);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplySecondaryProbe(
            float3 center,
            in MockTerrainSDF terrain,
            float radius,
            float weight,
            float quality,
            ref float3 normal,
            ref float push)
        {
            float safeWeight = math.saturate(weight);
            float probeOffset = radius * 0.65f;
            float probeRadius = math.max(0.0f, radius - probeOffset) * safeWeight;
            float3 correction = float3.zero;
            float3 strongestNormal = normal;
            float strongestPush = 0.0f;
            SdfSample px = SampleExosuitSdf(center + new float3(probeOffset, 0.0f, 0.0f), terrain, quality);
            SdfSample nx = SampleExosuitSdf(center - new float3(probeOffset, 0.0f, 0.0f), terrain, quality);
            SdfSample py = SampleExosuitSdf(center + new float3(0.0f, probeOffset, 0.0f), terrain, quality);
            SdfSample ny = SampleExosuitSdf(center - new float3(0.0f, probeOffset, 0.0f), terrain, quality);
            SdfSample pz = SampleExosuitSdf(center + new float3(0.0f, 0.0f, probeOffset), terrain, quality);
            SdfSample nz = SampleExosuitSdf(center - new float3(0.0f, 0.0f, probeOffset), terrain, quality);

            AccumulateProbeCorrection(px, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(nx, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(py, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(ny, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(pz, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(nz, probeRadius, ref correction, ref strongestNormal, ref strongestPush);

            correction *= safeWeight;
            strongestPush *= safeWeight;
            if (strongestPush <= 0.0f)
                return;

            float correctionSq = math.lengthsq(correction);
            if (correctionSq > 0.000001f)
            {
                float correctionMagnitude = LengthFromSq(correctionSq);
                if (correctionMagnitude > strongestPush * 0.25f)
                {
                    normal = correction * math.rsqrt(math.max(correctionSq, 0.0001f));
                    float boundedPush = math.max(strongestPush, math.min(correctionMagnitude, strongestPush * 2.0f));
                    push = math.max(push, boundedPush);
                    return;
                }
            }

            normal = strongestNormal;
            push = math.max(push, strongestPush);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AccumulateProbeCorrection(
            in SdfSample sample,
            float probeRadius,
            ref float3 correction,
            ref float3 strongestNormal,
            ref float strongestPush)
        {
            float probePenetration = probeRadius - sample.Distance;
            if (!math.isfinite(probePenetration) || probePenetration <= 0.0f)
                return;

            probePenetration = math.min(probePenetration, math.max(0.001f, probeRadius * 2.0f));
            float3 safeNormal = NormalizeWithFallback(sample.Normal, strongestNormal);
            correction += safeNormal * probePenetration;
            if (probePenetration > strongestPush)
            {
                strongestPush = probePenetration;
                strongestNormal = safeNormal;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SdfSample SampleExosuitSdf(float3 localPosition, in MockTerrainSDF terrain, float quality)
        {
            SdfSample fallback = SampleCaveSdf(localPosition, terrain);
            return TrySampleVoxelSdf(localPosition, fallback.Normal, math.saturate(quality), out SdfSample voxel)
                ? voxel
                : fallback;
        }

        private bool TrySampleVoxelSdf(float3 localPosition, float3 fallbackNormal, float quality, out SdfSample sample)
        {
            sample = default;
            if (!IsVoxelSdfPayloadValid() ||
                !math.all(math.isfinite(localPosition)) ||
                !math.all(math.isfinite(VoxelSdfOrigin)) ||
                !math.all(math.isfinite(VoxelSdfCellSize)) ||
                !math.isfinite(VoxelSdfRangeMeters))
            {
                return false;
            }

            float3 safeCellSize = math.max(math.abs(VoxelSdfCellSize), new float3(0.0001f));
            float3 grid = (localPosition - VoxelSdfOrigin) * math.rcp(safeCellSize);
            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0.0f ||
                grid.y < 0.0f ||
                grid.z < 0.0f ||
                grid.x > VoxelSdfDimensions.x - 1.0f ||
                grid.y > VoxelSdfDimensions.y - 1.0f ||
                grid.z > VoxelSdfDimensions.z - 1.0f)
            {
                return false;
            }

            float trilinearWeight = Smooth01(0.22f, 0.78f, quality);
            float signedDistance = SampleVoxelSignedNearest(grid);
            if (trilinearWeight > 0.0001f)
            {
                float trilinear = SampleVoxelSignedTrilinear(grid);
                signedDistance = math.lerp(signedDistance, trilinear, trilinearWeight);
            }

            if (!math.isfinite(signedDistance))
                return false;

            float3 normal = ResolveCheapVoxelNormal(grid, fallbackNormal);
            float gradientWeight = Smooth01(0.34f, 1.0f, quality);
            if (gradientWeight > 0.0001f)
            {
                float3 gradient = ResolveVoxelSignedGradient(grid, safeCellSize);
                float3 gradientNormal = NormalizeWithFallback(-gradient, normal);
                normal = NormalizeWithFallback(math.lerp(normal, gradientNormal, gradientWeight), normal);
            }

            sample.Distance = -signedDistance;
            sample.Normal = NormalizeWithFallback(normal, fallbackNormal);
            return math.isfinite(sample.Distance) && math.all(math.isfinite(sample.Normal));
        }

        private bool IsVoxelSdfPayloadValid()
        {
            return VoxelSdfTexture3D.IsCreated &&
                   TryResolveSdfVoxelCount(VoxelSdfDimensions, out int voxelCount) &&
                   VoxelSdfTexture3D.Length >= voxelCount &&
                   math.all(math.isfinite(VoxelSdfOrigin)) &&
                   math.all(math.isfinite(VoxelSdfCellSize)) &&
                   VoxelSdfCellSize.x > 0.0001f &&
                   VoxelSdfCellSize.y > 0.0001f &&
                   VoxelSdfCellSize.z > 0.0001f &&
                   math.isfinite(VoxelSdfRangeMeters) &&
                   VoxelSdfRangeMeters > 0.0001f;
        }

        public static bool TryResolveSdfVoxelCount(int3 dimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (dimensions.x <= 1 || dimensions.y <= 1 || dimensions.z <= 1)
                return false;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleVoxelSignedNearest(float3 grid)
        {
            int3 p = new int3(
                (int)math.round(grid.x),
                (int)math.round(grid.y),
                (int)math.round(grid.z));
            return DecodeVoxelSigned(ClampVoxelIndex(p));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleVoxelSignedTrilinear(float3 grid)
        {
            float3 clamped = math.clamp(
                grid,
                float3.zero,
                new float3(VoxelSdfDimensions.x - 1.001f, VoxelSdfDimensions.y - 1.001f, VoxelSdfDimensions.z - 1.001f));
            float3 floorGrid = math.floor(clamped);
            int3 p0 = new int3((int)floorGrid.x, (int)floorGrid.y, (int)floorGrid.z);
            int3 maxIndex = new int3(VoxelSdfDimensions.x - 1, VoxelSdfDimensions.y - 1, VoxelSdfDimensions.z - 1);
            int3 p1 = math.min(p0 + new int3(1, 1, 1), maxIndex);
            float3 f = math.saturate(clamped - floorGrid);

            float c000 = DecodeVoxelSigned(SdfIndex(p0.x, p0.y, p0.z));
            float c100 = DecodeVoxelSigned(SdfIndex(p1.x, p0.y, p0.z));
            float c010 = DecodeVoxelSigned(SdfIndex(p0.x, p1.y, p0.z));
            float c110 = DecodeVoxelSigned(SdfIndex(p1.x, p1.y, p0.z));
            float c001 = DecodeVoxelSigned(SdfIndex(p0.x, p0.y, p1.z));
            float c101 = DecodeVoxelSigned(SdfIndex(p1.x, p0.y, p1.z));
            float c011 = DecodeVoxelSigned(SdfIndex(p0.x, p1.y, p1.z));
            float c111 = DecodeVoxelSigned(SdfIndex(p1.x, p1.y, p1.z));
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            return math.lerp(c0, c1, f.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ResolveCheapVoxelNormal(float3 grid, float3 fallbackNormal)
        {
            float3 center = (new float3(VoxelSdfDimensions.x, VoxelSdfDimensions.y, VoxelSdfDimensions.z) - 1.0f) * 0.5f;
            return NormalizeWithFallback(center - grid, fallbackNormal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ResolveVoxelSignedGradient(float3 grid, float3 cellSize)
        {
            int3 p = new int3(
                (int)math.round(grid.x),
                (int)math.round(grid.y),
                (int)math.round(grid.z));
            float dx = DecodeVoxelSigned(ClampVoxelIndex(p + new int3(1, 0, 0))) -
                       DecodeVoxelSigned(ClampVoxelIndex(p - new int3(1, 0, 0)));
            float dy = DecodeVoxelSigned(ClampVoxelIndex(p + new int3(0, 1, 0))) -
                       DecodeVoxelSigned(ClampVoxelIndex(p - new int3(0, 1, 0)));
            float dz = DecodeVoxelSigned(ClampVoxelIndex(p + new int3(0, 0, 1))) -
                       DecodeVoxelSigned(ClampVoxelIndex(p - new int3(0, 0, 1)));
            return new float3(
                dx * math.rcp(math.max(0.0001f, cellSize.x + cellSize.x)),
                dy * math.rcp(math.max(0.0001f, cellSize.y + cellSize.y)),
                dz * math.rcp(math.max(0.0001f, cellSize.z + cellSize.z)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ClampVoxelIndex(int3 p)
        {
            int x = math.clamp(p.x, 0, VoxelSdfDimensions.x - 1);
            int y = math.clamp(p.y, 0, VoxelSdfDimensions.y - 1);
            int z = math.clamp(p.z, 0, VoxelSdfDimensions.z - 1);
            return SdfIndex(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DecodeVoxelSigned(int index)
        {
            if ((uint)index >= (uint)VoxelSdfTexture3D.Length)
                return -math.max(0.0001f, VoxelSdfRangeMeters);

            return ((VoxelSdfTexture3D[index] * InvEncodedByteMax) * 2.0f - 1.0f) * math.max(0.0001f, VoxelSdfRangeMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SdfIndex(int x, int y, int z)
        {
            return x + VoxelSdfDimensions.x * (y + VoxelSdfDimensions.y * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SdfSample SampleCaveSdf(float3 localPosition, in MockTerrainSDF terrain)
        {
            float radius = math.max(1.0f, SanitizeNonNegative(terrain.CaveRadius));
            float rawFloorY = SanitizeFloat(terrain.FloorY, -2.0f);
            float rawCeilingY = SanitizeFloat(terrain.CeilingY, rawFloorY + 2.0f);
            float floorY = math.min(rawFloorY, rawCeilingY - 2.0f);
            float ceilingY = math.max(rawCeilingY, floorY + 2.0f);
            float cornerSoftness = math.clamp(SanitizeNonNegative(terrain.WallSoftnessMeters), 0.0f, 0.5f);
            float3 center = SanitizeFloat3(terrain.CaveCenterLocal, float3.zero);
            float2 radial = localPosition.xz - center.xz;
            float radialSq = math.max(0.0f, math.lengthsq(radial));
            float radialLength = LengthFromSq(radialSq);
            float wallDistance = radius - radialLength;
            float floorDistance = localPosition.y - floorY;
            float ceilingDistance = ceilingY - localPosition.y;

            float distance = wallDistance;
            float3 wallNormal = math.select(
                new float3(1.0f, 0.0f, 0.0f),
                new float3(-radial.x, 0.0f, -radial.y) * math.rsqrt(math.max(radialSq, 0.0001f)),
                radialLength > 0.0001f);
            float3 normal = wallNormal;

            if (floorDistance < distance)
            {
                distance = floorDistance;
                normal = new float3(0.0f, 1.0f, 0.0f);
            }

            if (ceilingDistance < distance)
            {
                distance = ceilingDistance;
                normal = new float3(0.0f, -1.0f, 0.0f);
            }

            if (cornerSoftness > 0.0001f)
            {
                float wallWeight = 1.0f - Smooth01(0.0f, cornerSoftness, wallDistance - distance);
                float floorWeight = 1.0f - Smooth01(0.0f, cornerSoftness, floorDistance - distance);
                float ceilingWeight = 1.0f - Smooth01(0.0f, cornerSoftness, ceilingDistance - distance);
                float3 blendedNormal =
                    wallNormal * wallWeight +
                    new float3(0.0f, 1.0f, 0.0f) * floorWeight +
                    new float3(0.0f, -1.0f, 0.0f) * ceilingWeight;
                normal = NormalizeWithFallback(blendedNormal, normal);
            }

            SdfSample sample;
            sample.Distance = distance;
            sample.Normal = normal;
            return sample;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToLocal(double3 aup, double3 origin)
        {
            double3 delta = aup - origin;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;
            return current + math.sign(delta) * maxDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 MoveTowardsVector(float3 current, float3 target, float maxDelta)
        {
            float3 delta = target - current;
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            float distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq) || distanceSq <= 0.000001f)
                return target;

            float safeMaxDelta = SanitizeNonNegative(maxDelta);
            float safeMaxDeltaSq = safeMaxDelta * safeMaxDelta;
            if (distanceSq <= safeMaxDeltaSq)
                return target;

            return current + delta * (safeMaxDelta * math.rsqrt(math.max(distanceSq, 0.0001f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapRadians(float value)
        {
            const float TwoPi = 6.2831853071795864769f;
            const float InvTwoPi = 0.1591549430918953358f;
            if (!math.isfinite(value))
                return 0.0f;

            return value - math.round(value * InvTwoPi) * TwoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyAnalyticalDrag(float3 velocity, float drag, float dt, float quality)
        {
            float qualityDamping = math.lerp(1.2f, 0.85f, Smooth01(0.0f, 1.0f, quality));
            float speedSq = math.max(0.0f, math.lengthsq(velocity));
            float speed = speedSq * math.rsqrt(math.max(speedSq, 0.0001f));
            float denominator = 1.0f + math.max(0.0f, drag) * qualityDamping * speed * math.max(MinDt, dt);
            return velocity * math.rcp(math.max(0.0001f, denominator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyAngularHydraulicDamping(float3 angularVelocity, float pressure, float dt, float quality)
        {
            float load = math.saturate(SanitizeNonNegative(pressure));
            float qualityKeep = math.lerp(0.82f, 0.93f, Smooth01(0.0f, 1.0f, quality));
            float damping = math.lerp(5.0f, 1.5f, load) * math.max(MinDt, dt);
            return angularVelocity * math.rcp(1.0f + damping) * qualityKeep;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 DeterministicSinCos(float radians)
        {
            float sin = SinPolynomialDeterministic(radians);
            float cos = SinPolynomialDeterministic(radians + 1.57079632679f);
            float lenSq = math.max(0.0001f, (sin * sin) + (cos * cos));
            float invLen = math.rsqrt(math.max(lenSq, 0.0001f));
            return new float2(sin * invLen, cos * invLen);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SinPolynomialDeterministic(float radians)
        {
            const float Pi = 3.14159265359f;
            const float TwoPi = 6.28318530718f;
            const float HalfPi = 1.57079632679f;

            float wrapped = radians - (TwoPi * math.floor((radians + Pi) / TwoPi));
            float absWrapped = math.abs(wrapped);
            float reflected = math.sign(wrapped) * (Pi - absWrapped);
            float x = math.select(wrapped, reflected, absWrapped > HalfPi);
            float x2 = x * x;
            float x4 = x2 * x2;
            return x * (1f - (x2 * 0.16666666667f) + (x4 * 0.00833333333f) - (x4 * x2 * 0.00019841269f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyContactVelocityResponse(float3 velocity, float3 normal, float pushMagnitude, float quality, out float lostSpeed)
        {
            float3 safeNormal = NormalizeWithFallback(normal, new float3(0.0f, 1.0f, 0.0f));
            float3 before = velocity;
            float inwardVelocity = math.dot(velocity, -safeNormal);
            if (inwardVelocity > 0.0f)
                velocity += safeNormal * inwardVelocity;

            float normalVelocity = math.dot(velocity, safeNormal);
            float3 tangentVelocity = velocity - safeNormal * normalVelocity;
            float contactLoad = math.saturate(SanitizeNonNegative(pushMagnitude) * 3.0f + math.max(0.0f, inwardVelocity) * 0.2f);
            float tangentKeep = math.lerp(1.0f, math.lerp(0.78f, 0.92f, Smooth01(0.0f, 1.0f, quality)), contactLoad);
            velocity = safeNormal * math.max(0.0f, normalVelocity) + tangentVelocity * tangentKeep;
            lostSpeed = LengthFromSq(math.lengthsq(before - velocity));
            return velocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LengthFromSq(float lengthSq)
        {
            float safeLengthSq = math.max(0.0f, lengthSq);
            return safeLengthSq * math.rsqrt(math.max(safeLengthSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(0.0001f, edge1 - edge0)));
            return t * t * (3.0f - 2.0f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeWithFallback(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 SanitizeDouble3(double3 value, double3 fallback)
        {
            double3 safeFallback = math.select(fallback, double3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeFloat2(float2 value, float2 fallback)
        {
            float2 safeFallback = math.select(fallback, float2.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFloat(float value, float fallback)
        {
            return math.select(value, fallback, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                math.round(value.x * 1000.0f) * 0.001f,
                math.round(value.y * 1000.0f) * 0.001f,
                math.round(value.z * 1000.0f) * 0.001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeStateHash(float3 position, float3 velocity, float pressure, float heat, uint mask)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(position.x));
            hash = Mix(hash, math.asuint(position.y));
            hash = Mix(hash, math.asuint(position.z));
            hash = Mix(hash, math.asuint(velocity.x));
            hash = Mix(hash, math.asuint(velocity.y));
            hash = Mix(hash, math.asuint(velocity.z));
            hash = Mix(hash, math.asuint(pressure));
            hash = Mix(hash, math.asuint(heat));
            hash = Mix(hash, mask);
            return math.select(1u, hash, hash != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private struct SdfSample
        {
            public float Distance;
            public float3 Normal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ExosuitSdfCollisionJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [ReadOnly, NoAlias] public NativeArray<ExosuitTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<MockTerrainSDF> Terrain;
        [NoAlias] public NativeArray<ExosuitSolverOutput> Output;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0 ||
                !Tuning.IsCreated || Tuning.Length <= 0 ||
                !Terrain.IsCreated || Terrain.Length <= 0)
            {
                return;
            }

            ExosuitStateDTO state = State[0];
            ExosuitTuningDTO tuning = ExosuitMathGuards.SanitizeTuning(Tuning[0]);
            MockTerrainSDF terrain = Terrain[0];
            double3 cameraAup = ExosuitMathGuards.SanitizeDouble3(terrain.CameraAup, state.AUP_Position);
            state.AUP_Position = ExosuitMathGuards.SanitizeDouble3(state.AUP_Position, cameraAup);
            state.Velocity = ExosuitMathGuards.SanitizeFloat3(state.Velocity, float3.zero);
            state.AngularVelocity = ExosuitMathGuards.SanitizeFloat3(state.AngularVelocity, float3.zero);
            float3 local = ExosuitMathGuards.SanitizeFloat3(new float3(
                (float)(state.AUP_Position.x - cameraAup.x),
                (float)(state.AUP_Position.y - cameraAup.y),
                (float)(state.AUP_Position.z - cameraAup.z)), float3.zero);
            float quality = ExosuitMathGuards.AuthoritativeQualityWeight;
            float sdfEpsilon = tuning.SdfEpsilonMeters;
            float radius = math.max(0.25f, tuning.Radius) + math.lerp(sdfEpsilon * 1.5f, sdfEpsilon * 0.55f, quality);
            int maxSubsteps = math.clamp((int)tuning.MaxSubsteps, 2, 8);
            int iterations = math.clamp((int)math.lerp(2.0f, maxSubsteps, quality), 2, maxSubsteps);
            float push = 0.0f;
            float3 normal = new float3(0.0f, 1.0f, 0.0f);
            float floorY = ExosuitMathGuards.SanitizeFloat(terrain.FloorY, -2.0f);
            float ceilingY = ExosuitMathGuards.SanitizeFloat(terrain.CeilingY, floorY + 2.0f);
            floorY = math.min(floorY, ceilingY - 2.0f);
            ceilingY = math.max(ceilingY, floorY + 2.0f);
            float caveRadius = math.max(1.0f, ExosuitMathGuards.SanitizeNonNegative(terrain.CaveRadius));
            float3 caveCenter = ExosuitMathGuards.SanitizeFloat3(terrain.CaveCenterLocal, float3.zero);

            for (int i = 0; i < iterations; i++)
            {
                float floorDistance = local.y - floorY;
                float ceilingDistance = ceilingY - local.y;
                float2 radial = local.xz - caveCenter.xz;
                float radialSq = math.max(0.0f, math.lengthsq(radial));
                float radialLength = radialSq * math.rsqrt(math.max(radialSq, 0.0001f));
                float wallDistance = caveRadius - radialLength;
                float distance = wallDistance;
                normal = radialLength > 0.0001f
                    ? new float3(-radial.x, 0.0f, -radial.y) * math.rsqrt(math.max(radialSq, 0.0001f))
                    : new float3(1.0f, 0.0f, 0.0f);
                if (floorDistance < distance)
                {
                    distance = floorDistance;
                    normal = new float3(0.0f, 1.0f, 0.0f);
                }
                if (ceilingDistance < distance)
                {
                    distance = ceilingDistance;
                    normal = new float3(0.0f, -1.0f, 0.0f);
                }

                float penetration = radius - distance;
                if (penetration <= 0.0f)
                    continue;

                local += normal * penetration;
                state.Velocity = state.Velocity - normal * math.min(0.0f, math.dot(state.Velocity, normal));
                push = math.max(push, penetration);
            }

            if (!math.all(math.isfinite(local)) ||
                !math.all(math.isfinite(state.Velocity)) ||
                !math.all(math.isfinite(normal)) ||
                !math.isfinite(push))
            {
                local = float3.zero;
                state.Velocity = float3.zero;
                state.AngularVelocity = float3.zero;
                normal = new float3(0.0f, 1.0f, 0.0f);
                push = 0.0f;
                state.Flags |= ExosuitStateFlags.NaNDetected;
            }

            if (push > 0.0f)
                state.Flags |= ExosuitStateFlags.SdfContact;

            state.AUP_Position = cameraAup + new double3(local);
            State[0] = state;
            if (Output.IsCreated && Output.Length > 0)
            {
                ExosuitSolverOutput output = Output[0];
                output.LocalPosition = local;
                output.PushNormal = normal;
                output.PushOutMagnitude = math.max(output.PushOutMagnitude, push);
                output.Flags |= math.select(0u, ExosuitSolverOutput.FlagCollision, push > 0.0f);
                Output[0] = output;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyHydraulicDampeningJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [NoAlias] public NativeArray<ExoScreenDTO> Screen;
        public float DeltaTime;
        public float TargetPressure;
        public float LatencySeconds;

        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0 || !Screen.IsCreated || Screen.Length <= 0)
                return;

            float dt = math.clamp(ExosuitMathGuards.SanitizeFloat(DeltaTime, 0.02f), 0.0001f, 0.05f);
            float latency = math.max(0.05f, ExosuitMathGuards.SanitizeNonNegative(LatencySeconds));
            float pressure = MoveTowards(
                math.saturate(ExosuitMathGuards.SanitizeFloat(Screen[0].HydraulicPressure, 0.0f)),
                math.saturate(ExosuitMathGuards.SanitizeFloat(TargetPressure, 0.0f)),
                dt * math.rcp(latency));
            ExosuitStateDTO state = State[0];
            state.Velocity = ExosuitMathGuards.SanitizeFloat3(state.Velocity, float3.zero);
            state.AngularVelocity = ExosuitMathGuards.SanitizeFloat3(state.AngularVelocity, float3.zero);
            state.Velocity *= math.rcp(1.0f + pressure * dt * 0.85f);
            state.AngularVelocity *= math.rcp(1.0f + pressure * dt * 1.6f);
            State[0] = state;
            ExoScreenDTO screen = Screen[0];
            screen.HydraulicPressure = pressure;
            Screen[0] = screen;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float safeMaxDelta = ExosuitMathGuards.SanitizeNonNegative(maxDelta);
            float delta = ExosuitMathGuards.SanitizeFloat(target - current, 0.0f);
            return math.abs(delta) <= safeMaxDelta ? target : current + math.sign(delta) * safeMaxDelta;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateMagneticClampsJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [ReadOnly, NoAlias] public NativeArray<ExosuitFrameInputDTO> Input;
        [ReadOnly, NoAlias] public NativeArray<ExosuitSolverOutput> Output;

        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0 || !Input.IsCreated || Input.Length <= 0)
                return;

            ExosuitStateDTO state = State[0];
            state.Velocity = ExosuitMathGuards.SanitizeFloat3(state.Velocity, float3.zero);
            state.AngularVelocity = ExosuitMathGuards.SanitizeFloat3(state.AngularVelocity, float3.zero);
            bool grab = (Input[0].ActionMask & ExosuitInputActions.Grab) != 0u;
            float push = Output.IsCreated && Output.Length > 0
                ? ExosuitMathGuards.SanitizeNonNegative(Output[0].PushOutMagnitude)
                : 0.0f;
            if (grab && push > 0.0001f)
            {
                state.Velocity = float3.zero;
                state.AngularVelocity = float3.zero;
                state.Flags |= ExosuitStateFlags.Clamped;
            }
            else
            {
                state.Flags &= ~ExosuitStateFlags.Clamped;
            }

            State[0] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateExosuitMetabolismJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [ReadOnly, NoAlias] public NativeArray<ExosuitFrameInputDTO> Input;
        public float DeltaTime;

        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0)
                return;

            ExosuitStateDTO state = State[0];
            float dt = math.clamp(ExosuitMathGuards.SanitizeFloat(DeltaTime, 0.02f), 0.0001f, 0.05f);
            float activity = 0.0f;
            if (Input.IsCreated && Input.Length > 0)
            {
                ExosuitFrameInputDTO input = Input[0];
                float2 move = ExosuitMathGuards.SanitizeFloat2(input.MoveAxis, float2.zero);
                float vertical = math.clamp(ExosuitMathGuards.SanitizeFloat(input.VerticalAxis, 0.0f), -1.0f, 1.0f);
                activity = math.lengthsq(move) + math.abs(vertical);
            }

            bool active = activity > 0.0001f;
            float heat = math.saturate(ExosuitMathGuards.SanitizeNonNegative(state.ThrusterHeat) + (active ? dt * 0.18f : -dt * 0.12f));
            if (heat >= 0.995f)
                state.Flags |= ExosuitStateFlags.Overheated;
            else if (heat < 0.58f)
                state.Flags &= ~ExosuitStateFlags.Overheated;

            state.ThrusterHeat = heat;
            State[0] = state;
        }
    }
}
