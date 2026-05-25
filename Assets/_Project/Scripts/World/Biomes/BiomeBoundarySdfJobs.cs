using System.Runtime.InteropServices;
using Hecton8.World.Biomes.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.Biomes
{
    internal static class BiomeBoundarySdfJobLayout
    {
        public const int BiomeWeightEntryStrideBytes = 16;
    }

    public static class BiomeBoundarySdfJobs
    {
        private const float MinCellSizeMeters = 0.5f;
        private const float MinBlendWidthMeters = 0.01f;
        private const float ExactCenterDistanceSq = 0.0001f;

        [StructLayout(LayoutKind.Explicit, Size = BiomeBoundarySdfJobLayout.BiomeWeightEntryStrideBytes)]
        private struct BiomeWeightEntry
        {
            [FieldOffset(0)]
            public uint Hash;

            [FieldOffset(4)]
            public float Weight;

            [FieldOffset(8)]
            public byte Biome;

            [FieldOffset(9)]
            private byte _pad0;

            [FieldOffset(10)]
            private ushort _pad1;

            [FieldOffset(12)]
            private uint _pad2;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BiomeBoundarySdfSampleJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> GlobalBiomeMap;
            [ReadOnly, NoAlias] public NativeArray<uint> BiomeHashMap;
            [WriteOnly, NoAlias] public NativeArray<BiomeBoundarySdfResult> Result;

            public BiomeBoundarySdfSettings Settings;
            public double2 SampleAupXZ;

            public void Execute()
            {
                BiomeBoundarySdfResult result = default;
                int2 resolution = math.max(Settings.Resolution, new int2(1, 1));
                int expectedLength = resolution.x * resolution.y;
                if (!Result.IsCreated || Result.Length == 0)
                    return;

                if (!GlobalBiomeMap.IsCreated || GlobalBiomeMap.Length < expectedLength)
                {
                    result.Flags = (byte)BiomeBoundarySdfFlags.MissingMap;
                    WriteResult(result);
                    return;
                }

                float cellSize = math.max(MinCellSizeMeters, Settings.CellSizeMeters);
                float blendWidth = math.max(MinBlendWidthMeters, Settings.BlendWidthMeters);
                float invBlendWidth = math.rcp(blendWidth);
                double invCellSize = math.rcp((double)cellSize);
                double2 localCellsD = (SampleAupXZ - Settings.OriginAupXZ) * invCellSize;
                if (!math.all(math.isfinite(localCellsD)))
                {
                    result.Flags = (byte)BiomeBoundarySdfFlags.InvalidInput;
                    WriteResult(result);
                    return;
                }

                byte flags = Settings.Flags;
                double2 mapMaxExclusive = new double2(resolution.x, resolution.y) - 0.000001d;
                bool outOfBounds = localCellsD.x < 0d ||
                                   localCellsD.y < 0d ||
                                   localCellsD.x >= resolution.x ||
                                   localCellsD.y >= resolution.y;
                if (outOfBounds)
                    flags |= (byte)BiomeBoundarySdfFlags.OutOfBounds;

                double2 sampleCellsD = math.clamp(localCellsD, double2.zero, mapMaxExclusive);
                int2 maxCell = new int2(resolution.x - 1, resolution.y - 1);
                int2 centerCell = new int2((int)math.floor(sampleCellsD.x), (int)math.floor(sampleCellsD.y));
                centerCell = math.clamp(centerCell, int2.zero, maxCell);
                float2 sampleMeters = new float2((float)(sampleCellsD.x * cellSize), (float)(sampleCellsD.y * cellSize));
                int radius = math.clamp(Settings.SampleRadiusCells, 1, 2);

                FixedList512Bytes<BiomeWeightEntry> weights = default;
                float nearestBoundaryMeters = DistanceToCellBoundaryMeters(sampleMeters, centerCell, cellSize);
                int centerIndex = centerCell.y * resolution.x + centerCell.x;
                byte centerBiome = GlobalBiomeMap[centerIndex];

                for (int z = -radius; z <= radius; z++)
                {
                    int y = math.clamp(centerCell.y + z, 0, maxCell.y);
                    for (int x = -radius; x <= radius; x++)
                    {
                        int cellX = math.clamp(centerCell.x + x, 0, maxCell.x);
                        int index = y * resolution.x + cellX;
                        byte biome = GlobalBiomeMap[index];
                        if (biome == 0)
                            continue;

                        uint hash = BiomeHashMap.IsCreated && BiomeHashMap.Length > index ? BiomeHashMap[index] : 0u;
                        int2 cell = new int2(cellX, y);
                        float2 cellCenter = (new float2(cell.x, cell.y) + 0.5f) * cellSize;
                        float2 delta = sampleMeters - cellCenter;
                        float distanceSq = math.lengthsq(delta);
                        if (distanceSq < ExactCenterDistanceSq)
                        {
                            distanceSq = ExactCenterDistanceSq;
                            flags |= (byte)BiomeBoundarySdfFlags.ExactCellCenter;
                        }

                        float boundaryDistance = DistanceToCellBoundaryMeters(sampleMeters, cell, cellSize);
                        float boundaryBoost = 1f + math.saturate((blendWidth - boundaryDistance) * invBlendWidth);
                        float weight = boundaryBoost * math.rcp(distanceSq);
                        AddWeight(ref weights, biome, hash, weight);

                        if (centerBiome != 0 && biome != centerBiome)
                            nearestBoundaryMeters = math.min(nearestBoundaryMeters, boundaryDistance);
                    }
                }

                ResolveTopTwo(in weights, out BiomeWeightEntry primary, out BiomeWeightEntry secondary);
                float total = primary.Weight + secondary.Weight;
                float blend01 = total > 0f && secondary.Biome != 0
                    ? math.saturate((secondary.Weight * math.rcp(total)) * 2f)
                    : 0f;

                if (secondary.Biome != 0)
                    flags |= (byte)BiomeBoundarySdfFlags.HasSecondaryBiome;

                result.BiomeA = primary.Biome;
                result.BiomeB = secondary.Biome != 0 ? secondary.Biome : primary.Biome;
                result.SampleDiameter = (byte)(radius * 2 + 1);
                result.Flags = flags;
                result.BiomeAHash = primary.Hash;
                result.BiomeBHash = secondary.Biome != 0 ? secondary.Hash : primary.Hash;
                result.BlendFactor01 = blend01;
                result.BoundaryDistanceMeters = math.max(0f, nearestBoundaryMeters);
                result.PrimaryWeight = primary.Weight;
                result.SecondaryWeight = secondary.Weight;
                result.MacroCell = centerCell;
                WriteResult(result);
            }

            private void WriteResult(BiomeBoundarySdfResult result)
            {
                Result[0] = result;
            }
        }

        private static void AddWeight(ref FixedList512Bytes<BiomeWeightEntry> weights, byte biome, uint hash, float weight)
        {
            if (biome == 0 || !math.isfinite(weight) || weight <= 0f)
                return;

            for (int i = 0; i < weights.Length; i++)
            {
                BiomeWeightEntry entry = weights[i];
                if (!IsSameBiomeKey(entry, biome, hash))
                    continue;

                entry.Weight += weight;
                if (entry.Hash == 0u)
                    entry.Hash = hash;
                weights[i] = entry;
                return;
            }

            if (weights.Length >= weights.Capacity)
                return;

            weights.Add(new BiomeWeightEntry
            {
                Biome = biome,
                Hash = hash,
                Weight = weight
            });
        }

        private static void ResolveTopTwo(in FixedList512Bytes<BiomeWeightEntry> weights, out BiomeWeightEntry primary, out BiomeWeightEntry secondary)
        {
            primary = default;
            secondary = default;
            for (int i = 0; i < weights.Length; i++)
            {
                BiomeWeightEntry candidate = weights[i];
                if (candidate.Weight > primary.Weight)
                {
                    secondary = primary;
                    primary = candidate;
                    continue;
                }

                if (!IsSameBiomeKey(candidate, primary.Biome, primary.Hash) && candidate.Weight > secondary.Weight)
                    secondary = candidate;
            }
        }

        private static bool IsSameBiomeKey(in BiomeWeightEntry entry, byte biome, uint hash)
        {
            if (entry.Biome != biome)
                return false;

            return hash == 0u || entry.Hash == 0u || entry.Hash == hash;
        }

        private static float DistanceToCellBoundaryMeters(float2 sampleMeters, int2 cell, float cellSize)
        {
            float2 cellMin = new float2(cell.x, cell.y) * cellSize;
            float2 cellMax = cellMin + cellSize;
            bool inside = sampleMeters.x >= cellMin.x && sampleMeters.x <= cellMax.x &&
                          sampleMeters.y >= cellMin.y && sampleMeters.y <= cellMax.y;
            if (inside)
            {
                float left = sampleMeters.x - cellMin.x;
                float right = cellMax.x - sampleMeters.x;
                float bottom = sampleMeters.y - cellMin.y;
                float top = cellMax.y - sampleMeters.y;
                return math.max(0f, math.min(math.min(left, right), math.min(bottom, top)));
            }

            float2 clamped = math.clamp(sampleMeters, cellMin, cellMax);
            float distanceSq = math.lengthsq(sampleMeters - clamped);
            return distanceSq * math.rsqrt(math.max(distanceSq, ExactCenterDistanceSq));
        }
    }
}
