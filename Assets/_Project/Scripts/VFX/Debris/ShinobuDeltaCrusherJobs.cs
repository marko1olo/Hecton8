using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX.Debris
{
    public enum MockVoxelChunkState : byte
    {
        Unloaded = 0,
        Loading = 1,
        Ready = 2
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DebrisParticleDTO
    {
        public float3 Position;
        public float Radius;
        public float3 Velocity;
        public uint MaterialHash;
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct MockLaserFireSignal : ISignal
    {
        public double3 AupPosition;
        public float Radius;
        public sbyte DeltaDensity;
        public byte ChunkState;
        public ushort Reserved0;
        public uint MaterialHash;
        public uint Frame;
        private uint _pad0;
        private uint _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct CarveDebrisTuningDTO
    {
        public float3 Gravity;
        public float Bounce;
        public int MaxActiveDebris;
        public int MassUnitsPerParticle;
        public uint Flags;
        public uint Version;
    }

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct ChunkCarveDispatchDTO
    {
        public int3 ChunkCoord;
        public int3 MinCell;
        public int3 Span;
        public byte Active;
        private byte _reserved0;
        private ushort _reserved1;
    }

    public partial struct MockWorldSampler
    {
        public float SampleDistance(float3 position)
        {
            return position.y;
        }

        public float3 SampleNormal(float3 position)
        {
            return new float3(0f, 1f, 0f);
        }
    }

    public static class ShinobuDeltaCrusher
    {
        public const int DefaultChunkResolution = 32;
        public const int DefaultChunkCellCount = DefaultChunkResolution * DefaultChunkResolution * DefaultChunkResolution;
        public const int LowTierDebrisCap = 500;
        public const int UltraTierDebrisCap = 10000;
        public const sbyte MockSolidDensity = 127;
        public const sbyte MockEmptyDensity = sbyte.MinValue;
        public const uint TitaniumOreHash = 0x61C51592u;
        public const uint TitaniumScrapItemHash = 0xD150482Eu;
        public const byte TitaniumVoxelMaterialId = 4;
        public const uint OrphanedChunkTelemetryFlag = 1u;
        public const uint FragmentedChunkWarningFlag = 2u;
        public const float DefaultBounce = 0.4f;
        public const float DefaultSleepSpeedSq = 0.0025f;
        public const int CarveDebrisJobStateRuntimeLength = 5;
        public const int CarveDebrisTuningVersionIndex = 5;
        public const int CarveDebrisTuningGravityYBitsIndex = 6;
        public const int CarveDebrisTuningBounceBitsIndex = 7;
        public const int CarveDebrisTuningMaxDebrisIndex = 8;
        public const int CarveDebrisTuningMassUnitsIndex = 9;
        public const int CarveDebrisJobStateLength = 10;

        public static bool IsChunkReady(byte state)
        {
            return state == (byte)MockVoxelChunkState.Ready;
        }

        public static int ResolveDebrisCap(bool lowTier, bool highEndTier, int configuredCap)
        {
            int upper = math.clamp(configuredCap > 0 ? configuredCap : UltraTierDebrisCap, LowTierDebrisCap, UltraTierDebrisCap);
            if (lowTier)
                return math.min(LowTierDebrisCap, upper);

            return highEndTier ? upper : math.min(4096, upper);
        }

        public static bool TryReadCarveDebrisTuning(NativeArray<int> jobState, out CarveDebrisTuningDTO tuning)
        {
            tuning = default;
            if (!jobState.IsCreated || jobState.Length < CarveDebrisJobStateLength)
                return false;

            int version = jobState[CarveDebrisTuningVersionIndex];
            if (version <= 0)
                return false;

            float gravityY = math.asfloat(jobState[CarveDebrisTuningGravityYBitsIndex]);
            float bounce = math.asfloat(jobState[CarveDebrisTuningBounceBitsIndex]);
            tuning = new CarveDebrisTuningDTO
            {
                Gravity = new float3(0f, math.isfinite(gravityY) ? gravityY : -5.25f, 0f),
                Bounce = math.isfinite(bounce) ? math.saturate(bounce) : DefaultBounce,
                MaxActiveDebris = math.clamp(jobState[CarveDebrisTuningMaxDebrisIndex], LowTierDebrisCap, UltraTierDebrisCap),
                MassUnitsPerParticle = math.max(1, jobState[CarveDebrisTuningMassUnitsIndex]),
                Flags = 0u,
                Version = (uint)version
            };
            return true;
        }

        public static bool TryWriteCarveDebrisTuning(NativeArray<int> jobState, in CarveDebrisTuningDTO tuning)
        {
            if (!jobState.IsCreated || jobState.Length < CarveDebrisJobStateLength)
                return false;

            float gravityY = math.isfinite(tuning.Gravity.y) ? tuning.Gravity.y : -5.25f;
            float bounce = math.isfinite(tuning.Bounce) ? math.saturate(tuning.Bounce) : DefaultBounce;
            jobState[CarveDebrisTuningVersionIndex] = math.max(1, (int)tuning.Version);
            jobState[CarveDebrisTuningGravityYBitsIndex] = math.asint(gravityY);
            jobState[CarveDebrisTuningBounceBitsIndex] = math.asint(bounce);
            jobState[CarveDebrisTuningMaxDebrisIndex] = math.clamp(tuning.MaxActiveDebris, LowTierDebrisCap, UltraTierDebrisCap);
            jobState[CarveDebrisTuningMassUnitsIndex] = math.max(1, tuning.MassUnitsPerParticle);
            return true;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockLaserCarveGateJob : IJob
    {
        public MockLaserFireSignal Signal;
        public NativeArray<int> Accepted;
        public NativeArray<uint> TelemetryRing;
        public NativeArray<int> TelemetryCursor;

        public void Execute()
        {
            bool signalFinite = math.isfinite(Signal.AupPosition.x) &&
                                math.isfinite(Signal.AupPosition.y) &&
                                math.isfinite(Signal.AupPosition.z) &&
                                math.isfinite(Signal.Radius) &&
                                Signal.Radius > 0f;
            bool ready = signalFinite && ShinobuDeltaCrusher.IsChunkReady(Signal.ChunkState);
            if (Accepted.IsCreated && Accepted.Length > 0)
                Accepted[0] = ready ? 1 : 0;

            if (ready || !TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int cursor = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = cursor + 1;
            }

            uint hash = 2166136261u;
            hash = (hash ^ Signal.MaterialHash) * 16777619u;
            hash = (hash ^ Signal.Frame) * 16777619u;
            hash = (hash ^ Signal.ChunkState) * 16777619u;
            hash = (hash ^ (uint)(int)math.clamp(Signal.AupPosition.x, -2147483647d, 2147483647d)) * 16777619u;
            hash = (hash ^ (uint)(int)math.clamp(Signal.AupPosition.y, -2147483647d, 2147483647d)) * 16777619u;
            hash = (hash ^ (uint)(int)math.clamp(Signal.AupPosition.z, -2147483647d, 2147483647d)) * 16777619u;
            TelemetryRing[cursor % TelemetryRing.Length] = hash ^ ShinobuDeltaCrusher.OrphanedChunkTelemetryFlag;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ChunkBoundarySplitJob : IJobParallelFor
    {
        public int3 MinChunk;
        public int3 ChunkSpan;
        public int3 CarveMinCell;
        public int3 CarveMaxCell;
        public int ChunkResolution;
        [NativeDisableParallelForRestriction] public NativeArray<ChunkCarveDispatchDTO> Dispatches;

        public void Execute(int index)
        {
            if (!Dispatches.IsCreated || math.any(ChunkSpan <= 0) || ChunkResolution <= 0)
                return;

            int spanXY = ChunkSpan.x * ChunkSpan.y;
            int localZ = index / spanXY;
            int remainder = index - (localZ * spanXY);
            int localY = remainder / ChunkSpan.x;
            int localX = remainder - (localY * ChunkSpan.x);
            int3 chunkCoord = MinChunk + new int3(localX, localY, localZ);
            int resolution = math.max(1, ChunkResolution);
            int3 chunkMin = chunkCoord * resolution;
            int3 chunkMax = chunkMin + resolution - 1;
            int3 overlapMin = math.max(CarveMinCell, chunkMin);
            int3 overlapMax = math.min(CarveMaxCell, chunkMax);
            bool active = math.all(overlapMin <= overlapMax);
            Dispatches[index] = new ChunkCarveDispatchDTO
            {
                ChunkCoord = chunkCoord,
                MinCell = overlapMin,
                Span = active ? (overlapMax - overlapMin) + 1 : default,
                Active = active ? (byte)1 : (byte)0
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockVoxelGridGeneratorJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<sbyte> Densities;
        public sbyte Density;

        public void Execute(int index)
        {
            if (!Densities.IsCreated || (uint)index >= (uint)Densities.Length)
                return;

            Densities[index] = Density;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InitializeDensityAccumulatorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<sbyte> SourceDensities;
        [NativeDisableParallelForRestriction] public NativeArray<int> DensityAccumulator;

        public void Execute(int index)
        {
            if (!SourceDensities.IsCreated ||
                !DensityAccumulator.IsCreated ||
                (uint)index >= (uint)SourceDensities.Length ||
                (uint)index >= (uint)DensityAccumulator.Length)
            {
                return;
            }

            DensityAccumulator[index] = SourceDensities[index];
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct VoxelSphericalCarveJob : IJobParallelFor
    {
        public int3 MinCell;
        public int3 Span;
        public int3 Dimensions;
        public float3 Center;
        public float Radius;
        public int DeltaDensity;
        [NativeDisableUnsafePtrRestriction] public int* DensityAccumulatorPtr;
        [NativeDisableUnsafePtrRestriction] public int* RemovedMassPtr;

        public void Execute(int index)
        {
            if (DensityAccumulatorPtr == null ||
                RemovedMassPtr == null ||
                math.any(Span <= 0) ||
                math.any(Dimensions <= 0) ||
                !math.isfinite(Radius) ||
                Radius <= 0f ||
                !math.all(math.isfinite(Center)))
            {
                return;
            }

            int spanXY = Span.x * Span.y;
            int localZ = index / spanXY;
            int remainder = index - (localZ * spanXY);
            int localY = remainder / Span.x;
            int localX = remainder - (localY * Span.x);
            int3 cell = MinCell + new int3(localX, localY, localZ);
            if ((uint)cell.x >= (uint)Dimensions.x ||
                (uint)cell.y >= (uint)Dimensions.y ||
                (uint)cell.z >= (uint)Dimensions.z)
            {
                return;
            }

            float3 cellCenter = new float3(cell) + 0.5f;
            float3 delta = cellCenter - Center;
            if (math.lengthsq(delta) > Radius * Radius)
                return;

            int flatIndex = (cell.z * Dimensions.y + cell.y) * Dimensions.x + cell.x;
            ref int densityRef = ref *(DensityAccumulatorPtr + flatIndex);
            int next = Interlocked.Add(ref densityRef, DeltaDensity);
            int previous = next - DeltaDensity;
            int previousClamped = math.clamp(previous, sbyte.MinValue, sbyte.MaxValue);
            int nextClamped = math.clamp(next, sbyte.MinValue, sbyte.MaxValue);
            int removed = math.max(0, previousClamped - nextClamped);
            if (removed > 0)
                Interlocked.Add(ref *RemovedMassPtr, removed);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyCarveDensityDeltasJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> DensityAccumulator;
        [NativeDisableParallelForRestriction] public NativeArray<sbyte> OutputDensities;

        public void Execute(int index)
        {
            if (!DensityAccumulator.IsCreated ||
                !OutputDensities.IsCreated ||
                (uint)index >= (uint)DensityAccumulator.Length ||
                (uint)index >= (uint)OutputDensities.Length)
            {
                return;
            }

            OutputDensities[index] = (sbyte)math.clamp(DensityAccumulator[index], sbyte.MinValue, sbyte.MaxValue);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct RleCompressSByteJob : IJob
    {
        [ReadOnly] public NativeArray<sbyte> Input;
        public NativeList<short> OutputPairs;
        public NativeArray<int> Stats;

        public void Execute()
        {
            if (!OutputPairs.IsCreated)
            {
                WriteStats(0, 0, 0, ShinobuDeltaCrusher.FragmentedChunkWarningFlag);
                return;
            }

            OutputPairs.Clear();
            if (!Input.IsCreated || Input.Length == 0)
            {
                WriteStats(0, 0, 0);
                return;
            }

            uint flags = 0u;
            short current = Input[0];
            int run = 1;
            for (int i = 1; i < Input.Length; i++)
            {
                short value = Input[i];
                if (value == current && run < short.MaxValue)
                {
                    run++;
                    continue;
                }

                if (!TryAppendRun(current, run, ref flags))
                {
                    WriteFinalStats(flags);
                    return;
                }

                current = value;
                run = 1;
            }

            if (!TryAppendRun(current, run, ref flags))
            {
                WriteFinalStats(flags);
                return;
            }

            WriteFinalStats(flags);
        }

        private bool TryAppendRun(short value, int run, ref uint flags)
        {
            if (OutputPairs.Length + 2 > OutputPairs.Capacity)
            {
                flags |= ShinobuDeltaCrusher.FragmentedChunkWarningFlag;
                return false;
            }

            OutputPairs.AddNoResize(value);
            OutputPairs.AddNoResize((short)run);
            return true;
        }

        private void WriteFinalStats(uint flags)
        {
            int rleBytes = OutputPairs.Length * UnsafeUtility.SizeOf<short>();
            int rawBytes = Input.IsCreated ? Input.Length * UnsafeUtility.SizeOf<sbyte>() : 0;
            int ratioPermille = rawBytes > 0 ? (rleBytes * 1000) / rawBytes : 0;
            if (rawBytes > 0 && rleBytes > (rawBytes * 9) / 10)
                flags |= ShinobuDeltaCrusher.FragmentedChunkWarningFlag;

            WriteStats(rawBytes, rleBytes, ratioPermille, flags);
        }

        private void WriteStats(int rawBytes, int rleBytes, int ratioPermille, uint flags = 0u)
        {
            if (!Stats.IsCreated || Stats.Length < 3)
                return;

            Stats[0] = rawBytes;
            Stats[1] = rleBytes;
            Stats[2] = ratioPermille;
            if (Stats.Length > 3)
                Stats[3] = (int)flags;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct RleDecompressSByteJob : IJob
    {
        [ReadOnly] public NativeList<short> InputPairs;
        [NativeDisableParallelForRestriction] public NativeArray<sbyte> Output;
        public NativeArray<int> WrittenCount;

        public void Execute()
        {
            if (!Output.IsCreated)
                return;

            int cursor = 0;
            int pairCount = InputPairs.IsCreated ? InputPairs.Length & ~1 : 0;
            for (int i = 0; i < pairCount; i += 2)
            {
                sbyte value = (sbyte)math.clamp(InputPairs[i], sbyte.MinValue, sbyte.MaxValue);
                int run = math.max(0, InputPairs[i + 1]);
                for (int j = 0; j < run && cursor < Output.Length; j++)
                    Output[cursor++] = value;
            }

            if (WrittenCount.IsCreated && WrittenCount.Length > 0)
                WrittenCount[0] = cursor;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DebrisMassToCountJob : IJob
    {
        [ReadOnly] public NativeArray<int> RemovedMass;
        [WriteOnly] public NativeArray<int> DebrisCount;
        public int MassUnitsPerParticle;
        public int MaxDebris;

        public void Execute()
        {
            if (!DebrisCount.IsCreated || DebrisCount.Length <= 0)
                return;

            int mass = RemovedMass.IsCreated && RemovedMass.Length > 0 ? math.max(0, RemovedMass[0]) : 0;
            int units = math.max(1, MassUnitsPerParticle);
            long count64 = ((long)mass + units - 1L) / units;
            int count = count64 > int.MaxValue ? int.MaxValue : (int)count64;
            DebrisCount[0] = math.clamp(count, 0, math.max(0, MaxDebris));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DebrisEmitFromMassJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<DebrisParticleDTO> Particles;
        [ReadOnly] public NativeArray<int> DebrisCount;
        public float3 Origin;
        public float Radius;
        public float3 Impulse;
        public uint MaterialHash;
        public uint Seed;

        public void Execute(int index)
        {
            if (!Particles.IsCreated ||
                !DebrisCount.IsCreated ||
                DebrisCount.Length <= 0 ||
                (uint)index >= (uint)math.clamp(DebrisCount[0], 0, Particles.Length))
            {
                return;
            }

            uint hash = Hash((uint)index ^ Seed);
            float3 scatter = new float3(
                HashToSigned01(hash ^ 0x9E3779B9u),
                math.abs(HashToSigned01(hash ^ 0x85EBCA6Bu)) + 0.15f,
                HashToSigned01(hash ^ 0xC2B2AE35u));
            scatter = math.normalizesafe(scatter, new float3(0f, 1f, 0f));
            float speed = math.lerp(0.5f, 4.0f, HashTo01(hash ^ 0x27D4EB2Du));
            float safeRadius = math.max(0.025f, math.isfinite(Radius) ? Radius : 0.12f);
            float chipRadius = math.lerp(0.025f, safeRadius, HashTo01(hash ^ 0x165667B1u));
            float3 safeOrigin = math.all(math.isfinite(Origin)) ? Origin : default;
            float3 safeImpulse = math.all(math.isfinite(Impulse)) ? Impulse : new float3(0f, 1f, 0f);
            Particles[index] = new DebrisParticleDTO
            {
                Position = safeOrigin + scatter * safeRadius * HashTo01(hash ^ 0xA24BAED5u),
                Radius = chipRadius,
                Velocity = math.normalizesafe(safeImpulse + scatter, scatter) * speed,
                MaterialHash = MaterialHash
            };
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        private static float HashTo01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float HashToSigned01(uint value)
        {
            return (HashTo01(value) * 2f) - 1f;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DebrisPhysicsFakeJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<DebrisParticleDTO> Particles;
        public int Count;
        public float DeltaTime;
        public float3 Gravity;
        public float Bounce;
        public float SleepSpeedSq;
        public MockWorldSampler Sampler;

        public void Execute(int index)
        {
            if (!Particles.IsCreated ||
                (uint)index >= (uint)math.clamp(Count, 0, Particles.Length))
            {
                return;
            }

            DebrisParticleDTO particle = Particles[index];
            if (particle.Radius <= 0f ||
                !math.isfinite(particle.Radius) ||
                !math.all(math.isfinite(particle.Position)) ||
                !math.all(math.isfinite(particle.Velocity)))
            {
                Particles[index] = default;
                return;
            }

            float dt = math.clamp(DeltaTime, 0f, 0.0666667f);
            float3 safeGravity = math.all(math.isfinite(Gravity)) ? Gravity : default;
            float3 velocity = particle.Velocity + safeGravity * dt;
            float3 nextPosition = particle.Position + velocity * dt;
            float distance = Sampler.SampleDistance(nextPosition) - particle.Radius;
            if (!math.isfinite(distance))
            {
                Particles[index] = default;
                return;
            }

            if (distance < 0f)
            {
                float3 sampleNormal = Sampler.SampleNormal(nextPosition);
                float3 normal = math.all(math.isfinite(sampleNormal))
                    ? math.normalizesafe(sampleNormal, new float3(0f, 1f, 0f))
                    : new float3(0f, 1f, 0f);
                nextPosition -= normal * distance;
                velocity = Reflect(velocity, normal) * math.saturate(Bounce);
                if (math.lengthsq(velocity) < math.max(0f, SleepSpeedSq))
                    velocity = default;
            }

            particle.Position = nextPosition;
            if (!math.all(math.isfinite(particle.Position)) || !math.all(math.isfinite(velocity)))
            {
                Particles[index] = default;
                return;
            }

            particle.Velocity = velocity;
            Particles[index] = particle;
        }

        private static float3 Reflect(float3 velocity, float3 normal)
        {
            return velocity - (2f * math.dot(velocity, normal) * normal);
        }
    }
}
