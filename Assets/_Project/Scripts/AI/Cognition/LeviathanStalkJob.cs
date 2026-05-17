using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Burst tangent-orbit steering kernel for Alpha Leviathan stalking.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LeviathanStalkJob : IJobParallelFor
    {
        public NativeArray<AlphaLeviathanCognitionState> States;
        [ReadOnly] public NativeArray<AlphaLeviathanSensoryStimulus> SensoryStimuli;
        public NativeArray<AlphaLeviathanSteeringOutput> SteeringOutputs;
        [NativeDisableParallelForRestriction]
        public NativeArray<AlphaLeviathanTelemetryEntry> TelemetryRing;
        public uint Frame;

        /// <inheritdoc />
        public void Execute(int index)
        {
            AlphaLeviathanCognitionState state = States[index];
            AlphaLeviathanSensoryStimulus stimulus = SensoryStimuli[index];

            bool active = (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.Active) != 0u;
            bool hasPlayerAnchor = (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HasPlayerAnchor) != 0u;
            float sonarPingAge = math.max(0f, math.select(float.MaxValue, stimulus.SonarPingAgeSeconds, math.isfinite(stimulus.SonarPingAgeSeconds)));
            float sonarPingIntensity = math.saturate(math.select(0f, stimulus.SonarPingIntensity01, math.isfinite(stimulus.SonarPingIntensity01)));
            bool sonarActive =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HasSonarPing) != 0u &
                sonarPingAge <= AlphaLeviathanStalkConstants.SonarLureHoldSeconds &
                sonarPingIntensity > AlphaLeviathanStalkConstants.DirectionEpsilon;
            bool hasTrackingAnchor = hasPlayerAnchor | sonarActive;
            bool eligibleToAct = active & hasTrackingAnchor;
            float systemStress = math.saturate(math.select(0f, stimulus.SystemStress01, math.isfinite(stimulus.SystemStress01)));
            bool lowTier =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.MathLodLow) != 0u |
                systemStress > 0.8f;
            bool shiftFenceActive = (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.ShiftFenceActive) != 0u;
            bool shiftChanged = stimulus.ObservedShiftFrameId != state.LastShiftFrameId;
            AlphaLeviathanAup rawAnchor = SelectAup(stimulus.PlayerAup, stimulus.PingAup, sonarActive);
            bool anchorAupFinite = IsAupLocalFinite(in rawAnchor);
            bool leviathanAupFinite = IsAupLocalFinite(in state.LeviathanAup);
            AlphaLeviathanAup anchor = SanitizeAupLocal(in rawAnchor);
            AlphaLeviathanAup leviathanAup = SanitizeAupLocal(in state.LeviathanAup);
            ushort slotId = (ushort)math.min(index, (int)ushort.MaxValue);
            bool invalidPersistedState =
                !math.isfinite(state.AgressionLevel01) |
                !math.isfinite(state.PhaseStartSeconds) |
                !math.all(math.isfinite(state.Forward)) |
                !math.all(math.isfinite(state.PreviousSteeringDirection));
            float3 stateForward = NormalizeSafe(state.Forward, new float3(0f, 0f, 1f));
            float3 statePrevious = NormalizeSafe(state.PreviousSteeringDirection, stateForward);
            float stateAggression = math.saturate(math.select(0f, state.AgressionLevel01, math.isfinite(state.AgressionLevel01)));
            float statePhaseStartSeconds = math.max(0f, math.select(0f, state.PhaseStartSeconds, math.isfinite(state.PhaseStartSeconds)));
            float currentTimeSeconds = math.max(0f, math.select(statePhaseStartSeconds, stimulus.CurrentTimeSeconds, math.isfinite(stimulus.CurrentTimeSeconds)));

            double3 anchorAbsolute = anchor.ToAbsoluteDouble3();
            double3 leviathanAbsolute = leviathanAup.ToAbsoluteDouble3();
            double3 toAnchorDouble = anchorAbsolute - leviathanAbsolute;
            double distanceSqDouble = math.lengthsq(toAnchorDouble);
            bool finiteDelta = math.isfinite(distanceSqDouble);
            bool separatedDelta = distanceSqDouble > AlphaLeviathanStalkConstants.DoubleDirectionEpsilon;
            bool validDelta = finiteDelta & separatedDelta;
            bool invalidAupInput = !finiteDelta | !anchorAupFinite | !leviathanAupFinite;
            bool faultedInput = invalidAupInput | invalidPersistedState;
            bool safeToAct = eligibleToAct & !faultedInput;
            bool highTierSdf =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HighTierSdfContour) != 0u &
                !lowTier &
                safeToAct;

            float3 toAnchor = NormalizeDouble3(toAnchorDouble, new float3(0f, 1f, 0f), validDelta);
            float3 awayFromAnchor = -toAnchor;
            float3 playerForward = NormalizeSafe(stimulus.PlayerForward, toAnchor);
            float playerGazeDot = math.saturate(math.dot(playerForward, awayFromAnchor));
            bool playerGazeBreak = safeToAct & playerGazeDot > AlphaLeviathanStalkConstants.PlayerGazeBreakDot;
            float3 tangentFallback = NormalizeSafe(math.cross(new float3(0f, 0f, 1f), toAnchor), new float3(1f, 0f, 0f));
            float3 tangent = NormalizeSafe(math.cross(new float3(0f, 1f, 0f), toAnchor), tangentFallback);

            double safeDistanceSq = math.select(
                0d,
                math.max(distanceSqDouble, 0d),
                finiteDelta);
            double distanceMetersDouble = safeDistanceSq * math.rsqrt(math.max(safeDistanceSq, AlphaLeviathanStalkConstants.DoubleDirectionEpsilon));
            float finiteDistanceMeters = (float)math.min(distanceMetersDouble, (double)float.MaxValue);
            float distanceMeters = math.select(
                AlphaLeviathanStalkConstants.MinimumFogRingMeters,
                finiteDistanceMeters,
                finiteDelta & math.isfinite(distanceMetersDouble));

            float fogDistance = math.min(
                SanitizePositive(stimulus.FogDistanceMeters, AlphaLeviathanStalkConstants.MinimumFogRingMeters + AlphaLeviathanStalkConstants.FogEdgeOffsetMeters),
                AlphaLeviathanStalkConstants.MaxFogDistanceMeters);
            float ringDistance = math.max(
                AlphaLeviathanStalkConstants.MinimumFogRingMeters,
                fogDistance - AlphaLeviathanStalkConstants.FogEdgeOffsetMeters);
            float inverseRing = math.rcp(math.max(ringDistance, AlphaLeviathanStalkConstants.DirectionEpsilon));
            float radialCorrection = math.clamp((distanceMeters - ringDistance) * inverseRing, -1f, 1f);
            float3 radialSteer = toAnchor * radialCorrection;
            float3 sdfContour = NormalizeSafe(math.cross(new float3(0f, 1f, 0f), stimulus.SdfGradient), tangent);
            float sdfWeight = math.select(0f, AlphaLeviathanStalkConstants.HighTierSdfContourWeight, highTierSdf);
            float3 orbitSteer = NormalizeSafe(math.lerp(tangent + radialSteer, sdfContour + radialSteer, sdfWeight), tangent);

            float3 retreatSteer = NormalizeSafe(awayFromAnchor + new float3(0f, -0.2f, 0f), awayFromAnchor);
            float3 chargeSteer = toAnchor;
            float playerNoise = math.saturate(math.select(0f, stimulus.PlayerNoise01, math.isfinite(stimulus.PlayerNoise01)));
            float noiseThreshold = math.saturate(math.select(1f, stimulus.NoiseThreshold01, math.isfinite(stimulus.NoiseThreshold01)));
            float deltaTime = math.min(SanitizePositive(stimulus.DeltaTime, 0f), AlphaLeviathanStalkConstants.MaxDeltaTimeSeconds);
            float aggressionDelta = math.select(
                0f,
                deltaTime * AlphaLeviathanStalkConstants.NoiseAggressionGainPerSecond,
                safeToAct & playerNoise > noiseThreshold);
            float aggression = math.saturate(stateAggression + aggressionDelta);
            float headlightDot = math.saturate(math.select(0f, stimulus.HeadlightDot, math.isfinite(stimulus.HeadlightDot)));
            bool lightRetreat = safeToAct & headlightDot > AlphaLeviathanStalkConstants.LightRetreatDot;
            bool charge = safeToAct &
                          aggression > AlphaLeviathanStalkConstants.ChargeAggressionThreshold &
                          distanceMeters <= ringDistance + 12f &
                          !lightRetreat;

            int phase = AlphaLeviathanStalkPhase.Circle;
            phase = math.select(phase, AlphaLeviathanStalkPhase.Idle, !safeToAct);
            phase = math.select(phase, AlphaLeviathanStalkPhase.Charge, charge);
            phase = math.select(phase, AlphaLeviathanStalkPhase.Retreat, lightRetreat);
            bool phaseChanged = state.CurrentPhase != (byte)phase;

            float3 phaseSteer = orbitSteer;
            phaseSteer = math.select(phaseSteer, chargeSteer, phase == AlphaLeviathanStalkPhase.Charge);
            phaseSteer = math.select(phaseSteer, retreatSteer, phase == AlphaLeviathanStalkPhase.Retreat);
            float3 idleSteer = stateForward;
            phaseSteer = math.select(phaseSteer, idleSteer, phase == AlphaLeviathanStalkPhase.Idle);
            phaseSteer = math.select(phaseSteer, toAnchor, safeToAct & finiteDelta & !separatedDelta);
            phaseSteer = NormalizeSafe(phaseSteer, NormalizeSafe(stateForward, tangent));

            float3 previous = NormalizeSafe(statePrevious, phaseSteer);
            previous = math.select(previous, phaseSteer, shiftChanged);
            float blend = math.select(AlphaLeviathanStalkConstants.HighTierSteeringBlend, AlphaLeviathanStalkConstants.LowTierSteeringBlend, lowTier);
            float3 desiredDirection = NormalizeSafe(math.lerp(previous, phaseSteer, blend), phaseSteer);
            float activeIntent = math.select(0f, 1f, safeToAct);
            float reportedAggression = math.select(0f, aggression, safeToAct);
            float reportedDistanceMeters = math.select(0f, distanceMeters, safeToAct);
            float reportedRingDistance = math.select(0f, ringDistance, safeToAct);
            float3 outputDirection = math.select(float3.zero, desiredDirection, safeToAct);

            byte flags = 0;
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.LowTierRadialFallback, lowTier & safeToAct);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.SdfDiveRequested, highTierSdf);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.PlayerGazeBreak, playerGazeBreak);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.LightRetreat, lightRetreat);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.ShiftFenceReset, shiftChanged | shiftFenceActive);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.Fault, faultedInput & eligibleToAct);
            byte intentFlags = 0;
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.LowTierRadialFallback, lowTier & safeToAct);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.SdfContourRequested, highTierSdf);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.PlayerGazeBreak, playerGazeBreak);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.AcousticLure, safeToAct & sonarActive);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.LightRetreat, lightRetreat);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.ShiftFenceActive, shiftChanged | shiftFenceActive);
            intentFlags = (byte)math.select(intentFlags, intentFlags | AlphaLeviathanSteeringIntentFlags.FaultedInput, faultedInput & eligibleToAct);

            uint stateHash = BuildStateHash(index, (byte)phase, flags, aggression, Frame);
            state.LeviathanAup = leviathanAup;
            state.TargetAnchorAup = SelectAup(state.TargetAnchorAup, anchor, safeToAct);
            state.PreviousSteeringDirection = math.select(statePrevious, desiredDirection, safeToAct | shiftChanged);
            state.Forward = math.select(stateForward, desiredDirection, safeToAct | shiftChanged);
            state.AgressionLevel01 = aggression;
            state.PhaseStartSeconds = math.select(statePhaseStartSeconds, currentTimeSeconds, phaseChanged);
            state.CurrentPhase = (byte)phase;
            state.Flags = flags;
            state.LastShiftFrameId = stimulus.ObservedShiftFrameId;
            state.StateHash = stateHash;
            state.Slot = slotId;
            state.Reserved0 = 0u;
            States[index] = state;

            float3 targetOffset = math.select(float3.zero, toAnchor * ringDistance, safeToAct);
            float wakeSiltIntensity = math.saturate(
                (math.abs(radialCorrection) * 0.35f +
                 math.select(0.08f, 0.85f, phase == AlphaLeviathanStalkPhase.Charge) +
                 math.select(0f, 0.25f, highTierSdf)) * activeIntent);
            float visualOverkill = math.select(
                0f,
                AlphaLeviathanStalkConstants.HighTierVisualOverkill01,
                highTierSdf);
            float recommendedCadence = math.select(
                AlphaLeviathanStalkConstants.HighTierCadenceSeconds,
                AlphaLeviathanStalkConstants.LowTierCadenceSeconds,
                lowTier);
            recommendedCadence = math.select(0f, recommendedCadence, safeToAct);
            float charge01 = math.select(0f, 1f, phase == AlphaLeviathanStalkPhase.Charge);
            float saltGrowth = math.select(0f, math.select(0.03f, math.saturate(0.25f + aggression * 0.55f + sdfWeight * 0.2f), highTierSdf), safeToAct);
            float dentImpulse = math.saturate(charge01 * (aggression * 0.75f + math.abs(radialCorrection) * 0.25f));
            float sssPulse = math.saturate((0.05f + charge01 * 0.8f + math.select(0f, 0.15f, highTierSdf)) * activeIntent);
            float particleBudget = math.select(0f, math.select(0.18f, 1f, highTierSdf), safeToAct);
            float triangleNoise = Triangle01(((float)((Frame + (uint)(index * 17)) & 1023u)) * AlphaLeviathanStalkConstants.TriangleNoiseInvPeriod + aggression);
            float silhouetteNoise = math.select(0f, math.select(triangleNoise * 0.2f, triangleNoise, lowTier), safeToAct);
            SteeringOutputs[index] = new AlphaLeviathanSteeringOutput
            {
                DesiredDirection = outputDirection,
                TargetRuntimeOffsetMeters = targetOffset,
                DesiredRingDistanceMeters = reportedRingDistance,
                DistanceToAnchorMeters = reportedDistanceMeters,
                BioluminescenceIntensity = math.select(0f, math.select(0.05f, 10f, phase == AlphaLeviathanStalkPhase.Charge), safeToAct),
                AgressionLevel01 = reportedAggression,
                StateHash = stateHash,
                SdfContourWeight01 = sdfWeight,
                WakeSiltIntensity01 = wakeSiltIntensity,
                VisualOverkill01 = visualOverkill,
                RecommendedCadenceSeconds = recommendedCadence,
                VisorSaltCrystalGrowth01 = saltGrowth,
                HullDentImpulse01 = dentImpulse,
                SubsurfaceScatterPulse01 = sssPulse,
                ParticleOverkillBudget01 = particleBudget,
                PredatorSilhouetteNoise01 = silhouetteNoise,
                Slot = slotId,
                CurrentPhase = (byte)phase,
                Flags = flags,
                IntentFlags = intentFlags,
            };

            int telemetrySlot = math.min(index, AlphaLeviathanStalkConstants.MaxLeviathanSlots - 1);
            int telemetryFrame = (int)(Frame % AlphaLeviathanStalkConstants.TelemetryFrames);
            int telemetryIndex = (telemetryFrame * AlphaLeviathanStalkConstants.MaxLeviathanSlots) + telemetrySlot;
            TelemetryRing[telemetryIndex] = new AlphaLeviathanTelemetryEntry
            {
                Frame = Frame,
                Slot = slotId,
                Phase = (byte)phase,
                Flags = flags,
                DistanceToPlayerMeters = reportedDistanceMeters,
                FogRingDistanceMeters = reportedRingDistance,
                Position = SanitizeTelemetryPosition(leviathanAbsolute),
                PlayerPosition = SanitizeTelemetryPosition(anchorAbsolute),
                DesiredDirection = outputDirection,
                StateHash = stateHash,
                LeviathanAgressivity01 = reportedAggression,
                Reserved1 = stimulus.ObservedShiftFrameId
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAupLocalFinite(in AlphaLeviathanAup value)
        {
            return math.all(math.isfinite(value.Local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlphaLeviathanAup SanitizeAupLocal(in AlphaLeviathanAup value)
        {
            AlphaLeviathanAup sanitized = value;
            sanitized.Local = math.select(float4.zero, value.Local, math.isfinite(value.Local));
            return sanitized;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlphaLeviathanAup SelectAup(in AlphaLeviathanAup player, in AlphaLeviathanAup ping, bool usePing)
        {
            long signedMask = math.select(0L, -1L, usePing);
            ulong unsignedMask = (ulong)signedMask;
            AlphaLeviathanAup selected = default;
            selected.GridX = SelectLong(player.GridX, ping.GridX, signedMask);
            selected.GridY = SelectLong(player.GridY, ping.GridY, signedMask);
            selected.GridZ = SelectLong(player.GridZ, ping.GridZ, signedMask);
            selected.Local = math.select(player.Local, ping.Local, usePing);
            selected.Reserved = (player.Reserved & ~unsignedMask) | (ping.Reserved & unsignedMask);
            return selected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) & value > 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Triangle01(float value)
        {
            return math.saturate(math.abs(math.frac(value) - 0.5f) * 2f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeDouble3(double3 value, float3 fallback, bool valid)
        {
            double distanceSq = math.lengthsq(value);
            double safeDistanceSq = math.select(
                AlphaLeviathanStalkConstants.DoubleDirectionEpsilon,
                distanceSq,
                math.isfinite(distanceSq) & distanceSq > AlphaLeviathanStalkConstants.DoubleDirectionEpsilon);
            double inverseLength = math.rsqrt(safeDistanceSq);
            float3 normalized = new float3(
                (float)(value.x * inverseLength),
                (float)(value.y * inverseLength),
                (float)(value.z * inverseLength));
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & lengthSq > AlphaLeviathanStalkConstants.DirectionEpsilon;
            float safeLengthSq = math.select(AlphaLeviathanStalkConstants.DirectionEpsilon, lengthSq, valid);
            float inverseLength = math.rsqrt(safeLengthSq);
            float3 normalized = value * inverseLength;
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SelectLong(long player, long ping, long signedMask)
        {
            return (player & ~signedMask) | (ping & signedMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeTelemetryPosition(double3 value)
        {
            float3 telemetryPosition = new float3(
                (float)math.clamp(value.x, (double)-float.MaxValue, (double)float.MaxValue),
                (float)math.clamp(value.y, (double)-float.MaxValue, (double)float.MaxValue),
                (float)math.clamp(value.z, (double)-float.MaxValue, (double)float.MaxValue));
            return math.select(float3.zero, telemetryPosition, math.all(math.isfinite(telemetryPosition)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildStateHash(int index, byte phase, byte flags, float aggression, uint frame)
        {
            uint hash = 0xA11EADu;
            hash ^= (uint)(index + 1) * 0x9E3779B9u;
            hash = (hash << 5) | (hash >> 27);
            hash ^= (uint)phase * 0x85EBCA6Bu;
            hash = (hash << 7) | (hash >> 25);
            hash ^= (uint)flags * 0xC2B2AE35u;
            hash ^= (uint)math.asint(math.saturate(aggression)) * 0x27D4EB2Du;
            hash ^= frame * 0x165667B1u;
            return math.select(0xA11EADu, hash, hash != 0u);
        }
    }
}
