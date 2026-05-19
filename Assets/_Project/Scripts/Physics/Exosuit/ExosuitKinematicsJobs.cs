using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Exosuit
{
    /// <summary>
    /// Deterministic 6D kinematic exosuit solver over DataVault-owned buffers.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct Exosuit6DIntegratorJob : IJob
    {
        [NoAlias] public NativeArray<ExosuitStateDTO> State;
        [ReadOnly, NoAlias] public NativeArray<MockInputBuffer> Input;
        [NoAlias] public NativeArray<ExosuitTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<MockTerrainSDF> Terrain;
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
        public uint Frame;

        private const float MinDt = 0.0001f;
        private const float MaxDt = 0.05f;
        private const float MinMass = 1.0f;
        private const float LowProbeCutoff = 0.5f;
        private const float SecondaryProbeStart = 0.35f;
        private const float SecondaryProbeFull = 0.85f;
        private const float LowTierSdfSkinMeters = 0.04f;
        private const float UltraTierSdfSkinMeters = 0.015f;
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
            MockInputBuffer input = Input[0];
            MockTerrainSDF terrain = Terrain[0];
            ExosuitSolverOutput previousOutput = Output[0];
            MockFlowField flow = Flow.IsCreated && Flow.Length > 0 ? Flow[0] : default;
            MockCrushDepthSignal crush = CrushDepth.IsCreated && CrushDepth.Length > 0 ? CrushDepth[0] : default;

            state.AUP = SanitizeDouble3(state.AUP, terrain.CameraAup);
            double3 cameraAup = SanitizeDouble3(terrain.CameraAup, state.AUP);
            float3 localPosition = SanitizeFloat3(ToLocal(state.AUP, cameraAup), float3.zero);
            float3 velocity = SanitizeFloat3(state.Velocity, float3.zero);
            uint previousMask = state.StateMask;
            uint mask = ExosuitStateFlags.Active;
            float inputQuality = SanitizeNonNegative(input.GlobalQualityWeight);
            float tuningQuality = SanitizeNonNegative(tuning.GlobalQualityWeight);
            float quality = math.saturate(math.min(inputQuality, tuningQuality));
            float2 moveAxis = SanitizeFloat2(input.MoveAxis, float2.zero);
            float verticalAxis = math.clamp(SanitizeFloat(input.VerticalAxis, 0.0f), -1.0f, 1.0f);
            float desiredYaw = WrapRadians(SanitizeFloat(input.DesiredYawRadians, 0.0f));
            bool jumpRequested = (input.ActionMask & ExosuitInputActions.Jump) != 0u;
            verticalAxis = math.max(verticalAxis, math.select(0.0f, 1.0f, jumpRequested));

            float inputMagnitude = math.saturate(math.length(moveAxis) + math.abs(verticalAxis));
            if (jumpRequested)
                inputMagnitude = math.saturate(inputMagnitude + 1.0f);

            float pressureTarget = inputMagnitude;
            float latencyScale = math.lerp(1.35f, 0.75f, Smooth01(0.0f, 1.0f, quality));
            float pressureStep = dt * math.rcp(math.max(0.05f, tuning.HydraulicLatencySeconds * latencyScale));
            float pressure = MoveTowards(SanitizeNonNegative(state.HydraulicPressure), pressureTarget, pressureStep);
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
                pressure = math.min(1.0f, pressure + 0.35f);
                mask |= ExosuitStateFlags.PurgeLatched;
                emitSilt = true;
            }

            float3 yawForward = new float3(math.sin(desiredYaw), 0.0f, math.cos(desiredYaw));
            float3 yawRight = new float3(yawForward.z, 0.0f, -yawForward.x);
            float3 desiredDirection = yawRight * moveAxis.x + yawForward * moveAxis.y + new float3(0.0f, verticalAxis, 0.0f);
            desiredDirection = NormalizeWithFallback(desiredDirection, float3.zero);
            float3 rawDesiredVelocity = desiredDirection * tuning.MaxSpeedMetersPerSecond * pressure;
            float3 previousDesiredVelocity = SanitizeFloat3(previousOutput.DesiredVelocity, float3.zero);
            float actuatorRateScale = math.lerp(0.62f, 1.35f, Smooth01(0.0f, 1.0f, quality));
            float actuatorMaxDelta = tuning.MaxSpeedMetersPerSecond * actuatorRateScale * dt * math.rcp(math.max(0.05f, tuning.HydraulicLatencySeconds));
            float3 desiredVelocity = MoveTowardsVector(previousDesiredVelocity, rawDesiredVelocity, actuatorMaxDelta);
            float desiredSpeed = math.sqrt(math.max(0.0f, math.lengthsq(desiredVelocity)));
            float actuatorPressure = math.saturate(desiredSpeed * math.rcp(math.max(0.1f, tuning.MaxSpeedMetersPerSecond)));
            float3 actuatorDirection = NormalizeWithFallback(desiredVelocity, desiredDirection);

            float3 thrustAcceleration = actuatorDirection * tuning.ThrusterForce * actuatorPressure * math.rcp(math.max(MinMass, tuning.CurrentMass));
            velocity += thrustAcceleration * dt;
            velocity = ApplyAnalyticalDrag(velocity, tuning.Drag, dt, quality);

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
            float sdfSkinMeters = math.lerp(LowTierSdfSkinMeters, UltraTierSdfSkinMeters, Smooth01(0.0f, 1.0f, quality));
            float contactRadius = tuning.Radius + sdfSkinMeters;

            float lowProbeBudget = 1.0f - math.step(LowProbeCutoff, quality);
            mask |= ((uint)lowProbeBudget) * ExosuitStateFlags.LowProbeBudget;

            float secondaryWeight = Smooth01(SecondaryProbeStart, SecondaryProbeFull, quality);
            if (secondaryWeight > 0.0001f)
                mask |= ExosuitStateFlags.SecondaryProbeBlend;

            float pushMagnitude = 0.0f;
            float lostVelocityMagnitude = 0.0f;
            float3 pushNormal = new float3(0.0f, 1.0f, 0.0f);
            float pendingSecondaryPush = 0.0f;
            float3 pendingSecondaryNormal = pushNormal;

            float ccdWeight = Smooth01(0.72f, 1.0f, quality);
            if (ccdWeight > 0.0001f)
            {
                float3 midPosition = localPosition - velocity * (dt * 0.5f);
                SdfSample midSdf = SampleCaveSdf(midPosition, terrain);
                float midPenetration = contactRadius - midSdf.Distance;
                if (midPenetration > 0.0f)
                {
                    float sweepPush = midPenetration * ccdWeight;
                    float3 midNormal = NormalizeWithFallback(midSdf.Normal, pushNormal);
                    localPosition += midNormal * sweepPush;
                    pushMagnitude = math.max(pushMagnitude, sweepPush);
                    pushNormal = midNormal;
                }
            }

            SdfSample sdf = SampleCaveSdf(localPosition, terrain);
            pushNormal = pushMagnitude > 0.0f ? pushNormal : sdf.Normal;

            if (secondaryWeight > 0.0001f)
            {
                ApplySecondaryProbe(
                    localPosition,
                    terrain,
                    contactRadius,
                    secondaryWeight,
                    ref pendingSecondaryNormal,
                    ref pendingSecondaryPush);
            }

            float penetration = contactRadius - sdf.Distance;
            float pendingPush = math.max(penetration, pendingSecondaryPush);
            if (pendingPush > 0.0f)
            {
                pushNormal = pendingSecondaryPush > penetration ? pendingSecondaryNormal : sdf.Normal;
                pushNormal = NormalizeWithFallback(pushNormal, sdf.Normal);
                localPosition += pushNormal * pendingPush;
                velocity = ApplyContactVelocityResponse(velocity, pushNormal, pendingPush, quality, out float contactLostSpeed);
                lostVelocityMagnitude = math.max(lostVelocityMagnitude, contactLostSpeed);

                pushMagnitude = math.max(pushMagnitude, pendingPush);
                if (pushNormal.y > 0.5f)
                    mask |= ExosuitStateFlags.Grounded;
            }

            bool grabRequested = (input.ActionMask & ExosuitInputActions.Grab) != 0u;
            SdfSample postPushSdf = SampleCaveSdf(localPosition, terrain);
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

                postPushSdf = SampleCaveSdf(localPosition, terrain);
            }

            if (secondaryWeight > 0.0001f && pushMagnitude > 0.0f)
            {
                float residualSecondaryPush = 0.0f;
                float3 residualSecondaryNormal = pushNormal;
                ApplySecondaryProbe(
                    localPosition,
                    terrain,
                    contactRadius,
                    secondaryWeight,
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

                    postPushSdf = SampleCaveSdf(localPosition, terrain);
                }
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
                    postPushSdf = SampleCaveSdf(localPosition, terrain);
                    float clampResidual = contactRadius - postPushSdf.Distance;
                    if (clampResidual > 0.0001f)
                    {
                        float3 clampResidualNormal = NormalizeWithFallback(postPushSdf.Normal, clampAnchorNormal);
                        localPosition += clampResidualNormal * clampResidual;
                        pushMagnitude = math.max(pushMagnitude, clampResidual);
                        if (clampResidualNormal.y > 0.5f)
                            mask |= ExosuitStateFlags.Grounded;
                        postPushSdf = SampleCaveSdf(localPosition, terrain);
                    }
                }

                velocity = float3.zero;
                desiredVelocity = float3.zero;
                pushNormal = clampAnchorNormal;
                state.AnchorNormal = clampAnchorNormal;
                pushMagnitude = math.max(pushMagnitude, math.abs(clampCorrection));
                mask |= ExosuitStateFlags.Clamped;
            }
            else
            {
                state.AnchorNormal = pushNormal;
            }

            bool badMath = !math.all(math.isfinite(localPosition)) ||
                           !math.all(math.isfinite(velocity)) ||
                           !math.isfinite(pressure) ||
                           !math.all(math.isfinite(pushNormal)) ||
                           !math.isfinite(postPushSdf.Distance);
            if (badMath)
            {
                localPosition = float3.zero;
                velocity = float3.zero;
                pressure = 0.0f;
                desiredVelocity = float3.zero;
                pushMagnitude = 0.0f;
                lostVelocityMagnitude = 0.0f;
                pushNormal = new float3(0.0f, 1.0f, 0.0f);
                floorContact = false;
                mask |= ExosuitStateFlags.NaNDetected;
            }

            float3 snappedLocalPosition = SnapMillimeter(localPosition);
            float3 snappedVelocity = SnapMillimeter(velocity);
            state.AUP = cameraAup + new double3(snappedLocalPosition);
            state.Velocity = snappedVelocity;
            state.HydraulicPressure = math.saturate(pressure);
            state.AnchorNormal = NormalizeWithFallback(state.AnchorNormal, pushNormal);
            state.StateMask = mask | (tuning.Flags & ExosuitStateFlags.EmergencyMockData);
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
                outputFlags |= AccumulateFootstep(state.AUP, math.length(velocity) * dt, tuning.FootstepStrideMeters);

            uint stateHash = ComputeStateHash(snappedLocalPosition, snappedVelocity, state.HydraulicPressure, state.StateMask);
            ExosuitSolverOutput solverOutput = default;
            solverOutput.LocalPosition = snappedLocalPosition;
            solverOutput.DesiredVelocity = SnapMillimeter(desiredVelocity);
            solverOutput.PushNormal = pushNormal;
            solverOutput.PushOutMagnitude = pushMagnitude;
            solverOutput.LostVelocityMagnitude = lostVelocityMagnitude;
            solverOutput.Speed = math.sqrt(math.max(0.0f, math.lengthsq(velocity)));
            solverOutput.Flags = outputFlags;
            solverOutput.Frame = Frame;
            solverOutput.StateHash = stateHash;
            Output[0] = solverOutput;

            WriteOptionalOutputs(state, crush, outputFlags, pushMagnitude, lostVelocityMagnitude, tuning.CurrentMass, quality, emitSilt);
            WriteScreen(state, crush);
            WriteTelemetry(state, pushMagnitude, stateHash, badMath ? 1u : 0u);
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
                    float amplitude = math.saturate(math.sqrt(math.max(0.0f, impact)) * math.lerp(0.11f, 0.17f, Smooth01(0.0f, 1.0f, quality)));
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
                    silt.AUP = state.AUP;
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

        private void WriteScreen(in ExosuitStateDTO state, in MockCrushDepthSignal crush)
        {
            if (!Screen.IsCreated || Screen.Length <= 0)
                return;

            ExoScreenDTO screen = default;
            screen.HydraulicPressure = state.HydraulicPressure;
            screen.DepthMeters = SanitizeNonNegative(crush.DepthMeters);
            screen.StateMask = state.StateMask;
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
            entry.AUP = state.AUP;
            entry.Velocity = state.Velocity;
            entry.HydraulicPressure = state.HydraulicPressure;
            entry.SdfPushOutMagnitude = pushMagnitude;
            entry.SolverComputeTimeMs = 0.0f;
            entry.Frame = Frame;
            entry.StateMask = state.StateMask;
            entry.ErrorFlags = errorFlags;
            entry.StateHash = stateHash;
            TelemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= TelemetryRing.Length)
                cursor = 0;
            TelemetryCursor[0] = cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ExosuitTuningDTO SanitizeTuning(ExosuitTuningDTO tuning)
        {
            tuning.BaseMass = math.max(MinMass, SanitizeNonNegative(tuning.BaseMass));
            float currentMass = SanitizeNonNegative(tuning.CurrentMass);
            tuning.CurrentMass = currentMass > 0.0f ? math.max(MinMass, currentMass) : tuning.BaseMass;
            tuning.Drag = math.clamp(SanitizeNonNegative(tuning.Drag), 0.0f, 8.0f);
            tuning.ThrusterForce = math.max(0.0f, SanitizeNonNegative(tuning.ThrusterForce));
            tuning.Radius = math.clamp(SanitizeNonNegative(tuning.Radius), 0.25f, 5.0f);
            tuning.ClampRange = math.max(tuning.Radius, SanitizeNonNegative(tuning.ClampRange));
            tuning.HydraulicLatencySeconds = math.clamp(SanitizeNonNegative(tuning.HydraulicLatencySeconds), 0.05f, 3.0f);
            tuning.PurgeImpulse = math.clamp(SanitizeNonNegative(tuning.PurgeImpulse), 0.0f, 80.0f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeNonNegative(tuning.GlobalQualityWeight));
            tuning.FootstepStrideMeters = math.clamp(SanitizeNonNegative(tuning.FootstepStrideMeters), 0.25f, 12.0f);
            tuning.MaxSpeedMetersPerSecond = math.clamp(SanitizeNonNegative(tuning.MaxSpeedMetersPerSecond), 0.25f, 40.0f);
            tuning.CrushDepthMeters = math.max(1.0f, SanitizeNonNegative(tuning.CrushDepthMeters));
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplySecondaryProbe(
            float3 center,
            in MockTerrainSDF terrain,
            float radius,
            float weight,
            ref float3 normal,
            ref float push)
        {
            float probeOffset = radius * 0.65f;
            float probeRadius = math.max(0.0f, radius - probeOffset) * math.saturate(weight);
            float3 correction = float3.zero;
            float3 strongestNormal = normal;
            float strongestPush = 0.0f;
            SdfSample px = SampleCaveSdf(center + new float3(probeOffset, 0.0f, 0.0f), terrain);
            SdfSample nx = SampleCaveSdf(center - new float3(probeOffset, 0.0f, 0.0f), terrain);
            SdfSample py = SampleCaveSdf(center + new float3(0.0f, probeOffset, 0.0f), terrain);
            SdfSample ny = SampleCaveSdf(center - new float3(0.0f, probeOffset, 0.0f), terrain);
            SdfSample pz = SampleCaveSdf(center + new float3(0.0f, 0.0f, probeOffset), terrain);
            SdfSample nz = SampleCaveSdf(center - new float3(0.0f, 0.0f, probeOffset), terrain);

            AccumulateProbeCorrection(px, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(nx, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(py, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(ny, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(pz, probeRadius, ref correction, ref strongestNormal, ref strongestPush);
            AccumulateProbeCorrection(nz, probeRadius, ref correction, ref strongestNormal, ref strongestPush);

            if (strongestPush <= 0.0f)
                return;

            float correctionSq = math.lengthsq(correction);
            if (correctionSq > 0.000001f)
            {
                float correctionMagnitude = math.sqrt(math.max(0.0f, correctionSq));
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
            float radialLength = math.sqrt(math.max(0.0f, math.lengthsq(radial)));
            float wallDistance = radius - radialLength;
            float floorDistance = localPosition.y - floorY;
            float ceilingDistance = ceilingY - localPosition.y;

            float distance = wallDistance;
            float3 wallNormal = radialLength > 0.0001f
                ? new float3(-radial.x, 0.0f, -radial.y) * math.rsqrt(math.max(radialLength * radialLength, 0.0001f))
                : new float3(1.0f, 0.0f, 0.0f);
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
            float distance = math.sqrt(math.max(0.0f, distanceSq));
            if (distance <= safeMaxDelta)
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
            float speed = math.sqrt(math.max(0.0f, math.lengthsq(velocity)));
            float denominator = 1.0f + math.max(0.0f, drag) * qualityDamping * speed * math.max(MinDt, dt);
            return velocity * math.rcp(math.max(0.0001f, denominator));
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
            lostSpeed = math.sqrt(math.max(0.0f, math.lengthsq(before - velocity)));
            return velocity;
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
        private static uint ComputeStateHash(float3 position, float3 velocity, float pressure, uint mask)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(position.x));
            hash = Mix(hash, math.asuint(position.y));
            hash = Mix(hash, math.asuint(position.z));
            hash = Mix(hash, math.asuint(velocity.x));
            hash = Mix(hash, math.asuint(velocity.y));
            hash = Mix(hash, math.asuint(velocity.z));
            hash = Mix(hash, math.asuint(pressure));
            hash = Mix(hash, mask);
            return hash != 0u ? hash : 1u;
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
}
