using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// 64-bit fauna genome bit layout and Burst-safe mutation utilities.
    /// </summary>
    public static class FaunaGenome64
    {
        public const int SizeShift = 0;
        public const int SpeedShift = 8;
        public const int AggressionShift = 16;
        public const int HueShift = 24;
        public const int PatternIndexShift = 32;
        public const int BiolumFrequencyShift = 36;
        public const int FlagShift = 44;
        public const int ReservedShift = 48;
        public const ulong ByteMask = 0xFFUL;
        public const ulong NibbleMask = 0x0FUL;
        public const ulong HueMask = ByteMask;
        public const ulong MutationFlagRadiationBit = 1UL << 44;
        public const ulong MutationFlagBrineBit = 1UL << 45;
        public const ulong MutationFlagToxicityBit = 1UL << 46;
        public const ulong MutationFlagTwitchBit = 1UL << 47;
        public const ulong MutationFlagMask = 0x0000F00000000000UL;
        public const uint MutationFlagRadiation = 1u << 0;
        public const uint MutationFlagBrine = 1u << 1;
        public const uint MutationFlagToxicity = 1u << 2;
        public const uint MutationFlagTwitch = 1u << 3;
        public const uint ItemHash_ContaminatedMeat = 0x09E01466u;
        public const int GeneticMaskByteSize = 8;

        private const int YellowHue8 = 43;
        private const float InvGenomeByte = 1f / 255f;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static ulong BuildGenome(uint variationHash, float scaleMultiplier, float speedMultiplier)
        {
            int sizeByte = PackTraitByte(scaleMultiplier, 0.7f, 1.5f);
            int speedByte = PackTraitByte(speedMultiplier, 0.65f, 1.65f);
            int aggressionByte = 112 + (int)((variationHash >> 11) & 31u);
            int hueByte = (int)((variationHash ^ (variationHash >> 16)) & 0xFFu);
            int patternIndex = (int)((variationHash >> 23) & 0x0Fu);
            int biolumFrequency = (int)((variationHash >> 3) & 0xFFu);
            return PackGeneticMask(
                sizeByte,
                speedByte,
                aggressionByte,
                hueByte,
                patternIndex,
                biolumFrequency,
                0u,
                variationHash);
        }

        public static ulong CompileGeneticMaskFromAup(
            in AbsoluteUniversePosition spawnAup,
            uint worldSeed,
            uint speciesHash,
            uint rollIndex)
        {
            uint seed = BuildAupSeed(in spawnAup, worldSeed, speciesHash, rollIndex);
            return CompileGeneticMaskFromSeed(seed);
        }

        public static ulong CompileGeneticMaskFromDoubleAup(
            double3 spawnAup,
            uint worldSeed,
            uint speciesHash,
            uint rollIndex)
        {
            uint seed = BuildDoubleAupSeed(spawnAup, worldSeed, speciesHash, rollIndex);
            return CompileGeneticMaskFromSeed(seed);
        }

        public static ulong CompileGeneticMaskFromSeed(uint seed)
        {
            Unity.Mathematics.Random rng = CreateGeneticsRandom(seed);
            uint sizeRoll = rng.NextUInt();
            uint speedRoll = rng.NextUInt();
            uint aggressionRoll = rng.NextUInt();
            uint hueRoll = rng.NextUInt();
            uint biolumRoll = rng.NextUInt();
            return PackGeneticMask(
                (int)(sizeRoll & 0xFFu),
                (int)(speedRoll & 0xFFu),
                (int)(aggressionRoll & 0xFFu),
                (int)(hueRoll & 0xFFu),
                (int)((hueRoll >> 8) & 0x0Fu),
                (int)((biolumRoll >> 5) & 0xFFu),
                0u,
                seed);
        }

        public static ulong CompileGeneticMaskFromSeed(
            uint seed,
            in FaunaGeneticsTuningDTO tuning,
            in FaunaGeneticsProfileDTO profile)
        {
            ulong mask = CompileGeneticMaskFromSeed(seed);
            return ApplyTuningAndProfile(mask, in tuning, in profile);
        }

        public static ulong ApplyTuningAndProfile(
            ulong geneticMask,
            in FaunaGeneticsTuningDTO tuning,
            in FaunaGeneticsProfileDTO profile)
        {
            FaunaGeneticsTuningDTO safeTuning = tuning.StateHash != 0u
                ? FaunaGeneticsTuningDTO.Sanitize(tuning)
                : FaunaGeneticsTuningDTO.CreateDefault();
            int sizeByte = ExtractSizeByte(geneticMask);
            int speedByte = ExtractSpeedByte(geneticMask);
            int aggressionByte = ExtractAggressionByte(geneticMask);
            int hueByte = ExtractHueByte(geneticMask);
            int patternIndex = ExtractPatternIndex(geneticMask);
            int biolumFrequency = ExtractBiolumFrequencyByte(geneticMask);

            float size01 = math.saturate((sizeByte * InvGenomeByte - 0.5f) * safeTuning.BaseSizeScalar + 0.5f);
            size01 = math.lerp(safeTuning.MinimumSizeScalar, safeTuning.MaximumSizeScalar, size01);
            sizeByte = (int)math.clamp(math.round(size01 * 255f), 0f, 255f);
            float aggression01 = math.saturate(aggressionByte * InvGenomeByte);
            aggression01 = math.lerp(safeTuning.MinimumAggressionScalar, safeTuning.MaximumAggressionScalar, aggression01);
            aggressionByte = (int)math.clamp(math.round(aggression01 * 255f), 0f, 255f);
            float biolum01 = math.saturate(biolumFrequency * InvGenomeByte);
            biolum01 = math.lerp(safeTuning.MinimumBiolumFrequency, safeTuning.MaximumBiolumFrequency, biolum01);
            biolumFrequency = (int)math.clamp(math.round(biolum01 * 255f), 0f, 255f);
            hueByte = (int)math.clamp(math.round(hueByte * safeTuning.HueShiftRange), 0f, 255f);

            if (profile.SpeciesHash != 0u)
            {
                int minSize = profile.MinSizeByte;
                int maxSize = math.max(minSize, profile.MaxSizeByte);
                int minAggression = profile.MinAggressionByte;
                int maxAggression = math.max(minAggression, profile.MaxAggressionByte);
                sizeByte = math.clamp(sizeByte, minSize, maxSize);
                aggressionByte = math.clamp(aggressionByte, minAggression, maxAggression);
                hueByte = (int)math.clamp(math.round(hueByte * (profile.HueRangeByte * InvGenomeByte)), 0f, 255f);
                patternIndex &= profile.PatternMask & 0x0F;
                biolumFrequency = math.min(biolumFrequency, profile.BiolumFrequencyByte);
            }

            return PackGeneticMask(
                sizeByte,
                speedByte,
                aggressionByte,
                hueByte,
                patternIndex,
                biolumFrequency,
                ExtractMutationFlags(geneticMask),
                (uint)(geneticMask >> ReservedShift));
        }

        public static ulong PackGeneticMask(
            int sizeByte,
            int speedByte,
            int aggressionByte,
            int hueByte,
            int patternIndex,
            int biolumFrequencyByte,
            uint mutationFlags,
            uint variationHash)
        {
            return ((ulong)(byte)math.clamp(sizeByte, 0, 255) << SizeShift) |
                   ((ulong)(byte)math.clamp(speedByte, 0, 255) << SpeedShift) |
                   ((ulong)(byte)math.clamp(aggressionByte, 0, 255) << AggressionShift) |
                   ((ulong)(byte)math.clamp(hueByte, 0, 255) << HueShift) |
                   ((ulong)(byte)(math.clamp(patternIndex, 0, 15) & 0x0F) << PatternIndexShift) |
                   ((ulong)(byte)math.clamp(biolumFrequencyByte, 0, 255) << BiolumFrequencyShift) |
                   (((ulong)mutationFlags & NibbleMask) << FlagShift) |
                   ((ulong)(variationHash & 0xFFFFu) << ReservedShift);
        }

        public static FaunaGeneticTraits ResolveRuntimeTraitsFromGenome(FaunaGeneticTraits baseTraits, ulong genome)
        {
            float baseScale = SanitizePositiveMultiplier(
                baseTraits.BaseScaleMultiplier,
                SanitizePositiveMultiplier(baseTraits.ScaleMultiplier, 1f));
            float baseSpeed = SanitizePositiveMultiplier(
                baseTraits.BaseSpeedMultiplier,
                SanitizePositiveMultiplier(baseTraits.SpeedMultiplier, 1f));
            float baseHealth = SanitizePositiveMultiplier(
                baseTraits.BaseHealthMultiplier,
                SanitizePositiveMultiplier(baseTraits.HealthMultiplier, 1f));
            ulong baseGenome = baseTraits.BaseGenome != 0UL
                ? baseTraits.BaseGenome
                : BuildGenome(baseTraits.VariationHash, baseScale, baseSpeed);
            ulong resolvedGenome = genome != 0UL ? genome : baseGenome;

            int sizeDelta = ExtractByte(resolvedGenome, SizeShift) - ExtractByte(baseGenome, SizeShift);
            int speedDelta = ExtractByte(resolvedGenome, SpeedShift) - ExtractByte(baseGenome, SpeedShift);
            int aggressionDelta = ExtractByte(resolvedGenome, AggressionShift) - ExtractByte(baseGenome, AggressionShift);
            uint flags = ExtractMutationFlags(resolvedGenome);

            FaunaGeneticTraits traits = baseTraits;
            traits.BaseScaleMultiplier = baseScale;
            traits.BaseSpeedMultiplier = baseSpeed;
            traits.BaseHealthMultiplier = baseHealth;
            traits.BaseGenome = baseGenome;
            traits.Genome = resolvedGenome;
            traits.ScaleMultiplier = math.clamp(baseScale * (1f + sizeDelta * 0.0045f), 0.7f, 1.5f);
            traits.SpeedMultiplier = math.clamp(baseSpeed * (1f + speedDelta * 0.004f), 0.65f, 1.7f);
            traits.HealthMultiplier = math.clamp(baseHealth * (1f + math.max(0, sizeDelta) * 0.0035f), 0.65f, 2f);
            traits.AggressionMultiplier = math.clamp(1f + aggressionDelta * 0.006f, 0.75f, 2.25f);
            traits.HueOffset01 = ExtractHueShift01(resolvedGenome);
            traits.MutationHueShift01 = ResolveSicklyHueIntensity01(resolvedGenome);
            traits.MutationTwitch01 = ResolveTwitchIntensity01(resolvedGenome);
            traits.MutationFlags = flags;
            traits.ContaminatedMeatHash = flags != 0u ? ItemHash_ContaminatedMeat : 0u;
            return traits;
        }

        public static ulong MutateGenome(
            ulong genome,
            uint stableHash,
            float radiationRads,
            float toxicity01,
            float brineDepth01,
            uint rollIndex,
            out byte resultFlags)
        {
            resultFlags = 0;
            radiationRads = SanitizeScalar01(radiationRads);
            toxicity01 = SanitizeScalar01(toxicity01);
            brineDepth01 = SanitizeScalar01(brineDepth01);
            ulong resolvedGenome = genome != 0UL ? genome : BuildGenome(stableHash, 1f, 1f);
            int sizeByte = ExtractByte(resolvedGenome, SizeShift);
            int speedByte = ExtractByte(resolvedGenome, SpeedShift);
            int aggressionByte = ExtractByte(resolvedGenome, AggressionShift);
            int hue8 = ExtractHueByte(resolvedGenome);
            ulong flags = resolvedGenome & MutationFlagMask;
            uint rngSeed = stableHash ^ (uint)resolvedGenome ^ (uint)(resolvedGenome >> 32) ^ (rollIndex * 0x9E3779B9u);
            Unity.Mathematics.Random rng = CreateGeneticsRandom(rngSeed);
            uint radiationRoll = rng.NextUInt();

            if (radiationRads > 0.5f)
            {
                uint chanceMask = radiationRads > 0.85f ? 0x1u : 0x3u;
                if ((radiationRoll & chanceMask) == 0u)
                {
                    int speedStep = 1 + (int)((radiationRoll >> 4) & 7u);
                    int aggressionStep = 1 + (int)((radiationRoll >> 8) & 7u);
                    speedByte = math.min(255, speedByte + speedStep);
                    aggressionByte = math.min(255, aggressionByte + aggressionStep);
                    flags |= MutationFlagRadiationBit | MutationFlagTwitchBit;
                    resultFlags |= (byte)(MutationFlagRadiation | MutationFlagTwitch);
                }
            }

            if (brineDepth01 > 0.001f)
            {
                uint brineRoll = rng.NextUInt();
                int sizeStep = 1 + (int)((brineRoll >> 3) & 3u);
                sizeByte = math.min(255, sizeByte + sizeStep);
                hue8 = MoveHueTowardYellow(hue8, 3);
                flags |= MutationFlagBrineBit | MutationFlagTwitchBit;
                resultFlags |= (byte)(MutationFlagBrine | MutationFlagTwitch);
            }

            if (toxicity01 > 0.05f)
            {
                uint toxicRoll = rng.NextUInt();
                int aggressionStep = 1 + (int)((toxicRoll >> 6) & 3u);
                aggressionByte = math.min(255, aggressionByte + aggressionStep);
                hue8 = MoveHueTowardYellow(hue8, 4);
                flags |= MutationFlagToxicityBit | MutationFlagTwitchBit;
                resultFlags |= (byte)(MutationFlagToxicity | MutationFlagTwitch);
            }

            return PackGeneticMask(
                sizeByte,
                speedByte,
                aggressionByte,
                hue8,
                ExtractPatternIndex(resolvedGenome),
                ExtractBiolumFrequencyByte(resolvedGenome),
                (uint)((flags >> FlagShift) & NibbleMask),
                (uint)(resolvedGenome >> ReservedShift));
        }

        public static uint ExtractMutationFlags(ulong genome)
        {
            return (uint)((genome >> FlagShift) & NibbleMask);
        }

        public static bool HasContaminatedYield(ulong genome)
        {
            return (genome & MutationFlagMask) != 0UL;
        }

        public static float SanitizeScalar01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        public static float ResolveTwitchIntensity01(ulong genome)
        {
            if ((genome & MutationFlagTwitchBit) == 0UL)
                return 0f;

            uint flags = ExtractMutationFlags(genome);
            float strength = 0.2f + ((flags & 0x0Fu) * 0.06f);
            return math.saturate(strength);
        }

        public static float ResolveSicklyHueIntensity01(ulong genome)
        {
            uint flags = ExtractMutationFlags(genome);
            if ((flags & (MutationFlagBrine | MutationFlagToxicity | MutationFlagRadiation)) == 0u)
                return 0f;

            float strength = 0.18f;
            if ((flags & MutationFlagBrine) != 0u)
                strength += 0.22f;
            if ((flags & MutationFlagToxicity) != 0u)
                strength += 0.18f;
            if ((flags & MutationFlagRadiation) != 0u)
                strength += 0.08f;

            return math.saturate(strength);
        }

        public static int ExtractSizeByte(ulong genome)
        {
            return ExtractByte(genome, SizeShift);
        }

        public static int ExtractSpeedByte(ulong genome)
        {
            return ExtractByte(genome, SpeedShift);
        }

        public static int ExtractAggressionByte(ulong genome)
        {
            return ExtractByte(genome, AggressionShift);
        }

        public static int ExtractHueByte(ulong genome)
        {
            return ExtractByte(genome, HueShift);
        }

        public static int ExtractPatternIndex(ulong genome)
        {
            return (int)((genome >> PatternIndexShift) & NibbleMask);
        }

        public static int ExtractBiolumFrequencyByte(ulong genome)
        {
            return ExtractByte(genome, BiolumFrequencyShift);
        }

        public static FaunaGeneticsProfileDTO ResolveProfile(
            NativeArray<FaunaGeneticsProfileDTO> profiles,
            uint speciesHash)
        {
            if (!profiles.IsCreated || speciesHash == 0u)
                return default;

            for (int i = 0; i < profiles.Length; i++)
            {
                FaunaGeneticsProfileDTO profile = profiles[i];
                if (profile.SpeciesHash == speciesHash)
                    return profile;
                if (profile.SpeciesHash == 0u)
                    return default;
            }

            return default;
        }

        public static FaunaGeneticsProfileDTO ResolveProfile(
            NativeArray<FaunaGeneticsProfileDTO>.ReadOnly profiles,
            uint speciesHash)
        {
            if (speciesHash == 0u || profiles.Length <= 0)
                return default;

            for (int i = 0; i < profiles.Length; i++)
            {
                FaunaGeneticsProfileDTO profile = profiles[i];
                if (profile.SpeciesHash == speciesHash)
                    return profile;
                if (profile.SpeciesHash == 0u)
                    return default;
            }

            return default;
        }

        public static float ExtractScaleMultiplier(ulong geneticMask)
        {
            return 0.8f + ExtractSizeByte(geneticMask) * InvGenomeByte * 0.4f;
        }

        public static float ExtractSpeedMultiplier(ulong geneticMask)
        {
            return 1f + ExtractSpeedByte(geneticMask) * InvGenomeByte * 0.5f;
        }

        public static float ExtractAggressionMultiplier(ulong geneticMask)
        {
            return 0.75f + ExtractAggressionByte(geneticMask) * InvGenomeByte * 1.5f;
        }

        public static float ExtractHueShift01(ulong geneticMask)
        {
            return ExtractHueByte(geneticMask) * InvGenomeByte;
        }

        public static float ExtractBiolumFrequency01(ulong geneticMask)
        {
            return ExtractBiolumFrequencyByte(geneticMask) * InvGenomeByte;
        }

        public static uint BuildAupSeed(
            in AbsoluteUniversePosition aup,
            uint worldSeed,
            uint speciesHash,
            uint rollIndex)
        {
            uint hash = FnvOffset;
            hash = Fold(hash, worldSeed);
            hash = Fold(hash, speciesHash);
            hash = Fold(hash, rollIndex);
            hash = Fold(hash, (uint)aup.GridX);
            hash = Fold(hash, (uint)((ulong)aup.GridX >> 32));
            hash = Fold(hash, (uint)aup.GridY);
            hash = Fold(hash, (uint)((ulong)aup.GridY >> 32));
            hash = Fold(hash, (uint)aup.GridZ);
            hash = Fold(hash, (uint)((ulong)aup.GridZ >> 32));
            hash = Fold(hash, QuantizeMetersToMillimeters(aup.LocalX));
            hash = Fold(hash, QuantizeMetersToMillimeters(aup.LocalY));
            hash = Fold(hash, QuantizeMetersToMillimeters(aup.LocalZ));
            return hash != 0u ? Mix(hash) : 1u;
        }

        public static uint BuildDoubleAupSeed(
            double3 aup,
            uint worldSeed,
            uint speciesHash,
            uint rollIndex)
        {
            uint hash = FnvOffset;
            hash = Fold(hash, worldSeed);
            hash = Fold(hash, speciesHash);
            hash = Fold(hash, rollIndex);
            double x = math.select(0d, aup.x, math.isfinite(aup.x));
            double y = math.select(0d, aup.y, math.isfinite(aup.y));
            double z = math.select(0d, aup.z, math.isfinite(aup.z));
            hash = FoldDoubleBits(hash, x);
            hash = FoldDoubleBits(hash, y);
            hash = FoldDoubleBits(hash, z);
            hash = FoldQuantizedDoubleMillimeters(hash, x);
            hash = FoldQuantizedDoubleMillimeters(hash, y);
            hash = FoldQuantizedDoubleMillimeters(hash, z);
            return hash != 0u ? Mix(hash) : 1u;
        }

        public static uint BuildStableEntitySeed(
            uint stableSeed,
            uint speciesHash,
            uint salt)
        {
            uint hash = FnvOffset;
            hash = Fold(hash, stableSeed != 0u ? stableSeed : 1u);
            hash = Fold(hash, speciesHash);
            hash = Fold(hash, salt);
            return hash != 0u ? Mix(hash) : 1u;
        }

        private static int PackTraitByte(float value, float min, float max)
        {
            value = math.isfinite(value) ? value : min;
            float range = math.max(0.001f, max - min);
            return (int)math.clamp(math.round(math.saturate((value - min) * math.rcp(range)) * 255f), 0f, 255f);
        }

        private static float SanitizePositiveMultiplier(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f
                ? value
                : math.max(0.001f, fallback);
        }

        private static int ExtractByte(ulong genome, int shift)
        {
            return (int)((genome >> shift) & ByteMask);
        }

        private static int MoveHueTowardYellow(int hue16, int shift)
        {
            int delta = YellowHue8 - math.clamp(hue16, 0, 255);
            return math.clamp(hue16 + (delta >> math.clamp(shift, 1, 8)), 0, 255);
        }

        private static uint Fold(uint hash, uint value)
        {
            return (hash ^ value) * FnvPrime;
        }

        private static uint QuantizeMetersToMillimeters(float value)
        {
            float safe = math.select(0f, value, math.isfinite(value));
            return unchecked((uint)(int)math.clamp(math.round(safe * 1000f), int.MinValue, int.MaxValue));
        }

        private static uint FoldQuantizedDoubleMillimeters(uint hash, double value)
        {
            double safe = math.select(0d, value, math.isfinite(value));
            long millimeters = (long)math.round(math.clamp(safe * 1000d, long.MinValue, long.MaxValue));
            ulong folded = unchecked((ulong)millimeters);
            hash = Fold(hash, (uint)folded);
            return Fold(hash, (uint)(folded >> 32));
        }

        private static uint FoldDoubleBits(uint hash, double value)
        {
            ulong bits = math.asulong(value);
            hash = Fold(hash, (uint)bits);
            return Fold(hash, (uint)(bits >> 32));
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static Unity.Mathematics.Random CreateGeneticsRandom(uint seed)
        {
            uint index = seed == uint.MaxValue ? 0x306FAE31u : seed;
            return Unity.Mathematics.Random.CreateFromIndex(index);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FaunaGeneticsTuningDTO
    {
        [FieldOffset(0)] public float BaseSizeScalar;
        [FieldOffset(4)] public float HueShiftRange;
        [FieldOffset(8)] public float MutationProbability;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public uint ProfileCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint CsvByteCount;
        [FieldOffset(32)] public float MinimumSizeScalar;
        [FieldOffset(36)] public float MaximumSizeScalar;
        [FieldOffset(40)] public float MinimumAggressionScalar;
        [FieldOffset(44)] public float MaximumAggressionScalar;
        [FieldOffset(48)] public float MinimumBiolumFrequency;
        [FieldOffset(52)] public float MaximumBiolumFrequency;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;

        public static FaunaGeneticsTuningDTO CreateDefault()
        {
            return new FaunaGeneticsTuningDTO
            {
                BaseSizeScalar = 1f,
                HueShiftRange = 1f,
                MutationProbability = 0.04f,
                GlobalQualityWeight = 1f,
                MinimumSizeScalar = 0f,
                MaximumSizeScalar = 1f,
                MinimumAggressionScalar = 0f,
                MaximumAggressionScalar = 1f,
                MinimumBiolumFrequency = 0f,
                MaximumBiolumFrequency = 1f,
                StateHash = 0x46474E54u
            };
        }

        public static FaunaGeneticsTuningDTO Sanitize(FaunaGeneticsTuningDTO tuning)
        {
            tuning.BaseSizeScalar = math.clamp(Safe(tuning.BaseSizeScalar, 1f), 0.1f, 4f);
            tuning.HueShiftRange = math.saturate(Safe(tuning.HueShiftRange, 1f));
            tuning.MutationProbability = math.saturate(Safe(tuning.MutationProbability, 0f));
            tuning.GlobalQualityWeight = math.saturate(Safe(tuning.GlobalQualityWeight, 1f));
            tuning.MinimumSizeScalar = math.saturate(Safe(tuning.MinimumSizeScalar, 0f));
            tuning.MaximumSizeScalar = math.saturate(Safe(tuning.MaximumSizeScalar, 1f));
            tuning.MinimumAggressionScalar = math.saturate(Safe(tuning.MinimumAggressionScalar, 0f));
            tuning.MaximumAggressionScalar = math.saturate(Safe(tuning.MaximumAggressionScalar, 1f));
            tuning.MinimumBiolumFrequency = math.saturate(Safe(tuning.MinimumBiolumFrequency, 0f));
            tuning.MaximumBiolumFrequency = math.saturate(Safe(tuning.MaximumBiolumFrequency, 1f));
            if (tuning.MaximumSizeScalar < tuning.MinimumSizeScalar)
                tuning.MaximumSizeScalar = tuning.MinimumSizeScalar;
            if (tuning.MaximumAggressionScalar < tuning.MinimumAggressionScalar)
                tuning.MaximumAggressionScalar = tuning.MinimumAggressionScalar;
            if (tuning.MaximumBiolumFrequency < tuning.MinimumBiolumFrequency)
                tuning.MaximumBiolumFrequency = tuning.MinimumBiolumFrequency;
            tuning.StateHash = MixTuningStateHash(in tuning);
            return tuning;
        }

        private static float Safe(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static uint MixTuningStateHash(in FaunaGeneticsTuningDTO tuning)
        {
            uint hash = 2166136261u;
            hash = Fold(hash, math.asuint(tuning.BaseSizeScalar));
            hash = Fold(hash, math.asuint(tuning.HueShiftRange));
            hash = Fold(hash, math.asuint(tuning.MutationProbability));
            hash = Fold(hash, tuning.ProfileCount);
            hash = Fold(hash, tuning.CsvByteCount);
            return hash == 0u ? 1u : hash;
        }

        private static uint Fold(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FaunaGeneticsProfileDTO
    {
        [FieldOffset(0)] public ulong ProfileHash;
        [FieldOffset(8)] public ulong Reserved0;
        [FieldOffset(16)] public uint SpeciesHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public byte MinSizeByte;
        [FieldOffset(25)] public byte MaxSizeByte;
        [FieldOffset(26)] public byte MinAggressionByte;
        [FieldOffset(27)] public byte MaxAggressionByte;
        [FieldOffset(28)] public byte HueRangeByte;
        [FieldOffset(29)] public byte PatternMask;
        [FieldOffset(30)] public byte MutationProbabilityByte;
        [FieldOffset(31)] public byte BiolumFrequencyByte;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GeneticsTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int CompiledGenomeCount;
        [FieldOffset(12)] public int ActiveGenomeCount;
        [FieldOffset(16)] public int ExtractionOperationCount;
        [FieldOffset(20)] public int InvalidMaskCount;
        [FieldOffset(24)] public float AverageHueShift01;
        [FieldOffset(28)] public float AverageSize01;
        [FieldOffset(32)] public float AverageAggression01;
        [FieldOffset(36)] public float AveragePattern01;
        [FieldOffset(40)] public float BurstExecutionMicroseconds;
        [FieldOffset(44)] public uint TuningStateHash;
        [FieldOffset(48)] public uint PatternHistogramLo;
        [FieldOffset(52)] public uint PatternHistogramHi;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved0;
    }

    #if UNITY_EDITOR
    public static unsafe class FaunaGeneticsProfileCsv
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Comment = (byte)'#';

        public static bool TryApplyProfiles(
            ReadOnlySpan<byte> csv,
            NativeArray<FaunaGeneticsProfileDTO> profiles,
            ref FaunaGeneticsTuningDTO tuning,
            out int updatedCount)
        {
            updatedCount = 0;
            if (csv.Length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
                return false;

            fixed (byte* fixedBytes = csv)
            {
                byte* bytes = fixedBytes;
                int byteCount = csv.Length;
                int cursor = ResolveBomOffset(bytes, byteCount);
                while (cursor < byteCount)
                {
                    int lineStart = cursor;
                    while (cursor < byteCount && bytes[cursor] != LineFeed)
                        cursor++;

                    int lineEnd = cursor;
                    if (cursor < byteCount && bytes[cursor] == LineFeed)
                        cursor++;

                    while (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                        lineEnd--;

                    if (TryParseLine(bytes, lineStart, lineEnd, out FaunaGeneticsProfileDTO profile))
                    {
                        int slot = ResolveProfileSlot(profiles, profile.SpeciesHash, updatedCount);
                        if (slot >= 0)
                        {
                            profiles[slot] = profile;
                            updatedCount = math.max(updatedCount, slot + 1);
                        }
                    }
                }
            }

            tuning.ProfileCount = (uint)math.min(updatedCount, profiles.Length);
            tuning.CsvByteCount = (uint)math.min(csv.Length, int.MaxValue);
            tuning = FaunaGeneticsTuningDTO.Sanitize(tuning);
            return updatedCount > 0;
        }

        private static int ResolveProfileSlot(NativeArray<FaunaGeneticsProfileDTO> profiles, uint speciesHash, int fallback)
        {
            if (speciesHash == 0u)
                return -1;

            for (int i = 0; i < profiles.Length; i++)
            {
                uint existing = profiles[i].SpeciesHash;
                if (existing == speciesHash)
                    return i;
                if (existing == 0u)
                    return i;
            }

            return fallback >= 0 && fallback < profiles.Length ? fallback : -1;
        }

        private static bool TryParseLine(byte* bytes, int start, int end, out FaunaGeneticsProfileDTO profile)
        {
            profile = default;
            int first = TrimLeft(bytes, start, end);
            if (first >= end || bytes[first] == Comment || IsHeader(bytes, first, end))
                return false;

            int cursor = start;
            ConsumeCell(bytes, end, ref cursor, out int speciesStart, out int speciesEnd);
            ConsumeCell(bytes, end, ref cursor, out int sizeMinStart, out int sizeMinEnd);
            ConsumeCell(bytes, end, ref cursor, out int sizeMaxStart, out int sizeMaxEnd);
            ConsumeCell(bytes, end, ref cursor, out int aggressionMinStart, out int aggressionMinEnd);
            ConsumeCell(bytes, end, ref cursor, out int aggressionMaxStart, out int aggressionMaxEnd);
            ConsumeCell(bytes, end, ref cursor, out int hueRangeStart, out int hueRangeEnd);
            ConsumeCell(bytes, end, ref cursor, out int patternStart, out int patternEnd);
            ConsumeCell(bytes, end, ref cursor, out int mutationStart, out int mutationEnd);
            ConsumeCell(bytes, end, ref cursor, out int biolumStart, out int biolumEnd);

            uint speciesHash = ParseSpeciesHash(bytes, speciesStart, speciesEnd);
            if (speciesHash == 0u)
                return false;

            byte minSize = ParseUnitByte(bytes, sizeMinStart, sizeMinEnd, 0);
            byte maxSize = ParseUnitByte(bytes, sizeMaxStart, sizeMaxEnd, 255);
            byte minAggression = ParseUnitByte(bytes, aggressionMinStart, aggressionMinEnd, 0);
            byte maxAggression = ParseUnitByte(bytes, aggressionMaxStart, aggressionMaxEnd, 255);
            if (maxSize < minSize)
                maxSize = minSize;
            if (maxAggression < minAggression)
                maxAggression = minAggression;

            byte patternMask = (byte)(ParseUInt(bytes, patternStart, patternEnd, 15u) & 0x0Fu);
            profile.SpeciesHash = speciesHash;
            profile.MinSizeByte = minSize;
            profile.MaxSizeByte = maxSize;
            profile.MinAggressionByte = minAggression;
            profile.MaxAggressionByte = maxAggression;
            profile.HueRangeByte = ParseUnitByte(bytes, hueRangeStart, hueRangeEnd, 255);
            profile.PatternMask = patternMask;
            profile.MutationProbabilityByte = ParseUnitByte(bytes, mutationStart, mutationEnd, 0);
            profile.BiolumFrequencyByte = ParseUnitByte(bytes, biolumStart, biolumEnd, 255);
            profile.ProfileHash = ((ulong)speciesHash << 32) | ParseUInt(bytes, start, end, speciesHash);
            return true;
        }

        private static void ConsumeCell(byte* bytes, int rowEnd, ref int cursor, out int start, out int end)
        {
            start = TrimLeft(bytes, cursor, rowEnd);
            while (cursor < rowEnd && bytes[cursor] != Comma)
                cursor++;

            end = TrimRight(bytes, start, cursor);
            if (cursor < rowEnd && bytes[cursor] == Comma)
                cursor++;
        }

        private static uint ParseSpeciesHash(byte* bytes, int start, int end)
        {
            if (TryParseUInt(bytes, start, end, out uint numeric))
                return numeric == 0u ? 1u : numeric;

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte value = ToLowerAscii(bytes[i]);
                if (value <= 32)
                    continue;

                hash = (hash ^ value) * 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static byte ParseUnitByte(byte* bytes, int start, int end, byte fallback)
        {
            if (!TryParseFloat(bytes, start, end, out float value))
                return fallback;

            float normalized = value > 1f ? value * (1f / 255f) : value;
            return (byte)math.clamp((int)math.round(math.saturate(normalized) * 255f), 0, 255);
        }

        private static bool TryParseFloat(byte* bytes, int start, int end, out float value)
        {
            value = 0f;
            start = TrimLeft(bytes, start, end);
            end = TrimRight(bytes, start, end);
            if (end <= start)
                return false;

            int cursor = start;
            float sign = 1f;
            if (bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            double whole = 0d;
            bool any = false;
            while (cursor < end && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                whole = whole * 10d + (bytes[cursor] - (byte)'0');
                cursor++;
                any = true;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (cursor < end && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < end && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    fraction = fraction * 10d + (bytes[cursor] - (byte)'0');
                    divisor *= 10d;
                    cursor++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = (float)(sign * (whole + fraction / math.max(1d, divisor)));
            return math.isfinite(value);
        }

        private static uint ParseUInt(byte* bytes, int start, int end, uint fallback)
        {
            return TryParseUInt(bytes, start, end, out uint value) ? value : fallback;
        }

        private static bool TryParseUInt(byte* bytes, int start, int end, out uint value)
        {
            value = 0u;
            start = TrimLeft(bytes, start, end);
            end = TrimRight(bytes, start, end);
            if (end <= start)
                return false;

            int cursor = start;
            bool hex = false;
            if (cursor + 1 < end &&
                bytes[cursor] == (byte)'0' &&
                (bytes[cursor + 1] == (byte)'x' || bytes[cursor + 1] == (byte)'X'))
            {
                hex = true;
                cursor += 2;
            }

            bool any = false;
            for (; cursor < end; cursor++)
            {
                byte c = bytes[cursor];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(10 + c - (byte)'a');
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(10 + c - (byte)'A');
                else
                    return false;

                value = hex ? (value << 4) | digit : value * 10u + digit;
                any = true;
            }

            return any;
        }

        private static int TrimLeft(byte* bytes, int start, int end)
        {
            while (start < end && bytes[start] <= 32)
                start++;
            return start;
        }

        private static int TrimRight(byte* bytes, int start, int end)
        {
            while (end > start && bytes[end - 1] <= 32)
                end--;
            return end;
        }

        private static int ResolveBomOffset(byte* bytes, int length)
        {
            return length >= 3 &&
                   bytes[0] == 0xEF &&
                   bytes[1] == 0xBB &&
                   bytes[2] == 0xBF
                ? 3
                : 0;
        }

        private static bool IsHeader(byte* bytes, int start, int end)
        {
            return StartsWithIgnoreCase(bytes, start, end, "species");
        }

        private static bool StartsWithIgnoreCase(byte* bytes, int start, int end, string token)
        {
            if (end - start < token.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (ToLowerAscii(bytes[start + i]) != (byte)token[i])
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }
    }
    #endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CompileFaunaGenomeJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit> SpawnAups;
        [ReadOnly, NoAlias] public NativeArray<uint> SpeciesHashes;
        [ReadOnly, NoAlias] public NativeArray<FaunaGeneticsTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<FaunaGeneticsProfileDTO> Profiles;
        [NoAlias] public NativeArray<ulong> GeneticMasks;
        public uint WorldSeed;
        public uint RollIndex;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)GeneticMasks.Length || (uint)index >= (uint)SpawnAups.Length)
                return;

            uint speciesHash = (uint)index < (uint)SpeciesHashes.Length ? SpeciesHashes[index] : 0u;
            AbsoluteUniversePosition spawnAup = SpawnAups[index].ToAup();
            ulong mask = FaunaGenome64.CompileGeneticMaskFromAup(in spawnAup, WorldSeed, speciesHash, RollIndex);
            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                FaunaGeneticsTuningDTO tuning = Tuning[0];
                FaunaGeneticsProfileDTO profile = FaunaGenome64.ResolveProfile(Profiles, speciesHash);
                mask = FaunaGenome64.ApplyTuningAndProfile(mask, in tuning, in profile);
            }

            GeneticMasks[index] = mask;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockGenomesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ulong> GeneticMasks;
        public uint Seed;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)GeneticMasks.Length)
                return;

            int variant = index & 7;
            switch (variant)
            {
                case 0:
                    GeneticMasks[index] = 0UL;
                    return;
                case 1:
                    GeneticMasks[index] = ulong.MaxValue;
                    return;
                case 2:
                    GeneticMasks[index] = FaunaGenome64.PackGeneticMask(255, 255, 255, 255, 15, 255, 0u, uint.MaxValue);
                    return;
                case 3:
                    GeneticMasks[index] = FaunaGenome64.PackGeneticMask(0, 0, 0, 0, 0, 0, 0u, 0u);
                    return;
                default:
                    GeneticMasks[index] = FaunaGenome64.CompileGeneticMaskFromSeed(Seed ^ (uint)(index * 747796405));
                    return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FaunaGenomeMutationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ulong> Genomes;
        [ReadOnly, NoAlias] public NativeArray<float> Radiation;
        [ReadOnly, NoAlias] public NativeArray<float> Toxicity;
        [ReadOnly, NoAlias] public NativeArray<float> Brine;
        [ReadOnly, NoAlias] public NativeArray<uint> StableHashes;
        [NoAlias] public NativeArray<byte> MutationResults;
        public uint RollIndex;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Genomes.Length)
                return;

            float radiation = index < Radiation.Length ? FaunaGenome64.SanitizeScalar01(Radiation[index]) : 0f;
            float toxicity = index < Toxicity.Length ? FaunaGenome64.SanitizeScalar01(Toxicity[index]) : 0f;
            float brine = index < Brine.Length ? FaunaGenome64.SanitizeScalar01(Brine[index]) : 0f;
            uint stableHash = index < StableHashes.Length ? StableHashes[index] : (uint)(index + 1);
            ulong mutated = FaunaGenome64.MutateGenome(
                Genomes[index],
                stableHash,
                radiation,
                toxicity,
                brine,
                RollIndex,
                out byte flags);
            Genomes[index] = mutated;
            if (index < MutationResults.Length)
                MutationResults[index] = flags;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MacroSwarmGenomeMutationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MacroSwarm> Swarms;
        [ReadOnly, NoAlias] public NativeArray<float> Radiation;
        [ReadOnly, NoAlias] public NativeArray<float> Toxicity;
        [ReadOnly, NoAlias] public NativeArray<float> Brine;
        [NoAlias] public NativeArray<byte> MutationResults;
        public uint RollIndex;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Swarms.Length)
                return;

            MacroSwarm swarm = Swarms[index];
            if (swarm.HashId == 0u)
                return;

            float radiation = index < Radiation.Length ? FaunaGenome64.SanitizeScalar01(Radiation[index]) : 0f;
            float toxicity = index < Toxicity.Length ? FaunaGenome64.SanitizeScalar01(Toxicity[index]) : 0f;
            float brine = index < Brine.Length ? FaunaGenome64.SanitizeScalar01(Brine[index]) : 0f;
            swarm.Genome = FaunaGenome64.MutateGenome(
                swarm.Genome,
                swarm.HashId,
                radiation,
                toxicity,
                brine,
                RollIndex,
                out byte flags);
            if (flags != 0)
                swarm.Flags |= 1;

            Swarms[index] = swarm;
            if (index < MutationResults.Length)
                MutationResults[index] = flags;
        }
    }
}
