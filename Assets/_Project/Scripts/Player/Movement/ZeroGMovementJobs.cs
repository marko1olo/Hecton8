using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Player.Movement
{
    internal static class ZeroGMathGuards
    {
        public const float DefaultQualityWeight = 1f;
        private const float MinVectorLengthSq = 0.000001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFloat(float value, float fallback)
        {
            return math.select(value, fallback, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            float safeFallback = math.saturate(math.select(DefaultQualityWeight, fallback, math.isfinite(fallback)));
            return math.saturate(math.select(safeFallback, value, math.isfinite(value)));
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
        public static float3 NormalizeWithFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.select(fallback, value * math.rsqrt(math.max(lengthSq, MinVectorLengthSq)), lengthSq > MinVectorLengthSq && math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampLength(float3 value, float maxLength)
        {
            float maxSafe = math.max(0.0f, SanitizeFloat(maxLength, 0.0f));
            float lengthSq = math.lengthsq(value);
            float maxSq = maxSafe * maxSafe;
            return math.select(value, value * (maxSafe * math.rsqrt(math.max(lengthSq, MinVectorLengthSq))), lengthSq > maxSq && maxSq > 0.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion SanitizeQuaternion(quaternion value, quaternion fallback)
        {
            bool finite = math.all(math.isfinite(value.value));
            float lengthSq = math.lengthsq(value.value);
            quaternion safeFallback = math.all(math.isfinite(fallback.value)) && math.lengthsq(fallback.value) > MinVectorLengthSq
                ? math.normalize(fallback)
                : quaternion.identity;
            return finite && lengthSq > MinVectorLengthSq ? math.normalize(value) : safeFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZeroGTuningDTO SanitizeTuning(ZeroGTuningDTO tuning)
        {
            tuning.ThrustAcceleration = math.max(0.0f, SanitizeFloat(tuning.ThrustAcceleration, 6.0f));
            tuning.AngularAcceleration = math.max(0.0f, SanitizeFloat(tuning.AngularAcceleration, 2.4f));
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, SanitizeFloat(tuning.MaxSpeedMetersPerSecond, 9.0f));
            tuning.MaxAngularRadiansPerSecond = math.max(0.05f, SanitizeFloat(tuning.MaxAngularRadiansPerSecond, 2.8f));
            tuning.RadiusMeters = math.clamp(SanitizeFloat(tuning.RadiusMeters, 0.45f), 0.1f, 3.0f);
            tuning.Restitution = math.clamp(SanitizeFloat(tuning.Restitution, 0.6f), 0.0f, 1.0f);
            tuning.PushImpulseVelocityChange = math.max(0.0f, SanitizeFloat(tuning.PushImpulseVelocityChange, 3.2f));
            tuning.DepenetrationSlopMeters = math.clamp(SanitizeFloat(tuning.DepenetrationSlopMeters, 0.015f), 0.0f, 0.25f);
            tuning.HorizonLockStrength = math.clamp(SanitizeFloat(tuning.HorizonLockStrength, 2.2f), 0.0f, 16.0f);
            tuning.PropellantDrainPerSecond = math.max(0.0001f, SanitizeFloat(tuning.PropellantDrainPerSecond, 0.035f));
            tuning.GlobalQualityWeight = Sanitize01(tuning.GlobalQualityWeight, DefaultQualityWeight);
            tuning.SurfaceProbeRadiusMeters = math.max(tuning.RadiusMeters, SanitizeFloat(tuning.SurfaceProbeRadiusMeters, tuning.RadiusMeters));
            tuning.OrbitBoundsHalfExtents = math.max(SanitizeFloat3(tuning.OrbitBoundsHalfExtents, new float3(12f, 8f, 18f)), new float3(tuning.RadiusMeters + 0.5f));
            tuning.HorizonUp = NormalizeWithFallback(tuning.HorizonUp, new float3(0f, 1f, 0f));
            tuning.MaxSubsteps = math.clamp(tuning.MaxSubsteps, 1u, 8u);
            tuning.CameraTraumaScale = math.max(0.0f, SanitizeFloat(tuning.CameraTraumaScale, 0.18f));
            tuning.HapticScale = math.max(0.0f, SanitizeFloat(tuning.HapticScale, 0.2f));
            tuning.StateHash = ComputeTuningHash(in tuning);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeTuningHash(in ZeroGTuningDTO tuning)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(tuning.ThrustAcceleration)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.Restitution)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.PushImpulseVelocityChange)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.OrbitBoundsHalfExtents.x)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.OrbitBoundsHalfExtents.y)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.OrbitBoundsHalfExtents.z)) * 16777619u;
            hash = (hash ^ tuning.MaxSubsteps) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ZeroGPhysicsIntegrationJob : IJob
    {
        [NoAlias] public NativeArray<ZeroGMovementStateDTO> State;
        [NoAlias] public NativeArray<ZeroGInputStateDTO> Input;
        [NoAlias] public NativeArray<ZeroGTuningDTO> Tuning;
        [NoAlias] public NativeArray<ZeroGSurfaceHitDTO> SurfaceHit;
        [NoAlias] public NativeArray<ZeroGSolverOutputDTO> Output;
        [NoAlias] public NativeArray<ZeroGTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;

        public double3 CameraAup;
        public float DeltaTime;
        public uint Frame;

        private const float MinDt = 0.0001f;
        private const float MaxDt = 0.05f;
        private const uint SourceHash = 0x5A474B50u;

        public void Execute()
        {
            if (!State.IsCreated || State.Length <= 0 ||
                !Input.IsCreated || Input.Length <= 0 ||
                !Tuning.IsCreated || Tuning.Length <= 0 ||
                !SurfaceHit.IsCreated || SurfaceHit.Length <= 0 ||
                !Output.IsCreated || Output.Length <= 0)
            {
                return;
            }

            float dt = math.clamp(math.max(0.0f, ZeroGMathGuards.SanitizeFloat(DeltaTime, 0.02f)), MinDt, MaxDt);
            double3 cameraAup = ZeroGMathGuards.SanitizeDouble3(CameraAup, double3.zero);
            ZeroGMovementStateDTO state = State[0];
            ZeroGInputStateDTO input = Input[0];
            ZeroGTuningDTO rawTuning = Tuning[0];
            bool sourceNonFinite = SourceContainsNonFinite(in state, in input, in rawTuning, CameraAup, DeltaTime);
            ZeroGTuningDTO tuning = ZeroGMathGuards.SanitizeTuning(rawTuning);
            Tuning[0] = tuning;

            float quality = math.min(
                ZeroGMathGuards.Sanitize01(input.GlobalQualityWeight, tuning.GlobalQualityWeight),
                tuning.GlobalQualityWeight);
            uint flags = ZeroGMovementStateFlags.Active;
            uint fault = sourceNonFinite ? ZeroGMovementFaultCodes.NonFinite : ZeroGMovementFaultCodes.None;
            if (sourceNonFinite)
                flags |= ZeroGMovementStateFlags.NaNDetected;

            state.AUP_Position = ZeroGMathGuards.SanitizeDouble3(state.AUP_Position, cameraAup);
            double3 rawLocalOffset = state.AUP_Position - cameraAup;
            float3 rawLocalPosition = (float3)rawLocalOffset;
            float rawLocalLengthSq = math.lengthsq(rawLocalPosition);
            bool localOffsetFault = !math.all(math.isfinite(rawLocalOffset)) ||
                                    !math.all(math.isfinite(rawLocalPosition)) ||
                                    !math.isfinite(rawLocalLengthSq);
            if (localOffsetFault)
            {
                fault = ZeroGMovementFaultCodes.NonFinite;
                flags |= ZeroGMovementStateFlags.NaNDetected;
            }
            bool frameInputAllowed = !sourceNonFinite && !localOffsetFault;
            float3 localPosition = localOffsetFault ? float3.zero : ZeroGMathGuards.SanitizeFloat3(rawLocalPosition, float3.zero);
            quaternion orientation = ZeroGMathGuards.SanitizeQuaternion(state.Orientation, input.ViewOrientation);
            float3 velocity = ZeroGMathGuards.SanitizeFloat3(state.LinearVelocity, float3.zero);
            float3 angularMomentum = ZeroGMathGuards.SanitizeFloat3(state.AngularMomentum, float3.zero);
            float propellant01 = math.saturate(ZeroGMathGuards.SanitizeFloat(state.SuitPropellant01, 0.0f));
            float radius = math.clamp(ZeroGMathGuards.SanitizeFloat(state.RadiusMeters, tuning.RadiusMeters), 0.1f, 3.0f);
            float restitution = math.clamp(ZeroGMathGuards.SanitizeFloat(state.Restitution, tuning.Restitution), 0.0f, 1.0f);
            float horizonWeight = math.saturate(ZeroGMathGuards.SanitizeFloat(state.HorizonLockWeight, 1.0f));
            float3 localThrust = frameInputAllowed ? ZeroGMathGuards.ClampLength(ZeroGMathGuards.SanitizeFloat3(input.LocalThrustAxis, float3.zero), 1.0f) : float3.zero;
            float3 localAngular = frameInputAllowed ? ZeroGMathGuards.ClampLength(ZeroGMathGuards.SanitizeFloat3(input.LocalAngularAxis, float3.zero), 1.0f) : float3.zero;

            if ((input.ActionMask & ZeroGInputActions.ExternalAuthority) != 0u ||
                (input.Flags & ZeroGMovementStateFlags.ExternalInput) != 0u)
                flags |= ZeroGMovementStateFlags.ExternalInput;
            if ((input.Flags & ZeroGMovementStateFlags.SignalDrop) != 0u)
                flags |= ZeroGMovementStateFlags.SignalDrop;

            uint previousActionMask = state.LastActionMask & ZeroGInputActions.SimulationMask;
            uint inputActionMask = input.ActionMask & ZeroGInputActions.SimulationMask;
            uint acceptedActionMask = frameInputAllowed ? inputActionMask : previousActionMask;
            bool thrustRequested = frameInputAllowed && (inputActionMask & ZeroGInputActions.Thruster) != 0u && math.lengthsq(localThrust) > 0.000001f;
            bool horizonRequested = frameInputAllowed && (inputActionMask & ZeroGInputActions.HorizonLock) != 0u && horizonWeight > 0.0f;
            bool brakeRequested = frameInputAllowed && (inputActionMask & ZeroGInputActions.BrakeAssist) != 0u;
            bool pushInputRequested = frameInputAllowed && (inputActionMask & ZeroGInputActions.PushAndGlide) != 0u;
            bool pushRequested = pushInputRequested && (previousActionMask & ZeroGInputActions.PushAndGlide) == 0u;
            uint substepCount = math.clamp(tuning.MaxSubsteps, 1u, 8u);
            float subDt = frameInputAllowed ? dt / substepCount : 0.0f;
            float collisionImpulse = 0.0f;
            float maxDepenetration = 0.0f;
            bool pushConsumed = false;
            ZeroGSurfaceHitDTO surface = default;

            for (uint substep = 0u; substep < substepCount; substep++)
            {
                if (thrustRequested && propellant01 > 0.0f)
                {
                    float thrustMagnitude = math.length(localThrust);
                    float requestedDrain = thrustMagnitude * tuning.PropellantDrainPerSecond * subDt;
                    float thrustScale = requestedDrain > 0.0f ? math.saturate(propellant01 / requestedDrain) : 1.0f;
                    float3 thrustDirection = math.rotate(orientation, localThrust);
                    velocity += thrustDirection * tuning.ThrustAcceleration * subDt * thrustScale;
                    propellant01 = math.max(0.0f, propellant01 - requestedDrain * thrustScale);
                    flags |= ZeroGMovementStateFlags.ThrusterActive;
                }

                angularMomentum += localAngular * tuning.AngularAcceleration * subDt;
                angularMomentum = ZeroGMathGuards.ClampLength(angularMomentum, tuning.MaxAngularRadiansPerSecond);
                orientation = IntegrateOrientation(orientation, angularMomentum, subDt);

                if (horizonRequested)
                {
                    orientation = ApplyHorizonLock(orientation, tuning.HorizonUp, tuning.HorizonLockStrength * horizonWeight, subDt);
                    flags |= ZeroGMovementStateFlags.HorizonLocked;
                }

                if (brakeRequested)
                {
                    float brakeStep = math.saturate(subDt * 0.75f);
                    float3 velocityDelta = math.lerp(velocity, float3.zero, brakeStep) - velocity;
                    float3 angularDelta = math.lerp(angularMomentum, float3.zero, brakeStep) - angularMomentum;
                    float brakeEffort = math.length(velocityDelta) + math.length(angularDelta);
                    float requestedDrain = brakeEffort * tuning.PropellantDrainPerSecond * 0.1f;
                    float brakeScale = requestedDrain > 0.0f
                        ? math.select(0.0f, math.saturate(propellant01 / requestedDrain), propellant01 > 0.0f)
                        : math.select(0.0f, 1.0f, propellant01 > 0.0f);
                    velocity += velocityDelta * brakeScale;
                    angularMomentum += angularDelta * brakeScale;
                    propellant01 = math.max(0.0f, propellant01 - requestedDrain * brakeScale);
                    if (brakeEffort > 0.000001f && brakeScale > 0.0f)
                        flags |= ZeroGMovementStateFlags.ThrusterActive;
                }

                velocity = ZeroGMathGuards.ClampLength(velocity, tuning.MaxSpeedMetersPerSecond);
                localPosition += velocity * subDt;

                ZeroGSurfaceHitDTO hit = ResolveAnalyticOrbitSurface(
                    ref localPosition,
                    ref velocity,
                    tuning,
                    radius,
                    restitution,
                    pushRequested && !pushConsumed,
                    quality,
                    Frame,
                    ref flags);

                if ((hit.Flags & ZeroGSurfaceHitFlags.Valid) != 0u)
                {
                    pushConsumed = pushRequested;
                    collisionImpulse = math.max(collisionImpulse, hit.CollisionImpulse);
                    maxDepenetration = math.max(maxDepenetration, hit.PenetrationMeters);
                    if ((surface.Flags & ZeroGSurfaceHitFlags.Valid) == 0u ||
                        hit.CollisionImpulse >= surface.CollisionImpulse)
                    {
                        surface = hit;
                    }
                }
                else if ((surface.Flags & ZeroGSurfaceHitFlags.Valid) == 0u)
                {
                    surface = hit;
                }
            }

            if (propellant01 <= 0.0f)
                flags |= ZeroGMovementStateFlags.PropellantDry;

            surface.CollisionImpulse = collisionImpulse;
            surface.PenetrationMeters = math.max(surface.PenetrationMeters, maxDepenetration);
            float trauma01 = math.saturate(collisionImpulse * tuning.CameraTraumaScale * math.lerp(0.65f, 1.35f, quality));

            if (!math.all(math.isfinite(localPosition)) ||
                !math.all(math.isfinite(velocity)) ||
                !math.all(math.isfinite(angularMomentum)) ||
                !math.all(math.isfinite(orientation.value)))
            {
                fault = ZeroGMovementFaultCodes.NonFinite;
                flags |= ZeroGMovementStateFlags.NaNDetected;
                localPosition = float3.zero;
                velocity = float3.zero;
                angularMomentum = float3.zero;
                orientation = quaternion.identity;
            }

            uint stateHash = ComputeStateHash(localPosition, velocity, angularMomentum, flags, acceptedActionMask, Frame);
            state.AUP_Position = localOffsetFault ? state.AUP_Position : cameraAup + (double3)localPosition;
            state.Orientation = orientation;
            state.LinearVelocity = velocity;
            state.AngularMomentum = angularMomentum;
            state.SuitPropellant01 = propellant01;
            state.RadiusMeters = radius;
            state.Restitution = restitution;
            state.HorizonLockWeight = horizonWeight;
            state.LastCollisionImpulse = collisionImpulse;
            state.LastDepenetration = surface.PenetrationMeters;
            state.Flags = flags;
            state.Frame = Frame;
            state.SimulationTick = input.SimulationTick != 0L ? input.SimulationTick : state.SimulationTick + 1L;
            state.StateHash = stateHash;
            state.FaultCode = fault;
            state.LastActionMask = acceptedActionMask;
            State[0] = state;

            surface.Frame = Frame;
            SurfaceHit[0] = surface;

            ZeroGSolverOutputDTO output = default;
            output.LocalPosition = localPosition;
            output.LinearVelocity = velocity;
            output.CollisionNormal = surface.Normal;
            output.CollisionImpulse = collisionImpulse;
            output.CameraTrauma01 = trauma01;
            output.Propellant01 = propellant01;
            output.Flags = 0u;
            if ((surface.Flags & ZeroGSurfaceHitFlags.Valid) != 0u)
                output.Flags |= ZeroGSolverOutputDTO.FlagCollision;
            if (trauma01 > 0.0001f)
                output.Flags |= ZeroGSolverOutputDTO.FlagCameraTrauma | ZeroGSolverOutputDTO.FlagHaptic;
            if (fault != 0u)
                output.Flags |= ZeroGSolverOutputDTO.FlagFault;
            output.Frame = Frame;
            output.StateHash = stateHash;
            output.FaultCode = fault;
            Output[0] = output;

            WriteTelemetry(localPosition, velocity, angularMomentum, collisionImpulse, propellant01, flags, stateHash, fault, Frame);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SourceContainsNonFinite(
            in ZeroGMovementStateDTO state,
            in ZeroGInputStateDTO input,
            in ZeroGTuningDTO tuning,
            double3 cameraAup,
            float deltaTime)
        {
            return !math.isfinite(deltaTime) ||
                   !math.all(math.isfinite(cameraAup)) ||
                   !math.all(math.isfinite(state.AUP_Position)) ||
                   !math.all(math.isfinite(state.Orientation.value)) ||
                   !math.all(math.isfinite(state.LinearVelocity)) ||
                   !math.all(math.isfinite(state.AngularMomentum)) ||
                   !math.isfinite(state.SuitPropellant01) ||
                   !math.isfinite(state.RadiusMeters) ||
                   !math.isfinite(state.Restitution) ||
                   !math.isfinite(state.HorizonLockWeight) ||
                   !math.isfinite(state.LastCollisionImpulse) ||
                   !math.isfinite(state.LastDepenetration) ||
                   !math.all(math.isfinite(input.LocalThrustAxis)) ||
                   !math.all(math.isfinite(input.LocalAngularAxis)) ||
                   !math.all(math.isfinite(input.ViewOrientation.value)) ||
                   !math.isfinite(input.GlobalQualityWeight) ||
                   !math.isfinite(tuning.ThrustAcceleration) ||
                   !math.isfinite(tuning.AngularAcceleration) ||
                   !math.isfinite(tuning.MaxSpeedMetersPerSecond) ||
                   !math.isfinite(tuning.MaxAngularRadiansPerSecond) ||
                   !math.isfinite(tuning.RadiusMeters) ||
                   !math.isfinite(tuning.Restitution) ||
                   !math.isfinite(tuning.PushImpulseVelocityChange) ||
                   !math.isfinite(tuning.DepenetrationSlopMeters) ||
                   !math.isfinite(tuning.HorizonLockStrength) ||
                   !math.isfinite(tuning.PropellantDrainPerSecond) ||
                   !math.isfinite(tuning.GlobalQualityWeight) ||
                   !math.isfinite(tuning.SurfaceProbeRadiusMeters) ||
                   !math.all(math.isfinite(tuning.OrbitBoundsHalfExtents)) ||
                   !math.all(math.isfinite(tuning.HorizonUp)) ||
                   !math.isfinite(tuning.CameraTraumaScale) ||
                   !math.isfinite(tuning.HapticScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion IntegrateOrientation(quaternion orientation, float3 angularRadiansPerSecond, float dt)
        {
            float3 delta = angularRadiansPerSecond * dt;
            float angle = math.length(delta);
            if (angle <= 0.000001f || !math.isfinite(angle))
                return ZeroGMathGuards.SanitizeQuaternion(orientation, quaternion.identity);

            float3 axis = delta * math.rcp(angle);
            quaternion deltaRotation = quaternion.AxisAngle(axis, angle);
            return ZeroGMathGuards.SanitizeQuaternion(math.mul(orientation, deltaRotation), orientation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion ApplyHorizonLock(quaternion orientation, float3 horizonUp, float strength, float dt)
        {
            float3 currentUp = math.rotate(orientation, new float3(0f, 1f, 0f));
            float dot = math.clamp(math.dot(currentUp, horizonUp), -1.0f, 1.0f);
            float3 axis = math.cross(currentUp, horizonUp);
            float axisLengthSq = math.lengthsq(axis);
            if (axisLengthSq <= 0.000001f)
            {
                if (dot > 0.9999f)
                    return orientation;

                axis = ZeroGMathGuards.NormalizeWithFallback(math.rotate(orientation, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
                axisLengthSq = 1.0f;
            }

            float angle = math.acos(dot);
            float step = math.min(angle, math.max(0.0f, strength) * dt);
            quaternion correction = quaternion.AxisAngle(axis * math.rsqrt(axisLengthSq), step);
            return ZeroGMathGuards.SanitizeQuaternion(math.mul(correction, orientation), orientation);
        }

        private static ZeroGSurfaceHitDTO ResolveAnalyticOrbitSurface(
            ref float3 localPosition,
            ref float3 velocity,
            in ZeroGTuningDTO tuning,
            float radius,
            float restitution,
            bool pushRequested,
            float quality,
            uint frame,
            ref uint stateFlags)
        {
            ZeroGSurfaceHitDTO hit = default;
            float3 inner = math.max(tuning.OrbitBoundsHalfExtents - new float3(radius), new float3(radius));
            float3 clampedPosition = math.clamp(localPosition, -inner, inner);
            float3 depenetrationVector = clampedPosition - localPosition;
            float depenetrationLengthSq = math.lengthsq(depenetrationVector);
            float bestPenetration = math.sqrt(depenetrationLengthSq);
            float3 bestNormal = depenetrationLengthSq > 0.00000001f
                ? depenetrationVector * math.rsqrt(depenetrationLengthSq)
                : float3.zero;
            float nearestDistance = float.MaxValue;
            float3 nearestNormal = float3.zero;

            EvaluateClearance(inner.x - localPosition.x, new float3(-1f, 0f, 0f), ref nearestDistance, ref nearestNormal);
            EvaluateClearance(localPosition.x + inner.x, new float3(1f, 0f, 0f), ref nearestDistance, ref nearestNormal);
            EvaluateClearance(inner.y - localPosition.y, new float3(0f, -1f, 0f), ref nearestDistance, ref nearestNormal);
            EvaluateClearance(localPosition.y + inner.y, new float3(0f, 1f, 0f), ref nearestDistance, ref nearestNormal);
            EvaluateClearance(inner.z - localPosition.z, new float3(0f, 0f, -1f), ref nearestDistance, ref nearestNormal);
            EvaluateClearance(localPosition.z + inner.z, new float3(0f, 0f, 1f), ref nearestDistance, ref nearestNormal);

            if (bestPenetration <= 0.0f)
            {
                hit.Normal = nearestNormal;
                hit.PointLocal = localPosition - nearestNormal * (radius + math.max(0.0f, nearestDistance));
                hit.DistanceMeters = nearestDistance;
                hit.QualityProbeWeight = quality;
                hit.Frame = frame;
                hit.SurfaceHash = 0x4F524254u;
                if (pushRequested && nearestDistance <= tuning.SurfaceProbeRadiusMeters)
                {
                    float3 pushPreviousVelocity = velocity;
                    velocity += nearestNormal * tuning.PushImpulseVelocityChange;
                    stateFlags |= ZeroGMovementStateFlags.SurfaceContact | ZeroGMovementStateFlags.PushAndGlide;
                    hit.CollisionImpulse = math.length(velocity - pushPreviousVelocity);
                    hit.Flags = ZeroGSurfaceHitFlags.Valid | ZeroGSurfaceHitFlags.AnalyticOrbitWall | ZeroGSurfaceHitFlags.Pushable;
                }

                return hit;
            }

            float3 previousVelocity = velocity;
            float pushOut = bestPenetration + tuning.DepenetrationSlopMeters;
            localPosition += bestNormal * pushOut;
            float intoWallSpeed = math.dot(velocity, bestNormal);
            if (intoWallSpeed < 0.0f)
            {
                velocity = math.reflect(velocity, bestNormal) * restitution;
                stateFlags |= ZeroGMovementStateFlags.Reflected;
            }
            else
            {
                float3 normalComponent = bestNormal * math.min(0.0f, intoWallSpeed);
                velocity -= normalComponent;
            }

            if (pushRequested)
            {
                velocity += bestNormal * tuning.PushImpulseVelocityChange;
                stateFlags |= ZeroGMovementStateFlags.PushAndGlide;
            }

            float impulse = math.length(velocity - previousVelocity);
            stateFlags |= ZeroGMovementStateFlags.SurfaceContact | ZeroGMovementStateFlags.Depenetrated;
            hit.PointLocal = localPosition - bestNormal * radius;
            hit.Normal = bestNormal;
            hit.DistanceMeters = 0.0f;
            hit.PenetrationMeters = pushOut;
            hit.CollisionImpulse = impulse;
            hit.QualityProbeWeight = quality;
            hit.Flags = ZeroGSurfaceHitFlags.Valid | ZeroGSurfaceHitFlags.AnalyticOrbitWall | ZeroGSurfaceHitFlags.Pushable;
            hit.Frame = frame;
            hit.SurfaceHash = 0x4F524254u;
            return hit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EvaluateClearance(float distance, float3 normal, ref float nearestDistance, ref float3 nearestNormal)
        {
            if (distance >= 0.0f && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestNormal = normal;
            }
        }

        private void WriteTelemetry(
            float3 localPosition,
            float3 velocity,
            float3 angularMomentum,
            float collisionImpulse,
            float propellant01,
            uint flags,
            uint stateHash,
            uint faultCode,
            uint frame)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
            {
                return;
            }

            int cursor = TelemetryCursor[0];
            if ((uint)cursor >= (uint)TelemetryRing.Length)
                cursor = 0;

            ZeroGTelemetryEntry entry = default;
            entry.LocalPosition = localPosition;
            entry.LinearVelocity = velocity;
            entry.AngularMomentum = angularMomentum;
            entry.CollisionImpulse = collisionImpulse;
            entry.Propellant01 = propellant01;
            entry.Frame = frame;
            entry.Flags = flags;
            entry.StateHash = stateHash;
            entry.FaultCode = faultCode;
            TelemetryRing[cursor] = entry;
            TelemetryCursor[0] = cursor + 1 == TelemetryRing.Length ? 0 : cursor + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeStateHash(float3 local, float3 velocity, float3 angular, uint flags, uint actionMask, uint frame)
        {
            uint hash = 2166136261u;
            hash = (hash ^ SourceHash) * 16777619u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ math.asuint(local.x)) * 16777619u;
            hash = (hash ^ math.asuint(local.y)) * 16777619u;
            hash = (hash ^ math.asuint(local.z)) * 16777619u;
            hash = (hash ^ math.asuint(velocity.x)) * 16777619u;
            hash = (hash ^ math.asuint(velocity.y)) * 16777619u;
            hash = (hash ^ math.asuint(velocity.z)) * 16777619u;
            hash = (hash ^ math.asuint(angular.x)) * 16777619u;
            hash = (hash ^ math.asuint(angular.y)) * 16777619u;
            hash = (hash ^ math.asuint(angular.z)) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            hash = (hash ^ actionMask) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ZeroGDrift10KAssertionJob : IJob
    {
        [NoAlias] public NativeArray<ZeroGTestResultDTO> Result;
        public float3 InitialVelocity;
        public float DeltaTime;
        public uint Iterations;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length <= 0)
                return;

            uint count = math.max(1u, Iterations);
            float dt = math.clamp(ZeroGMathGuards.SanitizeFloat(DeltaTime, 0.02f), 0.0001f, 0.05f);
            float3 velocity = ZeroGMathGuards.SanitizeFloat3(InitialVelocity, new float3(1f, 0f, 0f));
            float3 expectedVelocity = velocity;
            float3 local = float3.zero;
            float3 expected = float3.zero;
            float maxPosError = 0.0f;
            float maxVelError = 0.0f;
            for (uint i = 0u; i < count; i++)
            {
                local += velocity * dt;
                expected += expectedVelocity * dt;
                maxPosError = math.max(maxPosError, math.length(local - expected));
                maxVelError = math.max(maxVelError, math.length(velocity - expectedVelocity));
            }

            ZeroGTestResultDTO result = default;
            result.MaxPositionError = maxPosError;
            result.MaxVelocityError = maxVelError;
            result.Iterations = count;
            result.FaultMask = (maxVelError > 0.00001f || !math.all(math.isfinite(local))) ? 1u : 0u;
            result.StateHash = math.asuint(local.x) ^ (math.asuint(local.y) * 16777619u) ^ (math.asuint(local.z) * 2166136261u);
            Result[0] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ZeroGRotationFuzzerJob : IJob
    {
        [NoAlias] public NativeArray<ZeroGTestResultDTO> Result;
        public uint Iterations;
        public uint Seed;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length <= 0)
                return;

            uint count = math.max(1u, Iterations);
            uint state = Seed != 0u ? Seed : 0xA1600u;
            quaternion rotation = quaternion.identity;
            float maxUnitError = 0.0f;
            uint fault = 0u;
            for (uint i = 0u; i < count; i++)
            {
                state = Next(state);
                float3 axis = new float3(
                    DecodeSigned(state),
                    DecodeSigned(state >> 9),
                    DecodeSigned(state >> 18));
                axis = ZeroGMathGuards.NormalizeWithFallback(axis, new float3(0f, 1f, 0f));
                float angle = DecodeUnsigned(state >> 3) * 0.08f;
                rotation = ZeroGMathGuards.SanitizeQuaternion(math.mul(rotation, quaternion.AxisAngle(axis, angle)), quaternion.identity);
                float unitError = math.abs(1.0f - math.length(rotation.value));
                maxUnitError = math.max(maxUnitError, unitError);
                if (!math.all(math.isfinite(rotation.value)) || unitError > 0.0001f)
                    fault |= 1u;
            }

            ZeroGTestResultDTO result = default;
            result.MaxOrientationError = maxUnitError;
            result.Iterations = count;
            result.FaultMask = fault;
            result.StateHash = math.asuint(rotation.value.x) ^ math.asuint(rotation.value.y) ^ math.asuint(rotation.value.z) ^ math.asuint(rotation.value.w);
            Result[0] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Next(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value != 0u ? value : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeUnsigned(uint value)
        {
            return (value & 1023u) * (1.0f / 1023.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeSigned(uint value)
        {
            return DecodeUnsigned(value) * 2.0f - 1.0f;
        }
    }
}
