using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Blittable AUP transfer payload used by H8_MacroDB without taking a World assembly dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct MacroDatabaseAup
    {
        public const int CellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;

        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] private float _pad;

        public double3 ToAbsoluteDouble3()
        {
            const double CellSize = CellSizeMeters;
            return new double3(
                (GridX * CellSize) + LocalX,
                (GridY * CellSize) + LocalY,
                (GridZ * CellSize) + LocalZ);
        }

        public double3 OffsetAbsoluteMeters(double3 deltaMeters)
        {
            double3 absolute = ToAbsoluteDouble3();
            if (!math.all(math.isfinite(deltaMeters)))
                return absolute;

            double3 shifted = absolute + deltaMeters;
            return math.all(math.isfinite(shifted)) ? shifted : absolute;
        }

        public MacroDatabaseAup OffsetMeters(double3 deltaMeters)
        {
            double3 absolute = OffsetAbsoluteMeters(deltaMeters);
            const double CellSize = CellSizeMeters;
            long gridX = (long)math.floor(absolute.x / CellSize);
            long gridY = (long)math.floor(absolute.y / CellSize);
            long gridZ = (long)math.floor(absolute.z / CellSize);

            return new MacroDatabaseAup
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolute.x - (gridX * CellSize)),
                LocalY = (float)(absolute.y - (gridY * CellSize)),
                LocalZ = (float)(absolute.z - (gridZ * CellSize))
            };
        }
    }

    public enum MacroDatabaseTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    public enum MacroDatabaseCompactionState : byte
    {
        Idle = 0,
        Copying = 1,
        ReadyToSwap = 2,
        Swapping = 3,
        Paused = 4,
        Faulted = 5
    }

    public static class MacroDatabaseCompactionFlags
    {
        public const byte MemoryPressurePaused = 1 << 0;
        public const byte PersistenceGate = 1 << 1;
        public const byte TempReady = 1 << 2;
        public const byte LastSwapExceededBudget = 1 << 3;
    }

    public static class MacroDatabasePayloadFlags
    {
        public const byte Dirty = 1 << 0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroDatabaseConfig
    {
        [FieldOffset(0)] public int NodeSizeBytes;
        [FieldOffset(4)] public int SectorSizeMeters;
        [FieldOffset(8)] public int LowTierRadiusMeters;
        [FieldOffset(12)] public int MiddleTierRadiusMeters;
        [FieldOffset(16)] public int HighTierRadiusMeters;
        [FieldOffset(20)] public int UltraTierRadiusMeters;
        [FieldOffset(24)] public int DehydrateRadiusMeters;
        [FieldOffset(28)] public int MaxPayloadBytes;
        [FieldOffset(32)] public int NativeCacheCapacity;
        [FieldOffset(36)] public int MaxQuerySectors;
        [FieldOffset(40)] public long InitialFileBytes;
        [FieldOffset(48)] public long MaxFileBytes;
        [FieldOffset(56)] public byte CreateIfMissing;
        [FieldOffset(57)] public byte DefaultTier;
        [FieldOffset(58)] public ushort Reserved;
        [FieldOffset(60)] private uint _pad0;

        public static MacroDatabaseConfig Default => new MacroDatabaseConfig
        {
            NodeSizeBytes = HectonMmfPagingContract.BTreePageSizeBytes,
            SectorSizeMeters = HectonMmfPagingContract.MacroDatabaseSectorSizeMeters,
            LowTierRadiusMeters = HectonMmfPagingContract.MacroDatabaseLowTierRadiusMeters,
            MiddleTierRadiusMeters = HectonMmfPagingContract.MacroDatabaseMiddleTierRadiusMeters,
            HighTierRadiusMeters = HectonMmfPagingContract.MacroDatabaseHighTierRadiusMeters,
            UltraTierRadiusMeters = HectonMmfPagingContract.MacroDatabaseUltraTierRadiusMeters,
            DehydrateRadiusMeters = HectonMmfPagingContract.MacroDatabaseDehydrateRadiusMeters,
            MaxPayloadBytes = HectonMmfPagingContract.MacroDatabaseMaxPayloadBytes,
            NativeCacheCapacity = HectonMmfPagingContract.MacroDatabaseNativeCacheCapacity,
            MaxQuerySectors = HectonMmfPagingContract.MacroDatabaseMaxQuerySectors,
            InitialFileBytes = HectonMmfPagingContract.MacroDatabaseInitialFileBytes,
            MaxFileBytes = HectonMmfPagingContract.MacroDatabaseMaxFileBytes,
            CreateIfMissing = 1,
            DefaultTier = (byte)MacroDatabaseTier.Middle
        };
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct MacroDatabasePayloadHandle
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ulong PayloadToken;
        [FieldOffset(16)] public long FileOffset;
        [FieldOffset(24)] public int ByteLength;
        [FieldOffset(28)] public uint Version;
        [FieldOffset(32)] public byte Flags;
        [FieldOffset(33)] public byte Reserved0;
        [FieldOffset(34)] public ushort Reserved1;
        [FieldOffset(36)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct MacroDatabaseNativeCacheStats
    {
        [FieldOffset(0)] public long Bytes;
        [FieldOffset(8)] public int Entries;
        [FieldOffset(12)] public int Capacity;
        [FieldOffset(16)] public int Evictions;
        [FieldOffset(20)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct MacroDatabaseStats
    {
        [FieldOffset(0)] public long FileBytes;
        [FieldOffset(8)] public long DeadBytes;
        [FieldOffset(16)] public long CompactionTempBytes;
        [FieldOffset(24)] public long RootNodeOffset;
        [FieldOffset(32)] public long CacheBytes;
        [FieldOffset(40)] public int PendingDirtyPayloads;
        [FieldOffset(44)] public int LastCompactionStallMicroseconds;
        [FieldOffset(48)] public int CacheEntries;
        [FieldOffset(52)] public int PageFaults;
        [FieldOffset(56)] public int HydratedSectors;
        [FieldOffset(60)] public int EvictedSectors;
        [FieldOffset(64)] public int DirtyAppendCount;
        [FieldOffset(68)] public uint FrameIndex;
        [FieldOffset(72)] public byte IsOpen;
        [FieldOffset(73)] public byte Tier;
        [FieldOffset(74)] public byte CompactionState;
        [FieldOffset(75)] public byte CompactionFlags;
        [FieldOffset(76)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct MacroDatabaseCompactionSnapshot
    {
        [FieldOffset(0)] public long FileBytes;
        [FieldOffset(8)] public long DeadBytes;
        [FieldOffset(16)] public long ThresholdBytes;
        [FieldOffset(24)] public long TempBytes;
        [FieldOffset(32)] public int PendingDirtyPayloads;
        [FieldOffset(36)] public int LastSwapMicroseconds;
        [FieldOffset(40)] public uint FrameIndex;
        [FieldOffset(44)] public byte State;
        [FieldOffset(45)] public byte Flags;
        [FieldOffset(46)] public byte Tier;
        [FieldOffset(47)] public byte Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SectorHydratedSignal
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public long FileOffset;
        [FieldOffset(16)] public int PayloadBytes;
        [FieldOffset(20)] public uint FrameIndex;
        [FieldOffset(24)] public byte SourceTier;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] public ushort Reserved;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroDatabaseTelemetryEntry
    {
        [FieldOffset(0)] public ulong PlayerSectorHash;
        [FieldOffset(8)] public long RootNodeOffset;
        [FieldOffset(16)] public long CacheBytes;
        [FieldOffset(24)] public long DeadBytes;
        [FieldOffset(32)] public int CacheEntries;
        [FieldOffset(36)] public int PageFaults;
        [FieldOffset(40)] public int PageFaultsTotal;
        [FieldOffset(44)] public int HydratedSectors;
        [FieldOffset(48)] public int EvictedSectors;
        [FieldOffset(52)] public int LastCompactionStallMicroseconds;
        [FieldOffset(56)] public uint FrameIndex;
        [FieldOffset(60)] public byte Tier;
        [FieldOffset(61)] public byte CompactionState;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] private byte _pad0;
    }

    public interface IMacroDatabaseSignalSink
    {
        void PublishSectorHydrated(in SectorHydratedSignal signal);
    }

    public interface IMacroDatabaseNativeCacheOwner
    {
        bool TryReserveMacroDatabaseCache(int capacity);

        bool TryStoreMacroDatabasePayload(
            ulong sectorHash,
            NativeArray<byte> source,
            int byteLength,
            long fileOffset,
            byte flags,
            out MacroDatabasePayloadHandle handle);

        /// <summary>Opens cached payload metadata and refreshes cache residency; this is not a pure read accessor.</summary>
        bool TryOpenMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle);

        /// <summary>Copies cached payload bytes into caller-owned native memory and refreshes cache residency.</summary>
        bool TryCopyMacroDatabasePayload(
            ulong sectorHash,
            int sourceOffsetBytes,
            NativeArray<byte> destination,
            int destinationCapacityBytes,
            out int bytesCopied,
            out MacroDatabasePayloadHandle handle);

        bool TryCopyMacroDatabasePayload<T>(
            ulong sectorHash,
            int sourceOffsetBytes,
            NativeArray<T> destination,
            int destinationCapacityBytes,
            out int bytesCopied,
            out MacroDatabasePayloadHandle handle)
            where T : struct;

        bool TryRemoveMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle removed);

        int CopyMacroDatabasePayloadKeys(NativeArray<ulong> destination);

        int EvictMacroDatabasePayloads(NativeArray<ulong> sectorHashes, int count);

        MacroDatabaseNativeCacheStats GetMacroDatabaseCacheStats();
    }

    public interface IMacroDatabaseService : IDisposable
    {
        bool IsOpen { get; }
        MacroDatabaseStats Stats { get; }
        MacroDatabaseCompactionSnapshot Compaction { get; }

        bool Initialize(
            string path,
            in MacroDatabaseConfig config,
            IMacroDatabaseNativeCacheOwner cacheOwner,
            IMacroDatabaseSignalSink signalSink);

        bool TryOpenExisting(string path);
        bool TryCreateEmpty(string path, long initialSizeBytes);
        int BuildSectorHashWindow(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, NativeArray<ulong> destination);
        int HydrateRadius(in MacroDatabaseAup playerAup, MacroDatabaseTier tier);
        Awaitable<int> HydrateRadiusAsync(MacroDatabaseAup playerAup, MacroDatabaseTier tier, CancellationToken cancellationToken = default);
        /// <summary>Ensures the payload is hydrated into the native cache and returns metadata only.</summary>
        bool EnsurePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle);

        /// <summary>Copies hydrated payload bytes into caller-owned native memory; no cache address escapes.</summary>
        bool TryCopyPayload(
            ulong sectorHash,
            int sourceOffsetBytes,
            NativeArray<byte> destination,
            int destinationCapacityBytes,
            out int bytesCopied,
            out MacroDatabasePayloadHandle handle);
        bool MarkDirty(ulong sectorHash, NativeArray<byte> payload, int byteLength, byte flags);
        bool MarkDirty<T>(ulong sectorHash, in T payload, byte flags) where T : unmanaged;
        int EvictDistant(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, NativeArray<ulong> evictionScratch);
        bool TryAppendDirtyPayload(ulong sectorHash);
        bool TryRepackOffline(string destinationPath);
        bool FrostTickCompaction(MacroDatabaseTier tier, bool persistenceBusy);
        bool TryRequestBackgroundCompaction(MacroDatabaseTier tier, byte reasonFlags = 0);
        bool TryCompleteCompactionSwap(MacroDatabaseTier tier, bool persistenceBusy);
        void NotifyPersistenceGate(bool blocked, uint frame);
        void NotifyCriticalMemoryPressure(long reservedMemoryBytes, long physicalMemoryBytes, float usageRatio, uint frame, byte severity);
        void DumpBlackBox(string path);
        void Shutdown();
    }
}
