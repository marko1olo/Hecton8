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
    public struct GenerateMockAnxietySpikesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<CognitionStateDTO> States;
        [NoAlias] public NativeArray<CognitionAupDTO> Aups;
        [NoAlias] public NativeArray<AnxietyRuntimeTuningDTO> Tuning;
        public uint Frame;
        public int SpikeCount;

        public void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)States.Length)
                return;

            AnxietyRuntimeTuningDTO tuning = AnxietyDecayJobMath.ReadTuning(Tuning);
            float quality = AnxietyDecayJobMath.Sanitize01(tuning.GlobalQualityWeight);
            uint hash = UtilityAICognitionJobMath.Hash(index, Frame ^ AnxietyDecayConstants.AgentHash);
            float phase = (hash & 2047u) * (1f / 2047f);
            float alt = ((hash >> 11) & 2047u) * (1f / 2047f);
            bool inSpikeBudget = index < math.max(0, SpikeCount);
            bool spike = inSpikeBudget | ((hash & 7u) == 0u);

            CognitionStateDTO state = States[index];
            state.Fear01 = math.select(
                AnxietyDecayJobMath.Sanitize01(state.Fear01),
                math.saturate(0.42f + (phase * 0.58f)),
                spike);
            state.Aggression01 = math.select(
                AnxietyDecayJobMath.Sanitize01(state.Aggression01),
                math.saturate(0.25f + (alt * 0.55f) + (quality * 0.1f)),
                spike);
            state.ActiveActionHash = math.select(UtilityAICognitionConstants.ActionPatrolHash, UtilityAICognitionConstants.ActionFleeHash, state.Fear01 > state.Aggression01);
            state.ActionCooldown = math.max(0f, state.ActionCooldown);
            States[index] = state;

            if (Aups.IsCreated && (uint)index < (uint)Aups.Length)
            {
                CognitionAupDTO aup = Aups[index];
                if (aup.EntityHash == 0u)
                    aup.EntityHash = 0xA3120000u ^ (uint)index;
                aup.Flags |= AnxietyDecayFlags.Agitated | AnxietyDecayFlags.EmergencyMock;
                if (!math.all(math.isfinite(aup.AUP)))
                {
                    float radius = math.lerp(12f, 180f, phase);
                    float angle = alt * 6.28318530718f;
                    AnxietyDecayJobMath.ApproxSinCosBhaskara(angle, out float sine, out float cosine);
                    aup.AUP = new double3(cosine * radius, -48.0 + ((index & 15) * 2.0), sine * radius);
                }

                Aups[index] = aup;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockShelterSdfJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float> ShelterSdf;
        [NoAlias] public NativeArray<AnxietyShelterSdfHeaderDTO> Header;

        public void Execute(int index)
        {
            AnxietyShelterSdfHeaderDTO header = AnxietyDecayDefaults.BuildShelterHeader();
            if (Header.IsCreated && Header.Length > 0 && index == 0)
                Header[0] = header;

            if (!ShelterSdf.IsCreated || (uint)index >= (uint)ShelterSdf.Length)
                return;

            int3 dims = header.Dimensions;
            int xy = math.max(1, dims.x * dims.y);
            int z = index / xy;
            int rem = index - (z * xy);
            int y = rem / math.max(1, dims.x);
            int x = rem - (y * math.max(1, dims.x));
            float3 center = new float3(dims) * 0.5f;
            float3 local = new float3(x, y, z) - center;
            float tube = math.length(local.xz) - math.lerp(5f, 10f, math.saturate((local.y + 12f) * (1f / 24f)));
            float ceiling = math.abs(local.y) - 10f;
            ShelterSdf[index] = math.max(tube, ceiling) * header.VoxelSizeMeters * 0.25f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateAnxietyDecayJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CognitionStateDTO> States;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CognitionAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<AnxietyProfileDTO> Profiles;
        [ReadOnly, NoAlias] public NativeArray<AnxietyRuntimeTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<float> ShelterSdf;
        [ReadOnly, NoAlias] public NativeArray<AnxietyShelterSdfHeaderDTO> ShelterHeader;
        [NoAlias] public NativeArray<AnxietyDecayScratchDTO> Scratch;
        public uint Frame;
        public float DeltaSeconds;

        public void Execute(int index)
        {
            if (!States.IsCreated || !Aups.IsCreated || (uint)index >= (uint)States.Length || (uint)index >= (uint)Aups.Length)
                return;

            void* statePtr = NativeArrayUnsafeUtility.GetUnsafePtr(States);
            void* aupPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Aups);
            ref CognitionStateDTO state = ref UnsafeUtility.AsRef<CognitionStateDTO>((byte*)statePtr + (UnsafeUtility.SizeOf<CognitionStateDTO>() * index));
            ref CognitionAupDTO aup = ref UnsafeUtility.AsRef<CognitionAupDTO>((byte*)aupPtr + (UnsafeUtility.SizeOf<CognitionAupDTO>() * index));

            AnxietyRuntimeTuningDTO tuning = AnxietyDecayJobMath.ReadTuning(Tuning);
            AnxietyProfileDTO profile = AnxietyDecayJobMath.ReadProfile(Profiles, 0, in tuning);
            float dt = AnxietyDecayJobMath.SanitizePositive(DeltaSeconds, tuning.SimulationDeltaSeconds);
            float quality = AnxietyDecayJobMath.Sanitize01(tuning.GlobalQualityWeight);
            float thermal = AnxietyDecayJobMath.Sanitize01(tuning.ThermalPressure01);
            float exactWeight = AnxietyDecayJobMath.ResolveExactWeight(quality, thermal, tuning.ExactExpWeight01);
            float shelterMultiplier = AnxietyDecayJobMath.ResolveShelterMultiplier(
                aup.AUP,
                ShelterSdf,
                ShelterHeader,
                tuning.ShelterCoolingMultiplier,
                out uint shelterFlags);

            float oldFear = AnxietyDecayJobMath.Sanitize01(state.Fear01);
            float oldAggression = AnxietyDecayJobMath.Sanitize01(state.Aggression01);
            bool finiteState = math.isfinite(state.Fear01) & math.isfinite(state.Aggression01) & math.all(math.isfinite(aup.AUP));
            float fearRate = AnxietyDecayJobMath.SanitizePositive(profile.FearDecayRate, tuning.BaseFearDecayRate) * shelterMultiplier;
            float aggressionRate = AnxietyDecayJobMath.SanitizePositive(profile.AggressionDecayRate, tuning.BaseAggressionDecayRate) * shelterMultiplier;
            float linearScale = AnxietyDecayJobMath.SanitizePositive(tuning.LinearDecayScale, AnxietyDecayConstants.DefaultLinearDecayScale);

            float fearLinear = math.max(0f, oldFear - (fearRate * dt * linearScale));
            float aggressionLinear = math.max(0f, oldAggression - (aggressionRate * dt * linearScale));
            float fearExact = oldFear * AnxietyDecayJobMath.ApproxExpNegPade33Reduced(fearRate * dt);
            float aggressionExact = oldAggression * AnxietyDecayJobMath.ApproxExpNegPade33Reduced(aggressionRate * dt);

            float fear = math.saturate(math.lerp(fearLinear, fearExact, exactWeight));
            float aggression = math.saturate(math.lerp(aggressionLinear, aggressionExact, exactWeight));

            float threshold = AnxietyDecayJobMath.SanitizePositive(profile.CalmingThreshold, tuning.CalmingThreshold);
            bool fearCalm = fear <= threshold;
            bool aggressionCalm = aggression <= threshold;
            fear = math.select(fear, 0f, fearCalm);
            aggression = math.select(aggression, 0f, aggressionCalm);

            state.Fear01 = math.select(0f, fear, finiteState);
            state.Aggression01 = math.select(0f, aggression, finiteState);
            bool calm = (state.Fear01 <= 0f) & (state.Aggression01 <= 0f);
            uint agitatedMask = AnxietyDecayFlags.Agitated;
            aup.Flags = math.select(aup.Flags | agitatedMask, aup.Flags & ~agitatedMask, calm);
            bool interruptibleAnxietyAction = (state.ActiveActionHash == UtilityAICognitionConstants.ActionFleeHash) |
                                               (state.ActiveActionHash == UtilityAICognitionConstants.ActionHuntHash);
            state.ActiveActionHash = math.select(state.ActiveActionHash, UtilityAICognitionConstants.ActionPatrolHash, calm & interruptibleAnxietyAction);

            uint flags = AnxietyDecayFlags.Active | shelterFlags;
            flags |= (uint)math.select(0, (int)AnxietyDecayFlags.UsedLinearApproximation, exactWeight < 0.999f);
            flags |= (uint)math.select(0, (int)AnxietyDecayFlags.NonFiniteInput, !finiteState);
            if (Scratch.IsCreated && (uint)index < (uint)Scratch.Length)
            {
                AnxietyDecayScratchDTO row = default;
                row.Fear01 = state.Fear01;
                row.Aggression01 = state.Aggression01;
                row.ShelterMultiplier = shelterMultiplier;
                row.Flags = flags;
                row.StateHash = AnxietyDecayJobMath.HashState(in state, aup.EntityHash, Frame);
                row.EntityHash = aup.EntityHash;
                Scratch[index] = row;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordAnxietyTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AnxietyDecayScratchDTO> Scratch;
        [ReadOnly, NoAlias] public NativeArray<AnxietyRuntimeTuningDTO> Tuning;
        [NoAlias] public NativeArray<AnxietyTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint Frame;
        public int StateCount;
        public float BurstMicroseconds;

        public void Execute()
        {
            if (!Scratch.IsCreated || !TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int count = math.min(math.max(0, StateCount), Scratch.Length);
            if (count <= 0)
                return;

            float fearSum = 0f;
            float aggressionSum = 0f;
            float shelterSum = 0f;
            uint activeCount = 0u;
            uint shelterCount = 0u;
            uint nonFiniteCount = 0u;
            uint stateHashFold = 2166136261u;

            for (int i = 0; i < count; i++)
            {
                AnxietyDecayScratchDTO row = Scratch[i];
                bool active = (row.Flags & AnxietyDecayFlags.Active) != 0u;
                activeCount += (uint)math.select(0, 1, active);
                shelterCount += (uint)math.select(0, 1, (row.Flags & AnxietyDecayFlags.ShelterSampled) != 0u);
                nonFiniteCount += (uint)math.select(0, 1, (row.Flags & AnxietyDecayFlags.NonFiniteInput) != 0u);
                fearSum += AnxietyDecayJobMath.Sanitize01(row.Fear01);
                aggressionSum += AnxietyDecayJobMath.Sanitize01(row.Aggression01);
                shelterSum += AnxietyDecayJobMath.SanitizePositive(row.ShelterMultiplier, 1f);
                stateHashFold = UtilityAICognitionJobMath.Fnv(stateHashFold, row.StateHash);
            }

            AnxietyRuntimeTuningDTO tuning = AnxietyDecayJobMath.ReadTuning(Tuning);
            float invCount = math.rcp(math.max(1f, count));
            uint faultFlags = 0u;
            faultFlags |= (uint)math.select(0, (int)AnxietyDecayFlags.NonFiniteInput, nonFiniteCount > 0u);
            faultFlags |= (uint)math.select(0, (int)AnxietyDecayFlags.Fault, BurstMicroseconds > tuning.FaultMicroseconds);

            int cursor = (int)(Frame % AnxietyDecayConstants.TelemetryFrames);
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = cursor;

            AnxietyTelemetryEntry entry = default;
            entry.Frame = Frame;
            entry.ActiveDecayCount = activeCount;
            entry.ShelterMultiplierCount = shelterCount;
            entry.NonFiniteCount = nonFiniteCount;
            entry.FaultFlags = faultFlags;
            entry.AverageFear01 = fearSum * invCount;
            entry.AverageAggression01 = aggressionSum * invCount;
            entry.AverageShelterMultiplier = shelterSum * invCount;
            entry.BurstMicroseconds = AnxietyDecayJobMath.SanitizeNonNegative(BurstMicroseconds, 0f);
            entry.GlobalQualityWeight = AnxietyDecayJobMath.Sanitize01(tuning.GlobalQualityWeight);
            entry.ExactExpWeight01 = AnxietyDecayJobMath.ResolveExactWeight(tuning.GlobalQualityWeight, tuning.ThermalPressure01, tuning.ExactExpWeight01);
            entry.ThermalPressure01 = AnxietyDecayJobMath.Sanitize01(tuning.ThermalPressure01);
            entry.StateHashFold = stateHashFold;
            entry.ProfileHashFold = AnxietyDecayJobMath.HashTuning(in tuning);
            TelemetryRing[cursor % TelemetryRing.Length] = entry;
        }
    }

    public static class AnxietyDecayJobMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnxietyRuntimeTuningDTO ReadTuning(NativeArray<AnxietyRuntimeTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return AnxietyDecayDefaults.BuildTuning();

            AnxietyRuntimeTuningDTO value = tuning[0];
            return SanitizeTuning(in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnxietyProfileDTO ReadProfile(NativeArray<AnxietyProfileDTO> profiles, int index, in AnxietyRuntimeTuningDTO tuning)
        {
            AnxietyProfileDTO fallback = AnxietyDecayDefaults.BuildProfile();
            fallback.FearDecayRate = tuning.BaseFearDecayRate;
            fallback.AggressionDecayRate = tuning.BaseAggressionDecayRate;
            fallback.CalmingThreshold = tuning.CalmingThreshold;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return fallback;

            int safeIndex = math.clamp(index, 0, profiles.Length - 1);
            AnxietyProfileDTO profile = profiles[safeIndex];
            profile.FearDecayRate = SanitizePositive(profile.FearDecayRate, fallback.FearDecayRate);
            profile.AggressionDecayRate = SanitizePositive(profile.AggressionDecayRate, fallback.AggressionDecayRate);
            profile.CalmingThreshold = SanitizePositive(profile.CalmingThreshold, fallback.CalmingThreshold);
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnxietyRuntimeTuningDTO SanitizeTuning(in AnxietyRuntimeTuningDTO input)
        {
            AnxietyRuntimeTuningDTO fallback = AnxietyDecayDefaults.BuildTuning();
            AnxietyRuntimeTuningDTO tuning = input;
            tuning.BaseFearDecayRate = SanitizePositive(tuning.BaseFearDecayRate, fallback.BaseFearDecayRate);
            tuning.BaseAggressionDecayRate = SanitizePositive(tuning.BaseAggressionDecayRate, fallback.BaseAggressionDecayRate);
            tuning.CalmingThreshold = SanitizePositive(tuning.CalmingThreshold, fallback.CalmingThreshold);
            tuning.ShelterCoolingMultiplier = math.max(1f, math.select(fallback.ShelterCoolingMultiplier, tuning.ShelterCoolingMultiplier, math.isfinite(tuning.ShelterCoolingMultiplier)));
            tuning.LinearDecayScale = SanitizePositive(tuning.LinearDecayScale, fallback.LinearDecayScale);
            tuning.SimulationDeltaSeconds = SanitizePositive(tuning.SimulationDeltaSeconds, fallback.SimulationDeltaSeconds);
            tuning.GlobalQualityWeight = Sanitize01(tuning.GlobalQualityWeight);
            tuning.ThermalPressure01 = Sanitize01(tuning.ThermalPressure01);
            tuning.ExactExpWeight01 = Sanitize01(math.select(ResolveExactWeight(tuning.GlobalQualityWeight, tuning.ThermalPressure01, 1f), tuning.ExactExpWeight01, math.isfinite(tuning.ExactExpWeight01)));
            tuning.FaultMicroseconds = SanitizePositive(tuning.FaultMicroseconds, fallback.FaultMicroseconds);
            tuning.ActiveProfileCount = tuning.ActiveProfileCount == 0u ? 1u : tuning.ActiveProfileCount;
            tuning.Flags = tuning.Flags == 0u ? AnxietyDecayFlags.Active : tuning.Flags;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveExactWeight(float quality, float thermalPressure, float requestedWeight)
        {
            float q = Sanitize01(quality);
            float thermalRoom = 1f - Sanitize01(thermalPressure);
            float qCurve = q * q * (3f - (2f * q));
            return Sanitize01(qCurve * thermalRoom * Sanitize01(requestedWeight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpNegPade33Reduced(float value)
        {
            float selected = math.select(0f, value, math.isfinite(value));
            float safe = math.min(math.max(0f, selected), 4f);
            float x = safe * 0.25f;
            float x2 = x * x;
            float x3 = x2 * x;
            float numerator = 1f - (0.5f * x) + (0.1f * x2) - ((1f / 120f) * x3);
            float denominator = 1f + (0.5f * x) + (0.1f * x2) + ((1f / 120f) * x3);
            float baseDecay = numerator * math.rcp(math.max(denominator, UtilityAICognitionConstants.Epsilon));
            float decay2 = baseDecay * baseDecay;
            float decay4 = decay2 * decay2;
            return math.saturate(math.select(0f, decay4, math.isfinite(decay4)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxSinBhaskara(float radians)
        {
            float angle = math.select(0f, radians, math.isfinite(radians));
            float cycle = angle * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * 6.28318530718f;
            float mirrored = math.select(x, 6.28318530718f - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float numerator = 16f * shape;
            float denominator = math.max(UtilityAICognitionConstants.Epsilon, (5f * math.PI * math.PI) - (4f * shape));
            float sine = sign * numerator * math.rcp(denominator);
            return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApproxSinCosBhaskara(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinBhaskara(radians);
            cosine = ApproxSinBhaskara(radians + (0.5f * math.PI));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveShelterMultiplier(
            double3 creatureAup,
            NativeArray<float> shelterSdf,
            NativeArray<AnxietyShelterSdfHeaderDTO> headerBuffer,
            float shelterCoolingMultiplier,
            out uint flags)
        {
            flags = 0u;
            if (!shelterSdf.IsCreated || !headerBuffer.IsCreated || headerBuffer.Length <= 0 || shelterSdf.Length <= 0)
                return 1f;

            AnxietyShelterSdfHeaderDTO header = headerBuffer[0];
            int3 dims = math.max(header.Dimensions, new int3(1));
            int required = dims.x * dims.y * dims.z;
            bool validHeader = required > 0 &&
                               required <= shelterSdf.Length &&
                               header.VoxelSizeMeters > 0f &&
                               math.all(math.isfinite(header.OriginAUP)) &&
                               math.all(math.isfinite(creatureAup));
            if (!validHeader)
                return 1f;

            double3 localD = AupPrecisionMath.LocalDeltaDouble(creatureAup, header.OriginAUP);
            float maxLocal = math.max(1f, header.VoxelSizeMeters * math.cmax(new float3(dims)));
            float3 local = AupPrecisionMath.DowncastLocalDeltaClamped(localD, maxLocal, new float3(-1f));
            float invVoxel = math.rcp(math.max(header.VoxelSizeMeters, UtilityAICognitionConstants.Epsilon));
            int3 cell = (int3)math.floor(local * invVoxel);
            bool inside = math.all(cell >= int3.zero) & math.all(cell < dims);
            int index = math.clamp(cell.x + (cell.y * dims.x) + (cell.z * dims.x * dims.y), 0, shelterSdf.Length - 1);
            float sdf = math.select(1f, shelterSdf[index], inside);
            float range = math.max(UtilityAICognitionConstants.Epsilon, math.abs(header.SdfRangeMeters));
            float sheltered01 = math.saturate((header.SolidThreshold - sdf) * math.rcp(range));
            flags = (uint)math.select(0, (int)AnxietyDecayFlags.ShelterSampled, inside & (sheltered01 > 0f));
            return math.lerp(1f, math.max(1f, shelterCoolingMultiplier), sheltered01);
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
        public static uint HashState(in CognitionStateDTO state, uint entityHash, uint frame)
        {
            uint hash = UtilityAICognitionJobMath.HashState(in state, entityHash, frame);
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(state.Fear01));
            return UtilityAICognitionJobMath.Fnv(hash, math.asuint(state.Aggression01));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashTuning(in AnxietyRuntimeTuningDTO tuning)
        {
            uint hash = 2166136261u;
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(tuning.BaseFearDecayRate));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(tuning.BaseAggressionDecayRate));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(tuning.CalmingThreshold));
            hash = UtilityAICognitionJobMath.Fnv(hash, math.asuint(tuning.ShelterCoolingMultiplier));
            return UtilityAICognitionJobMath.Fnv(hash, tuning.ActiveProfileCount);
        }
    }
}
