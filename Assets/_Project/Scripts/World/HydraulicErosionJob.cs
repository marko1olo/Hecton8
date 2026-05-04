using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Standalone deterministic hydraulic erosion kernel for heightmap buffers.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HydraulicErosionJob : IJob
    {
        /// <summary>Mutable heightmap in normalized 0..1 terrain space.</summary>
        public NativeArray<float> Heightmap;

        /// <summary>Mutable normalized-source sediment accumulation lane. Normalize after the job.</summary>
        public NativeArray<float> SedimentMask;

        /// <summary>Mutable normalized-source erosion/wear accumulation lane. Normalize after the job.</summary>
        public NativeArray<float> WearMask;

        /// <summary>Heightmap width, including any overlap margin.</summary>
        public int Width;

        /// <summary>Heightmap height, including any overlap margin.</summary>
        public int Height;

        /// <summary>Core chunk X offset inside the overlapped buffer.</summary>
        public int CoreOffsetX;

        /// <summary>Core chunk Z offset inside the overlapped buffer.</summary>
        public int CoreOffsetZ;

        /// <summary>Core chunk width excluding margin pixels.</summary>
        public int CoreWidth;

        /// <summary>Core chunk height excluding margin pixels.</summary>
        public int CoreHeight;

        /// <summary>Number of simulated droplets.</summary>
        public int DropletCount;

        /// <summary>Maximum per-droplet integration steps.</summary>
        public int MaxLifetime;

        /// <summary>Deterministic seed.</summary>
        public uint Seed;

        /// <summary>Direction inertia. Higher values keep channels straighter.</summary>
        public float Inertia;

        /// <summary>Sediment capacity multiplier.</summary>
        public float CapacityFactor;

        /// <summary>Minimum sediment capacity even on shallow slopes.</summary>
        public float MinCapacity;

        /// <summary>Rock removal rate when sediment capacity exceeds carried sediment.</summary>
        public float ErosionRate;

        /// <summary>Sediment drop rate when capacity falls below carried sediment.</summary>
        public float DepositRate;

        /// <summary>Per-step water evaporation ratio.</summary>
        public float EvaporationRate;

        /// <summary>Velocity gain from downhill movement.</summary>
        public float Gravity;

        /// <summary>Initial droplet water volume.</summary>
        public float InitialWater;

        /// <summary>Initial droplet speed.</summary>
        public float InitialSpeed;

        /// <summary>Local flat-fill strength used for sandy depression plains.</summary>
        public float DepressionFillStrength;

        /// <summary>Spawn score multiplier for cells lower than their neighborhood.</summary>
        public float DepressionSpawnBias;

        /// <summary>Spawn score multiplier for already carved cells.</summary>
        public float ChannelSpawnBias;

        /// <summary>Number of deterministic spawn candidates tested per droplet.</summary>
        public int SpawnCandidateCount;

        /// <summary>Minimum water volume before final deposition and termination.</summary>
        public float MinWater;

        /// <summary>
        /// Executes the droplet simulation in deterministic sequence.
        /// </summary>
        public void Execute()
        {
            if (Width < 4 || Height < 4 || !Heightmap.IsCreated)
                return;

            int safeDropletCount = math.max(0, DropletCount);
            int safeLifetime = math.max(1, MaxLifetime);
            float safeInertia = math.saturate(Inertia);
            float safeErosionRate = math.max(0f, ErosionRate);
            float safeDepositRate = math.max(0f, DepositRate);
            float safeEvaporation = math.saturate(EvaporationRate);
            float safeGravity = math.max(0.001f, Gravity);
            float safeInitialWater = math.max(0.001f, InitialWater);
            float safeInitialSpeed = math.max(0.001f, InitialSpeed);
            float safeMinWater = math.max(0.000001f, MinWater);

            for (int dropletIndex = 0; dropletIndex < safeDropletCount; dropletIndex++)
            {
                float2 position = ResolveSpawnPosition(dropletIndex);
                float2 direction = float2.zero;
                float speed = safeInitialSpeed;
                float water = safeInitialWater;
                float sediment = 0f;

                for (int step = 0; step < safeLifetime; step++)
                {
                    if (!IsInsideDropletBounds(position))
                        break;

                    float height = SampleHeight(position);
                    float2 gradient = SampleGradient(position);
                    direction = direction * safeInertia - gradient * (1f - safeInertia);
                    float directionLengthSq = math.lengthsq(direction);
                    if (directionLengthSq <= 0.0000001f)
                        direction = HashDirection(dropletIndex, step);
                    else
                        direction *= math.rsqrt(directionLengthSq);

                    float2 nextPosition = position + direction;
                    if (!IsInsideDropletBounds(nextPosition))
                    {
                        float deposited = DepositFlatSediment(position, sediment, height + sediment * DepressionFillStrength);
                        sediment -= deposited;
                        break;
                    }

                    float nextHeight = SampleHeight(nextPosition);
                    float heightDelta = nextHeight - height;
                    float capacity = CalculateSedimentCapacity(
                        heightDelta,
                        speed,
                        water,
                        CapacityFactor,
                        MinCapacity);

                    if (heightDelta > 0f || sediment > capacity)
                    {
                        float excessSediment = math.max(0f, sediment - capacity);
                        float depositAmount = heightDelta > 0f
                            ? math.min(sediment, heightDelta + excessSediment * safeDepositRate)
                            : excessSediment * safeDepositRate;

                        if (depositAmount > 0f)
                        {
                            float targetHeight = heightDelta > 0f
                                ? nextHeight
                                : height + depositAmount * math.max(0f, DepressionFillStrength);
                            float deposited = DepositFlatSediment(position, depositAmount, targetHeight);
                            sediment -= deposited;
                        }
                    }
                    else
                    {
                        float erodeAmount = math.min((capacity - sediment) * safeErosionRate, math.max(0f, -heightDelta));
                        if (erodeAmount > 0f)
                            sediment += ErodeBrush(position, erodeAmount);
                    }

                    float speedSquared = speed * speed + (-heightDelta) * safeGravity;
                    if (speedSquared <= 0.000001f)
                    {
                        float deposited = DepositFlatSediment(position, sediment, height + sediment * DepressionFillStrength);
                        sediment -= deposited;
                        break;
                    }

                    speed = math.sqrt(speedSquared);
                    water *= 1f - safeEvaporation;
                    position = nextPosition;

                    if (water <= safeMinWater)
                    {
                        float finalHeight = SampleHeight(position);
                        float deposited = DepositFlatSediment(position, sediment, finalHeight + sediment * DepressionFillStrength);
                        sediment -= deposited;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates droplet sediment capacity from downhill slope, velocity, and water.
        /// </summary>
        /// <param name="heightDelta">New height minus old height.</param>
        /// <param name="speed">Current droplet speed.</param>
        /// <param name="water">Current droplet water volume.</param>
        /// <param name="capacityFactor">Capacity multiplier.</param>
        /// <param name="minCapacity">Minimum allowed capacity.</param>
        /// <returns>Maximum sediment the droplet can carry.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateSedimentCapacity(
            float heightDelta,
            float speed,
            float water,
            float capacityFactor,
            float minCapacity)
        {
            float downhillSlope = math.max(-heightDelta, 0.001f);
            float velocityTerm = math.max(speed, 0.01f);
            float waterTerm = math.max(water, 0.01f);
            float rawCapacity = downhillSlope * velocityTerm * waterTerm * math.max(0f, capacityFactor);
            return math.max(rawCapacity, math.max(0f, minCapacity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 ResolveSpawnPosition(int dropletIndex)
        {
            int minX = math.clamp(CoreOffsetX, 1, math.max(1, Width - 2));
            int minZ = math.clamp(CoreOffsetZ, 1, math.max(1, Height - 2));
            int maxX = math.clamp(CoreOffsetX + math.max(1, CoreWidth) - 1, minX, math.max(minX, Width - 2));
            int maxZ = math.clamp(CoreOffsetZ + math.max(1, CoreHeight) - 1, minZ, math.max(minZ, Height - 2));
            int candidates = math.max(1, SpawnCandidateCount);

            uint state = Hash((uint)dropletIndex ^ Seed ^ 0xA511E9B3u);
            int bestX = minX;
            int bestZ = minZ;
            float bestScore = -1f;

            for (int i = 0; i < candidates; i++)
            {
                state = Hash(state + (uint)i * 0x9E3779B9u);
                int x = minX + (int)(state % (uint)math.max(1, maxX - minX + 1));
                state = Hash(state ^ 0xB5297A4Du);
                int z = minZ + (int)(state % (uint)math.max(1, maxZ - minZ + 1));

                int index = z * Width + x;
                float depression = CalculateLocalDepression(x, z);
                float channel = WearMask.IsCreated ? math.saturate(WearMask[index] * 32f) : 0f;
                float jitter = Hash01(state ^ 0x68E31DA4u);
                float score = jitter +
                              depression * math.max(0f, DepressionSpawnBias) +
                              channel * math.max(0f, ChannelSpawnBias);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestZ = z;
                }
            }

            state = Hash(state ^ 0x1B56C4E9u);
            float jitterX = Hash01(state) - 0.5f;
            state = Hash(state ^ 0x92D68CA2u);
            float jitterZ = Hash01(state) - 0.5f;
            return new float2(bestX + 0.5f + jitterX, bestZ + 0.5f + jitterZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateLocalDepression(int x, int z)
        {
            int index = z * Width + x;
            float center = math.saturate(Heightmap[index]);
            float neighborSum = 0f;
            int neighborCount = 0;

            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oz == 0)
                        continue;

                    int nx = math.clamp(x + ox, 0, Width - 1);
                    int nz = math.clamp(z + oz, 0, Height - 1);
                    neighborSum += math.saturate(Heightmap[nz * Width + nx]);
                    neighborCount++;
                }
            }

            float neighborAverage = neighborSum / math.max(1, neighborCount);
            return math.max(0f, neighborAverage - center);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInsideDropletBounds(float2 position)
        {
            return position.x >= 1f &&
                   position.y >= 1f &&
                   position.x < Width - 2f &&
                   position.y < Height - 2f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleHeight(float2 position)
        {
            int x = math.clamp((int)math.floor(position.x), 0, Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Height - 2);
            float fx = position.x - x;
            float fz = position.y - z;

            float h00 = math.saturate(Heightmap[z * Width + x]);
            float h10 = math.saturate(Heightmap[z * Width + x + 1]);
            float h01 = math.saturate(Heightmap[(z + 1) * Width + x]);
            float h11 = math.saturate(Heightmap[(z + 1) * Width + x + 1]);

            return math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 SampleGradient(float2 position)
        {
            float left = SampleHeight(new float2(position.x - 1f, position.y));
            float right = SampleHeight(new float2(position.x + 1f, position.y));
            float down = SampleHeight(new float2(position.x, position.y - 1f));
            float up = SampleHeight(new float2(position.x, position.y + 1f));
            return new float2((right - left) * 0.5f, (up - down) * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ErodeBrush(float2 position, float amount)
        {
            int centerX = math.clamp((int)math.floor(position.x), 1, Width - 2);
            int centerZ = math.clamp((int)math.floor(position.y), 1, Height - 2);
            float totalWeight = 0f;

            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    float2 cellCenter = new float2(centerX + ox + 0.5f, centerZ + oz + 0.5f);
                    float distance = math.length(cellCenter - position);
                    totalWeight += math.saturate(1f - distance * 0.6666667f);
                }
            }

            if (totalWeight <= 0.000001f)
                return 0f;

            float removed = 0f;
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = centerX + ox;
                    int z = centerZ + oz;
                    int index = z * Width + x;
                    float2 cellCenter = new float2(x + 0.5f, z + 0.5f);
                    float weight = math.saturate(1f - math.length(cellCenter - position) * 0.6666667f) / totalWeight;
                    float requested = amount * weight;
                    float current = math.saturate(Heightmap[index]);
                    float actual = math.min(current, requested);
                    Heightmap[index] = current - actual;
                    if (WearMask.IsCreated)
                        WearMask[index] += actual;
                    removed += actual;
                }
            }

            return removed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositFlatSediment(float2 position, float amount, float targetHeight)
        {
            if (amount <= 0f)
                return 0f;

            int centerX = math.clamp((int)math.floor(position.x), 1, Width - 2);
            int centerZ = math.clamp((int)math.floor(position.y), 1, Height - 2);
            float remaining = amount;
            float safeTargetHeight = math.saturate(targetHeight);

            for (int pass = 0; pass < 4; pass++)
            {
                float lowest = 2f;
                float nextLowest = 2f;
                int lowCount = 0;

                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float h = math.saturate(Heightmap[(centerZ + oz) * Width + centerX + ox]);
                        if (h < lowest - 0.000001f)
                        {
                            nextLowest = lowest;
                            lowest = h;
                            lowCount = 1;
                        }
                        else if (math.abs(h - lowest) <= 0.000001f)
                        {
                            lowCount++;
                        }
                        else if (h < nextLowest)
                        {
                            nextLowest = h;
                        }
                    }
                }

                if (lowCount <= 0 || lowest >= safeTargetHeight)
                    break;

                float fillHeight = math.min(safeTargetHeight, nextLowest < 1.5f ? nextLowest : safeTargetHeight);
                float capacity = math.max(0f, fillHeight - lowest) * lowCount;
                if (capacity <= 0.000001f)
                    break;

                float fillAmount = math.min(remaining, capacity);
                float raise = fillAmount / lowCount;

                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int index = (centerZ + oz) * Width + centerX + ox;
                        float h = math.saturate(Heightmap[index]);
                        if (math.abs(h - lowest) > 0.000001f)
                            continue;

                        Heightmap[index] = math.saturate(h + raise);
                        if (SedimentMask.IsCreated)
                            SedimentMask[index] += raise;
                    }
                }

                remaining -= fillAmount;
                if (remaining <= 0.000001f)
                    return amount;
            }

            if (remaining > 0.000001f)
            {
                float deposited = DepositBilinear(position, remaining);
                remaining -= deposited;
            }

            return amount - math.max(0f, remaining);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DepositBilinear(float2 position, float amount)
        {
            int x = math.clamp((int)math.floor(position.x), 0, Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Height - 2);
            float fx = position.x - x;
            float fz = position.y - z;

            float w00 = (1f - fx) * (1f - fz);
            float w10 = fx * (1f - fz);
            float w01 = (1f - fx) * fz;
            float w11 = fx * fz;

            DepositAtIndex(z * Width + x, amount * w00);
            DepositAtIndex(z * Width + x + 1, amount * w10);
            DepositAtIndex((z + 1) * Width + x, amount * w01);
            DepositAtIndex((z + 1) * Width + x + 1, amount * w11);
            return amount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DepositAtIndex(int index, float amount)
        {
            float actual = math.max(0f, amount);
            Heightmap[index] = math.saturate(Heightmap[index] + actual);
            if (SedimentMask.IsCreated)
                SedimentMask[index] += actual;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 HashDirection(int dropletIndex, int step)
        {
            uint hash = Hash((uint)dropletIndex * 0x9E3779B9u ^ (uint)step * 0x85EBCA6Bu);
            float angle = Hash01(hash) * 6.28318530718f;
            return new float2(math.cos(angle), math.sin(angle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
