using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuMetabolismJobMath
    {
        private const float Epsilon = 0.0001f;
        private const double DefaultSeaLevelAupY = 14.02d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PositiveOr(float value, float fallback)
        {
            return math.isfinite(value) && value > Epsilon ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastLengthFromSq(float lengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            return lengthSq * math.rsqrt(math.max(lengthSq, Epsilon));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSeaLevelDepthMeters(double absoluteY, double seaLevelAupY)
        {
            double resolvedSeaLevelAupY = math.isfinite(seaLevelAupY) &&
                                         math.abs(seaLevelAupY) > 0.0001d &&
                                         math.abs(seaLevelAupY) <= 1000d
                ? seaLevelAupY
                : DefaultSeaLevelAupY;
            return math.isfinite(absoluteY)
                ? (float)math.max(0d, resolvedSeaLevelAupY - absoluteY)
                : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCadenceSeconds(float globalQualityWeight)
        {
            float q = math.saturate(SanitizeFinite(globalQualityWeight, 1f));
            return math.lerp(1f, 0.1f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveThermalInterpolationWeight(float globalQualityWeight)
        {
            float q = math.saturate(SanitizeFinite(globalQualityWeight, 1f));
            return q * q * (3f - 2f * q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(SanitizeFinite(x, 0f), 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator * math.rcp(math.max(denominator, Epsilon)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolismTuningDTO BuildDefaultTuning()
        {
            MetabolismTuningDTO tuning = default;
            tuning.BaseCalorieDrainScale = 1f;
            tuning.BaseHydrationDrainScale = 1f;
            tuning.TemperatureLossRate = 0.00045f;
            tuning.ExertionMultiplier = 0.05f;
            tuning.ExertionHydrationMultiplier = 0.015f;
            tuning.ToxinAccumulationPerSecond = 0.02f;
            tuning.ToxinPurgePerSecond = 0.0025f;
            tuning.ShiverCalorieBoost = 1.4f;
            tuning.FrostStartTemperatureCelsius = 34.5f;
            tuning.FrostFullTemperatureCelsius = 30f;
            tuning.AmbientFallbackTemperatureCelsius = 2f;
            tuning.ToxicDamageScale = 4f;
            tuning.GlobalQualityWeight = 1f;
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolismTuningDTO SanitizeTuning(MetabolismTuningDTO tuning)
        {
            if (tuning.Version == 0u)
                tuning = BuildDefaultTuning();

            tuning.BaseCalorieDrainScale = math.clamp(SanitizeFinite(tuning.BaseCalorieDrainScale, 1f), 0f, 32f);
            tuning.BaseHydrationDrainScale = math.clamp(SanitizeFinite(tuning.BaseHydrationDrainScale, 1f), 0f, 32f);
            tuning.TemperatureLossRate = math.clamp(SanitizeFinite(tuning.TemperatureLossRate, 0.00045f), 0f, 2f);
            tuning.ExertionMultiplier = math.clamp(SanitizeFinite(tuning.ExertionMultiplier, 0.05f), 0f, 4f);
            tuning.ExertionHydrationMultiplier = math.clamp(SanitizeFinite(tuning.ExertionHydrationMultiplier, 0.015f), 0f, 4f);
            tuning.ToxinAccumulationPerSecond = math.clamp(SanitizeFinite(tuning.ToxinAccumulationPerSecond, 0.02f), 0f, 8f);
            tuning.ToxinPurgePerSecond = math.clamp(SanitizeFinite(tuning.ToxinPurgePerSecond, 0.0025f), 0f, 2f);
            tuning.ShiverCalorieBoost = math.clamp(SanitizeFinite(tuning.ShiverCalorieBoost, 1.4f), 0f, 8f);
            tuning.FrostStartTemperatureCelsius = math.clamp(SanitizeFinite(tuning.FrostStartTemperatureCelsius, 34.5f), 10f, 42f);
            tuning.FrostFullTemperatureCelsius = math.clamp(SanitizeFinite(tuning.FrostFullTemperatureCelsius, 30f), 5f, tuning.FrostStartTemperatureCelsius - 0.25f);
            tuning.AmbientFallbackTemperatureCelsius = math.clamp(SanitizeFinite(tuning.AmbientFallbackTemperatureCelsius, 2f), -80f, 120f);
            tuning.ToxicDamageScale = math.clamp(SanitizeFinite(tuning.ToxicDamageScale, 4f), 0f, 128f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolicSpeciesRuleDTO BuildDefaultRule(uint speciesHash)
        {
            MetabolicSpeciesRuleDTO rule = default;
            rule.SpeciesHash = speciesHash;
            rule.MaxCalories = 100f;
            rule.MaxHydration = 100f;
            rule.BaseCalorieDrainPerSecond = 0.0022f;
            rule.BaseHydrationDrainPerSecond = 0.003f;
            rule.ThermalConductance = 1f;
            rule.ToxinSusceptibility = 1f;
            rule.ShiverTemperatureCelsius = 35f;
            rule.HypothermiaTemperatureCelsius = 31f;
            rule.HeatHydrationLossScale = 0.35f;
            rule.ToxicDamagePerSecond = 1f;
            rule.RecoveryTemperatureCelsius = 37f;
            return rule;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolicSuitThermalProfileDTO BuildDefaultSuitProfile(uint profileHash)
        {
            MetabolicSuitThermalProfileDTO profile = default;
            profile.ProfileHash = profileHash;
            profile.ConductanceMultiplier = 1f;
            profile.Insulation01 = 0.55f;
            profile.ShiverMultiplier = 1f;
            profile.HeatHydrationMultiplier = 1f;
            profile.BatteryHeatingCelsiusPerSecond = 0f;
            profile.Flags = ShinobuMetabolismSuitProfileFlags.DefaultProfile;
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolicSuitThermalProfileDTO SanitizeSuitProfile(MetabolicSuitThermalProfileDTO profile)
        {
            if (profile.ProfileHash == 0u)
                profile = BuildDefaultSuitProfile(ShinobuMetabolismConstants.StandardSuitHash);

            profile.ConductanceMultiplier = math.clamp(SanitizeFinite(profile.ConductanceMultiplier, 1f), 0.01f, 8f);
            profile.Insulation01 = math.saturate(SanitizeFinite(profile.Insulation01, 0.55f));
            profile.ShiverMultiplier = math.clamp(SanitizeFinite(profile.ShiverMultiplier, 1f), 0f, 8f);
            profile.HeatHydrationMultiplier = math.clamp(SanitizeFinite(profile.HeatHydrationMultiplier, 1f), 0f, 8f);
            profile.BatteryHeatingCelsiusPerSecond = math.clamp(SanitizeFinite(profile.BatteryHeatingCelsiusPerSecond, 0f), 0f, 4f);
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SuitProfileHashMatches(uint profileHash, uint equippedSuitHash)
        {
            if (profileHash == 0u || equippedSuitHash == 0u)
                return false;
            if (profileHash == equippedSuitHash)
                return true;

            uint alias0 = 0u;
            uint alias1 = 0u;
            if (equippedSuitHash == ShinobuMetabolismConstants.StandardSuitHash)
            {
                alias0 = ShinobuMetabolismConstants.SuitProfileHashStandardWetsuit;
            }
            else if (equippedSuitHash == ShinobuMetabolismConstants.ReinforcedSuitHash)
            {
                alias0 = ShinobuMetabolismConstants.SuitProfileHashReinforcedSuit;
                alias1 = ShinobuMetabolismConstants.SuitProfileHashReinforcedWetsuit;
            }
            else if (equippedSuitHash == ShinobuMetabolismConstants.ExosuitHash)
            {
                alias0 = ShinobuMetabolismConstants.SuitProfileHashThermalPrawnSuit;
            }
            else if (equippedSuitHash == ShinobuMetabolismConstants.SubmarineHullHash)
            {
                alias0 = ShinobuMetabolismConstants.SuitProfileHashSubmarineHull;
            }

            return profileHash == alias0 || profileHash == alias1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MetabolicSpeciesRuleDTO SanitizeRule(MetabolicSpeciesRuleDTO rule)
        {
            if (rule.SpeciesHash == 0u)
                rule = BuildDefaultRule(0x51CCEFFAu);

            rule.MaxCalories = math.clamp(SanitizeFinite(rule.MaxCalories, 100f), 0.001f, 100000f);
            rule.MaxHydration = math.clamp(SanitizeFinite(rule.MaxHydration, 100f), 0.001f, 100000f);
            rule.BaseCalorieDrainPerSecond = math.clamp(SanitizeFinite(rule.BaseCalorieDrainPerSecond, 0.0022f), 0f, 128f);
            rule.BaseHydrationDrainPerSecond = math.clamp(SanitizeFinite(rule.BaseHydrationDrainPerSecond, 0.003f), 0f, 128f);
            rule.ThermalConductance = math.clamp(SanitizeFinite(rule.ThermalConductance, 1f), 0f, 64f);
            rule.ToxinSusceptibility = math.clamp(SanitizeFinite(rule.ToxinSusceptibility, 1f), 0f, 64f);
            rule.ShiverTemperatureCelsius = math.clamp(SanitizeFinite(rule.ShiverTemperatureCelsius, 35f), 5f, 45f);
            rule.HypothermiaTemperatureCelsius = math.clamp(SanitizeFinite(rule.HypothermiaTemperatureCelsius, 31f), 0f, rule.ShiverTemperatureCelsius - 0.1f);
            rule.HeatHydrationLossScale = math.clamp(SanitizeFinite(rule.HeatHydrationLossScale, 0.35f), 0f, 8f);
            rule.ToxicDamagePerSecond = math.clamp(SanitizeFinite(rule.ToxicDamagePerSecond, 1f), 0f, 128f);
            rule.RecoveryTemperatureCelsius = math.clamp(SanitizeFinite(rule.RecoveryTemperatureCelsius, 37f), 5f, 45f);
            return rule;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitMockMetabolismJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* EntityAups;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ExertionSpeedSq;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ToxinSamples;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* RuleIndices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* SuitProfileIndices;
        public int Count;
        public uint Seed;
        public uint Frame;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            uint hash = Seed ^ ((uint)index * 747796405u) ^ (Frame * 2891336453u);
            hash = ShinobuMetabolismJobMath.Fnv1A(hash, (uint)index);
            Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(math.max(1u, hash));
            float calorie01 = rng.NextFloat(0.62f, 1f);
            float hydration01 = rng.NextFloat(0.7f, 1f);
            float temp = rng.NextFloat(35.8f, 37.2f);

            ref MetabolicStateDTO state = ref UnsafeUtility.AsRef<MetabolicStateDTO>(States + index);
            state.Calories = 100f * calorie01;
            state.Hydration = 100f * hydration01;
            state.CoreTemperature = temp;
            state.Toxicity = rng.NextFloat(0f, 0.3f);
            state.EntityHashID = 0xA5000000u | (uint)index;
            state.Flags = ShinobuMetabolismFlags.MockEntity;
            state.Fatigue01 = 0f;
            state.RealO2 = 1f;
            state.AgonyTimeRemaining = 0f;
            state.IsInHypoxia = 0;

            double3 entityAup = default;
            entityAup.x = ((int)(index % 100) - 50) * 12.5;
            entityAup.y = -50.0 - ((index * 17) % 700);
            entityAup.z = ((int)((index / 100) % 50) - 25) * 12.5;
            EntityAups[index] = entityAup;
            ExertionSpeedSq[index] = ((index * 13) & 7) * 0.08f;
            ToxinSamples[index] = ((index * 29) & 31) * math.rcp(512f);
            RuleIndices[index] = (ushort)(index % 4);
            if (SuitProfileIndices != null)
                SuitProfileIndices[index] = (ushort)(index == 0 ? 2 : 0);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitMetabolismRulesJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicSpeciesRuleDTO* Rules;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicSuitThermalProfileDTO* SuitProfiles;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolismTuningDTO* Tuning;
        public int RuleCount;
        public int SuitProfileCount;

        public void Execute()
        {
            if (Tuning != null)
                UnsafeUtility.AsRef<MetabolismTuningDTO>(Tuning) = ShinobuMetabolismJobMath.BuildDefaultTuning();

            if (Rules == null || RuleCount <= 0)
                return;

            Rules[0] = ShinobuMetabolismJobMath.BuildDefaultRule(0x51CCEFFAu);
            if (RuleCount > 1)
            {
                MetabolicSpeciesRuleDTO player = ShinobuMetabolismJobMath.BuildDefaultRule(0x2C99C300u);
                player.BaseCalorieDrainPerSecond = 0.0028f;
                player.BaseHydrationDrainPerSecond = 0.0035f;
                player.ThermalConductance = 0.85f;
                Rules[1] = player;
            }

            if (RuleCount > 2)
            {
                MetabolicSpeciesRuleDTO peeper = ShinobuMetabolismJobMath.BuildDefaultRule(0x41077AC0u);
                peeper.MaxCalories = 22f;
                peeper.MaxHydration = 34f;
                peeper.BaseCalorieDrainPerSecond = 0.0015f;
                peeper.BaseHydrationDrainPerSecond = 0.002f;
                peeper.ThermalConductance = 1.25f;
                Rules[2] = peeper;
            }

            if (RuleCount > 3)
            {
                MetabolicSpeciesRuleDTO leviathan = ShinobuMetabolismJobMath.BuildDefaultRule(0x5FCCC0A9u);
                leviathan.MaxCalories = 4000f;
                leviathan.MaxHydration = 2200f;
                leviathan.BaseCalorieDrainPerSecond = 0.08f;
                leviathan.BaseHydrationDrainPerSecond = 0.035f;
                leviathan.ThermalConductance = 0.32f;
                leviathan.ToxinSusceptibility = 0.45f;
                Rules[3] = leviathan;
            }

            for (int i = 4; i < RuleCount; i++)
                Rules[i] = default;

            if (SuitProfiles == null || SuitProfileCount <= 0)
                return;

            SuitProfiles[0] = ShinobuMetabolismJobMath.BuildDefaultSuitProfile(ShinobuMetabolismConstants.StandardSuitHash);
            if (SuitProfileCount > 1)
            {
                MetabolicSuitThermalProfileDTO reinforced = ShinobuMetabolismJobMath.BuildDefaultSuitProfile(ShinobuMetabolismConstants.ReinforcedSuitHash);
                reinforced.ConductanceMultiplier = 0.78f;
                reinforced.Insulation01 = 0.7f;
                reinforced.ShiverMultiplier = 0.75f;
                SuitProfiles[1] = reinforced;
            }

            if (SuitProfileCount > 2)
            {
                MetabolicSuitThermalProfileDTO exosuit = ShinobuMetabolismJobMath.BuildDefaultSuitProfile(ShinobuMetabolismConstants.ExosuitHash);
                exosuit.ConductanceMultiplier = 0.42f;
                exosuit.Insulation01 = 0.88f;
                exosuit.ShiverMultiplier = 0.35f;
                exosuit.HeatHydrationMultiplier = 0.72f;
                exosuit.BatteryHeatingCelsiusPerSecond = 0.018f;
                exosuit.Flags |= ShinobuMetabolismSuitProfileFlags.HeatedSuit;
                SuitProfiles[2] = exosuit;
            }

            if (SuitProfileCount > 3)
            {
                MetabolicSuitThermalProfileDTO hull = ShinobuMetabolismJobMath.BuildDefaultSuitProfile(ShinobuMetabolismConstants.SubmarineHullHash);
                hull.ConductanceMultiplier = 0.24f;
                hull.Insulation01 = 0.94f;
                hull.ShiverMultiplier = 0.15f;
                hull.HeatHydrationMultiplier = 0.5f;
                hull.BatteryHeatingCelsiusPerSecond = 0.025f;
                hull.Flags |= ShinobuMetabolismSuitProfileFlags.HeatedSuit;
                SuitProfiles[3] = hull;
            }

            for (int i = 4; i < SuitProfileCount; i++)
                SuitProfiles[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitInactiveMetabolismJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* EntityAups;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ExertionSpeedSq;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ToxinSamples;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* RuleIndices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* SuitProfileIndices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public PhysiologyStateSignal* PhysiologySignals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicExposureSignalDTO* ExposureSignals;
        public int Count;
        public int PhysiologySignalLength;
        public int ExposureSignalLength;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            ref MetabolicStateDTO state = ref UnsafeUtility.AsRef<MetabolicStateDTO>(States + index);
            state.Calories = 0f;
            state.Hydration = 0f;
            state.CoreTemperature = 0f;
            state.Toxicity = 0f;
            state.EntityHashID = 0u;
            state.Flags = 0u;
            state.Fatigue01 = 0f;
            state.RealO2 = 0f;
            state.AgonyTimeRemaining = 0f;
            state.IsInHypoxia = 0;

            EntityAups[index] = double3.zero;
            ExertionSpeedSq[index] = 0f;
            ToxinSamples[index] = 0f;
            RuleIndices[index] = 0;
            if (SuitProfileIndices != null)
                SuitProfileIndices[index] = 0;
            ClearSignalSlots(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearSignalSlots(int index)
        {
            if (PhysiologySignals != null)
            {
                int baseSlot = index * ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
                for (int slot = 0; slot < ShinobuMetabolismConstants.PhysiologySignalsPerEntity; slot++)
                {
                    int signalIndex = baseSlot + slot;
                    if ((uint)signalIndex < (uint)PhysiologySignalLength)
                        PhysiologySignals[signalIndex] = default;
                }
            }

            if (ExposureSignals != null)
            {
                int baseSlot = index * ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity;
                for (int slot = 0; slot < ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity; slot++)
                {
                    int signalIndex = baseSlot + slot;
                    if ((uint)signalIndex < (uint)ExposureSignalLength)
                        ExposureSignals[signalIndex] = default;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockThermalEnvironmentJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ThermalCelsiusGrid;
        public int3 Resolution;
        public int Length;
        public float CellSizeMeters;
        public float BaseColdCelsius;
        public float HotspotCelsius;
        public float3 HotspotLocalMeters;
        public float HotspotRadiusMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (ThermalCelsiusGrid == null || (uint)index >= (uint)Length)
                return;

            int3 resolution = new int3(
                math.max(1, Resolution.x),
                math.max(1, Resolution.y),
                math.max(1, Resolution.z));
            int cellsPerY = math.max(1, resolution.x * resolution.z);
            int y = index / cellsPerY;
            int yRemainder = index - y * cellsPerY;
            int z = yRemainder / resolution.x;
            int x = yRemainder - z * resolution.x;

            float cellSize = math.max(0.001f, ShinobuMetabolismJobMath.SanitizeFinite(CellSizeMeters, 1f));
            float3 local = new float3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize;
            float3 hotspot = math.select(
                new float3(resolution.x, resolution.y, resolution.z) * cellSize * 0.5f,
                HotspotLocalMeters,
                math.all(math.isfinite(HotspotLocalMeters)));
            float radius = math.max(cellSize, ShinobuMetabolismJobMath.SanitizeFinite(HotspotRadiusMeters, cellSize * 6f));
            float dist01 = math.saturate(ShinobuMetabolismJobMath.FastLengthFromSq(math.lengthsq(local - hotspot)) * math.rcp(radius));
            float falloff = 1f - dist01;
            falloff = falloff * falloff * (3f - 2f * falloff);

            float q = math.saturate(ShinobuMetabolismJobMath.SanitizeFinite(GlobalQualityWeight, 1f));
            float turbulence = ShinobuMetabolismJobMath.ResolveThermalInterpolationWeight(q);
            float3 centered = (local - hotspot) * math.rcp(math.max(cellSize, radius));
            float phase = math.abs(centered.x * 7.13f + centered.y * 3.71f + centered.z * 5.29f);
            float phase01 = math.frac(phase);
            float wave = 1f - math.abs(phase01 * 2f - 1f);
            float denseGradient = math.lerp(falloff, falloff * wave, turbulence);
            float cold = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(BaseColdCelsius, -40f), -90f, 80f);
            float hot = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(HotspotCelsius, 88f), cold, 180f);
            ThermalCelsiusGrid[index] = math.lerp(cold, hot, denseGradient);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MetabolicIntegrationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* EntityAups;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ExertionSpeedSq;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ToxinSamples;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* RuleIndices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicSpeciesRuleDTO* Rules;
        [NativeDisableUnsafePtrRestriction, NoAlias] public ushort* SuitProfileIndices;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicSuitThermalProfileDTO* SuitProfiles;
        [NativeDisableUnsafePtrRestriction, NoAlias] public SuitIntegrityDTO* SuitIntegrityStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ThermalCelsiusGrid;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float4* ChemicalPublishedGrid;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float4* ChemicalOverlayGrid;
        [NativeDisableUnsafePtrRestriction, NoAlias] public PhysiologyStateSignal* PhysiologySignals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicExposureSignalDTO* ExposureSignals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicDetailTelemetryEntry* DetailTelemetry;
        public MetabolismTuningDTO Tuning;
        public double SeaLevelAupY;
        public double3 ThermalGridRootAup;
        public double3 ChemicalGridRootAup;
        public int3 ThermalGridResolution;
        public int3 ChemicalGridResolution;
        public int ThermalGridLength;
        public int ChemicalGridLength;
        public int PhysiologySignalLength;
        public int ExposureSignalLength;
        public int DetailTelemetryLength;
        public int DetailTelemetryCursor;
        public int SuitIntegrityStateCount;
        public float ThermalCellSizeMeters;
        public float ChemicalCellSizeMeters;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public int Count;
        public int RuleCount;
        public int SuitProfileCount;
        public byte HasThermalGrid;
        public byte HasChemicalGrid;
        public byte EmitSignals;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            ClearSignalSlots(index);
            ref MetabolicStateDTO state = ref UnsafeUtility.AsRef<MetabolicStateDTO>(States + index);
            if (state.EntityHashID == 0u)
            {
                state.Flags = 0u;
                state.RealO2 = 0f;
                state.AgonyTimeRemaining = 0f;
                state.IsInHypoxia = 0;
                return;
            }

            double3 entityAup = EntityAups != null ? EntityAups[index] : double3.zero;
            float dt = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(DeltaSeconds, ShinobuMetabolismConstants.NominalSlowTickSeconds), 0.0001f, ShinobuMetabolismConstants.MaxAccumulatedDeltaSeconds);
            MetabolismTuningDTO tuning = ShinobuMetabolismJobMath.SanitizeTuning(Tuning);
            float q = math.saturate(ShinobuMetabolismJobMath.SanitizeFinite(GlobalQualityWeight, tuning.GlobalQualityWeight));
            int ruleIndex = RuleIndices != null ? RuleIndices[index] : 0;
            ruleIndex = math.clamp(ruleIndex, 0, math.max(0, RuleCount - 1));
            MetabolicSpeciesRuleDTO rule = Rules != null && RuleCount > 0
                ? ShinobuMetabolismJobMath.SanitizeRule(Rules[ruleIndex])
                : ShinobuMetabolismJobMath.BuildDefaultRule(0x51CCEFFAu);
            ushort suitProfileIndex = SuitProfileIndices != null ? SuitProfileIndices[index] : (ushort)0;
            uint equippedSuitHash = ResolveEquippedSuitHash(index);
            MetabolicSuitThermalProfileDTO suitProfile = ResolveSuitProfile(index, suitProfileIndex, equippedSuitHash);

            uint flags = state.Flags & ~(ShinobuMetabolismFlags.Starving | ShinobuMetabolismFlags.Dehydrated | ShinobuMetabolismFlags.Hypothermia | ShinobuMetabolismFlags.Toxic | ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.ThermalSampled | ShinobuMetabolismFlags.ChemicalSampled | ShinobuMetabolismFlags.Fatigue | ShinobuMetabolismFlags.ExecutionBudgetExceeded | ShinobuMetabolismFlags.NanDetected);
            float calories = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(state.Calories, rule.MaxCalories), 0f, rule.MaxCalories);
            float hydration = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(state.Hydration, rule.MaxHydration), 0f, rule.MaxHydration);
            float coreTemperature = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(state.CoreTemperature, rule.RecoveryTemperatureCelsius), 18f, 45f);
            float previousCoreTemperature = coreTemperature;
            float toxicity = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(state.Toxicity, 0f), 0f, 8f);
            float rawRealO2 = state.RealO2;
            float rawAgonyTimeRemaining = state.AgonyTimeRemaining;
            bool oxygenBridgeInvalid = !math.isfinite(rawRealO2) || !math.isfinite(rawAgonyTimeRemaining);
            float realO2 = math.saturate(ShinobuMetabolismJobMath.SanitizeFinite(rawRealO2, 1f));
            float agonyTimeRemaining = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(rawAgonyTimeRemaining, 0f));
            byte isInHypoxia = (byte)math.select(0, 1, state.IsInHypoxia != 0 && realO2 <= 0.0001f);
            float speedSq = ExertionSpeedSq != null ? math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(ExertionSpeedSq[index], 0f)) : 0f;
            float ambient = SampleAmbientTemperature(entityAup, tuning.AmbientFallbackTemperatureCelsius, q, ref flags);

            float suitConductance = suitProfile.ConductanceMultiplier * (1f - suitProfile.Insulation01);
            float thermalK = math.max(0f, tuning.TemperatureLossRate * rule.ThermalConductance * math.max(0.01f, suitConductance));
            float thermalDecay = ShinobuMetabolismJobMath.ApproximateExpNegPositive(thermalK * dt);
            if (!math.isfinite(thermalDecay))
            {
                thermalDecay = 1f;
                flags |= ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.NanDetected;
            }

            coreTemperature = math.clamp(ambient + (coreTemperature - ambient) * thermalDecay, 18f, 45f);
            coreTemperature = math.clamp(coreTemperature + suitProfile.BatteryHeatingCelsiusPerSecond * dt, 18f, 45f);
            float cold01 = math.saturate((rule.ShiverTemperatureCelsius - coreTemperature) * math.rcp(math.max(0.0001f, rule.ShiverTemperatureCelsius - rule.HypothermiaTemperatureCelsius)));
            float hot01 = math.saturate((ambient - rule.RecoveryTemperatureCelsius) * math.rcp(24f));
            float basalCalorieDrain = rule.BaseCalorieDrainPerSecond * tuning.BaseCalorieDrainScale;
            float exertionCalorieDrain = speedSq * tuning.ExertionMultiplier;
            float shiverCalorieDrain = basalCalorieDrain * cold01 * tuning.ShiverCalorieBoost * suitProfile.ShiverMultiplier;
            float calorieDrain = math.max(0f, basalCalorieDrain + exertionCalorieDrain + shiverCalorieDrain);
            float hydrationDrain = rule.BaseHydrationDrainPerSecond * tuning.BaseHydrationDrainScale * (1f + speedSq * tuning.ExertionHydrationMultiplier + hot01 * rule.HeatHydrationLossScale * suitProfile.HeatHydrationMultiplier);
            calories = math.max(0f, calories - calorieDrain * dt);
            hydration = math.max(0f, hydration - hydrationDrain * dt);

            float toxinSample = ToxinSamples != null ? math.saturate(ShinobuMetabolismJobMath.SanitizeFinite(ToxinSamples[index], 0f)) : 0f;
            toxinSample = math.max(toxinSample, SampleChemicalToxin(entityAup, q, ref flags));
            toxicity = math.clamp(
                toxicity + toxinSample * tuning.ToxinAccumulationPerSecond * rule.ToxinSusceptibility * dt - tuning.ToxinPurgePerSecond * dt,
                0f,
                8f);

            if (!math.isfinite(calories) || !math.isfinite(hydration) || !math.isfinite(coreTemperature) || !math.isfinite(toxicity))
            {
                calories = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(calories, 0f));
                hydration = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(hydration, 0f));
                coreTemperature = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(coreTemperature, rule.RecoveryTemperatureCelsius), 18f, 45f);
                toxicity = math.clamp(ShinobuMetabolismJobMath.SanitizeFinite(toxicity, 0f), 0f, 8f);
                flags |= ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.NanDetected;
            }

            if (oxygenBridgeInvalid)
                flags |= ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.NanDetected;

            if (calories <= 0.0001f)
                flags |= ShinobuMetabolismFlags.Starving;
            if (hydration <= 0.0001f)
                flags |= ShinobuMetabolismFlags.Dehydrated;
            if (coreTemperature <= rule.HypothermiaTemperatureCelsius)
                flags |= ShinobuMetabolismFlags.Hypothermia;
            if (toxicity >= 1f)
                flags |= ShinobuMetabolismFlags.Toxic;

            float calorieFatigue01 = math.saturate((rule.MaxCalories * 0.2f - calories) * math.rcp(math.max(0.0001f, rule.MaxCalories * 0.2f)));
            float hydrationFatigue01 = math.saturate((rule.MaxHydration * 0.1f - hydration) * math.rcp(math.max(0.0001f, rule.MaxHydration * 0.1f)));
            float fatigue01 = math.max(calorieFatigue01, hydrationFatigue01);
            if (fatigue01 > 0.0001f)
                flags |= ShinobuMetabolismFlags.Fatigue;
            flags = isInHypoxia != 0
                ? flags | ShinobuMetabolismFlags.Hypoxia
                : flags & ~ShinobuMetabolismFlags.Hypoxia;

            state.Calories = calories;
            state.Hydration = hydration;
            state.CoreTemperature = coreTemperature;
            state.Toxicity = toxicity;
            state.Flags = flags;
            state.Fatigue01 = math.saturate(fatigue01);
            state.RealO2 = realO2;
            state.AgonyTimeRemaining = agonyTimeRemaining;
            state.IsInHypoxia = isInHypoxia;

            if (index == 0)
                WriteDetailTelemetry(entityAup, state.EntityHashID, flags, ambient, thermalK, previousCoreTemperature, coreTemperature, calorieDrain, suitProfile.ProfileHash, dt);

            if (EmitSignals == 0)
                return;

            if ((flags & ShinobuMetabolismFlags.Starving) != 0u)
                StagePhysiologySignal(index, 0, state.EntityHashID, ShinobuMetabolismConstants.PhysiologyCauseStarvation, flags, calories, hydration, coreTemperature, toxicity);
            if ((flags & ShinobuMetabolismFlags.Dehydrated) != 0u)
                StagePhysiologySignal(index, 1, state.EntityHashID, ShinobuMetabolismConstants.PhysiologyCauseDehydration, flags, calories, hydration, coreTemperature, toxicity);
            if ((flags & ShinobuMetabolismFlags.Hypothermia) != 0u)
                StagePhysiologySignal(index, 2, state.EntityHashID, ShinobuMetabolismConstants.PhysiologyCauseHypothermia, flags, calories, hydration, coreTemperature, toxicity);
            if (toxicity >= 1f)
                StageToxicityExposure(index, state.EntityHashID, entityAup, rule.ToxicDamagePerSecond * tuning.ToxicDamageScale * (toxicity - 1f) * dt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ResolveEquippedSuitHash(int index)
        {
            if (SuitIntegrityStates == null || (uint)index >= (uint)SuitIntegrityStateCount)
                return 0u;

            SuitIntegrityDTO suitState = SuitIntegrityStates[index];
            return suitState.EquippedSuitHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MetabolicSuitThermalProfileDTO ResolveSuitProfile(int entityIndex, ushort suitProfileIndex, uint equippedSuitHash)
        {
            if (SuitProfiles == null || SuitProfileCount <= 0)
                return ShinobuMetabolismJobMath.BuildDefaultSuitProfile(ShinobuMetabolismConstants.StandardSuitHash);

            int index = math.clamp((int)suitProfileIndex, 0, SuitProfileCount - 1);
            MetabolicSuitThermalProfileDTO indexed = ShinobuMetabolismJobMath.SanitizeSuitProfile(SuitProfiles[index]);
            if (equippedSuitHash == 0u ||
                ShinobuMetabolismJobMath.SuitProfileHashMatches(indexed.ProfileHash, equippedSuitHash))
            {
                return indexed;
            }

            for (int i = 0; i < SuitProfileCount; i++)
            {
                MetabolicSuitThermalProfileDTO candidate = ShinobuMetabolismJobMath.SanitizeSuitProfile(SuitProfiles[i]);
                if (!ShinobuMetabolismJobMath.SuitProfileHashMatches(candidate.ProfileHash, equippedSuitHash))
                    continue;

                if (SuitProfileIndices != null && (uint)entityIndex < (uint)Count)
                    SuitProfileIndices[entityIndex] = (ushort)i;
                return candidate;
            }

            return indexed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDetailTelemetry(
            double3 playerAup,
            uint entityHash,
            uint flags,
            float ambient,
            float thermalK,
            float previousCore,
            float currentCore,
            float calorieDrainPerSecond,
            uint suitProfileHash,
            float dt)
        {
            if (DetailTelemetry == null || DetailTelemetryLength <= 0)
                return;

            int ringIndex = DetailTelemetryCursor % DetailTelemetryLength;
            if (ringIndex < 0)
                ringIndex += DetailTelemetryLength;

            float safeDt = math.max(0.0001f, ShinobuMetabolismJobMath.SanitizeFinite(dt, ShinobuMetabolismConstants.NominalSlowTickSeconds));
            MetabolicDetailTelemetryEntry entry = default;
            entry.PlayerAup = playerAup;
            entry.PlayerDepthMeters = ShinobuMetabolismJobMath.ResolveSeaLevelDepthMeters(playerAup.y, SeaLevelAupY);
            entry.ActiveCalorieBurnPerSecond = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(calorieDrainPerSecond, 0f));
            entry.AmbientCelsius = ShinobuMetabolismJobMath.SanitizeFinite(ambient, 0f);
            entry.ThermalK = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(thermalK, 0f));
            entry.CoreAmbientDeltaCelsius = ShinobuMetabolismJobMath.SanitizeFinite(currentCore - ambient, 0f);
            entry.ThermalDeltaCelsiusPerSecond = ShinobuMetabolismJobMath.SanitizeFinite((currentCore - previousCore) * math.rcp(safeDt), 0f);
            entry.Frame = Frame;
            entry.EntityHashID = entityHash;
            entry.Flags = flags;
            entry.SuitProfileHash = suitProfileHash;
            DetailTelemetry[ringIndex] = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearSignalSlots(int index)
        {
            if (PhysiologySignals != null)
            {
                int baseSlot = index * ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
                for (int slot = 0; slot < ShinobuMetabolismConstants.PhysiologySignalsPerEntity; slot++)
                {
                    int signalIndex = baseSlot + slot;
                    if ((uint)signalIndex < (uint)PhysiologySignalLength)
                        PhysiologySignals[signalIndex] = default;
                }
            }

            if (ExposureSignals != null)
            {
                int baseSlot = index * ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity;
                for (int slot = 0; slot < ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity; slot++)
                {
                    int signalIndex = baseSlot + slot;
                    if ((uint)signalIndex < (uint)ExposureSignalLength)
                        ExposureSignals[signalIndex] = default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleAmbientTemperature(double3 entityAup, float fallback, float quality, ref uint flags)
        {
            if (HasThermalGrid == 0 ||
                ThermalCelsiusGrid == null ||
                ThermalGridLength <= 0 ||
                ThermalGridResolution.x <= 0 ||
                ThermalGridResolution.y <= 0 ||
                ThermalGridResolution.z <= 0)
            {
                return fallback;
            }

            double3 localDouble = entityAup - ThermalGridRootAup;
            float3 local = default;
            local.x = (float)localDouble.x;
            local.y = (float)localDouble.y;
            local.z = (float)localDouble.z;
            if (!math.all(math.isfinite(local)))
            {
                flags |= ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.NanDetected;
                return fallback;
            }

            float cellSize = math.max(0.001f, ThermalCellSizeMeters);
            float3 grid = local * math.rcp(cellSize);
            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                grid.x >= ThermalGridResolution.x ||
                grid.y >= ThermalGridResolution.y ||
                grid.z >= ThermalGridResolution.z)
            {
                return fallback;
            }

            int3 baseCell = (int3)math.floor(grid);
            float3 fraction = math.frac(grid);
            int index = ThermalIndex(baseCell.x, baseCell.y, baseCell.z);
            float nearest = IsValidThermalIndex(index) ? ThermalCelsiusGrid[index] : fallback;
            nearest = ShinobuMetabolismJobMath.SanitizeFinite(nearest, fallback);
            float interpolationWeight = ShinobuMetabolismJobMath.ResolveThermalInterpolationWeight(quality);
            if (interpolationWeight <= 0f)
            {
                flags |= ShinobuMetabolismFlags.ThermalSampled;
                return nearest;
            }

            float trilinear = SampleTrilinear(baseCell, fraction, fallback);
            flags |= ShinobuMetabolismFlags.ThermalSampled;
            return math.lerp(nearest, trilinear, interpolationWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleTrilinear(int3 baseCell, float3 fraction, float fallback)
        {
            int3 maxCell = ThermalGridResolution - 1;
            int x1 = math.min(baseCell.x + 1, maxCell.x);
            int y1 = math.min(baseCell.y + 1, maxCell.y);
            int z1 = math.min(baseCell.z + 1, maxCell.z);
            float c000 = ReadThermal(baseCell.x, baseCell.y, baseCell.z, fallback);
            float c100 = ReadThermal(x1, baseCell.y, baseCell.z, fallback);
            float c010 = ReadThermal(baseCell.x, y1, baseCell.z, fallback);
            float c110 = ReadThermal(x1, y1, baseCell.z, fallback);
            float c001 = ReadThermal(baseCell.x, baseCell.y, z1, fallback);
            float c101 = ReadThermal(x1, baseCell.y, z1, fallback);
            float c011 = ReadThermal(baseCell.x, y1, z1, fallback);
            float c111 = ReadThermal(x1, y1, z1, fallback);
            float c00 = math.lerp(c000, c100, fraction.x);
            float c10 = math.lerp(c010, c110, fraction.x);
            float c01 = math.lerp(c001, c101, fraction.x);
            float c11 = math.lerp(c011, c111, fraction.x);
            float c0 = math.lerp(c00, c10, fraction.y);
            float c1 = math.lerp(c01, c11, fraction.y);
            return math.lerp(c0, c1, fraction.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadThermal(int x, int y, int z, float fallback)
        {
            int index = ThermalIndex(x, y, z);
            return IsValidThermalIndex(index) ? ShinobuMetabolismJobMath.SanitizeFinite(ThermalCelsiusGrid[index], fallback) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ThermalIndex(int x, int y, int z)
        {
            return x + (z * ThermalGridResolution.x) + (y * ThermalGridResolution.x * ThermalGridResolution.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidThermalIndex(int index)
        {
            return (uint)index < (uint)ThermalGridLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleChemicalToxin(double3 entityAup, float quality, ref uint flags)
        {
            if (HasChemicalGrid == 0 ||
                ChemicalPublishedGrid == null ||
                ChemicalGridLength <= 0 ||
                ChemicalGridResolution.x <= 0 ||
                ChemicalGridResolution.y <= 0 ||
                ChemicalGridResolution.z <= 0)
            {
                return 0f;
            }

            double3 localDouble = entityAup - ChemicalGridRootAup;
            float3 local = default;
            local.x = (float)localDouble.x;
            local.y = (float)localDouble.y;
            local.z = (float)localDouble.z;
            if (!math.all(math.isfinite(local)))
            {
                flags |= ShinobuMetabolismFlags.InvalidMath | ShinobuMetabolismFlags.NanDetected;
                return 0f;
            }

            float cellSize = math.max(0.001f, ChemicalCellSizeMeters);
            float3 grid = local * math.rcp(cellSize);
            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0f || grid.y < 0f || grid.z < 0f ||
                grid.x >= ChemicalGridResolution.x ||
                grid.y >= ChemicalGridResolution.y ||
                grid.z >= ChemicalGridResolution.z)
            {
                return 0f;
            }

            int3 baseCell = (int3)math.floor(grid);
            float3 fraction = math.frac(grid);
            float4 nearest = ReadChemical(ChemicalPublishedGrid, baseCell.x, baseCell.y, baseCell.z);
            float interpolationWeight = ShinobuMetabolismJobMath.ResolveThermalInterpolationWeight(quality);
            float4 sampled = nearest;
            if (interpolationWeight > 0f)
                sampled = math.lerp(nearest, SampleChemicalTrilinear(ChemicalPublishedGrid, baseCell, fraction), interpolationWeight);

            if (ChemicalOverlayGrid != null)
            {
                float4 overlayNearest = ReadChemical(ChemicalOverlayGrid, baseCell.x, baseCell.y, baseCell.z);
                float4 overlay = overlayNearest;
                if (interpolationWeight > 0f)
                    overlay = math.lerp(overlayNearest, SampleChemicalTrilinear(ChemicalOverlayGrid, baseCell, fraction), interpolationWeight);
                sampled.w = math.min(sampled.w, overlay.w);
            }

            flags |= ShinobuMetabolismFlags.ChemicalSampled;
            return math.saturate(math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(sampled.w, 0f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float4 SampleChemicalTrilinear(float4* grid, int3 baseCell, float3 fraction)
        {
            int maxX = ChemicalGridResolution.x - 1;
            int maxY = ChemicalGridResolution.y - 1;
            int maxZ = ChemicalGridResolution.z - 1;
            int x1 = math.min(baseCell.x + 1, maxX);
            int y1 = math.min(baseCell.y + 1, maxY);
            int z1 = math.min(baseCell.z + 1, maxZ);
            float4 c000 = ReadChemical(grid, baseCell.x, baseCell.y, baseCell.z);
            float4 c100 = ReadChemical(grid, x1, baseCell.y, baseCell.z);
            float4 c010 = ReadChemical(grid, baseCell.x, y1, baseCell.z);
            float4 c110 = ReadChemical(grid, x1, y1, baseCell.z);
            float4 c001 = ReadChemical(grid, baseCell.x, baseCell.y, z1);
            float4 c101 = ReadChemical(grid, x1, baseCell.y, z1);
            float4 c011 = ReadChemical(grid, baseCell.x, y1, z1);
            float4 c111 = ReadChemical(grid, x1, y1, z1);
            float4 c00 = math.lerp(c000, c100, fraction.x);
            float4 c10 = math.lerp(c010, c110, fraction.x);
            float4 c01 = math.lerp(c001, c101, fraction.x);
            float4 c11 = math.lerp(c011, c111, fraction.x);
            float4 c0 = math.lerp(c00, c10, fraction.y);
            float4 c1 = math.lerp(c01, c11, fraction.y);
            return math.lerp(c0, c1, fraction.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float4 ReadChemical(float4* grid, int x, int y, int z)
        {
            int index = ChemicalIndex(x, y, z);
            return grid != null && IsValidChemicalIndex(index) ? grid[index] : float4.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ChemicalIndex(int x, int y, int z)
        {
            return x + z * ChemicalGridResolution.x + y * ChemicalGridResolution.x * ChemicalGridResolution.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidChemicalIndex(int index)
        {
            return (uint)index < (uint)ChemicalGridLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StagePhysiologySignal(int index, int slot, uint entityHash, byte cause, uint flags, float calories, float hydration, float coreTemperature, float toxicity)
        {
            if (PhysiologySignals == null || entityHash == 0u)
                return;

            int signalIndex = index * ShinobuMetabolismConstants.PhysiologySignalsPerEntity + slot;
            if ((uint)signalIndex >= (uint)PhysiologySignalLength)
                return;

            PhysiologyStateSignal signal = default;
            signal.PlayerStress01 = math.saturate(math.max(math.max(1f - calories * math.rcp(100f), 1f - hydration * math.rcp(100f)), toxicity));
            signal.O2DrainMultiplier = 1f;
            signal.Recovery01 = math.saturate(coreTemperature * math.rcp(37f));
            signal.Frame = Frame;
            signal.Cause = cause;
            signal.Flags = (byte)math.select(0, 1, flags != 0u);
            signal.Supersaturation01 = math.saturate(toxicity);
            signal.Narcosis01 = 0f;
            signal.AmbientPressureAtm = 1f;
            signal.NitrogenLoadAtm = 0f;
            signal.AscentRateMetersPerSecond = 0f;
            signal.TissueOverMValueMask = 0u;
            signal.SourceHash = ShinobuMetabolismConstants.SourceHash;
            signal.EntityIndex = index;
            signal.ActiveCompartments = 0;
            signal.FatalSeverity = (byte)math.round(math.saturate(signal.PlayerStress01) * 255f);
            signal.StatusFlags = flags;
            PhysiologySignals[signalIndex] = signal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StageToxicityExposure(int index, uint entityHash, double3 entityAup, float magnitude)
        {
            if (ExposureSignals == null ||
                entityHash == 0u ||
                !math.all(math.isfinite(entityAup)) ||
                !math.isfinite(magnitude) ||
                magnitude <= 0f)
            {
                return;
            }

            int signalIndex = index * ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity +
                              ShinobuMetabolismConstants.MetabolicExposureSignalSlotToxic;
            if ((uint)signalIndex >= (uint)ExposureSignalLength)
                return;

            MetabolicExposureSignalDTO exposure = default;
            exposure.AUP = entityAup;
            exposure.EntityHash = entityHash;
            exposure.Frame = Frame;
            exposure.Exposure01 = math.saturate(magnitude);
            exposure.ToxemiaDelta = math.saturate(magnitude);
            exposure.ChemicalHash = 0u;
            exposure.Flags = 1u;
            ExposureSignals[signalIndex] = exposure;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MetabolismTelemetryJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public MetabolicTelemetryEntry* Telemetry;
        public int Count;
        public int TelemetryLength;
        public int TelemetryCursor;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute()
        {
            if (Telemetry == null || TelemetryLength <= 0)
                return;

            int count = math.max(0, Count);
            float sumTemperature = 0f;
            float minTemperature = 999f;
            float maxToxicity = 0f;
            uint starvation = 0u;
            uint dehydration = 0u;
            uint hypothermia = 0u;
            uint toxicityCount = 0u;
            uint activeCount = 0u;
            uint flags = 0u;
            uint firstInvalid = uint.MaxValue;
            ulong hash = 14695981039346656037UL;

            for (int i = 0; i < count; i++)
            {
                MetabolicStateDTO state = States[i];
                if (state.EntityHashID == 0u)
                    continue;

                activeCount++;
                bool invalid = !math.isfinite(state.Calories) ||
                               !math.isfinite(state.Hydration) ||
                               !math.isfinite(state.CoreTemperature) ||
                               !math.isfinite(state.Toxicity) ||
                               !math.isfinite(state.RealO2) ||
                               !math.isfinite(state.AgonyTimeRemaining);
                if (invalid)
                {
                    flags |= ShinobuMetabolismFlags.NanDetected | ShinobuMetabolismFlags.InvalidMath;
                    if (firstInvalid == uint.MaxValue)
                        firstInvalid = (uint)i;
                }

                float core = ShinobuMetabolismJobMath.SanitizeFinite(state.CoreTemperature, 37f);
                sumTemperature += core;
                minTemperature = math.min(minTemperature, core);
                maxToxicity = math.max(maxToxicity, ShinobuMetabolismJobMath.SanitizeFinite(state.Toxicity, 0f));
                starvation += (state.Flags & ShinobuMetabolismFlags.Starving) != 0u ? 1u : 0u;
                dehydration += (state.Flags & ShinobuMetabolismFlags.Dehydrated) != 0u ? 1u : 0u;
                hypothermia += (state.Flags & ShinobuMetabolismFlags.Hypothermia) != 0u ? 1u : 0u;
                toxicityCount += (state.Flags & ShinobuMetabolismFlags.Toxic) != 0u ? 1u : 0u;
                hash ^= state.EntityHashID;
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.Calories);
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.Hydration);
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.CoreTemperature);
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.Toxicity);
                hash *= 1099511628211UL;
                hash ^= state.Flags;
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.RealO2);
                hash *= 1099511628211UL;
                hash ^= math.asuint(state.AgonyTimeRemaining);
                hash *= 1099511628211UL;
                hash ^= state.IsInHypoxia;
                hash *= 1099511628211UL;
            }

            int ringIndex = TelemetryCursor % TelemetryLength;
            if (ringIndex < 0)
                ringIndex += TelemetryLength;

            float denominator = math.max(1f, activeCount);
            MetabolicTelemetryEntry entry = default;
            entry.StateHash = hash;
            entry.Frame = Frame;
            entry.EntityCount = activeCount;
            entry.AverageCoreTemperature = sumTemperature * math.rcp(denominator);
            entry.MinimumCoreTemperature = activeCount > 0u ? minTemperature : 0f;
            entry.MaximumToxicity = maxToxicity;
            entry.StarvationCount = starvation;
            entry.DehydrationCount = dehydration;
            entry.ToxicityCount = toxicityCount;
            entry.DeltaSeconds = DeltaSeconds;
            entry.ExecutionMicroseconds = 0f;
            entry.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            entry.Flags = flags;
            entry.FirstInvalidIndex = firstInvalid == uint.MaxValue ? 0u : firstInvalid;
            entry.SignalCount = starvation + dehydration + hypothermia + toxicityCount;
            Telemetry[ringIndex] = entry;
        }
    }
}
