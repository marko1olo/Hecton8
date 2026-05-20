using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Cartography
{
    public static class CartographyGridConstants
    {
        public const int AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
        public const int MacroCellSizeMeters = 50;
        public const int AxisBits = 7;
        public const int AxisLength = 1 << AxisBits;
        public const int OriginOffset = AxisLength >> 1;
        public const int BitCount = AxisLength * AxisLength * AxisLength;
        public const int WordCount = BitCount >> 6;
        public const int ByteCount = BitCount >> 3;
        public const int PackedUploadWordCount = (BitCount + 3) >> 2;
        public const int ResidentSectorSide = 3;
        public const int ResidentSectorCount = ResidentSectorSide * ResidentSectorSide;
        public const int TotalResidentWordCount = WordCount * ResidentSectorCount;
        public const int BlackBoxFrameCount = 300;
        public const int MaxRevealSignalsPerSlowTick = 16;
        public const int MaxPoiRevealPerSlowTick = 64;
        public const int MaxVisibleMapPoints = 32768;
        public const int ScannerProfileCapacity = 32;
        public const int CsvScratchBytes = 8192;
        public const int RleRunCapacity = 4096;
        public const int DebugVoxelCapacity = 512;
        public const float DefaultPlayerRevealRadiusMeters = MacroCellSizeMeters;
        public const float MaxRevealRadiusMeters = 250f;
        public const float DefaultSurfaceThicknessMeters = 2f;
        public const float DefaultVisualGlowIntensity = 1.25f;
        public const uint SectorDirtyFlag = 1u << 0;
        public const uint SectorResidentFlag = 1u << 1;
        public const uint SectorMockDataFlag = 1u << 2;
        public const uint SectorSdfMaskValidFlag = 1u << 3;
        public const uint TelemetryFlagOutOfBoundsAup = 1u << 0;
        public const uint TelemetryFlagMockData = 1u << 1;
        public const uint TelemetryFlagSdfMask = 1u << 2;
        public const uint TelemetryFlagUploadSkipped = 1u << 3;
        public const uint DefaultSectorHashSeed = 0xC47A133u;
    }

    /// <summary>
    /// Vault buffer IDs reserved by SHINOBU_133 without mutating the shared BufferID enum.
    /// </summary>
    public static class CartographyVaultBufferIds
    {
        public const BufferID DiscoveryWords = (BufferID)71420;
        public const BufferID SectorTable = (BufferID)71421;
        public const BufferID UploadPackedR8 = (BufferID)71422;
        public const BufferID TelemetryRing = (BufferID)71423;
        public const BufferID TelemetryCursor = (BufferID)71424;
        public const BufferID Tuning = (BufferID)71425;
        public const BufferID ScannerProfiles = (BufferID)71426;
        public const BufferID CsvScratch = (BufferID)71427;
        public const BufferID MockPings = (BufferID)71428;
        public const BufferID Counters = (BufferID)71429;
        public const BufferID ActiveSectorHashes = (BufferID)71430;
        public const BufferID DebugVoxels = (BufferID)71431;
        public const BufferID RleRuns = (BufferID)71432;
        public const BufferID SurfaceMaskWords = (BufferID)71433;
        public const BufferID RollbackSnapshotWords = (BufferID)71434;
        public const BufferID PendingPings = (BufferID)71435;
        public const BufferID PendingSignalCounts = (BufferID)71436;
    }

    public enum MapRevealSignalFlags : byte
    {
        None = 0,
        Player = 1 << 0,
        Acoustic = 1 << 1,
        Sonar = 1 << 2,
        Poi = 1 << 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct CartographyAup
    {
        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float LocalX;
        [FieldOffset(28)]
        public float LocalY;
        [FieldOffset(32)]
        public float LocalZ;
        [FieldOffset(36)]
        public float Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct MapRevealSignal
    {
        [FieldOffset(0)]
        public CartographyAup Center;
        [FieldOffset(40)]
        public float RadiusMeters;
        [FieldOffset(44)]
        public uint SourceId;
        [FieldOffset(48)]
        public MapRevealSignalFlags Flags;
        [FieldOffset(49)]
        public byte _pad0;
        [FieldOffset(50)]
        public ushort _pad1;
        [FieldOffset(52)]
        public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct CartographyPoiRecord
    {
        [FieldOffset(0)]
        public CartographyAup Position;
        [FieldOffset(40)]
        public uint Kind;
        [FieldOffset(44)]
        public uint Hash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CartographySectorDTO
    {
        [FieldOffset(0)]
        public ulong SectorHash;
        [FieldOffset(8)]
        public int BaseDataOffset;
        [FieldOffset(12)]
        public uint DiscoveredVoxelCount;
        [FieldOffset(16)]
        public uint Flags;
        [FieldOffset(20)]
        public uint _pad0;
        [FieldOffset(24)]
        public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CartographyCounterDTO
    {
        [FieldOffset(0)]
        public int Changed;
        [FieldOffset(4)]
        public int DiscoveredDelta;
        [FieldOffset(8)]
        public uint Revision;
        [FieldOffset(12)]
        public uint LastBitIndex;
        [FieldOffset(16)]
        public ulong LastSectorHash;
        [FieldOffset(24)]
        public int TotalDiscoveredVoxels;
        [FieldOffset(28)]
        public uint PendingSignalCount;
        [FieldOffset(32)]
        public ulong _pad0;
        [FieldOffset(40)]
        public ulong _pad1;
        [FieldOffset(48)]
        public ulong _pad2;
        [FieldOffset(56)]
        public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CartographyTelemetryEntry
    {
        [FieldOffset(0)]
        public long PlayerGridX;
        [FieldOffset(8)]
        public long PlayerGridY;
        [FieldOffset(16)]
        public long PlayerGridZ;
        [FieldOffset(24)]
        public float PlayerLocalX;
        [FieldOffset(28)]
        public float PlayerLocalY;
        [FieldOffset(32)]
        public float PlayerLocalZ;
        [FieldOffset(36)]
        public float GlobalQualityWeight;
        [FieldOffset(40)]
        public uint FrameIndex;
        [FieldOffset(44)]
        public uint Revision;
        [FieldOffset(48)]
        public int LastBitIndex;
        [FieldOffset(52)]
        public uint DiscoveredVoxelCount;
        [FieldOffset(56)]
        public ushort RevealedSignalCount;
        [FieldOffset(58)]
        public ushort RevealedPoiCount;
        [FieldOffset(60)]
        public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CartographyTuningDTO
    {
        [FieldOffset(0)]
        public float SonarPingRadiusMeters;
        [FieldOffset(4)]
        public float SurfaceThicknessMeters;
        [FieldOffset(8)]
        public float VisualGlowIntensity;
        [FieldOffset(12)]
        public float GlobalQualityWeight;
        [FieldOffset(16)]
        public float CellSizeMeters;
        [FieldOffset(20)]
        public float UploadCadenceFrames;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        public uint Revision;
        [FieldOffset(32)]
        public ulong _pad0;
        [FieldOffset(40)]
        public ulong _pad1;
        [FieldOffset(48)]
        public ulong _pad2;
        [FieldOffset(56)]
        public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CartographyScannerProfileDTO
    {
        [FieldOffset(0)]
        public uint UpgradeHash;
        [FieldOffset(4)]
        public float PingRadiusMeters;
        [FieldOffset(8)]
        public float DiscoveryResolutionMeters;
        [FieldOffset(12)]
        public float SurfaceThicknessMeters;
        [FieldOffset(16)]
        public float VisualGlowIntensity;
        [FieldOffset(20)]
        public uint Flags;
        [FieldOffset(24)]
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CartographyRleRunDTO
    {
        [FieldOffset(0)]
        public ulong WordValue;
        [FieldOffset(8)]
        public int StartWordIndex;
        [FieldOffset(12)]
        public ushort WordCount;
        [FieldOffset(14)]
        public ushort Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CartographyDebugVoxelDTO
    {
        [FieldOffset(0)]
        public int X;
        [FieldOffset(4)]
        public int Y;
        [FieldOffset(8)]
        public int Z;
        [FieldOffset(12)]
        public uint Flags;
    }

    public struct CartographyVaultHandles
    {
        public VaultBufferHandle<ulong> DiscoveryWords;
        public VaultBufferHandle<CartographySectorDTO> SectorTable;
        public VaultBufferHandle<uint> UploadPackedR8;
        public VaultBufferHandle<CartographyTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;
        public VaultBufferHandle<CartographyTuningDTO> Tuning;
        public VaultBufferHandle<CartographyScannerProfileDTO> ScannerProfiles;
        public VaultBufferHandle<byte> CsvScratch;
        public VaultBufferHandle<MapRevealSignal> MockPings;
        public VaultBufferHandle<MapRevealSignal> PendingPings;
        public VaultBufferHandle<int> PendingSignalCounts;
        public VaultBufferHandle<CartographyCounterDTO> Counters;
        public VaultBufferHandle<ulong> ActiveSectorHashes;
        public VaultBufferHandle<CartographyDebugVoxelDTO> DebugVoxels;
        public VaultBufferHandle<CartographyRleRunDTO> RleRuns;
        public VaultBufferHandle<ulong> SurfaceMaskWords;
        public VaultBufferHandle<ulong> RollbackSnapshotWords;

        public bool IsCreated()
        {
            return DiscoveryWords.IsCreated &&
                   SectorTable.IsCreated &&
                   UploadPackedR8.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   Tuning.IsCreated &&
                   ScannerProfiles.IsCreated &&
                   CsvScratch.IsCreated &&
                   MockPings.IsCreated &&
                   PendingPings.IsCreated &&
                   PendingSignalCounts.IsCreated &&
                   Counters.IsCreated &&
                   ActiveSectorHashes.IsCreated &&
                   DebugVoxels.IsCreated &&
                   RleRuns.IsCreated &&
                   SurfaceMaskWords.IsCreated &&
                   RollbackSnapshotWords.IsCreated;
        }
    }

    public struct CartographyVaultBuffers
    {
        public NativeArray<ulong> DiscoveryWords;
        public NativeArray<CartographySectorDTO> SectorTable;
        public NativeArray<uint> UploadPackedR8;
        public NativeArray<CartographyTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<CartographyTuningDTO> Tuning;
        public NativeArray<CartographyScannerProfileDTO> ScannerProfiles;
        public NativeArray<byte> CsvScratch;
        public NativeArray<MapRevealSignal> MockPings;
        public NativeArray<MapRevealSignal> PendingPings;
        public NativeArray<int> PendingSignalCounts;
        public NativeArray<CartographyCounterDTO> Counters;
        public NativeArray<ulong> ActiveSectorHashes;
        public NativeArray<CartographyDebugVoxelDTO> DebugVoxels;
        public NativeArray<CartographyRleRunDTO> RleRuns;
        public NativeArray<ulong> SurfaceMaskWords;
        public NativeArray<ulong> RollbackSnapshotWords;

        public bool IsCreated()
        {
            return DiscoveryWords.IsCreated &&
                   SectorTable.IsCreated &&
                   UploadPackedR8.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   Tuning.IsCreated &&
                   ScannerProfiles.IsCreated &&
                   CsvScratch.IsCreated &&
                   MockPings.IsCreated &&
                   PendingPings.IsCreated &&
                   PendingSignalCounts.IsCreated &&
                   Counters.IsCreated &&
                   ActiveSectorHashes.IsCreated &&
                   DebugVoxels.IsCreated &&
                   RleRuns.IsCreated &&
                   SurfaceMaskWords.IsCreated &&
                   RollbackSnapshotWords.IsCreated;
        }
    }

    public static class CartographyVault
    {
        private const uint FnvOffset32 = 2166136261u;
        private const uint FnvPrime32 = 16777619u;
        private const uint DumpMagic = 0x534F4E52u;
        private const uint DumpVersion = 1u;
        private const string ScannerProfilesFileName = "scanner_hardware_profiles.csv";
        private const string DumpFileName = "Dump_SONAR_MAPPER.bin";

        public static bool TryResolve(IDataVault vault, out CartographyVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
                return TryResolveExisting(vault, out handles);

            handles.DiscoveryWords = vault.GetBufferHandle<ulong>(
                CartographyVaultBufferIds.DiscoveryWords,
                CartographyGridConstants.TotalResidentWordCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.SectorTable = vault.GetBufferHandle<CartographySectorDTO>(
                CartographyVaultBufferIds.SectorTable,
                CartographyGridConstants.ResidentSectorCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.UploadPackedR8 = vault.GetBufferHandle<uint>(
                CartographyVaultBufferIds.UploadPackedR8,
                CartographyGridConstants.PackedUploadWordCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.GetBufferHandle<CartographyTelemetryEntry>(
                CartographyVaultBufferIds.TelemetryRing,
                CartographyGridConstants.BlackBoxFrameCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryCursor = vault.GetBufferHandle<int>(
                CartographyVaultBufferIds.TelemetryCursor,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.Tuning = vault.GetBufferHandle<CartographyTuningDTO>(
                CartographyVaultBufferIds.Tuning,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.ScannerProfiles = vault.GetBufferHandle<CartographyScannerProfileDTO>(
                CartographyVaultBufferIds.ScannerProfiles,
                CartographyGridConstants.ScannerProfileCapacity,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.CsvScratch = vault.GetBufferHandle<byte>(
                CartographyVaultBufferIds.CsvScratch,
                CartographyGridConstants.CsvScratchBytes,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.MockPings = vault.GetBufferHandle<MapRevealSignal>(
                CartographyVaultBufferIds.MockPings,
                CartographyGridConstants.MaxRevealSignalsPerSlowTick,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.PendingPings = vault.GetBufferHandle<MapRevealSignal>(
                CartographyVaultBufferIds.PendingPings,
                CartographyGridConstants.MaxRevealSignalsPerSlowTick,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.PendingSignalCounts = vault.GetBufferHandle<int>(
                CartographyVaultBufferIds.PendingSignalCounts,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.Counters = vault.GetBufferHandle<CartographyCounterDTO>(
                CartographyVaultBufferIds.Counters,
                CartographyGridConstants.ResidentSectorCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.ActiveSectorHashes = vault.GetBufferHandle<ulong>(
                CartographyVaultBufferIds.ActiveSectorHashes,
                CartographyGridConstants.ResidentSectorCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.DebugVoxels = vault.GetBufferHandle<CartographyDebugVoxelDTO>(
                CartographyVaultBufferIds.DebugVoxels,
                CartographyGridConstants.DebugVoxelCapacity,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.RleRuns = vault.GetBufferHandle<CartographyRleRunDTO>(
                CartographyVaultBufferIds.RleRuns,
                CartographyGridConstants.RleRunCapacity,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.SurfaceMaskWords = vault.GetBufferHandle<ulong>(
                CartographyVaultBufferIds.SurfaceMaskWords,
                CartographyGridConstants.WordCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            handles.RollbackSnapshotWords = vault.GetBufferHandle<ulong>(
                CartographyVaultBufferIds.RollbackSnapshotWords,
                CartographyGridConstants.WordCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);

            return handles.IsCreated();
        }

        public static bool TryResolveViews(
            IDataVault vault,
            ref CartographyVaultHandles handles,
            out CartographyVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            buffers.DiscoveryWords = handles.DiscoveryWords.Resolve(vault);
            buffers.SectorTable = handles.SectorTable.Resolve(vault);
            buffers.UploadPackedR8 = handles.UploadPackedR8.Resolve(vault);
            buffers.TelemetryRing = handles.TelemetryRing.Resolve(vault);
            buffers.TelemetryCursor = handles.TelemetryCursor.Resolve(vault);
            buffers.Tuning = handles.Tuning.Resolve(vault);
            buffers.ScannerProfiles = handles.ScannerProfiles.Resolve(vault);
            buffers.CsvScratch = handles.CsvScratch.Resolve(vault);
            buffers.MockPings = handles.MockPings.Resolve(vault);
            buffers.PendingPings = handles.PendingPings.Resolve(vault);
            buffers.PendingSignalCounts = handles.PendingSignalCounts.Resolve(vault);
            buffers.Counters = handles.Counters.Resolve(vault);
            buffers.ActiveSectorHashes = handles.ActiveSectorHashes.Resolve(vault);
            buffers.DebugVoxels = handles.DebugVoxels.Resolve(vault);
            buffers.RleRuns = handles.RleRuns.Resolve(vault);
            buffers.SurfaceMaskWords = handles.SurfaceMaskWords.Resolve(vault);
            buffers.RollbackSnapshotWords = handles.RollbackSnapshotWords.Resolve(vault);
            return buffers.IsCreated();
        }

        public static CartographyTuningDTO BuildDefaultTuning(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return new CartographyTuningDTO
            {
                SonarPingRadiusMeters = CartographyGridConstants.MaxRevealRadiusMeters,
                SurfaceThicknessMeters = CartographyGridConstants.DefaultSurfaceThicknessMeters,
                VisualGlowIntensity = CartographyGridConstants.DefaultVisualGlowIntensity,
                GlobalQualityWeight = quality,
                CellSizeMeters = CartographyGridConstants.MacroCellSizeMeters,
                UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(quality),
                Flags = 0u,
                Revision = 1u
            };
        }

        public static bool TryGetTuning(IDataVault vault, ref CartographyVaultHandles handles, out CartographyTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveViews(vault, ref handles, out CartographyVaultBuffers buffers) || !buffers.Tuning.IsCreated)
                return false;

            tuning = buffers.Tuning[0];
            return true;
        }

        public static bool TrySetTuning(IDataVault vault, ref CartographyVaultHandles handles, in CartographyTuningDTO tuning)
        {
            if (!TryResolveViews(vault, ref handles, out CartographyVaultBuffers buffers) || !buffers.Tuning.IsCreated)
                return false;

            CartographyTuningDTO sanitized = tuning;
            sanitized.SonarPingRadiusMeters = math.clamp(
                math.isfinite(sanitized.SonarPingRadiusMeters) ? sanitized.SonarPingRadiusMeters : CartographyGridConstants.DefaultPlayerRevealRadiusMeters,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxRevealRadiusMeters);
            sanitized.SurfaceThicknessMeters = math.clamp(
                math.isfinite(sanitized.SurfaceThicknessMeters) ? sanitized.SurfaceThicknessMeters : CartographyGridConstants.DefaultSurfaceThicknessMeters,
                0.25f,
                8f);
            sanitized.VisualGlowIntensity = math.clamp(
                math.isfinite(sanitized.VisualGlowIntensity) ? sanitized.VisualGlowIntensity : CartographyGridConstants.DefaultVisualGlowIntensity,
                0f,
                8f);
            sanitized.GlobalQualityWeight = math.saturate(math.isfinite(sanitized.GlobalQualityWeight) ? sanitized.GlobalQualityWeight : 1f);
            sanitized.CellSizeMeters = CartographyGridConstants.MacroCellSizeMeters;
            sanitized.UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(sanitized.GlobalQualityWeight);
            sanitized.Revision++;
            buffers.Tuning[0] = sanitized;
            return true;
        }

        public static bool TryLoadScannerProfilesCsvForEditor(
            IDataVault vault,
            ref CartographyVaultHandles handles,
            string projectRoot,
            out int appliedRows)
        {
            appliedRows = 0;
            if (!TryResolveViews(vault, ref handles, out CartographyVaultBuffers buffers))
                return false;

            string path = Path.Combine(projectRoot, "Assets", "_Project", "Data", ScannerProfilesFileName);
            if (!File.Exists(path))
                path = Path.Combine(projectRoot, ScannerProfilesFileName);
            if (!File.Exists(path))
                return false;

            byte[] bytes = File.ReadAllBytes(path); // EDITOR/COLD ALLOC: CSV ingest source bytes - owner: Sonar Map Tuner.
            int byteCount = math.min(bytes.Length, buffers.CsvScratch.Length);
            for (int i = 0; i < byteCount; i++)
                buffers.CsvScratch[i] = bytes[i];

            appliedRows = ParseScannerProfilesCsv(buffers.CsvScratch, byteCount, buffers.ScannerProfiles);
            return appliedRows > 0;
        }

        public static int ParseScannerProfilesCsv(
            NativeArray<byte> csvBytes,
            int byteCount,
            NativeArray<CartographyScannerProfileDTO> profiles)
        {
            if (!csvBytes.IsCreated || !profiles.IsCreated)
                return 0;

            int length = math.clamp(byteCount, 0, csvBytes.Length);
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int applied = 0;
            int index = 0;
            while (index < length)
            {
                SkipSeparators(csvBytes, length, ref index);
                if (index >= length)
                    break;

                if (csvBytes[index] == (byte)'#')
                {
                    SkipLine(csvBytes, length, ref index);
                    continue;
                }

                if (!TryReadTokenHash(csvBytes, length, ref index, out uint upgradeHash) ||
                    !TryReadFloat(csvBytes, length, ref index, out float radius) ||
                    !TryReadFloat(csvBytes, length, ref index, out float resolution) ||
                    !TryReadFloat(csvBytes, length, ref index, out float thickness))
                {
                    SkipLine(csvBytes, length, ref index);
                    continue;
                }

                float glow = 1f;
                TryReadFloat(csvBytes, length, ref index, out glow);
                if (TryWriteScannerProfile(profiles, upgradeHash, radius, resolution, thickness, glow))
                    applied++;

                SkipLine(csvBytes, length, ref index);
            }

            return applied;
        }

        public static bool TryDumpBlackBox(
            in CartographyVaultBuffers buffers,
            string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated)
                return false;

            try
            {
                string dir = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, DumpFileName);
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(buffers.TelemetryCursor.IsCreated ? buffers.TelemetryCursor[0] : 0);
                writer.Write(buffers.TelemetryRing.Length);
                for (int i = 0; i < buffers.TelemetryRing.Length; i++)
                {
                    CartographyTelemetryEntry entry = buffers.TelemetryRing[i];
                    writer.Write(entry.PlayerGridX);
                    writer.Write(entry.PlayerGridY);
                    writer.Write(entry.PlayerGridZ);
                    writer.Write(entry.PlayerLocalX);
                    writer.Write(entry.PlayerLocalY);
                    writer.Write(entry.PlayerLocalZ);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.Revision);
                    writer.Write(entry.LastBitIndex);
                    writer.Write(entry.DiscoveredVoxelCount);
                    writer.Write(entry.RevealedSignalCount);
                    writer.Write(entry.RevealedPoiCount);
                    writer.Write(entry.StateHash);
                }

                return true;
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

        private static bool TryResolveExisting(IDataVault vault, out CartographyVaultHandles handles)
        {
            handles = default;
            return vault.TryGetBufferHandle(CartographyVaultBufferIds.DiscoveryWords, out handles.DiscoveryWords) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.SectorTable, out handles.SectorTable) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.UploadPackedR8, out handles.UploadPackedR8) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.TelemetryRing, out handles.TelemetryRing) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.Tuning, out handles.Tuning) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.ScannerProfiles, out handles.ScannerProfiles) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.CsvScratch, out handles.CsvScratch) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.MockPings, out handles.MockPings) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.PendingPings, out handles.PendingPings) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.PendingSignalCounts, out handles.PendingSignalCounts) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.Counters, out handles.Counters) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.ActiveSectorHashes, out handles.ActiveSectorHashes) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.DebugVoxels, out handles.DebugVoxels) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.RleRuns, out handles.RleRuns) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.SurfaceMaskWords, out handles.SurfaceMaskWords) &&
                   vault.TryGetBufferHandle(CartographyVaultBufferIds.RollbackSnapshotWords, out handles.RollbackSnapshotWords);
        }

        private static bool TryWriteScannerProfile(
            NativeArray<CartographyScannerProfileDTO> profiles,
            uint upgradeHash,
            float radius,
            float resolution,
            float thickness,
            float glow)
        {
            if (upgradeHash == 0u || !profiles.IsCreated || profiles.Length == 0)
                return false;

            uint mask = (uint)profiles.Length - 1u;
            int start = (int)(upgradeHash & mask);
            for (int probe = 0; probe < profiles.Length; probe++)
            {
                int slot = (start + probe) & (profiles.Length - 1);
                CartographyScannerProfileDTO profile = profiles[slot];
                if (profile.UpgradeHash != 0u && profile.UpgradeHash != upgradeHash)
                    continue;

                profile.UpgradeHash = upgradeHash;
                profile.PingRadiusMeters = math.clamp(
                    math.isfinite(radius) ? radius : CartographyGridConstants.DefaultPlayerRevealRadiusMeters,
                    CartographyGridConstants.MacroCellSizeMeters,
                    CartographyGridConstants.MaxRevealRadiusMeters);
                profile.DiscoveryResolutionMeters = math.clamp(
                    math.isfinite(resolution) ? resolution : CartographyGridConstants.MacroCellSizeMeters,
                    4f,
                    CartographyGridConstants.MacroCellSizeMeters);
                profile.SurfaceThicknessMeters = math.clamp(
                    math.isfinite(thickness) ? thickness : CartographyGridConstants.DefaultSurfaceThicknessMeters,
                    0.25f,
                    8f);
                profile.VisualGlowIntensity = math.clamp(math.isfinite(glow) ? glow : 1f, 0f, 8f);
                profile.Flags = 1u;
                profiles[slot] = profile;
                return true;
            }

            return false;
        }

        private static void SkipSeparators(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length)
            {
                byte b = bytes[index];
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)',' || b == (byte)';' || b == (byte)'\r' || b == (byte)'\n')
                {
                    index++;
                    continue;
                }

                break;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n')
                index++;
            if (index < length)
                index++;
        }

        private static bool TryReadTokenHash(NativeArray<byte> bytes, int length, ref int index, out uint hash)
        {
            SkipSeparators(bytes, length, ref index);
            hash = FnvOffset32;
            int start = index;
            while (index < length)
            {
                byte b = bytes[index];
                if (b == (byte)',' || b == (byte)';' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
                    break;

                byte lowered = b >= (byte)'A' && b <= (byte)'Z' ? (byte)(b + 32) : b;
                hash ^= lowered;
                hash *= FnvPrime32;
                index++;
            }

            return index > start;
        }

        private static bool TryReadFloat(NativeArray<byte> bytes, int length, ref int index, out float value)
        {
            SkipSeparators(bytes, length, ref index);
            value = 0f;
            if (index >= length)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            bool any = false;
            double integer = 0d;
            while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = (integer * 10d) + (bytes[index] - (byte)'0');
                index++;
                any = true;
            }

            double fraction = 0d;
            double scale = 1d;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = (fraction * 10d) + (bytes[index] - (byte)'0');
                    scale *= 10d;
                    index++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = (float)(sign * (integer + (fraction / math.max(scale, 1d))));
            return math.isfinite(value);
        }
    }

    public static class CartographyGridMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEncode(
            in CartographyAup aup,
            out int bitIndex,
            out int wordIndex,
            out int bitOffset)
        {
            if (!TryResolveMacroCell(in aup, out int3 macroCell))
            {
                bitIndex = -1;
                wordIndex = -1;
                bitOffset = -1;
                return false;
            }

            return TryEncodeMacroCell(macroCell, out bitIndex, out wordIndex, out bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEncodeDoubleAup(
            double3 absoluteAup,
            out int bitIndex,
            out int wordIndex,
            out int bitOffset)
        {
            if (!TryResolveMacroCell(absoluteAup, out int3 macroCell))
            {
                bitIndex = -1;
                wordIndex = -1;
                bitOffset = -1;
                return false;
            }

            return TryEncodeMacroCell(macroCell, out bitIndex, out wordIndex, out bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveMacroCell(in CartographyAup aup, out int3 macroCell)
        {
            macroCell = default;
            if (!IsFinite(in aup))
                return false;

            double3 absolute = ToAbsoluteDouble3(in aup);
            return TryResolveMacroCell(absolute, out macroCell);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveMacroCell(double3 absoluteAup, out int3 macroCell)
        {
            macroCell = default;
            if (!math.all(math.isfinite(absoluteAup)))
                return false;

            double invCell = 1.0 / CartographyGridConstants.MacroCellSizeMeters;
            double3 macro = math.floor(absoluteAup * invCell);
            if (!math.all(math.isfinite(macro)) ||
                macro.x < int.MinValue ||
                macro.y < int.MinValue ||
                macro.z < int.MinValue ||
                macro.x > int.MaxValue ||
                macro.y > int.MaxValue ||
                macro.z > int.MaxValue)
            {
                return false;
            }

            macroCell = new int3((int)macro.x, (int)macro.y, (int)macro.z);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 ToGridIndex(double3 absoluteAup)
        {
            double invCell = 1.0 / CartographyGridConstants.MacroCellSizeMeters;
            double3 grid = math.floor(absoluteAup * invCell);
            return new int3((int)grid.x, (int)grid.y, (int)grid.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEncodeMacroCell(
            int3 macroCell,
            out int bitIndex,
            out int wordIndex,
            out int bitOffset)
        {
            int localX = WrapMacroAxisToLocal(macroCell.x);
            int localY = WrapMacroAxisToLocal(macroCell.y);
            int localZ = WrapMacroAxisToLocal(macroCell.z);

            bitIndex = ToFlatIndex(new int3(localX, localY, localZ));
            wordIndex = bitIndex >> 6;
            bitOffset = bitIndex & 63;
            return (uint)wordIndex < CartographyGridConstants.WordCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToFlatIndex(int3 localGridPos)
        {
            return localGridPos.x +
                   (localGridPos.y * CartographyGridConstants.AxisLength) +
                   (localGridPos.z * CartographyGridConstants.AxisLength * CartographyGridConstants.AxisLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WrapMacroAxisToLocal(int macroAxis)
        {
            long shifted = (long)macroAxis + CartographyGridConstants.OriginOffset;
            long wrapped = shifted % CartographyGridConstants.AxisLength;
            if (wrapped < 0)
                wrapped += CartographyGridConstants.AxisLength;

            return (int)wrapped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 DecodeBitIndex(int bitIndex)
        {
            int localX = bitIndex % CartographyGridConstants.AxisLength;
            int yz = bitIndex / CartographyGridConstants.AxisLength;
            int localY = yz % CartographyGridConstants.AxisLength;
            int localZ = yz / CartographyGridConstants.AxisLength;
            return new int3(
                localX - CartographyGridConstants.OriginOffset,
                localY - CartographyGridConstants.OriginOffset,
                localZ - CartographyGridConstants.OriginOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ToAbsoluteDouble3(in CartographyAup aup)
        {
            return new double3(
                ((double)aup.GridX * CartographyGridConstants.AupCellSizeMeters) + aup.LocalX,
                ((double)aup.GridY * CartographyGridConstants.AupCellSizeMeters) + aup.LocalY,
                ((double)aup.GridZ * CartographyGridConstants.AupCellSizeMeters) + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(in CartographyAup aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveUploadIntervalFrames(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.max(1, (int)math.round(math.lerp(1f, 60f, 1f - quality)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ResolveSectorHash(int3 sector)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = (hash ^ (uint)sector.x) * 1099511628211UL;
                hash = (hash ^ (uint)sector.y) * 1099511628211UL;
                hash = (hash ^ (uint)sector.z) * 1099511628211UL;
                return hash == 0UL ? CartographyGridConstants.DefaultSectorHashSeed : hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveResidentSectorSlot(int2 sectorDelta)
        {
            int x = math.clamp(sectorDelta.x + 1, 0, CartographyGridConstants.ResidentSectorSide - 1);
            int z = math.clamp(sectorDelta.y + 1, 0, CartographyGridConstants.ResidentSectorSide - 1);
            return x + (z * CartographyGridConstants.ResidentSectorSide);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveWordOffsetForSlot(int residentSlot)
        {
            return math.clamp(residentSlot, 0, CartographyGridConstants.ResidentSectorCount - 1) *
                   CartographyGridConstants.WordCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BuildStateHash(in CartographyAup aup, uint revision, int lastBitIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)aup.GridX) * 16777619u;
                hash = (hash ^ (uint)aup.GridY) * 16777619u;
                hash = (hash ^ (uint)aup.GridZ) * 16777619u;
                hash = (hash ^ math.asuint(aup.LocalX)) * 16777619u;
                hash = (hash ^ math.asuint(aup.LocalY)) * 16777619u;
                hash = (hash ^ math.asuint(aup.LocalZ)) * 16777619u;
                hash = (hash ^ revision) * 16777619u;
                hash = (hash ^ (uint)lastBitIndex) * 16777619u;
                return hash;
            }
        }
    }

    public static class CartographyLayoutVerifier
    {
        public static bool ValidateRuntimeLayouts()
        {
            bool sizeOk = UnsafeUtility.SizeOf<CartographyAup>() == 40 &&
                          UnsafeUtility.SizeOf<MapRevealSignal>() == 56 &&
                          UnsafeUtility.SizeOf<CartographyPoiRecord>() == 48 &&
                          UnsafeUtility.SizeOf<CartographySectorDTO>() == 32 &&
                          UnsafeUtility.SizeOf<CartographyCounterDTO>() == 64 &&
                          UnsafeUtility.SizeOf<CartographyTelemetryEntry>() == 64 &&
                          UnsafeUtility.SizeOf<CartographyTuningDTO>() == 64 &&
                          UnsafeUtility.SizeOf<CartographyScannerProfileDTO>() == 32 &&
                          UnsafeUtility.SizeOf<CartographyRleRunDTO>() == 16 &&
                          UnsafeUtility.SizeOf<CartographyDebugVoxelDTO>() == 16;
#if UNITY_EDITOR
            return sizeOk &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO.SectorHash)) == 0 &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO.BaseDataOffset)) == 8 &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO.DiscoveredVoxelCount)) == 12 &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO.Flags)) == 16 &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO._pad0)) == 20 &&
                   GetOffset<CartographySectorDTO>(nameof(CartographySectorDTO._pad1)) == 24 &&
                   GetOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.Changed)) == 0 &&
                   GetOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.LastSectorHash)) == 16 &&
                   GetOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.TotalDiscoveredVoxels)) == 24 &&
                   GetOffset<CartographyCounterDTO>(nameof(CartographyCounterDTO.PendingSignalCount)) == 28 &&
                   GetOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.PlayerGridX)) == 0 &&
                   GetOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.DiscoveredVoxelCount)) == 52 &&
                   GetOffset<CartographyTelemetryEntry>(nameof(CartographyTelemetryEntry.StateHash)) == 60;
#else
            return sizeOk;
#endif
        }

#if UNITY_EDITOR
        private static int GetOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearCartographyUlongBufferJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<ulong> Buffer;

        public void Execute(int index)
        {
            Buffer[index] = 0UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearCartographyUintBufferJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<uint> Buffer;

        public void Execute(int index)
        {
            Buffer[index] = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearCartographyRevealSignalBufferJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<MapRevealSignal> Buffer;

        public void Execute(int index)
        {
            Buffer[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InitializeCartographyVaultJob : IJob
    {
        [NoAlias]
        public NativeArray<CartographySectorDTO> Sectors;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        [NoAlias]
        public NativeArray<CartographyTelemetryEntry> TelemetryRing;
        [NoAlias]
        public NativeArray<int> TelemetryCursor;
        [NoAlias]
        public NativeArray<CartographyTuningDTO> Tuning;
        [NoAlias]
        public NativeArray<CartographyScannerProfileDTO> ScannerProfiles;
        [NoAlias]
        public NativeArray<ulong> ActiveSectorHashes;
        public float GlobalQualityWeight;

        public void Execute()
        {
            for (int i = 0; i < Sectors.Length; i++)
            {
                int x = (i % CartographyGridConstants.ResidentSectorSide) - 1;
                int z = (i / CartographyGridConstants.ResidentSectorSide) - 1;
                ulong hash = CartographyGridMath.ResolveSectorHash(new int3(x, 0, z));
                Sectors[i] = new CartographySectorDTO
                {
                    SectorHash = hash,
                    BaseDataOffset = i * CartographyGridConstants.WordCount,
                    DiscoveredVoxelCount = 0u,
                    Flags = CartographyGridConstants.SectorResidentFlag
                };

                if (i < Counters.Length)
                {
                    Counters[i] = new CartographyCounterDTO
                    {
                        LastSectorHash = hash
                    };
                }

                if (i < ActiveSectorHashes.Length)
                    ActiveSectorHashes[i] = hash;
            }

            for (int i = 0; i < TelemetryRing.Length; i++)
                TelemetryRing[i] = default;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = 0;

            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
                Tuning[0] = new CartographyTuningDTO
                {
                    SonarPingRadiusMeters = CartographyGridConstants.MaxRevealRadiusMeters,
                    SurfaceThicknessMeters = CartographyGridConstants.DefaultSurfaceThicknessMeters,
                    VisualGlowIntensity = CartographyGridConstants.DefaultVisualGlowIntensity,
                    GlobalQualityWeight = quality,
                    CellSizeMeters = CartographyGridConstants.MacroCellSizeMeters,
                    UploadCadenceFrames = CartographyGridMath.ResolveUploadIntervalFrames(quality),
                    Flags = 0u,
                    Revision = 1u
                };
            }

            for (int i = 0; i < ScannerProfiles.Length; i++)
                ScannerProfiles[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CartographyRevealAupCellJob : IJob
    {
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public CartographyAup Center;
        public int WordOffset;

        public void Execute()
        {
            if (!CartographyGridMath.TryEncode(in Center, out _, out int wordIndex, out int bitOffset))
                return;

            int absoluteWordIndex = WordOffset + wordIndex;
            if ((uint)absoluteWordIndex >= (uint)DiscoveredSectors.Length)
                return;

            ulong* words = (ulong*)NativeArrayUnsafeUtility.GetUnsafePtr(DiscoveredSectors);
            if (AtomicOr(words, absoluteWordIndex, 1UL << bitOffset))
                MarkChanged(Counters, wordIndex, 1);
        }

        internal static bool AtomicOr(ulong* words, int wordIndex, ulong bitMask)
        {
            long* signedWords = (long*)words;
            ref long signedWord = ref UnsafeUtility.AsRef<long>(signedWords + wordIndex);
            long signedBit = unchecked((long)bitMask);
            while (true)
            {
                long before = Interlocked.CompareExchange(ref signedWord, 0L, 0L);
                long after = before | signedBit;
                if (before == after)
                    return false;

                if (Interlocked.CompareExchange(ref signedWord, after, before) == before)
                    return true;
            }
        }

        internal static unsafe void MarkChanged(NativeArray<CartographyCounterDTO> counters, int bitIndex, int delta)
        {
            if (!counters.IsCreated || counters.Length == 0)
                return;

            CartographyCounterDTO* ptr = (CartographyCounterDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(counters);
            Interlocked.Exchange(ref ptr[0].Changed, 1);
            Interlocked.Add(ref ptr[0].DiscoveredDelta, delta);
            Interlocked.Add(ref ptr[0].TotalDiscoveredVoxels, delta);
            ptr[0].LastBitIndex = (uint)math.max(0, bitIndex);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplySonarDiscoveryJob : IJob
    {
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> SurfaceMaskWords;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public CartographyAup Center;
        public double3 CenterAup;
        public float RadiusMeters;
        public float SurfaceThicknessMeters;
        public float GlobalQualityWeight;
        public int UseExplicitCenterAup;
        public int UseSdfSurfaceMask;
        public int WordOffset;

        public void Execute()
        {
            double3 centerAbsolute = UseExplicitCenterAup != 0
                ? CenterAup
                : CartographyGridMath.ToAbsoluteDouble3(in Center);
            if (!math.all(math.isfinite(centerAbsolute)) ||
                !CartographyGridMath.TryResolveMacroCell(centerAbsolute, out int3 centerCell))
            {
                MarkOutOfBounds();
                return;
            }

            double cellSize = math.max(1.0, CartographyGridConstants.MacroCellSizeMeters);
            double radiusInput = math.isfinite(RadiusMeters) ? RadiusMeters : CartographyGridConstants.MacroCellSizeMeters;
            double radius = math.clamp(
                radiusInput,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxRevealRadiusMeters);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            double softShell = math.lerp(
                math.max(0.25f, SurfaceThicknessMeters),
                math.max(0.25f, SurfaceThicknessMeters) * 2.5f,
                1f - quality);
            double radiusSq = radius * radius;
            int radiusCells = math.max(0, (int)math.ceil(radius / cellSize));
            int changedCount = 0;
            int lastChangedBitIndex = -1;
            ulong* words = (ulong*)NativeArrayUnsafeUtility.GetUnsafePtr(DiscoveredSectors);

            for (int z = -radiusCells; z <= radiusCells; z++)
            {
                for (int y = -radiusCells; y <= radiusCells; y++)
                {
                    for (int x = -radiusCells; x <= radiusCells; x++)
                    {
                        int3 macroCell = centerCell + new int3(x, y, z);
                        double3 cellCenter = ((double3)macroCell + new double3(0.5)) * cellSize;
                        double3 delta = cellCenter - centerAbsolute;
                        double distSq = math.lengthsq(delta);
                        if (!math.isfinite(distSq) || distSq > radiusSq)
                            continue;

                        if (!CartographyGridMath.TryEncodeMacroCell(macroCell, out int bitIndex, out int wordIndex, out int bitOffset))
                            continue;

                        if (!PassesSurfaceMask(wordIndex, bitOffset, softShell, delta))
                            continue;

                        int absoluteWordIndex = WordOffset + wordIndex;
                        if ((uint)absoluteWordIndex >= (uint)DiscoveredSectors.Length)
                            continue;

                        if (CartographyRevealAupCellJob.AtomicOr(words, absoluteWordIndex, 1UL << bitOffset))
                        {
                            changedCount++;
                            lastChangedBitIndex = bitIndex;
                        }
                    }
                }
            }

            if (changedCount > 0)
                CartographyRevealAupCellJob.MarkChanged(Counters, math.max(0, lastChangedBitIndex), changedCount);
        }

        private bool PassesSurfaceMask(int wordIndex, int bitOffset, double shellMeters, double3 delta)
        {
            if (UseSdfSurfaceMask != 0 && SurfaceMaskWords.IsCreated && (uint)wordIndex < (uint)SurfaceMaskWords.Length)
                return (SurfaceMaskWords[wordIndex] & (1UL << bitOffset)) != 0UL;

            double radialDistance = math.sqrt(math.max(0.0001, math.lengthsq(delta)));
            return math.abs(radialDistance - math.round(radialDistance / CartographyGridConstants.MacroCellSizeMeters) * CartographyGridConstants.MacroCellSizeMeters) <= shellMeters ||
                   UseSdfSurfaceMask == 0;
        }

        private unsafe void MarkOutOfBounds()
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            CartographyCounterDTO* ptr = (CartographyCounterDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
            Interlocked.Exchange(ref ptr[0].Changed, 1);
            ptr[0].LastBitIndex = uint.MaxValue;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyCartographyFrameDiscoveryJob : IJob
    {
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> SurfaceMaskWords;
        [NoAlias]
        public NativeArray<MapRevealSignal> PendingSignals;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public CartographyAup PlayerAup;
        public float SurfaceThicknessMeters;
        public float GlobalQualityWeight;
        public int HasPlayerAup;
        public int PendingSignalCount;
        public int WordOffset;

        public void Execute()
        {
            if (!DiscoveredSectors.IsCreated || !Counters.IsCreated || Counters.Length == 0)
                return;

            CartographyCounterDTO counter = Counters[0];
            counter.Changed = 0;
            counter.DiscoveredDelta = 0;
            counter.LastBitIndex = 0u;
            Counters[0] = counter;

            if (HasPlayerAup != 0 && CartographyGridMath.IsFinite(in PlayerAup))
            {
                new CartographyRevealAupCellJob
                {
                    DiscoveredSectors = DiscoveredSectors,
                    Counters = Counters,
                    Center = PlayerAup,
                    WordOffset = WordOffset
                }.Execute();
            }

            int safeCount = PendingSignals.IsCreated
                ? math.min(PendingSignalCount, PendingSignals.Length)
                : 0;
            safeCount = math.clamp(safeCount, 0, CartographyGridConstants.MaxRevealSignalsPerSlowTick);
            for (int i = 0; i < safeCount; i++)
            {
                MapRevealSignal signal = PendingSignals[i];
                PendingSignals[i] = default;
                if (!CartographyGridMath.IsFinite(in signal.Center))
                    continue;

                float radius = math.clamp(
                    math.isfinite(signal.RadiusMeters) ? signal.RadiusMeters : CartographyGridConstants.MacroCellSizeMeters,
                    CartographyGridConstants.MacroCellSizeMeters,
                    CartographyGridConstants.MaxRevealRadiusMeters);
                new ApplySonarDiscoveryJob
                {
                    DiscoveredSectors = DiscoveredSectors,
                    SurfaceMaskWords = SurfaceMaskWords,
                    Counters = Counters,
                    Center = signal.Center,
                    RadiusMeters = radius,
                    SurfaceThicknessMeters = SurfaceThicknessMeters,
                    GlobalQualityWeight = GlobalQualityWeight,
                    UseExplicitCenterAup = 0,
                    UseSdfSurfaceMask = 1,
                    WordOffset = WordOffset
                }.Execute();
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CartographyRevealSphereJob : IJob
    {
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public CartographyAup Center;
        public float RadiusMeters;

        public void Execute()
        {
            new ApplySonarDiscoveryJob
            {
                DiscoveredSectors = DiscoveredSectors,
                Counters = Counters,
                Center = Center,
                RadiusMeters = RadiusMeters,
                SurfaceThicknessMeters = CartographyGridConstants.DefaultSurfaceThicknessMeters,
                GlobalQualityWeight = 1f,
                UseExplicitCenterAup = 0,
                UseSdfSurfaceMask = 0,
                WordOffset = 0
            }.Execute();
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CartographyInjectPoiJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CartographyPoiRecord> PoiRecords;
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public int Count;
        public int WordOffset;

        public void Execute()
        {
            int safeCount = math.min(Count, PoiRecords.Length);
            safeCount = math.min(safeCount, CartographyGridConstants.MaxPoiRevealPerSlowTick);
            int changedCount = 0;
            int lastChangedBitIndex = -1;
            ulong* words = (ulong*)NativeArrayUnsafeUtility.GetUnsafePtr(DiscoveredSectors);
            for (int i = 0; i < safeCount; i++)
            {
                CartographyAup position = PoiRecords[i].Position;
                if (!CartographyGridMath.TryEncode(in position, out int bitIndex, out int wordIndex, out int bitOffset))
                    continue;

                int absoluteWordIndex = WordOffset + wordIndex;
                if ((uint)absoluteWordIndex >= (uint)DiscoveredSectors.Length)
                    continue;

                if (CartographyRevealAupCellJob.AtomicOr(words, absoluteWordIndex, 1UL << bitOffset))
                {
                    changedCount++;
                    lastChangedBitIndex = bitIndex;
                }
            }

            if (changedCount > 0)
                CartographyRevealAupCellJob.MarkChanged(Counters, math.max(0, lastChangedBitIndex), changedCount);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockExplorationDataJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [NoAlias]
        public NativeArray<CartographySectorDTO> SectorTable;
        public uint SimulationFrameCounter;
        public ulong SectorHash;
        public float GlobalQualityWeight;
        public int WordOffset;

        public void Execute(int wordIndex)
        {
            int localWordIndex = wordIndex % CartographyGridConstants.WordCount;
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            ulong result = 0UL;
            for (int bit = 0; bit < 64; bit++)
            {
                int bitIndex = (localWordIndex << 6) | bit;
                int3 cell = CartographyGridMath.DecodeBitIndex(bitIndex);
                float3 position = (float3)cell * CartographyGridConstants.MacroCellSizeMeters;
                if (InsideAnyMockCluster(position, quality))
                    result |= 1UL << bit;
            }

            int absoluteWordIndex = WordOffset + localWordIndex;
            if ((uint)absoluteWordIndex < (uint)DiscoveredSectors.Length)
                DiscoveredSectors[absoluteWordIndex] = result;
        }

        private bool InsideAnyMockCluster(float3 position, float quality)
        {
            float qualityCurve = quality * quality * (3f - (2f * quality));
            int clusterCount = 3 + (int)math.round(math.lerp(0f, 5f, qualityCurve));
            for (int i = 0; i < clusterCount; i++)
            {
                uint seed = BuildSeed((uint)i);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(math.max(1u, seed));
                float3 center = new float3(
                    rng.NextFloat(-2200f, 2200f),
                    rng.NextFloat(-420f, 420f),
                    rng.NextFloat(-2200f, 2200f));
                float radius = rng.NextFloat(180f, math.lerp(420f, 980f, qualityCurve));
                float shell = math.lerp(80f, 24f, qualityCurve);
                float dist = math.sqrt(math.max(0.0001f, math.lengthsq(position - center)));
                if (dist <= radius && math.abs(dist - radius) <= shell)
                    return true;
            }

            return false;
        }

        private uint BuildSeed(uint cluster)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)SectorHash) * 16777619u;
                hash = (hash ^ (uint)(SectorHash >> 32)) * 16777619u;
                hash = (hash ^ SimulationFrameCounter) * 16777619u;
                hash = (hash ^ cluster) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildMockSurfaceMaskJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<ulong> SurfaceMaskWords;
        public float SurfaceThicknessMeters;
        public float GlobalQualityWeight;

        public void Execute(int wordIndex)
        {
            float thickness = math.clamp(
                math.isfinite(SurfaceThicknessMeters) ? SurfaceThicknessMeters : CartographyGridConstants.DefaultSurfaceThicknessMeters,
                0.25f,
                8f);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float lowQualityBand = math.lerp(thickness * 3f, thickness, quality);
            ulong word = 0UL;
            for (int bit = 0; bit < 64; bit++)
            {
                int bitIndex = (wordIndex << 6) | bit;
                int3 cell = CartographyGridMath.DecodeBitIndex(bitIndex);
                float x = cell.x * 0.071f;
                float z = cell.z * 0.083f;
                float fakeSdf = (cell.y * CartographyGridConstants.MacroCellSizeMeters) -
                                (math.sin(x) * 46f) -
                                (math.cos(z) * 33f);
                if (math.abs(fakeSdf) <= lowQualityBand)
                    word |= 1UL << bit;
            }

            SurfaceMaskWords[wordIndex] = word;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FormatCartographyUploadR8Job : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> DiscoveredSectors;
        [NoAlias]
        public NativeArray<uint> UploadPackedR8;
        public float GlobalQualityWeight;
        public int WordOffset;

        public void Execute(int packedIndex)
        {
            int firstVoxel = packedIndex << 2;
            uint packed = 0u;
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float visualKeep = math.lerp(0.35f, 1f, quality * quality * (3f - (2f * quality)));
            for (int lane = 0; lane < 4; lane++)
            {
                int voxelIndex = firstVoxel + lane;
                int wordIndex = WordOffset + (voxelIndex >> 6);
                int bitOffset = voxelIndex & 63;
                uint value = 0u;
                if ((uint)wordIndex < (uint)DiscoveredSectors.Length &&
                    (DiscoveredSectors[wordIndex] & (1UL << bitOffset)) != 0UL &&
                    PassesVisualDecimation(voxelIndex, visualKeep))
                {
                    value = 255u;
                }

                packed |= value << (lane * 8);
            }

            UploadPackedR8[packedIndex] = packed;
        }

        private static bool PassesVisualDecimation(int voxelIndex, float visualKeep)
        {
            uint hash = (uint)voxelIndex * 747796405u + 2891336453u;
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            float normalized = (hash & 0x00FFFFFFu) * (1f / 16777215f);
            return normalized <= visualKeep;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordCartographyTelemetryJob : IJob
    {
        [NoAlias]
        public NativeArray<CartographyTelemetryEntry> TelemetryRing;
        [NoAlias]
        public NativeArray<int> TelemetryCursor;
        [ReadOnly]
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public CartographyAup PlayerAup;
        public uint FrameIndex;
        public uint Revision;
        public int RevealedSignalCount;
        public int RevealedPoiCount;
        public uint StateFlags;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int cursor = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = (cursor + 1) % TelemetryRing.Length;
            }

            int safeCursor = math.clamp(cursor, 0, TelemetryRing.Length - 1);
            int lastBitIndex = -1;
            int discoveredVoxels = 0;
            if (Counters.IsCreated && Counters.Length > 0)
            {
                lastBitIndex = unchecked((int)Counters[0].LastBitIndex);
                discoveredVoxels = math.max(0, Counters[0].TotalDiscoveredVoxels);
            }

            TelemetryRing[safeCursor] = new CartographyTelemetryEntry
            {
                PlayerGridX = PlayerAup.GridX,
                PlayerGridY = PlayerAup.GridY,
                PlayerGridZ = PlayerAup.GridZ,
                PlayerLocalX = math.isfinite(PlayerAup.LocalX) ? PlayerAup.LocalX : 0f,
                PlayerLocalY = math.isfinite(PlayerAup.LocalY) ? PlayerAup.LocalY : 0f,
                PlayerLocalZ = math.isfinite(PlayerAup.LocalZ) ? PlayerAup.LocalZ : 0f,
                GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f),
                FrameIndex = FrameIndex,
                Revision = Revision,
                LastBitIndex = lastBitIndex,
                DiscoveredVoxelCount = (uint)discoveredVoxels,
                RevealedSignalCount = (ushort)math.clamp(RevealedSignalCount, 0, ushort.MaxValue),
                RevealedPoiCount = (ushort)math.clamp(RevealedPoiCount, 0, ushort.MaxValue),
                StateHash = CartographyGridMath.BuildStateHash(in PlayerAup, Revision ^ StateFlags, lastBitIndex)
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CopyCartographyRollbackSnapshotJob : IJobParallelFor
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> DiscoveryWords;
        [NoAlias]
        public NativeArray<ulong> RollbackSnapshotWords;
        public int WordOffset;

        public void Execute(int wordIndex)
        {
            int sourceIndex = WordOffset + wordIndex;
            RollbackSnapshotWords[wordIndex] = (uint)sourceIndex < (uint)DiscoveryWords.Length
                ? DiscoveryWords[sourceIndex]
                : 0UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCartographyRleRunsJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> DiscoveryWords;
        [NoAlias]
        public NativeArray<CartographyRleRunDTO> RleRuns;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public int WordOffset;
        public int WordCount;

        public void Execute()
        {
            if (!DiscoveryWords.IsCreated || !RleRuns.IsCreated || RleRuns.Length == 0)
                return;

            int safeWordCount = math.min(WordCount <= 0 ? CartographyGridConstants.WordCount : WordCount, CartographyGridConstants.WordCount);
            int runCount = 0;
            int start = 0;
            ulong previous = ReadWord(0);
            for (int i = 1; i < safeWordCount; i++)
            {
                ulong current = ReadWord(i);
                int runLength = i - start;
                if (current == previous && runLength < ushort.MaxValue)
                    continue;

                if (runCount < RleRuns.Length)
                {
                    RleRuns[runCount] = new CartographyRleRunDTO
                    {
                        WordValue = previous,
                        StartWordIndex = start,
                        WordCount = (ushort)math.max(1, runLength),
                        Flags = previous == 0UL ? (ushort)0 : (ushort)1
                    };
                    runCount++;
                }

                start = i;
                previous = current;
            }

            if (runCount < RleRuns.Length)
            {
                RleRuns[runCount] = new CartographyRleRunDTO
                {
                    WordValue = previous,
                    StartWordIndex = start,
                    WordCount = (ushort)math.max(1, safeWordCount - start),
                    Flags = previous == 0UL ? (ushort)0 : (ushort)1
                };
                runCount++;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CartographyCounterDTO counter = Counters[0];
                counter.DiscoveredDelta = runCount;
                Counters[0] = counter;
            }
        }

        private ulong ReadWord(int localWordIndex)
        {
            int sourceIndex = WordOffset + localWordIndex;
            return (uint)sourceIndex < (uint)DiscoveryWords.Length ? DiscoveryWords[sourceIndex] : 0UL;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildCartographyDebugVoxelsJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<ulong> DiscoveryWords;
        [NoAlias]
        public NativeArray<CartographyDebugVoxelDTO> DebugVoxels;
        [NoAlias]
        public NativeArray<CartographyCounterDTO> Counters;
        public int3 CenterMacroCell;
        public int RadiusCells;
        public int WordOffset;

        public void Execute()
        {
            if (!DiscoveryWords.IsCreated || !DebugVoxels.IsCreated)
                return;

            int count = 0;
            int safeRadius = math.clamp(RadiusCells, 1, 8);
            for (int z = -safeRadius; z <= safeRadius; z++)
            {
                for (int y = -safeRadius; y <= safeRadius; y++)
                {
                    for (int x = -safeRadius; x <= safeRadius; x++)
                    {
                        int3 cell = CenterMacroCell + new int3(x, y, z);
                        if (!CartographyGridMath.TryEncodeMacroCell(cell, out int bitIndex, out int wordIndex, out int bitOffset))
                            continue;

                        int absoluteWordIndex = WordOffset + wordIndex;
                        if ((uint)absoluteWordIndex >= (uint)DiscoveryWords.Length ||
                            (DiscoveryWords[absoluteWordIndex] & (1UL << bitOffset)) == 0UL)
                        {
                            continue;
                        }

                        if (count < DebugVoxels.Length)
                        {
                            DebugVoxels[count] = new CartographyDebugVoxelDTO
                            {
                                X = cell.x,
                                Y = cell.y,
                                Z = cell.z,
                                Flags = (uint)bitIndex
                            };
                            count++;
                        }
                    }
                }
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CartographyCounterDTO counter = Counters[0];
                counter.DiscoveredDelta = count;
                Counters[0] = counter;
            }
        }
    }
}
