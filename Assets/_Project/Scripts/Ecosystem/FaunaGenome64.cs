using Hecton8.Core.Contracts;
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
        public const int FlagShift = 40;
        public const ulong ByteMask = 0xFFUL;
        public const ulong HueMask = 0xFFFFUL;
        public const ulong MutationFlagRadiationBit = 1UL << 40;
        public const ulong MutationFlagBrineBit = 1UL << 41;
        public const ulong MutationFlagToxicityBit = 1UL << 42;
        public const ulong MutationFlagTwitchBit = 1UL << 43;
        public const ulong MutationFlagMask = 0xFFFFFF0000000000UL;
        public const uint MutationFlagRadiation = 1u << 0;
        public const uint MutationFlagBrine = 1u << 1;
        public const uint MutationFlagToxicity = 1u << 2;
        public const uint MutationFlagTwitch = 1u << 3;
        public const uint ItemHash_ContaminatedMeat = 0x09E01466u;

        private const int YellowHue16 = 10922;
        private const float InvGenomeByte = 1f / 255f;
        private const float InvGenomeHue = 1f / 65535f;

        public static ulong BuildGenome(uint variationHash, float scaleMultiplier, float speedMultiplier)
        {
            int sizeByte = PackTraitByte(scaleMultiplier, 0.7f, 1.5f);
            int speedByte = PackTraitByte(speedMultiplier, 0.65f, 1.65f);
            int aggressionByte = 112 + (int)((variationHash >> 11) & 31u);
            ushort hue = (ushort)(variationHash ^ (variationHash >> 16));
            return ((ulong)(byte)sizeByte << SizeShift) |
                   ((ulong)(byte)speedByte << SpeedShift) |
                   ((ulong)(byte)math.clamp(aggressionByte, 0, 255) << AggressionShift) |
                   ((ulong)hue << HueShift);
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
            traits.HueOffset01 = ExtractHue(resolvedGenome) * InvGenomeHue;
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
            int hue16 = ExtractHue(resolvedGenome);
            ulong flags = resolvedGenome & MutationFlagMask;
            uint rng = NextLcg(stableHash ^ (uint)resolvedGenome ^ (uint)(resolvedGenome >> 32) ^ (rollIndex * 0x9E3779B9u));

            if (radiationRads > 0.5f)
            {
                uint chanceMask = radiationRads > 0.85f ? 0x1u : 0x3u;
                if ((rng & chanceMask) == 0u)
                {
                    int speedStep = 1 + (int)((rng >> 4) & 7u);
                    int aggressionStep = 1 + (int)((rng >> 8) & 7u);
                    speedByte = math.min(255, speedByte + speedStep);
                    aggressionByte = math.min(255, aggressionByte + aggressionStep);
                    flags |= MutationFlagRadiationBit | MutationFlagTwitchBit;
                    resultFlags |= (byte)(MutationFlagRadiation | MutationFlagTwitch);
                }
            }

            if (brineDepth01 > 0.001f)
            {
                uint brineRoll = NextLcg(rng ^ 0xB49D2B35u);
                int sizeStep = 1 + (int)((brineRoll >> 3) & 3u);
                sizeByte = math.min(255, sizeByte + sizeStep);
                hue16 = MoveHueTowardYellow(hue16, 3);
                flags |= MutationFlagBrineBit | MutationFlagTwitchBit;
                resultFlags |= (byte)(MutationFlagBrine | MutationFlagTwitch);
            }

            if (toxicity01 > 0.05f)
            {
                uint toxicRoll = NextLcg(rng ^ 0xC2B2AE35u);
                int aggressionStep = 1 + (int)((toxicRoll >> 6) & 3u);
                aggressionByte = math.min(255, aggressionByte + aggressionStep);
                hue16 = MoveHueTowardYellow(hue16, 4);
                flags |= MutationFlagToxicityBit | MutationFlagTwitchBit;
                resultFlags |= (byte)(MutationFlagToxicity | MutationFlagTwitch);
            }

            return ((ulong)(byte)sizeByte << SizeShift) |
                   ((ulong)(byte)speedByte << SpeedShift) |
                   ((ulong)(byte)aggressionByte << AggressionShift) |
                   ((ulong)(ushort)hue16 << HueShift) |
                   flags;
        }

        public static uint ExtractMutationFlags(ulong genome)
        {
            return (uint)((genome >> FlagShift) & 0x00FFFFFFUL);
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

        private static int ExtractHue(ulong genome)
        {
            return (int)((genome >> HueShift) & HueMask);
        }

        private static int MoveHueTowardYellow(int hue16, int shift)
        {
            int delta = YellowHue16 - math.clamp(hue16, 0, 65535);
            return math.clamp(hue16 + (delta >> math.clamp(shift, 1, 8)), 0, 65535);
        }

        private static uint NextLcg(uint state)
        {
            return (state * 1664525u) + 1013904223u;
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
