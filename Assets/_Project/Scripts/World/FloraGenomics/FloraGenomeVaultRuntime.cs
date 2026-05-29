using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Chunk-scoped buffer descriptor. Runtime instances point at Vault-owned memory; editor preview may own temp arrays.
    /// </summary>
    public ref struct FloraGenomeChunkWorkspace
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
    public sealed class FloraGenomeVaultRuntime
    {
        private static int s_x001FloraGenomeVaultRuntimeSignalPushDropCount;
        private const SystemID OwnerSystem = SystemID.FloraGenomics;
        public const int DefaultRawBytesCapacity = 5 * 1024 * 1024;
        public const int DefaultCsvScratchCapacity = 256 * 1024;
        public const uint OverloadWarningHash = 0x464F5632u; // FOV2
        private static readonly ulong RawBytesMutationGuardMask = FloraGenomeMutationGuardBit(BufferID.FloraGenomeRawBytes);
        private static readonly ulong DecodeMutationGuardMask =
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeRawBytes) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeDtos) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeStats);
        private static readonly ulong GenerationJobMutationGuardMask =
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeExpandedSymbols) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeScratchSymbols) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomePlantSeeds) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeBranchMatrices) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeHazardZones) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeTurtleStack) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeStats) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeBlackBox) |
            FloraGenomeMutationGuardBit(BufferID.FloraGenomeBlackBoxCursor);

        private IDataVault _vault;
        private IDataVault _rawBufferGuardVault;
        private IDataVault _generationJobGuardVault;
        private VaultGenerationHandle<byte> _rawBytesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<byte> _expandedSymbolsHandle;
        private VaultGenerationHandle<byte> _scratchSymbolsHandle;
        private VaultGenerationHandle<FloraGenomeDTO> _genomesHandle;
        private VaultGenerationHandle<FloraPlantSeedDTO> _plantSeedsHandle;
        private VaultGenerationHandle<BranchMatrixDTO> _branchMatricesHandle;
        private VaultGenerationHandle<HazardZoneDTO> _hazardsHandle;
        private VaultGenerationHandle<TurtleStackFrameDTO> _turtleStackHandle;
        private VaultGenerationHandle<FloraGenomeJobStats> _statsHandle;
        private VaultGenerationHandle<FloraGenomeBlackBoxEntry> _blackBoxHandle;
        private VaultGenerationHandle<int> _blackBoxCursorHandle;
        private bool _pendingBinaryReadActive;
        private bool _pendingBinaryReadCompleted;
        private int _pendingBinaryReadByteCount;
        private bool _rawBufferGuardHeld;
        private bool _generationJobGuardHeld;
        private bool _generationInFlight;
        private int _genomeCount;
        private int _genomeCapacity;
        private int _matrixCapacity;
        private int _hazardCapacity;
        private long _csvLastWriteTicks;

        public int GenomeCount => _genomeCount;

        public bool BindVault(
            IDataVault vault,
            int genomeCapacity = FloraGenomeLSystemConstants.DefaultGenomeCapacity,
            int matrixCapacity = FloraGenomeLSystemConstants.DefaultBranchMatrixCapacity,
            int hazardCapacity = FloraGenomeLSystemConstants.DefaultHazardCapacity)
        {
            if (vault == null)
            {
                ReleaseVault();
                return false;
            }

            if (_pendingBinaryReadActive || _generationInFlight)
                return ReferenceEquals(_vault, vault);

            if (_vault != null && !ReferenceEquals(_vault, vault))
                ReleaseVault();

            genomeCapacity = math.max(1, genomeCapacity);
            matrixCapacity = math.max(1, matrixCapacity);
            hazardCapacity = math.max(1, hazardCapacity);
            _vault = vault;
            if (!EnsureFloraGenomeVaultBuffer(vault, ref _rawBytesHandle, BufferID.FloraGenomeRawBytes, DefaultRawBytesCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _csvScratchHandle, BufferID.FloraGenomeCsvScratch, DefaultCsvScratchCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _expandedSymbolsHandle, BufferID.FloraGenomeExpandedSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _scratchSymbolsHandle, BufferID.FloraGenomeScratchSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _genomesHandle, BufferID.FloraGenomeDtos, genomeCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _plantSeedsHandle, BufferID.FloraGenomePlantSeeds, 1, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _branchMatricesHandle, BufferID.FloraGenomeBranchMatrices, matrixCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _hazardsHandle, BufferID.FloraGenomeHazardZones, hazardCapacity, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _turtleStackHandle, BufferID.FloraGenomeTurtleStack, FloraGenomeLSystemConstants.DefaultTurtleStackCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _statsHandle, BufferID.FloraGenomeStats, 1, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _blackBoxHandle, BufferID.FloraGenomeBlackBox, FloraGenomeLSystemConstants.BlackBoxFrameCount, NativeArrayOptions.ClearMemory, out _) ||
                !EnsureFloraGenomeVaultBuffer(vault, ref _blackBoxCursorHandle, BufferID.FloraGenomeBlackBoxCursor, 1, NativeArrayOptions.ClearMemory, out _))
            {
                ReleaseVault();
                return false;
            }

            _genomeCapacity = genomeCapacity;
            _matrixCapacity = matrixCapacity;
            _hazardCapacity = hazardCapacity;
            SignalBus<FloraSpawnedSignal>.Configure(256, 4096, 512, FloraGenomeLSystemConstants.OwnerHash);
            SignalBus<FloraSpawnedSignal>.EnsureInitialized();
            DecodeLoadedBytes(0);
            return true;
        }

        public bool ReleaseVault()
        {
            if (_pendingBinaryReadActive || _generationInFlight)
                return false;

            IDataVault vault = _vault;
            ReleaseRawBytesGuard();

            ReleaseFloraGenomeVaultHandle(vault, ref _rawBytesHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _csvScratchHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _expandedSymbolsHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _scratchSymbolsHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _genomesHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _plantSeedsHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _branchMatricesHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _hazardsHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _turtleStackHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _statsHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _blackBoxHandle);
            ReleaseFloraGenomeVaultHandle(vault, ref _blackBoxCursorHandle);

            _vault = null;
            _genomeCount = 0;
            _genomeCapacity = 0;
            _matrixCapacity = 0;
            _hazardCapacity = 0;
            _csvLastWriteTicks = 0L;
            return true;
        }

        public bool TryCreateChunkWorkspace(out FloraGenomeChunkWorkspace workspace)
        {
            workspace = default;
            if (_vault == null)
                return false;

            if (!TryResolveFloraGenomeVaultBuffer(_vault, ref _expandedSymbolsHandle, BufferID.FloraGenomeExpandedSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, out NativeArray<byte> expandedSymbols) ||
                !TryResolveFloraGenomeVaultBuffer(_vault, ref _scratchSymbolsHandle, BufferID.FloraGenomeScratchSymbols, FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity, out NativeArray<byte> scratchSymbols) ||
                !TryResolveFloraGenomeVaultBuffer(_vault, ref _branchMatricesHandle, BufferID.FloraGenomeBranchMatrices, math.max(1, _matrixCapacity), out NativeArray<BranchMatrixDTO> branchMatrices) ||
                !TryResolveFloraGenomeVaultBuffer(_vault, ref _hazardsHandle, BufferID.FloraGenomeHazardZones, math.max(1, _hazardCapacity), out NativeArray<HazardZoneDTO> hazardZones) ||
                !TryResolveFloraGenomeVaultBuffer(_vault, ref _turtleStackHandle, BufferID.FloraGenomeTurtleStack, FloraGenomeLSystemConstants.DefaultTurtleStackCapacity, out NativeArray<TurtleStackFrameDTO> turtleStack))
            {
                return false;
            }

            workspace = FloraGenomeChunkWorkspace.FromVault(expandedSymbols, scratchSymbols, branchMatrices, hazardZones, turtleStack);
            return workspace.IsCreated;
        }

        public bool BeginLoadGenomeBinaryAsync(string projectRoot)
        {
            if (_vault == null || _pendingBinaryReadActive)
                return false;

            if (!TryAcquireRawBytesGuard(out NativeArray<byte> rawBytes))
                return false;

            _pendingBinaryReadActive = true;
            _pendingBinaryReadCompleted = false;
            _pendingBinaryReadByteCount = 0;
            _ = RunGenomeBinaryLoadAsync(projectRoot, rawBytes);
            return true;
        }

        public bool TryCompletePendingBinaryLoad()
        {
            if (!_pendingBinaryReadActive || !_pendingBinaryReadCompleted)
                return false;

            int byteCount = _pendingBinaryReadByteCount;
            _pendingBinaryReadActive = false;
            _pendingBinaryReadCompleted = false;
            _pendingBinaryReadByteCount = 0;
            ReleaseRawBytesGuard();

            DecodeLoadedBytes(byteCount);
            return true;
        }

#if UNITY_EDITOR
        public bool TryApplyCsvOverrides(string csvPath, out int updatedCount)
        {
            updatedCount = 0;
            if (_vault == null)
                return false;

            if (!TryResolveFloraGenomeVaultBuffer(_vault, ref _csvScratchHandle, BufferID.FloraGenomeCsvScratch, DefaultCsvScratchCapacity, out NativeArray<byte> scratch) ||
                !TryResolveFloraGenomeVaultBuffer(_vault, ref _genomesHandle, BufferID.FloraGenomeDtos, math.max(1, _genomeCapacity), out NativeArray<FloraGenomeDTO> genomes))
            {
                return false;
            }

            return FloraGenomeCsvHotloader.TryApplyOverrides(csvPath, scratch, genomes, ref _csvLastWriteTicks, out updatedCount);
        }
#endif

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

            if (!TryAcquireGenerationJobGuard())
            {
                return false;
            }

            bool handoffGuard = false;
            try
            {
                if (!TryResolveFloraGenomeVaultBuffer(_vault, ref _genomesHandle, BufferID.FloraGenomeDtos, math.max(1, _genomeCapacity), out NativeArray<FloraGenomeDTO> genomes) ||
                    !TryResolveFloraGenomeVaultBuffer(_vault, ref _plantSeedsHandle, BufferID.FloraGenomePlantSeeds, 1, out NativeArray<FloraPlantSeedDTO> seeds) ||
                    !TryResolveFloraGenomeVaultBuffer(_vault, ref _statsHandle, BufferID.FloraGenomeStats, 1, out NativeArray<FloraGenomeJobStats> statsVault) ||
                    !TryResolveFloraGenomeVaultBuffer(_vault, ref _blackBoxHandle, BufferID.FloraGenomeBlackBox, FloraGenomeLSystemConstants.BlackBoxFrameCount, out NativeArray<FloraGenomeBlackBoxEntry> blackBox) ||
                    !TryResolveFloraGenomeVaultBuffer(_vault, ref _blackBoxCursorHandle, BufferID.FloraGenomeBlackBoxCursor, 1, out NativeArray<int> blackBoxCursor))
                {
                    return false;
                }

                if ((uint)genomeIndex >= (uint)genomes.Length)
                    return false;

                FloraPlantSeedDTO resolvedSeed = seed;
                FloraGenomeDTO genome = genomes[genomeIndex];
                resolvedSeed.SpeciesHash = genome.SpeciesHash;
                if (resolvedSeed.PlantHash == 0u)
                    resolvedSeed.PlantHash = FloraGenomeLSystemUtility.HashPlant(resolvedSeed.AupCell, genome.SpeciesHash, resolvedSeed.WorldSeed, resolvedSeed.ChunkSlot);
                float qualityWeight01 = ResolveGenomeQualityWeight01();
                resolvedSeed.QualityWeightQ8 = EncodeQualityWeightQ8(qualityWeight01);
                resolvedSeed.RequestedIterations = genome.MaxIterations;
                seeds[0] = resolvedSeed;

                int safeMatrixOffset = math.max(0, matrixOffset);
                int safeHazardOffset = math.max(0, hazardOffset);
                JobHandle expandHandle = new IterativeLSystemExpanderJob
                {
                    Genomes = genomes,
                    GenomeIndex = genomeIndex,
                    QualityWeight01 = qualityWeight01,
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
                    QualityWeight01 = qualityWeight01,
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
                handoffGuard = true;
                return true;
            }
            finally
            {
                if (!handoffGuard)
                    ReleaseGenerationJobGuard();
            }
        }

        public bool TryFinalizePlantGeneration(ref FloraGenomeGenerationTicket ticket, out FloraGenomeJobStats stats)
        {
            stats = default;
            if (_vault == null || ticket.IsCreated == 0 || !ticket.Handle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref ticket.Handle))
                return false;

            if (!TryResolveFloraGenomeVaultBuffer(_vault, ref _statsHandle, BufferID.FloraGenomeStats, 1, out NativeArray<FloraGenomeJobStats> statsVault) ||
                statsVault.Length <= FloraGenomeLSystemUtility.StatsDecoderIndex)
            {
                ReleaseGenerationJobGuard();
                _generationInFlight = false;
                ticket = default;
                return false;
            }

            try
            {
                stats = statsVault[0];
                PublishBiomassSignal(in ticket.Seed, in ticket.Genome, in stats, ticket.MatrixOffset);
                PublishOverloadWarningIfNeeded(ticket.FrameIndex, in stats);
                DumpBlackBoxIfFatal(in stats);
                return true;
            }
            finally
            {
                ReleaseGenerationJobGuard();
                _generationInFlight = false;
                ticket = default;
            }
        }

        private bool TryAcquireRawBytesGuard(out NativeArray<byte> rawBytes)
        {
            rawBytes = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _rawBufferGuardHeld ||
                !vault.TryAcquireMutationGuard(RawBytesMutationGuardMask))
            {
                return false;
            }

            bool keepGuard = false;
            try
            {
                keepGuard =
                    !vault.IsCompactionFenceActive &&
                    TryResolveFloraGenomeVaultBuffer(
                        vault,
                        ref _rawBytesHandle,
                        BufferID.FloraGenomeRawBytes,
                        DefaultRawBytesCapacity,
                        out rawBytes) &&
                    rawBytes.IsCreated;

                if (keepGuard)
                {
                    _rawBufferGuardVault = vault;
                    _rawBufferGuardHeld = true;
                }

                return keepGuard;
            }
            finally
            {
                if (!keepGuard)
                {
                    vault.ReleaseMutationGuard(RawBytesMutationGuardMask);
                    rawBytes = default;
                }
            }
        }

        private void ReleaseRawBytesGuard()
        {
            if (_rawBufferGuardHeld && _rawBufferGuardVault != null)
                _rawBufferGuardVault.ReleaseMutationGuard(RawBytesMutationGuardMask);

            _rawBufferGuardHeld = false;
            _rawBufferGuardVault = null;
        }

        private bool TryAcquireGenerationJobGuard()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _generationJobGuardHeld ||
                !vault.TryAcquireMutationGuard(GenerationJobMutationGuardMask))
            {
                return false;
            }

            _generationJobGuardVault = vault;
            _generationJobGuardHeld = true;
            return true;
        }

        private void ReleaseGenerationJobGuard()
        {
            if (_generationJobGuardHeld && _generationJobGuardVault != null)
                _generationJobGuardVault.ReleaseMutationGuard(GenerationJobMutationGuardMask);

            _generationJobGuardHeld = false;
            _generationJobGuardVault = null;
        }

        private void DecodeLoadedBytes(int byteCount)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DecodeMutationGuardMask))
            {
                return;
            }

            try
            {
                if (!TryResolveFloraGenomeVaultBuffer(vault, ref _rawBytesHandle, BufferID.FloraGenomeRawBytes, DefaultRawBytesCapacity, out NativeArray<byte> rawBytes) ||
                    !TryResolveFloraGenomeVaultBuffer(vault, ref _genomesHandle, BufferID.FloraGenomeDtos, math.max(1, _genomeCapacity), out NativeArray<FloraGenomeDTO> genomes) ||
                    !TryResolveFloraGenomeVaultBuffer(vault, ref _statsHandle, BufferID.FloraGenomeStats, 1, out NativeArray<FloraGenomeJobStats> stats))
                {
                    return;
                }

                FloraGenomeDecoderJob decoderJob = new FloraGenomeDecoderJob
                {
                    RawBytes = rawBytes,
                    RawByteCount = byteCount,
                    Genomes = genomes,
                    Stats = stats
                };
                decoderJob.Execute();

                _genomeCount = stats[0].GenomeCount;
            }
            finally
            {
                vault.ReleaseMutationGuard(DecodeMutationGuardMask);
            }
        }

        private async Awaitable RunGenomeBinaryLoadAsync(string projectRoot, NativeArray<byte> rawBytes)
        {
            int byteCount = 0;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                byteCount = FloraGenomeBinaryArchaeology.TryLoadFirstGenomeBinary(projectRoot, rawBytes, out int loadedBytes)
                    ? loadedBytes
                    : 0;
            }
            catch (IOException)
            {
                byteCount = 0;
            }
            catch (UnauthorizedAccessException)
            {
                byteCount = 0;
            }

            await Awaitable.MainThreadAsync();
            _pendingBinaryReadByteCount = byteCount;
            _pendingBinaryReadCompleted = true;
        }

        private static int ResolveWriteCapacity(int bufferLength, int offset)
        {
            if (bufferLength <= 0 || offset < 0 || offset >= bufferLength)
                return 0;

            return bufferLength - offset;
        }

        private static float ResolveGenomeQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1f;
        }

        private static byte EncodeQualityWeightQ8(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(ResolveFiniteQualityWeight01(qualityWeight01) * 255f), 0, 255);
        }

        private static float ResolveFiniteQualityWeight01(float qualityWeight01)
        {
            return math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
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
            SignalBus<FloraSpawnedSignal>.TryPushTracked(in signal, ref s_x001FloraGenomeVaultRuntimeSignalPushDropCount);
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
            SignalBus<FramePacingWarningSignal>.TryPushTracked(in warning, ref s_x001FloraGenomeVaultRuntimeSignalPushDropCount);
        }

        private void DumpBlackBoxIfFatal(in FloraGenomeJobStats stats)
        {
            if ((stats.FaultFlags & (uint)FloraGenomeFaultFlags.NaNDetected) == 0u || _vault == null)
                return;

            if (!TryResolveFloraGenomeVaultBuffer(_vault, ref _blackBoxHandle, BufferID.FloraGenomeBlackBox, FloraGenomeLSystemConstants.BlackBoxFrameCount, out NativeArray<FloraGenomeBlackBoxEntry> blackBox))
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

        private static unsafe void WriteBlackBoxDump(string path, NativeArray<FloraGenomeBlackBoxEntry> blackBox)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
            int byteCount = blackBox.Length * UnsafeUtility.SizeOf<FloraGenomeBlackBoxEntry>();
            stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
        }

        private static bool EnsureFloraGenomeVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveFloraGenomeVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return TryResolveFloraGenomeVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveFloraGenomeVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsFloraGenomeVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsFloraGenomeVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsFloraGenomeVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static ulong FloraGenomeMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static void ReleaseFloraGenomeVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null &&
                handle.SystemID == (uint)OwnerSystem &&
                handle.BufferID != 0u &&
                handle.Generation != 0u)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }
    }
}
