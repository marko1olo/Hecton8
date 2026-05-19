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

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MacroDatabaseConfig
    {
        public int NodeSizeBytes;
        public int SectorSizeMeters;
        public int LowTierRadiusMeters;
        public int MiddleTierRadiusMeters;
        public int HighTierRadiusMeters;
        public int UltraTierRadiusMeters;
        public int DehydrateRadiusMeters;
        public int MaxPayloadBytes;
        public int NativeCacheCapacity;
        public int MaxQuerySectors;
        public long InitialFileBytes;
        public long MaxFileBytes;
        public byte CreateIfMissing;
        public byte DefaultTier;
        public ushort Reserved;
        private uint _pad0;

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

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct MacroDatabasePayloadHandle
    {
        public ulong SectorHash;
        public IntPtr Pointer;
        public long FileOffset;
        public int ByteLength;
        public uint Version;
        public byte Flags;
        public byte Reserved0;
        public ushort Reserved1;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct MacroDatabaseNativeCacheStats
    {
        public long Bytes;
        public int Entries;
        public int Capacity;
        public int Evictions;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct MacroDatabaseStats
    {
        public long FileBytes;
        public long DeadBytes;
        public long CompactionTempBytes;
        public long RootNodeOffset;
        public long CacheBytes;
        public int PendingDirtyPayloads;
        public int LastCompactionStallMicroseconds;
        public int CacheEntries;
        public int PageFaults;
        public int HydratedSectors;
        public int EvictedSectors;
        public int DirtyAppendCount;
        public uint FrameIndex;
        public byte IsOpen;
        public byte Tier;
        public byte CompactionState;
        public byte CompactionFlags;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct MacroDatabaseCompactionSnapshot
    {
        public long FileBytes;
        public long DeadBytes;
        public long ThresholdBytes;
        public long TempBytes;
        public int PendingDirtyPayloads;
        public int LastSwapMicroseconds;
        public uint FrameIndex;
        public byte State;
        public byte Flags;
        public byte Tier;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct SectorHydratedSignal
    {
        public ulong SectorHash;
        public long FileOffset;
        public int PayloadBytes;
        public uint FrameIndex;
        public byte SourceTier;
        public byte Flags;
        public ushort Reserved;
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 72)]
    public struct MacroDatabaseTelemetryEntry
    {
        public ulong PlayerSectorHash;
        public long RootNodeOffset;
        public long CacheBytes;
        public long DeadBytes;
        public int CacheEntries;
        public int PageFaults;
        public int PageFaultsTotal;
        public int HydratedSectors;
        public int EvictedSectors;
        public int LastCompactionStallMicroseconds;
        public uint FrameIndex;
        public byte Tier;
        public byte CompactionState;
        public byte Flags;
        private byte _pad0;
        public ushort Reserved;
        private ushort _pad1;
        private uint _pad2;
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
            IntPtr source,
            int byteLength,
            long fileOffset,
            byte flags,
            out MacroDatabasePayloadHandle handle);

        bool TryGetMacroDatabasePayload(ulong sectorHash, out MacroDatabasePayloadHandle handle);

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
        bool TryGetPayload(ulong sectorHash, out MacroDatabasePayloadHandle handle);
        bool MarkDirty(ulong sectorHash, IntPtr payload, int byteLength, byte flags);
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
