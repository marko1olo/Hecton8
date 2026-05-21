using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [Flags]
    public enum FloraGenomeFaultFlags : uint
    {
        None = 0u,
        MockFallbackUsed = 1u << 0,
        InvalidBinary = 1u << 1,
        SymbolCapacityClamped = 1u << 2,
        TurtleStackClamped = 1u << 3,
        MatrixCapacityClamped = 1u << 4,
        HazardCapacityClamped = 1u << 5,
        LOD2BillboardForced = 1u << 6,
        NaNDetected = 1u << 7,
        TerrainConformed = 1u << 8,
        GenerationOver2Ms = 1u << 9
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraGenomeJobStats
    {
        [FieldOffset(0)] public int GenomeCount;
        [FieldOffset(4)] public int ExpandedSymbolCount;
        [FieldOffset(8)] public int MatrixCount;
        [FieldOffset(12)] public int HazardCount;
        [FieldOffset(16)] public int IterationCount;
        [FieldOffset(20)] public int EstimatedMicroseconds;
        [FieldOffset(24)] public uint FaultFlags;
        [FieldOffset(28)] public float Biomass;
    }

    public static class FloraGenomeLSystemUtility
    {
        public const int StatsDecoderIndex = 0;
        public const int LowTierMatrixCap = 512;
        public const int MiddleTierMatrixCap = 2048;
        public const int HighTierMatrixCap = 8192;
        public const int UltraTierMatrixCap = 16384;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveIterationCap(byte hardwareTier, byte requestedIterations)
        {
            int requested = math.clamp((int)requestedIterations, 0, FloraGenomeLSystemConstants.MaxRuntimeIterations);
            switch ((FloraGenomeHardwareTier)hardwareTier)
            {
                case FloraGenomeHardwareTier.Low:
                    return math.min(requested, FloraGenomeLSystemConstants.ToasterIterationCap);
                case FloraGenomeHardwareTier.Middle:
                    return math.min(requested, 4);
                case FloraGenomeHardwareTier.High:
                case FloraGenomeHardwareTier.Ultra:
                    return requested;
                default:
                    return FloraGenomeLSystemConstants.ToasterIterationCap;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMatrixCap(byte hardwareTier, int bufferCapacity)
        {
            int tierCap = LowTierMatrixCap;
            switch ((FloraGenomeHardwareTier)hardwareTier)
            {
                case FloraGenomeHardwareTier.Middle:
                    tierCap = MiddleTierMatrixCap;
                    break;
                case FloraGenomeHardwareTier.High:
                    tierCap = HighTierMatrixCap;
                    break;
                case FloraGenomeHardwareTier.Ultra:
                    tierCap = UltraTierMatrixCap;
                    break;
            }

            return math.max(0, math.min(bufferCapacity, tierCap));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AdvanceLcg(uint state)
        {
            return (state * 1664525u) + 1013904223u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Unit01(uint state)
        {
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveScaleVariance(uint plantHash, uint speciesHash, uint worldSeed)
        {
            uint state = plantHash ^ (speciesHash * 747796405u) ^ (worldSeed * 2891336453u);
            state = AdvanceLcg(state);
            return math.lerp(0.88f, 1.16f, Unit01(state));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashPlant(FloraAupCell aupCell, uint speciesHash, uint worldSeed, ushort chunkSlot)
        {
            uint hash = 2166136261u ^ speciesHash ^ worldSeed ^ chunkSlot;
            hash = (hash ^ (uint)aupCell.X) * 16777619u;
            hash = (hash ^ (uint)(aupCell.X >> 32)) * 16777619u;
            hash = (hash ^ (uint)aupCell.Y) * 16777619u;
            hash = (hash ^ (uint)(aupCell.Y >> 32)) * 16777619u;
            hash = (hash ^ (uint)aupCell.Z) * 16777619u;
            hash = (hash ^ (uint)(aupCell.Z >> 32)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedString32Bytes SingleSymbolAxiom(byte symbol)
        {
            FixedString32Bytes axiom = default;
            axiom.Add(symbol);
            return axiom;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct FloraGenomeDecoderJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> RawBytes;
        public int RawByteCount;
        [NoAlias] public NativeArray<FloraGenomeDTO> Genomes;
        [NoAlias] public NativeArray<FloraGenomeJobStats> Stats;

        public void Execute()
        {
            int decodedCount = 0;
            uint faultFlags = 0u;
            if (!Genomes.IsCreated || Genomes.Length <= 0)
            {
                WriteStats(0, 0u);
                return;
            }

            if (RawBytes.IsCreated && RawByteCount >= UnsafeUtility.SizeOf<FloraGenomeBinaryHeader>())
            {
                decodedCount = DecodeBinary(ref faultFlags);
            }

            if (decodedCount <= 0)
            {
                decodedCount = MockGenomeGenerator.Populate(Genomes);
                faultFlags |= (uint)FloraGenomeFaultFlags.MockFallbackUsed;
            }

            WriteStats(decodedCount, faultFlags);
        }

        private int DecodeBinary(ref uint faultFlags)
        {
            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(RawBytes);
            FloraGenomeBinaryHeader header = default;
            UnsafeUtility.MemCpy(&header, rawPtr, UnsafeUtility.SizeOf<FloraGenomeBinaryHeader>());

            if (header.Magic != FloraGenomeLSystemConstants.GenomeBinaryMagic ||
                header.HeaderBytes < UnsafeUtility.SizeOf<FloraGenomeBinaryHeader>() ||
                header.RecordStrideBytes < FloraGenomeLSystemConstants.FloraGenomeStrideBytes ||
                header.RecordCount <= 0)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.InvalidBinary;
                return 0;
            }

            int payloadStart = header.HeaderBytes;
            int availableBytes = RawByteCount - payloadStart;
            if (availableBytes < FloraGenomeLSystemConstants.FloraGenomeStrideBytes)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.InvalidBinary;
                return 0;
            }

            int maxRecordsInPayload = availableBytes / header.RecordStrideBytes;
            int count = math.min(math.min(header.RecordCount, maxRecordsInPayload), Genomes.Length);
            for (int i = 0; i < count; i++)
            {
                FloraGenomeDTO genome = default;
                byte* source = rawPtr + payloadStart + (i * header.RecordStrideBytes);
                if (header.RecordStrideBytes == FloraGenomeLSystemConstants.FloraGenomeStrideBytes)
                    genome = UnsafeUtility.ReadArrayElement<FloraGenomeDTO>(source, 0);
                else
                    UnsafeUtility.MemCpy(&genome, source, FloraGenomeLSystemConstants.FloraGenomeStrideBytes);

                Genomes[i] = NormalizeGenome(genome, i);
            }

            return count;
        }

        private static FloraGenomeDTO NormalizeGenome(FloraGenomeDTO genome, int profileFallback)
        {
            if (!math.isfinite(genome.BaseScale) || genome.BaseScale <= 0f)
                genome.BaseScale = 1f;
            if (!math.isfinite(genome.BranchAngleRadians) || math.abs(genome.BranchAngleRadians) < 0.001f)
                genome.BranchAngleRadians = math.radians(22f);
            if (!math.isfinite(genome.SegmentLengthMeters) || genome.SegmentLengthMeters <= 0f)
                genome.SegmentLengthMeters = 0.25f;
            if (!math.isfinite(genome.BiolumThreshold))
                genome.BiolumThreshold = 0.5f;
            if (genome.Axiom.Length <= 0 || genome.Axiom.Length > FixedString32Bytes.UTF8MaxLengthInBytes)
                genome.Axiom = FloraGenomeLSystemUtility.SingleSymbolAxiom((byte)'X');

            genome.MaxIterations = (byte)math.clamp((int)genome.MaxIterations, 1, FloraGenomeLSystemConstants.MaxRuntimeIterations);
            genome.RuleProfile = (byte)math.clamp((int)genome.RuleProfile, 0, 2);
            if (genome.RuleProfile == 0 && profileFallback > 0)
                genome.RuleProfile = (byte)math.clamp(profileFallback, 0, 2);

            return genome;
        }

        private void WriteStats(int genomeCount, uint faultFlags)
        {
            if (!Stats.IsCreated || Stats.Length <= FloraGenomeLSystemUtility.StatsDecoderIndex)
                return;

            Stats[FloraGenomeLSystemUtility.StatsDecoderIndex] = new FloraGenomeJobStats
            {
                GenomeCount = genomeCount,
                ExpandedSymbolCount = 0,
                MatrixCount = 0,
                HazardCount = 0,
                IterationCount = 0,
                EstimatedMicroseconds = 0,
                FaultFlags = faultFlags,
                Biomass = 0f
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct IterativeLSystemExpanderJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<FloraGenomeDTO> Genomes;
        public int GenomeIndex;
        public byte HardwareTier;
        [NoAlias] public NativeArray<byte> ExpandedSymbols;
        [NoAlias] public NativeArray<byte> ScratchSymbols;
        [NoAlias] public NativeArray<FloraGenomeJobStats> Stats;

        public void Execute()
        {
            uint faultFlags = 0u;
            if (!Genomes.IsCreated ||
                !ExpandedSymbols.IsCreated ||
                !ScratchSymbols.IsCreated ||
                (uint)GenomeIndex >= (uint)Genomes.Length)
            {
                WriteStats(0, 0, faultFlags | (uint)FloraGenomeFaultFlags.InvalidBinary);
                return;
            }

            FloraGenomeDTO genome = Genomes[GenomeIndex];
            int iterations = FloraGenomeLSystemUtility.ResolveIterationCap(HardwareTier, genome.MaxIterations);
            int expandedCount = 0;
            int scratchCount = 0;
            int axiomLength = math.min(genome.Axiom.Length, FixedString32Bytes.UTF8MaxLengthInBytes);
            for (int i = 0; i < axiomLength; i++)
            {
                if (!TryAddSymbol(ExpandedSymbols, ref expandedCount, genome.Axiom[i], ref faultFlags))
                    break;
            }

            bool primaryHasLatest = true;
            int completedIterations = 0;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (primaryHasLatest)
                {
                    scratchCount = 0;
                    ExpandList(ExpandedSymbols, expandedCount, ScratchSymbols, ref scratchCount, genome.RuleProfile, ref faultFlags);
                    primaryHasLatest = false;
                }
                else
                {
                    expandedCount = 0;
                    ExpandList(ScratchSymbols, scratchCount, ExpandedSymbols, ref expandedCount, genome.RuleProfile, ref faultFlags);
                    primaryHasLatest = true;
                }

                completedIterations++;
                if ((faultFlags & (uint)FloraGenomeFaultFlags.SymbolCapacityClamped) != 0u)
                    break;
            }

            if (!primaryHasLatest)
            {
                expandedCount = 0;
                for (int i = 0; i < scratchCount; i++)
                {
                    if (!TryAddSymbol(ExpandedSymbols, ref expandedCount, ScratchSymbols[i], ref faultFlags))
                        break;
                }
            }

            WriteStats(expandedCount, completedIterations, faultFlags);
        }

        private static void ExpandList(NativeArray<byte> source, int sourceCount, NativeArray<byte> destination, ref int destinationCount, byte ruleProfile, ref uint faultFlags)
        {
            int count = math.min(sourceCount, source.IsCreated ? source.Length : 0);
            for (int i = 0; i < count; i++)
            {
                ExpandSymbol(source[i], ruleProfile, destination, ref destinationCount, ref faultFlags);
                if ((faultFlags & (uint)FloraGenomeFaultFlags.SymbolCapacityClamped) != 0u)
                    return;
            }
        }

        private static void ExpandSymbol(byte symbol, byte ruleProfile, NativeArray<byte> destination, ref int destinationCount, ref uint faultFlags)
        {
            if (symbol == (byte)'X')
            {
                if (ruleProfile == 0)
                {
                    AppendKelpRule(destination, ref destinationCount, ref faultFlags);
                    return;
                }

                if (ruleProfile == 1)
                {
                    AppendCoralRule(destination, ref destinationCount, ref faultFlags);
                    return;
                }

                AppendSpongeRule(destination, ref destinationCount, ref faultFlags);
                return;
            }

            if (symbol == (byte)'F' && ruleProfile == 2)
            {
                TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
                TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
                return;
            }

            TryAddSymbol(destination, ref destinationCount, symbol, ref faultFlags);
        }

        private static void AppendKelpRule(NativeArray<byte> destination, ref int destinationCount, ref uint faultFlags)
        {
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'[', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'+', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)']', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'[', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'-', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)']', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'L', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
        }

        private static void AppendCoralRule(NativeArray<byte> destination, ref int destinationCount, ref uint faultFlags)
        {
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'[', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'+', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)']', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'[', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'-', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)']', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
        }

        private static void AppendSpongeRule(NativeArray<byte> destination, ref int destinationCount, ref uint faultFlags)
        {
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'[', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'+', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'F', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'L', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)']', ref faultFlags);
            TryAddSymbol(destination, ref destinationCount, (byte)'X', ref faultFlags);
        }

        private static bool TryAddSymbol(NativeArray<byte> destination, ref int destinationCount, byte symbol, ref uint faultFlags)
        {
            if (!destination.IsCreated || destinationCount >= destination.Length)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.SymbolCapacityClamped;
                return false;
            }

            destination[destinationCount++] = symbol;
            return true;
        }

        private void WriteStats(int symbolCount, int iterations, uint faultFlags)
        {
            if (!Stats.IsCreated || Stats.Length <= FloraGenomeLSystemUtility.StatsDecoderIndex)
                return;

            FloraGenomeJobStats stats = Stats[FloraGenomeLSystemUtility.StatsDecoderIndex];
            stats.ExpandedSymbolCount = symbolCount;
            stats.MatrixCount = 0;
            stats.HazardCount = 0;
            stats.IterationCount = iterations;
            stats.EstimatedMicroseconds = 0;
            stats.FaultFlags = faultFlags;
            stats.Biomass = 0f;
            Stats[FloraGenomeLSystemUtility.StatsDecoderIndex] = stats;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct TurtleGraphicsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<FloraGenomeDTO> Genomes;
        [ReadOnly, NoAlias] public NativeArray<FloraPlantSeedDTO> PlantSeeds;
        [ReadOnly, NoAlias] public NativeArray<byte> Symbols;
        public int GenomeIndex;
        public int PlantIndex;
        public uint FrameIndex;
        public byte HardwareTier;
        [NoAlias] public NativeArray<TurtleStackFrameDTO> TurtleStack;
        [NoAlias] public NativeArray<BranchMatrixDTO> BranchMatrices;
        public int MatrixWriteOffset;
        public int MatrixWriteCapacity;
        [NoAlias] public NativeArray<HazardZoneDTO> HazardZones;
        public int HazardWriteOffset;
        public int HazardWriteCapacity;
        [NoAlias] public NativeArray<FloraGenomeBlackBoxEntry> BlackBox;
        [NoAlias] public NativeArray<int> BlackBoxCursor;
        [NoAlias] public NativeArray<FloraGenomeJobStats> Stats;
        private int _branchMatrixCount;
        private int _hazardZoneCount;

        public void Execute()
        {
            _branchMatrixCount = 0;
            _hazardZoneCount = 0;
            uint faultFlags = 0u;
            if (!Genomes.IsCreated ||
                !PlantSeeds.IsCreated ||
                (uint)GenomeIndex >= (uint)Genomes.Length ||
                (uint)PlantIndex >= (uint)PlantSeeds.Length)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.InvalidBinary;
                WriteStatsAndTelemetry(default, default, 0, 0, 0f, faultFlags);
                return;
            }

            FloraGenomeDTO genome = Genomes[GenomeIndex];
            FloraPlantSeedDTO seed = PlantSeeds[PlantIndex];
            uint plantHash = seed.PlantHash == 0u
                ? FloraGenomeLSystemUtility.HashPlant(seed.AupCell, genome.SpeciesHash, seed.WorldSeed, seed.ChunkSlot)
                : seed.PlantHash;
            seed.PlantHash = plantHash;

            float variance = FloraGenomeLSystemUtility.ResolveScaleVariance(plantHash, genome.SpeciesHash, seed.WorldSeed);
            uint varianceState = FloraGenomeLSystemUtility.AdvanceLcg(plantHash ^ genome.SpeciesHash ^ seed.WorldSeed ^ 0xA511E9B3u);
            float branchAngleRadians = genome.BranchAngleRadians * math.lerp(0.92f, 1.08f, FloraGenomeLSystemUtility.Unit01(varianceState));
            varianceState = FloraGenomeLSystemUtility.AdvanceLcg(varianceState);
            float segmentLengthMeters = genome.SegmentLengthMeters * math.lerp(0.90f, 1.14f, FloraGenomeLSystemUtility.Unit01(varianceState));
            branchAngleRadians = math.select(math.radians(22f), branchAngleRadians, math.isfinite(branchAngleRadians));
            segmentLengthMeters = math.max(0.01f, math.select(0.25f, segmentLengthMeters, math.isfinite(segmentLengthMeters)));

            TurtleStackFrameDTO frame = CreateRootFrame(seed, genome, plantHash, variance, ref faultFlags);
            int branchCapacity = ResolveBranchWriteCapacity();
            int matrixCap = FloraGenomeLSystemUtility.ResolveMatrixCap(HardwareTier, branchCapacity);
            if (!BranchMatrices.IsCreated || matrixCap <= 0)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.MatrixCapacityClamped;
                WriteStatsAndTelemetry(seed, genome, ResolveSymbolCount(), 0, 0f, faultFlags);
                return;
            }

            int stackDepth = 0;
            int segmentIndex = 0;
            float biomass = 0f;

            if ((genome.HazardFlags & (byte)(FloraHazardFlags.Caustic | FloraHazardFlags.Thorny)) != 0)
                TryAddHazardZone(frame.Position, genome, plantHash, frame.Scale, ref faultFlags);

            int symbolCount = ResolveSymbolCount();
            for (int i = 0; i < symbolCount; i++)
            {
                byte symbol = Symbols[i];
                if (symbol == (byte)'F')
                {
                    if (_branchMatrixCount + 1 >= matrixCap)
                    {
                        TryAddBillboard(frame, genome, plantHash, segmentIndex, 1, ref biomass, ref faultFlags);
                        break;
                    }

                    StepForward(ref frame, genome, plantHash, segmentIndex, segmentLengthMeters, false, ref biomass, ref faultFlags);
                    segmentIndex++;
                    continue;
                }

                if (symbol == (byte)'L')
                {
                    TryAddBillboard(frame, genome, plantHash, segmentIndex, 0, ref biomass, ref faultFlags);
                    segmentIndex++;
                    continue;
                }

                if (symbol == (byte)'+')
                {
                    Bend(ref frame, branchAngleRadians);
                    continue;
                }

                if (symbol == (byte)'-')
                {
                    Bend(ref frame, -branchAngleRadians);
                    continue;
                }

                if (symbol == (byte)'[')
                {
                    if (TurtleStack.IsCreated && stackDepth < TurtleStack.Length)
                    {
                        TurtleStack[stackDepth++] = frame;
                    }
                    else
                    {
                        faultFlags |= (uint)FloraGenomeFaultFlags.TurtleStackClamped;
                    }

                    continue;
                }

                if (symbol == (byte)']')
                {
                    if (stackDepth > 0)
                        frame = TurtleStack[--stackDepth];
                }
            }

            if (_branchMatrixCount == 0)
                TryAddBillboard(frame, genome, plantHash, segmentIndex, 1, ref biomass, ref faultFlags);

            if (!IsFinite(frame.Position))
                faultFlags |= (uint)FloraGenomeFaultFlags.NaNDetected;

            WriteStatsAndTelemetry(seed, genome, symbolCount, segmentIndex, biomass, faultFlags);
        }

        private TurtleStackFrameDTO CreateRootFrame(FloraPlantSeedDTO seed, FloraGenomeDTO genome, uint plantHash, float variance, ref uint faultFlags)
        {
            float3 position = seed.LocalPosition;
            float terrainHeight = MockTerrainHeight.SampleHeight(position.xz);
            if (position.y < terrainHeight)
            {
                position.y = terrainHeight;
                faultFlags |= (uint)FloraGenomeFaultFlags.TerrainConformed;
            }

            return new TurtleStackFrameDTO
            {
                Position = position,
                Scale = math.max(0.01f, genome.BaseScale * variance),
                Rotation = quaternion.identity,
                RngState = FloraGenomeLSystemUtility.AdvanceLcg(plantHash),
                Depth = 0,
                Reserved0 = 0,
                BishopUp = new float3(0f, 0f, 1f),
                Reserved1 = 0f
            };
        }

        private void StepForward(
            ref TurtleStackFrameDTO frame,
            FloraGenomeDTO genome,
            uint plantHash,
            int segmentIndex,
            float segmentLengthMeters,
            bool billboard,
            ref float biomass,
            ref uint faultFlags)
        {
            float length = math.max(0.01f, segmentLengthMeters * frame.Scale);
            float radius = math.max(0.0125f, frame.Scale * 0.035f);
            float3 direction = math.mul(frame.Rotation, new float3(0f, 1f, 0f));
            float3 center = frame.Position + (direction * (length * 0.5f));
            float terrainHeight = MockTerrainHeight.SampleHeight(center.xz);
            bool conformed = false;
            if (center.y < terrainHeight)
            {
                center.y = terrainHeight;
                conformed = true;
                faultFlags |= (uint)FloraGenomeFaultFlags.TerrainConformed;
                ApplyTerrainUpwardBias(ref frame);
                direction = math.mul(frame.Rotation, new float3(0f, 1f, 0f));
            }

            float4x4 matrix = float4x4.TRS(center, frame.Rotation, new float3(radius, length, radius));
            TryAddMatrix(matrix, genome, plantHash, segmentIndex, billboard, length * frame.Scale, ref biomass, ref faultFlags);
            frame.Position = conformed ? center + (direction * (length * 0.5f)) : frame.Position + (direction * length);
        }

        private void TryAddBillboard(
            TurtleStackFrameDTO frame,
            FloraGenomeDTO genome,
            uint plantHash,
            int segmentIndex,
            byte capacityForced,
            ref float biomass,
            ref uint faultFlags)
        {
            float size = math.max(0.18f, frame.Scale * 0.32f);
            float4x4 matrix = float4x4.TRS(frame.Position, frame.Rotation, new float3(size, size, size));
            if (TryAddMatrix(matrix, genome, plantHash, segmentIndex, true, size * 0.5f, ref biomass, ref faultFlags))
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.LOD2BillboardForced;
                if (capacityForced != 0)
                    faultFlags |= (uint)FloraGenomeFaultFlags.MatrixCapacityClamped;
            }
        }

        private bool TryAddMatrix(
            float4x4 matrix,
            FloraGenomeDTO genome,
            uint plantHash,
            int segmentIndex,
            bool billboard,
            float biomassDelta,
            ref float biomass,
            ref uint faultFlags)
        {
            int branchCapacity = ResolveBranchWriteCapacity();
            if (!BranchMatrices.IsCreated || _branchMatrixCount >= branchCapacity)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.MatrixCapacityClamped;
                return false;
            }

            float biolum = ((genome.TraitFlags & (uint)FloraGenomeTraitFlags.Bioluminescent) != 0u)
                ? math.saturate((FrameSafeBiomass(biomass + biomassDelta) * 0.08f) - genome.BiolumThreshold + 0.5f)
                : 0f;
            float3 color = DecodeRgb01(genome.PackedColorHDR);

            byte lodFlags = billboard ? (byte)FloraMatrixLodFlags.LOD2Billboard : (byte)FloraMatrixLodFlags.Segment;
            if ((faultFlags & (uint)FloraGenomeFaultFlags.TerrainConformed) != 0u)
                lodFlags |= (byte)FloraMatrixLodFlags.TerrainConformed;
            if (biolum > 0f)
                lodFlags |= (byte)FloraMatrixLodFlags.BiolumPayload;

            BranchMatrices[MatrixWriteOffset + _branchMatrixCount] = new BranchMatrixDTO
            {
                Matrix = matrix,
                CustomData = new float4(color * biolum, biolum),
                SpeciesHash = genome.SpeciesHash,
                PlantHash = plantHash,
                SegmentIndex = (ushort)math.min(segmentIndex, ushort.MaxValue),
                LodFlags = lodFlags,
                HazardFlags = genome.HazardFlags,
                Reserved0 = 0u
            };

            _branchMatrixCount++;
            biomass += biomassDelta;
            return true;
        }

        private void TryAddHazardZone(float3 root, FloraGenomeDTO genome, uint plantHash, float scale, ref uint faultFlags)
        {
            int hazardCapacity = ResolveHazardWriteCapacity();
            if (!HazardZones.IsCreated || _hazardZoneCount >= hazardCapacity)
            {
                faultFlags |= (uint)FloraGenomeFaultFlags.HazardCapacityClamped;
                return;
            }

            HazardZones[HazardWriteOffset + _hazardZoneCount] = new HazardZoneDTO
            {
                Center = root,
                RadiusMeters = math.max(0.35f, scale * 0.65f),
                SpeciesHash = genome.SpeciesHash,
                PlantHash = plantHash,
                HazardFlags = genome.HazardFlags,
                Reserved0 = 0,
                Biomass = 0f
            };
            _hazardZoneCount++;
        }

        private int ResolveSymbolCount()
        {
            if (!Symbols.IsCreated)
                return 0;

            int symbolCount = Symbols.Length;
            if (Stats.IsCreated && Stats.Length > FloraGenomeLSystemUtility.StatsDecoderIndex)
            {
                FloraGenomeJobStats stats = Stats[FloraGenomeLSystemUtility.StatsDecoderIndex];
                symbolCount = math.min(symbolCount, math.max(0, stats.ExpandedSymbolCount));
            }

            return symbolCount;
        }

        private int ResolveBranchWriteCapacity()
        {
            if (!BranchMatrices.IsCreated || MatrixWriteOffset < 0 || MatrixWriteOffset >= BranchMatrices.Length)
                return 0;

            int available = BranchMatrices.Length - MatrixWriteOffset;
            return MatrixWriteCapacity > 0 ? math.min(MatrixWriteCapacity, available) : available;
        }

        private int ResolveHazardWriteCapacity()
        {
            if (!HazardZones.IsCreated || HazardWriteOffset < 0 || HazardWriteOffset >= HazardZones.Length)
                return 0;

            int available = HazardZones.Length - HazardWriteOffset;
            return HazardWriteCapacity > 0 ? math.min(HazardWriteCapacity, available) : available;
        }

        private static void Bend(ref TurtleStackFrameDTO frame, float angle)
        {
            float3 forward = math.mul(frame.Rotation, new float3(0f, 1f, 0f));
            float3 side = math.normalizesafe(math.cross(frame.BishopUp, forward), new float3(1f, 0f, 0f));
            quaternion bend = quaternion.AxisAngle(side, angle);
            frame.Rotation = math.normalize(math.mul(bend, frame.Rotation));
            float3 bentForward = math.mul(frame.Rotation, new float3(0f, 1f, 0f));
            frame.BishopUp = math.normalizesafe(math.cross(bentForward, side), frame.BishopUp);
        }

        private static void ApplyTerrainUpwardBias(ref TurtleStackFrameDTO frame)
        {
            frame.Rotation = math.normalize(math.slerp(frame.Rotation, quaternion.identity, 0.35f));
            frame.BishopUp = math.normalizesafe(math.mul(frame.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FrameSafeBiomass(float value)
        {
            return math.select(value, 0f, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DecodeRgb01(uint packedColor)
        {
            float inv255 = 1f / 255f;
            return new float3(
                ((packedColor >> 24) & 0xFFu) * inv255,
                ((packedColor >> 16) & 0xFFu) * inv255,
                ((packedColor >> 8) & 0xFFu) * inv255);
        }

        private void WriteStatsAndTelemetry(
            FloraPlantSeedDTO seed,
            FloraGenomeDTO genome,
            int symbolCount,
            int segmentCount,
            float biomass,
            uint faultFlags)
        {
            int matrixCount = _branchMatrixCount;
            int hazardCount = _hazardZoneCount;
            int estimatedMicroseconds = 16 + ((symbolCount * 3) / 10) + (matrixCount * 2) + (hazardCount * 3);
            if (estimatedMicroseconds > 2000)
                faultFlags |= (uint)FloraGenomeFaultFlags.GenerationOver2Ms;

            StampHazardBiomass(biomass);

            if (Stats.IsCreated && Stats.Length > FloraGenomeLSystemUtility.StatsDecoderIndex)
            {
                FloraGenomeJobStats stats = Stats[FloraGenomeLSystemUtility.StatsDecoderIndex];
                stats.MatrixCount = matrixCount;
                stats.HazardCount = hazardCount;
                stats.EstimatedMicroseconds = estimatedMicroseconds;
                stats.FaultFlags |= faultFlags;
                stats.Biomass = biomass;
                Stats[FloraGenomeLSystemUtility.StatsDecoderIndex] = stats;
            }

            if (!BlackBox.IsCreated || BlackBox.Length <= 0 || !BlackBoxCursor.IsCreated || BlackBoxCursor.Length <= 0)
                return;

            int cursor = BlackBoxCursor[0];
            int writeIndex = (cursor < 0 ? 0 : cursor) % BlackBox.Length;
            BlackBox[writeIndex] = new FloraGenomeBlackBoxEntry
            {
                FrameIndex = FrameIndex,
                SpeciesHash = genome.SpeciesHash,
                PlantHash = seed.PlantHash,
                ExpandedSymbolCount = symbolCount,
                MatrixCount = matrixCount,
                HazardCount = hazardCount,
                EstimatedMicroseconds = estimatedMicroseconds,
                Biomass = biomass,
                RootPosition = seed.LocalPosition,
                FaultFlags = faultFlags,
                IterationCount = (uint)segmentCount,
                Reserved0 = FloraGenomeLSystemConstants.OwnerHash,
                Reserved1 = 0u
            };
            BlackBoxCursor[0] = (writeIndex + 1) % BlackBox.Length;
        }

        private void StampHazardBiomass(float biomass)
        {
            if (!HazardZones.IsCreated || _hazardZoneCount <= 0)
                return;

            int capacity = ResolveHazardWriteCapacity();
            int count = math.min(_hazardZoneCount, capacity);
            for (int i = 0; i < count; i++)
            {
                int index = HazardWriteOffset + i;
                HazardZoneDTO hazard = HazardZones[index];
                hazard.Biomass = biomass;
                HazardZones[index] = hazard;
            }
        }
    }
}
