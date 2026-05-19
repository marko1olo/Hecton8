using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Cold-path fallback and archaeology table for load-shedding priorities.
    /// </summary>
    [Preserve]
    public static class SignalPriorityTable
    {
        private const int MaxPriorities = 64;
        private const int RecordSizeBytes = 8;
        private const int HeaderSizeBytes = 8;
        private const int DefaultCombatPriority = 100;
        private const int DefaultFatalPriority = 255;
        private const int DefaultAudioPriority = 40;
        private const int DefaultVfxPriority = 10;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        // COLD ALLOC: uint[64] - signal priority lane hashes - owner: SignalPriorityTable
        private static readonly uint[] _laneHashes = new uint[MaxPriorities];
        // COLD ALLOC: int[64] - signal priority values - owner: SignalPriorityTable
        private static readonly int[] _priorities = new int[MaxPriorities];
        private static int _count;
        private static int _initialized;
        private static int _fallbackUsed;

        /// <summary>True when hardcoded priorities were used because archaeology failed.</summary>
        public static bool FallbackUsed => _fallbackUsed != 0;

        /// <summary>Active priority row count.</summary>
        public static int Count => _count;

        /// <summary>Initializes the priority table from archived OSHINO binaries or deterministic fallback rows.</summary>
        public static void InitializeFromDisk()
        {
            if (_initialized != 0)
                return;

            _count = 0;
            _fallbackUsed = 0;
            if (!TryLoadFromArchaeology())
                ConstructFallbackSignalPriorities();

            _initialized = 1;
        }

        /// <summary>Builds safe fallback signal priorities when legacy binaries are absent or corrupt.</summary>
        public static void ConstructFallbackSignalPriorities()
        {
            _count = 0;
            UpsertPriority(ComputeLabelHash(nameof(KillSwitchSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(SystemGlitchSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(PlayerFatalPressureSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(CombatDamageSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(SignalWardenMockDamageSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(WakeRequestSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(CrashTelemetrySignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(InputStateSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(AcousticPingSignal)), DefaultAudioPriority);
            UpsertPriority(ComputeLabelHash(nameof(MovementAcousticSignal)), DefaultAudioPriority);
            UpsertPriority(ComputeLabelHash(nameof(BulletTimeVisualSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(CameraJuiceImpactSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(DebrisSpawnSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(MockPlayerFootstepSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(VisualFlareSignal)), DefaultVfxPriority);
            _fallbackUsed = 1;
        }

        /// <summary>Returns the priority for a lane hash, or zero when no row exists.</summary>
        public static int GetPriority(uint laneHash)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_laneHashes[i] == laneHash)
                    return _priorities[i];
            }

            return 0;
        }

        /// <summary>Applies a priority row without allocating. Used by CSV hot-swap and binary archaeology.</summary>
        public static void UpsertPriority(uint laneHash, int priority)
        {
            if (laneHash == 0u)
                return;

            int sanitized = math.clamp(priority, 0, 255);
            for (int i = 0; i < _count; i++)
            {
                if (_laneHashes[i] != laneHash)
                    continue;

                _priorities[i] = sanitized;
                return;
            }

            if (_count >= MaxPriorities)
                return;

            _laneHashes[_count] = laneHash;
            _priorities[_count] = sanitized;
            _count++;
        }

        private static bool TryLoadFromArchaeology()
        {
            bool loaded = false;
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return false;

            string projectRoot = Directory.GetParent(dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            loaded |= TryLoadDirectory(Path.Combine(projectRoot, "Docs", "Archive"));
            loaded |= TryLoadDirectory(Path.Combine(dataPath, "StreamingAssets"));
            return loaded && _count > 0;
        }

        private static bool TryLoadDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            bool loaded = false;
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "event_definitions_*.h8bin", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return false;
            }

            for (int i = 0; i < files.Length; i++)
                loaded |= TryLoadFile(files[i]);

            return loaded;
        }

        private static bool TryLoadFile(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < HeaderSizeBytes + RecordSizeBytes)
                        return false;

                    Span<byte> header = stackalloc byte[HeaderSizeBytes];
                    if (stream.Read(header) != HeaderSizeBytes)
                        return false;

                    int recordCount = math.min((int)((stream.Length - HeaderSizeBytes) / RecordSizeBytes), MaxPriorities);
                    Span<byte> record = stackalloc byte[RecordSizeBytes];
                    for (int i = 0; i < recordCount; i++)
                    {
                        if (stream.Read(record) != RecordSizeBytes)
                            break;

                        uint laneHash = ReadUInt32LittleEndian(record, 0);
                        int priority = unchecked((int)ReadUInt32LittleEndian(record, 4));
                        UpsertPriority(laneHash, priority);
                    }

                    return recordCount > 0;
                }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            return
                (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private static uint ComputeLabelHash(string label)
        {
            uint hash = FnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= FnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }

    /// <summary>
    /// Designer-authored signal coalescing and frame-cap tuning row. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SignalTuningProfile
    {
        [FieldOffset(0)] public uint LaneHash;
        [FieldOffset(4)] public int MinFrameSignals;
        [FieldOffset(8)] public int MaxFrameSignals;
        [FieldOffset(12)] public float CoalescingRadiusMeters;
        [FieldOffset(16)] public int Priority;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved0;
    }

    /// <summary>
    /// Vault-backed table for cold signal_tuning_profiles.csv overrides.
    /// </summary>
    [Preserve]
    public static class SignalTuningTable
    {
        private const int MaxProfiles = 64;
        private const int CsvScratchBytes = 8192;
        private const float DefaultCoalescingRadiusMeters = 1f;
        private const BufferID ProfileBufferId = (BufferID)73040;
        private const BufferID ProfileCountBufferId = (BufferID)73041;
        private const BufferID CsvScratchBufferId = (BufferID)73042;

        private static IDataVault _vault;
        private static VaultBufferHandle<SignalTuningProfile> _profilesHandle;
        private static VaultBufferHandle<int> _countHandle;
        private static VaultBufferHandle<byte> _csvScratchHandle;
        private static NativeArray<SignalTuningProfile> _profiles;
        private static NativeArray<int> _count;
        private static NativeArray<byte> _csvScratch;
        private static int _initialized;

        /// <summary>Initializes the unmanaged tuning DTO table from the global vault.</summary>
        public static void Initialize(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_initialized != 0 && ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            _profilesHandle = vault.GetBufferHandle<SignalTuningProfile>(
                ProfileBufferId,
                MaxProfiles,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _countHandle = vault.GetBufferHandle<int>(
                ProfileCountBufferId,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                CsvScratchBufferId,
                CsvScratchBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);

            _profiles = _profilesHandle.Resolve(vault);
            _count = _countHandle.Resolve(vault);
            _csvScratch = _csvScratchHandle.Resolve(vault);
            if (!_profiles.IsCreated || _profiles.Length < MaxProfiles || !_count.IsCreated || _count.Length < 1)
            {
                _profiles = default;
                _count = default;
                _csvScratch = default;
                _initialized = 0;
                return;
            }

            _initialized = 1;
            _count[0] = 0;
            UpsertProfile(ComputeLabelHash(nameof(AcousticPingSignal)), 16, 128, DefaultCoalescingRadiusMeters, 40);
            UpsertProfile(ComputeLabelHash(nameof(CombatDamageSignal)), 16, 128, DefaultCoalescingRadiusMeters, 100);
        }

        /// <summary>Returns the resolved scratch buffer used by the zero-string CSV parser.</summary>
        public static bool TryGetCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            if (_initialized == 0 || !_csvScratch.IsCreated)
                return false;

            scratch = _csvScratch;
            return true;
        }

        /// <summary>Reads a tuning profile by stable lane hash without touching GlobalRegistry.</summary>
        public static bool TryGetProfile(uint laneHash, out SignalTuningProfile profile)
        {
            profile = default;
            if (laneHash == 0u || _initialized == 0 || !_profiles.IsCreated || !_count.IsCreated)
                return false;

            int count = math.clamp(_count[0], 0, math.min(MaxProfiles, _profiles.Length));
            for (int i = 0; i < count; i++)
            {
                SignalTuningProfile candidate = _profiles[i];
                if (candidate.LaneHash != laneHash)
                    continue;

                profile = candidate;
                return true;
            }

            return false;
        }

        /// <summary>Applies or replaces a tuning profile in the vault table.</summary>
        public static bool UpsertProfile(uint laneHash, int minFrameSignals, int maxFrameSignals, float coalescingRadiusMeters, int priority)
        {
            if (laneHash == 0u || _initialized == 0 || !_profiles.IsCreated || !_count.IsCreated)
                return false;

            int safeMin = math.clamp(minFrameSignals, 1, 4096);
            int safeMax = math.clamp(maxFrameSignals, safeMin, 4096);
            float safeRadius = math.max(0.0001f, math.isfinite(coalescingRadiusMeters) ? coalescingRadiusMeters : DefaultCoalescingRadiusMeters);
            int safePriority = math.clamp(priority, 0, 255);
            int count = math.clamp(_count[0], 0, math.min(MaxProfiles, _profiles.Length));
            for (int i = 0; i < count; i++)
            {
                SignalTuningProfile existing = _profiles[i];
                if (existing.LaneHash != laneHash)
                    continue;

                existing.MinFrameSignals = safeMin;
                existing.MaxFrameSignals = safeMax;
                existing.CoalescingRadiusMeters = safeRadius;
                existing.Priority = safePriority;
                _profiles[i] = existing;
                SignalPriorityTable.UpsertPriority(laneHash, safePriority);
                return true;
            }

            if (count >= MaxProfiles)
                return false;

            _profiles[count] = new SignalTuningProfile
            {
                LaneHash = laneHash,
                MinFrameSignals = safeMin,
                MaxFrameSignals = safeMax,
                CoalescingRadiusMeters = safeRadius,
                Priority = safePriority
            };
            _count[0] = count + 1;
            SignalPriorityTable.UpsertPriority(laneHash, safePriority);
            return true;
        }

        internal static uint ComputeLabelHash(ReadOnlySpan<byte> label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            for (int i = 0; i < label.Length; i++)
            {
                byte c = label[i];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;

                hash ^= c;
                hash *= fnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ComputeLabelHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }

    /// <summary>
    /// Cold allocation-free parser for StreamingAssets/signal_tuning_profiles.csv.
    /// Expected columns: signal,min_frame,max_frame,coalescing_radius,priority.
    /// </summary>
    [Preserve]
    public static class SignalTuningCsvHotSwap
    {
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const byte Period = (byte)'.';

        /// <summary>Loads the default signal tuning CSV if the vault and file are available.</summary>
        public static bool TryLoadDefault()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return false;

            string path = Path.Combine(dataPath, "StreamingAssets", "signal_tuning_profiles.csv");
            return TryLoad(path);
        }

        /// <summary>Loads a signal tuning CSV into vault-backed DTO rows.</summary>
        public static unsafe bool TryLoad(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            IDataVault vault = GlobalRegistry.DataVault;
            SignalTuningTable.Initialize(vault);
            if (!SignalTuningTable.TryGetCsvScratch(out NativeArray<byte> scratch) || !scratch.IsCreated)
                return false;

            int bytesRead;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Span<byte> bytes = new Span<byte>(scratch.GetUnsafePtr(), scratch.Length);
                    bytesRead = stream.Read(bytes);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return Parse(new ReadOnlySpan<byte>(scratch.GetUnsafeReadOnlyPtr(), bytesRead));
        }

        private static bool Parse(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int rowStart = 0;
            while (rowStart < bytes.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < bytes.Length && bytes[rowEnd] != LineFeed && bytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                if (TryParseRow(bytes.Slice(rowStart, rowEnd - rowStart)))
                    changed = true;

                rowStart = rowEnd + 1;
                while (rowStart < bytes.Length && (bytes[rowStart] == LineFeed || bytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row)
        {
            if (row.Length <= 0 || row[0] == (byte)'#')
                return false;

            int first = IndexOf(row, Comma, 0);
            int second = IndexOf(row, Comma, first + 1);
            int third = IndexOf(row, Comma, second + 1);
            int fourth = IndexOf(row, Comma, third + 1);
            if (first <= 0 || second <= first || third <= second || fourth <= third)
                return false;

            ReadOnlySpan<byte> name = Trim(row.Slice(0, first));
            if (!TryResolveLaneHash(name, out uint laneHash))
                return false;

            if (!TryParseInt(Trim(row.Slice(first + 1, second - first - 1)), out int minSignals) ||
                !TryParseInt(Trim(row.Slice(second + 1, third - second - 1)), out int maxSignals) ||
                !TryParseFloat(Trim(row.Slice(third + 1, fourth - third - 1)), out float radiusMeters) ||
                !TryParseInt(Trim(row.Slice(fourth + 1)), out int priority))
            {
                return false;
            }

            return SignalTuningTable.UpsertProfile(laneHash, minSignals, maxSignals, radiusMeters, priority);
        }

        private static bool TryResolveLaneHash(ReadOnlySpan<byte> name, out uint laneHash)
        {
            laneHash = 0u;
            if (name.Length <= 0)
                return false;

            if (TryParseUInt(name, out uint parsed))
            {
                laneHash = parsed;
                return laneHash != 0u;
            }

            laneHash = SignalTuningTable.ComputeLabelHash(name);
            return laneHash != 0u;
        }

        private static int IndexOf(ReadOnlySpan<byte> bytes, byte target, int start)
        {
            if (start < 0)
                return -1;

            for (int i = start; i < bytes.Length; i++)
            {
                if (bytes[i] == target)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length;
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;
            return bytes.Slice(start, end - start);
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            if (!TryParseUInt(bytes, out uint parsed))
                return false;

            value = unchecked((int)math.min(parsed, int.MaxValue));
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> bytes, out uint value)
        {
            value = 0u;
            if (bytes.Length <= 0)
                return false;

            int index = 0;
            bool hex = false;
            if (bytes.Length > 2 && bytes[0] == (byte)'0' && (bytes[1] == (byte)'x' || bytes[1] == (byte)'X'))
            {
                hex = true;
                index = 2;
            }

            for (; index < bytes.Length; index++)
            {
                byte c = bytes[index];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(c - (byte)'A' + 10);
                else
                    return false;

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length <= 0)
                return false;

            uint whole = 0u;
            uint fraction = 0u;
            uint fractionScale = 1u;
            bool afterPeriod = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == Period && !afterPeriod)
                {
                    afterPeriod = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                uint digit = (uint)(c - (byte)'0');
                if (afterPeriod)
                {
                    fraction = (fraction * 10u) + digit;
                    fractionScale *= 10u;
                }
                else
                {
                    whole = (whole * 10u) + digit;
                }
            }

            value = whole + (fractionScale > 1u ? fraction / (float)fractionScale : 0f);
            return math.isfinite(value);
        }
    }

    /// <summary>
    /// Fixed black-box row for signal-bus throughput snapshots. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalTelemetryFrame
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TotalPushedSignals;
        [FieldOffset(8)] public uint PeakSignalsPerFrame;
        [FieldOffset(12)] public uint CoalescedSignals;
        [FieldOffset(16)] public uint DroppedSignals;
        [FieldOffset(20)] public uint CorruptedSignals;
        [FieldOffset(24)] public uint ActiveLaneCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint GlobalQualityMilli;
        [FieldOffset(36)] public uint SystemStressMilli;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// Dedicated 300-frame signal-bus black box.
    /// </summary>
    [Preserve]
    public static class SignalTelemetryRingBuffer
    {
        private const int Capacity = 300;
        private const int HeaderSizeBytes = 16;
        private const uint DumpMagic0 = 0x48454354u; // HECT
        private const uint DumpMagic1 = 0x4F4E3800u; // ON8\0
        private const string DumpPath = "Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin";
        private const BufferID SignalTelemetryRingBufferId = (BufferID)73038;
        private const BufferID SignalTelemetryCursorBufferId = (BufferID)73039;
        private const SystemID OwnerSystemId = SystemID.CoreDiagnostics;

        private static IDataVault _vault;
        private static VaultBufferHandle<SignalTelemetryFrame> _ringHandle;
        private static VaultBufferHandle<int> _cursorHandle;
        private static int _initialized;

        /// <summary>Initializes the vault-backed black-box ring.</summary>
        public static void Initialize()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (_initialized != 0 && ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            _ringHandle = vault.GetBufferHandle<SignalTelemetryFrame>(
                SignalTelemetryRingBufferId,
                Capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _cursorHandle = vault.GetBufferHandle<int>(
                SignalTelemetryCursorBufferId,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            NativeArray<int> cursor = _cursorHandle.Resolve(vault);
            if (cursor.IsCreated && cursor.Length > 0)
                cursor[0] = 0;

            _initialized = 1;
        }

        /// <summary>Releases cached vault handles. GlobalDataVault owns backing memory.</summary>
        public static void Dispose()
        {
            _vault = null;
            _ringHandle = default;
            _cursorHandle = default;
            _initialized = 0;
        }

        /// <summary>Writes one black-box row. Call cadence is owned by GlobalSignals.</summary>
        public static void ReportFrame(
            int frame,
            int totalPushedSignals,
            int peakSignals,
            int coalescedSignals,
            int droppedSignals,
            int corruptedSignals,
            int activeLaneCount,
            int globalQualityMilli,
            int systemStressMilli)
        {
            if (!TryResolveRing(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor))
                return;

            int index = math.clamp(cursor[0], 0, Capacity - 1);
            ring[index] = new SignalTelemetryFrame
            {
                Frame = unchecked((uint)math.max(0, frame)),
                TotalPushedSignals = unchecked((uint)math.max(0, totalPushedSignals)),
                PeakSignalsPerFrame = unchecked((uint)math.max(0, peakSignals)),
                CoalescedSignals = unchecked((uint)math.max(0, coalescedSignals)),
                DroppedSignals = unchecked((uint)math.max(0, droppedSignals)),
                CorruptedSignals = unchecked((uint)math.max(0, corruptedSignals)),
                ActiveLaneCount = unchecked((uint)math.max(0, activeLaneCount)),
                Flags = (droppedSignals > 0 ? 1u : 0u) |
                        (coalescedSignals > 0 ? 2u : 0u) |
                        (corruptedSignals > 0 ? 4u : 0u),
                GlobalQualityMilli = unchecked((uint)math.clamp(globalQualityMilli, 0, 1000)),
                SystemStressMilli = unchecked((uint)math.clamp(systemStressMilli, 0, 1000))
            };

            cursor[0] = index + 1 >= Capacity ? 0 : index + 1;
        }

        /// <summary>Dumps the full signal black-box ring to Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin.</summary>
        public static bool DumpToDisk()
        {
            if (!TryResolveRing(out NativeArray<SignalTelemetryFrame> ring, out _))
                return false;

            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(root))
                    return false;

                string path = Path.Combine(root, DumpPath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[HeaderSizeBytes];
                    WriteUInt32LittleEndian(header, 0, DumpMagic0);
                    WriteUInt32LittleEndian(header, 4, DumpMagic1);
                    WriteUInt32LittleEndian(header, 8, Capacity);
                    WriteUInt32LittleEndian(header, 12, unchecked((uint)UnsafeUtility.SizeOf<SignalTelemetryFrame>()));
                    stream.Write(header);
                    unsafe
                    {
                        byte* ptr = (byte*)ring.GetUnsafeReadOnlyPtr();
                        int byteCount = Capacity * UnsafeUtility.SizeOf<SignalTelemetryFrame>();
                        stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                    }
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

        /// <summary>Copies the current vault-backed telemetry ring into an editor/runtime diagnostic buffer.</summary>
        public static int CopyFrames(NativeArray<SignalTelemetryFrame> destination)
        {
            if (!destination.IsCreated || !TryResolveRing(out NativeArray<SignalTelemetryFrame> ring, out _))
                return 0;

            int count = math.min(destination.Length, Capacity);
            for (int i = 0; i < count; i++)
                destination[i] = ring[i];

            return count;
        }

        private static void WriteUInt32LittleEndian(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static bool TryResolveRing(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _vault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (_initialized == 0 || !_ringHandle.IsCreated || !_cursorHandle.IsCreated || !ReferenceEquals(_vault, vault))
                Initialize();

            vault = _vault;
            if (vault == null || !_ringHandle.IsCreated || !_cursorHandle.IsCreated)
                return false;

            ring = _ringHandle.Resolve(vault);
            cursor = _cursorHandle.Resolve(vault);
            if (!ring.IsCreated || ring.Length < Capacity || !cursor.IsCreated || cursor.Length < 1)
            {
                ring = default;
                cursor = default;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Cold-path CSV hot swap for signal priority overrides.
    /// </summary>
    [Preserve]
    public static class SignalPriorityCsvHotSwap
    {
        private const int ScratchBytes = 4096;
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';

        // COLD ALLOC: byte[4096] - priority CSV parser scratch - owner: SignalPriorityCsvHotSwap
        private static readonly byte[] _scratch = new byte[ScratchBytes];

        /// <summary>Loads signal_priorities.csv into the runtime priority table without per-row managed objects.</summary>
        public static bool TryLoad(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    bytesRead = stream.Read(_scratch, 0, _scratch.Length);
            }
            catch (IOException)
            {
                return false;
            }

            return Parse(_scratch, bytesRead);
        }

        private static bool Parse(byte[] bytes, int length)
        {
            if (bytes == null || length <= 0)
                return false;

            bool changed = false;
            int rowStart = 0;
            while (rowStart < length)
            {
                int rowEnd = rowStart;
                while (rowEnd < length && bytes[rowEnd] != LineFeed && bytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                int comma = rowStart;
                while (comma < rowEnd && bytes[comma] != Comma)
                    comma++;

                if (comma > rowStart &&
                    comma + 1 < rowEnd &&
                    TryParseUInt(bytes, rowStart, comma - rowStart, out uint laneHash) &&
                    TryParseInt(bytes, comma + 1, rowEnd - comma - 1, out int priority))
                {
                    SignalPriorityTable.UpsertPriority(laneHash, priority);
                    changed = true;
                }

                rowStart = rowEnd + 1;
                while (rowStart < length && (bytes[rowStart] == LineFeed || bytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool TryParseInt(byte[] bytes, int start, int length, out int value)
        {
            value = 0;
            if (!TryParseUInt(bytes, start, length, out uint parsed))
                return false;

            value = unchecked((int)math.min(parsed, int.MaxValue));
            return true;
        }

        private static bool TryParseUInt(byte[] bytes, int start, int length, out uint value)
        {
            value = 0u;
            if (length <= 0)
                return false;

            int index = start;
            int end = start + length;
            bool hex = false;
            if (index + 1 < end && bytes[index] == (byte)'0' && (bytes[index + 1] == (byte)'x' || bytes[index + 1] == (byte)'X'))
            {
                hex = true;
                index += 2;
            }

            for (; index < end; index++)
            {
                byte c = bytes[index];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;

                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(c - (byte)'A' + 10);
                else
                    return false;

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
            }

            return true;
        }
    }

    /// <summary>Implemented by signal payloads that target a single entity id.</summary>
    public interface IEntityAddressedSignal
    {
        uint EntityId { get; }
    }

    /// <summary>Alive-mask filter compatible with the DataVault 64-entity tombstone mask convention.</summary>
    public struct EntityAliveMaskSignalFilter<T> : ISignalSnapshotFilter<T>
        where T : unmanaged, ISignal, IEntityAddressedSignal
    {
        public ulong AliveMask;

        public bool Keep(in T signal)
        {
            int bit = (int)(signal.EntityId & 63u);
            return (AliveMask & (1UL << bit)) != 0UL;
        }
    }

    /// <summary>Cold facade for applying caller-owned DataVault liveness masks to current snapshots.</summary>
    public static class SignalGhostFiltering
    {
        public static int ApplyAliveMask<T>(ulong aliveMask)
            where T : unmanaged, ISignal, IEntityAddressedSignal
        {
            EntityAliveMaskSignalFilter<T> filter = default;
            filter.AliveMask = aliveMask;
            return SignalBus<T>.FilterSnapshot(filter);
        }
    }

    /// <summary>Mock footstep payload used to prove blind signal splicing without external agents. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct MockPlayerFootstepSignal : ISignal, IEntityAddressedSignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Intensity01;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public FixedString64Bytes SurfaceName;
        [FieldOffset(112)] public byte Flags;
        [FieldOffset(113)] private byte _pad0;
        [FieldOffset(114)] private byte _pad1;
        [FieldOffset(115)] private byte _pad2;
        [FieldOffset(116)] private byte _pad3;
        [FieldOffset(117)] private byte _pad4;
        [FieldOffset(118)] private byte _pad5;
        [FieldOffset(119)] private byte _pad6;
        [FieldOffset(120)] private byte _pad7;
        [FieldOffset(121)] private byte _pad8;
        [FieldOffset(122)] private byte _pad9;
        [FieldOffset(123)] private byte _pad10;
        [FieldOffset(124)] private byte _pad11;
        [FieldOffset(125)] private byte _pad12;
        [FieldOffset(126)] private byte _pad13;
        [FieldOffset(127)] private byte _pad14;

        uint IEntityAddressedSignal.EntityId => EntityId;
    }

    /// <summary>Signal Warden damage payload used when combat/physics producers are absent. Size: 48 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SignalWardenMockDamageSignal : ISignal, IEntityAddressedSignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Damage;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] private byte _pad0;
        [FieldOffset(46)] private byte _pad1;
        [FieldOffset(47)] private byte _pad2;

        uint IEntityAddressedSignal.EntityId => EntityId;
    }

    /// <summary>Mock high-frequency rock collision input for the aggregation kernel. Size: 48 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct MockRockCollisionSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint SectorHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
        [FieldOffset(40)] private byte _pad3;
        [FieldOffset(41)] private byte _pad4;
        [FieldOffset(42)] private byte _pad5;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
    }

    /// <summary>Coalesced collision payload emitted after redundant rock impacts are merged. Size: 48 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct MacroCollisionSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint SectorHash;
        [FieldOffset(32)] public uint Count;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public byte Flags;
        [FieldOffset(41)] private byte _pad0;
        [FieldOffset(42)] private byte _pad1;
        [FieldOffset(43)] private byte _pad2;
        [FieldOffset(44)] private byte _pad3;
        [FieldOffset(45)] private byte _pad4;
        [FieldOffset(46)] private byte _pad5;
        [FieldOffset(47)] private byte _pad6;
    }

    /// <summary>
    /// Burst aggregation kernel for blind high-frequency collision compression.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockRockCollisionAggregationJob : IJob
    {
        [ReadOnly] public NativeArray<MockRockCollisionSignal> Input;
        public NativeQueue<MacroCollisionSignal>.ParallelWriter Output;
        public uint Frame;

        public void Execute()
        {
            if (!Input.IsCreated || Input.Length <= 0)
                return;

            MockRockCollisionSignal first = Input[0];
            double3 anchor = first.Aup;
            if (!math.all(math.isfinite(anchor)))
                return;

            float magnitude = 0f;
            uint count = 0u;
            uint sectorHash = first.SectorHash;
            for (int i = 0; i < Input.Length; i++)
            {
                MockRockCollisionSignal signal = Input[i];
                if (signal.SectorHash != sectorHash || !math.all(math.isfinite(signal.Aup)))
                    continue;

                double3 deltaAup = signal.Aup - anchor;
                float3 localDelta = new float3((float)deltaAup.x, (float)deltaAup.y, (float)deltaAup.z);
                float distanceSq = math.lengthsq(localDelta);
                if (distanceSq > 4f)
                    continue;

                magnitude += math.max(0f, signal.Magnitude);
                count++;
            }

            if (count == 0u)
                return;

            MacroCollisionSignal macro = default;
            macro.Aup = anchor;
            macro.Magnitude = magnitude;
            macro.SectorHash = sectorHash;
            macro.Count = count;
            macro.Frame = Frame;
            macro.Flags = 1;
            Output.Enqueue(macro);
        }
    }

    /// <summary>
    /// IL2CPP AOT preservation anchor for closed generic signal lanes.
    /// </summary>
    [Preserve]
    public static class SignalBusAotPreserve
    {
        /// <summary>Cold method referenced by build/link preservation scans.</summary>
        [Preserve]
        public static void PreserveGenerics()
        {
            PreserveLane<CombatDamageSignal>();
            PreserveLane<AcousticPingSignal>();
            PreserveLane<SignalWardenMockDamageSignal>();
            PreserveLane<MockPlayerFootstepSignal>();
            PreserveLane<MacroCollisionSignal>();
            PreserveLane<WakeRequestSignal>();
            PreserveLane<WaterlineBreachSignal>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PreserveLane<T>()
            where T : unmanaged, ISignal
        {
            GC.KeepAlive(typeof(SignalBus<T>));
        }
    }
}
