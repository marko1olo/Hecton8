using System;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        private const byte PayloadDirtyFlag = 1 << 0;
        private const int BlackBoxFrameCount = 300;
        private const long MinimumFileBytes = H8MacroDatabaseFileFormat.HeaderSizeBytes + H8MacroDatabaseFileFormat.NodeSizeBytes;

        private readonly object _fileGate = new object(); // COLD ALLOC: Object[1] — guards MMF pointer remaps against background hydration — owner: H8MacroDatabaseService
        private MemoryMappedFile _mappedFile;
        private MemoryMappedViewAccessor _viewAccessor;
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
        private int _hydratedSectors;
        private int _evictedSectors;
        private int _dirtyAppendCount;
        private int _asyncHydrationActive;
        private uint _frameIndex;
        private double _sectorSizeRcp = 1.0d / 512.0d;

        [StructLayout(LayoutKind.Sequential)]
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

        [StructLayout(LayoutKind.Sequential)]
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
                        RootNodeOffset = IsOpen ? ReadRootNodeOffset() : 0L,
                        CacheBytes = cacheStats.Bytes,
                        CacheEntries = cacheStats.Entries,
                        PageFaults = _pageFaults,
                        HydratedSectors = _hydratedSectors,
                        EvictedSectors = _evictedSectors,
                        DirtyAppendCount = _dirtyAppendCount,
                        FrameIndex = _frameIndex,
                        IsOpen = IsOpen ? (byte)1 : (byte)0,
                        Tier = _config.DefaultTier
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
            if (!IsValidDatabasePath(path) || !File.Exists(path))
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
            if (!IsValidDatabasePath(path))
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

                _pageFaults++;
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

                _pageFaults++;
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
                        (byte)(flags | PayloadDirtyFlag),
                        out MacroDatabasePayloadHandle handle))
                {
                    return false;
                }

                handle.Flags = (byte)(handle.Flags | PayloadDirtyFlag);
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
                return TryAppendDirtyPayloadLocked(sectorHash);
            }
        }

        private bool TryAppendDirtyPayloadLocked(ulong sectorHash)
        {
            if (!IsOpen || !_dirtyPayloads.IsCreated || !_dirtyPayloads.TryGetValue(sectorHash, out MacroDatabasePayloadHandle dirty))
                return false;

            if (dirty.Pointer == IntPtr.Zero || dirty.ByteLength <= 0 || dirty.ByteLength > _config.MaxPayloadBytes)
                return false;

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
            _dirtyAppendCount++;
            Flush();
            return true;
        }

        public bool TryRepackOffline(string destinationPath)
        {
            lock (_fileGate)
            {
                if (!IsOpen || !IsValidDatabasePath(destinationPath))
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
            lock (_fileGate)
            {
                FlushDirtyPayloadsLocked();
                CloseFileHandles();
                if (_sectorWindowScratch.IsCreated)
                    _sectorWindowScratch.Dispose();
                if (_sectorCoordWindowScratch.IsCreated)
                    _sectorCoordWindowScratch.Dispose();
                if (_asyncHydrateScratch.IsCreated)
                    _asyncHydrateScratch.Dispose();
                if (_blackBox.IsCreated)
                    _blackBox.Dispose();
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
                _hydratedSectors = 0;
                _evictedSectors = 0;
                _dirtyAppendCount = 0;
                _asyncHydrationActive = 0;
                _frameIndex = 0u;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private bool CopyNodePayloadsTo(long nodeOffset, H8MacroDatabaseService target)
        {
            byte* node = NodeAt(nodeOffset);
            if (node == null)
                return false;

            int keyCount = ReadNodeKeyCount(node);
            if ((uint)keyCount > H8MacroDatabaseFileFormat.NodeMaxKeys)
                return false;

            bool isLeaf = IsLeaf(node);
            for (int i = 0; i < keyCount; i++)
            {
                if (!isLeaf && !CopyNodePayloadsTo(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, i), target))
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

            return isLeaf || CopyNodePayloadsTo(H8MacroDatabaseFileFormat.ReadNodeChildOffset(node, keyCount), target);
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

            _pageFaults++;
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

            long appendOffset = H8MacroDatabaseFileFormat.AlignUp(ReadAppendOffset(), 16);
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
            H8MacroDatabaseFileFormat.WriteByte(header, H8MacroDatabaseFileFormat.PayloadFlagsOffset, (byte)(flags & ~PayloadDirtyFlag));

            byte* destination = _basePointer + appendOffset;
            UnsafeUtility.MemCpy(destination, header, H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes);
            UnsafeUtility.MemCpy(destination + H8MacroDatabaseFileFormat.PayloadHeaderSizeBytes, payloadPointer, payloadBytes);
            payloadOffset = appendOffset;
            WriteAppendOffset(H8MacroDatabaseFileFormat.AlignUp(endOffset, 16));
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

                int index = 0;
                while (index < keyCount && sectorHash > H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
                    index++;

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

            int index = 0;
            while (index < keyCount && sectorHash > H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
                index++;

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
                while (index >= 0 && sectorHash < H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
                {
                    H8MacroDatabaseFileFormat.WriteNodeSectorHash(node, index + 1, H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index));
                    H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, index + 1, H8MacroDatabaseFileFormat.ReadNodeFileOffset(node, index));
                    index--;
                }

                H8MacroDatabaseFileFormat.WriteNodeSectorHash(node, index + 1, sectorHash);
                H8MacroDatabaseFileFormat.WriteNodeFileOffset(node, index + 1, payloadOffset);
                WriteNodeKeyCount(node, keyCount + 1);
                return true;
            }

            while (index >= 0 && sectorHash < H8MacroDatabaseFileFormat.ReadNodeSectorHash(node, index))
                index--;
            index++;

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

            return InsertNonFull(H8MacroDatabaseFileFormat.ReadNodeChildOffset(NodeAt(nodeOffset), index), sectorHash, payloadOffset);
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
                CacheEntries = cacheStats.Entries,
                PageFaults = hydratedThisCall,
                PageFaultsTotal = _pageFaults,
                HydratedSectors = _hydratedSectors,
                EvictedSectors = _evictedSectors,
                FrameIndex = _frameIndex,
                Tier = (byte)tier,
                Flags = IsOpen ? (byte)1 : (byte)0
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
            _viewAccessor?.Flush();
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
}
