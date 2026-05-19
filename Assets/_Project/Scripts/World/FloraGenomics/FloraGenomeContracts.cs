using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Constants for SHINOBU flora genome decoding and iterative L-system generation.
    /// </summary>
    public static class FloraGenomeLSystemConstants
    {
        public const int FloraGenomeStrideBytes = 64;
        public const int BranchMatrixStrideBytes = 96;
        public const int HazardZoneStrideBytes = 32;
        public const int BlackBoxFrameCount = 300;
        public const int MaxMockGenomeCount = 3;
        public const int DefaultGenomeCapacity = 160;
        public const int DefaultExpandedSymbolCapacity = 8192;
        public const int DefaultBranchMatrixCapacity = 16384;
        public const int DefaultTurtleStackCapacity = 512;
        public const int DefaultHazardCapacity = 2048;
        public const int MaxRuntimeIterations = 4;
        public const int ToasterIterationCap = 3;
        public const uint GenomeBinaryMagic = 0x464C4738u; // FLG8
        public const uint OwnerHash = 0x53483038u; // SH08
        public const uint KelpSpeciesHash = 0x4B454C50u; // KELP
        public const uint CoralSpeciesHash = 0x434F524Cu; // CORL
        public const uint SpongeSpeciesHash = 0x53504E47u; // SPNG
    }

    /// <summary>
    /// Hardware tier used by generation jobs to clamp math cost.
    /// </summary>
    public enum FloraGenomeHardwareTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Genome trait flags copied from OSHINO binary payloads.
    /// </summary>
    [Flags]
    public enum FloraGenomeTraitFlags : uint
    {
        None = 0u,
        Bioluminescent = 1u << 0,
        Edible = 1u << 1,
        Caustic = 1u << 2,
        Thorny = 1u << 3,
        FoliageBlobEligible = 1u << 4
    }

    /// <summary>
    /// Runtime hazard flags emitted for kinematic and survival consumers.
    /// </summary>
    [Flags]
    public enum FloraHazardFlags : byte
    {
        None = 0,
        Caustic = 1 << 0,
        Thorny = 1 << 1
    }

    /// <summary>
    /// Matrix output LOD flags consumed by downstream render/export systems.
    /// </summary>
    [Flags]
    public enum FloraMatrixLodFlags : byte
    {
        None = 0,
        Segment = 1 << 0,
        LOD2Billboard = 1 << 1,
        TerrainConformed = 1 << 2,
        BiolumPayload = 1 << 3
    }

    /// <summary>
    /// OSHINO binary header. Size: 32 bytes. Runtime structs are explicitly aligned; no packed runtime layout attribute.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct FloraGenomeBinaryHeader
    {
        public uint Magic;
        public ushort Version;
        public ushort HeaderBytes;
        public int RecordCount;
        public int RecordStrideBytes;
        public uint Flags;
        public uint PayloadCrc;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Decoded OSHINO plant genome. Size: 64 bytes, all fields are raw public data for NativeArray mutation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = FloraGenomeLSystemConstants.FloraGenomeStrideBytes)]
    public struct FloraGenomeDTO
    {
        public uint SpeciesHash;
        public float BaseScale;
        public float BranchAngleRadians;
        public float SegmentLengthMeters;
        public FixedString32Bytes Axiom;
        public float BiolumThreshold;
        public uint PackedColorHDR;
        public uint TraitFlags;
        public byte MaxIterations;
        public byte RuleProfile;
        public byte HazardFlags;
        public byte _pad0;
    }

    /// <summary>
    /// Signed 64-bit AUP cell coordinate. Size: 24 bytes. Unity.Mathematics does not provide long3 in this project.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct FloraAupCell
    {
        public long X;
        public long Y;
        public long Z;
    }

    /// <summary>
    /// Per-plant deterministic seed used by turtle generation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct FloraPlantSeedDTO
    {
        public FloraAupCell AupCell;
        public float3 LocalPosition;
        public uint PlantHash;
        public uint SpeciesHash;
        public uint WorldSeed;
        public byte HardwareTier;
        public byte RequestedIterations;
        public ushort ChunkSlot;
        public uint Reserved0;
    }

    /// <summary>
    /// One generated branch or billboard matrix plus shader custom payload. Size: 96 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = FloraGenomeLSystemConstants.BranchMatrixStrideBytes)]
    public struct BranchMatrixDTO
    {
        public float4x4 Matrix;
        public float4 CustomData;
        public uint SpeciesHash;
        public uint PlantHash;
        public ushort SegmentIndex;
        public byte LodFlags;
        public byte HazardFlags;
        public uint Reserved0;
    }

    /// <summary>
    /// Hazard sphere generated from a toxic or thorny genome. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = FloraGenomeLSystemConstants.HazardZoneStrideBytes)]
    public struct HazardZoneDTO
    {
        public float3 Center;
        public float RadiusMeters;
        public uint SpeciesHash;
        public uint PlantHash;
        public ushort HazardFlags;
        public ushort Reserved0;
        public float Biomass;
    }

    /// <summary>
    /// NativeArray-backed turtle stack frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct TurtleStackFrameDTO
    {
        public float3 Position;
        public float Scale;
        public quaternion Rotation;
        public float3 BishopUp;
        public float Reserved1;
        public uint RngState;
        public ushort Depth;
        public ushort Reserved0;
    }

    /// <summary>
    /// Fixed 300-frame telemetry entry for SHINOBU flora generation black box. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct FloraGenomeBlackBoxEntry
    {
        public uint FrameIndex;
        public uint SpeciesHash;
        public uint PlantHash;
        public int ExpandedSymbolCount;
        public int MatrixCount;
        public int HazardCount;
        public int EstimatedMicroseconds;
        public float Biomass;
        public float3 RootPosition;
        public uint FaultFlags;
        public uint IterationCount;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Terrain seam mock used while the unified terrain sampler is unavailable in this batch.
    /// </summary>
    public partial struct MockTerrainHeight
    {
        /// <summary>
        /// Returns a deterministic flat seabed at Y=0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleHeight(float2 xz)
        {
            return 0f;
        }
    }

    /// <summary>
    /// Direct ref-access helpers for generated matrices. NativeArray indexer copies are not used.
    /// </summary>
    public static unsafe class FloraBranchMatrixAccess
    {
        private static BranchMatrixDTO s_InvalidMatrix;

        /// <summary>
        /// Returns a read-only reference to one generated matrix DTO.
        /// </summary>
        public static ref readonly BranchMatrixDTO GetMatrixAsReadOnlyRef(NativeArray<BranchMatrixDTO> matrices, int index)
        {
            if (!matrices.IsCreated || matrices.Length <= 0)
                return ref s_InvalidMatrix;

            int safeIndex = math.clamp(index, 0, matrices.Length - 1);
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(matrices);
            return ref UnsafeUtility.AsRef<BranchMatrixDTO>((byte*)basePtr + (safeIndex * UnsafeUtility.SizeOf<BranchMatrixDTO>()));
        }

        /// <summary>
        /// Returns a mutable reference to one generated matrix DTO.
        /// </summary>
        public static ref BranchMatrixDTO GetMatrixAsRef(NativeArray<BranchMatrixDTO> matrices, int index)
        {
            if (!matrices.IsCreated || matrices.Length <= 0)
                return ref s_InvalidMatrix;

            int safeIndex = math.clamp(index, 0, matrices.Length - 1);
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(matrices);
            return ref UnsafeUtility.AsRef<BranchMatrixDTO>((byte*)basePtr + (safeIndex * UnsafeUtility.SizeOf<BranchMatrixDTO>()));
        }
    }

    /// <summary>
    /// Mock genome injector used when OSHINO binaries are absent.
    /// </summary>
    public static class MockGenomeGenerator
    {
        /// <summary>
        /// Populates Kelp, Coral, and Sponge profiles through the same DTO path used by binary decode.
        /// </summary>
        public static int Populate(NativeArray<FloraGenomeDTO> genomes)
        {
            if (!genomes.IsCreated || genomes.Length < FloraGenomeLSystemConstants.MaxMockGenomeCount)
                return 0;

            genomes[0] = CreateGenome(
                FloraGenomeLSystemConstants.KelpSpeciesHash,
                1.35f,
                math.radians(18f),
                0.42f,
                (byte)'X',
                0.35f,
                0x5AFFC0FFu,
                FloraGenomeTraitFlags.Bioluminescent | FloraGenomeTraitFlags.Edible | FloraGenomeTraitFlags.FoliageBlobEligible,
                4,
                0,
                FloraHazardFlags.None);

            genomes[1] = CreateGenome(
                FloraGenomeLSystemConstants.CoralSpeciesHash,
                0.72f,
                math.radians(27f),
                0.22f,
                (byte)'X',
                0.72f,
                0xFF306DFFu,
                FloraGenomeTraitFlags.Bioluminescent | FloraGenomeTraitFlags.Thorny | FloraGenomeTraitFlags.FoliageBlobEligible,
                4,
                1,
                FloraHazardFlags.Thorny);

            genomes[2] = CreateGenome(
                FloraGenomeLSystemConstants.SpongeSpeciesHash,
                0.58f,
                math.radians(15f),
                0.18f,
                (byte)'F',
                0.18f,
                0xD7B85CFFu,
                FloraGenomeTraitFlags.Caustic | FloraGenomeTraitFlags.FoliageBlobEligible,
                3,
                2,
                FloraHazardFlags.Caustic);

            return FloraGenomeLSystemConstants.MaxMockGenomeCount;
        }

        private static FloraGenomeDTO CreateGenome(
            uint speciesHash,
            float baseScale,
            float branchAngleRadians,
            float segmentLengthMeters,
            byte axiom0,
            float biolumThreshold,
            uint packedColorHdr,
            FloraGenomeTraitFlags traitFlags,
            byte maxIterations,
            byte ruleProfile,
            FloraHazardFlags hazardFlags)
        {
            FixedString32Bytes axiom = default;
            axiom.Add(axiom0);
            return new FloraGenomeDTO
            {
                SpeciesHash = speciesHash,
                BaseScale = baseScale,
                BranchAngleRadians = branchAngleRadians,
                SegmentLengthMeters = segmentLengthMeters,
                Axiom = axiom,
                BiolumThreshold = biolumThreshold,
                PackedColorHDR = packedColorHdr,
                TraitFlags = (uint)traitFlags,
                MaxIterations = maxIterations,
                RuleProfile = ruleProfile,
                HazardFlags = (byte)hazardFlags,
                _pad0 = 0
            };
        }
    }

    /// <summary>
    /// Cold-path scanner for legacy OSHINO botanical binaries.
    /// </summary>
    public static unsafe class FloraGenomeBinaryArchaeology
    {
        private static readonly string[] CandidatePatterns =
        {
            "flora_genetics.h8bin",
            "l_system_axioms_006.h8bin",
            "botanical_traits.bin",
            "*flora_genetics*.h8bin",
            "*l_system*.h8bin",
            "*botanical*.bin"
        };

        /// <summary>
        /// Attempts to find and load a legacy binary into a caller-owned native byte buffer.
        /// </summary>
        public static bool TryLoadFirstGenomeBinary(string projectRoot, NativeArray<byte> destination, out int byteCount)
        {
            byteCount = 0;
            if (string.IsNullOrEmpty(projectRoot) || !destination.IsCreated || destination.Length <= 0)
                return false;

            string[] roots =
            {
                Path.Combine(projectRoot, "Docs", "Archive"),
                Path.Combine(projectRoot, "Docs", "_Archive"),
                Path.Combine(projectRoot, "Assets", "StreamingAssets"),
                Path.Combine(projectRoot, "StreamingAssets")
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                for (int patternIndex = 0; patternIndex < CandidatePatterns.Length; patternIndex++)
                {
                    if (TryReadFirstMatchingFile(root, CandidatePatterns[patternIndex], destination, out byteCount))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the first legacy botanical binary. Cold-path only; never called from frame jobs.
        /// </summary>
        public static bool TryFindFirstGenomeBinaryPath(string projectRoot, out string filePath)
        {
            filePath = string.Empty;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            string[] roots =
            {
                Path.Combine(projectRoot, "Docs", "Archive"),
                Path.Combine(projectRoot, "Docs", "_Archive"),
                Path.Combine(projectRoot, "Assets", "StreamingAssets"),
                Path.Combine(projectRoot, "StreamingAssets")
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                for (int patternIndex = 0; patternIndex < CandidatePatterns.Length; patternIndex++)
                {
                    if (TryFindFirstMatchingFile(root, CandidatePatterns[patternIndex], out filePath))
                        return true;
                }
            }

            return false;
        }

        private static bool TryReadFirstMatchingFile(
            string root,
            string pattern,
            NativeArray<byte> destination,
            out int byteCount)
        {
            byteCount = 0;
            try
            {
                string[] files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    if (TryReadFileIntoNative(files[i], destination, out byteCount))
                        return true;
                }
            }
            catch (IOException)
            {
                byteCount = 0;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                byteCount = 0;
                return false;
            }

            return false;
        }

        private static bool TryFindFirstMatchingFile(string root, string pattern, out string filePath)
        {
            filePath = string.Empty;
            try
            {
                string[] files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    filePath = files[0];
                    return true;
                }
            }
            catch (IOException)
            {
                filePath = string.Empty;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                filePath = string.Empty;
                return false;
            }

            return false;
        }

        private static bool TryReadFileIntoNative(string filePath, NativeArray<byte> destination, out int byteCount)
        {
            byteCount = 0;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                FileInfo info = new FileInfo(filePath);
                long length = info.Length;
                if (length <= 0L || length > destination.Length)
                    return false;

                int requestedBytes = (int)length;
                if (TryReadFileIntoNativeMmf(filePath, requestedBytes, destination))
                {
                    byteCount = requestedBytes;
                    return true;
                }

                if (TryReadFileIntoNativeStream(filePath, requestedBytes, destination))
                {
                    byteCount = requestedBytes;
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                byteCount = 0;
                return false;
            }
            catch (IOException)
            {
                byteCount = 0;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                byteCount = 0;
                return false;
            }

            return false;
        }

        private static bool TryReadFileIntoNativeMmf(string filePath, int byteCount, NativeArray<byte> destination)
        {
            MemoryMappedFile mappedFile = null;
            MemoryMappedViewAccessor view = null;
            byte* viewBase = null;

            try
            {
                mappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, byteCount, MemoryMappedFileAccess.Read);
                view = mappedFile.CreateViewAccessor(0L, byteCount, MemoryMappedFileAccess.Read);
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewBase);
                byte* source = viewBase + view.PointerOffset;
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(destination);
                UnsafeUtility.MemCpy(ptr, source, byteCount);
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            finally
            {
                if (viewBase != null && view != null)
                    view.SafeMemoryMappedViewHandle.ReleasePointer();

                if (view != null)
                    view.Dispose();
                if (mappedFile != null)
                    mappedFile.Dispose();
            }
        }

        private static bool TryReadFileIntoNativeStream(string filePath, int byteCount, NativeArray<byte> destination)
        {
            try
            {
                using FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 128 * 1024,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(destination);
                Span<byte> span = new Span<byte>(ptr, byteCount);
                int totalRead = 0;
                while (totalRead < span.Length)
                {
                    int read = stream.Read(span.Slice(totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                return totalRead == byteCount;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Biomass publication emitted after a flora plant is generated. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct FloraSpawnedSignal : ISignal
    {
        [FieldOffset(0)] public Hecton8.World.FloraAupCell AupCell;
        [FieldOffset(24)] public uint SpeciesHash;
        [FieldOffset(28)] public uint PlantHash;
        [FieldOffset(32)] public float Biomass;
        [FieldOffset(36)] public uint MatrixOffset;
        [FieldOffset(40)] public uint MatrixCount;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }
}
