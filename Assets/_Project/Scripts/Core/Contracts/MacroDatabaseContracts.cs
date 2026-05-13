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
        public const int CellSizeMeters = 5000;

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
    }

    public enum MacroDatabaseTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    [StructLayout(LayoutKind.Sequential)]
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

        public static MacroDatabaseConfig Default => new MacroDatabaseConfig
        {
            NodeSizeBytes = 4096,
            SectorSizeMeters = 512,
            LowTierRadiusMeters = 1000,
            MiddleTierRadiusMeters = 2000,
            HighTierRadiusMeters = 3000,
            UltraTierRadiusMeters = 4000,
            DehydrateRadiusMeters = 3000,
            MaxPayloadBytes = 256 * 1024,
            NativeCacheCapacity = 2048,
            MaxQuerySectors = 4096,
            InitialFileBytes = 8L * 1024L * 1024L,
            MaxFileBytes = 2L * 1024L * 1024L * 1024L,
            CreateIfMissing = 1,
            DefaultTier = (byte)MacroDatabaseTier.Middle
        };
    }

    [StructLayout(LayoutKind.Sequential)]
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
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MacroDatabaseNativeCacheStats
    {
        public long Bytes;
        public int Entries;
        public int Capacity;
        public int Evictions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MacroDatabaseStats
    {
        public long FileBytes;
        public long RootNodeOffset;
        public long CacheBytes;
        public int CacheEntries;
        public int PageFaults;
        public int HydratedSectors;
        public int EvictedSectors;
        public int DirtyAppendCount;
        public uint FrameIndex;
        public byte IsOpen;
        public byte Tier;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SectorHydratedSignal
    {
        public ulong SectorHash;
        public long FileOffset;
        public int PayloadBytes;
        public uint FrameIndex;
        public byte SourceTier;
        public byte Flags;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MacroDatabaseTelemetryEntry
    {
        public ulong PlayerSectorHash;
        public long RootNodeOffset;
        public long CacheBytes;
        public int CacheEntries;
        public int PageFaults;
        public int PageFaultsTotal;
        public int HydratedSectors;
        public int EvictedSectors;
        public uint FrameIndex;
        public byte Tier;
        public byte Flags;
        public ushort Reserved;
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
        void DumpBlackBox(string path);
        void Shutdown();
    }
}
