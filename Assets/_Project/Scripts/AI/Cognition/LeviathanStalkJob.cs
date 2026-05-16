using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Burst tangent-orbit steering kernel for Alpha Leviathan stalking.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct LeviathanStalkJob : IJobParallelFor
    {
        public NativeArray<AlphaLeviathanCognitionState> States;
        [ReadOnly] public NativeArray<AlphaLeviathanSensoryStimulus> SensoryStimuli;
        public NativeArray<AlphaLeviathanSteeringOutput> SteeringOutputs;
        public NativeArray<AlphaLeviathanTelemetryEntry> TelemetryRing;
        public uint Frame;

        /// <inheritdoc />
        public void Execute(int index)
        {
            AlphaLeviathanCognitionState state = States[index];
            AlphaLeviathanSensoryStimulus stimulus = SensoryStimuli[index];

            bool active = (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.Active) != 0u;
            bool hasPlayerAnchor = (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HasPlayerAnchor) != 0u;
            bool sonarActive =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HasSonarPing) != 0u &
                stimulus.SonarPingAgeSeconds <= AlphaLeviathanStalkConstants.SonarLureHoldSeconds &
                stimulus.SonarPingIntensity01 > AlphaLeviathanStalkConstants.DirectionEpsilon;
            bool hasTrackingAnchor = hasPlayerAnchor | sonarActive;
            float systemStress = math.saturate(math.select(0f, stimulus.SystemStress01, math.isfinite(stimulus.SystemStress01)));
            bool lowTier =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.MathLodLow) != 0u |
                systemStress > 0.8f;
            bool highTierSdf =
                (stimulus.RuntimeFlags & AlphaLeviathanStalkRuntimeFlags.HighTierSdfContour) != 0u &
                !lowTier;
            bool shiftChanged = stimulus.ObservedShiftFrameId != state.LastShiftFrameId;
            ushort slotId = (ushort)math.min(index, (int)ushort.MaxValue);

            AlphaLeviathanAup anchor = SelectAup(stimulus.PlayerAup, stimulus.PingAup, sonarActive);
            double3 anchorAbsolute = anchor.ToAbsoluteDouble3();
            double3 leviathanAbsolute = state.LeviathanAup.ToAbsoluteDouble3();
            double3 toAnchorDouble = anchorAbsolute - leviathanAbsolute;
            double distanceSqDouble = math.lengthsq(toAnchorDouble);
            bool validDelta = math.isfinite(distanceSqDouble) &
                              distanceSqDouble > AlphaLeviathanStalkConstants.DoubleDirectionEpsilon;

            float3 toAnchor = NormalizeDouble3(toAnchorDouble, new float3(0f, 1f, 0f), validDelta);
            float3 awayFromAnchor = -toAnchor;
            float3 tangentFallback = NormalizeSafe(math.cross(new float3(0f, 0f, 1f), toAnchor), new float3(1f, 0f, 0f));
            float3 tangent = NormalizeSafe(math.cross(new float3(0f, 1f, 0f), toAnchor), tangentFallback);

            double safeDistanceSq = math.select(
                AlphaLeviathanStalkConstants.DoubleDirectionEpsilon,
                math.max(distanceSqDouble, AlphaLeviathanStalkConstants.DoubleDirectionEpsilon),
                math.isfinite(distanceSqDouble));
            double distanceMetersDouble = safeDistanceSq * math.rsqrt(safeDistanceSq);
            float finiteDistanceMeters = (float)math.min(distanceMetersDouble, (double)float.MaxValue);
            float distanceMeters = math.select(
                AlphaLeviathanStalkConstants.MinimumFogRingMeters,
                finiteDistanceMeters,
                validDelta & math.isfinite(distanceMetersDouble));

            float fogDistance = SanitizePositive(stimulus.FogDistanceMeters, AlphaLeviathanStalkConstants.MinimumFogRingMeters + AlphaLeviathanStalkConstants.FogEdgeOffsetMeters);
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
            float aggressionDelta = math.select(
                0f,
                SanitizePositive(stimulus.DeltaTime, 0f) * AlphaLeviathanStalkConstants.NoiseAggressionGainPerSecond,
                stimulus.PlayerNoise01 > stimulus.NoiseThreshold01);
            float aggression = math.saturate(state.AgressionLevel01 + aggressionDelta);
            bool lightRetreat = stimulus.HeadlightDot > AlphaLeviathanStalkConstants.LightRetreatDot;
            bool charge = aggression > AlphaLeviathanStalkConstants.ChargeAggressionThreshold &
                          distanceMeters <= ringDistance + 12f &
                          !lightRetreat;

            int phase = AlphaLeviathanStalkPhase.Circle;
            phase = math.select(phase, AlphaLeviathanStalkPhase.Idle, !active | !hasTrackingAnchor);
            phase = math.select(phase, AlphaLeviathanStalkPhase.Charge, charge);
            phase = math.select(phase, AlphaLeviathanStalkPhase.Retreat, lightRetreat);

            float3 phaseSteer = orbitSteer;
            phaseSteer = math.select(phaseSteer, chargeSteer, phase == AlphaLeviathanStalkPhase.Charge);
            phaseSteer = math.select(phaseSteer, retreatSteer, phase == AlphaLeviathanStalkPhase.Retreat);
            phaseSteer = math.select(phaseSteer, state.Forward, phase == AlphaLeviathanStalkPhase.Idle);
            phaseSteer = NormalizeSafe(phaseSteer, NormalizeSafe(state.Forward, tangent));

            float3 previous = NormalizeSafe(state.PreviousSteeringDirection, phaseSteer);
            previous = math.select(previous, phaseSteer, shiftChanged);
            float blend = math.select(AlphaLeviathanStalkConstants.HighTierSteeringBlend, AlphaLeviathanStalkConstants.LowTierSteeringBlend, lowTier);
            float3 desiredDirection = NormalizeSafe(math.lerp(previous, phaseSteer, blend), phaseSteer);

            byte flags = 0;
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.LowTierRadialFallback, lowTier);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.SdfDiveRequested, highTierSdf);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.AcousticLure, sonarActive);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.LightRetreat, lightRetreat);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.ShiftFenceReset, shiftChanged);
            flags = (byte)math.select(flags, flags | AlphaLeviathanTelemetryFlags.Fault, !validDelta & active & hasTrackingAnchor);

            uint stateHash = BuildStateHash(index, (byte)phase, flags, aggression, Frame);
            state.TargetAnchorAup = anchor;
            state.PreviousSteeringDirection = desiredDirection;
            state.Forward = desiredDirection;
            state.AgressionLevel01 = aggression;
            state.CurrentPhase = (byte)phase;
            state.Flags = flags;
            state.LastShiftFrameId = stimulus.ObservedShiftFrameId;
            state.StateHash = stateHash;
            state.Slot = slotId;
            state.Reserved0 = 0u;
            States[index] = state;

            float3 targetOffset = toAnchor * ringDistance;
            float wakeSiltIntensity = math.saturate(
                math.abs(radialCorrection) * 0.35f +
                math.select(0.08f, 0.85f, phase == AlphaLeviathanStalkPhase.Charge) +
                math.select(0f, 0.25f, highTierSdf));
            float visualOverkill = math.select(
                0f,
                AlphaLeviathanStalkConstants.HighTierVisualOverkill01,
                highTierSdf);
            float recommendedCadence = math.select(
                AlphaLeviathanStalkConstants.HighTierCadenceSeconds,
                AlphaLeviathanStalkConstants.LowTierCadenceSeconds,
                lowTier);
            float charge01 = math.select(0f, 1f, phase == AlphaLeviathanStalkPhase.Charge);
            float saltGrowth = math.select(0.03f, math.saturate(0.25f + aggression * 0.55f + sdfWeight * 0.2f), highTierSdf);
            float dentImpulse = math.saturate(charge01 * (aggression * 0.75f + math.abs(radialCorrection) * 0.25f));
            float sssPulse = math.saturate(0.05f + charge01 * 0.8f + math.select(0f, 0.15f, highTierSdf));
            float particleBudget = math.select(0.18f, 1f, highTierSdf);
            SteeringOutputs[index] = new AlphaLeviathanSteeringOutput
            {
                DesiredDirection = desiredDirection,
                TargetRuntimeOffsetMeters = targetOffset,
                DesiredRingDistanceMeters = ringDistance,
                DistanceToAnchorMeters = distanceMeters,
                BioluminescenceIntensity = math.select(0.05f, 10f, phase == AlphaLeviathanStalkPhase.Charge),
                AgressionLevel01 = aggression,
                StateHash = stateHash,
                SdfContourWeight01 = sdfWeight,
                WakeSiltIntensity01 = wakeSiltIntensity,
                VisualOverkill01 = visualOverkill,
                RecommendedCadenceSeconds = recommendedCadence,
                VisorSaltCrystalGrowth01 = saltGrowth,
                HullDentImpulse01 = dentImpulse,
                SubsurfaceScatterPulse01 = sssPulse,
                ParticleOverkillBudget01 = particleBudget,
                Slot = slotId,
                CurrentPhase = (byte)phase,
                Flags = flags,
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
                DistanceToPlayerMeters = distanceMeters,
                FogRingDistanceMeters = ringDistance,
                Position = SanitizeTelemetryPosition(leviathanAbsolute),
                PlayerPosition = SanitizeTelemetryPosition(anchorAbsolute),
                DesiredDirection = desiredDirection,
                StateHash = stateHash,
                LeviathanAgressivity01 = aggression,
                Reserved1 = 0u
            };
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
