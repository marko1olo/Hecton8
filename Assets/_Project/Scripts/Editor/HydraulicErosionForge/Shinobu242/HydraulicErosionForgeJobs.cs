#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static class ErosionDeterminismHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A32(double3 sectorAup, uint worldSeed, uint salt)
        {
            uint hash = 2166136261u;
            hash = HashLong(hash, QuantizeComponentToMillimeters(sectorAup.x));
            hash = HashLong(hash, QuantizeComponentToMillimeters(sectorAup.y));
            hash = HashLong(hash, QuantizeComponentToMillimeters(sectorAup.z));
            hash = HashUInt(hash, worldSeed);
            hash = HashUInt(hash, salt);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 QuantizeAupToMillimeters(double3 sectorAup)
        {
            return new double3(
                QuantizeComponentToMeters(sectorAup.x),
                QuantizeComponentToMeters(sectorAup.y),
                QuantizeComponentToMeters(sectorAup.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Hash01(uint value)
        {
            return (Mix(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long QuantizeComponentToMillimeters(double value)
        {
            double finite = math.isfinite(value) ? value : 0.0;
            return (long)math.round(finite * 1000.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double QuantizeComponentToMeters(double value)
        {
            return QuantizeComponentToMillimeters(value) * 0.001;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashLong(uint hash, long value)
        {
            ulong raw = (ulong)value;
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(raw >> shift);
                hash *= 16777619u;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashUInt(uint hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= 16777619u;
            }

            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMockHeightmapJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Heights;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> SiltMask;
        public int Width;
        public int Height;
        public float ConeHeight01;
        public float BasinDepth01;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            float2 p = new float2(
                safeWidth <= 1 ? 0f : (x * 2f / (safeWidth - 1)) - 1f,
                safeHeight <= 1 ? 0f : (z * 2f / (safeHeight - 1)) - 1f);
            float ridge = 1f - math.saturate(math.max(math.abs(p.x), math.abs(p.y)));
            float cone = 1f - math.saturate(math.sqrt(math.max(0f, math.lengthsq(p))));
            float valley = math.exp(-math.abs(p.x + p.y * 0.37f) * 8f) * math.saturate(1f - math.abs(p.y));
            float h = math.saturate((ridge * 0.58f + cone * 0.42f) * math.max(0f, ConeHeight01) - valley * math.max(0f, BasinDepth01));

            float* heightPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Heights);
            ref float heightRef = ref UnsafeUtility.AsRef<float>(heightPtr + index);
            heightRef = h;

            if (SiltMask.IsCreated && (uint)index < (uint)SiltMask.Length)
            {
                float* siltPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SiltMask);
                ref float siltRef = ref UnsafeUtility.AsRef<float>(siltPtr + index);
                siltRef = 0f;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct InitializeErosionDropletsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ErosionDropletDTO> Droplets;
        public HydraulicErosionSettingsDTO Settings;
        public uint SeedSalt;

        public void Execute(int index)
        {
            uint seed = ErosionDeterminismHash.Fnv1A32(Settings.SectorAup, Settings.WorldSeed, SeedSalt ^ (uint)index);
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            float x = random.NextFloat() * math.max(1, Settings.Width - 2) + 0.5f;
            float z = random.NextFloat() * math.max(1, Settings.Height - 2) + 0.5f;
            float angle = random.NextFloat() * 6.2831855f;
            ErosionDropletDTO* ptr = (ErosionDropletDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Droplets);
            ref ErosionDropletDTO droplet = ref UnsafeUtility.AsRef<ErosionDropletDTO>(ptr + index);
            droplet.Position = new float2(x, z);
            droplet.Direction = new float2(math.cos(angle), math.sin(angle));
            droplet.Velocity = math.max(0.001f, Settings.InitialVelocity);
            droplet.WaterVolume = math.max(0.001f, Settings.InitialWater);
            droplet.SedimentCapacity = 0f;
            droplet._pad0 = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct SimulateHydraulicErosionJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's safety system cannot prove the raw pointer loop is single-writer because this job samples and mutates the same
        // height array through bilinear erosion/deposition helpers. In this pipeline ScheduleCore schedules exactly one
        // SimulateHydraulicErosionJob after the mock-height and droplet-init dependencies; no IJobParallelFor height writer exists.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected designs: (a) per-droplet IJobParallelFor direct height writes, because deterministic float atomics are not
        // available for arbitrary height deltas; (b) full duplicate height delta reduction buffers, because the offline sector pass
        // would triple memory bandwidth before profiler evidence justifies the cost. This single-writer kernel is slower than ideal
        // parallel mutation but structurally deterministic and cache-local for the current editor-only baker.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: Heightmap, SiltMask, Droplets, Metrics, Telemetry, and TelemetryCursor are owned exclusively by this scheduled
        // bake chain until the cold editor sync point completes. Directional NativeQueues have this job as their only producer and
        // are consumed only after completion by editor code or the bridge helper.
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<float> Heightmap;
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<float> SiltMask;
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<ErosionDropletDTO> Droplets;
        public NativeQueue<ErosionDropletDTO> NorthTransfers;
        public NativeQueue<ErosionDropletDTO> SouthTransfers;
        public NativeQueue<ErosionDropletDTO> EastTransfers;
        public NativeQueue<ErosionDropletDTO> WestTransfers;
        [NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<ErosionBakeTelemetryEntry> Telemetry;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> TelemetryCursor;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float> Metrics;
        public HydraulicErosionSettingsDTO Settings;

        public void Execute()
        {
            if (!Heightmap.IsCreated || !Droplets.IsCreated || Settings.Width < 2 || Settings.Height < 2)
                return;

            float* heights = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Heightmap);
            float* silt = SiltMask.IsCreated ? (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SiltMask) : null;
            ErosionDropletDTO* droplets = (ErosionDropletDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Droplets);
            int dropletCount = math.clamp(Settings.DropletCount, 0, Droplets.Length);
            float quality = math.saturate(Settings.GlobalQualityWeight);
            float qualityCurve = quality * quality * (3f - 2f * quality);
            float interpolationWeight = math.smoothstep(0.18f, 0.82f, quality);
            float capacityScale = math.lerp(0.62f, 1.18f, qualityCurve);
            float erosionScale = math.lerp(0.7f, 1.12f, qualityCurve);
            int maxLifetime = math.clamp((int)math.round(Settings.MaxLifetime * math.lerp(0.55f, 1.22f, qualityCurve)), 1, HydraulicErosionForgeConstants.MaxDropletLifetime);
            float maxDepth = 0f;
            float sedimentTransported = 0f;
            uint warningFlags = 0u;

            for (int i = 0; i < dropletCount; i++)
            {
                ref ErosionDropletDTO d = ref UnsafeUtility.AsRef<ErosionDropletDTO>(droplets + i);
                for (int step = 0; step < maxLifetime; step++)
                {
                    if (!math.all(math.isfinite(d.Position)) || !math.all(math.isfinite(d.Direction)))
                    {
                        warningFlags |= HydraulicErosionForgeConstants.WarningNonFiniteHeight;
                        break;
                    }

                    if (TryTransferAcrossChunk(ref d))
                        break;

                    int x = math.clamp((int)math.floor(d.Position.x), 0, Settings.Width - 2);
                    int z = math.clamp((int)math.floor(d.Position.y), 0, Settings.Height - 2);
                    float2 frac = d.Position - new float2(x, z);
                    int index00 = z * Settings.Width + x;
                    int index10 = index00 + 1;
                    int index01 = index00 + Settings.Width;
                    int index11 = index01 + 1;
                    float h00 = heights[index00];
                    float h10 = heights[index10];
                    float h01 = heights[index01];
                    float h11 = heights[index11];
                    if (!math.isfinite(h00) || !math.isfinite(h10) || !math.isfinite(h01) || !math.isfinite(h11))
                    {
                        warningFlags |= HydraulicErosionForgeConstants.WarningNonFiniteHeight;
                        break;
                    }

                    float currentHeight = SampleHeightLod(h00, h10, h01, h11, frac, interpolationWeight);
                    float2 gradient = new float2(
                        math.lerp(h10 - h00, h11 - h01, frac.y),
                        math.lerp(h01 - h00, h11 - h10, frac.x));
                    float2 nextDirection = (d.Direction * math.saturate(Settings.Inertia)) - (gradient * (1f - math.saturate(Settings.Inertia)) * math.lerp(0.72f, 1f, qualityCurve));
                    float dirLenSq = math.lengthsq(nextDirection);
                    d.Direction = dirLenSq > 0.000001f ? nextDirection * math.rsqrt(dirLenSq) : HashDirection(i, step);
                    float2 nextPosition = d.Position + d.Direction;

                    float nextHeight = SampleHeight(heights, nextPosition, interpolationWeight);
                    float heightDelta = nextHeight - currentHeight;
                    float capacity = math.max(-heightDelta * d.Velocity * d.WaterVolume * math.max(0f, Settings.CapacityFactor) * capacityScale, math.max(0f, Settings.MinSedimentCapacity));
                    float carriedSediment = math.max(0f, d.SedimentCapacity);

                    if (carriedSediment > capacity || heightDelta > 0f)
                    {
                        float deposit = heightDelta > 0f ? math.min(carriedSediment, heightDelta) : (carriedSediment - capacity) * math.saturate(Settings.DepositRate);
                        Deposit(heights, silt, d.Position, deposit);
                        carriedSediment -= deposit;
                        sedimentTransported += math.max(0f, deposit);
                    }
                    else
                    {
                        float erode = math.min((capacity - carriedSediment) * math.saturate(Settings.ErosionRate) * erosionScale, -heightDelta);
                        if (erode > 0f)
                        {
                            float removed = Erode(heights, d.Position, erode);
                            carriedSediment += removed;
                            maxDepth = math.max(maxDepth, removed);
                            sedimentTransported += removed;
                        }
                    }

                    d.SedimentCapacity = math.max(0f, carriedSediment);
                    d.Velocity = math.sqrt(math.max(0.000001f, d.Velocity * d.Velocity + math.max(0f, -heightDelta) * math.max(0f, Settings.Gravity)));
                    d.WaterVolume *= 1f - math.saturate(Settings.EvaporationRate);
                    d.Position = nextPosition;

                    if (d.WaterVolume <= math.max(0.000001f, Settings.MinWater))
                    {
                        if (d.SedimentCapacity > 0f)
                        {
                            Deposit(heights, silt, d.Position, d.SedimentCapacity);
                            sedimentTransported += d.SedimentCapacity;
                            d.SedimentCapacity = 0f;
                        }

                        break;
                    }
                }
            }

            if (Metrics.IsCreated && Metrics.Length >= 2)
            {
                Metrics[0] = maxDepth;
                Metrics[1] = sedimentTransported;
            }

            RecordTelemetry(warningFlags, maxDepth, sedimentTransported);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryTransferAcrossChunk(ref ErosionDropletDTO droplet)
        {
            if (droplet.Position.x < 0f)
            {
                droplet.Position.x += Settings.Width - 1;
                if (WestTransfers.IsCreated) WestTransfers.Enqueue(droplet);
                return true;
            }

            if (droplet.Position.x >= Settings.Width - 1)
            {
                droplet.Position.x -= Settings.Width - 1;
                if (EastTransfers.IsCreated) EastTransfers.Enqueue(droplet);
                return true;
            }

            if (droplet.Position.y < 0f)
            {
                droplet.Position.y += Settings.Height - 1;
                if (SouthTransfers.IsCreated) SouthTransfers.Enqueue(droplet);
                return true;
            }

            if (droplet.Position.y >= Settings.Height - 1)
            {
                droplet.Position.y -= Settings.Height - 1;
                if (NorthTransfers.IsCreated) NorthTransfers.Enqueue(droplet);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleHeight(float* heights, float2 position, float interpolationWeight)
        {
            int x = math.clamp((int)math.floor(position.x), 0, Settings.Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Settings.Height - 2);
            float2 frac = position - new float2(x, z);
            int index = z * Settings.Width + x;
            return SampleHeightLod(
                heights[index],
                heights[index + 1],
                heights[index + Settings.Width],
                heights[index + Settings.Width + 1],
                frac,
                interpolationWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleHeightLod(float h00, float h10, float h01, float h11, float2 frac, float interpolationWeight)
        {
            float nearestX = math.step(0.5f, frac.x);
            float nearestY = math.step(0.5f, frac.y);
            float nearest = math.lerp(math.lerp(h00, h10, nearestX), math.lerp(h01, h11, nearestX), nearestY);
            float bilinear = math.lerp(math.lerp(h00, h10, frac.x), math.lerp(h01, h11, frac.x), frac.y);
            return math.lerp(nearest, bilinear, interpolationWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float Erode(float* heights, float2 position, float amount)
        {
            int x = math.clamp((int)math.floor(position.x), 1, Settings.Width - 2);
            int z = math.clamp((int)math.floor(position.y), 1, Settings.Height - 2);
            int center = z * Settings.Width + x;
            float totalRemoved = 0f;
            float edgeWeight = math.lerp(0.02f, 0.0825f, math.smoothstep(0.24f, 0.88f, Settings.GlobalQualityWeight));
            float centerWeight = math.saturate(1f - edgeWeight * 8f);
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int index = center + oz * Settings.Width + ox;
                    float weight = ox == 0 && oz == 0 ? centerWeight : edgeWeight;
                    float remove = math.min(math.max(0f, heights[index]), amount * weight);
                    heights[index] = math.saturate(heights[index] - remove);
                    totalRemoved += remove;
                }
            }

            return totalRemoved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Deposit(float* heights, float* silt, float2 position, float amount)
        {
            if (amount <= 0f)
                return;

            int x = math.clamp((int)math.floor(position.x), 0, Settings.Width - 2);
            int z = math.clamp((int)math.floor(position.y), 0, Settings.Height - 2);
            float2 frac = position - new float2(x, z);
            float w00 = (1f - frac.x) * (1f - frac.y);
            float w10 = frac.x * (1f - frac.y);
            float w01 = (1f - frac.x) * frac.y;
            float w11 = frac.x * frac.y;
            int index = z * Settings.Width + x;
            DepositAt(heights, silt, index, amount * w00);
            DepositAt(heights, silt, index + 1, amount * w10);
            DepositAt(heights, silt, index + Settings.Width, amount * w01);
            DepositAt(heights, silt, index + Settings.Width + 1, amount * w11);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DepositAt(float* heights, float* silt, int index, float amount)
        {
            heights[index] = math.saturate(heights[index] + amount);
            if (silt != null)
                silt[index] = math.saturate(silt[index] + amount * math.max(0.001f, Settings.SiltMaskGain));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTelemetry(uint warningFlags, float maxDepth, float sediment)
        {
            if (!Telemetry.IsCreated || Telemetry.Length == 0)
                return;

            int cursor = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = (cursor + 1) % Telemetry.Length;
            }

            int index = math.clamp(cursor, 0, Telemetry.Length - 1);
            double3 quantizedAup = ErosionDeterminismHash.QuantizeAupToMillimeters(Settings.SectorAup);
            Telemetry[index] = new ErosionBakeTelemetryEntry
            {
                SectorAup = quantizedAup,
                Stage = 2u,
                StateHash = ErosionDeterminismHash.Fnv1A32(quantizedAup, Settings.WorldSeed, warningFlags),
                MinHeight = 0f,
                MaxHeight = 1f,
                MaxCarvedDepth = maxDepth,
                SedimentTransported = sediment,
                SectorX = Settings.SectorX,
                SectorZ = Settings.SectorZ,
                WarningFlags = warningFlags,
                DropletSample = (uint)math.max(0, Settings.DropletCount)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 HashDirection(int dropletIndex, int step)
        {
            float angle = ErosionDeterminismHash.Hash01((uint)dropletIndex ^ ((uint)step * 0x9E3779B9u)) * 6.2831855f;
            return new float2(math.cos(angle), math.sin(angle));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMacroErosionMapJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Source;
        [WriteOnly, NoAlias] public NativeArray<float> Macro;
        public int SourceWidth;
        public int SourceHeight;
        public int MacroWidth;
        public int MacroHeight;

        public void Execute(int index)
        {
            int mx = index % MacroWidth;
            int mz = index / MacroWidth;
            int x0 = (int)math.floor(mx * (SourceWidth / (float)math.max(1, MacroWidth)));
            int z0 = (int)math.floor(mz * (SourceHeight / (float)math.max(1, MacroHeight)));
            int x1 = (int)math.floor((mx + 1) * (SourceWidth / (float)math.max(1, MacroWidth)));
            int z1 = (int)math.floor((mz + 1) * (SourceHeight / (float)math.max(1, MacroHeight)));
            x0 = math.clamp(x0, 0, SourceWidth - 1);
            z0 = math.clamp(z0, 0, SourceHeight - 1);
            x1 = math.clamp(math.max(x0 + 1, x1), 1, SourceWidth);
            z1 = math.clamp(math.max(z0 + 1, z1), 1, SourceHeight);
            float sum = 0f;
            int count = 0;
            for (int z = z0; z < z1; z++)
            {
                int row = z * SourceWidth;
                for (int x = x0; x < x1; x++)
                {
                    float v = Source[row + x];
                    sum += math.isfinite(v) ? v : 0f;
                    count++;
                }
            }

            Macro[index] = count > 0 ? math.saturate(sum / count) : 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ErosionMetricScanJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights;
        [ReadOnly, NoAlias] public NativeArray<float> Silt;
        [WriteOnly, NoAlias] public NativeArray<float> Metrics;

        public void Execute()
        {
            float minHeight = 1f;
            float maxHeight = 0f;
            float siltSum = 0f;
            float nan = 0f;
            int count = math.min(Heights.IsCreated ? Heights.Length : 0, Silt.IsCreated ? Silt.Length : 0);
            for (int i = 0; i < count; i++)
            {
                float h = Heights[i];
                float s = Silt[i];
                if (!math.isfinite(h) || !math.isfinite(s))
                {
                    nan += 1f;
                    continue;
                }

                minHeight = math.min(minHeight, h);
                maxHeight = math.max(maxHeight, h);
                siltSum += s;
            }

            if (Metrics.IsCreated && Metrics.Length >= 4)
            {
                Metrics[0] = minHeight;
                Metrics[1] = maxHeight;
                Metrics[2] = siltSum;
                Metrics[3] = nan;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ErosionPreviewRgbaJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights;
        [ReadOnly, NoAlias] public NativeArray<float> Silt;
        [WriteOnly, NoAlias] public NativeArray<uint> Rgba;
        public int Width;
        public int Height;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            float h = math.saturate(Heights[index]);
            float s = Silt.IsCreated && index < Silt.Length ? math.saturate(Silt[index]) : 0f;
            float west = Heights[z * safeWidth + math.max(0, x - 1)];
            float east = Heights[z * safeWidth + math.min(safeWidth - 1, x + 1)];
            float south = Heights[math.max(0, z - 1) * safeWidth + x];
            float north = Heights[math.min(safeHeight - 1, z + 1) * safeWidth + x];
            float carved = math.saturate((math.max(math.max(west, east), math.max(south, north)) - h) * 8f);
            byte r = (byte)math.clamp((int)math.round(carved * 255f), 0, 255);
            byte g = (byte)math.clamp((int)math.round(h * 180f), 0, 255);
            byte b = (byte)math.clamp((int)math.round(s * 255f), 0, 255);
            Rgba[index] = (uint)(r | (g << 8) | (b << 16) | (255 << 24));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct SanitizeFloatPayloadJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Payload;

        public void Execute(int index)
        {
            float value = Payload[index];
            Payload[index] = math.isfinite(value) ? value : 0f;
        }
    }
}
#endif
