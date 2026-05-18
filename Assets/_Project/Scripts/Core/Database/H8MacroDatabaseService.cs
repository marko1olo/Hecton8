#if UNITY_EDITOR || UNITY_STANDALONE
#define HECTON8_MMF_AVAILABLE
#endif

using System;
using System.Diagnostics;
using System.IO;
#if HECTON8_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Database
{
    public sealed unsafe class H8MacroDatabaseService : IMacroDatabaseService
    {
        private const int BlackBoxFrameCount = 300;
        private const int MemoryPressurePauseMilliseconds = 3000;
        private const long DefaultCompactionThresholdBytes = 10L * 1024L * 1024L;
        private const long LowTierCompactionThresholdBytes = 50L * 1024L * 1024L;
        private const long MinimumFileBytes = H8MacroDatabaseFileFormat.HeaderSizeBytes + H8MacroDatabaseFileFormat.NodeSizeBytes;

        private readonly object _fileGate = new object(); // COLD ALLOC: Object[1] — guards MMF pointer remaps against background hydration — owner: H8MacroDatabaseService
#if HECTON8_MMF_AVAILABLE
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _viewAccessor;
#endif
        private FileStream _fileStream;
        private byte* _basePointer;
        private long _mappedBytes;
        private string _path;
        private MacroDatabaseConfig _config;
        private IMacroDatabaseNativeCacheOwner _cacheOwner;
        private IMacroDatabaseSignalSink _signalSink;
        private NativeArray<ulong> _sectorWindowScratch;
        private NativeArray<SectorCoord64> _sectorCoordWindowScratch;
        private NativeArray<HydrationCandidate> _asyncHydrateScratch;
        private NativeArray<MacroDatabaseTelemetryEntry> _blackBox;
        private NativeParallelHashMap<ulong, MacroDatabasePayloadHandle> _dirtyPayloads;
        private NativeList<ulong> _dirtyPayloadKeys;
        private NativeParallelHashMap<ulong, SectorCoord64> _sectorCoordsByHash;
        private int _blackBoxWriteIndex;
        private int _pageFaults;
        private int _pageFaultWindowStartTickMs;
        private int _pageFaultWindowCount;
        private int _hydratedSectors;
        private int _evictedSectors;
        private int _dirtyAppendCount;
        private int _asyncHydrationActive;
        private int _compactionActive;
        private int _compactionCopyActive;
        private int _compactionState;
        private int _compactionPersistenceGate;
        private int _compactionMemoryResumeTickMs;
        private uint _frameIndex;
        private long _deadBytes;
        private long _compactionTempBytes;
        private long _lastCompactionStallMicroseconds;
        private string _compactionTempPath;
        private MacroDatabaseTier _compactionTier;
        private byte _compactionFlags;
        private double _sectorSizeRcp = 1.0d / 512.0d;

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private struct SectorCoord64
        {
            public long X;
            public long Y;
            public long Z;

            public SectorCoord64(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 48)]
        private struct HydrationCandidate
        {
            public ulong SectorHash;
            public long PayloadOffset;
            public SectorCoord64 Sector;
            public int PayloadBytes;
            public byte Flags;
            public byte Reserved0;
            public ushort Reserved1;
        }

        public bool IsOpen => _basePointer != null && _mappedBytes >= MinimumFileBytes;

        public MacroDatabaseStats Stats
        {
            get
            {
                lock (_fileGate)
                {
                    MacroDatabaseNativeCacheStats cacheStats = _cacheOwner != null
                        ? _cacheOwner.GetMacroDatabaseCacheStats()
                        : default;

                    return new MacroDatabaseStats
                    {
                        FileBytes = _mappedBytes,
                        DeadBytes = _deadBytes,
                        CompactionTempBytes = _compactionTempBytes,
                        RootNodeOffset = IsOpen ? ReadRootNodeOffset() : 0L,
                        CacheBytes = cacheStats.Bytes,
                        PendingDirtyPayloads = _dirtyPayloadKeys.IsCreated ? _dirtyPayloadKeys.Length : 0,
                        LastCompactionStallMicroseconds = SaturateToInt(_lastCompactionStallMicroseconds),
                        CacheEntries = cacheStats.Entries,
                        PageFaults = _pageFaults,
                        HydratedSectors = _hydratedSectors,
                        EvictedSectors = _evictedSectors,
                        DirtyAppendCount = _dirtyAppendCount,
                        FrameIndex = _frameIndex,
                        IsOpen = IsOpen ? (byte)1 : (byte)0,
                        Tier = _config.DefaultTier,
                        CompactionState = (byte)_compactionState,
                        CompactionFlags = _compactionFlags
                    };
                }
            }
        }

        public MacroDatabaseCompactionSnapshot Compaction
        {
            get
            {
                lock (_fileGate)
                {
                    MacroDatabaseTier tier = _compactionTier;
                    if ((byte)tier > (byte)MacroDatabaseTier.Ultra)
                        tier = (MacroDatabaseTier)_config.DefaultTier;

                    return new MacroDatabaseCompactionSnapshot
                    {
                        FileBytes = _mappedBytes,
                        DeadBytes = _deadBytes,
                        ThresholdBytes = ResolveCompactionThresholdBytes(tier),
                        TempBytes = _compactionTempBytes,
                        PendingDirtyPayloads = _dirtyPayloadKeys.IsCreated ? _dirtyPayloadKeys.Length : 0,
                        LastSwapMicroseconds = SaturateToInt(_lastCompactionStallMicroseconds),
                        FrameIndex = _frameIndex,
                        State = (byte)_compactionState,
                        Flags = _compactionFlags,
                        Tier = (byte)tier
                    };
                }
            }
        }

        public bool Initialize(
            string path,
            in MacroDatabaseConfig config,
            IMacroDatabaseNativeCacheOwner cacheOwner,
            IMacroDatabaseSignalSink signalSink)
        {
            Shutdown();
            _config = NormalizeConfig(config);
            _cacheOwner = cacheOwner;
            _signalSink = signalSink;
            EnsureNativeState();
            CleanupCompactionTemp(path);

            if (_cacheOwner != null && !_cacheOwner.TryReserveMacroDatabaseCache(_config.NativeCacheCapacity))
            {
                Shutdown();
                return false;
            }

            bool opened = File.Exists(path)
                ? TryOpenExisting(path)
                : _config.CreateIfMissing != 0 && TryCreateEmpty(path, _config.InitialFileBytes);

            if (!opened)
                Shutdown();

            return opened;
        }

        public bool TryOpenExisting(string path)
        {
            return TryOpenExistingFile(path, true);
        }

        private bool TryOpenExistingFile(string path, bool requireDatabaseExtension)
        {
            if ((requireDatabaseExtension && !IsValidDatabasePath(path)) || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            lock (_fileGate)
            {
                CloseFileHandles();
                EnsureNativeState();
                try
                {
                    _path = path;
                    _fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    if (_fileStream.Length < MinimumFileBytes)
                    {
                        CloseFileHandles();
                        return false;
                    }

                    if (_config.MaxFileBytes > 0L && _fileStream.Length > _config.MaxFileBytes)
                    {
                        CloseFileHandles();
                        return false;
                    }

                    if (!MapFile(_fileStream.Length) || !ValidateHeader())
                    {
                        CloseFileHandles();
                        return false;
                    }

                    ReconcileDeadBytesLocked();
                    RecordBlackBox(0UL, (MacroDatabaseTier)_config.DefaultTier, 0);
                    return true;
                }
                catch
                {
                    CloseFileHandles();
                    return false;
                }
            }
        }

        public bool TryCreateEmpty(string path, long initialSizeBytes)
        {
            return TryCreateEmptyFile(path, initialSizeBytes, true);
        }

        private bool TryCreateEmptyFile(string path, long initialSizeBytes, bool requireDatabaseExtension)
        {
            if ((requireDatabaseExtension && !IsValidDatabasePath(path)) || string.IsNullOrEmpty(path))
                return false;

            lock (_fileGate)
            {
                CloseFileHandles();
                EnsureNativeState();
                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    _path = path;
                    long safeLength = H8MacroDatabaseFileFormat.AlignUp(
                        math.max((long)MinimumFileBytes, math.max(initialSizeBytes, _config.InitialFileBytes)),
                        H8MacroDatabaseFileFormat.NodeSizeBytes);

                    if (_config.MaxFileBytes > 0L && safeLength > _config.MaxFileBytes)
                    {
                        CloseFileHandles();
                        return false;
                    }

                    _fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    _fileStream.SetLength(safeLength);
                    if (!MapFile(safeLength))
                    {
                        CloseFileHandles();
                        return false;
                    }

                    WriteEmptyHeader();
                    _deadBytes = 0L;
                    WriteDeadBytes(0L);
                    byte* root = NodeAt(H8MacroDatabaseFileFormat.HeaderSizeBytes);
                    if (root == null)
                    {
                        CloseFileHandles();
                        return false;
                    }

                    ClearNode(root, true);
                    Flush();
                    RecordBlackBox(0UL, (MacroDatabaseTier)_config.DefaultTier, 0);
                    return true;
                }
                catch
                {
                    CloseFileHandles();
                    return false;
                }
            }
        }

        public int BuildSectorHashWindow(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, NativeArray<ulong> destination)
        {
            lock (_fileGate)
            {
                return BuildSectorHashWindowLocked(in playerAup, tier, destination);
            }
        }

        private int BuildSectorHashWindowLocked(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, NativeArray<ulong> destination)
        {
            return BuildSectorHashWindowLocked(in playerAup, tier, destination, default);
        }

        private int BuildSectorHashWindowLocked(
            in MacroDatabaseAup playerAup,
            MacroDatabaseTier tier,
            NativeArray<ulong> destination,
            NativeArray<SectorCoord64> coordDestination)
        {
            if (!destination.IsCreated || destination.Length == 0)
                return 0;

            int radiusMeters = ResolveRadiusMeters(tier);
            int sectorSize = math.max(1, _config.SectorSizeMeters);
            int sectorRadius = math.max(0, (int)math.ceil(radiusMeters * _sectorSizeRcp));
            SectorCoord64 center = ResolveSectorCoord(in playerAup);
            long radiusSq = (long)radiusMeters * radiusMeters;
            int count = 0;

            for (int z = -sectorRadius; z <= sectorRadius; z++)
            {
                for (int y = -sectorRadius; y <= sectorRadius; y++)
                {
                    for (int x = -sectorRadius; x <= sectorRadius; x++)
                    {
                        long dx = (long)x * sectorSize;
                        long dy = (long)y * sectorSize;
                        long dz = (long)z * sectorSize;
                        if ((dx * dx) + (dy * dy) + (dz * dz) > radiusSq)
                            continue;

                        if (count >= destination.Length)
                            return count;

                        SectorCoord64 sector = new SectorCoord64(center.X + x, center.Y + y, center.Z + z);
                        ulong hash = ComputeSectorHash(sector, sectorSize);
                        destination[count] = hash;
                        if (coordDestination.IsCreated && count < coordDestination.Length)
                            coordDestination[count] = sector;
                        count++;
                    }
                }
            }

            return count;
        }

        public int HydrateRadius(in MacroDatabaseAup playerAup, MacroDatabaseTier tier)
        {
            lock (_fileGate)
            {
                return HydrateRadiusLocked(in playerAup, tier);
            }
        }

        public Awaitable<int> HydrateRadiusAsync(
            MacroDatabaseAup playerAup,
            MacroDatabaseTier tier,
            CancellationToken cancellationToken = default)
        {
            return H8MacroDatabaseAsyncHydration.RunAsync(this, playerAup, tier, cancellationToken);
        }

        private int HydrateRadiusLocked(in MacroDatabaseAup playerAup, MacroDatabaseTier tier)
        {
            if (!IsOpen || _cacheOwner == null || !_sectorWindowScratch.IsCreated)
                return 0;

            int hydratedThisCall = 0;
            int hashCount = BuildSectorHashWindowLocked(in playerAup, tier, _sectorWindowScratch, _sectorCoordWindowScratch);
            for (int i = 0; i < hashCount; i++)
            {
                ulong sectorHash = _sectorWindowScratch[i];
                if (_cacheOwner.TryGetMacroDatabasePayload(sectorHash, out _))
                {
                    if (_sectorCoordWindowScratch.IsCreated && i < _sectorCoordWindowScratch.Length)
                        CacheSectorCoord(sectorHash, _sectorCoordWindowScratch[i]);
                    continue;
                }

                if (!TryFindPayloadOffset(sectorHash, out long payloadOffset) ||
                    !TryReadPayloadPointer(payloadOffset, sectorHash, out byte* payloadPointer, out int payloadBytes, out byte flags))
                {
                    continue;
                }

                RecordPageFaultLocked(tier);
                if (_cacheOwner.TryStoreMacroDatabasePayload(
                        sectorHash,
                        (IntPtr)payloadPointer,
                        payloadBytes,
                        payloadOffset,
                        flags,
                        out _))
                {
                    hydratedThisCall++;
                    _hydratedSectors++;
                    if (_sectorCoordWindowScratch.IsCreated && i < _sectorCoordWindowScratch.Length)
                        CacheSectorCoord(sectorHash, _sectorCoordWindowScratch[i]);
                    PublishHydrated(sectorHash, payloadOffset, payloadBytes, tier, flags);
                }
            }

            _frameIndex++;
            RecordBlackBox(hashCount > 0 ? _sectorWindowScratch[0] : 0UL, tier, hydratedThisCall);
            return hydratedThisCall;
        }

        public bool TryGetPayload(ulong sectorHash, out MacroDatabasePayloadHandle handle)
        {
            lock (_fileGate)
            {
                handle = default;
                if (_cacheOwner != null && _cacheOwner.TryGetMacroDatabasePayload(sectorHash, out handle))
                    return true;

                if (!IsOpen ||
                    _cacheOwner == null ||
                    !TryFindPayloadOffset(sectorHash, out long payloadOffset) ||
                    !TryReadPayloadPointer(payloadOffset, sectorHash, out byte* payloadPointer, out int payloadBytes, out byte flags))
                {
                    return false;
                }

                RecordPageFaultLocked((MacroDatabaseTier)_config.DefaultTier);
                if (!_cacheOwner.TryStoreMacroDatabasePayload(
                        sectorHash,
                        (IntPtr)payloadPointer,
                        payloadBytes,
                        payloadOffset,
                        flags,
                        out handle))
                {
                    return false;
                }

                _hydratedSectors++;
                _frameIndex++;
                PublishHydrated(sectorHash, payloadOffset, payloadBytes, (MacroDatabaseTier)_config.DefaultTier, flags);
                RecordBlackBox(sectorHash, (MacroDatabaseTier)_config.DefaultTier, 1);
                return true;
            }
        }

        public bool MarkDirty(ulong sectorHash, IntPtr payload, int byteLength, byte flags)
        {
            if (payload == IntPtr.Zero || byteLength <= 0 || byteLength > _config.MaxPayloadBytes)
                return false;

            lock (_fileGate)
            {
                EnsureNativeState();
                bool hadDirty = _dirtyPayloads.ContainsKey(sectorHash);
                if (!hadDirty &&
                    (!_dirtyPayloadKeys.IsCreated ||
                     _dirtyPayloadKeys.Length >= _dirtyPayloadKeys.Capacity ||
                     _dirtyPayloads.Count() >= _dirtyPayloads.Capacity))
                {
                    return false;
                }

                if (_cacheOwner == null ||
                    !_cacheOwner.TryStoreMacroDatabasePayload(
                        sectorHash,
                        payload,
                        byteLength,
                        0L,
                        (byte)(flags | MacroDatabasePayloadFlags.Dirty),
                        out MacroDatabasePayloadHandle handle))
                {
                    return false;
                }

                handle.Flags = (byte)(handle.Flags | MacroDatabasePayloadFlags.Dirty);
                if (hadDirty)
                {
                    _dirtyPayloads[sectorHash] = handle;
                    return true;
                }

                if (!_dirtyPayloads.TryAdd(sectorHash, handle))
                {
                    return false;
                }

                _dirtyPayloadKeys.AddNoResize(sectorHash);
                return true;
            }
        }

        public int EvictDistant(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, NativeArray<ulong> evictionScratch)
        {
            lock (_fileGate)
            {
                if (_cacheOwner == null || !evictionScratch.IsCreated || evictionScratch.Length == 0)
                    return 0;

                int cachedCount = _cacheOwner.CopyMacroDatabasePayloadKeys(_sectorWindowScratch);
                if (cachedCount <= 0)
                    return 0;

                SectorCoord64 center = ResolveSectorCoord(in playerAup);
                int sectorSize = math.max(1, _config.SectorSizeMeters);
                long dehydrateRadius = math.max(_config.DehydrateRadiusMeters, ResolveRadiusMeters(tier));
                long dehydrateRadiusSq = dehydrateRadius * dehydrateRadius;
                int evictionCount = 0;
                for (int i = 0; i < cachedCount && evictionCount < evictionScratch.Length; i++)
                {
                    ulong sectorHash = _sectorWindowScratch[i];
                    if (!_sectorCoordsByHash.TryGetValue(sectorHash, out SectorCoord64 sector))
                        continue;

                    long dx = (sector.X - center.X) * sectorSize;
                    long dy = (sector.Y - center.Y) * sectorSize;
                    long dz = (sector.Z - center.Z) * sectorSize;
                    if ((dx * dx) + (dy * dy) + (dz * dz) <= dehydrateRadiusSq)
                        continue;

                    if (_dirtyPayloads.IsCreated &&
                        _dirtyPayloads.ContainsKey(sectorHash) &&
                        !TryAppendDirtyPayloadLocked(sectorHash))
                    {
                        continue;
                    }

                    evictionScratch[evictionCount++] = sectorHash;
                }

                int evicted = _cacheOwner.EvictMacroDatabasePayloads(evictionScratch, evictionCount);
                for (int i = 0; i < evictionCount; i++)
                {
                    if (!_cacheOwner.TryGetMacroDatabasePayload(evictionScratch[i], out _))
                        RemoveSectorCoord(evictionScratch[i]);
                }

                _evictedSectors += evicted;
                _frameIndex++;
                RecordBlackBox(ComputeSectorHash(center, sectorSize), tier, 0);
                return evicted;
            }
        }

        public bool TryAppendDirtyPayload(ulong sectorHash)
        {
            lock (_fileGate)
            {
                if (_dirtyPayloads.IsCreated &&
                    _dirtyPayloads.TryGetValue(sectorHash, out _) &&
                    IsCompactionWriteLocked())
                {
                    return false;
                }

                if (!_dirtyPayloads.IsCreated ||
                    !_dirtyPayloads.ContainsKey(sectorHash))
                {
                    return IsOpen &&
                           TryFindPayloadOffset(sectorHash, out long committedOffset) &&
                           TryReadPayloadPointer(committedOffset, sectorHash, out _, out _, out _);
                }

                return TryAppendDirtyPayloadLocked(sectorHash);
            }
        }

        private bool TryAppendDirtyPayloadLocked(ulong sectorHash)
        {
            if (!IsOpen || !_dirtyPayloads.IsCreated || !_dirtyPayloads.TryGetValue(sectorHash, out MacroDatabasePayloadHandle dirty))
                return false;

            if (IsCompactionWriteLocked())
                return false;

            if (dirty.Pointer == IntPtr.Zero || dirty.ByteLength <= 0 || dirty.ByteLength > _config.MaxPayloadBytes)
                return false;

            long oldPayloadRecordBytes = 0L;
            bool hadLivePayload = TryFindPayloadOffset(sectorHash, out long oldPayloadOffset) &&
                                  TryReadPayloadRecordBytes(oldPayloadOffset, sectorHash, out oldPayloadRecordBytes);
            long previousAppendOffset = ReadAppendOffset();
            if (!AppendPayloadRaw(sectorHash, dirty.Pointer.ToPointer(), dirty.ByteLength, dirty.Flags, out long payloadOffset))
                return false;

            if (!UpsertPayloadOffset(sectorHash, payloadOffset))
            {
                WriteAppendOffset(previousAppendOffset);
                Flush();
                return false;
            }

            _dirtyPayloads.Remove(sectorHash);
            RemoveDirtyPayloadKey(sectorHash);
            MarkPayloadCleanInCacheLocked(sectorHash, in dirty, payloadOffset);
            if (hadLivePayload && oldPayloadOffset != payloadOffset)
                AddDeadBytesLocked(oldPayloadRecordBytes);

            _dirtyAppendCount++;
            Flush();
            return true;
        }

        public bool TryRepackOffline(string destinationPath)
        {
            lock (_fileGate)
            {
                if (!IsOpen || !IsValidDatabasePath(destinationPath) || IsCompactionWriteLocked())
                    return false;

                H8MacroDatabaseService target = new H8MacroDatabaseService();
                try
                {
                    target._config = NormalizeConfig(_config);
                    target.EnsureNativeState();
                    if (!target.TryCreateEmpty(destinationPath, _config.InitialFileBytes))
                        return false;

                    bool copied = CopyNodePayloadsTo(ReadRootNodeOffset(), target);
                    target.Flush();
                    return copied;
                }
                finally
                {
                    target.Shutdown();
                }
            }
        }

        public bool FrostTickCompaction(MacroDatabaseTier tier, bool persistenceBusy)
        {
            NotifyPersistenceGate(persistenceBusy, _frameIndex);
            if (persistenceBusy)
                return false;

            if (TryCompleteCompactionSwap(tier, false))
                return true;

            return TryRequestBackgroundCompaction(tier, 0);
        }

        public bool TryRequestBackgroundCompaction(MacroDatabaseTier tier, byte reasonFlags = 0)
        {
            lock (_fileGate)
            {
                if (!IsOpen ||
                    _deadBytes < ResolveCompactionThresholdBytes(tier) ||
                    Volatile.Read(ref _compactionActive) != 0 ||
                    Volatile.Read(ref _compactionPersistenceGate) != 0 ||
                    IsMemoryPressurePauseActive())
                {
                    return false;
                }

                string tempPath = ResolveCompactionTempPath(_path);
                if (string.IsNullOrEmpty(tempPath))
                    return false;

                CleanupCompactionTemp(_path);
                _compactionTempPath = tempPath;
                _compactionTempBytes = 0L;
                _compactionTier = tier;
                _compactionFlags = reasonFlags;
                _compactionState = (int)MacroDatabaseCompactionState.Copying;
                Volatile.Write(ref _compactionActive, 1);
            }

            _ = H8MacroDatabaseCompaction.RunAsync(this, tier);
            return true;
        }

        public bool TryCompleteCompactionSwap(MacroDatabaseTier tier, bool persistenceBusy)
        {
            if (persistenceBusy)
            {
                NotifyPersistenceGate(true, _frameIndex);
                return false;
            }

            lock (_fileGate)
            {
                if (_compactionState != (int)MacroDatabaseCompactionState.ReadyToSwap ||
                    string.IsNullOrEmpty(_compactionTempPath) ||
                    !File.Exists(_compactionTempPath) ||
                    Volatile.Read(ref _compactionPersistenceGate) != 0)
                {
                    return false;
                }

                _compactionState = (int)MacroDatabaseCompactionState.Swapping;
                long startTimestamp = Stopwatch.GetTimestamp();
                bool swapped = false;
                int flushedDirtyPayloads = 0;
                H8MacroDatabaseService target = new H8MacroDatabaseService();
                try
                {
                    target._config = NormalizeConfig(_config);
                    target.EnsureNativeState();
                    if (!target.TryOpenExistingFile(_compactionTempPath, false))
                    {
                        MarkCompactionFaultLocked();
                        return false;
                    }

                    if (!FlushDirtyPayloadsIntoTargetLocked(target, out flushedDirtyPayloads))
                    {
                        MarkCompactionFaultLocked();
                        return false;
                    }

                    target.WriteDeadBytes(0L);
                    if (!target.TruncateToAppendOffset())
                    {
                        MarkCompactionFaultLocked();
                        return false;
                    }

                    target.Flush();
                    target.Shutdown();
                    target = null;

                    string activePath = _path;
                    CloseFileHandles();
                    File.Replace(_compactionTempPath, activePath, null, true);
                    swapped = TryOpenExistingFile(activePath, true);
                    if (!swapped)
                    {
                        MarkCompactionFaultLocked();
                        return false;
                    }

                    _deadBytes = 0L;
                    WriteDeadBytes(0L);
                    MarkDirtyPayloadCacheCleanAfterSwapLocked();
                    ClearDirtyPayloadQueueLocked();
                    _dirtyAppendCount += flushedDirtyPayloads;
                    _compactionTempBytes = 0L;
                    _compactionTempPath = null;
                    _compactionFlags = 0;
                    _compactionTier = tier;
                    _compactionState = (int)MacroDatabaseCompactionState.Idle;
                    Volatile.Write(ref _compactionActive, 0);
                    Flush();
                    return true;
                }
                catch
                {
                    if (!swapped && !IsOpen && !string.IsNullOrEmpty(_path) && File.Exists(_path))
                        TryOpenExistingFile(_path, true);

                    MarkCompactionFaultLocked();
                    return false;
                }
                finally
                {
                    bool faulted = _compactionState == (int)MacroDatabaseCompactionState.Faulted;
                    if (target != null)
                        target.Shutdown();

                    if (faulted)
                        CleanupCompactionTemp(_path);

                    long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
                    _lastCompactionStallMicroseconds = elapsedTicks > 0L
                        ? (elapsedTicks * 1000000L) / Stopwatch.Frequency
                        : 0L;

                    if (_lastCompactionStallMicroseconds > 2000L)
                        _compactionFlags = (byte)(_compactionFlags | MacroDatabaseCompactionFlags.LastSwapExceededBudget);
                }
            }
        }

        public void NotifyPersistenceGate(bool blocked, uint frame)
        {
            Volatile.Write(ref _compactionPersistenceGate, blocked ? 1 : 0);
            lock (_fileGate)
            {
                if (frame > _frameIndex)
                    _frameIndex = frame;

                if (blocked)
                    _compactionFlags = (byte)(_compactionFlags | MacroDatabaseCompactionFlags.PersistenceGate);
                else
                    _compactionFlags = (byte)(_compactionFlags & ~MacroDatabaseCompactionFlags.PersistenceGate);
            }
        }

        public void NotifyCriticalMemoryPressure(long reservedMemoryBytes, long physicalMemoryBytes, float usageRatio, uint frame, byte severity)
        {
            int resumeTick = unchecked(Environment.TickCount + MemoryPressurePauseMilliseconds);
            Volatile.Write(ref _compactionMemoryResumeTickMs, resumeTick);
            lock (_fileGate)
            {
                if (frame > _frameIndex)
                    _frameIndex = frame;

                _compactionFlags = (byte)(_compactionFlags | MacroDatabaseCompactionFlags.MemoryPressurePaused);
                if (_compactionState == (int)MacroDatabaseCompactionState.Copying)
                    _compactionState = (int)MacroDatabaseCompactionState.Paused;
                RecordBlackBox(0UL, (MacroDatabaseTier)_config.DefaultTier, severity);
            }
        }

        public void DumpBlackBox(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            lock (_fileGate)
            {
                if (!_blackBox.IsCreated)
                    return;

                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_blackBox);
                    int bytes = _blackBox.Length * UnsafeUtility.SizeOf<MacroDatabaseTelemetryEntry>();
                    using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        stream.Write(new ReadOnlySpan<byte>(source, bytes));
                    }
                }
                catch
                {
                }
            }
        }

        public void Shutdown()
        {
            Volatile.Write(ref _compactionActive, 0);
            while (Volatile.Read(ref _compactionCopyActive) != 0)
                Thread.Sleep(1);

            lock (_fileGate)
            {
                _compactionState = (int)MacroDatabaseCompactionState.Idle;
                FlushDirtyPayloadsLocked();
                CloseFileHandles();
                CleanupCompactionTemp(_path);
                if (_sectorWindowScratch.IsCreated)
                    _sectorWindowScratch.Dispose();
                if (_sectorCoordWindowScratch.IsCreated)
                    _sectorCoordWindowScratch.Dispose();
                if (_asyncHydrateScratch.IsCreated)
                    _asyncHydrateScratch.Dispose();
                DisposeBlackBox();
                if (_dirtyPayloads.IsCreated)
                    _dirtyPayloads.Dispose();
                if (_dirtyPayloadKeys.IsCreated)
                    _dirtyPayloadKeys.Dispose();
                if (_sectorCoordsByHash.IsCreated)
                    _sectorCoordsByHash.Dispose();

                _cacheOwner = null;
                _signalSink = null;
                _path = null;
                _blackBoxWriteIndex = 0;
                _pageFaults = 0;
                _pageFaultWindowStartTickMs = 0;
                _pageFaultWindowCount = 0;
                _hydratedSectors = 0;
                _evictedSectors = 0;
                _dirtyAppendCount = 0;
                _asyncHydrationActive = 0;
                _compactionCopyActive = 0;
                _frameIndex = 0u;
                _deadBytes = 0L;
                ResetCompactionStateLocked();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private bool CopyNodePayloadsTo(long nodeOffset, H8MacroDatabaseService target, bool respectCompactionPause = false)
        {
            if (respectCompactionPause)
            {
                WaitForCompactionResume();
                if (Volatile.Read(ref _compactionActive) == 0)
                    return false;
            }

            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return false;

            int keyCount = ReadNodeKeyCount(node);
            if ((uint)keyCount > H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            bool isLeaf = IsLeaf(node);
            for (int i = 0; i < keyCount; i++)
            {
                if (!isLeaf && !CopyNodePayloadsTo(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, i), target, respectCompactionPause))
                    return false;

                ulong sectorHash = H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, i);
                long payloadOffset = H8MacroDatabaseFileFormat.ReadNodeFileOffset(node, i);
                if (!TryReadPayloadPointer(payloadOffset, sectorHash, out byte* payload, out int payloadBytes, out byte flags))
                    continue;

                if (!target.AppendPayloadRaw(sectorHash, payload, payloadBytes, flags, out long newPayloadOffset) ||
                    !target.UpsertPayloadOffset(sectorHash, newPayloadOffset))
                {
                    return false;
                }
            }

            return isLeaf || CopyNodePayloadsTo(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, keyCount), target, respectCompactionPause);
        }

        internal bool CopyLivePayloadsToCompactTempThreadSafe(MacroDatabaseTier tier)
        {
            string tempPath;
            long rootOffset;
            lock (_fileGate)
            {
                if (!IsOpen ||
                    Volatile.Read(ref _compactionActive) == 0 ||
                    _compactionState != (int)MacroDatabaseCompactionState.Copying &&
                    _compactionState != (int)MacroDatabaseCompactionState.Paused ||
                    Volatile.Read(ref _compactionPersistenceGate) != 0)
                {
                    return false;
                }

                tempPath = _compactionTempPath;
                if (string.IsNullOrEmpty(tempPath))
                    return false;

                rootOffset = ReadRootNodeOffset();
                Volatile.Write(ref _compactionCopyActive, 1);
            }

            H8MacroDatabaseService target = new H8MacroDatabaseService();
            try
            {
                target._config = NormalizeConfig(_config);
                target.EnsureNativeState();
                if (!target.TryCreateEmptyFile(tempPath, _config.InitialFileBytes, false))
                    return false;

                WaitForCompactionResume();
                if (!CopyNodePayloadsTo(rootOffset, target, true))
                    return false;

                target.WriteDeadBytes(0L);
                if (!target.TruncateToAppendOffset())
                    return false;

                target.Flush();
                long tempBytes = target._mappedBytes;
                lock (_fileGate)
                {
                    _compactionTempBytes = tempBytes;
                    _compactionTier = tier;
                }

                return tempBytes > 0L;
            }
            catch
            {
                return false;
            }
            finally
            {
                Volatile.Write(ref _compactionCopyActive, 0);
                target.Shutdown();
            }
        }

        internal void MarkCompactionCopyCompleteThreadSafe(bool copied)
        {
            lock (_fileGate)
            {
                if (Volatile.Read(ref _compactionActive) == 0 &&
                    _compactionState == (int)MacroDatabaseCompactionState.Idle)
                {
                    return;
                }

                if (!copied ||
                    _compactionState != (int)MacroDatabaseCompactionState.Copying &&
                    _compactionState != (int)MacroDatabaseCompactionState.Paused)
                {
                    MarkCompactionFaultLocked();
                    return;
                }

                _compactionState = (int)MacroDatabaseCompactionState.ReadyToSwap;
                _compactionFlags = (byte)(_compactionFlags | MacroDatabaseCompactionFlags.TempReady);
            }
        }

        private bool FlushDirtyPayloadsIntoTargetLocked(H8MacroDatabaseService target, out int flushedDirtyPayloads)
        {
            flushedDirtyPayloads = 0;
            if (target == null || !target.IsOpen)
                return false;

            int dirtyCount = _dirtyPayloadKeys.IsCreated ? _dirtyPayloadKeys.Length : 0;
            for (int index = 0; index < dirtyCount; index++)
            {
                ulong sectorHash = _dirtyPayloadKeys[index];
                if (!_dirtyPayloads.TryGetValue(sectorHash, out MacroDatabasePayloadHandle dirty) ||
                    dirty.Pointer == IntPtr.Zero ||
                    dirty.ByteLength <= 0 ||
                    dirty.ByteLength > _config.MaxPayloadBytes)
                {
                    continue;
                }

                if (!target.AppendPayloadRaw(
                        sectorHash,
                        dirty.Pointer.ToPointer(),
                        dirty.ByteLength,
                        dirty.Flags,
                        out long newPayloadOffset) ||
                    !target.UpsertPayloadOffset(sectorHash, newPayloadOffset))
                {
                    return false;
                }

                flushedDirtyPayloads++;
            }

            return true;
        }

        private void MarkDirtyPayloadCacheCleanAfterSwapLocked()
        {
            if (_cacheOwner == null || !_dirtyPayloadKeys.IsCreated || _dirtyPayloadKeys.Length == 0)
                return;

            int dirtyCount = _dirtyPayloadKeys.Length;
            for (int index = 0; index < dirtyCount; index++)
            {
                ulong sectorHash = _dirtyPayloadKeys[index];
                if (!_dirtyPayloads.TryGetValue(sectorHash, out MacroDatabasePayloadHandle dirty) ||
                    dirty.Pointer == IntPtr.Zero ||
                    dirty.ByteLength <= 0 ||
                    dirty.ByteLength > _config.MaxPayloadBytes)
                {
                    _cacheOwner.TryRemoveMacroDatabasePayload(sectorHash, out _);
                    continue;
                }

                if (TryFindPayloadOffset(sectorHash, out long payloadOffset))
                    MarkPayloadCleanInCacheLocked(sectorHash, in dirty, payloadOffset);
                else
                    _cacheOwner.TryRemoveMacroDatabasePayload(sectorHash, out _);
            }
        }

        private void MarkPayloadCleanInCacheLocked(
            ulong sectorHash,
            in MacroDatabasePayloadHandle dirty,
            long payloadOffset)
        {
            if (_cacheOwner == null ||
                dirty.Pointer == IntPtr.Zero ||
                dirty.ByteLength <= 0 ||
                dirty.ByteLength > _config.MaxPayloadBytes)
            {
                return;
            }

            byte cleanFlags = (byte)(dirty.Flags & ~MacroDatabasePayloadFlags.Dirty);
            if (!_cacheOwner.TryStoreMacroDatabasePayload(
                    sectorHash,
                    dirty.Pointer,
                    dirty.ByteLength,
                    payloadOffset,
                    cleanFlags,
                    out _))
            {
                _cacheOwner.TryRemoveMacroDatabasePayload(sectorHash, out _);
            }
        }

        private void ClearDirtyPayloadQueueLocked()
        {
            if (_dirtyPayloads.IsCreated)
                _dirtyPayloads.Clear();
            if (_dirtyPayloadKeys.IsCreated)
                _dirtyPayloadKeys.Clear();
        }

        private bool TryMeasureLiveTreeLocked(long nodeOffset, ref long nodeBytes, ref long livePayloadBytes)
        {
            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return false;

            int keyCount = ReadNodeKeyCount(node);
            if ((uint)keyCount > H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            nodeBytes += H8MacroDatabaseFileFormat.NodeSizeBytes;
            bool isLeaf = IsLeaf(node);
            for (int i = 0; i < keyCount; i++)
            {
                if (!isLeaf && !TryMeasureLiveTreeLocked(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, i), ref nodeBytes, ref livePayloadBytes))
                    return false;

                ulong sectorHash = H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, i);
                long payloadOffset = H8MacroDatabaseFileFormat.ReadNodeFileOffset(node, i);
                if (TryReadPayloadRecordBytes(payloadOffset, sectorHash, out long payloadRecordBytes))
                    livePayloadBytes += payloadRecordBytes;
            }

            return isLeaf || TryMeasureLiveTreeLocked(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, keyCount), ref nodeBytes, ref livePayloadBytes);
        }

        private void ReconcileDeadBytesLocked()
        {
            long storedDeadBytes = ReadDeadBytes();
            long livePayloadBytes = 0L;
            long nodeBytes = 0L;
            long estimatedDeadBytes = 0L;
            if (IsOpen && TryMeasureLiveTreeLocked(ReadRootNodeOffset(), ref nodeBytes, ref livePayloadBytes))
            {
                long appendOffset = ReadAppendOffset();
                long occupiedBytes = H8MacroDatabaseFileFormat.HeaderSizeBytes + nodeBytes + livePayloadBytes;
                if (appendOffset > occupiedBytes)
                    estimatedDeadBytes = appendOffset - occupiedBytes;
            }

            _deadBytes = math.max(0L, math.max(storedDeadBytes, estimatedDeadBytes));
            WriteDeadBytes(_deadBytes);
        }

        private bool TryReadPayloadRecordBytes(long payloadOffset, ulong sectorHash, out long payloadRecordBytes)
        {
            payloadRecordBytes = 0L;
            if (!TryReadPayloadPointer(payloadOffset, sectorHash, out _, out int payloadBytes, out _))
                return false;

            payloadRecordBytes = H8MacroDatabaseFileFormat.AlignUp(
                H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes + (long)payloadBytes,
                16);
            return payloadRecordBytes > 0L;
        }

        private long ReadDeadBytes()
        {
            if (_basePointer == null)
                return 0L;

            long value = H8MacroDatabaseFileFormat.ReadLong(_basePointer, H8MacroDatabaseFileFormat.HeaderDeadBytesOffset);
            return value > 0L && value <= _mappedBytes ? value : 0L;
        }

        private void WriteDeadBytes(long value)
        {
            if (_basePointer != null)
                H8MacroDatabaseFileFormat.WriteLong(_basePointer, H8MacroDatabaseFileFormat.HeaderDeadBytesOffset, math.max(0L, value));
        }

        private void AddDeadBytesLocked(long bytes)
        {
            if (bytes <= 0L)
                return;

            long safeBytes = _deadBytes > long.MaxValue - bytes
                ? long.MaxValue
                : _deadBytes + bytes;
            _deadBytes = safeBytes;
            WriteDeadBytes(safeBytes);
        }

        private bool IsCompactionWriteLocked()
        {
            int state = _compactionState;
            return state == (int)MacroDatabaseCompactionState.Copying ||
                   state == (int)MacroDatabaseCompactionState.Paused ||
                   state == (int)MacroDatabaseCompactionState.ReadyToSwap ||
                   state == (int)MacroDatabaseCompactionState.Swapping;
        }

        private bool IsMemoryPressurePauseActive()
        {
            int resumeTick = Volatile.Read(ref _compactionMemoryResumeTickMs);
            if (resumeTick == 0)
                return false;

            int remaining = unchecked(resumeTick - Environment.TickCount);
            if (remaining > 0)
                return true;

            if (Interlocked.CompareExchange(ref _compactionMemoryResumeTickMs, 0, resumeTick) == resumeTick)
            {
                lock (_fileGate)
                {
                    _compactionFlags = (byte)(_compactionFlags & ~MacroDatabaseCompactionFlags.MemoryPressurePaused);
                    if (_compactionState == (int)MacroDatabaseCompactionState.Paused &&
                        Volatile.Read(ref _compactionActive) != 0)
                    {
                        _compactionState = (int)MacroDatabaseCompactionState.Copying;
                    }
                }
            }

            return false;
        }

        private void WaitForCompactionResume()
        {
            while (Volatile.Read(ref _compactionActive) != 0 &&
                   (IsMemoryPressurePauseActive() || Volatile.Read(ref _compactionPersistenceGate) != 0))
            {
                Thread.Sleep(16);
            }
        }

        private long ResolveCompactionThresholdBytes(MacroDatabaseTier tier)
        {
            return tier == MacroDatabaseTier.Low
                ? LowTierCompactionThresholdBytes
                : DefaultCompactionThresholdBytes;
        }

        private void MarkCompactionFaultLocked()
        {
            _compactionState = (int)MacroDatabaseCompactionState.Faulted;
            Volatile.Write(ref _compactionActive, 0);
            _compactionTempBytes = 0L;
            CleanupCompactionTemp(_path);
        }

        private void ResetCompactionStateLocked()
        {
            Volatile.Write(ref _compactionActive, 0);
            Volatile.Write(ref _compactionMemoryResumeTickMs, 0);
            Volatile.Write(ref _compactionPersistenceGate, 0);
            _compactionState = (int)MacroDatabaseCompactionState.Idle;
            _compactionTempBytes = 0L;
            _lastCompactionStallMicroseconds = 0L;
            _compactionTempPath = null;
            _compactionTier = (MacroDatabaseTier)_config.DefaultTier;
            _compactionFlags = 0;
        }

        private static string ResolveCompactionTempPath(string activePath)
        {
            if (string.IsNullOrEmpty(activePath))
                return null;

            string directory = Path.GetDirectoryName(activePath);
            return string.IsNullOrEmpty(directory)
                ? H8MacroDatabaseFileFormat.CompactionTempFileName
                : Path.Combine(directory, H8MacroDatabaseFileFormat.CompactionTempFileName);
        }

        private static void CleanupCompactionTemp(string activePath)
        {
            if (string.Equals(
                    Path.GetFileName(activePath),
                    H8MacroDatabaseFileFormat.CompactionTempFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string tempPath = ResolveCompactionTempPath(activePath);
            if (string.IsNullOrEmpty(tempPath))
                return;

            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }

        private bool TruncateToAppendOffset()
        {
            if (!IsOpen || _fileStream == null)
                return false;

            long appendOffset = ReadAppendOffset();
            if (appendOffset < MinimumFileBytes)
                return false;

            Flush();
            ReleaseMapOnly();
            _fileStream.SetLength(appendOffset);
            return MapFile(appendOffset);
        }

        private static int SaturateToInt(long value)
        {
            if (value <= 0L)
                return 0;

            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        internal bool TryBeginAsyncHydration(CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested &&
                   Interlocked.Exchange(ref _asyncHydrationActive, 1) == 0;
        }

        internal void EndAsyncHydration()
        {
            Interlocked.Exchange(ref _asyncHydrationActive, 0);
        }

        internal int StageHydrationCandidatesThreadSafe(
            in MacroDatabaseAup playerAup,
            MacroDatabaseTier tier,
            out ulong firstHash)
        {
            lock (_fileGate)
            {
                if (!IsOpen || !_asyncHydrateScratch.IsCreated)
                {
                    firstHash = 0UL;
                    return 0;
                }

                return StageHydrationCandidatesLocked(in playerAup, tier, out firstHash);
            }
        }

        internal int StoreHydrationCandidatesThreadSafe(int candidateCount, MacroDatabaseTier tier, ulong firstHash)
        {
            lock (_fileGate)
            {
                if (!IsOpen || _cacheOwner == null || !_asyncHydrateScratch.IsCreated)
                    return 0;

                int limit = candidateCount < _asyncHydrateScratch.Length
                    ? candidateCount
                    : _asyncHydrateScratch.Length;
                int hydrated = 0;
                for (int i = 0; i < limit; i++)
                {
                    HydrationCandidate candidate = _asyncHydrateScratch[i];
                    if (StoreHydrationCandidateLocked(in candidate, tier))
                        hydrated++;
                }

                _frameIndex++;
                RecordBlackBox(firstHash, tier, hydrated);
                return hydrated;
            }
        }

        private int StageHydrationCandidatesLocked(in MacroDatabaseAup playerAup, MacroDatabaseTier tier, out ulong firstHash)
        {
            firstHash = 0UL;
            if (!IsOpen || !_sectorWindowScratch.IsCreated || !_asyncHydrateScratch.IsCreated)
                return 0;

            int hashCount = BuildSectorHashWindowLocked(in playerAup, tier, _sectorWindowScratch, _sectorCoordWindowScratch);
            if (hashCount > 0)
                firstHash = _sectorWindowScratch[0];

            int limit = hashCount < _asyncHydrateScratch.Length ? hashCount : _asyncHydrateScratch.Length;
            int candidateCount = 0;
            for (int i = 0; i < limit; i++)
            {
                ulong sectorHash = _sectorWindowScratch[i];
                if (!TryFindPayloadOffset(sectorHash, out long payloadOffset) ||
                    !TryReadPayloadPointer(payloadOffset, sectorHash, out _, out int payloadBytes, out byte flags))
                {
                    continue;
                }

                _asyncHydrateScratch[candidateCount++] = new HydrationCandidate
                {
                    SectorHash = sectorHash,
                    PayloadOffset = payloadOffset,
                    Sector = _sectorCoordWindowScratch.IsCreated && i < _sectorCoordWindowScratch.Length
                        ? _sectorCoordWindowScratch[i]
                        : default,
                    PayloadBytes = payloadBytes,
                    Flags = flags
                };
            }

            return candidateCount;
        }

        private bool StoreHydrationCandidateLocked(in HydrationCandidate candidate, MacroDatabaseTier tier)
        {
            if (candidate.SectorHash == 0UL ||
                _cacheOwner == null ||
                _cacheOwner.TryGetMacroDatabasePayload(candidate.SectorHash, out _))
            {
                return false;
            }

            if (!TryReadPayloadPointer(
                    candidate.PayloadOffset,
                    candidate.SectorHash,
                    out byte* payloadPointer,
                    out int payloadBytes,
                    out byte flags))
            {
                return false;
            }

            RecordPageFaultLocked(tier);
            if (!_cacheOwner.TryStoreMacroDatabasePayload(
                    candidate.SectorHash,
                    (IntPtr)payloadPointer,
                    payloadBytes,
                    candidate.PayloadOffset,
                    flags,
                    out _))
            {
                return false;
            }

            _hydratedSectors++;
            CacheSectorCoord(candidate.SectorHash, candidate.Sector);
            PublishHydrated(candidate.SectorHash, candidate.PayloadOffset, payloadBytes, tier, flags);
            return true;
        }

        private bool AppendPayloadRaw(ulong sectorHash, void* payloadPointer, int payloadBytes, byte flags, out long payloadOffset)
        {
            payloadOffset = 0L;
            if (!IsOpen || payloadPointer == null || payloadBytes <= 0 || payloadBytes > _config.MaxPayloadBytes)
                return false;

            long appendOffset = H8MacroDatabaseFileFormat.AlignUp(
                ReadAppendOffset(),
                H8MacroDatabaseFileFormat.PayloadAlignmentBytes);
            if (appendOffset < 0L ||
                appendOffset > long.MaxValue - H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes ||
                appendOffset + H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes > long.MaxValue - payloadBytes)
            {
                return false;
            }

            long endOffset = appendOffset + H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes + payloadBytes;
            if (_config.MaxFileBytes > 0L && endOffset > _config.MaxFileBytes)
                return false;

            if (!EnsureMappedLength(endOffset))
                return false;

            byte* header = stackalloc byte[H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes];
            UnsafeUtility.MemClear(header, H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes);
            H8MacroDatabaseFileFormat.WriteUInt(header, H8MacroDatabaseFileFormat.PayloadMagicOffset, H8MacroDatabaseFileFormat.PayloadMagic);
            H8MacroDatabaseFileFormat.WriteULong(header, H8MacroDatabaseFileFormat.PayloadHashOffset, sectorHash);
            H8MacroDatabaseFileFormat.WriteInt(header, H8MacroDatabaseFileFormat.PayloadBytesOffset, payloadBytes);
            H8MacroDatabaseFileFormat.WriteUInt(header, H8MacroDatabaseFileFormat.PayloadVersionOffset, unchecked(_frameIndex + 1u));
            H8MacroDatabaseFileFormat.WriteByte(header, H8MacroDatabaseFileFormat.PayloadFlagsOffset, (byte)(flags & ~MacroDatabasePayloadFlags.Dirty));

            byte* destination = _basePointer + appendOffset;
            UnsafeUtility.MemCpy(destination, header, H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes);
            UnsafeUtility.MemCpy(destination + H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes, payloadPointer, payloadBytes);
            payloadOffset = appendOffset;
            WriteAppendOffset(H8MacroDatabaseFileFormat.AlignUp(
                endOffset,
                H8MacroDatabaseFileFormat.PayloadAlignmentBytes));
            return true;
        }

        private bool TryFindPayloadOffset(ulong sectorHash, out long payloadOffset)
        {
            payloadOffset = 0L;
            if (!IsOpen)
                return false;

            long nodeOffset = ReadRootNodeOffset();
            while (nodeOffset > 0L)
            {
                byte* node = NodeAt(nodeOffset);
                if (node == null)
                    return false;

                int keyCount = ReadNodeKeyCount(node);
                if ((uint)keyCount > H8MacroDatabaseFileFormat.NodeMaxKeys)
                    return false;

                int index = FindFirstGreaterOrEqual(node, keyCount, sectorHash);

                if (index < keyCount && sectorHash == H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
                {
                    payloadOffset = H8MacroDatabaseFileFormat.ReadNodeFileOffset(node, index);
                    return payloadOffset > 0L;
                }

                if (IsLeaf(node))
                    return false;

                nodeOffset = H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, index);
            }

            return false;
        }

        private bool TryReadPayloadPointer(long payloadOffset, ulong sectorHash, out byte* payloadPointer, out int payloadBytes, out byte flags)
        {
            payloadPointer = null;
            payloadBytes = 0;
            flags = 0;
            if (!IsOpen ||
                payloadOffset < H8MacroDatabaseFileFormat.HeaderSizeBytes ||
                (payloadOffset & (H8MacroDatabaseFileFormat.PayloadAlignmentBytes - 1L)) != 0L ||
                payloadOffset > _mappedBytes - H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes)
            {
                return false;
            }

            byte* header = _basePointer + payloadOffset;
            if (H8MacroDatabaseFileFormat.ReadUInt(header, H8MacroDatabaseFileFormat.PayloadMagicOffset) != H8MacroDatabaseFileFormat.PayloadMagic ||
                H8MacroDatabaseFileFormat.ReadULong(header, H8MacroDatabaseFileFormat.PayloadHashOffset) != sectorHash)
            {
                return false;
            }

            payloadBytes = H8MacroDatabaseFileFormat.ReadInt(header, H8MacroDatabaseFileFormat.PayloadBytesOffset);
            long payloadStart = payloadOffset + H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes;
            if (payloadBytes <= 0 ||
                payloadBytes > _config.MaxPayloadBytes ||
                payloadStart > _mappedBytes - payloadBytes)
            {
                return false;
            }

            flags = H8MacroDatabaseFileFormat.ReadByte(header, H8MacroDatabaseFileFormat.PayloadFlagsOffset);
            payloadPointer = _basePointer + payloadStart;
            return true;
        }

        private bool UpsertPayloadOffset(ulong sectorHash, long payloadOffset)
        {
            if (!IsOpen)
                return false;

            if (TryUpdatePayloadOffset(ReadRootNodeOffset(), sectorHash, payloadOffset))
                return true;

            long rootOffset = ReadRootNodeOffset();
            byte* root = NodeAt(rootOffset);
            if (root == null)
                return false;

            if (ReadNodeKeyCount(root) == H8MacroDatabaseFileFormat.NodeMaxKeys)
            {
                long newRootOffset = AllocateNode(false);
                if (newRootOffset <= 0L)
                    return false;

                byte* newRoot = NodeAt(newRootOffset);
                H8MacroDatabaseFileFormat.WriteNodeChildOffset(newRoot, 0, rootOffset);
                if (!SplitChild(newRootOffset, 0, rootOffset))
                    return false;

                WriteRootNodeOffset(newRootOffset);
                return InsertNonFull(newRootOffset, sectorHash, payloadOffset);
            }

            return InsertNonFull(rootOffset, sectorHash, payloadOffset);
        }

        private bool TryUpdatePayloadOffset(long nodeOffset, ulong sectorHash, long payloadOffset)
        {
            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return false;

            int keyCount = ReadNodeKeyCount(node);
            if ((uint)keyCount > H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            int index = FindFirstGreaterOrEqual(node, keyCount, sectorHash);

            if (index < keyCount && sectorHash == H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
            {
                H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, index, payloadOffset);
                return true;
            }

            return !IsLeaf(node) && TryUpdatePayloadOffset(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, index), sectorHash, payloadOffset);
        }

        private bool InsertNonFull(long nodeOffset, ulong sectorHash, long payloadOffset)
        {
            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return false;

            int keyCount = ReadNodeKeyCount(node);
            if ((uint)keyCount >= H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            int index = keyCount - 1;
            if (IsLeaf(node))
            {
                int insertIndex = FindFirstGreaterOrEqual(node, keyCount, sectorHash);
                if (insertIndex < keyCount && sectorHash == H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, insertIndex))
                {
                    H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, insertIndex, payloadOffset);
                    return true;
                }

                while (index >= insertIndex)
                {
                    H8MacroDatabaseFileFormat.WriteNodeSectorHash(node, index + 1, H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index));
                    H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, index + 1, H8MacroDatabaseFileFormat.ReadNodeFileOffset(node, index));
                    index--;
                }

                H8MacroDatabaseFileFormat.WriteNodeSectorHash(node, insertIndex, sectorHash);
                H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, insertIndex, payloadOffset);
                WriteNodeKeyCount(node, keyCount + 1);
                return true;
            }

            index = FindFirstGreaterOrEqual(node, keyCount, sectorHash);

            long childOffset = H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, index);
            byte* child = NodeAt(childOffset);
            if (child == null)
                return false;

            if (ReadNodeKeyCount(child) == H8MacroDatabaseFileFormat.NodeMaxKeys)
            {
                if (!SplitChild(nodeOffset, index, childOffset))
                    return false;

                node = NodeAt(nodeOffset);
                ulong promoted = H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index);
                if (sectorHash > promoted)
                    index++;
                else if (sectorHash == promoted)
                {
                    H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, index, payloadOffset);
                    return true;
                }
            }

            node = NodeAt(nodeOffset);
            return node != null &&
                   InsertNonFull(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, index), sectorHash, payloadOffset);
        }

        private bool SplitChild(long parentOffset, int childIndex, long childOffset)
        {
            byte* childProbe = NodeAt(childOffset);
            if (childProbe == null)
                return false;

            bool childIsLeaf = IsLeaf(childProbe);
            long newChildOffset = AllocateNode(childIsLeaf);
            if (newChildOffset <= 0L)
                return false;

            byte* parent = NodeAt(parentOffset);
            byte* child = NodeAt(childOffset);
            byte* newChild = NodeAt(newChildOffset);
            if (parent == null || child == null || newChild == null)
                return false;

            int t = H8MacroDatabaseFileFormat.NodeMinDegree;
            if (ReadNodeKeyCount(child) != H8MacroDatabaseFileFormat.NodeMaxKeys ||
                (uint)childIndex > H8MacroDatabaseFileFormat.NodeMaxKeys ||
                ReadNodeKeyCount(parent) >= H8MacroDatabaseFileFormat.NodeMaxKeys)
            {
                return false;
            }

            int newKeyCount = t - 1;
            for (int j = 0; j < newKeyCount; j++)
            {
                H8MacroDatabaseFileFormat.WriteNodeSectorHash(newChild, j, H8MacroDatabaseFileFormat.ReadNodeSectorHash(child, j + t));
                H8MacroDatabaseFileFormat.WriteNodeFileOffset(newChild, j, H8MacroDatabaseFileFormat.ReadNodeFileOffset(child, j + t));
            }

            if (!childIsLeaf)
            {
                for (int j = 0; j < t; j++)
                    H8MacroDatabaseFileFormat.WriteNodeChildOffset(newChild, j, H8MacroDatabaseFileFormat.ReadNodeChildOffset(child, j + t));
            }
            else
            {
                H8MacroDatabaseFileFormat.WriteLong(newChild, H8MacroDatabaseFileFormat.NodeNextLeafOffset, H8MacroDatabaseFileFormat.ReadLong(child, H8MacroDatabaseFileFormat.NodeNextLeafOffset));
                H8MacroDatabaseFileFormat.WriteLong(child, H8MacroDatabaseFileFormat.NodeNextLeafOffset, newChildOffset);
            }

            ulong medianHash = H8MacroDatabaseFileFormat.ReadNodeSectorHash(child, t - 1);
            long medianPayload = H8MacroDatabaseFileFormat.ReadNodeFileOffset(child, t - 1);
            WriteNodeKeyCount(child, newKeyCount);
            WriteNodeKeyCount(newChild, newKeyCount);

            int parentKeyCount = ReadNodeKeyCount(parent);
            for (int j = parentKeyCount; j >= childIndex + 1; j--)
                H8MacroDatabaseFileFormat.WriteNodeChildOffset(parent, j + 1, H8MacroDatabaseFileFormat.ReadNodeChildOffset(parent, j));

            H8MacroDatabaseFileFormat.WriteNodeChildOffset(parent, childIndex + 1, newChildOffset);

            for (int j = parentKeyCount - 1; j >= childIndex; j--)
            {
                H8MacroDatabaseFileFormat.WriteNodeSectorHash(parent, j + 1, H8MacroDatabaseFileFormat.ReadNodeSectorHash(parent, j));
                H8MacroDatabaseFileFormat.WriteNodeFileOffset(parent, j + 1, H8MacroDatabaseFileFormat.ReadNodeFileOffset(parent, j));
            }

            H8MacroDatabaseFileFormat.WriteNodeSectorHash(parent, childIndex, medianHash);
            H8MacroDatabaseFileFormat.WriteNodeFileOffset(parent, childIndex, medianPayload);
            WriteNodeKeyCount(parent, parentKeyCount + 1);
            return true;
        }

        private long AllocateNode(bool isLeaf)
        {
            long nodeOffset = H8MacroDatabaseFileFormat.AlignUp(ReadAppendOffset(), H8MacroDatabaseFileFormat.NodeSizeBytes);
            if (nodeOffset < 0L || nodeOffset > long.MaxValue - H8MacroDatabaseFileFormat.NodeSizeBytes)
                return 0L;

            long endOffset = nodeOffset + H8MacroDatabaseFileFormat.NodeSizeBytes;
            if (_config.MaxFileBytes > 0L && endOffset > _config.MaxFileBytes)
                return 0L;

            if (!EnsureMappedLength(endOffset))
                return 0L;

            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return 0L;

            ClearNode(node, isLeaf);
            WriteAppendOffset(endOffset);
            return nodeOffset;
        }

        private void ClearNode(byte* node, bool isLeaf)
        {
            UnsafeUtility.MemClear(node, H8MacroDatabaseFileFormat.NodeSizeBytes);
            WriteNodeKeyCount(node, 0);
            H8MacroDatabaseFileFormat.WriteByte(node, H8MacroDatabaseFileFormat.NodeIsLeafOffset, isLeaf ? (byte)1 : (byte)0);
        }

        private byte* NodeAt(long nodeOffset)
        {
            if (_basePointer == null ||
                nodeOffset < H8MacroDatabaseFileFormat.HeaderSizeBytes ||
                nodeOffset > _mappedBytes - H8MacroDatabaseFileFormat.NodeSizeBytes)
            {
                return null;
            }

            return _basePointer + nodeOffset;
        }

        private bool MapFile(long length)
        {
            if (_fileStream == null || length < MinimumFileBytes)
                return false;

#if !HECTON8_MMF_AVAILABLE
            _basePointer = null;
            _mappedBytes = 0L;
            return false;
#else
            ReleaseMapOnly();
            _mappedFile = MemoryMappedFile.CreateFromFile(
                _fileStream,
                null,
                length,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                true);
            _viewAccessor = _mappedFile.CreateViewAccessor(0L, length, MemoryMappedFileAccess.ReadWrite);
            _viewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
            _mappedBytes = length;
            return _basePointer != null;
#endif
        }

        private bool EnsureMappedLength(long requiredBytes)
        {
            if (requiredBytes < MinimumFileBytes)
                return false;

            if (requiredBytes <= _mappedBytes)
                return true;

            if (_fileStream == null)
                return false;

            long targetBytes = H8MacroDatabaseFileFormat.AlignUp(requiredBytes, H8MacroDatabaseFileFormat.NodeSizeBytes);
            if (targetBytes < requiredBytes)
                return false;

            if (_config.MaxFileBytes > 0L && targetBytes > _config.MaxFileBytes)
                return false;

            Flush();
            ReleaseMapOnly();
            _fileStream.SetLength(targetBytes);
            return MapFile(targetBytes);
        }

        private bool ValidateHeader()
        {
            if (_basePointer == null ||
                H8MacroDatabaseFileFormat.ReadUInt(_basePointer, H8MacroDatabaseFileFormat.HeaderMagicOffset) != H8MacroDatabaseFileFormat.FileMagic ||
                H8MacroDatabaseFileFormat.ReadInt(_basePointer, H8MacroDatabaseFileFormat.HeaderVersionOffset) != H8MacroDatabaseFileFormat.Version ||
                H8MacroDatabaseFileFormat.ReadInt(_basePointer, H8MacroDatabaseFileFormat.HeaderSizeOffset) != H8MacroDatabaseFileFormat.HeaderSizeBytes ||
                H8MacroDatabaseFileFormat.ReadInt(_basePointer, H8MacroDatabaseFileFormat.HeaderNodeSizeOffset) != H8MacroDatabaseFileFormat.NodeSizeBytes)
            {
                return false;
            }

            int sectorSize = H8MacroDatabaseFileFormat.ReadInt(_basePointer, H8MacroDatabaseFileFormat.HeaderSectorSizeOffset);
            long rootOffset = ReadRootNodeOffset();
            long appendOffset = ReadAppendOffset();
            if (sectorSize <= 0 ||
                rootOffset < H8MacroDatabaseFileFormat.HeaderSizeBytes ||
                rootOffset > _mappedBytes - H8MacroDatabaseFileFormat.NodeSizeBytes ||
                ((rootOffset - H8MacroDatabaseFileFormat.HeaderSizeBytes) & (H8MacroDatabaseFileFormat.NodeSizeBytes - 1L)) != 0L ||
                appendOffset < MinimumFileBytes ||
                appendOffset > _mappedBytes)
            {
                return false;
            }

            byte* root = NodeAt(rootOffset);
            if (root == null || ReadNodeKeyCount(root) > H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            _config.SectorSizeMeters = sectorSize;
            _sectorSizeRcp = 1.0d / sectorSize;
            return true;
        }

        private void WriteEmptyHeader()
        {
            UnsafeUtility.MemClear(_basePointer, H8MacroDatabaseFileFormat.HeaderSizeBytes);
            H8MacroDatabaseFileFormat.WriteUInt(_basePointer, H8MacroDatabaseFileFormat.HeaderMagicOffset, H8MacroDatabaseFileFormat.FileMagic);
            H8MacroDatabaseFileFormat.WriteInt(_basePointer, H8MacroDatabaseFileFormat.HeaderVersionOffset, H8MacroDatabaseFileFormat.Version);
            H8MacroDatabaseFileFormat.WriteInt(_basePointer, H8MacroDatabaseFileFormat.HeaderSizeOffset, H8MacroDatabaseFileFormat.HeaderSizeBytes);
            H8MacroDatabaseFileFormat.WriteInt(_basePointer, H8MacroDatabaseFileFormat.HeaderNodeSizeOffset, H8MacroDatabaseFileFormat.NodeSizeBytes);
            WriteRootNodeOffset(H8MacroDatabaseFileFormat.HeaderSizeBytes);
            WriteAppendOffset(MinimumFileBytes);
            H8MacroDatabaseFileFormat.WriteInt(_basePointer, H8MacroDatabaseFileFormat.HeaderSectorSizeOffset, math.max(1, _config.SectorSizeMeters));
        }

        private long ReadRootNodeOffsetIfOpen()
        {
            return IsOpen ? ReadRootNodeOffset() : 0L;
        }

        private long ReadRootNodeOffset()
        {
            return H8MacroDatabaseFileFormat.ReadLong(_basePointer, H8MacroDatabaseFileFormat.HeaderRootNodeOffset);
        }

        private void WriteRootNodeOffset(long value)
        {
            H8MacroDatabaseFileFormat.WriteLong(_basePointer, H8MacroDatabaseFileFormat.HeaderRootNodeOffset, value);
        }

        private long ReadAppendOffset()
        {
            return H8MacroDatabaseFileFormat.ReadLong(_basePointer, H8MacroDatabaseFileFormat.HeaderAppendOffset);
        }

        private void WriteAppendOffset(long value)
        {
            H8MacroDatabaseFileFormat.WriteLong(_basePointer, H8MacroDatabaseFileFormat.HeaderAppendOffset, value);
        }

        private static int ReadNodeKeyCount(byte* node)
        {
            return H8MacroDatabaseFileFormat.ReadUShort(node, H8MacroDatabaseFileFormat.NodeKeyCountOffset);
        }

        private static int FindFirstGreaterOrEqual(byte* node, int keyCount, ulong sectorHash)
        {
            int low = 0;
            int high = keyCount;
            while (low < high)
            {
                int middle = (low + high) >> 1;
                ulong middleHash = H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, middle);
                if (middleHash < sectorHash)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private static void WriteNodeKeyCount(byte* node, int value)
        {
            H8MacroDatabaseFileFormat.WriteUShort(node, H8MacroDatabaseFileFormat.NodeKeyCountOffset, (ushort)value);
        }

        private static bool IsLeaf(byte* node)
        {
            return H8MacroDatabaseFileFormat.ReadByte(node, H8MacroDatabaseFileFormat.NodeIsLeafOffset) != 0;
        }

        private void PublishHydrated(ulong sectorHash, long payloadOffset, int payloadBytes, MacroDatabaseTier tier, byte flags)
        {
            if (_signalSink == null)
                return;

            SectorHydratedSignal signal = new SectorHydratedSignal
            {
                SectorHash = sectorHash,
                FileOffset = payloadOffset,
                PayloadBytes = payloadBytes,
                FrameIndex = _frameIndex,
                SourceTier = (byte)tier,
                Flags = flags
            };
            _signalSink.PublishSectorHydrated(in signal);
        }

        private void RecordBlackBox(ulong playerSectorHash, MacroDatabaseTier tier, int hydratedThisCall)
        {
            if (!_blackBox.IsCreated)
                return;

            MacroDatabaseNativeCacheStats cacheStats = _cacheOwner != null
                ? _cacheOwner.GetMacroDatabaseCacheStats()
                : default;

            _blackBox[_blackBoxWriteIndex] = new MacroDatabaseTelemetryEntry
            {
                PlayerSectorHash = playerSectorHash,
                RootNodeOffset = IsOpen ? ReadRootNodeOffset() : 0L,
                CacheBytes = cacheStats.Bytes,
                DeadBytes = _deadBytes,
                CacheEntries = cacheStats.Entries,
                PageFaults = hydratedThisCall,
                PageFaultsTotal = _pageFaults,
                HydratedSectors = _hydratedSectors,
                EvictedSectors = _evictedSectors,
                LastCompactionStallMicroseconds = SaturateToInt(_lastCompactionStallMicroseconds),
                FrameIndex = _frameIndex,
                Tier = (byte)tier,
                CompactionState = (byte)_compactionState,
                Flags = (byte)((IsOpen ? 1 : 0) | (_compactionFlags << 1))
            };
            _blackBoxWriteIndex++;
            if (_blackBoxWriteIndex >= _blackBox.Length)
                _blackBoxWriteIndex = 0;
        }

        private int ResolveRadiusMeters(MacroDatabaseTier tier)
        {
            switch (tier)
            {
                case MacroDatabaseTier.Low:
                    return math.max(1, _config.LowTierRadiusMeters);
                case MacroDatabaseTier.High:
                    return math.max(1, _config.HighTierRadiusMeters);
                case MacroDatabaseTier.Ultra:
                    return math.max(1, _config.UltraTierRadiusMeters);
                default:
                    return math.max(1, _config.MiddleTierRadiusMeters);
            }
        }

        private void RecordPageFaultLocked(MacroDatabaseTier tier)
        {
            _pageFaults++;
            int now = Environment.TickCount;
            if (_pageFaultWindowStartTickMs == 0)
            {
                _pageFaultWindowStartTickMs = now;
                _pageFaultWindowCount = 1;
                return;
            }

            int elapsedMs = unchecked(now - _pageFaultWindowStartTickMs);
            if (elapsedMs < 1000)
            {
                _pageFaultWindowCount++;
                return;
            }

            if (_pageFaultWindowCount > 2)
                IncreaseHydrationRadiusLocked(tier);

            _pageFaultWindowStartTickMs = now;
            _pageFaultWindowCount = 1;
        }

        private void IncreaseHydrationRadiusLocked(MacroDatabaseTier tier)
        {
            int stepMeters = math.max(1, _config.SectorSizeMeters);
            switch (tier)
            {
                case MacroDatabaseTier.Low:
                    _config.LowTierRadiusMeters = SafeIncreaseRadius(_config.LowTierRadiusMeters, stepMeters);
                    _config.MiddleTierRadiusMeters = math.max(_config.MiddleTierRadiusMeters, _config.LowTierRadiusMeters);
                    _config.HighTierRadiusMeters = math.max(_config.HighTierRadiusMeters, _config.MiddleTierRadiusMeters);
                    _config.UltraTierRadiusMeters = math.max(_config.UltraTierRadiusMeters, _config.HighTierRadiusMeters);
                    break;
                case MacroDatabaseTier.High:
                    _config.HighTierRadiusMeters = SafeIncreaseRadius(_config.HighTierRadiusMeters, stepMeters);
                    _config.UltraTierRadiusMeters = math.max(_config.UltraTierRadiusMeters, _config.HighTierRadiusMeters);
                    break;
                case MacroDatabaseTier.Ultra:
                    _config.UltraTierRadiusMeters = SafeIncreaseRadius(_config.UltraTierRadiusMeters, stepMeters);
                    break;
                default:
                    _config.MiddleTierRadiusMeters = SafeIncreaseRadius(_config.MiddleTierRadiusMeters, stepMeters);
                    _config.HighTierRadiusMeters = math.max(_config.HighTierRadiusMeters, _config.MiddleTierRadiusMeters);
                    _config.UltraTierRadiusMeters = math.max(_config.UltraTierRadiusMeters, _config.HighTierRadiusMeters);
                    break;
            }

            _config.DehydrateRadiusMeters = math.max(_config.DehydrateRadiusMeters, ResolveRadiusMeters(tier) + stepMeters);
        }

        private static int SafeIncreaseRadius(int radiusMeters, int stepMeters)
        {
            if (radiusMeters > int.MaxValue - stepMeters)
                return int.MaxValue;

            return radiusMeters + stepMeters;
        }

        private SectorCoord64 ResolveSectorCoord(in MacroDatabaseAup aup)
        {
            double3 absolute = aup.ToAbsoluteDouble3();
            return new SectorCoord64(
                (long)math.floor(absolute.x * _sectorSizeRcp),
                (long)math.floor(absolute.y * _sectorSizeRcp),
                (long)math.floor(absolute.z * _sectorSizeRcp));
        }

        private void CacheSectorCoord(ulong sectorHash, SectorCoord64 sector)
        {
            if (!_sectorCoordsByHash.IsCreated)
                return;

            if (_sectorCoordsByHash.ContainsKey(sectorHash))
                _sectorCoordsByHash[sectorHash] = sector;
            else
                _sectorCoordsByHash.TryAdd(sectorHash, sector);
        }

        private static ulong ComputeSectorHash(SectorCoord64 sector, int sectorSize)
        {
            ulong hash = 1469598103934665603UL;
            hash = MixHash(hash, unchecked((ulong)sector.X));
            hash = MixHash(hash, unchecked((ulong)sector.Y));
            hash = MixHash(hash, unchecked((ulong)sector.Z));
            hash = MixHash(hash, unchecked((ulong)sectorSize));
            return hash == 0UL ? 1UL : hash;
        }

        private static ulong MixHash(ulong hash, ulong value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        private void EnsureNativeState()
        {
            _config = NormalizeConfig(_config);
            _sectorSizeRcp = 1.0d / math.max(1, _config.SectorSizeMeters);
            if (!_sectorWindowScratch.IsCreated)
            {
                _sectorWindowScratch = new NativeArray<ulong>(
                    math.max(1, _config.MaxQuerySectors),
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_sectorCoordWindowScratch.IsCreated)
            {
                _sectorCoordWindowScratch = new NativeArray<SectorCoord64>(
                    math.max(1, _config.MaxQuerySectors),
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_asyncHydrateScratch.IsCreated)
            {
                _asyncHydrateScratch = new NativeArray<HydrationCandidate>(
                    math.max(1, _config.MaxQuerySectors),
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_blackBox.IsCreated)
            {
                _blackBox = new NativeArray<MacroDatabaseTelemetryEntry>(
                    BlackBoxFrameCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemoryTrackingBridge.RegisterNativeArray(
                    _blackBox,
                    nameof(H8MacroDatabaseService),
                    nameof(_blackBox),
                    NativeMemoryBridgeLifetime.Session);
            }

            if (!_dirtyPayloads.IsCreated)
            {
                _dirtyPayloads = new NativeParallelHashMap<ulong, MacroDatabasePayloadHandle>(
                    math.max(1, _config.NativeCacheCapacity),
                    Allocator.Persistent);
            }

            if (!_dirtyPayloadKeys.IsCreated)
            {
                _dirtyPayloadKeys = new NativeList<ulong>(
                    math.max(1, _config.NativeCacheCapacity),
                    Allocator.Persistent);
            }

            if (!_sectorCoordsByHash.IsCreated)
            {
                _sectorCoordsByHash = new NativeParallelHashMap<ulong, SectorCoord64>(
                    math.max(_config.NativeCacheCapacity * 2, _config.MaxQuerySectors),
                    Allocator.Persistent);
            }
        }

        private void DisposeBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            NativeMemoryTrackingBridge.UnregisterNativeArray(_blackBox, nameof(H8MacroDatabaseService), nameof(_blackBox));
            _blackBox.Dispose();
            _blackBox = default;
        }

        private int FlushDirtyPayloadsLocked()
        {
            if (!IsOpen || !_dirtyPayloadKeys.IsCreated || _dirtyPayloadKeys.Length == 0)
                return 0;

            int flushed = 0;
            int index = 0;
            while (index < _dirtyPayloadKeys.Length)
            {
                ulong sectorHash = _dirtyPayloadKeys[index];
                if (TryAppendDirtyPayloadLocked(sectorHash))
                {
                    flushed++;
                    continue;
                }

                index++;
            }

            if (flushed > 0)
                Flush();

            return flushed;
        }

        private void RemoveDirtyPayloadKey(ulong sectorHash)
        {
            if (!_dirtyPayloadKeys.IsCreated)
                return;

            for (int i = 0; i < _dirtyPayloadKeys.Length; i++)
            {
                if (_dirtyPayloadKeys[i] != sectorHash)
                    continue;

                _dirtyPayloadKeys.RemoveAtSwapBack(i);
                return;
            }
        }

        private void RemoveSectorCoord(ulong sectorHash)
        {
            if (_sectorCoordsByHash.IsCreated)
                _sectorCoordsByHash.Remove(sectorHash);
        }

        private static MacroDatabaseConfig NormalizeConfig(MacroDatabaseConfig config)
        {
            if (config.NodeSizeBytes == 0)
                config = MacroDatabaseConfig.Default;

            config.NodeSizeBytes = H8MacroDatabaseFileFormat.NodeSizeBytes;
            config.SectorSizeMeters = math.max(1, config.SectorSizeMeters);
            config.LowTierRadiusMeters = math.max(1, config.LowTierRadiusMeters);
            config.MiddleTierRadiusMeters = math.max(config.LowTierRadiusMeters, config.MiddleTierRadiusMeters);
            config.HighTierRadiusMeters = math.max(config.MiddleTierRadiusMeters, config.HighTierRadiusMeters);
            config.UltraTierRadiusMeters = math.max(config.HighTierRadiusMeters, config.UltraTierRadiusMeters);
            config.DehydrateRadiusMeters = math.max(config.MiddleTierRadiusMeters, config.DehydrateRadiusMeters);
            config.MaxPayloadBytes = math.max(256, config.MaxPayloadBytes);
            config.NativeCacheCapacity = math.max(16, config.NativeCacheCapacity);
            config.MaxQuerySectors = math.max(64, config.MaxQuerySectors);
            config.InitialFileBytes = math.max(MinimumFileBytes, config.InitialFileBytes);
            if (config.MaxFileBytes > 0L)
                config.MaxFileBytes = math.max(config.InitialFileBytes, config.MaxFileBytes);
            return config;
        }

        private static bool IsValidDatabasePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(H8MacroDatabaseFileFormat.Extension, StringComparison.OrdinalIgnoreCase);
        }

        private void Flush()
        {
#if HECTON8_MMF_AVAILABLE
            _viewAccessor?.Flush();
#endif
            _fileStream?.Flush(false);
        }

        private void CloseFileHandles()
        {
            Flush();
            ReleaseMapOnly();
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            _mappedBytes = 0L;
        }

        private void ReleaseMapOnly()
        {
#if HECTON8_MMF_AVAILABLE
            if (_viewAccessor != null)
            {
                if (_basePointer != null)
                {
                    _viewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _basePointer = null;
                }

                _viewAccessor.Dispose();
                _viewAccessor = null;
            }

            if (_mappedFile != null)
            {
                _mappedFile.Dispose();
                _mappedFile = null;
            }
#else
            _basePointer = null;
#endif
        }

    }

    internal static class H8MacroDatabaseAsyncHydration
    {
        internal static async Awaitable<int> RunAsync(
            H8MacroDatabaseService service,
            MacroDatabaseAup playerAup,
            MacroDatabaseTier tier,
            CancellationToken cancellationToken)
        {
            if (service == null || !service.TryBeginAsyncHydration(cancellationToken))
                return 0;

            int candidateCount = 0;
            ulong firstHash = 0UL;
            bool cancelledOnWorker = false;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                cancelledOnWorker = cancellationToken.IsCancellationRequested;
                if (!cancelledOnWorker)
                    candidateCount = service.StageHydrationCandidatesThreadSafe(in playerAup, tier, out firstHash);

                await Awaitable.MainThreadAsync();
                if (cancelledOnWorker || cancellationToken.IsCancellationRequested)
                    return 0;

                return service.StoreHydrationCandidatesThreadSafe(candidateCount, tier, firstHash);
            }
            finally
            {
                service.EndAsyncHydration();
            }
        }
    }

    internal static class H8MacroDatabaseCompaction
    {
        internal static async Awaitable RunAsync(H8MacroDatabaseService service, MacroDatabaseTier tier)
        {
            if (service == null)
                return;

            bool copied = false;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                copied = service.CopyLivePayloadsToCompactTempThreadSafe(tier);
                await Awaitable.MainThreadAsync();
            }
            catch
            {
                try
                {
                    await Awaitable.MainThreadAsync();
                }
                catch
                {
                }
            }

            service.MarkCompactionCopyCompleteThreadSafe(copied);
        }
    }
}
