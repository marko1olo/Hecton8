using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Chunk-scoped buffer descriptor. Runtime instances point at Vault-owned memory; editor preview may own temp arrays.
    /// </summary>
    public struct FloraGenomeChunkWorkspace : IDisposable
    {
        public NativeArray<byte> ExpandedSymbols;
        public NativeArray<byte> ScratchSymbols;
        public NativeArray<BranchMatrixDTO> BranchMatrices;
        public NativeArray<HazardZoneDTO> HazardZones;
        public NativeArray<TurtleStackFrameDTO> TurtleStack;
        public byte OwnsNativeMemory;
        public byte IsVaultBacked;

        public bool IsCreated =>
            ExpandedSymbols.IsCreated &&
            ScratchSymbols.IsCreated &&
            BranchMatrices.IsCreated &&
            HazardZones.IsCreated &&
            TurtleStack.IsCreated;

        public static FloraGenomeChunkWorkspace FromVault(
            NativeArray<byte> expandedSymbols,
            NativeArray<byte> scratchSymbols,
            NativeArray<BranchMatrixDTO> branchMatrices,
            NativeArray<HazardZoneDTO> hazardZones,
            NativeArray<TurtleStackFrameDTO> turtleStack)
        {
            return new FloraGenomeChunkWorkspace
            {
                ExpandedSymbols = expandedSymbols,
                ScratchSymbols = scratchSymbols,
                BranchMatrices = branchMatrices,
                HazardZones = hazardZones,
                TurtleStack = turtleStack,
                OwnsNativeMemory = 0,
                IsVaultBacked = 1
            };
        }

        public void Dispose()
        {
            if (OwnsNativeMemory == 0)
                return;
            if (ExpandedSymbols.IsCreated)
                ExpandedSymbols.Dispose();
            if (ScratchSymbols.IsCreated)
                ScratchSymbols.Dispose();
            if (BranchMatrices.IsCreated)
                BranchMatrices.Dispose();
            if (HazardZones.IsCreated)
                HazardZones.Dispose();
            if (TurtleStack.IsCreated)
                TurtleStack.Dispose();
            OwnsNativeMemory = 0;
            IsVaultBacked = 0;
        }
    }

    /// <summary>
    /// Non-owning runtime ticket for a scheduled plant generation slice. Complete only after IsCompleted is true.
    /// </summary>
    public struct FloraGenomeGenerationTicket
    {
        public JobHandle Handle;
        public FloraPlantSeedDTO Seed;
        public FloraGenomeDTO Genome;
        public int MatrixOffset;
        public uint FrameIndex;
        public byte IsCreated;
    }

    /// <summary>
    /// Stateless-facing facade over Vault-owned flora genome buffers. No Unity Update loop and no private NativeArray ownership.
    /// </summary>
    public sealed unsafe class FloraGenomeVaultRuntime
    {
        public const int DefaultRawBytesCapacity = 5 * 1024 * 1024;
        public const int DefaultCsvScratchCapacity = 256 * 1024;
        public const uint OverloadWarningHash = 0x464F5632u; // FOV2

        private IDataVault _vault;
        private VaultBufferHandle<byte> _rawBytesHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<byte> _expandedSymbolsHandle;
        private VaultBufferHandle<byte> _scratchSymbolsHandle;
        private VaultBufferHandle<FloraGenomeDTO> _genomesHandle;
        private VaultBufferHandle<FloraPlantSeedDTO> _plantSeedsHandle;
        private VaultBufferHandle<BranchMatrixDTO> _branchMatricesHandle;
        private VaultBufferHandle<HazardZoneDTO> _hazardsHandle;
        private VaultBufferHandle<TurtleStackFrameDTO> _turtleStackHandle;
        private VaultBufferHandle<FloraGenomeJobStats> _statsHandle;
        private VaultBufferHandle<FloraGenomeBlackBoxEntry> _blackBoxHandle;
        private VaultBufferHandle<int> _blackBoxCursorHandle;
        private Task<int> _pendingBinaryRead;
        private bool _rawBufferLocked;
        private bool _generationInFlight;
        private int _genomeCount;
        private long _csvLastWriteTicks;

        public int GenomeCount => _genomeCount;

        private sealed class BinaryReadRequest
        {
            public string ProjectRoot;
            public NativeArray<byte> RawBytes;
        }

        public bool BindVault(
            IDataVault vault,
            int genomeCapacity = FloraGenomeLSystemConstants.DefaultGenomeCapacity,
            int matrixCapacity = FloraGenomeLSystemConstants.DefaultBranchMatrixCapacity,
            int hazardCapacity = FloraGenomeLSystemConstants.DefaultHazardCapacity)
        {
            if (vault == null)
                return false;

            _vault = vault;
            _rawBytesHandle = vault.GetBufferHandle<byte>(BufferID.FloraGenomeRawBytes, DefaultRawBytesCapacity, SystemID.FloraGenomics, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(BufferID.FloraGenomeCsvScratch, DefaultCsvScratchCapacity, SystemID.FloraGenomics, NativeArrayOptions.UninitializedMemory);
            _expandedSymbolsHandle = vault.GetBufferHandle<byte>(BufferID.FloraGenomeExpandedSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, SystemID.FloraGenomics, NativeArrayOptions.UninitializedMemory);
            _scratchSymbolsHandle = vault.GetBufferHandle<byte>(BufferID.FloraGenomeScratchSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, SystemID.FloraGenomics, NativeArrayOptions.UninitializedMemory);
            _genomesHandle = vault.GetBufferHandle<FloraGenomeDTO>(BufferID.FloraGenomeDtos, genomeCapacity, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _plantSeedsHandle = vault.GetBufferHandle<FloraPlantSeedDTO>(BufferID.FloraGenomePlantSeeds, 1, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _branchMatricesHandle = vault.GetBufferHandle<BranchMatrixDTO>(BufferID.FloraGenomeBranchMatrices, matrixCapacity, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _hazardsHandle = vault.GetBufferHandle<HazardZoneDTO>(BufferID.FloraGenomeHazardZones, hazardCapacity, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _turtleStackHandle = vault.GetBufferHandle<TurtleStackFrameDTO>(BufferID.FloraGenomeTurtleStack, FloraGenomeLSystemConstants.DefaultTurtleStackCapacity, SystemID.FloraGenomics, NativeArrayOptions.UninitializedMemory);
            _statsHandle = vault.GetBufferHandle<FloraGenomeJobStats>(BufferID.FloraGenomeStats, 1, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _blackBoxHandle = vault.GetBufferHandle<FloraGenomeBlackBoxEntry>(BufferID.FloraGenomeBlackBox, FloraGenomeLSystemConstants.BlackBoxFrameCount, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);
            _blackBoxCursorHandle = vault.GetBufferHandle<int>(BufferID.FloraGenomeBlackBoxCursor, 1, SystemID.FloraGenomics, NativeArrayOptions.ClearMemory);

            SignalBus<FloraSpawnedSignal>.Configure(256, 4096, 512, FloraGenomeLSystemConstants.OwnerHash);
            DecodeLoadedBytes(0);
            return true;
        }

        public bool TryCreateChunkWorkspace(out FloraGenomeChunkWorkspace workspace)
        {
            workspace = default;
            if (_vault == null)
                return false;

            NativeArray<byte> expandedSymbols = _expandedSymbolsHandle.Resolve(_vault);
            NativeArray<byte> scratchSymbols = _scratchSymbolsHandle.Resolve(_vault);
            NativeArray<BranchMatrixDTO> branchMatrices = _branchMatricesHandle.Resolve(_vault);
            NativeArray<HazardZoneDTO> hazardZones = _hazardsHandle.Resolve(_vault);
            NativeArray<TurtleStackFrameDTO> turtleStack = _turtleStackHandle.Resolve(_vault);
            workspace = FloraGenomeChunkWorkspace.FromVault(expandedSymbols, scratchSymbols, branchMatrices, hazardZones, turtleStack);
            return workspace.IsCreated;
        }

        public bool BeginLoadGenomeBinaryAsync(string projectRoot)
        {
            if (_vault == null || _pendingBinaryRead != null)
                return false;

            NativeArray<byte> rawBytes = _rawBytesHandle.Resolve(_vault);
            if (!rawBytes.IsCreated || rawBytes.Length <= 0)
                return false;

            if (!_vault.TryLockBuffer(BufferID.FloraGenomeRawBytes, SystemID.FloraGenomics))
                return false;

            _rawBufferLocked = true;
            BinaryReadRequest request = new BinaryReadRequest
            {
                ProjectRoot = projectRoot,
                RawBytes = rawBytes
            };
            _pendingBinaryRead = Task.Factory.StartNew(
                ReadGenomeBinaryWorker,
                request,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            return true;
        }

        public bool TryCompletePendingBinaryLoad()
        {
            if (_pendingBinaryRead == null || !_pendingBinaryRead.IsCompleted)
                return false;

            int byteCount = 0;
            try
            {
                byteCount = _pendingBinaryRead.Result;
            }
            catch (AggregateException)
            {
                byteCount = 0;
            }
            finally
            {
                _pendingBinaryRead.Dispose();
                _pendingBinaryRead = null;
                if (_rawBufferLocked && _vault != null)
                    _vault.TryUnlockBuffer(BufferID.FloraGenomeRawBytes, SystemID.FloraGenomics);
                _rawBufferLocked = false;
            }

            DecodeLoadedBytes(byteCount);
            return true;
        }

        public bool TryApplyCsvOverrides(string csvPath, out int updatedCount)
        {
            updatedCount = 0;
            if (_vault == null)
                return false;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
            NativeArray<FloraGenomeDTO> genomes = _genomesHandle.Resolve(_vault);
            return FloraGenomeCsvHotloader.TryApplyOverrides(csvPath, scratch, genomes, ref _csvLastWriteTicks, out updatedCount);
        }

        public bool TrySchedulePlantGeneration(
            int genomeIndex,
            in FloraPlantSeedDTO seed,
            ref FloraGenomeChunkWorkspace workspace,
            uint frameIndex,
            int matrixOffset,
            int hazardOffset,
            JobHandle inputDeps,
            out FloraGenomeGenerationTicket ticket)
        {
            ticket = default;
            if (_vault == null || _generationInFlight || !workspace.IsCreated)
                return false;

            NativeArray<FloraGenomeDTO> genomes = _genomesHandle.Resolve(_vault);
            NativeArray<FloraPlantSeedDTO> seeds = _plantSeedsHandle.Resolve(_vault);
            NativeArray<FloraGenomeJobStats> statsVault = _statsHandle.Resolve(_vault);
            NativeArray<FloraGenomeBlackBoxEntry> blackBox = _blackBoxHandle.Resolve(_vault);
            NativeArray<int> blackBoxCursor = _blackBoxCursorHandle.Resolve(_vault);
            if (!genomes.IsCreated || !seeds.IsCreated || seeds.Length <= 0 || !statsVault.IsCreated || statsVault.Length <= 0)
                return false;
            if ((uint)genomeIndex >= (uint)genomes.Length)
                return false;

            FloraPlantSeedDTO resolvedSeed = seed;
            FloraGenomeDTO genome = genomes[genomeIndex];
            resolvedSeed.SpeciesHash = genome.SpeciesHash;
            if (resolvedSeed.PlantHash == 0u)
                resolvedSeed.PlantHash = FloraGenomeLSystemUtility.HashPlant(resolvedSeed.AupCell, genome.SpeciesHash, resolvedSeed.WorldSeed, resolvedSeed.ChunkSlot);
            resolvedSeed.HardwareTier = (byte)ResolveHardwareTier();
            resolvedSeed.RequestedIterations = genome.MaxIterations;
            seeds[0] = resolvedSeed;

            byte hardwareTier = resolvedSeed.HardwareTier;
            int safeMatrixOffset = math.max(0, matrixOffset);
            int safeHazardOffset = math.max(0, hazardOffset);
            JobHandle expandHandle = new IterativeLSystemExpanderJob
            {
                Genomes = genomes,
                GenomeIndex = genomeIndex,
                HardwareTier = hardwareTier,
                ExpandedSymbols = workspace.ExpandedSymbols,
                ScratchSymbols = workspace.ScratchSymbols,
                Stats = statsVault
            }.Schedule(inputDeps);

            JobHandle turtleHandle = new TurtleGraphicsJob
            {
                Genomes = genomes,
                PlantSeeds = seeds,
                Symbols = workspace.ExpandedSymbols,
                GenomeIndex = genomeIndex,
                PlantIndex = 0,
                FrameIndex = frameIndex,
                HardwareTier = hardwareTier,
                TurtleStack = workspace.TurtleStack,
                BranchMatrices = workspace.BranchMatrices,
                MatrixWriteOffset = safeMatrixOffset,
                MatrixWriteCapacity = ResolveWriteCapacity(workspace.BranchMatrices.Length, safeMatrixOffset),
                HazardZones = workspace.HazardZones,
                HazardWriteOffset = safeHazardOffset,
                HazardWriteCapacity = ResolveWriteCapacity(workspace.HazardZones.Length, safeHazardOffset),
                BlackBox = blackBox,
                BlackBoxCursor = blackBoxCursor,
                Stats = statsVault
            }.Schedule(expandHandle);

            ticket = new FloraGenomeGenerationTicket
            {
                Handle = turtleHandle,
                Seed = resolvedSeed,
                Genome = genome,
                MatrixOffset = safeMatrixOffset,
                FrameIndex = frameIndex,
                IsCreated = 1
            };
            _generationInFlight = true;
            return true;
        }

        public bool TryFinalizePlantGeneration(ref FloraGenomeGenerationTicket ticket, out FloraGenomeJobStats stats)
        {
            stats = default;
            if (_vault == null || ticket.IsCreated == 0 || !ticket.Handle.IsCompleted)
                return false;

            // Frame-boundary drain only: IsCompleted is true, so this does not stall gameplay ticks.
            ticket.Handle.Complete();

            NativeArray<FloraGenomeJobStats> statsVault = _statsHandle.Resolve(_vault);
            if (!statsVault.IsCreated || statsVault.Length <= FloraGenomeLSystemUtility.StatsDecoderIndex)
            {
                _generationInFlight = false;
                ticket = default;
                return false;
            }

            stats = statsVault[0];
            PublishBiomassSignal(in ticket.Seed, in ticket.Genome, in stats, ticket.MatrixOffset);
            PublishOverloadWarningIfNeeded(ticket.FrameIndex, in stats);
            DumpBlackBoxIfFatal(in stats);
            _generationInFlight = false;
            ticket = default;
            return true;
        }

        private void DecodeLoadedBytes(int byteCount)
        {
            if (_vault == null)
                return;

            NativeArray<byte> rawBytes = _rawBytesHandle.Resolve(_vault);
            NativeArray<FloraGenomeDTO> genomes = _genomesHandle.Resolve(_vault);
            NativeArray<FloraGenomeJobStats> stats = _statsHandle.Resolve(_vault);
            if (!genomes.IsCreated || !stats.IsCreated)
                return;

            new FloraGenomeDecoderJob
            {
                RawBytes = rawBytes,
                RawByteCount = byteCount,
                Genomes = genomes,
                Stats = stats
            }.Run();

            _genomeCount = stats[0].GenomeCount;
        }

        private static int ReadGenomeBinaryWorker(object state)
        {
            BinaryReadRequest request = state as BinaryReadRequest;
            if (request == null)
                return 0;

            return FloraGenomeBinaryArchaeology.TryLoadFirstGenomeBinary(request.ProjectRoot, request.RawBytes, out int byteCount)
                ? byteCount
                : 0;
        }

        private static int ResolveWriteCapacity(int bufferLength, int offset)
        {
            if (bufferLength <= 0 || offset < 0 || offset >= bufferLength)
                return 0;

            return bufferLength - offset;
        }

        private static FloraGenomeHardwareTier ResolveHardwareTier()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return FloraGenomeHardwareTier.Low;
                case HectonQualityTier.Mid:
                    return FloraGenomeHardwareTier.Middle;
                case HectonQualityTier.Ultra:
                    return FloraGenomeHardwareTier.Ultra;
                case HectonQualityTier.High:
                    return FloraGenomeHardwareTier.High;
                default:
                    return FloraGenomeHardwareTier.Low;
            }
        }

        private static void PublishBiomassSignal(
            in FloraPlantSeedDTO seed,
            in FloraGenomeDTO genome,
            in FloraGenomeJobStats stats,
            int matrixOffset)
        {
            FloraSpawnedSignal signal = new FloraSpawnedSignal
            {
                AupCell = seed.AupCell,
                SpeciesHash = genome.SpeciesHash,
                PlantHash = seed.PlantHash,
                Biomass = stats.Biomass,
                MatrixOffset = (uint)math.max(0, matrixOffset),
                MatrixCount = (uint)math.max(0, stats.MatrixCount),
                Reserved0 = 0u
            };
            SignalBus<FloraSpawnedSignal>.TryPush(in signal);
        }

        private static void PublishOverloadWarningIfNeeded(uint frameIndex, in FloraGenomeJobStats stats)
        {
            if ((stats.FaultFlags & (uint)FloraGenomeFaultFlags.GenerationOver2Ms) == 0u)
                return;

            FramePacingWarningSignal warning = new FramePacingWarningSignal
            {
                Frame = frameIndex,
                SourceHash = FloraGenomeLSystemConstants.OwnerHash,
                Flags = OverloadWarningHash,
                CurrentFrameMs = stats.EstimatedMicroseconds * 0.001f,
                TargetFrameMs = 2f,
                PreSimulationMs = stats.EstimatedMicroseconds * 0.001f,
                ActiveBucketLoadMs = stats.EstimatedMicroseconds * 0.001f,
                JitterVarianceMs = 0f,
                ExpectedMaxBucketLoadMs = 2f,
                ExpectedMeanBucketLoadMs = 0.1f,
                ActiveSlowBucket = 0,
                SlowBucketMask = 0,
                RebalanceSequence = 0u,
                Severity = 2
            };
            SignalBus<FramePacingWarningSignal>.TryPush(in warning);
        }

        private void DumpBlackBoxIfFatal(in FloraGenomeJobStats stats)
        {
            if ((stats.FaultFlags & (uint)FloraGenomeFaultFlags.NaNDetected) == 0u || _vault == null)
                return;

            NativeArray<FloraGenomeBlackBoxEntry> blackBox = _blackBoxHandle.Resolve(_vault);
            if (!blackBox.IsCreated || blackBox.Length <= 0)
                return;

            string dumpDirectory = Path.Combine("Docs", "AgentLogs");
            string dumpPath = Path.Combine(dumpDirectory, "Dump_SHINOBU_08.bin");
            string h8DumpPath = Path.Combine(dumpDirectory, "Dump_SHINOBU_08.h8dump");
            try
            {
                Directory.CreateDirectory(dumpDirectory);
                WriteBlackBoxDump(dumpPath, blackBox);
                WriteBlackBoxDump(h8DumpPath, blackBox);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteBlackBoxDump(string path, NativeArray<FloraGenomeBlackBoxEntry> blackBox)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
            int byteCount = blackBox.Length * UnsafeUtility.SizeOf<FloraGenomeBlackBoxEntry>();
            stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
        }
    }
}
