using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockCognitionLoadJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<CognitionStateDTO> States;
        [NoAlias] public NativeArray<CognitionAupDTO> Aups;
        [NoAlias] public NativeArray<CognitionTargetCandidateDTO> Targets;
        [ReadOnly, NoAlias] public NativeArray<CognitionUtilityTuningDTO> Tuning;
        public uint Frame;

        public void Execute(int index)
        {
            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.ReadTuning(Tuning);
            float quality = UtilityAICognitionJobMath.ResolveQuality(tuning.Runtime.x);
            uint hash = UtilityAICognitionJobMath.Hash(index, Frame ^ UtilityAICognitionConstants.AgentHash);
            float phase = (hash & 1023u) * (1f / 1023f);
            float alt = ((hash >> 10) & 1023u) * (1f / 1023f);

            if ((uint)index < (uint)States.Length)
            {
                CognitionStateDTO state = default;
                state.Hunger01 = math.saturate(phase);
                state.Fear01 = math.saturate(1f - math.abs((alt * 2f) - 1f));
                state.Aggression01 = math.saturate((phase * 0.6f) + (quality * 0.25f));
                state.ActiveActionHash = UtilityAICognitionConstants.ActionPatrolHash;
                state.TargetEntityHash = 0u;
                state.ActionCooldown = UtilityAICognitionJobMath.ResolveTickInterval(quality);
                States[index] = state;
            }

            if ((uint)index < (uint)Aups.Length)
            {
                float radius = math.lerp(60f, 420f, phase);
                float angle = alt * 6.2831855f;
                UtilityAICognitionJobMath.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
                CognitionAupDTO aup = default;
                aup.AUP = new double3(
                    angleCos * radius,
                    -90.0 + ((index & 31) * 3.0),
                    angleSin * radius);
                aup.EntityHash = 0xA1302000u ^ (uint)index;
                aup.Flags = UtilityAICognitionActionFlags.Active | UtilityAICognitionActionFlags.EmergencyMock;
                Aups[index] = aup;
            }

            if ((uint)index < (uint)Targets.Length)
            {
                uint targetHash = UtilityAICognitionJobMath.Hash(index + 4919, Frame ^ 0xDEAD302u);
                float t0 = (targetHash & 2047u) * (1f / 2047f);
                float t1 = ((targetHash >> 11) & 2047u) * (1f / 2047f);
                float angle = t0 * 6.2831855f;
                float radius = math.lerp(24f, 620f, t1);
                UtilityAICognitionJobMath.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);

                CognitionTargetCandidateDTO target = default;
                target.AUP = new double3(
                    angleCos * radius,
                    -110.0 + ((index & 63) * 2.25),
                    angleSin * radius);
                target.EntityHash = 0xC0200000u ^ (uint)index;
                target.SpeciesHash = 0xF00D0000u ^ (uint)(index & 15);
                target.Threat01 = math.saturate(t0);
                target.FoodValue01 = math.saturate(1f - t0);
                target.Weakness01 = math.saturate(t1);
                target.Noise01 = math.saturate((t0 + t1) * 0.5f);
                target.Flags = UtilityAICognitionActionFlags.Active | UtilityAICognitionActionFlags.EmergencyMock;
                target.SpatialHash = UtilityAICognitionJobMath.HashAupCell(target.AUP, tuning.DistanceMeters.z, UtilityAICognitionConstants.TargetBucketCount);
                Targets[index] = target;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCognitionTargetBucketsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CognitionTargetCandidateDTO> Targets;
        [ReadOnly, NoAlias] public NativeArray<CognitionUtilityTuningDTO> Tuning;
        [NoAlias] public NativeArray<int> BucketHeads;
        [NoAlias] public NativeArray<int> TargetNext;
        public int TargetCount;

        public void Execute()
        {
            bool invalid = !Targets.IsCreated | !BucketHeads.IsCreated | !TargetNext.IsCreated;
            if (invalid)
                return;

            for (int i = 0; i < BucketHeads.Length; i++)
                BucketHeads[i] = -1;

            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.ReadTuning(Tuning);
            float cellSize = UtilityAICognitionJobMath.SanitizePositive(tuning.DistanceMeters.z, 48f);
            int count = math.min(TargetCount, math.min(Targets.Length, TargetNext.Length));
            for (int i = 0; i < count; i++)
            {
                CognitionTargetCandidateDTO target = Targets[i];
                uint bucketHash = UtilityAICognitionJobMath.HashAupCell(target.AUP, cellSize, BucketHeads.Length);
                int bucket = (int)bucketHash;
                TargetNext[i] = BucketHeads[bucket];
                BucketHeads[bucket] = i;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct IntegrateCognitionSensoryInputJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<CognitionStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<CognitionAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<CognitionMovementAcousticSignalDTO>.ReadOnly MovementSignals;
        [ReadOnly, NoAlias] public NativeArray<CognitionCombatDamageSignalDTO>.ReadOnly DamageSignals;
        [ReadOnly, NoAlias] public NativeArray<CognitionUtilityTuningDTO> Tuning;
        public int MovementSignalCount;
        public int DamageSignalCount;
        public float DeltaSeconds;

        public void Execute(int index)
        {
            bool invalid = !States.IsCreated |
                           !Aups.IsCreated |
                           (uint)index >= (uint)States.Length |
                           (uint)index >= (uint)Aups.Length;
            if (invalid)
                return;

            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.ReadTuning(Tuning);
            float quality = UtilityAICognitionJobMath.ResolveQuality(tuning.Runtime.x);
            float dt = UtilityAICognitionJobMath.SanitizePositive(DeltaSeconds, tuning.Runtime.y);
            float acousticGain = UtilityAICognitionJobMath.SanitizeNonNegative(tuning.SignalGains.x, 0.35f);
            float damageFearGain = UtilityAICognitionJobMath.SanitizeNonNegative(tuning.SignalGains.y, 0.55f);
            float hungerGain = UtilityAICognitionJobMath.SanitizeNonNegative(tuning.SignalGains.z, 0.035f);
            float aggressionDamageGain = UtilityAICognitionJobMath.SanitizeNonNegative(tuning.SignalGains.w, 0.4f);
            float threatRadius = UtilityAICognitionJobMath.SanitizePositive(tuning.DistanceMeters.x, 220f);
            float threatRadiusSq = threatRadius * threatRadius;
            int signalLimit = UtilityAICognitionJobMath.ResolveSignalTapLimit(quality, 8, 64);

            CognitionStateDTO state = States[index];
            CognitionAupDTO aup = Aups[index];
            double3 selfAup = aup.AUP;
            bool selfFinite = math.all(math.isfinite(selfAup));

            float acousticFear = 0f;
            int movementCount = math.select(0, math.min(math.min(MovementSignalCount, MovementSignals.Length), signalLimit), MovementSignals.IsCreated);
            for (int i = 0; i < movementCount; i++)
            {
                CognitionMovementAcousticSignalDTO signal = MovementSignals[i];
                double3 signalAup = signal.PositionAup;
                double distanceSqD = AupPrecisionMath.DistanceSqSafeDouble(signalAup, selfAup);
                bool distanceFinite = math.isfinite(distanceSqD) & distanceSqD >= 0d;
                float distanceSq = math.select(float.MaxValue, (float)math.min(distanceSqD, (double)float.MaxValue), distanceFinite);
                float proximity = math.saturate(1f - (distanceSq * math.rcp(math.max(threatRadiusSq, UtilityAICognitionConstants.Epsilon))));
                float volume = UtilityAICognitionJobMath.SanitizeNonNegative(signal.Volume, 0f);
                float velocity = math.sqrt(UtilityAICognitionJobMath.SanitizeNonNegative(signal.VelocitySq, 0f));
                float valid = math.select(0f, 1f, selfFinite & distanceFinite & math.all(math.isfinite(signalAup)));
                acousticFear = math.max(acousticFear, proximity * (volume + (velocity * 0.05f)) * acousticGain * valid);
            }

            float damageFear = 0f;
            float damageAggression = 0f;
            int damageCount = math.select(0, math.min(math.min(DamageSignalCount, DamageSignals.Length), signalLimit), DamageSignals.IsCreated);
            for (int i = 0; i < damageCount; i++)
            {
                CognitionCombatDamageSignalDTO signal = DamageSignals[i];
                double distanceSqD = AupPrecisionMath.DistanceSqSafeDouble(signal.ImpactAup, selfAup);
                bool distanceFinite = math.isfinite(distanceSqD) & distanceSqD >= 0d;
                float distanceSq = math.select(float.MaxValue, (float)math.min(distanceSqD, (double)float.MaxValue), distanceFinite);
                float proximity = math.saturate(1f - (distanceSq * math.rcp(math.max(threatRadiusSq, UtilityAICognitionConstants.Epsilon))));
                float magnitude = UtilityAICognitionJobMath.SanitizeNonNegative(signal.Magnitude, 0f);
                float normalized = math.saturate(magnitude * 0.0025f);
                float valid = math.select(0f, 1f, selfFinite & distanceFinite & math.all(math.isfinite(signal.ImpactAup)));
                damageFear = math.max(damageFear, proximity * normalized * damageFearGain * valid);
                damageAggression = math.max(damageAggression, proximity * normalized * aggressionDamageGain * valid);
            }

            float fearDecay = math.saturate(1f - (dt * 0.3f));
            float aggressionDecay = math.saturate(1f - (dt * 0.12f));
            state.Hunger01 = math.saturate(UtilityAICognitionJobMath.Sanitize01(state.Hunger01) + (hungerGain * dt));
            state.Fear01 = math.saturate((UtilityAICognitionJobMath.Sanitize01(state.Fear01) * fearDecay) + acousticFear + damageFear);
            state.Aggression01 = math.saturate((UtilityAICognitionJobMath.Sanitize01(state.Aggression01) * aggressionDecay) + damageAggression);
            state.ActionCooldown = math.max(0f, UtilityAICognitionJobMath.SanitizeNonNegative(state.ActionCooldown, 0f) - dt);
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateUtilityCognitionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<CognitionStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<CognitionAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<CognitionTargetCandidateDTO> Targets;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> TargetNext;
        [ReadOnly, NoAlias] public NativeArray<CognitionUtilityTuningDTO> Tuning;
        [NoAlias] public NativeArray<CognitionActionOutputDTO> Outputs;
        public uint Frame;
        public int TargetCount;

        public void Execute(int index)
        {
            if (!CanExecute(index))
                return;

            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.ReadTuning(Tuning);
            float quality = UtilityAICognitionJobMath.ResolveQuality(tuning.Runtime.x);
            float dt = UtilityAICognitionJobMath.SanitizePositive(tuning.Runtime.y, 1f / 30f);
            float tickInterval = UtilityAICognitionJobMath.ResolveTickInterval(quality);
            int candidateBudget = UtilityAICognitionJobMath.ResolveCandidateBudget(quality);
            byte candidateCount = 0;

            CognitionStateDTO state = States[index];
            CognitionAupDTO aup = Aups[index];
            state.Hunger01 = UtilityAICognitionJobMath.Sanitize01(state.Hunger01);
            state.Fear01 = UtilityAICognitionJobMath.Sanitize01(state.Fear01);
            state.Aggression01 = UtilityAICognitionJobMath.Sanitize01(state.Aggression01);
            float cooldown = UtilityAICognitionJobMath.SanitizeNonNegative(state.ActionCooldown, 0f) - dt;
            bool dueTick = cooldown <= 0f;

            float hungerCurve = UtilityAICognitionJobMath.EvaluatePolynomial01(state.Hunger01, tuning.HungerPolynomial);
            float fearCurve = UtilityAICognitionJobMath.EvaluatePolynomial01(state.Fear01, tuning.FearPolynomial);
            float aggressionCurve = UtilityAICognitionJobMath.EvaluatePolynomial01(state.Aggression01, tuning.AggressionPolynomial);

            TargetSelection selection = SelectDearLieTarget(aup.AUP, state, tuning, candidateBudget, ref candidateCount);
            float noTarget = math.select(1f, 0f, selection.TargetHash != 0u);
            float fleeUtility = math.saturate((fearCurve * (1f + selection.Threat01)) + tuning.ActionBiases.x);
            float huntUtility = math.saturate((hungerCurve * 0.58f) + (aggressionCurve * 0.33f) + (selection.Score01 * 0.35f) + tuning.ActionBiases.y - (noTarget * 0.65f));
            float patrolUtility = math.saturate(((1f - math.max(state.Hunger01, state.Fear01)) * math.lerp(0.35f, 0.8f, quality)) + tuning.ActionBiases.z);
            float restUtility = math.saturate(((1f - state.Hunger01) * (1f - state.Fear01) * (1f - state.Aggression01)) + tuning.ActionBiases.w);
            float4 utilities = new float4(fleeUtility, huntUtility, patrolUtility, restUtility);
            bool finiteUtilities = math.all(math.isfinite(utilities)) & math.isfinite(selection.Score01);
            utilities = math.select(float4.zero, utilities, finiteUtilities);

            float bestScore = utilities.w;
            uint selectedAction = UtilityAICognitionConstants.ActionRestHash;
            bool patrolBetter = utilities.z > bestScore;
            bestScore = math.select(bestScore, utilities.z, patrolBetter);
            selectedAction = math.select(selectedAction, UtilityAICognitionConstants.ActionPatrolHash, patrolBetter);
            bool huntBetter = utilities.y > bestScore;
            bestScore = math.select(bestScore, utilities.y, huntBetter);
            selectedAction = math.select(selectedAction, UtilityAICognitionConstants.ActionHuntHash, huntBetter);
            bool fleeBetter = utilities.x > bestScore;
            bestScore = math.select(bestScore, utilities.x, fleeBetter);
            selectedAction = math.select(selectedAction, UtilityAICognitionConstants.ActionFleeHash, fleeBetter);

            bool targetAction = (selectedAction == UtilityAICognitionConstants.ActionHuntHash) | (selectedAction == UtilityAICognitionConstants.ActionFleeHash);
            uint selectedTarget = math.select(0u, selection.TargetHash, targetAction);
            state.ActiveActionHash = math.select(state.ActiveActionHash, selectedAction, dueTick);
            state.TargetEntityHash = math.select(state.TargetEntityHash, selectedTarget, dueTick);
            state.ActionCooldown = math.select(math.max(0f, cooldown), tickInterval, dueTick);
            States[index] = state;

            float3 huntDirection = UtilityAICognitionJobMath.NormalizeSafe(selection.LocalDirection, new float3(0f, 0f, 1f));
            float3 fleeDirection = -huntDirection;
            float3 patrolDirection = UtilityAICognitionJobMath.HashDirection(index, Frame);
            float3 desired = float3.zero;
            desired = math.select(desired, patrolDirection, state.ActiveActionHash == UtilityAICognitionConstants.ActionPatrolHash);
            desired = math.select(desired, huntDirection, state.ActiveActionHash == UtilityAICognitionConstants.ActionHuntHash);
            desired = math.select(desired, fleeDirection, state.ActiveActionHash == UtilityAICognitionConstants.ActionFleeHash);

            byte flags = UtilityAICognitionActionFlags.Active;
            flags = (byte)(flags | math.select(0, UtilityAICognitionActionFlags.DueTick, dueTick));
            flags = (byte)(flags | math.select(0, UtilityAICognitionActionFlags.NoTarget, selection.TargetHash == 0u));
            flags = (byte)(flags | math.select(0, UtilityAICognitionActionFlags.Fault, !finiteUtilities));
            flags = (byte)(flags | math.select(0, UtilityAICognitionActionFlags.ReducedCandidateBudget, candidateBudget < UtilityAICognitionConstants.DearLieCandidateLimit));
            byte qualityWeightQ8 = UtilityAICognitionJobMath.EncodeQualityWeightQ8(quality);

            CognitionActionOutputDTO output = default;
            output.Utilities = utilities;
            output.DesiredLocalDirection = desired;
            output.MaxUtility = bestScore;
            output.ActionHash = state.ActiveActionHash;
            output.TargetEntityHash = state.TargetEntityHash;
            output.StateHash = UtilityAICognitionJobMath.HashState(in state, aup.EntityHash, Frame);
            output.TickIntervalSeconds = tickInterval;
            output.CooldownRemaining = state.ActionCooldown;
            output.Frame = Frame;
            output.Flags = flags;
            output.CandidateCount = candidateCount;
            output.QualityWeightQ8 = qualityWeightQ8;
            Outputs[index] = output;
        }

        private bool CanExecute(int index)
        {
            return States.IsCreated &
                   Aups.IsCreated &
                   Outputs.IsCreated &
                   (uint)index < (uint)States.Length &
                   (uint)index < (uint)Aups.Length &
                   (uint)index < (uint)Outputs.Length;
        }

        private TargetSelection SelectDearLieTarget(double3 selfAup, in CognitionStateDTO state, in CognitionUtilityTuningDTO tuning, int candidateBudget, ref byte candidateCount)
        {
            TargetSelection selection = default;
            selection.Score01 = -1f;

            bool invalid = !Targets.IsCreated |
                           !BucketHeads.IsCreated |
                           !TargetNext.IsCreated |
                           BucketHeads.Length <= 0;
            if (invalid)
                return selection;

            float cellSize = UtilityAICognitionJobMath.SanitizePositive(tuning.DistanceMeters.z, 48f);
            float hungerRadius = UtilityAICognitionJobMath.SanitizePositive(tuning.DistanceMeters.y, 140f);
            float threatRadius = UtilityAICognitionJobMath.SanitizePositive(tuning.DistanceMeters.x, 220f);
            float radius = math.max(hungerRadius, threatRadius);
            float invRadiusSq = math.rcp(math.max(radius * radius, UtilityAICognitionConstants.Epsilon));
            uint bucketHash = UtilityAICognitionJobMath.HashAupCell(selfAup, cellSize, BucketHeads.Length);
            int cursor = BucketHeads[(int)bucketHash];
            int count = math.min(TargetCount, math.min(Targets.Length, TargetNext.Length));

            for (int candidateSlot = 0; candidateSlot < UtilityAICognitionConstants.DearLieCandidateLimit; candidateSlot++)
            {
                bool readable = (candidateSlot < candidateBudget) & (cursor >= 0) & (cursor < count);
                int targetIndex = math.select(0, cursor, readable);
                CognitionTargetCandidateDTO candidate = Targets[targetIndex];
                double3 deltaD = AupPrecisionMath.LocalDeltaDouble(candidate.AUP, selfAup);
                float3 local = AupPrecisionMath.DowncastLocalDeltaClamped(deltaD, tuning.DistanceMeters.w, float3.zero);
                float distanceSq = math.lengthsq(local);
                float proximity = math.saturate(1f - (distanceSq * invRadiusSq));
                float food = UtilityAICognitionJobMath.Sanitize01(candidate.FoodValue01) * state.Hunger01;
                float weakness = UtilityAICognitionJobMath.Sanitize01(candidate.Weakness01) * state.Aggression01;
                float threat = UtilityAICognitionJobMath.Sanitize01(candidate.Threat01);
                float score = math.saturate((food * 0.55f) + (weakness * 0.35f) + (proximity * 0.2f) - (threat * state.Fear01 * 0.25f));
                bool finite = math.isfinite(score) & math.isfinite(distanceSq) & math.all(math.isfinite(candidate.AUP));
                bool better = readable & finite & score > selection.Score01;
                selection.Score01 = math.select(selection.Score01, score, better);
                selection.TargetHash = math.select(selection.TargetHash, candidate.EntityHash, better);
                selection.LocalDirection = math.select(selection.LocalDirection, local, better);
                selection.Threat01 = math.select(selection.Threat01, threat, better);
                candidateCount = (byte)(candidateCount + math.select(0, 1, readable));
                cursor = math.select(-1, TargetNext[targetIndex], readable);
            }

            selection.Score01 = math.select(0f, selection.Score01, selection.TargetHash != 0u);
            return selection;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordCognitionTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CognitionStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<CognitionActionOutputDTO> Outputs;
        [ReadOnly, NoAlias] public NativeArray<CognitionUtilityTuningDTO> Tuning;
        [NoAlias] public NativeArray<CognitionTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint Frame;
        public float BurstMicroseconds;

        public void Execute()
        {
            bool invalid = !States.IsCreated |
                           !Outputs.IsCreated |
                           !TelemetryRing.IsCreated |
                           TelemetryRing.Length <= 0;
            if (invalid)
                return;

            int count = math.min(States.Length, Outputs.Length);
            if (count <= 0)
                return;

            float fearSum = 0f;
            float hungerSum = 0f;
            float aggressionSum = 0f;
            float maxUtility = 0f;
            uint huntingCount = 0u;
            uint nonFiniteCount = 0u;
            uint actionFold = 2166136261u;
            ulong targetFold = 1469598103934665603UL;

            for (int i = 0; i < count; i++)
            {
                CognitionStateDTO state = States[i];
                CognitionActionOutputDTO output = Outputs[i];
                fearSum += UtilityAICognitionJobMath.Sanitize01(state.Fear01);
                hungerSum += UtilityAICognitionJobMath.Sanitize01(state.Hunger01);
                aggressionSum += UtilityAICognitionJobMath.Sanitize01(state.Aggression01);
                maxUtility = math.max(maxUtility, UtilityAICognitionJobMath.SanitizeNonNegative(output.MaxUtility, 0f));
                huntingCount += (uint)math.select(0, 1, output.ActionHash == UtilityAICognitionConstants.ActionHuntHash);
                nonFiniteCount += (uint)math.select(0, 1, (output.Flags & UtilityAICognitionActionFlags.Fault) != 0);
                actionFold = UtilityAICognitionJobMath.Fnv(actionFold, output.ActionHash);
                targetFold = UtilityAICognitionJobMath.Fnv64(targetFold, output.TargetEntityHash);
            }

            float invCount = math.rcp(math.max(1f, count));
            CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.ReadTuning(Tuning);
            float faultLimit = UtilityAICognitionJobMath.SanitizePositive(tuning.Runtime.w, UtilityAICognitionConstants.FaultMicroseconds);
            uint faultFlags = 0u;
            faultFlags |= (uint)math.select(0, UtilityAICognitionActionFlags.Fault, nonFiniteCount > 0u);
            faultFlags |= (uint)math.select(0, UtilityAICognitionActionFlags.OverBudget, BurstMicroseconds > faultLimit);

            int cursor = (int)(Frame % UtilityAICognitionConstants.TelemetryFrames);
            if (TelemetryCursor.IsCreated & TelemetryCursor.Length > 0)
                TelemetryCursor[0] = cursor;

            int ringIndex = cursor % TelemetryRing.Length;
            CognitionTelemetryEntry entry = default;
            entry.Frame = Frame;
            entry.ActionHashFold = actionFold;
            entry.HuntingCount = huntingCount;
            entry.FaultFlags = faultFlags;
            entry.AverageFear01 = fearSum * invCount;
            entry.AverageHunger01 = hungerSum * invCount;
            entry.AverageAggression01 = aggressionSum * invCount;
            entry.MaximumUtility = maxUtility;
            entry.BurstMicroseconds = UtilityAICognitionJobMath.SanitizeNonNegative(BurstMicroseconds, 0f);
            entry.GlobalQualityWeight = UtilityAICognitionJobMath.ResolveQuality(tuning.Runtime.x);
            entry.ActiveCount = (uint)count;
            entry.NonFiniteCount = nonFiniteCount;
            entry.TargetHashFold = targetFold;
            TelemetryRing[ringIndex] = entry;
        }
    }

    public struct TargetSelection
    {
        public float Score01;
        public uint TargetHash;
        public float3 LocalDirection;
        public float Threat01;
    }

    public static class UtilityAICognitionJobMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CognitionUtilityTuningDTO ReadTuning(NativeArray<CognitionUtilityTuningDTO> tuning)
        {
            if (!tuning.IsCreated | tuning.Length <= 0)
                return UtilityAICognitionDefaults.BuildTuning();

            CognitionUtilityTuningDTO value = tuning[0];
            return SanitizeTuning(in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CognitionUtilityTuningDTO ReadTuning(NativeArray<CognitionUtilityTuningDTO>.ReadOnly tuning)
        {
            if (!tuning.IsCreated | tuning.Length <= 0)
                return UtilityAICognitionDefaults.BuildTuning();

            CognitionUtilityTuningDTO value = tuning[0];
            return SanitizeTuning(in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CognitionUtilityTuningDTO SanitizeTuning(in CognitionUtilityTuningDTO input)
        {
            CognitionUtilityTuningDTO fallback = UtilityAICognitionDefaults.BuildTuning();
            CognitionUtilityTuningDTO tuning = input;
            tuning.HungerPolynomial = SelectFinite(fallback.HungerPolynomial, tuning.HungerPolynomial);
            tuning.FearPolynomial = SelectFinite(fallback.FearPolynomial, tuning.FearPolynomial);
            tuning.AggressionPolynomial = SelectFinite(fallback.AggressionPolynomial, tuning.AggressionPolynomial);
            tuning.ActionBiases = SelectFinite(fallback.ActionBiases, tuning.ActionBiases);
            tuning.SignalGains = math.max(float4.zero, SelectFinite(fallback.SignalGains, tuning.SignalGains));
            tuning.DistanceMeters = math.max(new float4(1f, 1f, 1f, 1024f), SelectFinite(fallback.DistanceMeters, tuning.DistanceMeters));
            tuning.Runtime = SelectFinite(fallback.Runtime, tuning.Runtime);
            tuning.Runtime.x = ResolveQuality(tuning.Runtime.x);
            tuning.Runtime.y = SanitizePositive(tuning.Runtime.y, fallback.Runtime.y);
            tuning.Runtime.w = SanitizePositive(tuning.Runtime.w, fallback.Runtime.w);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQuality(float quality)
        {
            float q = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return math.smoothstep(0f, 1f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTickInterval(float quality)
        {
            return math.lerp(0.1f, 1.5f, 1f - ResolveQuality(quality));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveCandidateBudget(float quality)
        {
            return math.clamp((int)math.ceil(1f + (ResolveQuality(quality) * 3f)), 1, UtilityAICognitionConstants.DearLieCandidateLimit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSignalTapLimit(float quality, int minCount, int maxCount)
        {
            float value = math.lerp(math.max(1, minCount), math.max(minCount, maxCount), ResolveQuality(quality));
            return math.max(1, (int)math.round(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte EncodeQualityWeightQ8(float quality)
        {
            float encodedQuality = Sanitize01(quality);
            return (byte)math.clamp((int)math.round(encodedQuality * 255f), 0, 255);
        }

        public static float EvaluatePolynomial01(float x, float4 coefficients)
        {
            float input = Sanitize01(x);
            float value = (((coefficients.x * input) + coefficients.y) * input + coefficients.z) * input + coefficients.w;
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizePositive(float value, float fallback)
        {
            float selected = math.select(fallback, value, math.isfinite(value) & value > 0f);
            return math.max(selected, UtilityAICognitionConstants.Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeNonNegative(float value, float fallback)
        {
            return math.max(0f, math.select(fallback, value, math.isfinite(value) & value >= 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.isfinite(lengthSq) & lengthSq > UtilityAICognitionConstants.Epsilon;
            float3 normalized = value * math.rsqrt(math.max(lengthSq, UtilityAICognitionConstants.Epsilon));
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 HashDirection(int index, uint frame)
        {
            uint hash = Hash(index, frame);
            float angle = ((hash & 4095u) * (1f / 4095f)) * 6.2831855f;
            ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
            return new float3(angleCos, 0f, angleSin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApproxSinCosBhaskara(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinBhaskara(radians);
            cosine = ApproxSinBhaskara(radians + 1.57079632679f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproxSinBhaskara(float radians)
        {
            float angle = math.select(0f, radians, math.isfinite(radians));
            float cycle = angle * 0.15915494309f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * 6.28318530718f;
            float mirrored = math.select(x, 6.28318530718f - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float numerator = 16f * shape;
            float denominator = math.max(UtilityAICognitionConstants.Epsilon, (5f * math.PI * math.PI) - (4f * shape));
            float value = sign * numerator * math.rcp(denominator);
            return math.clamp(math.select(0f, value, math.isfinite(value)), -1f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashAupCell(double3 aup, float cellSize, int bucketCount)
        {
            float safeCell = SanitizePositive(cellSize, 48f);
            int safeBuckets = math.max(1, bucketCount);
            double invCell = math.rcp((double)safeCell);
            double3 scaled = aup * invCell;
            if (!math.all(math.isfinite(scaled)))
                return 0u;

            const double maxSafeLongCell = 9000000000000000000.0;
            const double minSafeLongCell = -9000000000000000000.0;
            scaled = math.clamp(scaled, new double3(minSafeLongCell), new double3(maxSafeLongCell));
            long x = (long)math.floor(scaled.x);
            long y = (long)math.floor(scaled.y);
            long z = (long)math.floor(scaled.z);
            uint hash = 2166136261u;
            hash = Fnv(hash, (uint)x);
            hash = Fnv(hash, (uint)(x >> 32));
            hash = Fnv(hash, (uint)y);
            hash = Fnv(hash, (uint)(y >> 32));
            hash = Fnv(hash, (uint)z);
            hash = Fnv(hash, (uint)(z >> 32));
            return hash % (uint)safeBuckets;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(int index, uint seed)
        {
            uint hash = 2166136261u;
            hash = Fnv(hash, (uint)index);
            hash = Fnv(hash, seed);
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(in CognitionStateDTO state, uint entityHash, uint frame)
        {
            uint hash = 2166136261u;
            hash = Fnv(hash, entityHash);
            hash = Fnv(hash, frame);
            hash = Fnv(hash, math.asuint(state.Hunger01));
            hash = Fnv(hash, math.asuint(state.Fear01));
            hash = Fnv(hash, math.asuint(state.Aggression01));
            hash = Fnv(hash, state.ActiveActionHash);
            hash = Fnv(hash, state.TargetEntityHash);
            return Fnv(hash, math.asuint(state.ActionCooldown));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Fnv64(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 SelectFinite(float4 fallback, float4 value)
        {
            return math.select(fallback, value, math.all(math.isfinite(value)));
        }
    }
}
